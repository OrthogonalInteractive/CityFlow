#nullable enable

using System;
using System.Linq;
using CityFlow.Application.Connections;
using CityFlow.Application.Routing;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Domain.Spatial;
using CityFlow.Infrastructure.Routing;
using NUnit.Framework;
using UnityEngine;

namespace CityFlow.Tests.EditMode
{
    public sealed class HeightRoutingTests
    {
        private static readonly Vector3 A = new(-15, 0, 0), B = new(15, 0, 0);
        private static StageDefinition Stage(Vector3 start, Vector3 end, params Bounds[] buildings) =>
            new(0, new Rect(-40, -40, 80, 80), buildings, new NodeDefinition[] {
                new SourceNodeDefinition("A", start, generationInterval: 1000),
                new SinkNodeDefinition("B", end, FlowColor.Red) }, maximumAltitude: 40);
        private static FlowNetwork Network(StageDefinition stage) => new(stage, new NetworkSettings(10, 5, 3, 1, 0.5f));

        [Test] public void PolylineUsesThreeDimensionalLengthAndInterpolation()
        {
            var route = new LineRoute(new[] { Vector3.zero, new Vector3(3, 4, 0), new Vector3(3, 8, 0) });
            Assert.That(route.Length, Is.EqualTo(9).Within(1e-6));
            Assert.That(route.PositionAt(2.5), Is.EqualTo(new Vector3(1.5f, 2, 0)));
            Assert.That(route.PositionAt(7), Is.EqualTo(new Vector3(3, 6, 0)));
        }

        [Test] public void RoofAndAirNodesAreValidButBuildingInteriorIsNot()
        {
            var building = new Bounds(new Vector3(0, 3, 0), new Vector3(8, 6, 8));
            var stage = Stage(A, new Vector3(0, 7, 0), building);
            Assert.DoesNotThrow(() => stage.Validate(0.5f));
            Assert.That(stage.ValidatePoint(new Vector3(0, 3, 0), 0.5f), Is.EqualTo(RouteFailure.Obstacle));
            Assert.That(stage.IsWalkable(new Vector3(0, 6.4f, 0), 0.5f), Is.False);
        }

        [Test] public void VolumeChecksAllowOverpassAndUnderpassButRejectSlantedPenetration()
        {
            var stage = Stage(A, B, new Bounds(new Vector3(0, 3, 0), new Vector3(8, 6, 8)));
            var planner = new LineRoutePlanner(stage, 0.5f);
            Assert.That(planner.Validate(new[] { A, new Vector3(-5, 7, 0), new Vector3(5, 7, 0), B }).IsValid, Is.True);
            var blocked = planner.Validate(new[] { A, new Vector3(8, 10, 0), B });
            Assert.That(blocked.Failure, Is.EqualTo(RouteFailure.Obstacle));
            Assert.That(blocked.InvalidSegment, Is.Zero);
            var floating = Stage(A, B, new Bounds(new Vector3(0, 15, 0), new Vector3(8, 10, 8)));
            Assert.That(new LineRoutePlanner(floating, 0.5f).Generate(A, B).Route!.Length, Is.EqualTo(30));
        }

        [Test] public void AltitudeBoundsRejectUndergroundAndAboveCeiling()
        {
            var stage = Stage(A, B);
            Assert.That(stage.IsWalkable(new Vector3(0, -0.01f, 0), 0.5f), Is.False);
            Assert.That(stage.IsWalkable(new Vector3(0, 40.01f, 0), 0.5f), Is.False);
            Assert.That(stage.IsWalkable(new Vector3(0, 40, 0), 0.5f), Is.True);
        }

        [TestCase(6f, 60f, true)]
        [TestCase(35f, 8f, false)]
        public void AutomaticRouteComparesOverpassWithSideDetour(float height, float width, bool overpass)
        {
            var stage = Stage(A, B, new Bounds(new Vector3(0, height / 2, 0), new Vector3(8, height, width)));
            var planner = new LineRoutePlanner(stage, 0.5f);
            var result = planner.Generate(A, B);
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Route!.Points.Any(p => p.y > height), Is.EqualTo(overpass));
            Assert.That(stage.IsRouteWalkable(result.Route, 0.5f), Is.True);
            Assert.That(planner.Generate(A, B).Route!.Points, Is.EqualTo(result.Route.Points));
        }

        [Test] public void CeilingAndFullWidthWallCanMakeAConnectionImpossible()
        {
            var stage = Stage(A, B, new Bounds(new Vector3(0, 25, 0), new Vector3(8, 50, 90)));
            Assert.That(new LineRoutePlanner(stage, 0.5f).Generate(A, B).Failure, Is.EqualTo(RouteFailure.SearchFailed));
        }

        [TestCase(false)] [TestCase(true)]
        public void SlopedTransportUsesTheSameThreeDimensionalRouteAndTime(bool descending)
        {
            Vector3 start = descending ? new Vector3(3, 4, 0) : Vector3.zero;
            Vector3 end = descending ? Vector3.zero : new Vector3(3, 4, 0);
            var stage = Stage(start, end);
            var network = Network(stage);
            using var preview = new LinePreviewService(network, new LineRoutePlanner(stage, 0.5f));
            preview.Generate("A", "B");
            Assert.That(preview.Current!.Length, Is.EqualTo(5));
            Assert.That(preview.Current.TravelTime, Is.EqualTo(5));
            Assert.That(preview.Current.Throughput, Is.EqualTo(0.6).Within(1e-6));
            Assert.That(preview.TryConfirm(out _), Is.EqualTo(ConnectionFailure.None));
            network.GenerateFlow("A", FlowColor.Red); network.RouteWaitingFlows(); network.AdvanceInFlight(2.5);
            var line = network.Snapshot().Lines.Single();
            Assert.That(line.Route.PositionAt(line.InFlight.Single().Distance), Is.EqualTo(new Vector3(1.5f, 2, 0)));
            network.AdvanceInFlight(2.5);
            Assert.That(network.Snapshot().DeliveredCount, Is.EqualTo(1));
        }

        [Test] public void ManualHeightEditingPreservesYAndRejectsCollisionsWithoutChangingNetwork()
        {
            var stage = Stage(A, B);
            var network = Network(stage);
            using var preview = new LinePreviewService(network, new LineRoutePlanner(stage, 0.5f));
            preview.Generate("A", "B");
            preview.InsertPoint(0, new Vector3(0, 10, 0));
            Assert.That(preview.Current!.Points[1].y, Is.EqualTo(10));
            preview.MovePoint(1, new Vector3(2, 20, 1));
            Assert.That(preview.Current!.Points[1], Is.EqualTo(new Vector3(2, 20, 1)));
            Assert.That(preview.MovePoint(0, Vector3.one), Is.False);
            preview.MovePoint(1, new Vector3(2, 41, 1));
            Assert.That(preview.Current.CanConfirm, Is.False);
            Assert.That(preview.TryConfirm(out _), Is.EqualTo(ConnectionFailure.InvalidRoute));
            Assert.That(network.Snapshot().Lines, Is.Empty);
        }

        [Test] public void CandidateDistanceBandsIncludeHeightAndVerticalConnectionsWork()
        {
            var stage = Stage(Vector3.zero, new Vector3(0, 35, 0));
            var network = Network(stage);
            using var preview = new LinePreviewService(network, new LineRoutePlanner(stage, 0.5f));
            using var session = new ConnectionSession(network, preview, 20, 30);
            session.Begin("A");
            Assert.That(session.Candidates().Single().Distance, Is.EqualTo(35));
            Assert.That(session.Candidates().Single().Band, Is.EqualTo(DistanceBand.Far));
            session.SelectTarget("B");
            Assert.That(session.Confirm(), Is.EqualTo(ConnectionFailure.None));
        }

        [Test] public void RaisedWaveNodesKeepHeightRulesWhenAddedToNetwork()
        {
            var network = Network(Stage(A, B));
            Assert.That(network.TryAddNodes(new NodeDefinition[] {
                new RelayNodeDefinition("ROOF", new Vector3(0, 20, 0)) }), Is.True);
            Assert.That(network.TryConnect("A", "ROOF", new[] { A, new Vector3(0, 20, 0) }).Succeeded, Is.True);
        }

        [Test] public void HeightRouteChangeWaitsForFlightsBeforeChangingTheirPolyline()
        {
            var network = Network(Stage(A, B));
            int id = network.TryConnect("A", "B", new[] { A, B }).LineId.GetValueOrDefault();
            network.GenerateFlow("A", FlowColor.Red); network.RouteWaitingFlows(); network.AdvanceInFlight(5);
            Assert.That(network.RequestRouteChange(id, new[] { A, new Vector3(0, 10, 0), B }), Is.True);
            Assert.That(network.Snapshot().Lines.Single().Route.Length, Is.EqualTo(30));
            Assert.That(network.Snapshot().Lines.Single().InFlight.Single().Distance, Is.EqualTo(5));
            network.AdvanceInFlight(25);
            Assert.That(network.Snapshot().Lines.Single().Route.Length, Is.GreaterThan(30));
            Assert.That(network.Snapshot().DeliveredCount, Is.EqualTo(1));
        }
    }
}
