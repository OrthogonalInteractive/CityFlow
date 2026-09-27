#nullable enable

using System;
using System.Collections;
using System.Linq;
using CityFlow.Application.Connections;
using CityFlow.Application.Routing;
using CityFlow.Application.UseCases;
using CityFlow.Composition;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Presentation.Connections;
using CityFlow.Presentation.Overview;
using CityFlow.Presentation.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using VContainer;
using Object = UnityEngine.Object;

namespace CityFlow.Tests.PlayMode
{
    public sealed class LineRevealTests
    {
        private Mouse[] mice = Array.Empty<Mouse>();
        private static T Resolve<T>() => Object.FindAnyObjectByType<CityFlowLifetimeScope>().Container.Resolve<T>();
        private static NodeConnectionController Controller => Object.FindAnyObjectByType<NodeConnectionController>();
        private static ValidationCityView City => Object.FindAnyObjectByType<ValidationCityView>();
        private static OverviewController Overview => Object.FindAnyObjectByType<OverviewController>();

        [UnitySetUp] public IEnumerator Load()
        {
            mice = InputSystem.devices.OfType<Mouse>().Where(m => m.enabled).ToArray();
            foreach (var mouse in mice) InputSystem.DisableDevice(mouse);
            yield return SceneManager.LoadSceneAsync("WiringLab"); yield return null;
            Object.FindAnyObjectByType<SimulationDriver>().enabled = false;
            Resolve<FlowSimulation>().SetPaused(true);
        }

        [TearDown] public void RestoreInput()
        { foreach (var mouse in mice) if (mouse.added) InputSystem.EnableDevice(mouse); }

        private static void Begin(string source, string target)
        {
            Overview.Select(OverviewTarget.Node(source)); Controller.BeginSelected(); Controller.FocusTarget(target);
            Assert.That(Controller.IsNode360, Is.True);
        }
        private static LineRenderer Body(LineSnapshot line) =>
            GameObject.Find($"Line {line.Id}: {line.SourceId} -> {line.DestinationId}").GetComponent<LineRenderer>();
        private static Vector3[] Points(LineRenderer line)
        { var points = new Vector3[line.positionCount]; line.GetPositions(points); return points; }
        private static float Length(LineRenderer line) => Points(line).Zip(Points(line).Skip(1), Vector3.Distance).Sum();
        private static LineRenderer[] Arrows(LineRenderer body) => Object.FindObjectsByType<LineRenderer>()
            .Where(r => r.name == "Direction" && r.sharedMaterial == body.sharedMaterial).ToArray();

        [UnityTest] public IEnumerator ConfirmedLineRevealsFromSourceAlongBendsWhilePaused()
        {
            var camera = Camera.main; var home = Overview.CaptureView();
            Begin("S1", "BLUE");
            var preview = Resolve<LinePreviewService>();
            var original = preview.Current!.Points;
            preview.InsertPoint(0, (original[0] + original[1]) * 0.5f + Vector3.back * 5);
            Assert.That(preview.Current!.CanConfirm, Is.True);
            yield return null; yield return null;
            var button = Object.FindAnyObjectByType<UIDocument>().rootVisualElement.Q<Button>("candidate-option-BLUE");
            UiPointer.Click(button);
            Assert.That(Controller.IsNode360, Is.False);
            Assert.That(camera.transform.position, Is.EqualTo(home.Position));
            var network = Resolve<FlowNetwork>(); var snapshot = network.Snapshot(); var line = snapshot.Lines.Single();
            double elapsed = Resolve<FlowSimulation>().ElapsedSeconds;
            City.AdvanceLineReveals(0);
            var body = Body(line);
            Assert.That(Length(body), Is.Zero.Within(0.001), "A new Line must start at its source, not appear at full length.");
            Assert.That(Arrows(body).All(r => !r.enabled), Is.True, "Arrows ahead of the reveal must remain hidden.");
            City.AdvanceLineReveals(0.3f);
            float drawn = Length(body);
            Assert.That(drawn, Is.GreaterThan(0).And.LessThan(line.Route.Length));
            Assert.That(Vector3.Distance(body.GetPosition(body.positionCount - 1), line.Route.PositionAt(drawn) + Vector3.up * 0.2f), Is.LessThan(0.001f));
            City.AdvanceLineReveals(2);
            Assert.That(Points(body), Is.EqualTo(line.Route.Points.Select(p => p + Vector3.up * 0.2f)));
            Assert.That(Arrows(body).All(r => r.enabled), Is.True);
            Assert.That(network.Snapshot(), Is.SameAs(snapshot));
            Assert.That(Resolve<FlowSimulation>().ElapsedSeconds, Is.EqualTo(elapsed));
            Assert.That(Resolve<FlowSimulation>().IsPaused, Is.True);
        }

        [UnityTest] public IEnumerator ElevatedLineRevealsVerticalThenHorizontalThenDownwardSegments()
        {
            yield return SceneManager.LoadSceneAsync("HeightLab"); yield return null;
            Object.FindAnyObjectByType<SimulationDriver>().enabled = false;
            Resolve<FlowSimulation>().SetPaused(true);
            Begin("R2", "R3");
            Assert.That(Resolve<LinePreviewService>().SetRouteHeight(9), Is.True);
            Assert.That(Resolve<ConnectionSession>().Confirm(), Is.EqualTo(ConnectionFailure.None));
            var snapshot = Resolve<FlowNetwork>().Snapshot(); var line = snapshot.Lines.Single();
            City.AdvanceLineReveals(0.05f);
            var body = Body(line);
            Assert.That(body.positionCount, Is.EqualTo(2));
            Assert.That(body.GetPosition(1).x, Is.EqualTo(line.Route.Points[0].x));
            Assert.That(body.GetPosition(1).y, Is.GreaterThan(0).And.LessThan(9));
            Assert.That(Arrows(body).All(r => !r.enabled), Is.True);
            City.AdvanceLineReveals(0.45f);
            Assert.That(body.positionCount, Is.EqualTo(3));
            Assert.That(body.GetPosition(1), Is.EqualTo(line.Route.Points[1]));
            Assert.That(body.GetPosition(2).y, Is.EqualTo(9));
            Assert.That(Arrows(body).Count(r => r.enabled), Is.EqualTo(2));
            City.AdvanceLineReveals(0.35f);
            Assert.That(body.positionCount, Is.EqualTo(4));
            Assert.That(body.GetPosition(3).x, Is.EqualTo(line.Route.Points[3].x));
            Assert.That(body.GetPosition(3).y, Is.GreaterThan(0).And.LessThan(9));
            City.AdvanceLineReveals(1);
            Assert.That(Points(body), Is.EqualTo(line.Route.Points));
            Assert.That(Resolve<FlowNetwork>().Snapshot(), Is.SameAs(snapshot));
        }

        [UnityTest] public IEnumerator ExistingLinesFailureCancellationAndReentering360NeverReplayTheReveal()
        {
            yield return SceneManager.LoadSceneAsync("Bootstrap"); yield return null;
            Object.FindAnyObjectByType<SimulationDriver>().enabled = false;
            Resolve<FlowSimulation>().SetPaused(true);
            var network = Resolve<FlowNetwork>(); var existing = network.Snapshot().Lines.ToArray();
            Begin("S1", "RED"); Controller.ConfirmTarget("RED");
            Assert.That(Resolve<ConnectionSession>().IsActive, Is.True, "A duplicate connection cannot be confirmed.");
            Controller.CancelSelection(); City.AdvanceLineReveals(0.2f);
            foreach (var line in existing)
                Assert.That(Points(Body(line)), Is.EqualTo(line.Route.Points.Select(p => p + Vector3.up * 0.2f)));
            Begin("R1", "BLUE"); Controller.ConfirmTarget("BLUE");
            var added = network.Snapshot().Lines.Single(l => existing.All(old => old.Id != l.Id));
            City.AdvanceLineReveals(0.2f);
            Assert.That(Length(Body(added)), Is.LessThan(added.Route.Length));
            Begin("R2", "RED");
            Assert.That(Points(Body(added)), Is.EqualTo(added.Route.Points.Select(p => p + Vector3.up * 0.2f)), "A new wiring session immediately shows the complete existing network.");
            Controller.CancelSelection(); City.AdvanceLineReveals(0.1f);
            Assert.That(Points(Body(added)), Is.EqualTo(added.Route.Points.Select(p => p + Vector3.up * 0.2f)));
        }

        [UnityTest] public IEnumerator UndoAndDeletionDuringRevealPreserveTransportAndRemoveTheAnimation()
        {
            Begin("S1", "BLUE"); Controller.ConfirmTarget("BLUE"); City.AdvanceLineReveals(0.1f);
            var network = Resolve<FlowNetwork>(); var removed = network.Snapshot().Lines.Single();
            Controller.UndoConnection(); City.AdvanceLineReveals(0);
            yield return null;
            Assert.That(network.Snapshot().Lines, Is.Empty);
            Assert.That(GameObject.Find($"Line {removed.Id}: S1 -> BLUE"), Is.Null);
            Begin("S1", "BLUE"); Controller.ConfirmTarget("BLUE"); City.AdvanceLineReveals(0.1f);
            var line = network.Snapshot().Lines.Single();
            network.GenerateFlow("S1", FlowColor.Blue); network.RouteWaitingFlows(); network.AdvanceInFlight(0.1);
            var flight = network.Snapshot().Lines.Single().InFlight.Single();
            Assert.That(network.RequestDeletion(line.Id), Is.True);
            City.AdvanceLineReveals(0.1f); yield return null;
            Assert.That(Body(line).enabled, Is.False);
            Assert.That(Points(Body(line)), Is.EqualTo(line.Route.Points.Select(p => p + Vector3.up * 0.2f)));
            Assert.That(network.Snapshot().Lines.Single().InFlight.Single().Flow.Id, Is.EqualTo(flight.Flow.Id));
            Assert.That(network.Snapshot().Lines.Single().InFlight.Single().Distance, Is.EqualTo(flight.Distance));
            Assert.That(network.CancelPending(line.Id), Is.True);
            City.AdvanceLineReveals(0.1f); yield return null;
            Assert.That(Body(line).enabled, Is.True);
            Assert.That(Points(Body(line)), Is.EqualTo(line.Route.Points.Select(p => p + Vector3.up * 0.2f)));
        }
    }
}
