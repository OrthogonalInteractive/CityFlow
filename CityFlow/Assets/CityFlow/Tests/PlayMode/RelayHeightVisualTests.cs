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
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VContainer;
using Object = UnityEngine.Object;

namespace CityFlow.Tests.PlayMode
{
    public sealed class RelayHeightVisualTests
    {
        private Mouse[] mice = Array.Empty<Mouse>();
        private static T Resolve<T>() => Object.FindAnyObjectByType<CityFlowLifetimeScope>().Container.Resolve<T>();
        private static Renderer? Height(string id) => Object.FindObjectsByType<MeshRenderer>()
            .SingleOrDefault(renderer => renderer.name == "Relay height " + id);

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

        [UnityTest] public IEnumerator HologramsSpanActualRelayLimitsIncludingLaterAndElevatedNodes()
        {
            var network = Resolve<FlowNetwork>();
            var stage = Resolve<StageDefinition>();
            // Later additions use the same path as Wave nodes; the elevated Relay is capped by the stage.
            Assert.That(network.TryAddNodes(new NodeDefinition[] {
                new RelayNodeDefinition("TALL", new Vector3(40, 0, 19), maximumRise: 26),
                new RelayNodeDefinition("ELEVATED", new Vector3(52, 24, 30), maximumRise: 22),
                new RelayNodeDefinition("FIXED", new Vector3(50, 0, 35)) }), Is.True);
            yield return null;
            foreach (var node in network.NodeDefinitions)
            {
                var renderer = Height(node.Id);
                float rise = stage.ConnectionCeiling(node) - node.Position.y;
                if (rise == 0) { Assert.That(renderer, Is.Null, node.Id); continue; }
                Assert.That(renderer, Is.Not.Null, node.Id + " must display its usable height.");
                if (renderer == null) continue;
                Assert.That(renderer.bounds.min.y, Is.EqualTo(node.Position.y).Within(0.001f));
                Assert.That(renderer.bounds.max.y, Is.EqualTo(stage.ConnectionCeiling(node)).Within(0.001f));
                Assert.That(renderer.bounds.center.x, Is.EqualTo(node.Position.x).Within(0.001f));
                Assert.That(renderer.bounds.center.z, Is.EqualTo(node.Position.z).Within(0.001f));
                Assert.That(renderer.GetComponent<Collider>(), Is.Null, "Selection must not add physical routing obstacles.");
                Assert.That(renderer.sharedMaterial.shader.isSupported, Is.True);
                Assert.That(renderer.sharedMaterial.renderQueue, Is.GreaterThanOrEqualTo(3000));
            }
            var overview = Object.FindAnyObjectByType<OverviewController>();
            var relay = network.NodeDefinitions.Single(node => node.Id == "R2");
            Assert.That(overview.Pick(Camera.main.WorldToScreenPoint(relay.Position + Vector3.up * 1.4f)),
                Is.EqualTo(OverviewTarget.Node("R2")));
        }

        [UnityTest] public IEnumerator OverviewSelectsColumnSidesAndCapsWithinActualHeightLimits()
        {
            var network = Resolve<FlowNetwork>();
            var tall = new RelayNodeDefinition("TALL", new Vector3(40, 0, 19), maximumRise: 26);
            var elevated = new RelayNodeDefinition("ELEVATED", new Vector3(52, 24, 30), maximumRise: 22);
            Assert.That(network.TryAddNodes(new NodeDefinition[] { tall, elevated,
                new RelayNodeDefinition("FIXED", new Vector3(50, 0, 35)) }), Is.True);
            yield return null;
            // Locator beams exceed lift limits; inspect lift selection after those arrivals finish.
            Resolve<CityFlow.Application.UseCases.FlowSimulation>().Tick(8);
            yield return null;
            var overview = Object.FindAnyObjectByType<OverviewController>();
            var camera = Camera.main;
            SideView(camera, tall.Position + Vector3.up * 13, 18);
            foreach (float height in new[] { 6f, 13f, 25f })
                foreach (float x in new[] { -2.2f, 0, 2.2f })
                {
                    overview.Hover(camera.WorldToScreenPoint(tall.Position + new Vector3(x, height, 0)));
                    Assert.That(overview.Hovered.NodeId, Is.EqualTo(tall.Id), "The whole visible column must be selectable.");
                }
            foreach (var offset in new[] { new Vector3(0, 28, 0), new Vector3(0, -3, 0), new Vector3(3, 13, 0) })
                Assert.That(overview.Pick(camera.WorldToScreenPoint(tall.Position + offset)).NodeId, Is.Not.EqualTo(tall.Id));

            SideView(camera, elevated.Position + Vector3.up * 3, 8);
            Assert.That(overview.Pick(camera.WorldToScreenPoint(elevated.Position + Vector3.up * 4)).NodeId, Is.EqualTo(elevated.Id));
            foreach (float y in new[] { 20f, 32f })
                Assert.That(overview.Pick(camera.WorldToScreenPoint(new Vector3(52, y, 30))).NodeId, Is.Not.EqualTo(elevated.Id),
                    "The column starts at its placement and ends at the stage ceiling.");
            Assert.That(overview.Pick(camera.WorldToScreenPoint(new Vector3(50, 26, 35))).NodeId, Is.Not.EqualTo("FIXED"));

            camera.transform.SetPositionAndRotation(tall.Position + Vector3.up * 70, Quaternion.Euler(90, 0, 0));
            Assert.That(overview.Pick(camera.WorldToScreenPoint(tall.Position + Vector3.right * 2)).NodeId, Is.EqualTo(tall.Id),
                "The top cap is selectable from above.");
            camera.nearClipPlane = 50;
            Assert.That(overview.Pick(camera.WorldToScreenPoint(tall.Position + Vector3.right * 2)).NodeId, Is.EqualTo(tall.Id),
                "The cap remains selectable when the near plane starts inside the column.");
            camera.transform.rotation = Quaternion.Euler(-90, 0, 0);
            Assert.That(overview.Pick(camera.pixelRect.center).IsEmpty, Is.True, "Columns behind the camera cannot be selected.");
        }

        [UnityTest] public IEnumerator MouseSelectsRelayColumnsInOverviewAndNode360AndShiftStillEditsVerticalLines()
        {
            var network = Resolve<FlowNetwork>();
            var tall = new RelayNodeDefinition("TALL", new Vector3(40, 0, 19), maximumRise: 26);
            var elevated = new RelayNodeDefinition("ELEVATED", new Vector3(52, 24, 30), maximumRise: 22);
            Assert.That(network.TryAddNodes(new NodeDefinition[] { tall, elevated }), Is.True);
            yield return null;
            var overview = Object.FindAnyObjectByType<OverviewController>();
            var controller = Object.FindAnyObjectByType<NodeConnectionController>();
            var session = Resolve<ConnectionSession>();
            var camera = Camera.main;
            var old = InputSystem.settings.editorInputBehaviorInPlayMode;
            var background = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var mouse = InputSystem.AddDevice<Mouse>();
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                SideView(camera, tall.Position + Vector3.up * 13, 18);
                Vector2 point = camera.WorldToScreenPoint(tall.Position + Vector3.up * 13);
                Assert.That(overview.IsPointerBlocked?.Invoke(point), Is.False);
                yield return Click(mouse, point);
                Assert.That(controller.IsNode360, Is.True, "Clicking the column must open its Relay.");
                Assert.That(session.SourceId, Is.EqualTo(tall.Id));

                // Aim at the target column above its marker, without selecting a target through the controller.
                Vector3 direction = elevated.Position + Vector3.up * 5 - camera.transform.position;
                float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
                float pitch = -Mathf.Atan2(direction.y, new Vector2(direction.x, direction.z).magnitude) * Mathf.Rad2Deg;
                controller.Look(new Vector2(Mathf.DeltaAngle(camera.transform.eulerAngles.y, yaw),
                    -Mathf.DeltaAngle(camera.transform.eulerAngles.x, pitch)));
                yield return null;
                point = camera.WorldToScreenPoint(elevated.Position + Vector3.up * 5);
                Assert.That(Vector2.Distance(point, camera.WorldToScreenPoint(elevated.Position + Vector3.up * 1.4f)), Is.GreaterThan(24));
                Assert.That(overview.IsPointerBlocked?.Invoke(point), Is.False);
                yield return Click(mouse, point);
                Assert.That(session.IsActive, Is.False, "Clicking the target column must confirm the valid connection.");
                var line = network.Snapshot().Lines.Single();
                Assert.That(line.SourceId, Is.EqualTo(tall.Id));
                Assert.That(line.DestinationId, Is.EqualTo(elevated.Id));

                SideView(camera, tall.Position + Vector3.up * 13, 18);
                point = camera.WorldToScreenPoint(tall.Position + Vector3.up * 13);
                Assert.That(overview.Pick(point).NodeId, Is.EqualTo(tall.Id), "Normal selection prefers the Relay column.");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftShift));
                yield return Click(mouse, point);
                Assert.That(controller.IsEditing, Is.True, "Shift-click must still edit a Line inside the column.");
                Assert.That(Resolve<LinePreviewService>().EditingLineId, Is.EqualTo(line.Id));
                Assert.That(Resolve<LinePreviewService>().Current?.Points, Is.EqualTo(line.Route.Points));
            }
            finally
            {
                InputSystem.RemoveDevice(mouse); InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.editorInputBehaviorInPlayMode = old;
                InputSystem.settings.backgroundBehavior = background;
            }
        }

        private static void SideView(Camera camera, Vector3 center, float size)
        {
            camera.orthographic = true;
            camera.orthographicSize = size;
            camera.rect = new Rect(0, 0, 1, 1);
            camera.transform.SetPositionAndRotation(center + Vector3.back * 100, Quaternion.identity);
        }

        private static IEnumerator Click(Mouse mouse, Vector2 point)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }); yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Left)); yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }); yield return null;
        }

        [UnityTest] public IEnumerator HologramsFollowConnectionFocusAndNode360Visibility()
        {
            var overview = Object.FindAnyObjectByType<OverviewController>();
            var controller = Object.FindAnyObjectByType<NodeConnectionController>();
            var renderer = Height("R2");
            Assert.That(renderer, Is.Not.Null);
            if (renderer == null) yield break;
            var network = Resolve<FlowNetwork>();
            var before = network.Snapshot();
            Color normal = renderer.sharedMaterial.GetColor("_BaseColor");
            overview.Select(OverviewTarget.Node("R1"));
            overview.FocusSelection();
            yield return null; yield return null;
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            Assert.That(block.GetColor("_BaseColor").r, Is.LessThan(normal.r * 0.3f));
            Assert.That(block.GetColor("_BaseColor").a, Is.EqualTo(normal.a));
            Assert.That(network.Snapshot(), Is.SameAs(before));

            overview.Select(OverviewTarget.Node("R2"));
            controller.BeginSelected();
            yield return null; yield return null;
            Assert.That(renderer.gameObject.activeSelf, Is.False, "The source projection must not cover the Node 360 camera.");
            Assert.That(Height("R1"), Is.Not.Null);
            controller.CancelSelection();
            yield return null; yield return null;
            Assert.That(renderer.gameObject.activeSelf, Is.True);
            renderer.GetPropertyBlock(block);
            Assert.That(block.isEmpty, Is.True);
            Assert.That(renderer.sharedMaterial.GetColor("_BaseColor"), Is.EqualTo(normal));
        }
    }
}
