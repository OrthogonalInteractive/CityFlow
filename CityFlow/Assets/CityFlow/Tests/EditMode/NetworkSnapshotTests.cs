#nullable enable

using System;
using System.Linq;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Domain.Spatial;
using NUnit.Framework;
using UnityEngine;

namespace CityFlow.Tests.EditMode
{
    public sealed class NetworkSnapshotTests
    {
        private static readonly Vector3 A = new(-10, 0, 0), B = new(10, 0, 0), C = new(10, 0, 10);
        private sealed class First : IRandomSource { public int NextIndex(int count) => 0; }
        private static FlowNetwork Network() => new(new StageDefinition(0, new Rect(-30, -30, 60, 60), Array.Empty<Bounds>(),
            new[] { new NodeDefinition("A", NodeKind.Source, A), new NodeDefinition("B", NodeKind.Relay, B),
                new NodeDefinition("C", NodeKind.Sink, C, sinkColor: FlowColor.Red) }), new NetworkSettings(2, 2, 2, 10, 0.5f));

        [Test] public void RepeatedReadsReuseImmutableStateAndRejectedCommandsDoNotReplaceIt()
        {
            var network = Network();
            var before = network.Snapshot();
            Assert.That(network.Snapshot(), Is.SameAs(before));
            Assert.That(network.NodeDefinitions, Is.SameAs(network.NodeDefinitions));
            Assert.That(network.TryConnect("missing", "C", new[] { A, C }).Succeeded, Is.False);
            Assert.That(network.RequestDeletion(100), Is.False);
            Assert.That(network.Snapshot(), Is.SameAs(before));
        }
        [Test] public void GenerationConnectionRoutingAndTransportPublishFreshStateWithoutChangingHistory()
        {
            var network = Network();
            var empty = network.Snapshot();
            network.GenerateFlow("A", FlowColor.Red);
            var buffered = network.Snapshot();
            Assert.That(buffered.GeneratedCount, Is.EqualTo(1));
            network.TryConnect("A", "C", new[] { A, C });
            var connected = network.Snapshot();
            Assert.That(connected.Lines.Count, Is.EqualTo(1));
            network.RouteWaitingFlows(new First());
            var departed = network.Snapshot();
            Assert.That(departed.Nodes[0].Buffer, Is.Empty);
            Assert.That(departed.Lines[0].InFlight.Count, Is.EqualTo(1));
            network.AdvanceInFlight(0.5);
            var moving = network.Snapshot();
            Assert.That(moving.Lines[0].InFlight[0].Distance, Is.EqualTo(5));
            network.AdvanceInFlight(10);
            var delivered = network.Snapshot();
            Assert.That(delivered.DeliveredCount, Is.EqualTo(1));
            Assert.That(delivered.Lines[0].InFlight, Is.Empty);
            Assert.That(network.Snapshot(), Is.SameAs(delivered));
            Assert.That(empty.GeneratedCount, Is.Zero);
            Assert.That(buffered.Lines, Is.Empty);
            Assert.That(buffered.Nodes[0].Buffer.Count, Is.EqualTo(1));
            Assert.That(connected.Lines[0].InFlight, Is.Empty);
            Assert.That(departed.Lines[0].InFlight[0].Distance, Is.Zero);
            Assert.That(moving.Lines[0].InFlight.Count, Is.EqualTo(1));
        }
        [Test] public void PausedLifecycleCommandsAreVisibleImmediatelyAndOldRoutesStayImmutable()
        {
            var network = Network();
            int id = network.TryConnect("A", "B", new[] { A, B }).LineId ?? throw new AssertionException("Missing Line");
            network.GenerateFlow("A", FlowColor.Red);
            network.RouteWaitingFlows(new First());
            var running = network.Snapshot();
            network.RequestDeletion(id);
            var deleting = network.Snapshot();
            Assert.That(deleting.Lines[0].Status, Is.EqualTo(LineStatus.DeletePending));
            network.CancelPending(id);
            Assert.That(network.Snapshot().Lines[0].Status, Is.EqualTo(LineStatus.Running));
            network.RequestRouteChange(id, new[] { A, new Vector3(-10, 0, -10), new Vector3(10, 0, -10), B });
            var pending = network.Snapshot();
            Assert.That(pending.Lines[0].PendingRoute, Is.Not.Null);
            network.AdvanceInFlight(10);
            var changed = network.Snapshot();
            Assert.That(changed.Lines[0].Route.Length, Is.EqualTo(40));
            Assert.That(changed.Lines[0].PendingRoute, Is.Null);
            network.RequestDeletion(id);
            Assert.That(network.Snapshot().Lines, Is.Empty);
            Assert.That(network.Snapshot().Nodes[0].OutgoingUsed, Is.Zero);
            Assert.That(running.Lines[0].Status, Is.EqualTo(LineStatus.Running));
            Assert.That(deleting.Lines[0].Status, Is.EqualTo(LineStatus.DeletePending));
            Assert.That(pending.Lines[0].Route.Length, Is.EqualTo(20));
            Assert.That(pending.Lines[0].InFlight.Count, Is.EqualTo(1));
        }
        [Test] public void WaveRosterAndOverloadRecoveryRefreshWithoutMutatingEarlierReads()
        {
            var network = Network();
            var before = network.Snapshot();
            var roster = network.NodeDefinitions;
            Assert.That(network.TryAddNodes(new[] { new NodeDefinition("D", NodeKind.Sink, new Vector3(-10, 0, 10), sinkColor: FlowColor.Blue) }), Is.True);
            Assert.That(network.Snapshot().Nodes.Count, Is.EqualTo(4));
            Assert.That(network.NodeDefinitions.Count, Is.EqualTo(4));
            Assert.That(before.Nodes.Count, Is.EqualTo(3));
            Assert.That(roster.Count, Is.EqualTo(3));
            network.GenerateFlow("A", FlowColor.Red); network.GenerateFlow("A", FlowColor.Red);
            network.EvaluateOverload(1);
            var warning = network.Snapshot();
            Assert.That(warning.Nodes[0].OverloadSeconds, Is.EqualTo(1));
            network.TryConnect("A", "C", new[] { A, C });
            network.RouteWaitingFlows(new First());
            network.EvaluateOverload(0.1);
            Assert.That(network.Snapshot().Nodes[0].OverloadSeconds, Is.Zero);
            Assert.That(warning.Nodes[0].OverloadSeconds, Is.EqualTo(1));
        }
    }
}
