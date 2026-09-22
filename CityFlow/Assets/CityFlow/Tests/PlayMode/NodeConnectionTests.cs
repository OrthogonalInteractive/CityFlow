#nullable enable

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

namespace CityFlow.Tests.PlayMode
{
    public sealed class NodeConnectionTests
    {
        [UnitySetUp] public IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("Bootstrap",LoadSceneMode.Single); yield return null;
            Object.FindAnyObjectByType<SimulationDriver>().enabled=false;
        }
        private static NodeConnectionController Controller()
        {
            var c=Object.FindAnyObjectByType<NodeConnectionController>();
            Assert.That(c,Is.Not.Null,"Bootstrap must compose the Node 360 connection flow."); return c;
        }
        private static void Submit(Button button)
        { button.Focus(); using var ev=NavigationSubmitEvent.GetPooled(); button.SendEvent(ev); }
        [UnityTest] public IEnumerator Node360SeparatesPanelsFromCityAndRestoresOpaqueBuildings()
        {
            var c=Controller(); var overview=Object.FindAnyObjectByType<OverviewController>();
            var camera=Camera.main; var viewport=camera.rect;
            var buildings=Object.FindObjectsByType<MeshRenderer>()
                .Where(r=>r.name=="Building" || r.name=="Roof").ToArray();
            var original=buildings.Select(r=>r.sharedMaterial.GetColor("_BaseColor")).ToArray();
            overview.Select(OverviewTarget.Node("R1")); c.BeginSelected(); c.FocusTarget("BLUE"); yield return null; yield return null;
            Assert.That(buildings.All(r=>r.sharedMaterial.GetColor("_BaseColor").a<0.5f),Is.True,"Walls and roofs must reveal the city behind them.");
            Assert.That(buildings.All(r=>r.sharedMaterial.GetFloat("_ZWrite")==0),Is.True);
            var root=Object.FindAnyObjectByType<UIDocument>().rootVisualElement;
            Rect view=new Rect(camera.rect.x*root.layout.width,(1-camera.rect.yMax)*root.layout.height,
                camera.rect.width*root.layout.width,camera.rect.height*root.layout.height);
            foreach(string name in new[]{"connection-panel","candidate-list-panel"})
                Assert.That(root.Q(name).worldBound.Overlaps(view),Is.False,name+" must not hide Nodes in the camera viewport.");
            var blue=Object.FindAnyObjectByType<CityFlowLifetimeScope>().Container.Resolve<ConnectionSession>().Nodes.Single(n=>n.Id=="BLUE");
            Vector3 projected=camera.WorldToViewportPoint(blue.Position+Vector3.up*1.4f);
            Vector2 point=new Vector2(view.x+projected.x*view.width,view.y+(1-projected.y)*view.height);
            Assert.That(root.Q("candidate-BLUE").worldBound.Contains(point),Is.False,"A focused label must not cover the Node itself.");
            c.ToggleOverview(); yield return null;
            Assert.That(camera.rect,Is.EqualTo(viewport));
            Assert.That(buildings.Select(r=>r.sharedMaterial.GetColor("_BaseColor")),Is.EqualTo(original));
            c.ToggleOverview(); c.enabled=false; yield return null;
            Assert.That(camera.rect,Is.EqualTo(viewport));
            Assert.That(buildings.Select(r=>r.sharedMaterial.GetColor("_BaseColor")),Is.EqualTo(original));
        }
        [UnityTest] public IEnumerator MouseOpensNodeAndShiftClickOpensLineWithoutAddingPoint()
        {
            var c=Controller(); var overview=Object.FindAnyObjectByType<OverviewController>();
            var scope=Object.FindAnyObjectByType<CityFlowLifetimeScope>(); var session=scope.Container.Resolve<ConnectionSession>();
            var preview=scope.Container.Resolve<LinePreviewService>(); var network=scope.Container.Resolve<FlowNetwork>();
            var old=InputSystem.settings.editorInputBehaviorInPlayMode; var background=InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            var mouse=InputSystem.AddDevice<Mouse>(); var keyboard=InputSystem.AddDevice<Keyboard>();
            try
            {
                Vector2 point=Camera.main.WorldToScreenPoint(network.NodeDefinitions.Single(n=>n.Id=="R1").Position+Vector3.up*1.4f);
                Assert.That(overview.Pick(point).NodeId,Is.EqualTo("R1"));
                Assert.That(overview.IsPointerBlocked?.Invoke(point),Is.False,"R1 must be outside HUD panels for the click test.");
                InputSystem.QueueStateEvent(mouse,new MouseState { position=point }); yield return null;
                InputSystem.QueueStateEvent(mouse,new MouseState { position=point }.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left)); yield return null;
                InputSystem.QueueStateEvent(mouse,new MouseState { position=point }); yield return null;
                Assert.That(c.IsNode360,Is.True,"A Node click must enter Node 360 without Connect. Selected="+overview.Selected.NodeId+" source="+session.SourceId);
                session.Cancel(); yield return null;
                var line=network.Snapshot().Lines.First(l=>l.SourceId=="S1" && l.DestinationId=="R1");
                point=Camera.main.WorldToScreenPoint(line.Route.PositionAt(line.Route.Length*0.5f)+Vector3.up*0.2f);
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.LeftShift));
                InputSystem.QueueStateEvent(mouse,new MouseState { position=point }); yield return null;
                InputSystem.QueueStateEvent(mouse,new MouseState { position=point }.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left)); yield return null;
                InputSystem.QueueStateEvent(mouse,new MouseState { position=point }); yield return null;
                Assert.That(c.IsEditing,Is.True,"Shift-clicking a Line must enter its editor.");
                Assert.That(preview.EditingLineId,Is.EqualTo(line.Id));
                Assert.That(preview.Current!.Points,Is.EqualTo(line.Route.Points),"The opening click must not insert a control point.");
            }
            finally
            {
                InputSystem.RemoveDevice(mouse); InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.editorInputBehaviorInPlayMode=old; InputSystem.settings.backgroundBehavior=background;
            }
        }
        [UnityTest] public IEnumerator ConnectCandidateConfirmCreatesLineThenRestoresOverview()
        {
            var c=Controller(); var overview=Object.FindAnyObjectByType<OverviewController>();
            var scope=Object.FindAnyObjectByType<CityFlowLifetimeScope>(); var n=scope.Container.Resolve<FlowNetwork>();
            var s=scope.Container.Resolve<ConnectionSession>(); var root=Object.FindAnyObjectByType<UIDocument>().rootVisualElement;
            overview.Pan(new Vector2(5,2)); overview.Zoom(1); overview.Select(OverviewTarget.Node("R1")); yield return null;
            var camera=Camera.main; Vector3 before=camera.transform.position; Quaternion rotation=camera.transform.rotation; float size=camera.orthographicSize;
            int count=n.Snapshot().Lines.Count;
            c.BeginSelected(); yield return null;
            Assert.That(c.IsNode360,Is.True); Assert.That(camera.orthographic,Is.False); Assert.That(overview.enabled,Is.False);
            c.FocusTarget("BLUE"); yield return null;
            Assert.That(scope.Container.Resolve<LinePreviewService>().Current?.DestinationId,Is.EqualTo("BLUE"));
            Assert.That(n.Snapshot().Lines.Count,Is.EqualTo(count));
            Submit(root.Q<Button>("candidate-BLUE")); yield return null;
            Assert.That(s.IsActive,Is.False); Assert.That(camera.orthographic,Is.True); Assert.That(overview.enabled,Is.True);
            Assert.That(camera.transform.position,Is.EqualTo(before)); Assert.That(camera.transform.rotation,Is.EqualTo(rotation));
            Assert.That(camera.orthographicSize,Is.EqualTo(size)); Assert.That(n.Snapshot().Lines.Count,Is.EqualTo(count+1));
            Assert.That(root.Q("line-rows"),Is.Null);
        }
        [UnityTest] public IEnumerator CameraReviewAndDistanceFiltersRetainPreviewAndCancelRestoresOriginalPose()
        {
            var c=Controller(); var overview=Object.FindAnyObjectByType<OverviewController>(); var camera=Camera.main;
            overview.Orbit(new Vector2(23,4)); Vector3 before=camera.transform.position; Quaternion rotation=camera.transform.rotation;
            overview.Select(OverviewTarget.Node("R1")); c.BeginSelected();
            var scope=Object.FindAnyObjectByType<CityFlowLifetimeScope>(); var s=scope.Container.Resolve<ConnectionSession>();
            var p=scope.Container.Resolve<LinePreviewService>(); s.SelectTarget("BLUE"); var preview=p.Current;
            c.Look(new Vector2(400,250)); Assert.That(camera.transform.forward.y,Is.GreaterThan(-1).And.LessThan(1));
            c.ToggleOverview(); Assert.That(c.IsNode360,Is.False); overview.Pan(new Vector2(10,20)); overview.Select(OverviewTarget.Line(1));
            c.ToggleOverview(); Assert.That(c.IsNode360,Is.True); Assert.That(p.Current,Is.SameAs(preview));
            yield return null;
            s.SetFilter(DistanceBand.Near); yield return null;
            var root=Object.FindAnyObjectByType<UIDocument>().rootVisualElement;
            Assert.That(root.Q<Button>("candidate-BLUE").resolvedStyle.display,Is.EqualTo(DisplayStyle.None));
            Assert.That(p.Current,Is.SameAs(preview)); Submit(root.Q<Button>("connect-cancel")); yield return null;
            Assert.That(p.Current,Is.Null); Assert.That(camera.transform.position,Is.EqualTo(before));
            Assert.That(camera.transform.rotation,Is.EqualTo(rotation)); Assert.That(overview.Selected.NodeId,Is.EqualTo("R1"));
            overview.Pan(Vector2.zero); Assert.That(camera.transform.position,Is.EqualTo(before),"The internal Overview pivot must also be restored.");
        }
        [UnityTest] public IEnumerator WiringLabStartsUnconnectedAndConfirmedSourceLineTransportsFlow()
        {
            yield return SceneManager.LoadSceneAsync("WiringLab",LoadSceneMode.Single); yield return null;
            Object.FindAnyObjectByType<SimulationDriver>().enabled=false;
            var scope=Object.FindAnyObjectByType<CityFlowLifetimeScope>(); var n=scope.Container.Resolve<FlowNetwork>();
            Assert.That(n.Snapshot().Lines,Is.Empty);
            var c=Controller(); Object.FindAnyObjectByType<OverviewController>().Select(OverviewTarget.Node("S1"));
            c.BeginSelected(); c.FocusTarget("BLUE"); yield return null; yield return null; yield return null;
            Assert.That(GameObject.Find("S1 / Source"),Is.Null,"The source's own mesh must not cover the Node 360 view.");
            var root=Object.FindAnyObjectByType<UIDocument>().rootVisualElement;
            var p=scope.Container.Resolve<LinePreviewService>().Current ?? throw new AssertionException("Missing Source Preview");
            Assert.That(p.CanConfirm,Is.True); Assert.That(p.Length,Is.GreaterThan(0));
            Submit(root.Q<Button>("candidate-BLUE")); yield return null;
            Assert.That(GameObject.Find("S1 / Source"),Is.Not.Null);
            Assert.That(n.Snapshot().Lines.Single().Route.Points,Is.EqualTo(p.Points));
            n.GenerateFlow("S1",FlowColor.Blue);
            scope.Container.Resolve<FlowSimulation>().Tick(0.1); yield return null;
            var line=n.Snapshot().Lines.Single(); Assert.That(line.InFlight.Any(f=>f.Flow.Color==FlowColor.Blue),Is.True);
            Assert.That(Object.FindAnyObjectByType<ValidationCityView>().VisibleFlowCount,Is.EqualTo(line.InFlight.Count));
            scope.Container.Resolve<FlowSimulation>().Tick(p.TravelTime+0.1); yield return null;
            Assert.That(n.Snapshot().DeliveredCount,Is.GreaterThan(0));
        }
        [UnityTest] public IEnumerator DisablingControllerCancelsSessionAndReenableCanStartAgain()
        {
            var c=Controller(); var overview=Object.FindAnyObjectByType<OverviewController>();
            overview.Select(OverviewTarget.Node("R1")); Vector3 before=Camera.main.transform.position; c.BeginSelected();
            var scope=Object.FindAnyObjectByType<CityFlowLifetimeScope>(); var s=scope.Container.Resolve<ConnectionSession>();
            s.SelectTarget("BLUE"); c.enabled=false; yield return null;
            Assert.That(s.IsActive,Is.False); Assert.That(scope.Container.Resolve<LinePreviewService>().Current,Is.Null);
            Assert.That(Camera.main.transform.position,Is.EqualTo(before)); Assert.That(overview.enabled,Is.True);
            c.enabled=true; c.BeginSelected(); yield return null; Assert.That(c.IsNode360,Is.True);
        }
        [UnityTest] public IEnumerator OccludedAndOffscreenCandidatesStaySelectableAndInvalidReasonsAreVisible()
        {
            var c=Controller(); var overview=Object.FindAnyObjectByType<OverviewController>(); overview.Select(OverviewTarget.Node("R1")); c.BeginSelected();
            c.FocusTarget("BLUE"); yield return null;
            var root=Object.FindAnyObjectByType<UIDocument>().rootVisualElement;
            var blue=root.Q<Button>("candidate-BLUE"); Assert.That(blue.text,Does.Contain("OCCLUDED")); Assert.That(blue.enabledSelf,Is.True);
            c.Look(new Vector2(180,0)); yield return null;
            Assert.That(blue.text,Does.Contain("OFFSCREEN"));
            yield return null;
            Assert.That(root.Q<Label>("route-feedback").text,Does.Contain("R1 → BLUE"));
            Assert.That(root.Q<Label>("candidate-detail").text,Does.Contain("Route preview ready"));
            Assert.That(blue.text,Does.Contain("PREVIEW READY"));
            var s=Object.FindAnyObjectByType<CityFlowLifetimeScope>().Container.Resolve<ConnectionSession>();
            s.SelectTarget("R2"); yield return null;
            Assert.That(root.Q<Label>("route-feedback").text,Does.Contain("already exists"));
            Assert.That(root.Q<Button>("connect-confirm").enabledSelf,Is.False);
            s.SelectTarget("R1"); yield return null;
            Assert.That(root.Q<Label>("route-feedback").text,Does.Contain("different Nodes"));
            s.Cancel(); overview.Select(OverviewTarget.Node("S1")); c.BeginSelected(); s.SelectTarget("R2"); yield return null;
            Assert.That(root.Q<Label>("route-feedback").text,Does.Contain("FROM OUT slots are full"));
            Assert.That(root.Q<Button>("connect-confirm").enabledSelf,Is.False);
        }
        [UnityTest] public IEnumerator SinkClickStaysInOverviewAndExplainsWhyItCannotStartALine()
        {
            var overview = Object.FindAnyObjectByType<OverviewController>();
            var before = Camera.main.transform.position;
            overview.Select(OverviewTarget.Node("RED")); Controller().BeginSelected();
            yield return null; yield return null;
            Assert.That(Controller().IsNode360, Is.False);
            Assert.That(Camera.main.transform.position, Is.EqualTo(before));
            var label = Object.FindAnyObjectByType<UIDocument>().rootVisualElement.Q<Label>("connection-notice");
            Assert.That(label, Is.Not.Null);
            Assert.That(label.text, Does.Contain("RED").And.Contain("cannot start"));
        }
        [UnityTest] public IEnumerator NewConnectionCanBeUndoneWithControlZWhilePaused()
        {
            var scope = Object.FindAnyObjectByType<CityFlowLifetimeScope>();
            var network = scope.Container.Resolve<FlowNetwork>();
            scope.Container.Resolve<FlowSimulation>().SetPaused(true);
            int count = network.Snapshot().Lines.Count;
            Object.FindAnyObjectByType<OverviewController>().Select(OverviewTarget.Node("R1"));
            Controller().BeginSelected(); Controller().ConfirmTarget("BLUE");
            yield return null; yield return null;
            var root = Object.FindAnyObjectByType<UIDocument>().rootVisualElement;
            var undo = root.Q<Button>("undo-connection");
            Assert.That(undo, Is.Not.Null); Assert.That(undo.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            var old = InputSystem.settings.editorInputBehaviorInPlayMode;
            var background = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftCtrl, Key.Z));
                yield return null; yield return null;
                Assert.That(network.Snapshot().Lines.Count, Is.EqualTo(count));
                Assert.That(scope.Container.Resolve<ConnectionSession>().CanUndoLastConnection, Is.False);
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.editorInputBehaviorInPlayMode = old;
                InputSystem.settings.backgroundBehavior = background;
            }
        }
        [UnityTest] public IEnumerator KeyboardCanBeginLookConfirmAndCancelWithoutFocusDependentInput()
        {
            var c=Controller(); var overview=Object.FindAnyObjectByType<OverviewController>(); overview.Select(OverviewTarget.Node("R1"));
#if UNITY_EDITOR
            var old=InputSystem.settings.editorInputBehaviorInPlayMode; var oldBackground=InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
#endif
            var keyboard=InputSystem.AddDevice<Keyboard>();
            try
            {
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.C)); yield return null; yield return null;
                Assert.That(c.IsNode360,Is.True); Quaternion rotation=Camera.main.transform.rotation;
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.RightArrow)); yield return null; yield return null;
                Assert.That(Camera.main.transform.rotation,Is.Not.EqualTo(rotation));
                InputSystem.QueueStateEvent(keyboard,new KeyboardState()); yield return null;
                var s=Object.FindAnyObjectByType<CityFlowLifetimeScope>().Container.Resolve<ConnectionSession>(); s.SelectTarget("BLUE");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Enter)); yield return null; yield return null;
                Assert.That(s.IsActive,Is.False); Assert.That(s.LastCreatedLineId,Is.Not.Null);
                InputSystem.QueueStateEvent(keyboard,new KeyboardState()); yield return null;
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.C)); yield return null; yield return null;
                Assert.That(s.IsActive,Is.True);
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Backspace)); yield return null; yield return null;
                Assert.That(s.IsActive,Is.False); Assert.That(c.IsNode360,Is.False);
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
#if UNITY_EDITOR
                InputSystem.settings.editorInputBehaviorInPlayMode=old; InputSystem.settings.backgroundBehavior=oldBackground;
#endif
            }
        }
    }
}
