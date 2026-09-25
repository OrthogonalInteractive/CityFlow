#nullable enable

using System;
using System.Collections;
using System.Linq;
using CityFlow.Application.Connections;
using CityFlow.Application.Routing;
using CityFlow.Composition;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Domain.Spatial;
using CityFlow.Presentation.Connections;
using CityFlow.Presentation.Overview;
using CityFlow.Presentation.Rendering;
using CityFlow.Presentation.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using VContainer;
using Object = UnityEngine.Object;

namespace CityFlow.Tests.PlayMode
{
    public sealed class HeightInteractionTests
    {
        private Mouse[] mice = Array.Empty<Mouse>();
        private static T Resolve<T>() => Object.FindAnyObjectByType<CityFlowLifetimeScope>().Container.Resolve<T>();
        private static NodeConnectionController Controller => Object.FindAnyObjectByType<NodeConnectionController>();
        private static VisualElement Root => Object.FindAnyObjectByType<UIDocument>().rootVisualElement;

        [UnitySetUp] public IEnumerator Load()
        {
            mice = InputSystem.devices.OfType<Mouse>().Where(mouse => mouse.enabled).ToArray();
            foreach (var mouse in mice) InputSystem.DisableDevice(mouse);
            yield return SceneManager.LoadSceneAsync("HeightLab");
            yield return null;
            Object.FindAnyObjectByType<SimulationDriver>().enabled = false;
        }

        [TearDown] public void RestoreInput()
        {
            foreach (var mouse in mice) if (mouse.added) InputSystem.EnableDevice(mouse);
        }

        private static void Begin(string from, string to)
        {
            Object.FindAnyObjectByType<OverviewController>().Select(OverviewTarget.Node(from));
            Controller.BeginSelected();
            Controller.FocusTarget(to);
        }

        [UnityTest] public IEnumerator HeightFieldMovesTheWholePlaneAndConfirmedRouteMatchesFlowRendering()
        {
            var network = Resolve<FlowNetwork>();
            var preview = Resolve<LinePreviewService>();
            Begin("R2", "R3");
            yield return null;
            Assert.That(Root.Q<Label>("candidate-detail").text, Does.Contain("LIFT +10"));
            Controller.BeginEditing();
            yield return null;
            Assert.That(Root.Q("connection-panel").resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            var field = Root.Q<FloatField>("route-height");
            Assert.That(field.enabledInHierarchy, Is.True, "Plane height does not require selecting an individual handle.");
            Assert.That(Root.Q<Label>("route-height-range").text, Does.Contain("0–10"));
            Assert.That(preview.Current!.Points.Count, Is.EqualTo(4));
            field.value = 9;
            Assert.That(preview.Current.Points.Skip(1).Take(2).All(p => p.y == 9), Is.True);
            Assert.That(preview.CanMovePoint(1), Is.False, "A Relay lift elbow stays directly above its Node.");
            field.value = 11;
            yield return null;
            Assert.That(Root.Q<Button>("route-apply").enabledSelf, Is.False);
            field.value = 9;
            yield return null;
            Vector3[] edited = preview.Current.Points.ToArray();
            UiPointer.Click(Root.Q<Button>("route-apply"));
            yield return null;
            var line = network.Snapshot().Lines.Single();
            Assert.That(line.Route.Points, Is.EqualTo(edited));
            var renderer = GameObject.Find($"Line {line.Id}: R2 -> R3").GetComponent<LineRenderer>();
            for (int i = 0; i < edited.Length; i++) Assert.That(renderer.GetPosition(i), Is.EqualTo(edited[i]));
            var planner = Resolve<ILineRoutePlanner>();
            NodeDefinition Node(string id) => network.NodeDefinitions.Single(n => n.Id == id);
            var input = planner.Generate(Node("S1"), Node("R2"));
            Assert.That(network.TryConnect("S1", "R2", input.Route!.Points).Succeeded, Is.True);
            Assert.That(network.TryConnect("R3", "BLUE", planner.Generate(Node("R3"), Node("BLUE")).Route!.Points).Succeeded, Is.True);
            var flow = network.GenerateFlow("S1", FlowColor.Blue);
            network.RouteWaitingFlows();
            network.AdvanceInFlight(input.Route.Length / network.Settings.FlowSpeed);
            network.RouteWaitingFlows();
            network.AdvanceInFlight(4.5 / network.Settings.FlowSpeed);
            yield return null;
            Assert.That(GameObject.Find($"FLOW {flow.Id} / Blue").transform.position, Is.EqualTo(Node("R2").Position + Vector3.up * 4.5f));
        }

        [UnityTest] public IEnumerator EnterInHeightFieldDoesNotConfirmTheConnection()
        {
            Begin("R2", "R3"); Controller.BeginEditing();
            yield return null;
            Root.Q<FloatField>("route-height").Focus();
            yield return null;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var old = InputSystem.settings.editorInputBehaviorInPlayMode;
            var background = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter)); yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
                Assert.That(Resolve<ConnectionSession>().IsActive, Is.True);
                Assert.That(Resolve<FlowNetwork>().Snapshot().Lines, Is.Empty);
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.editorInputBehaviorInPlayMode = old;
                InputSystem.settings.backgroundBehavior = background;
            }
        }

        [UnityTest] public IEnumerator VerticalRelayConnectionHasVisibleArrowsAndSupportsOrbitDuringEditing()
        {
            var network = Resolve<FlowNetwork>();
            Vector3 start = network.NodeDefinitions.Single(n => n.Id == "R2").Position;
            Assert.That(network.TryAddNodes(new NodeDefinition[] { new RelayNodeDefinition("UP", start + Vector3.up * 8) }), Is.True);
            Begin("R2", "UP");
            Assert.That(Resolve<ConnectionSession>().Candidates().Single(c => c.Node.Definition.Id == "UP").Distance, Is.EqualTo(8));
            Controller.BeginEditing();
            var before = Camera.main.transform.rotation;
            Object.FindAnyObjectByType<OverviewController>().Orbit(new Vector2(20, 10));
            Assert.That(Camera.main.transform.rotation, Is.Not.EqualTo(before));
            Assert.That(Resolve<ConnectionSession>().Confirm(), Is.EqualTo(ConnectionFailure.None));
            yield return null;
            var arrow = Object.FindObjectsByType<LineRenderer>().Single(r => r.name == "Direction");
            Assert.That(Vector3.Distance(arrow.GetPosition(0), arrow.GetPosition(2)), Is.GreaterThan(1));
        }

        [UnityTest] public IEnumerator LowRelayShowsItsLimitAndFixedHeightEndpointsCannotBeLiftedByTheEditor()
        {
            Begin("R1", "R3");
            yield return null;
            Assert.That(Resolve<LinePreviewService>().Current!.CanConfirm, Is.False);
            Assert.That(Root.Q<Button>("candidate-option-R2").text, Does.Contain("↑10m"));
            var network = Resolve<FlowNetwork>();
            Assert.That(OverviewReadout.Describe(OverviewTarget.Node("R1"), network.Snapshot(), network.Settings), Does.Contain("LIFT +6 m"));
            Controller.CancelSelection(); Begin("S1", "RED"); Controller.BeginEditing();
            yield return null;
            Assert.That(Root.Q<FloatField>("route-height").enabledSelf, Is.False);
            Assert.That(Root.Q<Label>("route-height-range").text, Does.Contain("FIXED Y 0"));
            Assert.That(Resolve<LinePreviewService>().Current!.CanConfirm, Is.True);
        }
    }
}
