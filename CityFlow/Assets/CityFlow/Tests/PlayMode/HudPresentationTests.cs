#nullable enable

using System.Collections;
using System.Linq;
using CityFlow.Application.Connections;
using CityFlow.Application.UseCases;
using CityFlow.Composition;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Presentation.Connections;
using CityFlow.Presentation.Overview;
using CityFlow.Presentation.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using VContainer;

namespace CityFlow.Tests.PlayMode
{
    public sealed class HudPresentationTests
    {
        [UnitySetUp] public IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("WiringLab"); yield return null;
            Object.FindAnyObjectByType<SimulationDriver>().enabled = false;
        }
        private static void Connect(ConnectionSession session, string from, string to)
        {
            Assert.That(session.Begin(from), Is.True); session.SelectTarget(to);
            Assert.That(session.Confirm(), Is.EqualTo(ConnectionFailure.None));
        }
        [UnityTest] public IEnumerator WaveClockAndPreparationHintRemainAfterTheArrivalNotice()
        {
            var scope = Object.FindAnyObjectByType<CityFlowLifetimeScope>();
            var simulation = scope.Container.Resolve<FlowSimulation>();
            var session = scope.Container.Resolve<ConnectionSession>();
            Connect(session, "S1", "RED"); Connect(session, "S1", "BLUE");
            simulation.Tick(60 - simulation.ElapsedSeconds); simulation.Tick(13); simulation.SetPaused(true);
            yield return null; yield return null;
            var root = Object.FindAnyObjectByType<UIDocument>().rootVisualElement;
            Assert.That(root.Q("line-rows"), Is.Null);
            Assert.That(root.Q<Label>("elapsed-value").text, Is.EqualTo("1:13"));
            Assert.That(root.Q<Label>("wave-value").text, Is.EqualTo("WAVE 2 · NEXT 47s"));
            Assert.That(root.Q<Label>("context-hint").text, Does.Contain("S2").And.Contain("7s"));
            Assert.That(root.Q("wave-notice").resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            string clock = root.Q<Label>("wave-value").text;
            simulation.Tick(100); yield return null;
            Assert.That(root.Q<Label>("wave-value").text, Is.EqualTo(clock));
        }
        [UnityTest] public IEnumerator HoverUsesStructuredSectionsColorInitialsAndATargetLeader()
        {
            var scope = Object.FindAnyObjectByType<CityFlowLifetimeScope>();
            var network = scope.Container.Resolve<FlowNetwork>();
            network.GenerateFlow("S1", FlowColor.Red); network.GenerateFlow("S1", FlowColor.Blue);
            var overview = Object.FindAnyObjectByType<OverviewController>();
            overview.Hover(Camera.main.WorldToScreenPoint(network.NodeDefinitions.Single(n => n.Id == "S1").Position + Vector3.up * 1.4f));
            yield return null; yield return null;
            var root = Object.FindAnyObjectByType<UIDocument>().rootVisualElement;
            Assert.That(root.Q<Label>("hover-title"), Is.Not.Null);
            Assert.That(root.Q<Label>("hover-title").text, Does.Contain("S1"));
            Assert.That(root.Q<Label>("hover-primary").text, Does.Contain("BUFFER 2/10"));
            Assert.That(root.Q<Label>("hover-metrics").text, Does.Contain("OUT ").And.Not.Contain("IN "));
            Assert.That(root.Q("hover-leader").resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(root.Q("hover-buffer").Children().OfType<Label>().Take(2).Select(l => l.text), Is.EqualTo(new[] { "R", "B" }));
            Assert.That(root.Q("node-gauge-S1").Children().OfType<Label>().Take(2).Select(l => l.text), Is.EqualTo(new[] { "R", "B" }));
            overview.Hover(new Vector2(-100, -100)); yield return null;
            Assert.That(root.Q("node-tooltip").resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            Assert.That(root.Q("hover-leader").resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
        }
        [UnityTest] public IEnumerator WaveNoticeAndArrivalMarkersAvoidNodeLabelsAndPauseControls()
        {
            var scope = Object.FindAnyObjectByType<CityFlowLifetimeScope>();
            var session = scope.Container.Resolve<ConnectionSession>();
            Connect(session, "S1", "RED"); Connect(session, "S1", "BLUE");
            var simulation = scope.Container.Resolve<FlowSimulation>(); simulation.Tick(60 - simulation.ElapsedSeconds);
            simulation.SetPaused(true); yield return null; yield return null; yield return null;
            var root = Object.FindAnyObjectByType<UIDocument>().rootVisualElement;
            var notice = root.Q("wave-notice");
            // Camera panning can place a Node underneath the notification's previous position.
            var green = scope.Container.Resolve<FlowNetwork>().NodeDefinitions.Single(n => n.Id == "GREEN");
            var camera = Camera.main;
            Vector3 projected = camera.WorldToScreenPoint(green.Position + Vector3.up * 5);
            Vector2 center = notice.worldBound.center;
            var target = new Vector3(center.x / root.layout.width * Screen.width,
                (1 - center.y / root.layout.height) * Screen.height, projected.z);
            camera.transform.position += camera.ScreenToWorldPoint(projected) - camera.ScreenToWorldPoint(target);
            yield return null; yield return null;
            var markers = root.Q("arrival-markers").Query<Button>().ToList();
            Assert.That(markers.Count, Is.EqualTo(2));
            foreach (var element in markers.Cast<VisualElement>().Append(notice))
            {
                Assert.That(element.worldBound.Overlaps(root.Q(className: "session-controls").worldBound), Is.False);
                foreach (var node in root.Q("node-labels").Children().Where(e => e.resolvedStyle.display != DisplayStyle.None))
                    Assert.That(element.worldBound.Overlaps(node.worldBound), Is.False, element.name + " covers " + node.name);
            }
            Assert.That(markers[0].worldBound.Overlaps(markers[1].worldBound), Is.False);
            Assert.That(markers.All(m => !m.worldBound.Overlaps(notice.worldBound)), Is.True);
        }
        [UnityTest] public IEnumerator CandidateGroupsExcludeSelfAndAllMarkersRemainVisible()
        {
            var scope = Object.FindAnyObjectByType<CityFlowLifetimeScope>();
            var session = scope.Container.Resolve<ConnectionSession>();
            var controller = Object.FindAnyObjectByType<NodeConnectionController>();
            Object.FindAnyObjectByType<OverviewController>().Select(OverviewTarget.Node("S1"));
            controller.BeginSelected(); controller.FocusTarget("BLUE"); yield return null; yield return null; yield return null;
            var root = Object.FindAnyObjectByType<UIDocument>().rootVisualElement;
            Assert.That(root.Query<Label>(className: "candidate-group-title").ToList().Count, Is.GreaterThan(0));
            foreach (var candidate in session.Candidates())
            {
                string id = candidate.Node.Definition.Id;
                Assert.That(id, Is.Not.EqualTo("S1"));
                Assert.That(root.Q("candidate-" + id).resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            }
            var swatch = root.Q("candidate-option-BLUE").Q<Label>(className: "candidate-swatch");
            Assert.That(swatch.text, Is.EqualTo("B"));
            Assert.That(swatch.resolvedStyle.backgroundColor, Is.EqualTo(ValidationCityView.ColorFor(FlowColor.Blue)));
        }
    }
}
