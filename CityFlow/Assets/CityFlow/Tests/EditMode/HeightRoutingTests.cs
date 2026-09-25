#nullable enable

using System;
using System.Linq;
using CityFlow.Application.Routing;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Domain.Spatial;
using CityFlow.Infrastructure.Configuration;
using CityFlow.Infrastructure.Routing;
using NUnit.Framework;
using UnityEngine;

namespace CityFlow.Tests.EditMode
{
    public sealed class HeightRoutingTests
    {
        private static readonly Vector3 A = new(-15, 0, 0), B = new(15, 0, 0);
        private static RelayNodeDefinition Relay(string id, Vector3 position, float rise = 30) => new(id, position, maximumRise: rise);
        private static StageDefinition Stage(NodeDefinition a, NodeDefinition b, params Bounds[] buildings) =>
            new(0, new Rect(-40, -40, 80, 80), buildings, new[] { a, b }, 40);
        private static FlowNetwork Network(StageDefinition stage) => new(stage, new NetworkSettings(10, 5, 3, 1, 0.5f));

        [Test] public void VerticalAndPlanarSegmentsUseTheirActualLengthAndInterpolation()
        {
            var route = new LineRoute(new[] { Vector3.zero, new Vector3(0, 4, 0), new Vector3(3, 4, 0) });
            Assert.That(route.Length, Is.EqualTo(7));
            Assert.That(route.PositionAt(2), Is.EqualTo(new Vector3(0, 2, 0)));
            Assert.That(route.PositionAt(5), Is.EqualTo(new Vector3(1, 4, 0)));
        }

        [Test] public void RoofNodesAndAltitudeBoundsKeepTheirSpatialConstraints()
        {
            var stage = Stage(Relay("A", A), new SinkNodeDefinition("B", new Vector3(0, 7, 0), FlowColor.Red),
                new Bounds(new Vector3(0, 3, 0), new Vector3(8, 6, 8)));
            Assert.DoesNotThrow(() => stage.Validate(0.5f));
            Assert.That(stage.IsWalkable(new Vector3(0, 6.4f, 0), 0.5f), Is.False);
            Assert.That(stage.IsWalkable(new Vector3(0, -0.01f, 0), 0.5f), Is.False);
            Assert.That(stage.IsWalkable(new Vector3(0, 40.01f, 0), 0.5f), Is.False);
            Assert.That(stage.IsWalkable(new Vector3(0, 40, 0), 0.5f), Is.True);
        }

        [TestCase(6f, 60f, true)]
        [TestCase(25f, 8f, false)]
        public void AutomaticRouteComparesVerticalLiftCostWithGroundDetour(float height, float width, bool overpass)
        {
            var a = Relay("A", A); var b = Relay("B", B);
            var stage = Stage(a, b, new Bounds(new Vector3(0, height / 2, 0), new Vector3(8, height, width)));
            var planner = new LineRoutePlanner(stage, 0.5f);
            var result = planner.Generate(a, b);
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Route!.Points.Any(p => p.y > height), Is.EqualTo(overpass));
            Assert.That(planner.Validate(a, b, result.Route.Points).IsValid, Is.True);
            Assert.That(planner.Generate(a, b).Route!.Points, Is.EqualTo(result.Route.Points));
            foreach (var pair in result.Route.Points.Zip(result.Route.Points.Skip(1), (x, y) => (x, y)))
                Assert.That(pair.x.y == pair.y.y || (pair.x.x == pair.y.x && pair.x.z == pair.y.z), Is.True);
        }

        [TestCase(6f, 10f, false)] [TestCase(10f, 6f, false)] [TestCase(10f, 10f, true)]
        public void BothEndpointLiftLimitsConstrainCrossingAFullWidthWall(float fromRise, float toRise, bool reachable)
        {
            var a = Relay("A", A, fromRise); var b = Relay("B", B, toRise);
            var planner = new LineRoutePlanner(Stage(a, b,
                new Bounds(new Vector3(0, 4, 0), new Vector3(8, 8, 90))), 0.5f);
            Assert.That(planner.Generate(a, b).IsValid, Is.EqualTo(reachable));
        }

        [Test] public void RelayLimitIsRelativeToItsPlacementAndStillClampedByTheStage()
        {
            var a = Relay("A", new Vector3(-15, 20, 0), 10);
            var b = new SinkNodeDefinition("B", new Vector3(15, 30, 0), FlowColor.Red);
            var planner = new LineRoutePlanner(Stage(a, b), 0.5f);
            Assert.That(planner.Generate(a, b).Route!.Points, Is.EqualTo(new[] { a.Position, new Vector3(-15, 30, 0), b.Position }));
            Assert.That(planner.Generate(a, new SinkNodeDefinition("C", new Vector3(15, 31, 0), FlowColor.Red)).Failure,
                Is.EqualTo(RouteFailure.RelayHeightLimit));
            var high = Relay("H", A, 100);
            var stage = Stage(high, Relay("L", B, 100));
            Assert.That(stage.ConnectionCeiling(high), Is.EqualTo(40));
        }

        [TestCase(-1f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void InvalidRelayLiftLimitsCannotEnterTheStage(float rise)
        {
            Assert.Throws<ArgumentException>(() => Stage(Relay("A", A, rise), Relay("B", B)).Validate(0.5f));
        }

        [Test] public void SourceFeedsRaisedRelayColumnAtItsOwnFixedHeight()
        {
            var source = new SourceNodeDefinition("S", new Vector3(-15, 12, 0));
            var relay = Relay("R", B, 12);
            var sink = new SinkNodeDefinition("D", new Vector3(25, 0, 0), FlowColor.Red);
            var stage = new StageDefinition(0, new Rect(-40, -40, 80, 80), Array.Empty<Bounds>(), new NodeDefinition[] { source, relay, sink }, 40);
            var result = new LineRoutePlanner(stage, 0.5f).Generate(source, relay);
            Assert.That(result.Route!.Points, Is.EqualTo(new[] { source.Position, new Vector3(15, 12, 0), relay.Position }));
        }

        [Test] public void HeightChangesInTheMiddleAndAtSinksAreRejectedEvenWithAxisAlignedSegments()
        {
            var a = Relay("A", A); var b = Relay("B", B);
            var planner = new LineRoutePlanner(Stage(a, b), 0.5f);
            Assert.That(planner.Validate(a, b, new[] { A, new Vector3(0, 0, 0), new Vector3(0, 10, 0), new Vector3(15, 10, 0), B }).Failure,
                Is.EqualTo(RouteFailure.VerticalAtRelayOnly));
            var sink = new SinkNodeDefinition("S", B, FlowColor.Red);
            Assert.That(planner.Validate(a, sink, new[] { A, A + Vector3.up * 10, B + Vector3.up * 10, B }).Failure,
                Is.EqualTo(RouteFailure.VerticalAtRelayOnly));
        }

        [Test] public void LiftColumnCannotPassThroughAnOverhang()
        {
            var a = Relay("A", A); var b = new SinkNodeDefinition("B", new Vector3(15, 15, 0), FlowColor.Red);
            var planner = new LineRoutePlanner(Stage(a, b,
                new Bounds(new Vector3(-15, 7, 0), new Vector3(6, 4, 6))), 0.5f);
            Assert.That(planner.Generate(a, b).Failure, Is.EqualTo(RouteFailure.SearchFailed));
        }

        [Test] public void LiftedRelayRouteTransportsFlowsAndDrainsBeforeChangingHeight()
        {
            var source = new SourceNodeDefinition("S", new Vector3(-25, 0, 0));
            var a = Relay("A", A); var b = Relay("B", B);
            var sink = new SinkNodeDefinition("D", new Vector3(25, 0, 0), FlowColor.Red);
            var stage = new StageDefinition(0, new Rect(-40, -40, 80, 80), Array.Empty<Bounds>(), new NodeDefinition[] { source, a, b, sink }, 40);
            var network = Network(stage);
            network.TryConnect("S", "A", new[] { source.Position, A });
            var lifted = new[] { A, A + Vector3.up * 10, B + Vector3.up * 10, B };
            int id = network.TryConnect("A", "B", lifted).LineId.GetValueOrDefault();
            network.TryConnect("B", "D", new[] { B, sink.Position });
            network.GenerateFlow("S", FlowColor.Red); network.RouteWaitingFlows(); network.AdvanceInFlight(10); network.RouteWaitingFlows();
            network.AdvanceInFlight(5);
            var flight = network.Snapshot().Lines.Single(l => l.Id == id).InFlight.Single();
            Assert.That(new LineRoute(lifted).PositionAt(flight.Distance), Is.EqualTo(A + Vector3.up * 5));
            Assert.That(network.RequestRouteChange(id, new[] { A, A + Vector3.up * 20, B + Vector3.up * 20, B }), Is.True);
            Assert.That(network.Snapshot().Lines.Single(l => l.Id == id).Route.Length, Is.EqualTo(50));
            network.AdvanceInFlight(45);
            Assert.That(network.Snapshot().Lines.Single(l => l.Id == id).Route.Length, Is.EqualTo(70));
            network.RouteWaitingFlows(); network.AdvanceInFlight(10);
            Assert.That(network.Snapshot().DeliveredCount, Is.EqualTo(1));
        }

        [Test] public void RaisedWaveRelayRetainsItsLiftLimitInConnectionValidation()
        {
            var a = Relay("A", A);
            var network = Network(Stage(a, Relay("B", B)));
            var added = Relay("W", new Vector3(0, 10, 0), 5);
            Assert.That(network.TryAddNodes(new NodeDefinition[] { added }), Is.True);
            Assert.That(network.TryConnect("A", "W", new[] { A, A + Vector3.up * 10, added.Position }).Succeeded, Is.True);
            Assert.That(network.RequestRouteChange(1, new[] { A, A + Vector3.up * 16, new Vector3(0, 16, 0), added.Position }), Is.False);
        }
    }
}
