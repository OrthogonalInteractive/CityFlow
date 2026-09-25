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
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using VContainer;
using Object = UnityEngine.Object;

namespace CityFlow.Tests.PlayMode
{
    public sealed class PauseInputTests
    {
        private Keyboard? keyboard;
        private Mouse[] mice = Array.Empty<Mouse>();
        private InputSettings.EditorInputBehaviorInPlayMode editorInput;
        private InputSettings.BackgroundBehavior background;
        private static VisualElement Root => Object.FindAnyObjectByType<UIDocument>().rootVisualElement;
        private static T Resolve<T>() => Object.FindAnyObjectByType<CityFlowLifetimeScope>().Container.Resolve<T>();
        private static NodeConnectionController Controller => Object.FindAnyObjectByType<NodeConnectionController>();
        private static OverviewController Overview => Object.FindAnyObjectByType<OverviewController>();

        [UnitySetUp] public IEnumerator Load()
        {
            mice = InputSystem.devices.OfType<Mouse>().Where(m => m.enabled).ToArray();
            foreach (var mouse in mice) InputSystem.DisableDevice(mouse);
            editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            background = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            keyboard = InputSystem.AddDevice<Keyboard>();
            yield return SceneManager.LoadSceneAsync("WiringLab");
            yield return null;
            Object.FindAnyObjectByType<SimulationDriver>().enabled = false;
        }

        [TearDown] public void RestoreInput()
        {
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            foreach (var mouse in mice) if (mouse.added) InputSystem.EnableDevice(mouse);
            InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            InputSystem.settings.backgroundBehavior = background;
        }

        private IEnumerator Press(Key key)
        {
            InputSystem.QueueStateEvent(keyboard ?? throw new InvalidOperationException("Keyboard not initialized"), new KeyboardState(key));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard ?? throw new InvalidOperationException("Keyboard not initialized"), new KeyboardState());
            yield return null;
        }

        private static void Begin()
        {
            Overview.Select(OverviewTarget.Node("S1"));
            Controller.BeginSelected();
            Controller.FocusTarget("BLUE");
        }

        [UnityTest] public IEnumerator PauseButtonKeepsEditingAndCameraActiveAndApplyWorksWhilePaused()
        {
            var sim = Resolve<FlowSimulation>();
            var preview = Resolve<LinePreviewService>();
            Begin(); Controller.BeginEditing();
            yield return null;
            var state = preview.Current;
            double elapsed = sim.ElapsedSeconds;
            UiPointer.Click(Root.Q<Button>("pause-toggle"));
            yield return null;
            Assert.That(sim.IsPaused, Is.True);
            sim.Tick(60);
            Assert.That(sim.ElapsedSeconds, Is.EqualTo(elapsed));
            Assert.That(Controller.IsEditing, Is.True);
            Assert.That(preview.Current, Is.SameAs(state));
            Assert.That(Root.Q("connection-panel").resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            Assert.That(Root.Q<Button>("pause-toggle").text, Is.EqualTo("Resume"));
            var before = Camera.main.transform.position;
            yield return Press(Key.W);
            Assert.That(Camera.main.transform.position, Is.Not.EqualTo(before));
            Assert.That(preview.InsertPoint(0, (state!.Points[0] + state.Points[1]) * 0.5f + Vector3.back * 5), Is.True);
            yield return null;
            UiPointer.Click(Root.Q<Button>("route-apply"));
            yield return null;
            Assert.That(Resolve<ConnectionSession>().IsActive, Is.False);
            Assert.That(sim.IsPaused, Is.True);
            Assert.That(Resolve<FlowNetwork>().Snapshot().Lines.Count, Is.EqualTo(1));
            UiPointer.Click(Root.Q<Button>("pause-toggle"));
            yield return null;
            Assert.That(sim.IsPaused, Is.False);
            sim.Tick(0.2);
            Assert.That(sim.ElapsedSeconds, Is.EqualTo(elapsed + 0.2).Within(1e-8));
        }

        [UnityTest] public IEnumerator EscapeLeavesNode360ClearsSelectionAndPreservesPauseState()
        {
            var sim = Resolve<FlowSimulation>();
            foreach (bool paused in new[] { false, true })
            {
                sim.SetPaused(paused);
                var before = Overview.CaptureView();
                Begin();
                yield return null;
                yield return Press(Key.Escape);
                Assert.That(sim.IsPaused, Is.EqualTo(paused));
                Assert.That(Resolve<ConnectionSession>().IsActive, Is.False);
                Assert.That(Resolve<LinePreviewService>().Current, Is.Null);
                Assert.That(Controller.IsNode360, Is.False);
                Assert.That(Overview.Selected.IsEmpty, Is.True);
                Assert.That(Camera.main.transform.position, Is.EqualTo(before.Position));
                Assert.That(Resolve<FlowNetwork>().Snapshot().Lines, Is.Empty);
            }
        }

        [UnityTest] public IEnumerator EscapeDiscardsLineEditsWithoutDeletingOrPausingTheLine()
        {
            Begin(); Controller.ConfirmTarget("BLUE");
            var network = Resolve<FlowNetwork>();
            var original = network.Snapshot().Lines.Single();
            Overview.Select(OverviewTarget.Line(original.Id));
            Controller.EditSelectedLine();
            var preview = Resolve<LinePreviewService>();
            preview.InsertPoint(0, new Vector3(500, 0, 500));
            yield return Press(Key.Escape);
            Assert.That(Controller.IsEditing, Is.False);
            Assert.That(Overview.Selected.IsEmpty, Is.True);
            Assert.That(network.Snapshot().Lines.Single().Route, Is.SameAs(original.Route));
            Assert.That(network.Snapshot().Lines.Single().Status, Is.EqualTo(LineStatus.Running));
            Assert.That(Resolve<FlowSimulation>().IsPaused, Is.False);
        }

        [UnityTest] public IEnumerator EscapeClearsOverviewFocusWithoutMovingCameraOrChangingTime()
        {
            Overview.Select(OverviewTarget.Node("S1")); Overview.FocusSelection();
            var position = Camera.main.transform.position;
            yield return Press(Key.Escape);
            Assert.That(Overview.Selected.IsEmpty, Is.True);
            Assert.That(Overview.Focused.IsEmpty, Is.True);
            Assert.That(Camera.main.transform.position, Is.EqualTo(position));
            Assert.That(Resolve<FlowSimulation>().IsPaused, Is.False);
            yield return Press(Key.Escape);
            Assert.That(Resolve<FlowSimulation>().IsPaused, Is.False);
        }

        [UnityTest] public IEnumerator EnterAndBackspaceCannotActivateFocusedButtonsOrConfirmOrCancel()
        {
            var sim = Resolve<FlowSimulation>();
            var pause = Root.Q<Button>("pause-toggle");
            pause.Focus();
            foreach (var key in new[] { Key.Enter, Key.NumpadEnter, Key.Backspace })
            {
                yield return Press(key);
                Assert.That(sim.IsPaused, Is.False);
            }
            // UI Toolkit navigation submission must not bypass the pointer-only action policy.
            using (var submit = NavigationSubmitEvent.GetPooled()) pause.SendEvent(submit);
            Assert.That(sim.IsPaused, Is.False);
            Begin(); Controller.BeginEditing();
            yield return null;
            var preview = Resolve<LinePreviewService>();
            Object.FindAnyObjectByType<CityFlow.Presentation.UI.RouteEditView>().InsertAtScreen(
                Camera.main.WorldToScreenPoint((preview.Current!.Points[0] + preview.Current.Points[1]) * 0.5f));
            int pointCount = preview.Current.Points.Count;
            yield return null;
            Root.Q<Button>("route-apply").Focus();
            foreach (var key in new[] { Key.Enter, Key.NumpadEnter, Key.Backspace, Key.Delete, Key.V, Key.E })
            {
                yield return Press(key);
                Assert.That(preview.Current!.Points.Count, Is.EqualTo(pointCount));
                Assert.That(Resolve<ConnectionSession>().IsActive, Is.True);
                Assert.That(Resolve<FlowNetwork>().Snapshot().Lines, Is.Empty);
            }
            UiPointer.Click(Root.Q<Button>("route-cancel"));
            yield return null;
            Assert.That(Resolve<ConnectionSession>().IsActive, Is.False);
        }
    }
}
