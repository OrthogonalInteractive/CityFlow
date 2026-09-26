#nullable enable

using System;
using System.Linq;
using CityFlow.Application.Routing;
using CityFlow.Application.Connections;
using CityFlow.Application.UseCases;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Domain.Progression;
using CityFlow.Domain.Spatial;
using CityFlow.Infrastructure.Routing;
using NUnit.Framework;
using UnityEngine;

namespace CityFlow.Tests.EditMode
{
    public sealed class AreaExpansionTests
    {
        private sealed class First : IRandomSource { public int NextIndex(int count) => 0; }
        private static readonly Rect Initial = new(-20, -20, 40, 40);
        private static readonly Rect Expanded = new(-40, -30, 80, 60);
        private static StageDefinition Stage() => new(0, Initial,
            new[] { new Bounds(new Vector3(35, 4, 0), new Vector3(4, 8, 8)) }, new NodeDefinition[] {
                new SourceNodeDefinition("S", new Vector3(-10, 0, 0), generationInterval: 1000),
                new RelayNodeDefinition("R", Vector3.zero, maximumRise: 20),
                new SinkNodeDefinition("T", new Vector3(10, 0, 0), FlowColor.Red) },
            30, new Rect(-60, -50, 120, 100));
        private static FlowNetwork Network(StageDefinition stage) => new(stage, new NetworkSettings(10, 5, 3, 1, 0.5f));
        private static WaveDefinition Wave(Rect area) => new(1, 1, Array.Empty<NodeDefinition>(), area);

        [Test] public void WavesExpandTheSameStageAndPauseDoesNotSkipOrRepeatExpansion()
        {
            var stage = Stage(); var network = Network(stage);
            var sim = new FlowSimulation(network, new First(), new[] { Wave(Expanded),
                new WaveDefinition(2, 1, Array.Empty<NodeDefinition>(), stage.MaximumArea) });
            sim.Tick(0.95); sim.SetPaused(true); sim.Tick(50);
            Assert.That(stage.WalkableArea, Is.EqualTo(Initial));
            sim.SetPaused(false); sim.Tick(0.05);
            Assert.That(stage.WalkableArea, Is.EqualTo(Expanded));
            Assert.That(sim.Wave, Is.EqualTo(2));
            sim.Tick(1); Assert.That(stage.WalkableArea, Is.EqualTo(stage.MaximumArea));
            sim.Tick(1); Assert.That(sim.Wave, Is.EqualTo(3));
        }

        [Test] public void ExpansionPreservesFlightsBuffersRoutesAndBothPendingOperations()
        {
            var stage = Stage(); var n = Network(stage);
            int incoming = n.TryConnect("S", "R", new[] { new Vector3(-10, 0, 0), Vector3.zero }).LineId.GetValueOrDefault();
            int outgoing = n.TryConnect("R", "T", new[] { Vector3.zero, new Vector3(10, 0, 0) }).LineId.GetValueOrDefault();
            n.GenerateFlow("S", FlowColor.Red); n.RouteWaitingFlows(); n.AdvanceInFlight(10); n.RouteWaitingFlows();
            n.GenerateFlow("S", FlowColor.Red); n.RouteWaitingFlows(); n.GenerateFlow("S", FlowColor.Red);
            Assert.That(n.RequestRouteChange(incoming, new[] { new Vector3(-10, 0, 0), new Vector3(-5, 0, 5), Vector3.zero }), Is.True);
            Assert.That(n.RequestDeletion(outgoing), Is.True);
            var sim = new FlowSimulation(n, new First(), new[] { Wave(Expanded) });
            sim.Tick(0.95); var before = n.Snapshot(); sim.Tick(0.05); var after = n.Snapshot();
            Assert.That(stage.WalkableArea, Is.EqualTo(Expanded));
            Assert.That(after.GeneratedCount, Is.EqualTo(before.GeneratedCount));
            foreach (var line in before.Lines)
            {
                var same = after.Lines.Single(l => l.Id == line.Id);
                Assert.That(same.Route, Is.SameAs(line.Route));
                Assert.That(same.Status, Is.EqualTo(line.Status));
                Assert.That(same.InFlight.Single().Flow.Id, Is.EqualTo(line.InFlight.Single().Flow.Id));
                Assert.That(same.InFlight.Single().Distance, Is.EqualTo(line.InFlight.Single().Distance + 0.05).Within(0.00001));
            }
            Assert.That(after.Nodes.SelectMany(n => n.Buffer).Select(f => f.Id),
                Is.EqualTo(before.Nodes.SelectMany(n => n.Buffer).Select(f => f.Id)));
        }

        [Test] public void ElevatedNewNodeCanConnectAcrossTheOldBoundaryButNotThroughBuildingsOrOutsideTheNewArea()
        {
            var stage = Stage(); var n = Network(stage);
            var added = new RelayNodeDefinition("NEW", new Vector3(30, 12, 15), maximumRise: 8);
            var sim = new FlowSimulation(n, new First(), new[] { new WaveDefinition(1, 1, new[] { added }, Expanded) });
            Assert.That(n.NodeDefinitions.Any(node => node.Id == "NEW"), Is.False);
            sim.Tick(1);
            var planner = new LineRoutePlanner(stage, 0.5f);
            var route = planner.Generate(n.NodeDefinitions.Single(node => node.Id == "R"), added);
            Assert.That(route.IsValid, Is.True);
            Assert.That(n.TryConnect("R", "NEW", route.Route!.Points).Succeeded, Is.True);
            Assert.That(stage.ValidatePoint(new Vector3(35, 4, 0), 0.5f), Is.EqualTo(RouteFailure.Obstacle));
            Assert.That(stage.ValidatePoint(new Vector3(45, 0, 0), 0.5f), Is.EqualTo(RouteFailure.OutsideArea));
        }

        [Test] public void PreviewConfirmationRevalidatesAgainstTheExpandedArea()
        {
            var stage = Stage(); var n = Network(stage);
            using var preview = new LinePreviewService(n, new LineRoutePlanner(stage, 0.5f));
            preview.Generate("S", "T");
            preview.UpdatePoints(new[] { new Vector3(-10, 0, 0), new Vector3(-10, 0, 25), new Vector3(10, 0, 25), new Vector3(10, 0, 0) });
            Assert.That(preview.Current!.CanConfirm, Is.False);
            var sim = new FlowSimulation(n, new First(), new[] { Wave(Expanded) }); sim.Tick(1);
            Assert.That(preview.TryConfirm(out _), Is.EqualTo(ConnectionFailure.None));
        }

        [Test] public void InvalidExpansionSchedulesAreRejectedBeforeChangingLiveState()
        {
            foreach (Rect area in new[] { new Rect(-10, -10, 20, 20), new Rect(-100, -50, 200, 100), new Rect(float.NaN, 0, 10, 10) })
            {
                var stage = Stage(); var n = Network(stage);
                Assert.Throws<ArgumentException>(() => new FlowSimulation(n, new First(), new[] { Wave(area) }));
                Assert.That(stage.WalkableArea, Is.EqualTo(Initial));
            }
            var unchanged = Stage(); var network = Network(unchanged);
            Assert.Throws<ArgumentException>(() => new FlowSimulation(network, new First(), new[] { Wave(Expanded),
                new WaveDefinition(2, 1, Array.Empty<NodeDefinition>(), Initial) }));
            Assert.That(unchanged.WalkableArea, Is.EqualTo(Initial));
        }

        [Test] public void InvalidNodeAdditionDoesNotPartiallyUnlockSpaceAndDistanceBandsFollowSuccessfulExpansion()
        {
            var stage = Stage(); var network = Network(stage);
            using var preview = new LinePreviewService(network, new LineRoutePlanner(stage, 0.5f));
            using var session = new ConnectionSession(network, preview, 20, 40);
            float oldNear = session.NearLimit;
            Assert.That(network.TryAddNodes(new[] { new RelayNodeDefinition("BAD", new Vector3(35, 0, 0)) }, Expanded), Is.False);
            Assert.That(stage.WalkableArea, Is.EqualTo(Initial));
            Assert.That(network.NodeDefinitions.Count, Is.EqualTo(3));
            Assert.That(network.TryAddNodes(Array.Empty<NodeDefinition>(), Expanded), Is.True);
            Assert.That(session.NearLimit, Is.EqualTo(oldNear * Expanded.size.magnitude / Initial.size.magnitude).Within(0.001f));
            Assert.That(session.MidLimit, Is.EqualTo(session.NearLimit * 2).Within(0.001f));
        }
    }
}
