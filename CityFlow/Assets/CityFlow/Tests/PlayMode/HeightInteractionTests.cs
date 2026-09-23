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

        [UnityTest] public IEnumerator HeightFieldChangesPreviewAndConfirmedRouteMatchesFlowRendering()
        {
            var container = Object.FindAnyObjectByType<CityFlowLifetimeScope>().Container;
            var network = container.Resolve<FlowNetwork>();
            var session = container.Resolve<ConnectionSession>();
            var preview = container.Resolve<LinePreviewService>();
            var controller = Object.FindAnyObjectByType<NodeConnectionController>();
            var overview = Object.FindAnyObjectByType<OverviewController>();
            var root = Object.FindAnyObjectByType<UIDocument>().rootVisualElement;
            overview.Select(OverviewTarget.Node("S1"));
            controller.BeginSelected();
            controller.FocusTarget("R2");
            yield return null;
            Assert.That(ConnectionReadout.Candidate(session.Candidates().Single(c => c.Node.Definition.Id == "R2")),
                Does.Contain("HEIGHT +24.0 m"));
            controller.BeginEditing();
            yield return null;
            Assert.That(root.Q("connection-panel").resolvedStyle.display, Is.EqualTo(DisplayStyle.None),
                "The candidate panel must not cover manual controls.");
            var points = preview.Current!.Points;
            Object.FindAnyObjectByType<RouteEditView>().InsertAtScreen(Camera.main.WorldToScreenPoint((points[0] + points[1]) * 0.5f));
            yield return null;
            var field = root.Q<FloatField>("route-height");
            Assert.That(field.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(field.enabledInHierarchy, Is.True);
            Vector3 before = preview.Current!.Points[1];
            Assert.That(before.y, Is.EqualTo(12).Within(0.01));
            field.value = 18;
            Assert.That(preview.Current.Points[1], Is.EqualTo(new Vector3(before.x, 18, before.z)));
            field.value = 65;
            yield return null;
            Assert.That(root.Q<Button>("route-apply").enabledSelf, Is.False);
            field.value = 18;
            yield return null;
            Vector3[] edited = preview.Current.Points.ToArray();
            UiPointer.Click(root.Q<Button>("route-apply"));
            yield return null;
            var line = network.Snapshot().Lines.Single();
            Assert.That(line.Route.Points, Is.EqualTo(edited));
            var renderer = GameObject.Find($"Line {line.Id}: S1 -> R2").GetComponent<LineRenderer>();
            for (int i = 0; i < edited.Length; i++) Assert.That(renderer.GetPosition(i), Is.EqualTo(edited[i]));
            var planner = container.Resolve<ILineRoutePlanner>();
            var from = network.NodeDefinitions.Single(n => n.Id == "R2").Position;
            var to = network.NodeDefinitions.Single(n => n.Id == "RED").Position;
            Assert.That(network.TryConnect("R2", "RED", planner.Generate(from, to).Route!.Points).Succeeded, Is.True);
            var flow = network.GenerateFlow("S1", FlowColor.Red);
            network.RouteWaitingFlows();
            network.AdvanceInFlight(line.Route.Length / network.Settings.FlowSpeed / 2);
            yield return null;
            Assert.That(GameObject.Find($"FLOW {flow.Id} / Red").transform.position,
                Is.EqualTo(line.Route.PositionAt(line.Route.Length / 2)));
        }

        [UnityTest] public IEnumerator EnterInHeightFieldDoesNotConfirmTheConnection()
        {
            var container = Object.FindAnyObjectByType<CityFlowLifetimeScope>().Container;
            var session = container.Resolve<ConnectionSession>();
            var preview = container.Resolve<LinePreviewService>();
            var controller = Object.FindAnyObjectByType<NodeConnectionController>();
            Object.FindAnyObjectByType<OverviewController>().Select(OverviewTarget.Node("S1"));
            controller.BeginSelected(); session.SelectTarget("R2"); controller.BeginEditing();
            yield return null;
            var points = preview.Current!.Points;
            Object.FindAnyObjectByType<RouteEditView>().InsertAtScreen(Camera.main.WorldToScreenPoint((points[0] + points[1]) * 0.5f));
            yield return null;
            var field = Object.FindAnyObjectByType<UIDocument>().rootVisualElement.Q<FloatField>("route-height");
            field.Focus();
            yield return null;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var old = InputSystem.settings.editorInputBehaviorInPlayMode;
            var background = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter));
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                Assert.That(session.IsActive, Is.True);
                Assert.That(container.Resolve<FlowNetwork>().Snapshot().Lines, Is.Empty);
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.editorInputBehaviorInPlayMode = old;
                InputSystem.settings.backgroundBehavior = background;
            }
        }

        [UnityTest] public IEnumerator VerticalConnectionsHaveVisibleArrowsAndSupportOrbitDuringEditing()
        {
            var container = Object.FindAnyObjectByType<CityFlowLifetimeScope>().Container;
            var network = container.Resolve<FlowNetwork>();
            Vector3 start = network.NodeDefinitions.Single(n => n.Id == "S1").Position;
            Assert.That(network.TryAddNodes(new NodeDefinition[] { new RelayNodeDefinition("UP", start + Vector3.up * 20) }), Is.True);
            var session = container.Resolve<ConnectionSession>();
            var controller = Object.FindAnyObjectByType<NodeConnectionController>();
            var overview = Object.FindAnyObjectByType<OverviewController>();
            overview.Select(OverviewTarget.Node("S1")); controller.BeginSelected(); controller.FocusTarget("UP");
            Assert.That(session.Candidates().Single(c => c.Node.Definition.Id == "UP").Distance, Is.EqualTo(20));
            controller.BeginEditing();
            var before = Camera.main.transform.rotation;
            overview.Orbit(new Vector2(20, 10));
            Assert.That(Camera.main.transform.rotation, Is.Not.EqualTo(before));
            Assert.That(session.Confirm(), Is.EqualTo(ConnectionFailure.None));
            yield return null;
            var arrow = Object.FindObjectsByType<LineRenderer>().Single(r => r.name == "Direction");
            Assert.That(Vector3.Distance(arrow.GetPosition(0), arrow.GetPosition(2)), Is.GreaterThan(1));
        }
    }
}
