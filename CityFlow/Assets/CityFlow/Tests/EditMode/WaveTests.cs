#nullable enable
using System;
using System.Linq;
using CityFlow.Application.UseCases;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Domain.Progression;
using CityFlow.Domain.Spatial;
using NUnit.Framework;
using UnityEngine;
namespace CityFlow.Tests.EditMode
{
    public sealed class WaveTests
    {
        private sealed class Last : IRandomSource { public int NextIndex(int count)=>count-1; }
        private static FlowNetwork Network(double interval=1000,int capacity=1000,double grace=5)=>new FlowNetwork(
            new StageDefinition(0,new Rect(-50,-50,100,100),Array.Empty<Bounds>(),new[]{
                new NodeDefinition("S",NodeKind.Source,Vector3.zero,3,3,generationInterval:interval),
                new NodeDefinition("R",NodeKind.Relay,new Vector3(10,0,0)),
                new NodeDefinition("T",NodeKind.Sink,new Vector3(20,0,0),3,3,FlowColor.Red)}),new NetworkSettings(capacity,capacity,2,1,0,grace));
        private static WaveDefinition Wave(double at,double delay=2)=>new WaveDefinition(at,1,new[]{
            new NodeDefinition("GREEN",NodeKind.Sink,new Vector3(-10,0,0),3,3,FlowColor.Green),
            new NodeDefinition("NEW",NodeKind.Source,new Vector3(-20,0,0),3,3,generationInterval:0.25,generationDelay:delay)});
        [Test] public void WavePreservesExistingLineBufferFlightsAndDeletionReservation()
        {
            var n=Network(); n.TryConnect("S","R",new[]{Vector3.zero,new Vector3(10,0,0)});
            n.GenerateFlow("S",FlowColor.Red); var sim=new FlowSimulation(n,new Last(),new[]{Wave(2)}); sim.Tick(1);
            var old=n.Snapshot().Lines.Single(); n.RequestDeletion(old.Id); n.GenerateFlow("S",FlowColor.Red); sim.Tick(0.95);
            var before=n.Snapshot(); sim.Tick(0.05); var after=n.Snapshot();
            Assert.That(sim.Wave,Is.EqualTo(2)); Assert.That(after.Nodes.Count,Is.EqualTo(5));
            Assert.That(after.Lines.Single().Id,Is.EqualTo(old.Id)); Assert.That(after.Lines.Single().Route,Is.SameAs(old.Route));
            Assert.That(after.Lines.Single().Status,Is.EqualTo(LineStatus.DeletePending));
            Assert.That(after.Lines.Single().InFlight.Single().Flow.Id,Is.EqualTo(old.InFlight.Single().Flow.Id));
            Assert.That(after.Lines.Single().InFlight.Single().Distance,Is.EqualTo(before.Lines.Single().InFlight.Single().Distance+0.05).Within(1e-8));
            Assert.That(after.Nodes[0].Buffer.Select(f=>f.Id),Is.EqualTo(before.Nodes[0].Buffer.Select(f=>f.Id)));
            Assert.That(after.GeneratedCount,Is.EqualTo(before.GeneratedCount));
        }
        [Test] public void NewColorAppearsOnlyAfterSinkAndNewSourceWaitsForGrace()
        {
            var n=Network(0.1); var sim=new FlowSimulation(n,new Last(),new[]{Wave(1)}); sim.Tick(0.95);
            Assert.That(n.Snapshot().Nodes.SelectMany(x=>x.Buffer).All(f=>f.Color==FlowColor.Red),Is.True);
            sim.Tick(0.05); Assert.That(n.NodeDefinitions.Any(x=>x.Id=="GREEN"),Is.True);
            Assert.That(n.Snapshot().Nodes[0].Buffer.Any(f=>f.Color==FlowColor.Green),Is.True);
            sim.Tick(2.2); Assert.That(n.Snapshot().Nodes.Single(x=>x.Definition.Id=="NEW").Buffer,Is.Empty);
            sim.Tick(0.05); Assert.That(n.Snapshot().Nodes.Single(x=>x.Definition.Id=="NEW").Buffer.Count,Is.EqualTo(1));
        }
        [Test] public void PauseFreezesWaveAdditionAndSourceWarmup()
        {
            var n=Network(); var sim=new FlowSimulation(n,new Last(),new[]{Wave(0.5)}); sim.Tick(0.3);
            sim.SetPaused(true); sim.Tick(100); Assert.That(sim.Wave,Is.EqualTo(1)); Assert.That(n.NodeDefinitions.Count,Is.EqualTo(3));
            sim.SetPaused(false); sim.Tick(0.2); Assert.That(sim.Wave,Is.EqualTo(2));
            double remaining=sim.SourceStartRemaining("NEW"); Assert.That(remaining,Is.GreaterThan(0));
            sim.SetPaused(true); sim.Tick(100); Assert.That(sim.SourceStartRemaining("NEW"),Is.EqualTo(remaining));
            sim.SetPaused(false); sim.Tick(2.25); Assert.That(n.Snapshot().Nodes.Single(x=>x.Definition.Id=="NEW").Buffer.Count,Is.EqualTo(1));
        }
        [Test] public void GameOverResultCapturesWaveTimeDeliveredAndCauseThenFreezes()
        {
            var n=Network(1000,2,1); var sim=new FlowSimulation(n,new Last(),new[]{Wave(0.5),new WaveDefinition(3,1,Array.Empty<NodeDefinition>())});
            n.GenerateFlow("S",FlowColor.Red); n.GenerateFlow("S",FlowColor.Red); sim.Tick(10);
            Assert.That(sim.Result,Is.Not.Null); var result=sim.Result!;
            Assert.That(result.Wave,Is.EqualTo(2)); Assert.That(result.SurvivalSeconds,Is.EqualTo(1).Within(1e-8));
            Assert.That(result.Delivered,Is.Zero); Assert.That(result.SourceId,Is.EqualTo("S"));
            sim.Tick(100); Assert.That(sim.Result,Is.SameAs(result)); Assert.That(sim.Wave,Is.EqualTo(2));
        }
        [Test] public void InitialSourceAlsoReceivesConfiguredPreparationTime()
        {
            var stage=new StageDefinition(0,new Rect(-50,-50,100,100),Array.Empty<Bounds>(),new[]{
                new NodeDefinition("S",NodeKind.Source,Vector3.zero,generationInterval:1,generationDelay:3),
                new NodeDefinition("T",NodeKind.Sink,new Vector3(10,0,0),sinkColor:FlowColor.Red)});
            var n=new FlowNetwork(stage,new NetworkSettings(20,20,2,10,0)); var sim=new FlowSimulation(n,new Last());
            sim.Tick(3.95); Assert.That(n.Snapshot().GeneratedCount,Is.Zero); sim.Tick(0.05); Assert.That(n.Snapshot().GeneratedCount,Is.EqualTo(1));
        }
        [Test] public void FasterWavePreservesSourcePreparationAndRemainingGenerationPhase()
        {
            var stage=new StageDefinition(0,new Rect(-50,-50,100,100),Array.Empty<Bounds>(),new[]{
                new NodeDefinition("S",NodeKind.Source,Vector3.zero,generationInterval:2,generationDelay:3),
                new NodeDefinition("T",NodeKind.Sink,new Vector3(10,0,0),sinkColor:FlowColor.Red)});
            var n=new FlowNetwork(stage,new NetworkSettings(20,20,2,10,0)); var sim=new FlowSimulation(n,new Last(),new[]{
                new WaveDefinition(1,0.5,Array.Empty<NodeDefinition>()),new WaveDefinition(4.5,0.25,Array.Empty<NodeDefinition>())});
            sim.Tick(3.95); Assert.That(n.Snapshot().GeneratedCount,Is.Zero); sim.Tick(0.05); Assert.That(n.Snapshot().GeneratedCount,Is.EqualTo(1));
            sim.Tick(0.7); Assert.That(n.Snapshot().GeneratedCount,Is.EqualTo(1)); sim.Tick(0.05); Assert.That(n.Snapshot().GeneratedCount,Is.EqualTo(2));
        }
        [Test] public void InvalidScheduleOrDuplicateAdditionFailsBeforeMutatingNetwork()
        {
            var n=Network(); Assert.Throws<ArgumentException>(()=>new FlowSimulation(n,new Last(),new[]{Wave(2),Wave(1)}));
            Assert.Throws<ArgumentException>(()=>new FlowSimulation(n,new Last(),new[]{Wave(1),Wave(2)}));
            Assert.That(n.NodeDefinitions.Count,Is.EqualTo(3));
            Assert.That(n.TryAddNodes(new[]{new NodeDefinition("S",NodeKind.Relay,new Vector3(2,0,0))}),Is.False);
            Assert.That(n.NodeDefinitions.Count,Is.EqualTo(3));
        }
        [Test] public void AuthoredWiringAssetStartsEmptyAndValidatesAllSpawnRoutes()
        {
            var stage=UnityEditor.AssetDatabase.LoadAssetAtPath<CityFlow.Infrastructure.Configuration.StageConfiguration>("Assets/CityFlow/Settings/Gameplay/WiringStage.asset");
            var settings=UnityEditor.AssetDatabase.LoadAssetAtPath<CityFlow.Infrastructure.Configuration.GameplaySettings>("Assets/CityFlow/Settings/Gameplay/ValidationGameplay.asset");
            var initial=stage.Load(settings.Clearance); var waves=stage.LoadWaves(initial,settings.Clearance);
            Assert.That(stage.Lines,Is.Empty); Assert.That(initial.Nodes.First(n=>n.Kind==NodeKind.Source).GenerationDelay,Is.EqualTo(15));
            Assert.That(waves.Count,Is.EqualTo(3));
            Assert.That(initial.Nodes.Concat(waves.SelectMany(w=>w.Additions)).Where(n=>n.SinkColor.HasValue).Select(n=>n.SinkColor).Distinct().Count(),Is.EqualTo(5));
        }
    }
}
