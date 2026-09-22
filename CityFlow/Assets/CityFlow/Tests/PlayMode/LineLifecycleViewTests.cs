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
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using VContainer;
namespace CityFlow.Tests.PlayMode
{
    public sealed class LineLifecycleViewTests
    {
        private sealed class First : IRandomSource { public int NextIndex(int count)=>0; }
        [UnitySetUp] public IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("WiringLab"); yield return null;
            Object.FindAnyObjectByType<SimulationDriver>().enabled=false;
        }
        private static void Submit(Button b) { b.Focus(); using var e=NavigationSubmitEvent.GetPooled(); b.SendEvent(e); }
        private static int Connect(ConnectionSession session,string from,string to)
        { Assert.That(session.Begin(from),Is.True); session.SelectTarget(to); Assert.That(session.Confirm(),Is.EqualTo(ConnectionFailure.None)); return session.LastCreatedLineId!.Value; }
        [UnityTest] public IEnumerator DeleteShowsBlockedReceiverCancelKeepsFlightsAndDrainRemovesRenderer()
        {
            var scope=Object.FindAnyObjectByType<CityFlowLifetimeScope>(); var n=scope.Container.Resolve<FlowNetwork>();
            var session=scope.Container.Resolve<ConnectionSession>(); int id=Connect(session,"S1","R1");
            for(int i=0;i<n.Settings.RelayBufferCapacity;i++) n.GenerateFlow("S1",FlowColor.Red);
            n.RouteWaitingFlows(new First()); n.AdvanceInFlight(100);
            n.GenerateFlow("S1",FlowColor.Red); n.RouteWaitingFlows(new First()); n.AdvanceInFlight(100);
            var before=n.Snapshot().Lines.Single().InFlight.Single();
            Object.FindAnyObjectByType<OverviewController>().Select(OverviewTarget.Line(id)); yield return null;
            var root=Object.FindAnyObjectByType<UIDocument>().rootVisualElement;
            Submit(root.Q<Button>("line-delete")); yield return null;
            Assert.That(root.Q<Label>("line-action-detail").text,Does.Contain("DELETE PENDING").And.Contain("Buffer space"));
            Assert.That(n.Snapshot().Lines.Single().Status,Is.EqualTo(LineStatus.DeletePending));
            Submit(root.Q<Button>("line-cancel")); yield return null;
            Assert.That(n.Snapshot().Lines.Single().InFlight.Single().Flow.Id,Is.EqualTo(before.Flow.Id));
            Assert.That(n.Snapshot().Lines.Single().InFlight.Single().Distance,Is.EqualTo(before.Distance));
            Submit(root.Q<Button>("line-delete")); Connect(session,"R1","RED");
            n.RouteWaitingFlows(new First()); n.AdvanceInFlight(100); yield return null; yield return null;
            Assert.That(n.Snapshot().Lines.Any(l=>l.Id==id),Is.False);
            Assert.That(GameObject.Find($"Line {id}: S1 -> R1"),Is.Null);
            Assert.That(n.Snapshot().GeneratedCount,Is.EqualTo(n.Snapshot().Nodes.Sum(x=>x.Buffer.Count)+n.Snapshot().Lines.Sum(l=>l.InFlight.Count)+n.Snapshot().DeliveredCount));
        }
        [UnityTest] public IEnumerator ExistingLineEditKeepsOldPathUntilDrainAndRefreshesRendererAndReadout()
        {
            var scope=Object.FindAnyObjectByType<CityFlowLifetimeScope>(); var n=scope.Container.Resolve<FlowNetwork>();
            var session=scope.Container.Resolve<ConnectionSession>(); int id=Connect(session,"S1","BLUE");
            n.GenerateFlow("S1",FlowColor.Blue); n.RouteWaitingFlows(new First()); n.AdvanceInFlight(0.2);
            var old=n.Snapshot().Lines.Single();
            Object.FindAnyObjectByType<OverviewController>().Select(OverviewTarget.Line(id)); yield return null;
            var root=Object.FindAnyObjectByType<UIDocument>().rootVisualElement; Submit(root.Q<Button>("line-edit")); yield return null;
            Assert.That(Object.FindAnyObjectByType<NodeConnectionController>().IsEditing,Is.True);
            var p=scope.Container.Resolve<LinePreviewService>();
            Assert.That(p.Current!.OutgoingAfter,Is.EqualTo(1));
            p.InsertPoint(0,(old.Route.Points[0]+old.Route.Points[1])*0.5f+Vector3.back*5); yield return null;
            Assert.That(p.Current!.CanConfirm,Is.True); var edited=p.Current.Points.ToArray();
            Submit(root.Q<Button>("route-apply")); yield return null;
            Assert.That(n.Snapshot().Lines.Single().Status,Is.EqualTo(LineStatus.RouteChangePending));
            Assert.That(n.Snapshot().Lines.Single().Route,Is.SameAs(old.Route));
            Assert.That(n.Snapshot().Lines.Single().InFlight.Single().Distance,Is.EqualTo(old.InFlight.Single().Distance));
            Assert.That(root.Q<Label>("line-action-detail").text,Does.Contain("ROUTE CHANGE PENDING"));
            n.AdvanceInFlight(100); yield return null; yield return null;
            var line=n.Snapshot().Lines.Single(); Assert.That(line.Status,Is.EqualTo(LineStatus.Running));
            Assert.That(line.Route.Points,Is.EqualTo(edited)); Assert.That(n.Snapshot().DeliveredCount,Is.EqualTo(1));
            Assert.That(GameObject.Find($"Line {id}: S1 -> BLUE").GetComponent<LineRenderer>().positionCount,Is.EqualTo(edited.Length));
            Assert.That(OverviewReadout.Describe(OverviewTarget.Line(id),n.Snapshot(),n.Settings),Does.Contain($"LENGTH {line.Route.Length:0.0} m"));
        }
    }
}
