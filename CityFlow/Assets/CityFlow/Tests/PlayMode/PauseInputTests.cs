#nullable enable
using System.Collections;
using CityFlow.Application.Connections;
using CityFlow.Application.Routing;
using CityFlow.Application.UseCases;
using CityFlow.Composition;
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
namespace CityFlow.Tests.PlayMode
{
    public sealed class PauseInputTests
    {
        [UnityTest] public IEnumerator EscapePausesWithoutCancellingEditorAndCameraAndEditingRemainActive()
        {
            yield return SceneManager.LoadSceneAsync("WiringLab"); yield return null;
            Object.FindAnyObjectByType<SimulationDriver>().enabled=false;
            var scope=Object.FindAnyObjectByType<CityFlowLifetimeScope>(); var sim=scope.Container.Resolve<FlowSimulation>();
            var connection=scope.Container.Resolve<ConnectionSession>(); var preview=scope.Container.Resolve<LinePreviewService>();
            var overview=Object.FindAnyObjectByType<OverviewController>(); var controller=Object.FindAnyObjectByType<NodeConnectionController>();
            overview.Select(OverviewTarget.Node("S1")); controller.BeginSelected(); connection.SelectTarget("BLUE"); controller.BeginEditing();
            var state=preview.Current; sim.Tick(0.1); double elapsed=sim.ElapsedSeconds;
#if UNITY_EDITOR
            var behavior=InputSystem.settings.editorInputBehaviorInPlayMode; var background=InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
#endif
            var keyboard=InputSystem.AddDevice<Keyboard>();
            try
            {
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Escape)); yield return null; yield return null;
                Assert.That(sim.IsPaused,Is.True); sim.Tick(60); Assert.That(sim.ElapsedSeconds,Is.EqualTo(elapsed).Within(1e-8));
                Assert.That(connection.IsActive,Is.True); Assert.That(controller.IsEditing,Is.True); Assert.That(preview.Current,Is.SameAs(state));
                var root=Object.FindAnyObjectByType<UIDocument>().rootVisualElement;
                Assert.That(root.Q<Label>("pause-status").text,Does.Contain("PAUSED"));
                var before=Camera.main.transform.position; InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W)); yield return null; yield return null;
                Assert.That(Camera.main.transform.position,Is.Not.EqualTo(before));
                Assert.That(preview.InsertPoint(0,(state!.Points[0]+state.Points[1])*0.5f+Vector3.back*5),Is.True);
                var button=root.Q<Button>("route-apply"); yield return null;
                button.Focus(); using(var e=NavigationSubmitEvent.GetPooled()) button.SendEvent(e); yield return null;
                Assert.That(connection.IsActive,Is.False); Assert.That(sim.IsPaused,Is.True);
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Escape)); yield return null; yield return null;
                Assert.That(sim.IsPaused,Is.False); sim.Tick(0.2); Assert.That(sim.ElapsedSeconds,Is.EqualTo(elapsed+0.2).Within(1e-8));
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
#if UNITY_EDITOR
                InputSystem.settings.editorInputBehaviorInPlayMode=behavior; InputSystem.settings.backgroundBehavior=background;
#endif
            }
        }
    }
}
