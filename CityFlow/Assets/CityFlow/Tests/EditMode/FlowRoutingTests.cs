#nullable enable

using System;
using System.Linq;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Domain.Spatial;
using NUnit.Framework;
using UnityEngine;

namespace CityFlow.Tests.EditMode
{
    public sealed class FlowRoutingTests
    {
        private static FlowNetwork Network(int capacity = 1) => new FlowNetwork(
            new StageDefinition(0, new Rect(-100, -100, 200, 200), Array.Empty<Bounds>(), new NodeDefinition[] {
                new SourceNodeDefinition("S", Vector3.zero, maxOutgoing: 8),
                new RelayNodeDefinition("R", new Vector3(10, 0, 0), maxIncoming: 8, maxOutgoing: 8),
                new RelayNodeDefinition("Q", new Vector3(0, 0, 10), maxIncoming: 8, maxOutgoing: 8),
                new RelayNodeDefinition("V", new Vector3(10, 0, 10), maxIncoming: 8, maxOutgoing: 8),
                new SinkNodeDefinition("T", new Vector3(20, 0, 0), FlowColor.Red, maxIncoming: 8),
                new SinkNodeDefinition("U", new Vector3(30, 0, 0), FlowColor.Red, maxIncoming: 8),
                new SinkNodeDefinition("B", new Vector3(20, 0, 10), FlowColor.Blue, maxIncoming: 8) }),
            new NetworkSettings(10, 5, capacity, 10, 0));

        private static Vector3 Position(FlowNetwork network, string id) =>
            network.NodeDefinitions.Single(n => n.Id == id).Position;

        private static int Connect(FlowNetwork network, string from, string to, params Vector3[] via)
        {
            var result = network.TryConnect(from, to,
                new[] { Position(network, from) }.Concat(via).Append(Position(network, to)).ToArray());
            Assert.That(result.Succeeded, Is.True, result.Failure.ToString());
            return result.LineId.GetValueOrDefault();
        }

        private static LineSnapshot Line(FlowNetwork network, int id) => network.Snapshot().Lines.Single(l => l.Id == id);
        private static NodeSnapshot Node(FlowNetwork network, string id) => network.Snapshot().Nodes.Single(n => n.Definition.Id == id);
        private static Flow Send(FlowNetwork network, FlowColor color = FlowColor.Red)
        {
            Flow flow = network.GenerateFlow("S", color);
            network.RouteWaitingFlows();
            return flow;
        }

        [Test]
        public void LongerDirectSinkStillWinsOverShortRelayPath()
        {
            var network = Network();
            int relay = Connect(network, "S", "R");
            Connect(network, "R", "T");
            int direct = Connect(network, "S", "T", new Vector3(0, 0, -40), new Vector3(20, 0, -40));
            Flow flow = Send(network);
            Assert.That(Line(network, direct).InFlight.Single().Flow.Id, Is.EqualTo(flow.Id));
            Assert.That(Line(network, relay).InFlight, Is.Empty);
            Flow waiting = Send(network);
            Assert.That(Node(network, "S").Buffer.Single().Id, Is.EqualTo(waiting.Id));
        }

        [Test]
        public void RelayChoiceIncludesFirstLineLengthAndPolylineDetours()
        {
            var network = Network();
            int longFirstLeg = Connect(network, "S", "R", new Vector3(-40, 0, 0));
            Connect(network, "R", "T");
            int shorterTotal = Connect(network, "S", "Q");
            Connect(network, "Q", "T");
            Send(network);
            Assert.That(Line(network, shorterTotal).InFlight.Count, Is.EqualTo(1));
            Assert.That(Line(network, longFirstLeg).InFlight, Is.Empty);
        }

        [Test]
        public void ShorterDistanceWinsEvenWithMoreSteps()
        {
            var network = Network();
            int fewerSteps = Connect(network, "S", "R");
            Connect(network, "R", "T", new Vector3(10, 0, -40));
            int shorter = Connect(network, "S", "Q");
            Connect(network, "Q", "V");
            Connect(network, "V", "T");
            Send(network);
            Assert.That(Line(network, shorter).InFlight.Count, Is.EqualTo(1));
            Assert.That(Line(network, fewerSteps).InFlight, Is.Empty);
        }

        [Test]
        public void RemainingDistanceHonorsDownstreamDirectSinkPriority()
        {
            var network = Network();
            int misleading = Connect(network, "S", "R");
            Connect(network, "R", "T", new Vector3(10, 0, -40));
            Connect(network, "R", "V");
            Connect(network, "V", "T");
            int chosen = Connect(network, "S", "Q");
            Connect(network, "Q", "T", new Vector3(-10, 0, 10));
            Send(network);
            Assert.That(Line(network, chosen).InFlight.Count, Is.EqualTo(1));
            Assert.That(Line(network, misleading).InFlight, Is.Empty);
        }

        [Test]
        public void MultipleDirectSinksKeepConnectionOrderInRemainingDistance()
        {
            var network = Network();
            int misleading = Connect(network, "S", "R");
            Connect(network, "R", "U", new Vector3(10, 0, -40));
            Connect(network, "R", "T");
            int chosen = Connect(network, "S", "Q");
            Connect(network, "Q", "T");
            Send(network);
            Assert.That(Line(network, chosen).InFlight.Count, Is.EqualTo(1));
            Assert.That(Line(network, misleading).InFlight, Is.Empty);
        }

        [Test]
        public void UnreachableDirectedCycleWaitsThenResumesAfterSinkConnection()
        {
            var network = Network();
            int incoming = Connect(network, "S", "R");
            Connect(network, "R", "Q");
            Connect(network, "Q", "R");
            Flow flow = Send(network);
            Assert.That(Node(network, "S").Buffer.Single().Id, Is.EqualTo(flow.Id));
            Assert.That(network.Snapshot().Lines.All(l => l.InFlight.Count == 0), Is.True);
            Connect(network, "Q", "T");
            network.RouteWaitingFlows();
            Assert.That(Line(network, incoming).InFlight.Single().Flow.Id, Is.EqualTo(flow.Id));
        }

        [Test]
        public void SinkColorAndLineDirectionDetermineReachability()
        {
            var network = Network();
            int red = Connect(network, "S", "R");
            Connect(network, "R", "T");
            int blue = Connect(network, "S", "Q");
            Connect(network, "Q", "B");
            Connect(network, "R", "Q");
            Send(network, FlowColor.Blue);
            Assert.That(Line(network, blue).InFlight.Count, Is.EqualTo(1));
            Assert.That(Line(network, red).InFlight, Is.Empty);
        }

        [Test]
        public void FullShortestLineWaitsInsteadOfTakingLongerDetour()
        {
            var network = Network();
            int shortest = Connect(network, "S", "R");
            Connect(network, "R", "T");
            int detour = Connect(network, "S", "Q");
            Connect(network, "Q", "T");
            Send(network);
            Flow waiting = Send(network);
            Assert.That(Node(network, "S").Buffer.Single().Id, Is.EqualTo(waiting.Id));
            Assert.That(Line(network, detour).InFlight, Is.Empty);
            network.AdvanceInFlight(1);
            network.RouteWaitingFlows();
            Assert.That(Line(network, shortest).InFlight.Single().Flow.Id, Is.EqualTo(waiting.Id));
        }

        [Test]
        public void EqualDistanceUsesFewerStepsThenAvailableAlternative()
        {
            var network = Network();
            int moreSteps = Connect(network, "S", "Q");
            Connect(network, "Q", "V");
            Connect(network, "V", "B");
            int fewerSteps = Connect(network, "S", "R");
            Connect(network, "R", "B", new Vector3(10, 0, 10));
            Flow first = Send(network, FlowColor.Blue);
            Assert.That(Line(network, fewerSteps).InFlight.Single().Flow.Id, Is.EqualTo(first.Id));
            Flow second = Send(network, FlowColor.Blue);
            Assert.That(Line(network, moreSteps).InFlight.Single().Flow.Id, Is.EqualTo(second.Id));
        }

        [Test]
        public void RelayWaitsRatherThanReturningToPredecessorWhenShortestLineIsFull()
        {
            var network = Network();
            Connect(network, "S", "R");
            int shortest = Connect(network, "R", "V", new Vector3(30, 0, 0));
            Connect(network, "V", "T");
            int backwards = Connect(network, "R", "Q");
            Connect(network, "Q", "R");
            Send(network);
            network.AdvanceInFlight(1);
            network.RouteWaitingFlows();
            Send(network);
            network.AdvanceInFlight(1);
            network.RouteWaitingFlows();
            Assert.That(Node(network, "R").Buffer.Count, Is.EqualTo(1));
            Assert.That(Line(network, backwards).InFlight, Is.Empty);
            Assert.That(Line(network, shortest).InFlight.Count, Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DownstreamReservationAndCancellationRefreshUpstreamChoice(bool changeRoute)
        {
            var network = Network(3);
            int incoming = Connect(network, "S", "R");
            int exit = Connect(network, "R", "T");
            Send(network);
            network.AdvanceInFlight(1);
            network.RouteWaitingFlows();
            Assert.That(Line(network, exit).InFlight.Count, Is.EqualTo(1));
            bool reserved = changeRoute
                ? network.RequestRouteChange(exit, new[] { Position(network, "R"), new Vector3(10, 0, -20), Position(network, "T") })
                : network.RequestDeletion(exit);
            Assert.That(reserved, Is.True);
            Flow waiting = Send(network);
            Assert.That(Node(network, "S").Buffer.Single().Id, Is.EqualTo(waiting.Id));
            Assert.That(network.CancelPending(exit), Is.True);
            network.RouteWaitingFlows();
            Assert.That(Line(network, incoming).InFlight.Single().Flow.Id, Is.EqualTo(waiting.Id));
        }

        [Test]
        public void CompletedRouteChangeRefreshesDistanceWithoutMovingExistingFlow()
        {
            var network = Network(3);
            int incoming = Connect(network, "S", "R");
            int exit = Connect(network, "R", "T");
            int alternative = Connect(network, "S", "Q");
            Connect(network, "Q", "T");
            Send(network);
            network.AdvanceInFlight(1);
            network.RouteWaitingFlows();
            network.AdvanceInFlight(0.2);
            double oldDistance = Line(network, exit).InFlight.Single().Distance;
            Assert.That(network.RequestRouteChange(exit,
                new[] { Position(network, "R"), new Vector3(10, 0, -40), Position(network, "T") }), Is.True);
            Assert.That(Line(network, exit).InFlight.Single().Distance, Is.EqualTo(oldDistance));
            Send(network);
            Assert.That(Line(network, alternative).InFlight.Count, Is.EqualTo(1));
            network.AdvanceInFlight(10);
            Send(network);
            Assert.That(Line(network, alternative).InFlight.Count, Is.EqualTo(1));
            Assert.That(Line(network, incoming).InFlight, Is.Empty);
        }
    }
}
