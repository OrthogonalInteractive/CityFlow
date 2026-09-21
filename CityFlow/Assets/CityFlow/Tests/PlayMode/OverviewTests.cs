#nullable enable

using System;
using System.Collections;
using System.Linq;
using CityFlow.Composition;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Domain.Spatial;
using CityFlow.Presentation.Overview;
using CityFlow.Presentation.Rendering;
using NUnit.Framework;
using R3;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using VContainer;
using Object = UnityEngine.Object;

namespace CityFlow.Tests.PlayMode
{
    public sealed class OverviewTests
    {
        [UnitySetUp] public IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("Bootstrap", LoadSceneMode.Single);
            yield return null;
            Object.FindAnyObjectByType<SimulationDriver>().enabled = false;
        }
        private static OverviewController Controller()
        {
            var controller = Object.FindAnyObjectByType<OverviewController>();
            Assert.That(controller, Is.Not.Null, "Bootstrap must compose Overview input and camera controls.");
            return controller;
        }
        [UnityTest] public IEnumerator CameraPanZoomOrbitFocusAndHomeDoNotMutateTransport()
        {
            var c = Controller(); var cam = Camera.main;
            var network = Object.FindAnyObjectByType<CityFlowLifetimeScope>().Container.Resolve<FlowNetwork>();
            var before = network.Snapshot(); Vector3 home = cam.transform.position; Quaternion rotation = cam.transform.rotation;
            float size = cam.orthographicSize;
            c.Pan(new Vector2(10, 5)); Assert.That(cam.transform.position, Is.Not.EqualTo(home));
            c.Zoom(1); Assert.That(cam.orthographicSize, Is.LessThan(size));
            c.Orbit(new Vector2(20, 10)); Assert.That(cam.transform.rotation, Is.Not.EqualTo(rotation));
            c.Select(OverviewTarget.Node("S1")); c.FocusSelection();
            Vector3 node = network.NodeDefinitions.Single(n => n.Id == "S1").Position;
            Vector3 viewport = cam.WorldToViewportPoint(node);
            Assert.That(viewport.x, Is.EqualTo(0.5).Within(0.01)); Assert.That(viewport.y, Is.EqualTo(0.5).Within(0.01));
            c.ResetView(); Assert.That(Vector3.Distance(cam.transform.position, home), Is.LessThan(0.001));
            Assert.That(cam.orthographicSize, Is.EqualTo(size));
            Assert.That(network.Snapshot().GeneratedCount, Is.EqualTo(before.GeneratedCount));
            Assert.That(network.Snapshot().Lines.Count, Is.EqualTo(before.Lines.Count));
            yield return null;
        }
        [UnityTest] public IEnumerator HoverAndSelectionShowNodeAndLineDetails()
        {
            var c = Controller(); var cam = Camera.main;
            var scope = Object.FindAnyObjectByType<CityFlowLifetimeScope>();
            var n = scope.Container.Resolve<FlowNetwork>();
            Vector3 p = n.NodeDefinitions.Single(x=>x.Id=="S1").Position;
            c.Hover(cam.WorldToScreenPoint(p + Vector3.up * 1.4f));
            Assert.That(c.Hovered.NodeId, Is.EqualTo("S1"));
            yield return null;
            var label = Object.FindAnyObjectByType<UIDocument>().rootVisualElement.Q<Label>("overview-detail");
            Assert.That(label.text, Does.Contain("S1").And.Contain("BUFFER").And.Contain("OUT"));
            c.Select(OverviewTarget.Line(1)); c.Hover(new Vector2(-100,-100));
            yield return null;
            Assert.That(label.text, Does.Contain("S1 → RED").And.Contain("THROUGHPUT").And.Contain("STOPPED"));
            var route = n.Snapshot().Lines[0].Route;
            Assert.That(c.Pick(cam.WorldToScreenPoint(route.PositionAt(route.Length * 0.5f) + Vector3.up * 0.2f)).LineId, Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator FocusInputActionMovesCameraAndSubscriptionsEndOnDestroy()
        {
            var c = Controller(); int changes = 0; bool completed = false;
            using var subscription = c.SelectionChanged.Subscribe(_=>changes++, _=>completed=true);
            c.Select(OverviewTarget.Node("R1")); Assert.That(changes, Is.EqualTo(1));
            Vector3 before = Camera.main.transform.position;
            // Editor focus must not determine whether a synthetic test event reaches gameplay.
#if UNITY_EDITOR
            var previousBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            var previousBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F));

                yield return null;
                yield return null;
                Assert.That(Camera.main.transform.position, Is.Not.EqualTo(before));
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
#if UNITY_EDITOR
                InputSystem.settings.editorInputBehaviorInPlayMode = previousBehavior;
                InputSystem.settings.backgroundBehavior = previousBackground;
#endif
            }
            Object.Destroy(c);
            yield return null;
            Assert.That(completed, Is.True);
        }
    }
}
