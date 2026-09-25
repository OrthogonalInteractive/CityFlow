#nullable enable
using System.Collections;
using System.Linq;
using CityFlow.Application.Connections;
using CityFlow.Application.Routing;
using CityFlow.Composition;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Presentation.Connections;
using CityFlow.Presentation.Overview;
using CityFlow.Presentation.Rendering;
using CityFlow.Presentation.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using VContainer;
namespace CityFlow.Tests.PlayMode
{
    public sealed class ManualRouteInputTests
    {
        [UnitySetUp] public IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("WiringLab"); yield return null;
            Object.FindAnyObjectByType<SimulationDriver>().enabled=false;
        }
        private static void Submit(Button button) => UiPointer.Click(button);
        [UnityTest] public IEnumerator ManualEditorKeepsEndpointsValidatesAndAppliesVisibleRoute()
        {
            var scope=Object.FindAnyObjectByType<CityFlowLifetimeScope>(); var session=scope.Container.Resolve<ConnectionSession>();
            var preview=scope.Container.Resolve<LinePreviewService>(); var network=scope.Container.Resolve<FlowNetwork>();
            var overview=Object.FindAnyObjectByType<OverviewController>(); var controller=Object.FindAnyObjectByType<NodeConnectionController>();
            overview.Pan(new Vector2(6,8)); var before=overview.CaptureView(); overview.Select(OverviewTarget.Node("S1"));
            controller.BeginSelected(); session.SelectTarget("BLUE"); yield return null;
            var root=Object.FindAnyObjectByType<UIDocument>().rootVisualElement;
            Submit(root.Q<Button>("route-edit")); yield return null;
            Assert.That(controller.IsEditing,Is.True); Assert.That(Camera.main.transform.forward.y,Is.EqualTo(-1).Within(0.001));
            Assert.That(root.Q("route-point-0"),Is.Not.Null);
            var endpoints=preview.Current!.Points; var first=endpoints[0]; var last=endpoints.Last();
            preview.InsertPoint(0,new Vector3(500,0,500)); yield return null;
            Assert.That(root.Q<Button>("route-apply").enabledSelf,Is.False); Assert.That(network.Snapshot().Lines,Is.Empty);
            Submit(root.Q<Button>("route-regenerate")); yield return null;
            Assert.That(preview.Current!.Points[0],Is.EqualTo(first)); Assert.That(preview.Current.Points.Last(),Is.EqualTo(last));
            // A bend outside the obstacle footprint remains valid and changes the actual path.
            var points=preview.Current.Points; Vector3 middle=(points[0]+points[1])*0.5f+Vector3.left;
            Object.FindAnyObjectByType<RouteEditView>().InsertAtScreen(Camera.main.WorldToScreenPoint(middle)); yield return null;
            Assert.That(preview.Current.Points.Count,Is.GreaterThan(points.Count));
            Assert.That(root.Q<Button>("route-apply").enabledSelf,Is.True);
            var edited=preview.Current.Points.ToArray(); Submit(root.Q<Button>("route-apply")); yield return null;
            Assert.That(network.Snapshot().Lines.Single().Route.Points,Is.EqualTo(edited));
            Assert.That(controller.IsEditing,Is.False); Assert.That(overview.EditingRoute,Is.False);
            Assert.That(Camera.main.transform.position,Is.EqualTo(before.Position));
            Assert.That(Camera.main.transform.rotation,Is.EqualTo(before.Rotation));
        }
        [UnityTest] public IEnumerator ManualCancelRestoresCameraWithoutLeavingLineOrSlots()
        {
            var scope=Object.FindAnyObjectByType<CityFlowLifetimeScope>(); var session=scope.Container.Resolve<ConnectionSession>();
            var preview=scope.Container.Resolve<LinePreviewService>(); var overview=Object.FindAnyObjectByType<OverviewController>();
            overview.Select(OverviewTarget.Node("S1")); var before=overview.CaptureView();
            Object.FindAnyObjectByType<NodeConnectionController>().BeginSelected(); session.SelectTarget("BLUE");
            var root=Object.FindAnyObjectByType<UIDocument>().rootVisualElement; yield return null;
            Submit(root.Q<Button>("route-edit")); yield return null; overview.Pan(new Vector2(10,20));
            Submit(root.Q<Button>("route-cancel")); yield return null;
            Assert.That(preview.Current,Is.Null); Assert.That(session.IsActive,Is.False);
            var snapshot=scope.Container.Resolve<FlowNetwork>().Snapshot(); Assert.That(snapshot.Lines,Is.Empty);
            Assert.That(snapshot.Nodes.All(n=>n.IncomingUsed+n.OutgoingUsed==0),Is.True);
            Assert.That(Camera.main.transform.position,Is.EqualTo(before.Position));
        }
    }
}
