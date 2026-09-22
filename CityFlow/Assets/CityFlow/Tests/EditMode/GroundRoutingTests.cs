#nullable enable

using System;
using System.Linq;
using CityFlow.Application.Routing;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Domain.Spatial;
using CityFlow.Infrastructure.Routing;
using NUnit.Framework;
using UnityEngine;

namespace CityFlow.Tests.EditMode
{
    public sealed class GroundRoutingTests
    {
        private static StageDefinition Stage(params Bounds[] buildings) => new StageDefinition(0,
            new Rect(-20,-20,40,40), buildings, new NodeDefinition[] {
                new SourceNodeDefinition("A", new Vector3(-15,0,0), maxOutgoing: 2),
                new SinkNodeDefinition("B", new Vector3(15,0,0), FlowColor.Red, maxIncoming: 2) });
        private static LineRoute Route(GroundRouteResult result) => result.Route ?? throw new AssertionException("A valid route is required.");
        private static LinePreviewState Current(LinePreviewService preview) => preview.Current ?? throw new AssertionException("A Preview is required.");
        private static readonly Vector3 A = new Vector3(-15,0,0), B = new Vector3(15,0,0);
        [Test] public void ClearGroundReturnsDirectRouteWithExactEndpoints()
        {
            var r = new GroundRoutePlanner(Stage(),0.5f).Generate(A,B);
            Assert.That(r.IsValid, Is.True); Assert.That(Route(r).Points, Is.EqualTo(new[] { A,B }));
            Assert.That(Route(r).Length, Is.EqualTo(30));
        }
        [Test] public void CentralFootprintProducesRepeatableGroundDetour()
        {
            var stage = Stage(new Bounds(new Vector3(0,5,0),new Vector3(10,10,10)));
            var planner = new GroundRoutePlanner(stage,0.5f); var r = planner.Generate(A,B);
            Assert.That(r.IsValid, Is.True); Assert.That(Route(r).Length, Is.GreaterThan(30));
            Assert.That(stage.IsRouteWalkable(Route(r),0.5f), Is.True);
            Assert.That(Route(r).Points.All(p=>p.y==0), Is.True);
            Assert.That(Route(planner.Generate(A,B)).Points, Is.EqualTo(Route(r).Points));
        }
        [TestCase(1)] [TestCase(-1)] public void ForcedDetourUsesTheOpenSide(int closedSide)
        {
            var stage = Stage(new Bounds(new Vector3(0,5,closedSide*10),new Vector3(8,10,30)));
            var r = new GroundRoutePlanner(stage,0.5f).Generate(A,B);
            Assert.That(r.IsValid, Is.True);
            Assert.That(Route(r).Points.Any(p=>p.z*closedSide < -5), Is.True);
            Assert.That(stage.IsRouteWalkable(Route(r),0.5f), Is.True);
        }
        [TestCase(1f, false)] [TestCase(3f, true)] public void ClearanceControlsNarrowPassage(float gap, bool valid)
        {
            var stage = Stage(new Bounds(new Vector3(0,5,-10-gap/4), new Vector3(5,10,20-gap/2)),
                new Bounds(new Vector3(0,5,10+gap/4),new Vector3(5,10,20-gap/2)));
            var r = new GroundRoutePlanner(stage,0.6f).Generate(A,B);
            Assert.That(r.IsValid, Is.EqualTo(valid));
            if (!valid) Assert.That(r.Failure, Is.EqualTo(RouteFailure.SearchFailed));
        }
        [Test] public void SegmentCrossingFootprintIsInvalidEvenWhenControlPointsAreOutside()
        {
            var planner = new GroundRoutePlanner(Stage(new Bounds(new Vector3(0,5,0),new Vector3(10,10,10))),0.5f);
            var r = planner.Validate(new[] { A,B });
            Assert.That(r.Failure, Is.EqualTo(RouteFailure.Obstacle)); Assert.That(r.InvalidSegment, Is.Zero);
        }
        [Test] public void DomainAndInfrastructureRejectTheSameFullRoute()
        {
            var stage = Stage(new Bounds(new Vector3(0,5,0),new Vector3(10,10,10)));
            var r = new GroundRoutePlanner(stage,0.5f).Validate(new[] { A,B });
            Assert.That(r.IsValid, Is.EqualTo(stage.IsRouteWalkable(new LineRoute(new[] { A,B }),0.5f)));
        }
        [Test] public void HeightAndAreaFailuresHaveDistinctReasons()
        {
            var planner = new GroundRoutePlanner(Stage(),0.5f);
            Assert.That(planner.Validate(new[] { A,B+Vector3.up }).Failure, Is.EqualTo(RouteFailure.GroundHeight));
            Assert.That(planner.Generate(A,new Vector3(30,0,0)).Failure, Is.EqualTo(RouteFailure.OutsideArea));
        }
        [Test] public void PreviewAndCancelDoNotCreateLinesOrUseConnectionSlots()
        {
            var stage = Stage(new Bounds(new Vector3(0,5,0),new Vector3(10,10,10)));
            var n = new FlowNetwork(stage,new NetworkSettings(50,50,10,20,0.5f));
            var preview = new LinePreviewService(n,new GroundRoutePlanner(stage,0.5f)); preview.Generate("A","B");
            Assert.That(preview.Current, Is.Not.Null); var r = Current(preview);
            Assert.That(r.CanConfirm, Is.True); Assert.That(r.OutgoingAfter, Is.EqualTo(1)); Assert.That(r.IncomingAfter, Is.EqualTo(1));
            Assert.That(r.TravelTime, Is.EqualTo(r.Length/20)); Assert.That(r.Throughput, Is.EqualTo(10/r.TravelTime));
            Assert.That(n.Snapshot().Lines, Is.Empty); Assert.That(n.Snapshot().Nodes.All(x=>x.IncomingUsed+x.OutgoingUsed==0), Is.True);
            preview.Cancel(); Assert.That(preview.Current, Is.Null); Assert.That(n.Snapshot().Lines, Is.Empty);
        }
        [Test] public void FailedAutomaticSearchRetainsEndpointsForManualEditing()
        {
            var stage = Stage(new Bounds(new Vector3(0,5,0),new Vector3(5,10,50)));
            var n = new FlowNetwork(stage,new NetworkSettings(50,50,10,20,0.5f));
            var preview = new LinePreviewService(n,new GroundRoutePlanner(stage,0.5f)); preview.Generate("A","B");
            Assert.That(preview.Current, Is.Not.Null); Assert.That(Current(preview).CanConfirm, Is.False);
            Assert.That(Current(preview).Geometry.Failure, Is.EqualTo(RouteFailure.SearchFailed));
            Assert.That(Current(preview).Points, Is.EqualTo(new[] { A,B })); Assert.That(n.Snapshot().Lines, Is.Empty);
        }
        [Test] public void EditedPreviewValidatesAllSegmentsAndKeepsEndpointsFixed()
        {
            var stage = Stage(new Bounds(new Vector3(0,5,0),new Vector3(10,10,10)));
            var preview = new LinePreviewService(new FlowNetwork(stage,new NetworkSettings(50,50,10,20,0.5f)),new GroundRoutePlanner(stage,0.5f));
            preview.Generate("A","B"); preview.UpdatePoints(new[] { A,B });
            Assert.That(Current(preview).Geometry.Failure, Is.EqualTo(RouteFailure.Obstacle));
            preview.UpdatePoints(new[] { A,new Vector3(-15,0,-10),new Vector3(15,0,-10),B });
            Assert.That(Current(preview).CanConfirm, Is.True); Assert.That(Current(preview).Length, Is.EqualTo(50));
            preview.UpdatePoints(new[] { A+Vector3.left,B });
            Assert.That(Current(preview).Geometry.Failure, Is.EqualTo(RouteFailure.EndpointMismatch));
        }
        [Test] public void DuplicateConnectionIsReportedWithoutChangingExistingNetwork()
        {
            var stage = Stage(); var n = new FlowNetwork(stage,new NetworkSettings(50,50,10,20,0.5f));
            n.TryConnect("A","B",new[] { A,B });
            var preview = new LinePreviewService(n,new GroundRoutePlanner(stage,0.5f)); preview.Generate("A","B");
            Assert.That(preview.Current, Is.Not.Null);
            Assert.That(Current(preview).ConnectionFailure, Is.EqualTo(ConnectionFailure.DuplicateDirection));
            Assert.That(Current(preview).CanConfirm, Is.False); Assert.That(n.Snapshot().Lines.Count, Is.EqualTo(1));
        }
        [TestCase(true)] [TestCase(false)] public void PreviewReportsFullConnectionSlotsWithoutReservingAny(bool outgoing)
        {
            var stage = new StageDefinition(0,new Rect(-20,-20,40,40),Array.Empty<Bounds>(),new NodeDefinition[] {
                new SourceNodeDefinition("A", A, maxOutgoing: 1),
                new SinkNodeDefinition("B", B, FlowColor.Red, maxIncoming: 1),
                new RelayNodeDefinition("C", new Vector3(0,0,15), maxIncoming: 2, maxOutgoing: 2) });
            var n = new FlowNetwork(stage,new NetworkSettings(50,50,10,20,0.5f));
            Assert.That(n.TryConnect(outgoing ? "A" : "C",outgoing ? "C" : "B",
                outgoing ? new[] { A,new Vector3(0,0,15) } : new[] { new Vector3(0,0,15),B }).Succeeded, Is.True);
            var preview = new LinePreviewService(n,new GroundRoutePlanner(stage,0.5f)); preview.Generate("A","B");
            Assert.That(Current(preview).ConnectionFailure, Is.EqualTo(outgoing ? ConnectionFailure.OutgoingLimit : ConnectionFailure.IncomingLimit));
            Assert.That(Current(preview).CanConfirm, Is.False); Assert.That(n.Snapshot().Lines.Count, Is.EqualTo(1));
        }
        [Test] public void InvalidPointsAndUnknownEndpointsAreRejected()
        {
            var stage = Stage(); var planner = new GroundRoutePlanner(stage,0.5f);
            Assert.That(planner.Validate(Array.Empty<Vector3>()).Failure, Is.EqualTo(RouteFailure.InvalidPoints));
            Assert.That(planner.Validate(new[] { A,A,B }).Failure, Is.EqualTo(RouteFailure.InvalidPoints));
            Assert.That(planner.Validate(new[] { A,new Vector3(float.NaN,0,0),B }).Failure, Is.EqualTo(RouteFailure.InvalidPoints));
            var preview = new LinePreviewService(new FlowNetwork(stage,new NetworkSettings(50,50,10,20,0.5f)),planner);
            preview.Generate("missing","B");
            Assert.That(Current(preview).ConnectionFailure, Is.EqualTo(ConnectionFailure.UnknownSource));
        }
        private sealed class First : IRandomSource { public int NextIndex(int count) => 0; }
        [Test] public void GeneratedRouteIsTheSameRouteUsedForTransportAndDistance()
        {
            var stage = Stage(new Bounds(new Vector3(0,5,0),new Vector3(10,10,10)));
            var n = new FlowNetwork(stage,new NetworkSettings(50,50,10,20,0.5f));
            var preview = new LinePreviewService(n,new GroundRoutePlanner(stage,0.5f)); preview.Generate("A","B");
            var candidate = Current(preview);
            Assert.That(n.TryConnect("A","B",candidate.Points).Succeeded, Is.True);
            n.GenerateFlow("A",FlowColor.Red); n.RouteWaitingFlows(new First()); n.AdvanceInFlight(candidate.TravelTime/2);
            var line = n.Snapshot().Lines.Single();
            Assert.That(line.Route.Points, Is.EqualTo(candidate.Points));
            Assert.That(line.Route.Length, Is.EqualTo(candidate.Length));
            Assert.That(line.Route.PositionAt(line.InFlight.Single().Distance),
                Is.EqualTo(Route(candidate.Geometry).PositionAt(candidate.Length/2)));
        }
    }
}
