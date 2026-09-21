#nullable enable

using System;
using System.Linq;
using CityFlow.Application.UseCases;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Domain.Spatial;
using NUnit.Framework;
using UnityEngine;

namespace CityFlow.Tests.EditMode
{
    public sealed class CongestionTests
    {
        private sealed class First : IRandomSource { public int NextIndex(int count) => 0; }
        private static FlowNetwork Create() => new FlowNetwork(new StageDefinition(0,
            new Rect(-50, -50, 100, 100), Array.Empty<Bounds>(), new[] {
                new NodeDefinition("S", NodeKind.Source, Vector3.zero, 4, 4, generationInterval: 1000),
                new NodeDefinition("R", NodeKind.Relay, new Vector3(10, 0, 0), 4, 4),
                new NodeDefinition("T", NodeKind.Sink, new Vector3(20, 0, 0), 4, 4, FlowColor.Red) }),
            new NetworkSettings(2, 2, 10, 0));
        private static NodeSnapshot Node(FlowNetwork n, string id) => n.Snapshot().Nodes.Single(x => x.Definition.Id == id);
        private static void Fill(FlowNetwork n)
        { n.GenerateFlow("S", FlowColor.Red); n.GenerateFlow("S", FlowColor.Red); }
        private static void Connect(FlowNetwork n, string from, string to) => Assert.That(n.TryConnect(from, to,
            new[] { Node(n, from).Definition.Position, Node(n, to).Definition.Position }).Succeeded, Is.True);
        private static void Conserve(FlowNetwork n)
        {
            var s = n.Snapshot();
            var ids = s.Nodes.SelectMany(x => x.Buffer).Select(x => x.Id)
                .Concat(s.Lines.SelectMany(x => x.InFlight).Select(x => x.Flow.Id)).ToArray();
            Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Length));
            Assert.That(ids.Length + s.DeliveredCount, Is.EqualTo(s.GeneratedCount));
        }
        [Test] public void SourceAtThresholdLosesOnlyAfterContinuousGrace()
        {
            // Specification 5.2 proposal: equality starts overload and five continuous seconds cause defeat.
            var n = Create(); Fill(n); n.EvaluateOverload(4.95);
            Assert.That(Node(n, "S").IsInputStopped, Is.True);
            Assert.That(Node(n, "S").OverloadSeconds, Is.EqualTo(4.95).Within(1e-8));
            Assert.That(n.IsGameOver, Is.False);
            n.EvaluateOverload(0.05);
            Assert.That(n.IsGameOver, Is.True); Assert.That(n.GameOverSourceId, Is.EqualTo("S")); Conserve(n);
        }
        [Test] public void RecoveryResetsGraceAndNewOverloadStartsFromZero()
        {
            var n = Create(); Fill(n); n.EvaluateOverload(4);
            Connect(n, "S", "T"); n.RouteWaitingFlows(new First()); n.EvaluateOverload(0.05);
            Assert.That(Node(n, "S").IsInputStopped, Is.False);
            Assert.That(Node(n, "S").OverloadSeconds, Is.Zero);
            Fill(n); n.EvaluateOverload(1);
            Assert.That(n.IsGameOver, Is.False);
            Assert.That(Node(n, "S").OverloadSeconds, Is.EqualTo(1)); Conserve(n);
        }
        [Test] public void RelayBackpressureStopsFlightsAndRecoveryRetainsIds()
        {
            var n = Create(); Connect(n, "S", "R"); Fill(n); n.RouteWaitingFlows(new First()); n.AdvanceInFlight(2);
            Fill(n); n.RouteWaitingFlows(new First()); n.AdvanceInFlight(2); n.EvaluateOverload(100);
            Assert.That(n.IsGameOver, Is.False); Assert.That(Node(n, "R").IsInputStopped, Is.True);
            var blocked = n.Snapshot().Lines[0].InFlight;
            Assert.That(blocked.All(x => x.IsStopped), Is.True);
            Assert.That(blocked[0].Distance, Is.GreaterThan(blocked[1].Distance));
            n.AdvanceInFlight(2);
            Assert.That(n.Snapshot().Lines[0].InFlight.Select(x => (x.Flow.Id, x.Distance)),
                Is.EqualTo(blocked.Select(x => (x.Flow.Id, x.Distance))));
            Connect(n, "R", "T"); n.RouteWaitingFlows(new First());
            Assert.That(Node(n, "R").IsInputStopped, Is.False);
            n.AdvanceInFlight(2);
            Assert.That(n.Snapshot().Lines[0].InFlight, Is.Empty);
            Assert.That(Node(n, "R").Buffer.Select(x => x.Id), Is.EqualTo(blocked.Select(x => x.Flow.Id))); Conserve(n);
        }
        [Test] public void SourceOwnGenerationRetainsOverflowDuringGrace()
        {
            var n = Create(); Fill(n); n.GenerateFlow("S", FlowColor.Red); n.EvaluateOverload(1);
            Assert.That(Node(n, "S").Buffer.Count, Is.EqualTo(3));
            Assert.That(Node(n, "S").OverloadSeconds, Is.EqualTo(1)); Assert.That(n.IsGameOver, Is.False); Conserve(n);
        }
        [Test] public void SimulationStopsAtDefeatWithoutDiscardingOrAdvancingFlows()
        {
            var n = Create(); Fill(n); var sim = new FlowSimulation(n, new First()); sim.Tick(20);
            Assert.That(n.IsGameOver, Is.True); Assert.That(sim.ElapsedSeconds, Is.EqualTo(5).Within(1e-8));
            var before = n.Snapshot(); sim.Tick(100);
            Assert.That(sim.ElapsedSeconds, Is.EqualTo(5).Within(1e-8));
            Assert.That(n.Snapshot().GeneratedCount, Is.EqualTo(before.GeneratedCount)); Conserve(n);
        }
        [TestCase(-1)] [TestCase(double.NaN)] [TestCase(double.PositiveInfinity)]
        public void InvalidOverloadDeltaDoesNotMutateTimers(double delta)
        {
            var n = Create(); Fill(n);
            Assert.Throws<ArgumentOutOfRangeException>(() => n.EvaluateOverload(delta));
            Assert.That(Node(n, "S").OverloadSeconds, Is.Zero);
        }
    }
}
