#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CityFlow.Application.UseCases;
using CityFlow.Composition;
using CityFlow.Domain.FlowNetwork;
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
    public sealed class OverviewZoomTests
    {
        private Mouse[] mice = Array.Empty<Mouse>();
        private static OverviewController Controller => Object.FindAnyObjectByType<OverviewController>();
        private static T Resolve<T>() => Object.FindAnyObjectByType<CityFlowLifetimeScope>().Container.Resolve<T>();

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

        [UnityTest] public IEnumerator WheelTraversesFiveLevelsWithAnimatedZoomInAndOutWhilePaused()
        {
            var c = Controller; var camera = Camera.main;
            var snapshot = Resolve<FlowNetwork>().Snapshot(); double elapsed = Resolve<FlowSimulation>().ElapsedSeconds;
            var position = camera.transform.position; var rotation = camera.transform.rotation;
            float home = camera.orthographicSize;
            for (int i = 0; i < 5; i++) { c.Zoom(-1); c.AdvanceZoom(1); }
            var sizes = new List<float> { camera.orthographicSize };
            for (int i = 0; i < 4; i++)
            {
                float before = camera.orthographicSize;
                c.Zoom(0.01f);
                Assert.That(camera.orthographicSize, Is.EqualTo(before), "A light wheel input starts a transition without jumping.");
                c.AdvanceZoom(0.15f); float middle = camera.orthographicSize;
                Assert.That(middle, Is.LessThan(before));
                c.AdvanceZoom(1);
                Assert.That(camera.orthographicSize, Is.LessThan(middle));
                sizes.Add(camera.orthographicSize);
            }
            Assert.That(sizes.Distinct().Count(), Is.EqualTo(5));
            Assert.That(sizes.Any(size => Mathf.Abs(size - home) < 0.001f), Is.True, "Home remains one of the five levels.");
            c.Zoom(120); c.AdvanceZoom(1);
            Assert.That(camera.orthographicSize, Is.EqualTo(sizes[4]));
            for (int i = 3; i >= 0; i--)
            {
                float before = camera.orthographicSize;
                c.Zoom(-0.01f); c.AdvanceZoom(0.15f);
                Assert.That(camera.orthographicSize, Is.GreaterThan(before).And.LessThan(sizes[i]));
                c.AdvanceZoom(1);
                Assert.That(camera.orthographicSize, Is.EqualTo(sizes[i]).Within(0.001f));
            }
            c.Zoom(-120); c.AdvanceZoom(1);
            Assert.That(camera.orthographicSize, Is.EqualTo(sizes[0]));
            Assert.That(camera.transform.position, Is.EqualTo(position));
            Assert.That(camera.transform.rotation, Is.EqualTo(rotation));
            Assert.That(Resolve<FlowNetwork>().Snapshot(), Is.SameAs(snapshot));
            Assert.That(Resolve<FlowSimulation>().ElapsedSeconds, Is.EqualTo(elapsed));
            Assert.That(Resolve<FlowSimulation>().IsPaused, Is.True);
            yield return null;
        }

        [UnityTest] public IEnumerator WheelBurstAdvancesOnceAndFreshOrReversedInputRetargetsWithoutJumping()
        {
            var c = Controller; var camera = Camera.main;
            c.Zoom(1); c.AdvanceZoom(1); float first = camera.orthographicSize;
            c.ResetView();
            foreach (float delta in new[] { 0.01f, 1, 120, 0.1f, 0.01f })
            { c.Zoom(delta); c.AdvanceZoom(0.04f); }
            c.AdvanceZoom(1);
            Assert.That(camera.orthographicSize, Is.EqualTo(first).Within(0.001f), "A multi-frame wheel burst is a single step regardless of delta units.");
            c.Zoom(1); c.AdvanceZoom(0.1f);
            float turning = camera.orthographicSize;
            Assert.That(turning, Is.LessThan(first));
            c.Zoom(-0.01f);
            Assert.That(camera.orthographicSize, Is.EqualTo(turning), "Reversing direction starts from the current interpolated size.");
            c.AdvanceZoom(1);
            Assert.That(camera.orthographicSize, Is.EqualTo(first).Within(0.001f));
            foreach (float invalid in new[] { 0f, float.NaN, float.PositiveInfinity, float.NegativeInfinity }) c.Zoom(invalid);
            c.AdvanceZoom(1);
            Assert.That(camera.orthographicSize, Is.EqualTo(first).Within(0.001f));
            yield return null;
        }

        [UnityTest] public IEnumerator FocusHomeAndWiringCancelZoomAndRestoreTheVisibleCameraSize()
        {
            var c = Controller; var camera = Camera.main; var home = c.CaptureView();
            Action[] interruptions =
            {
                c.ResetView, c.ClearSelection, () => c.FocusNodeSmooth("S1"),
                () => { c.Select(OverviewTarget.Node("S1")); c.FocusSelection(); },
                c.BeginRouteView, () => c.RestoreView(home), () => { c.enabled = false; c.enabled = true; }
            };
            foreach (Action interrupt in interruptions)
            {
                c.EditingRoute = false; c.RestoreView(home);
                c.Zoom(1, camera.ViewportToScreenPoint(new Vector3(0.7f, 0.3f))); c.AdvanceZoom(0.1f);
                interrupt(); float size = camera.orthographicSize; Vector3 position = camera.transform.position;
                c.AdvanceZoom(1);
                Assert.That(camera.orthographicSize, Is.EqualTo(size), "A canceled transition must not resume after another camera operation.");
                Assert.That(camera.transform.position, Is.EqualTo(position));
            }
            c.EditingRoute = false; c.RestoreView(home);
            c.Zoom(1, camera.ViewportToScreenPoint(new Vector3(0.7f, 0.3f))); c.AdvanceZoom(0.1f);
            var bookmark = c.CaptureView();
            c.Select(OverviewTarget.Node("S1"));
            var wiring = Object.FindAnyObjectByType<NodeConnectionController>(); wiring.BeginSelected();
            Assert.That(wiring.IsNode360, Is.True);
            wiring.CancelSelection(); c.AdvanceZoom(1);
            Assert.That(camera.orthographicSize, Is.EqualTo(bookmark.Size));
            Assert.That(camera.transform.position, Is.EqualTo(bookmark.Position));
            Assert.That(c.CaptureView().Pivot, Is.EqualTo(bookmark.Pivot));
            c.Zoom(-0.01f); c.AdvanceZoom(1);
            Assert.That(camera.orthographicSize, Is.GreaterThan(bookmark.Size));
            yield return null;
        }

        [UnityTest] public IEnumerator CursorAnchorStaysFixedThroughoutZoomAtDifferentDepthsAndInRouteView()
        {
            var c = Controller; var camera = Camera.main;
            var snapshot = Resolve<FlowNetwork>().Snapshot(); double elapsed = Resolve<FlowSimulation>().ElapsedSeconds;
            foreach (bool editing in new[] { false, true })
            {
                c.EditingRoute = editing; c.ResetView();
                if (!editing) c.Orbit(new Vector2(32, -18));
                camera.rect = new Rect(0.1f, 0.15f, 0.75f, 0.7f);
                var home = c.CaptureView();
                Vector2 cursor = camera.ViewportToScreenPoint(new Vector3(0.73f, 0.32f));
                Ray ray = camera.ScreenPointToRay(cursor);
                Vector3 nearPoint = ray.GetPoint(90), farPoint = ray.GetPoint(220);
                c.Zoom(0.01f, cursor);
                Assert.That(camera.transform.position, Is.EqualTo(home.Position), "Input must not snap the camera.");
                foreach (float dt in new[] { 0.05f, 0.1f, 1 })
                {
                    c.AdvanceZoom(dt);
                    AssertAtCursor(camera, nearPoint, cursor); AssertAtCursor(camera, farPoint, cursor);
                }
                Assert.That(Vector3.Distance(camera.transform.position, home.Position), Is.GreaterThan(1));
                Assert.That(camera.transform.position.y, Is.EqualTo(home.Position.y).Within(0.001f));
                Assert.That(camera.transform.rotation, Is.EqualTo(home.Rotation));
                Vector3 zoomPosition = camera.transform.position;
                if (editing) c.Pan(Vector2.zero); else c.Orbit(Vector2.zero);
                Assert.That(Vector3.Distance(camera.transform.position, zoomPosition), Is.LessThan(0.001f), "Manual camera operations must retain the translated pivot.");
                c.Zoom(-0.01f, cursor);
                foreach (float dt in new[] { 0.08f, 0.1f, 1 })
                {
                    c.AdvanceZoom(dt);
                    AssertAtCursor(camera, nearPoint, cursor); AssertAtCursor(camera, farPoint, cursor);
                }
                Assert.That(Vector3.Distance(camera.transform.position, home.Position), Is.LessThan(0.002f));
                Assert.That(camera.orthographicSize, Is.EqualTo(home.Size).Within(0.001f));
                Assert.That(Resolve<FlowNetwork>().Snapshot(), Is.SameAs(snapshot));
                Assert.That(Resolve<FlowSimulation>().ElapsedSeconds, Is.EqualTo(elapsed));
            }
            yield return null;
        }

        [UnityTest] public IEnumerator NewGestureUsesCurrentCursorWithoutJumpAndBurstKeepsOriginalAnchor()
        {
            var c = Controller; var camera = Camera.main;
            Vector2 first = camera.ViewportToScreenPoint(new Vector3(0.25f, 0.65f));
            Vector2 second = camera.ViewportToScreenPoint(new Vector3(0.8f, 0.25f));
            Vector3 firstWorld = camera.ScreenPointToRay(first).GetPoint(180);
            c.Zoom(1, first); c.AdvanceZoom(0.08f);
            c.Zoom(1, second); c.AdvanceZoom(0.08f);
            AssertAtCursor(camera, firstWorld, first);
            Vector3 secondWorld = camera.ScreenPointToRay(second).GetPoint(180);
            var turning = c.CaptureView();
            c.Zoom(-1, second);
            Assert.That(camera.transform.position, Is.EqualTo(turning.Position));
            Assert.That(camera.orthographicSize, Is.EqualTo(turning.Size));
            c.AdvanceZoom(0.1f); AssertAtCursor(camera, secondWorld, second);
            c.AdvanceZoom(1); AssertAtCursor(camera, secondWorld, second);
            var before = c.CaptureView();
            c.Zoom(1, new Vector2(camera.pixelRect.xMax + 10, camera.pixelRect.yMax + 10)); c.AdvanceZoom(1);
            Assert.That(camera.orthographicSize, Is.EqualTo(before.Size), "Scrolling outside the camera viewport must be ignored.");
            Assert.That(camera.transform.position, Is.EqualTo(before.Position));
            yield return null;
        }

        private static void AssertAtCursor(Camera camera, Vector3 world, Vector2 cursor)
        { Assert.That(Vector2.Distance(camera.WorldToScreenPoint(world), cursor), Is.LessThan(0.1f), "The point under the cursor must stay at the same screen position."); }

        [UnityTest] public IEnumerator PanAfterZoomingOutAtScreenEdgeDoesNotSnapBackToStageBounds()
        {
            var c = Controller; var camera = Camera.main;
            c.Zoom(-1, camera.ViewportToScreenPoint(new Vector3(0.95f, 0.5f))); c.AdvanceZoom(1);
            Vector3 position = camera.transform.position;
            c.Pan(Vector2.zero);
            Assert.That(Vector3.Distance(camera.transform.position, position), Is.LessThan(0.001f));
            c.Pan(new Vector2(0.1f, 0));
            Assert.That(Vector3.Distance(camera.transform.position, position), Is.EqualTo(0.1f).Within(0.001f), "Moving back toward the stage must preserve the requested pan distance.");
            position = camera.transform.position;
            c.Pan(new Vector2(-0.1f, 0));
            Assert.That(Vector3.Distance(camera.transform.position, position), Is.LessThanOrEqualTo(0.101f), "The existing pan limit must not pull an outside pivot abruptly onto the boundary.");
            yield return null;
        }

        [UnityTest] public IEnumerator InputSystemSmallScrollMovesOneLevelAndUiScrollDoesNotZoomTheWorld()
        {
            var c = Controller; var camera = Camera.main;
            c.Zoom(1); c.AdvanceZoom(1); float expected = camera.orthographicSize;
            c.ResetView(); float home = camera.orthographicSize;
            var oldEditor = InputSystem.settings.editorInputBehaviorInPlayMode;
            var oldBackground = InputSystem.settings.backgroundBehavior;
            var oldBlocker = c.IsPointerBlocked;
            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                // A device added while the Editor is unfocused can already be disabled before the settings change.
                InputSystem.EnableDevice(mouse);
                c.IsPointerBlocked = _ => false;
                Vector2 cursor = camera.ViewportToScreenPoint(new Vector3(0.75f, 0.35f));
                Vector3 anchor = camera.ScreenPointToRay(cursor).GetPoint(180);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = cursor, scroll = Vector2.up });
                yield return null; yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = camera.pixelRect.center });
                yield return null;
                c.AdvanceZoom(1);
                Assert.That(camera.orthographicSize, Is.EqualTo(expected).Within(0.001f));
                Assert.That(expected, Is.LessThan(home));
                AssertAtCursor(camera, anchor, cursor);
                Vector3 position = camera.transform.position;
                c.IsPointerBlocked = _ => true;
                InputSystem.QueueStateEvent(mouse, new MouseState { scroll = new Vector2(0, 120) });
                yield return null; yield return null;
                c.AdvanceZoom(1);
                Assert.That(camera.orthographicSize, Is.EqualTo(expected).Within(0.001f));
                Assert.That(camera.transform.position, Is.EqualTo(position));
            }
            finally
            {
                InputSystem.RemoveDevice(mouse); c.IsPointerBlocked = oldBlocker;
                InputSystem.settings.editorInputBehaviorInPlayMode = oldEditor;
                InputSystem.settings.backgroundBehavior = oldBackground;
            }
        }
    }
}
