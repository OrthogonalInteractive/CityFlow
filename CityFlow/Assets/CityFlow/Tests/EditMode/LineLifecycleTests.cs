#nullable enable
using System;
using System.Linq;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Domain.Spatial;
using NUnit.Framework;
using UnityEngine;
namespace CityFlow.Tests.EditMode
{
    public sealed class LineLifecycleTests
    {
        private sealed class First : IRandomSource { public int NextIndex(int count)=>0; }
        private static FlowNetwork Create() => new FlowNetwork(new StageDefinition(0,new Rect(-50,-50,100,100),Array.Empty<Bounds>(),new[] {
            new NodeDefinition("S",NodeKind.Source,Vector3.zero,3,3,generationInterval:1000),
            new NodeDefinition("R",NodeKind.Relay,new Vector3(10,0,0)),
            new NodeDefinition("T",NodeKind.Sink,new Vector3(20,0,0),3,3,FlowColor.Red) }),new NetworkSettings(2,2,2,10,0));
        private static int Connect(FlowNetwork n,string from,string to)
        {
            var nodes=n.NodeDefinitions;
            return n.TryConnect(from,to,new[]{nodes.Single(x=>x.Id==from).Position,nodes.Single(x=>x.Id==to).Position}).LineId!.Value;
        }
        private static void Send(FlowNetwork n)
        { n.GenerateFlow("S",FlowColor.Red); n.RouteWaitingFlows(new First()); }
        private static void Conserve(FlowNetwork n)
        {
            var s=n.Snapshot(); var ids=s.Nodes.SelectMany(x=>x.Buffer).Select(x=>x.Id).Concat(s.Lines.SelectMany(x=>x.InFlight).Select(x=>x.Flow.Id)).ToArray();
            Assert.That(ids.Distinct().Count(),Is.EqualTo(ids.Length)); Assert.That(ids.Length+s.DeliveredCount,Is.EqualTo(s.GeneratedCount));
        }
        [Test] public void DeleteBlocksNewDeparturesAndReleasesSlotsOnlyAfterDrain()
        {
            var n=Create(); int id=Connect(n,"S","R"); Send(n); n.AdvanceInFlight(0.3);
            Assert.That(n.RequestDeletion(id),Is.True); Send(n);
            Assert.That(n.Snapshot().Lines.Single().InFlight.Count,Is.EqualTo(1));
            Assert.That(n.Snapshot().Nodes.Single(x=>x.Definition.Id=="S").OutgoingUsed,Is.EqualTo(1));
            n.AdvanceInFlight(1); Assert.That(n.Snapshot().Lines,Is.Empty);
            Assert.That(n.Snapshot().Nodes.Single(x=>x.Definition.Id=="R").IncomingUsed,Is.Zero);
            Assert.That(n.CancelPending(id),Is.False); Conserve(n);
        }
        [Test] public void BlockedDestinationNeverForcesDeletionAndCancelPreservesFlights()
        {
            var n=Create(); int id=Connect(n,"S","R"); Send(n); Send(n); n.AdvanceInFlight(2);
            Send(n); Send(n); n.AdvanceInFlight(2); var before=n.Snapshot().Lines.Single();
            Assert.That(n.RequestDeletion(id),Is.True); n.AdvanceInFlight(10000);
            Assert.That(n.Snapshot().Lines.Single().InFlight.Select(f=>(f.Flow.Id,f.Distance)),Is.EqualTo(before.InFlight.Select(f=>(f.Flow.Id,f.Distance))));
            Assert.That(n.CancelPending(id),Is.True);
            Assert.That(n.Snapshot().Lines.Single().Route,Is.SameAs(before.Route));
            Assert.That(n.Snapshot().Nodes.Single(x=>x.Definition.Id=="S").OutgoingUsed,Is.EqualTo(1));
            n.RequestDeletion(id); Connect(n,"R","T"); n.RouteWaitingFlows(new First()); n.AdvanceInFlight(2);
            Assert.That(n.Snapshot().Lines.Any(l=>l.Id==id),Is.False); Conserve(n);
        }
        [Test] public void RouteChangeKeepsOldPositionsUntilDrainThenUsesNewPolyline()
        {
            var n=Create(); int id=Connect(n,"S","T"); Send(n); n.AdvanceInFlight(0.5);
            var before=n.Snapshot().Lines.Single(); var route=new[]{Vector3.zero,new Vector3(10,0,10),new Vector3(20,0,0)};
            Assert.That(n.RequestRouteChange(id,route),Is.True); Assert.That(n.RequestDeletion(id),Is.False);
            Assert.That(n.Snapshot().Lines.Single().Route,Is.SameAs(before.Route));
            Assert.That(n.Snapshot().Lines.Single().InFlight.Single().Distance,Is.EqualTo(5));
            Send(n); Assert.That(n.Snapshot().Lines.Single().InFlight.Count,Is.EqualTo(1));
            n.AdvanceInFlight(2); Assert.That(n.Snapshot().Lines.Single().Route.Points,Is.EqualTo(route));
            n.RouteWaitingFlows(new First()); Assert.That(n.Snapshot().Lines.Single().InFlight.Single().Distance,Is.Zero); Conserve(n);
        }
        [Test] public void CancellingRouteChangeKeepsOldRouteAndRestartsDepartures()
        {
            var n=Create(); int id=Connect(n,"S","T"); Send(n); n.AdvanceInFlight(0.3); var old=n.Snapshot().Lines.Single();
            Assert.That(n.RequestRouteChange(id,new[]{Vector3.zero,new Vector3(10,0,10),new Vector3(20,0,0)}),Is.True);
            Assert.That(n.CancelPending(id),Is.True); Send(n);
            Assert.That(n.Snapshot().Lines.Single().Route,Is.SameAs(old.Route));
            Assert.That(n.Snapshot().Lines.Single().InFlight.Count,Is.EqualTo(2));
            Assert.That(n.Snapshot().Lines.Single().InFlight[0].Distance,Is.EqualTo(old.InFlight[0].Distance)); Conserve(n);
        }
        [Test] public void EmptyLineCommandsCompleteImmediatelyAndIdsAreNeverReused()
        {
            var n=Create(); int a=Connect(n,"S","R"),b=Connect(n,"S","T");
            Assert.That(n.RequestDeletion(a),Is.True); int c=Connect(n,"R","T"); Assert.That(c,Is.GreaterThan(b));
            var route=new[]{Vector3.zero,new Vector3(10,0,10),new Vector3(20,0,0)};
            Assert.That(n.RequestRouteChange(b,route),Is.True);
            Assert.That(n.Snapshot().Lines.Single(x=>x.Id==b).Route.Points,Is.EqualTo(route));
            Assert.That(n.RequestRouteChange(b,new[]{Vector3.one,new Vector3(20,0,0)}),Is.False);
            Assert.That(n.RequestDeletion(999),Is.False); Assert.That(n.CancelPending(b),Is.False);
        }
        [Test] public void ReservedMatchingSinkIsExcludedFromDirectPriority()
        {
            var n=Create(); int direct=Connect(n,"S","T"); int relay=Connect(n,"S","R"); Send(n);
            Assert.That(n.RequestDeletion(direct),Is.True); Send(n);
            Assert.That(n.Snapshot().Lines.Single(x=>x.Id==relay).InFlight.Count,Is.EqualTo(1)); Conserve(n);
        }
        [Test] public void ExistingPreviewIsNonDestructiveAndRechecksConflictingReservation()
        {
            var n=Create(); int id=Connect(n,"S","T"); Send(n); n.AdvanceInFlight(0.2);
            var stage=new StageDefinition(0,new Rect(-50,-50,100,100),Array.Empty<Bounds>(),n.NodeDefinitions);
            using var p=new CityFlow.Application.Routing.LinePreviewService(n,new CityFlow.Infrastructure.Routing.GroundRoutePlanner(stage,0));
            using var session=new CityFlow.Application.Connections.ConnectionSession(n,p);
            Assert.That(session.BeginLineEdit(id),Is.True); var old=n.Snapshot().Lines.Single();
            p.InsertPoint(0,new Vector3(10,0,10)); session.SelectTarget("R");
            Assert.That(p.Current!.DestinationId,Is.EqualTo("T")); Assert.That(p.Current.OutgoingAfter,Is.EqualTo(1));
            Assert.That(n.Snapshot().Lines.Single().Route,Is.SameAs(old.Route));
            Assert.That(n.RequestDeletion(id),Is.True);
            Assert.That(session.Confirm(),Is.EqualTo(ConnectionFailure.LineUnavailable));
            Assert.That(session.IsActive,Is.True); Assert.That(p.Current,Is.Not.Null);
            Assert.That(n.CancelPending(id),Is.True); session.Cancel();
            Assert.That(n.Snapshot().Lines.Single().Route,Is.SameAs(old.Route)); Conserve(n);
        }
    }
}
