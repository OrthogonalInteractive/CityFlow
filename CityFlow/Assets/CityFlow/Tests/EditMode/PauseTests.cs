#nullable enable
using System;
using System.Linq;
using CityFlow.Application.UseCases;
using CityFlow.Application.Routing;
using CityFlow.Application.Connections;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Domain.Spatial;
using CityFlow.Infrastructure.Routing;
using NUnit.Framework;
using UnityEngine;
namespace CityFlow.Tests.EditMode
{
    public sealed class PauseTests
    {
        private sealed class First : IRandomSource { public int Calls; public int NextIndex(int count) { Calls++; return 0; } }
        private static StageDefinition Stage()=>new StageDefinition(0,new Rect(-50,-50,100,100),Array.Empty<Bounds>(),new[]{
            new NodeDefinition("S",NodeKind.Source,Vector3.zero),new NodeDefinition("R",NodeKind.Relay,new Vector3(10,0,0)),
            new NodeDefinition("T",NodeKind.Sink,new Vector3(20,0,0),3,3,FlowColor.Red)});
        [Test] public void PausePreservesGenerationMovementOverloadElapsedAndFractionalRemainder()
        {
            var n=new FlowNetwork(Stage(),new NetworkSettings(2,2,2,10,0)); var random=new First(); var s=new FlowSimulation(n,random);
            s.Tick(2.02); Assert.That(n.Snapshot().Nodes[0].OverloadSeconds,Is.GreaterThan(0));
            double time=s.ElapsedSeconds, grace=n.Snapshot().Nodes[0].OverloadSeconds; long generated=n.Snapshot().GeneratedCount;
            s.SetPaused(true); Assert.That(s.IsPaused,Is.True); s.Tick(100);
            Assert.That(s.ElapsedSeconds,Is.EqualTo(time)); Assert.That(n.Snapshot().GeneratedCount,Is.EqualTo(generated));
            Assert.That(n.Snapshot().Nodes[0].OverloadSeconds,Is.EqualTo(grace)); Assert.That(n.IsGameOver,Is.False);
            s.SetPaused(false); s.Tick(0.03); Assert.That(s.ElapsedSeconds,Is.EqualTo(time+0.05).Within(1e-8));
            Assert.That(n.Snapshot().GeneratedCount,Is.EqualTo(generated));
        }
        [Test] public void PausedEditingAndEmptyDeletionWorkButOccupiedDeletionWaitsForResume()
        {
            var stage=Stage(); var n=new FlowNetwork(stage,new NetworkSettings(20,20,2,10,0)); var s=new FlowSimulation(n,new First());
            n.TryConnect("S","T",new[]{Vector3.zero,new Vector3(20,0,0)}); n.GenerateFlow("S",FlowColor.Red); s.Tick(0.3);
            int id=n.Snapshot().Lines.Single().Id; var before=n.Snapshot().Lines.Single().InFlight.Single(); s.SetPaused(true);
            using var p=new LinePreviewService(n,new GroundRoutePlanner(stage,0)); using var c=new ConnectionSession(n,p);
            c.Begin("R"); c.SelectTarget("T"); p.InsertPoint(0,new Vector3(15,0,5)); Assert.That(c.Confirm(),Is.EqualTo(ConnectionFailure.None));
            int empty=c.LastCreatedLineId!.Value; Assert.That(n.RequestDeletion(empty),Is.True);
            Assert.That(n.Snapshot().Lines.Count,Is.EqualTo(1)); Assert.That(n.RequestDeletion(id),Is.True);
            s.Tick(100); Assert.That(n.Snapshot().Lines.Single().InFlight.Single().Distance,Is.EqualTo(before.Distance));
            Assert.That(n.CancelPending(id),Is.True); Assert.That(n.RequestDeletion(id),Is.True);
            s.SetPaused(false); s.Tick(2); Assert.That(n.Snapshot().Lines,Is.Empty); Assert.That(n.Snapshot().DeliveredCount,Is.EqualTo(1));
        }
        [Test] public void PausedRouteSwitchRetainsOldGeometryAndResumesFromRemainingDistance()
        {
            var n=new FlowNetwork(Stage(),new NetworkSettings(20,20,2,10,0)); var s=new FlowSimulation(n,new First());
            n.TryConnect("S","T",new[]{Vector3.zero,new Vector3(20,0,0)}); n.GenerateFlow("S",FlowColor.Red); s.Tick(0.3);
            var old=n.Snapshot().Lines.Single(); s.SetPaused(true);
            Assert.That(n.RequestRouteChange(old.Id,new[]{Vector3.zero,new Vector3(10,0,10),new Vector3(20,0,0)}),Is.True);
            s.Tick(300); Assert.That(n.Snapshot().Lines.Single().Route,Is.SameAs(old.Route));
            Assert.That(n.Snapshot().Lines.Single().InFlight.Single().Distance,Is.EqualTo(old.InFlight.Single().Distance));
            s.SetPaused(false); s.Tick(0.1);
            Assert.That(n.Snapshot().Lines.Single().InFlight.Single().Distance,Is.EqualTo(old.InFlight.Single().Distance+1).Within(1e-8));
        }
    }
}
