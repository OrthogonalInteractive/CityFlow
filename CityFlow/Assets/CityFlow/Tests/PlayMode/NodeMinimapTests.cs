#nullable enable

using System.Collections;
using System.Linq;
using CityFlow.Application.Connections;
using CityFlow.Application.UseCases;
using CityFlow.Composition;
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
    public sealed class NodeMinimapTests
    {
        private static VisualElement Root => Object.FindAnyObjectByType<UIDocument>().rootVisualElement;
        private static NodeConnectionController Controller => Object.FindAnyObjectByType<NodeConnectionController>();
        private static T Resolve<T>() => Object.FindAnyObjectByType<CityFlowLifetimeScope>().Container.Resolve<T>();
        private static Camera MiniCamera => Object.FindAnyObjectByType<UIDocument>().GetComponentInChildren<Camera>();

        [UnitySetUp] public IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("WiringLab"); yield return null;
            Object.FindAnyObjectByType<SimulationDriver>().enabled = false;
        }

        private static void Begin(string id)
        {
            Object.FindAnyObjectByType<OverviewController>().Select(OverviewTarget.Node(id));
            Controller.BeginSelected();
        }

        [UnityTest] public IEnumerator MiniCameraCentersSourceWhilePreviewAndLookChangeWithoutAffectingMainCamera()
        {
            Assert.That(Root.Q("node-minimap"), Is.Not.Null, "Node 360 needs its own local camera panel.");
            Assert.That(Root.Q("node-minimap").resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            Resolve<FlowSimulation>().SetPaused(true);
            Begin("S1"); yield return null; yield return null;
            var camera = MiniCamera;
            Assert.That(camera.enabled, Is.True);
            Assert.That(camera.orthographic, Is.True);
            Assert.That(Vector3.Dot(camera.transform.forward, Vector3.down), Is.GreaterThan(0.9999f));
            var source = Resolve<ConnectionSession>().Nodes.Single(n => n.Id == "S1");
            Vector3 centered = camera.WorldToViewportPoint(source.Position);
            Assert.That(centered.x, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(centered.y, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(camera.targetTexture != null && camera.targetTexture.IsCreated(), Is.True);
            Assert.That(Root.Q<Image>("node-minimap-image").image, Is.SameAs(camera.targetTexture));
            Assert.That(Camera.main.targetTexture, Is.Null);
            Assert.That(Camera.main.orthographic, Is.False);
            Assert.That(Root.Q("node-minimap").worldBound.Overlaps(Root.Q("connection-workspace").worldBound), Is.False);
            Assert.That(Root.Q("node-minimap").pickingMode, Is.EqualTo(PickingMode.Ignore));
            Vector3 position = camera.transform.position;
            Quaternion rotation = camera.transform.rotation;
            Controller.FocusTarget("BLUE"); Controller.Look(new Vector2(90, 10)); yield return null;
            Assert.That(camera.transform.position, Is.EqualTo(position), "A target preview must not move the center away from the Source.");
            Assert.That(camera.transform.rotation, Is.EqualTo(rotation), "The local map stays north-up while the main camera turns.");
            Assert.That(Resolve<FlowSimulation>().IsPaused, Is.True);
            Assert.That(Resolve<ConnectionSession>().TargetId, Is.EqualTo("BLUE"));
        }

        [UnityTest] public IEnumerator MiniCameraStopsOutside360ReusesResourcesAndReleasesThemWithTheScene()
        {
            Assert.That(Root.Q("node-minimap"), Is.Not.Null);
            Begin("S1"); yield return null; yield return null;
            var camera = MiniCamera;
            var texture = camera.targetTexture;
            Controller.ToggleOverview(); yield return null;
            Assert.That(camera.enabled, Is.False);
            Assert.That(Root.Q("node-minimap").resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            Controller.ToggleOverview(); yield return null;
            Assert.That(MiniCamera, Is.SameAs(camera));
            Assert.That(camera.targetTexture, Is.SameAs(texture));
            Controller.CancelSelection(); Begin("R1"); yield return null;
            var relay = Resolve<ConnectionSession>().Nodes.Single(n => n.Id == "R1");
            Assert.That(camera.WorldToViewportPoint(relay.Position).x, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(camera.WorldToViewportPoint(relay.Position).y, Is.EqualTo(0.5f).Within(0.0001f));
            Controller.FocusTarget("BLUE"); Controller.BeginEditing(); yield return null;
            Assert.That(camera.enabled, Is.False);
            Assert.That(Root.Q("node-minimap").resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            yield return SceneManager.LoadSceneAsync("WiringLab"); yield return null;
            Object.FindAnyObjectByType<SimulationDriver>().enabled = false;
            Assert.That(camera == null, Is.True, "The scene owns the secondary camera.");
            Assert.That(texture == null, Is.True, "The RenderTexture must not survive Retry or scene disposal.");
        }

        [UnityTest] public IEnumerator RebuiltDocumentKeepsTheImageAndGameOverStopsTheMiniCamera()
        {
            Begin("S1"); yield return null; yield return null;
            var camera = MiniCamera;
            Assert.That(camera, Is.Not.Null);
            var texture = camera.targetTexture;
            var document = Object.FindAnyObjectByType<UIDocument>();
            document.enabled = false;
            document.enabled = true; yield return null; yield return null;
            Assert.That(Root.Q<Image>("node-minimap-image").image, Is.SameAs(texture));
            Assert.That(Root.Q("node-minimap").resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            Resolve<FlowSimulation>().Tick(100); yield return null;
            Assert.That(Resolve<FlowSimulation>().Result, Is.Not.Null);
            Assert.That(camera.enabled, Is.False);
            Assert.That(Root.Q("node-minimap").resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
        }
    }
}
