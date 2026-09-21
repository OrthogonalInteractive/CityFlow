#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Domain.Spatial;
using NUnit.Framework;
using UnityEngine;

namespace CityFlow.Tests.EditMode
{
    public sealed class FlowNetworkTests
    {
        private static readonly Vector3 A = new Vector3(-10, 0, -5);
        private static readonly Vector3 B = new Vector3(10, 0, -5);
        private static readonly Vector3 C = new Vector3(10, 0, 5);
        private static readonly Vector3 D = new Vector3(-10, 0, 5);
        private static FlowNetwork Network(int outgoing = 3, int incoming = 3, bool building = false) =>
            new FlowNetwork(new StageDefinition(0, new Rect(-30, -30, 60, 60), building ?
                new[] { new Bounds(new Vector3(0, 2, 0), new Vector3(4, 4, 4)) } : Array.Empty<Bounds>(),
                new[] { new NodeDefinition("A", NodeKind.Source, A, maxOutgoing: outgoing),
                    new NodeDefinition("B", NodeKind.Relay, B, maxIncoming: incoming),
                    new NodeDefinition("C", NodeKind.Sink, C, sinkColor: FlowColor.Red),
                    new NodeDefinition("D", NodeKind.Sink, D, sinkColor: FlowColor.Blue) }), new NetworkSettings(2, 2, 10, 0.5f));

        [Test] public void DirectedConnectionReservesBothEndsAtomically()
        {
            var network = Network();
            Assert.That(network.TryConnect("A", "B", new[] { A, B }).Succeeded, Is.True);
            var state = network.Snapshot();
            Assert.That(state.Nodes.Single(n => n.Definition.Id == "A").OutgoingUsed, Is.EqualTo(1));
            Assert.That(state.Nodes.Single(n => n.Definition.Id == "B").IncomingUsed, Is.EqualTo(1));
            Assert.That(state.Lines.Single().Capacity, Is.EqualTo(2));
        }
        [TestCase("missing", "B", ConnectionFailure.UnknownSource)]
        [TestCase("A", "missing", ConnectionFailure.UnknownDestination)]
        [TestCase("A", "A", ConnectionFailure.SelfConnection)]
        public void InvalidEndpointReturnsSpecificFailure(string from, string to, ConnectionFailure expected)
        {
            var network = Network();
            Assert.That(network.TryConnect(from, to, new[] { A, B }).Failure, Is.EqualTo(expected));
            Assert.That(network.Snapshot().Lines, Is.Empty);
        }
        [Test] public void DuplicateDirectionIsRejectedButReverseIsIndependent()
        {
            var network = Network();
            network.TryConnect("A", "B", new[] { A, B });
            Assert.That(network.TryConnect("A", "B", new[] { A, B }).Failure, Is.EqualTo(ConnectionFailure.DuplicateDirection));
            Assert.That(network.TryConnect("B", "A", new[] { B, A }).Succeeded, Is.True);
            Assert.That(network.Snapshot().Lines.Count, Is.EqualTo(2));
        }
        [TestCase(0, 3, ConnectionFailure.OutgoingLimit)]
        [TestCase(3, 0, ConnectionFailure.IncomingLimit)]
        public void ExhaustedEndpointDoesNotReserveTheOtherEnd(int outgoing, int incoming, ConnectionFailure failure)
        {
            var network = Network(outgoing, incoming);
            Assert.That(network.TryConnect("A", "B", new[] { A, B }).Failure, Is.EqualTo(failure));
            Assert.That(network.Snapshot().Nodes.All(n => n.IncomingUsed == 0 && n.OutgoingUsed == 0), Is.True);
        }
        [Test] public void ValidControlPointsCannotHideABuildingCrossing()
        {
            var network = Network(building: true);
            Assert.That(network.TryConnect("A", "C", new[] { A, C }).Failure, Is.EqualTo(ConnectionFailure.InvalidRoute));
            Assert.That(network.TryConnect("A", "C", new[] { A, D, C }).Succeeded, Is.True);
        }
        [Test] public void OffGroundAndMismatchedEndpointsAreRejected()
        {
            var network = Network();
            Assert.That(network.TryConnect("A", "B", new[] { A, Vector3.up, B }).Failure, Is.EqualTo(ConnectionFailure.InvalidRoute));
            Assert.That(network.TryConnect("A", "B", new[] { D, B }).Failure, Is.EqualTo(ConnectionFailure.InvalidRoute));
        }
        [Test] public void SnapshotsAndRouteInputCannotMutateNetworkState()
        {
            var network = Network();
            Vector3[] path = { A, B };
            network.TryConnect("A", "B", path);
            path[0] = Vector3.one * 999;
            var before = network.Snapshot();
            Flow flow = network.GenerateFlow("A", FlowColor.Red);
            var after = network.Snapshot();
            Assert.That(before.Nodes.Single(n => n.Definition.Id == "A").Buffer, Is.Empty);
            Assert.That(after.Nodes.SelectMany(n => n.Buffer).Select(f => f.Id), Is.EqualTo(new[] { flow.Id }));
            Assert.That(after.Lines.Single().Route.Points[0], Is.EqualTo(A));
            Assert.Throws<NotSupportedException>(() => ((IList<NodeSnapshot>)after.Nodes).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<Flow>)after.Nodes[0].Buffer).Clear());
        }
        [Test] public void OnlySourcesGenerateExistingSinkColorsWithUniqueIds()
        {
            var network = Network();
            Assert.Throws<ArgumentException>(() => network.GenerateFlow("B", FlowColor.Red));
            Assert.Throws<ArgumentException>(() => network.GenerateFlow("A", FlowColor.Green));
            for (int i = 0; i < 5; i++) network.GenerateFlow("A", FlowColor.Blue);
            Assert.That(network.Snapshot().Nodes.SelectMany(n => n.Buffer).Select(f => f.Id).Distinct().Count(), Is.EqualTo(5));
        }
    }
}
