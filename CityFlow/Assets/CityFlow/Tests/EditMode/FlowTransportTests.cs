#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using CityFlow.Application.UseCases;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Domain.Spatial;
using NUnit.Framework;
using UnityEngine;

namespace CityFlow.Tests.EditMode
{
    public sealed class FlowTransportTests
    {
        private sealed class Choices : IRandomSource
        {
            private readonly Queue<int> values;
            public List<int> Bounds { get; } = new List<int>();
            public Choices(params int[] values) => this.values = new Queue<int>(values);
            public int NextIndex(int exclusiveMax)
            { Bounds.Add(exclusiveMax); return values.Count > 0 ? values.Dequeue() : 0; }
        }
        private sealed class Alternating : IRandomSource
        {
            private int index;
            public int NextIndex(int exclusiveMax) => index++ % exclusiveMax;
        }
        private static FlowNetwork Network(int capacity = 2, int buffer = 2, double interval = 1000) =>
            new FlowNetwork(new StageDefinition(0, new Rect(-100, -100, 200, 200), Array.Empty<Bounds>(), new[] {
                new NodeDefinition("S", NodeKind.Source, Vector3.zero, 8, 8, generationInterval: interval),
                new NodeDefinition("R", NodeKind.Relay, new Vector3(10, 0, 0), 8, 8),
                new NodeDefinition("Q", NodeKind.Relay, new Vector3(0, 0, 10), 8, 8),
                new NodeDefinition("T", NodeKind.Sink, new Vector3(20, 0, 0), 8, 8, FlowColor.Red),
                new NodeDefinition("U", NodeKind.Sink, new Vector3(30, 0, 0), 8, 8, FlowColor.Red),
                new NodeDefinition("B", NodeKind.Sink, new Vector3(20, 0, 10), 8, 8, FlowColor.Blue) }),
                new NetworkSettings(buffer,buffer, capacity, 10, 0, overloadGrace: 1000));
        private static int Connect(FlowNetwork network, string from, string to, params Vector3[] via)
        {
            Vector3 start = network.NodeDefinitions.Single(n => n.Id == from).Position;
            Vector3 end = network.NodeDefinitions.Single(n => n.Id == to).Position;
            var result = network.TryConnect(from, to, new[] { start }.Concat(via).Append(end).ToArray());
            Assert.That(result.Succeeded, Is.True, result.Failure.ToString());
            return result.LineId.GetValueOrDefault();
        }
        private static LineSnapshot Line(FlowNetwork network, int id) => network.Snapshot().Lines.Single(l => l.Id == id);
        private static NodeSnapshot Node(FlowNetwork network, string id) => network.Snapshot().Nodes.Single(n => n.Definition.Id == id);
        private static void AssertConserved(FlowNetwork network)
        {
            NetworkSnapshot state = network.Snapshot();
            long[] ids = state.Nodes.SelectMany(n => n.Buffer).Concat(state.Lines.SelectMany(l => l.InFlight).Select(f => f.Flow)).Select(f => f.Id).ToArray();
            Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Length));
            Assert.That(ids.Length + state.DeliveredCount, Is.EqualTo(state.GeneratedCount));
            Assert.That(state.Lines.All(l => l.InFlight.Count <= l.Capacity), Is.True);
        }

        [Test] public void MatchingSinkDirectLineWinsOverRelay()
        {
            var network = Network();
            int relay = Connect(network, "S", "R"), direct = Connect(network, "S", "T");
            network.GenerateFlow("S", FlowColor.Red); network.RouteWaitingFlows(new Choices());
            Assert.That(Line(network, direct).InFlight.Count, Is.EqualTo(1));
            Assert.That(Line(network, relay).InFlight, Is.Empty); AssertConserved(network);
        }
        [Test] public void FullDirectLineWaitsWhileLaterDifferentColorDeparts()
        {
            // Specification 7 proposal: a full matching direct Line never falls back to a Relay.
            var network = Network(capacity: 1);
            int direct = Connect(network, "S", "T"), relay = Connect(network, "S", "R"), blue = Connect(network, "S", "B");
            network.GenerateFlow("S", FlowColor.Red); network.RouteWaitingFlows(new Choices());
            Flow waiting = network.GenerateFlow("S", FlowColor.Red);
            Flow departing = network.GenerateFlow("S", FlowColor.Blue);
            network.RouteWaitingFlows(new Choices());
            Assert.That(Node(network, "S").Buffer.Single().Id, Is.EqualTo(waiting.Id));
            Assert.That(Line(network, blue).InFlight.Single().Flow.Id, Is.EqualTo(departing.Id));
            Assert.That(Line(network, relay).InFlight, Is.Empty);
            Assert.That(Line(network, direct).InFlight.Count, Is.EqualTo(1)); AssertConserved(network);
        }
        [Test] public void MultipleMatchingSinksUseOnlyAvailableDirectLines()
        {
            var network = Network(capacity: 1);
            int first = Connect(network, "S", "T"), second = Connect(network, "S", "U");
            for (int i = 0; i < 3; i++) network.GenerateFlow("S", FlowColor.Red);
            var choices = new Choices(1); network.RouteWaitingFlows(choices);
            Assert.That(choices.Bounds, Is.Empty, "Matching Sink selection is deterministic.");
            Assert.That(Line(network, first).InFlight.Count, Is.EqualTo(1));
            Assert.That(Line(network, second).InFlight.Count, Is.EqualTo(1));
            Assert.That(Node(network, "S").Buffer.Count, Is.EqualTo(1)); AssertConserved(network);
        }
        [TestCase(0)] [TestCase(1)]
        public void RandomBranchingOffersEveryAvailableLineOnce(int choice)
        {
            var network = Network(); int first = Connect(network, "S", "R"), second = Connect(network, "S", "Q");
            network.GenerateFlow("S", FlowColor.Red);
            var choices = new Choices(choice); network.RouteWaitingFlows(choices);
            Assert.That(choices.Bounds, Is.EqualTo(new[] { 2 }));
            Assert.That(Line(network, choice == 0 ? first : second).InFlight.Count, Is.EqualTo(1)); AssertConserved(network);
        }
        [Test] public void RandomBranchingExcludesFullLinesAndNoExitPreservesBuffer()
        {
            var network = Network(capacity: 1); int first = Connect(network, "S", "R"), second = Connect(network, "S", "Q");
            for (int i = 0; i < 3; i++) network.GenerateFlow("S", FlowColor.Red);
            network.RouteWaitingFlows(new Choices(0));
            Assert.That(Line(network, first).InFlight.Count, Is.EqualTo(1));
            Assert.That(Line(network, second).InFlight.Count, Is.EqualTo(1));
            Assert.That(Node(network, "S").Buffer.Count, Is.EqualTo(1)); AssertConserved(network);
        }
        [Test] public void SameCapacityLongRouteArrivesAndReleasesCapacityLater()
        {
            var network = Network(capacity: 1);
            int shortLine = Connect(network, "S", "T");
            int longLine = Connect(network, "S", "B", new Vector3(0, 0, -20), new Vector3(20, 0, -20));
            network.GenerateFlow("S", FlowColor.Red); network.GenerateFlow("S", FlowColor.Blue);
            network.RouteWaitingFlows(new Choices());
            Assert.That(Line(network, longLine).Route.Length, Is.EqualTo(70));
            Assert.That(Line(network, shortLine).Capacity, Is.EqualTo(Line(network, longLine).Capacity));
            network.AdvanceInFlight(1.9);
            Assert.That(network.Snapshot().DeliveredCount, Is.Zero);
            network.AdvanceInFlight(0.1);
            Assert.That(Line(network, shortLine).InFlight, Is.Empty);
            Assert.That(Line(network, longLine).InFlight.Single().Distance, Is.EqualTo(20).Within(1e-8));
            Assert.That(Line(network, longLine).Route.PositionAt(30), Is.EqualTo(new Vector3(10, 0, -20)));
            network.AdvanceInFlight(5);
            Assert.That(network.Snapshot().DeliveredCount, Is.EqualTo(2)); AssertConserved(network);
        }
        [Test] public void UnblockedBurstTravelsAtCommonSpeedWithoutArtificialQueueDelay()
        {
            var network = Network(); int line = Connect(network, "S", "T");
            network.GenerateFlow("S", FlowColor.Red); network.GenerateFlow("S", FlowColor.Red);
            network.RouteWaitingFlows(new Choices());
            for (int i = 0; i < 4; i++) network.AdvanceInFlight(0.5);
            Assert.That(Line(network, line).InFlight, Is.Empty);
            Assert.That(network.Snapshot().DeliveredCount, Is.EqualTo(2)); AssertConserved(network);
        }

        [TestCase(0)] [TestCase(1)]
        public void BlueFlowChoosesOnlyRelaysWhenRedSinkIsAlsoConnected(int choice)
        {
            var network = Network(); int red = Connect(network, "S", "T");
            int first = Connect(network, "S", "R"), second = Connect(network, "S", "Q");
            network.GenerateFlow("S", FlowColor.Blue);
            var choices = new Choices(choice); network.RouteWaitingFlows(choices);
            Assert.That(choices.Bounds, Is.EqualTo(new[] { 2 }));
            Assert.That(Line(network, red).InFlight, Is.Empty);
            Assert.That(Line(network, choice == 0 ? first : second).InFlight.Single().Flow.Color, Is.EqualTo(FlowColor.Blue));
            AssertConserved(network);
        }
        [Test] public void WrongColorSinkWithoutRelayLeavesFlowAtSource()
        {
            var network = Network(); int red = Connect(network, "S", "T");
            Flow blue = network.GenerateFlow("S", FlowColor.Blue); var choices = new Choices();
            network.RouteWaitingFlows(choices); network.AdvanceInFlight(100);
            Assert.That(Line(network, red).InFlight, Is.Empty);
            Assert.That(Node(network, "S").Buffer.Single().Id, Is.EqualTo(blue.Id));
            Assert.That(Node(network, "T").Buffer, Is.Empty); Assert.That(choices.Bounds, Is.Empty);
            Assert.That(network.Snapshot().DeliveredCount, Is.Zero); AssertConserved(network);
        }
        [Test] public void FullRelayDoesNotRedirectFlowToWrongColorSink()
        {
            var network = Network(capacity: 1); int relay = Connect(network, "S", "R");
            int red = Connect(network, "S", "T");
            network.GenerateFlow("S", FlowColor.Blue); network.RouteWaitingFlows(new Choices());
            Flow waiting = network.GenerateFlow("S", FlowColor.Blue); network.RouteWaitingFlows(new Choices());
            Assert.That(Line(network, relay).InFlight.Count, Is.EqualTo(1));
            Assert.That(Line(network, red).InFlight, Is.Empty);
            Assert.That(Node(network, "S").Buffer.Single().Id, Is.EqualTo(waiting.Id)); AssertConserved(network);
        }
        [Test] public void BlockedReceiverRetainsLineOwnershipAndCapacityUntilAccepted()
        {
            var network = Network(); int incoming = Connect(network, "S", "R");
            for (int i = 0; i < 2; i++) network.GenerateFlow("S", FlowColor.Red);
            network.RouteWaitingFlows(new Choices()); network.AdvanceInFlight(2);
            for (int i = 0; i < 2; i++) network.GenerateFlow("S", FlowColor.Blue);
            network.RouteWaitingFlows(new Choices()); network.AdvanceInFlight(2);
            Assert.That(Node(network, "R").Buffer.Count, Is.EqualTo(2));
            Assert.That(Line(network, incoming).InFlight.Count, Is.EqualTo(2));
            Assert.That(Line(network, incoming).InFlight[1].Distance, Is.LessThan(Line(network, incoming).InFlight[0].Distance));
            network.AdvanceInFlight(100);
            Assert.That(Line(network, incoming).InFlight.Count, Is.EqualTo(2));
            Connect(network, "R", "T"); network.RouteWaitingFlows(new Choices()); network.AdvanceInFlight(1);
            Assert.That(Line(network, incoming).InFlight, Is.Empty);
            Assert.That(Node(network, "R").Buffer.Count, Is.EqualTo(2)); AssertConserved(network);
        }
        [Test] public void SinkConsumesMatchingFlowsWithoutBufferOrOutgoingConnections()
        {
            var network = Network(buffer: 1, capacity: 2); int line = Connect(network, "S", "B");
            network.GenerateFlow("S", FlowColor.Blue); network.GenerateFlow("S", FlowColor.Blue);
            network.RouteWaitingFlows(new Choices()); network.AdvanceInFlight(10);
            Assert.That(Node(network, "B").Buffer, Is.Empty);
            Assert.That(Node(network, "B").IsInputStopped, Is.False);
            Assert.That(Node(network, "B").Definition.MaxOutgoing, Is.Zero);
            Assert.That(network.CheckConnection("B", "R"), Is.EqualTo(ConnectionFailure.OutgoingLimit));
            Assert.That(network.Snapshot().DeliveredCount, Is.EqualTo(2));
            Assert.That(Line(network, line).InFlight, Is.Empty); AssertConserved(network);
        }
        [Test] public void SimultaneousIncomingLinesCannotOverfillReceiver()
        {
            var network = Network(buffer: 1, capacity: 1);
            Connect(network, "S", "R"); Connect(network, "S", "Q");
            Connect(network, "Q", "R");
            network.GenerateFlow("S", FlowColor.Red); network.GenerateFlow("S", FlowColor.Red);
            network.RouteWaitingFlows(new Choices(0)); network.AdvanceInFlight(2);
            network.RouteWaitingFlows(new Choices()); network.AdvanceInFlight(2);
            Assert.That(Node(network, "R").Buffer.Count, Is.EqualTo(1));
            Assert.That(network.Snapshot().Lines.Sum(l => l.InFlight.Count), Is.EqualTo(1)); AssertConserved(network);
        }
        [Test] public void TickGeneratesFromDistinctExistingSinkColorsAndRetainsOverflow()
        {
            // Specification 5.2 proposal: own Source generation can exceed SourceBufferCapacity without loss.
            var network = Network(buffer: 1, interval: 0.1);
            var random = new Choices(0, 1, 0, 1); var simulation = new FlowSimulation(network, random);
            simulation.Tick(0.4);
            Assert.That(Node(network, "S").Buffer.Select(f => f.Color), Is.EqualTo(new[] { FlowColor.Red, FlowColor.Blue, FlowColor.Red, FlowColor.Blue }));
            Assert.That(random.Bounds, Is.EqualTo(new[] { 2, 2, 2, 2 }));
            Assert.That(network.Snapshot().Nodes.Where(n => n.Definition.Id != "S").All(n => n.Buffer.Count == 0), Is.True);
            AssertConserved(network);
        }
        [Test] public void TickGeneratesThenMovesExistingFlightsThenDispatchesWithoutDoubleMovement()
        {
            var network = Network(interval: 0.05); int incoming = Connect(network, "S", "R"), outgoing = Connect(network, "R", "T");
            var simulation = new FlowSimulation(network, new Choices()); simulation.Tick(0.05);
            Assert.That(Line(network, incoming).InFlight.Single().Distance, Is.Zero);
            simulation.Tick(1);
            Assert.That(Line(network, outgoing).InFlight.First().Distance, Is.Zero);
            Assert.That(network.Snapshot().DeliveredCount, Is.Zero); AssertConserved(network);
        }
        [TestCase(30)] [TestCase(60)] [TestCase(144)]
        public void FramePartitionDoesNotChangeGeneratedDeliveredOrOwnedFlows(int frameRate)
        {
            FlowNetwork Setup()
            { var result = Network(interval: 0.1); Connect(result, "S", "T"); Connect(result, "S", "B"); return result; }
            var batched = Setup(); var split = Setup();
            var expected = new FlowSimulation(batched, new Alternating()); var actual = new FlowSimulation(split, new Alternating());
            expected.Tick(10);
            for (int i = 0; i < frameRate * 10; i++) { actual.Tick(1d / frameRate); AssertConserved(split); }
            Assert.That(actual.ElapsedSeconds, Is.EqualTo(10).Within(1e-8));
            Assert.That(split.Snapshot().GeneratedCount, Is.EqualTo(100));
            Assert.That(split.Snapshot().DeliveredCount, Is.EqualTo(batched.Snapshot().DeliveredCount));
            Assert.That(split.Snapshot().Nodes.SelectMany(n => n.Buffer).Select(f => f.Id),
                Is.EqualTo(batched.Snapshot().Nodes.SelectMany(n => n.Buffer).Select(f => f.Id)));
            Assert.That(split.Snapshot().Lines.SelectMany(l => l.InFlight).Select(f => (f.Flow.Id, f.Distance)),
                Is.EqualTo(batched.Snapshot().Lines.SelectMany(l => l.InFlight).Select(f => (f.Flow.Id, f.Distance))));
        }
        [TestCase(-1)] [TestCase(double.NaN)] [TestCase(double.PositiveInfinity)]
        public void InvalidDeltaIsRejectedWithoutChangingState(double delta)
        {
            var network = Network(); var simulation = new FlowSimulation(network, new Choices());
            Assert.Throws<ArgumentOutOfRangeException>(() => simulation.Tick(delta));
            Assert.Throws<ArgumentOutOfRangeException>(() => network.AdvanceInFlight(delta));
            Assert.That(network.Snapshot().GeneratedCount, Is.Zero);
        }
        [Test] public void ZeroDeltaDoesNotDispatchOrGenerate()
        {
            var network = Network(); int line = Connect(network, "S", "T"); network.GenerateFlow("S", FlowColor.Red);
            new FlowSimulation(network, new Choices()).Tick(0);
            Assert.That(Line(network, line).InFlight, Is.Empty);
            Assert.That(Node(network, "S").Buffer.Count, Is.EqualTo(1));
        }
    }
}
