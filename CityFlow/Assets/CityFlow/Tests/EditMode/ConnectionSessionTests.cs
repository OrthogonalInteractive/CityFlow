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
    public sealed class ConnectionSessionTests
    {
        private static StageDefinition Stage(int outgoing = 3, int incoming = 3) => new StageDefinition(0,
            new Rect(-10,-10,120,80), Array.Empty<Bounds>(), new NodeDefinition[] {
                new SourceNodeDefinition("A", Vector3.zero, maxOutgoing: outgoing),
                new SinkNodeDefinition("B", new Vector3(30,0,0), FlowColor.Red, maxIncoming: incoming),
                new RelayNodeDefinition("C", new Vector3(70,0,0)),
                new RelayNodeDefinition("D", new Vector3(90,0,0)) });
        private static FlowNetwork Network(StageDefinition stage) => new FlowNetwork(stage,new NetworkSettings(50,50,10,20,0.5f));
        [Test] public void BeginPreviewAndConfirmCreateExactlyOneDirectedLine()
        {
            var stage = Stage(); var n = Network(stage);
            using var p = new LinePreviewService(n,new LineRoutePlanner(stage,0.5f));
            using var s = new ConnectionSession(n,p);
            Assert.That(s.Begin("A"),Is.True); Assert.That(s.SourceId,Is.EqualTo("A")); Assert.That(p.Current,Is.Null);
            s.SelectTarget("B"); Assert.That(p.Current?.DestinationId,Is.EqualTo("B")); Assert.That(n.Snapshot().Lines,Is.Empty);
            Assert.That(s.Confirm(),Is.EqualTo(ConnectionFailure.None)); Assert.That(s.IsActive,Is.False); Assert.That(p.Current,Is.Null);
            var line=n.Snapshot().Lines.Single(); Assert.That(line.Id,Is.EqualTo(s.LastCreatedLineId));
            Assert.That(line.SourceId,Is.EqualTo("A")); Assert.That(line.DestinationId,Is.EqualTo("B"));
            Assert.That(n.Snapshot().Nodes.Single(x=>x.Definition.Id=="A").OutgoingUsed,Is.EqualTo(1));
            s.Confirm(); Assert.That(n.Snapshot().Lines.Count,Is.EqualTo(1));
        }
        [Test] public void SourcesAreNotTargetsAndSelectingOnePreservesTheCurrentPreview()
        {
            var stage = Stage(); var n = Network(stage);
            using var p = new LinePreviewService(n, new LineRoutePlanner(stage, 0.5f));
            using var s = new ConnectionSession(n, p);
            Assert.That(s.Begin("C"), Is.True);
            Assert.That(s.Candidates().Select(c => c.Node.Definition.Id), Is.EquivalentTo(new[] { "B", "D" }));
            s.SelectTarget("B");
            var before = p.Current;
            s.SelectTarget("A");
            Assert.That(p.Current, Is.SameAs(before));
        }
        [TestCase(false)] [TestCase(true)] public void CancelAtEitherStepUsesNoSlots(bool withPreview)
        {
            var stage=Stage(); var n=Network(stage); using var p=new LinePreviewService(n,new LineRoutePlanner(stage,0.5f));
            using var s=new ConnectionSession(n,p); Assert.That(s.Begin("A"),Is.True);
            if(withPreview) s.SelectTarget("B"); s.Cancel();
            Assert.That(s.IsActive,Is.False); Assert.That(p.Current,Is.Null); Assert.That(n.Snapshot().Lines,Is.Empty);
            Assert.That(n.Snapshot().Nodes.All(x=>x.IncomingUsed+x.OutgoingUsed==0),Is.True);
        }
        [Test] public void CandidateBandsUseGroundDistanceAndRetainPreviewAcrossFiltering()
        {
            var stage=Stage(); var n=Network(stage); using var p=new LinePreviewService(n,new LineRoutePlanner(stage,0.5f));
            using var s=new ConnectionSession(n,p); s.Begin("A");
            var candidates=s.Candidates(); Assert.That(candidates.Count,Is.EqualTo(3));
            var b=candidates.Single(x=>x.Node.Definition.Id=="B"); Assert.That(b.Distance,Is.EqualTo(30));
            Assert.That(b.Node.Definition.SinkColor,Is.EqualTo(FlowColor.Red)); Assert.That(b.SourceOutgoingUsed,Is.Zero);
            Assert.That(b.SourceOutgoingLimit,Is.EqualTo(3)); Assert.That(b.Node.IncomingUsed,Is.Zero);
            s.SetFilter(DistanceBand.Near); Assert.That(s.Candidates().Select(x=>x.Node.Definition.Id),Is.EquivalentTo(new[]{"B"}));
            s.SelectTarget("B"); var before=p.Current;
            s.SetFilter(DistanceBand.Mid); Assert.That(s.Candidates().Single().Node.Definition.Id,Is.EqualTo("C"));
            s.SetFilter(DistanceBand.Far); Assert.That(s.Candidates().Single().Node.Definition.Id,Is.EqualTo("D"));
            Assert.That(p.Current,Is.SameAs(before));
        }
        [TestCase(true)] [TestCase(false)] public void ConfirmRechecksSlotsFilledAfterPreview(bool outgoing)
        {
            var stage=Stage(1,1); var n=Network(stage); using var p=new LinePreviewService(n,new LineRoutePlanner(stage,0.5f));
            using var s=new ConnectionSession(n,p); s.Begin("A"); s.SelectTarget("B");
            Assert.That(p.Current?.CanConfirm,Is.True);
            n.TryConnect(outgoing?"A":"C",outgoing?"C":"B",outgoing?
                new[]{Vector3.zero,new Vector3(70,0,0)}:new[]{new Vector3(70,0,0),new Vector3(30,0,0)});
            var failure=outgoing?ConnectionFailure.OutgoingLimit:ConnectionFailure.IncomingLimit;
            Assert.That(s.Confirm(),Is.EqualTo(failure)); Assert.That(p.Current?.ConnectionFailure,Is.EqualTo(failure));
            Assert.That(s.IsActive,Is.True); Assert.That(n.Snapshot().Lines.Count,Is.EqualTo(1));
        }
        [Test] public void SelfIsIgnoredAndDuplicateRemainsSelectableWithAReason()
        {
            var stage=Stage(); var n=Network(stage); using var p=new LinePreviewService(n,new LineRoutePlanner(stage,0.5f));
            using var s=new ConnectionSession(n,p); s.Begin("A"); s.SelectTarget("A");
            Assert.That(p.Current, Is.Null);
            Assert.That(s.Confirm(), Is.EqualTo(ConnectionFailure.InvalidRoute));
            s.SelectTarget("B"); n.TryConnect("A","B",new[]{Vector3.zero,new Vector3(30,0,0)});
            Assert.That(s.Confirm(),Is.EqualTo(ConnectionFailure.DuplicateDirection));
            Assert.That(s.Candidates().Single(x=>x.Node.Definition.Id=="B").Failure,Is.EqualTo(ConnectionFailure.DuplicateDirection));
            Assert.That(n.Snapshot().Lines.Count,Is.EqualTo(1));
        }
        private sealed class ChangingPlanner : ILineRoutePlanner
        {
            public bool Blocked;
            public LineRouteResult Generate(Vector3 a,Vector3 b)=>new LineRouteResult(new LineRoute(new[]{a,b}));
            public LineRouteResult Validate(System.Collections.Generic.IReadOnlyList<Vector3> points)=>Blocked?
                new LineRouteResult(RouteFailure.Obstacle,0):new LineRouteResult(new LineRoute(points));
        }
        [Test] public void ConfirmRevalidatesGeometryAndKeepsInvalidPreviewForAnotherChoice()
        {
            var n=Network(Stage()); var planner=new ChangingPlanner(); using var p=new LinePreviewService(n,planner);
            using var s=new ConnectionSession(n,p); s.Begin("A"); s.SelectTarget("B"); planner.Blocked=true;
            Assert.That(s.Confirm(),Is.EqualTo(ConnectionFailure.InvalidRoute)); Assert.That(s.IsActive,Is.True);
            Assert.That(p.Current?.Geometry.Failure,Is.EqualTo(RouteFailure.Obstacle)); Assert.That(n.Snapshot().Lines,Is.Empty);
            planner.Blocked=false; s.SelectTarget("C"); Assert.That(s.Confirm(),Is.EqualTo(ConnectionFailure.None));
        }
        [Test] public void FailedSearchCannotConfirmAndRetainsEndpoints()
        {
            var original=Stage(); var stage=new StageDefinition(0,original.WalkableArea,
                new[]{new Bounds(new Vector3(15,5,30),new Vector3(5,10,90))},original.Nodes);
            var n=Network(stage); using var p=new LinePreviewService(n,new LineRoutePlanner(stage,0.5f));
            using var s=new ConnectionSession(n,p); s.Begin("A"); s.SelectTarget("B");
            Assert.That(p.Current?.Geometry.Failure,Is.EqualTo(RouteFailure.SearchFailed));
            Assert.That(s.Confirm(),Is.EqualTo(ConnectionFailure.InvalidRoute)); Assert.That(s.SourceId,Is.EqualTo("A"));
            Assert.That(p.Current?.DestinationId,Is.EqualTo("B")); Assert.That(n.Snapshot().Lines,Is.Empty);
        }
        [Test] public void IdleActionsAndUnknownSourceDoNotCreateSessionOrPreview()
        {
            var stage=Stage(); var n=Network(stage); using var p=new LinePreviewService(n,new LineRoutePlanner(stage,0.5f));
            using var s=new ConnectionSession(n,p); s.SelectTarget("B");
            Assert.That(s.Begin("missing"),Is.False); Assert.That(s.IsActive,Is.False); Assert.That(p.Current,Is.Null);
            Assert.That(s.Confirm(),Is.EqualTo(ConnectionFailure.InvalidRoute)); Assert.That(s.Candidates(),Is.Empty);
        }
        [Test] public void BeginCannotReplaceActiveSourceAndUnknownTargetDoesNotErasePreview()
        {
            var stage=Stage(); var n=Network(stage); using var p=new LinePreviewService(n,new LineRoutePlanner(stage,0.5f));
            using var s=new ConnectionSession(n,p); s.Begin("A"); s.SelectTarget("B");
            Assert.That(s.Begin("C"),Is.False); s.SelectTarget("missing");
            Assert.That(s.SourceId,Is.EqualTo("A")); Assert.That(p.Current?.DestinationId,Is.EqualTo("B"));
        }
        [Test] public void SinkCannotStartConnectionAndOwnNodeIsNeverACandidate()
        {
            var stage = Stage(); var n = Network(stage);
            using var p = new LinePreviewService(n, new LineRoutePlanner(stage, 0.5f));
            using var s = new ConnectionSession(n, p);
            Assert.That(s.Begin("B"), Is.False);
            Assert.That(s.IsActive, Is.False); Assert.That(p.Current, Is.Null);
            Assert.That(s.Begin("A"), Is.True);
            Assert.That(s.Candidates().Any(c => c.Node.Definition.Id == "A"), Is.False);
        }
        [Test] public void UndoNewEmptyLineReleasesBothSlotsOnlyOnce()
        {
            var stage = Stage(); var n = Network(stage);
            using var p = new LinePreviewService(n, new LineRoutePlanner(stage, 0.5f));
            using var s = new ConnectionSession(n, p);
            s.Begin("A"); s.SelectTarget("B"); s.Confirm();
            Assert.That(s.CanUndoLastConnection, Is.True);
            Assert.That(s.UndoLastConnection(), Is.True);
            Assert.That(n.Snapshot().Lines, Is.Empty);
            Assert.That(n.Snapshot().Nodes.All(node => node.IncomingUsed + node.OutgoingUsed == 0), Is.True);
            Assert.That(s.UndoLastConnection(), Is.False);
        }
        [Test] public void UndoDoesNotDeleteOccupiedLineOrUndoAnEdit()
        {
            var stage = Stage(); var n = Network(stage);
            using var p = new LinePreviewService(n, new LineRoutePlanner(stage, 0.5f));
            using var s = new ConnectionSession(n, p);
            s.Begin("A"); s.SelectTarget("B"); s.Confirm();
            int id = s.LastCreatedLineId.GetValueOrDefault();
            n.GenerateFlow("A", FlowColor.Red);
            n.RouteWaitingFlows();
            Assert.That(s.CanUndoLastConnection, Is.False);
            Assert.That(s.UndoLastConnection(), Is.False);
            Assert.That(n.Snapshot().Lines.Single().InFlight.Count, Is.EqualTo(1));
            n.AdvanceInFlight(10);
            s.BeginLineEdit(id); s.Confirm();
            Assert.That(s.CanUndoLastConnection, Is.False);
            Assert.That(s.UndoLastConnection(), Is.False);
            Assert.That(n.Snapshot().Lines.Single().Id, Is.EqualTo(id));
        }
        [Test] public void DismissingUndoOrStartingAnotherConnectionInvalidatesThePreviousUndo()
        {
            var stage = Stage(); var n = Network(stage);
            using var p = new LinePreviewService(n, new LineRoutePlanner(stage, 0.5f));
            using var s = new ConnectionSession(n, p);
            s.Begin("A"); s.SelectTarget("B"); s.Confirm();
            Assert.That(s.CanUndoLastConnection, Is.True); s.DismissUndo();
            Assert.That(s.UndoLastConnection(), Is.False);
            s.Begin("A"); s.SelectTarget("C"); s.Confirm();
            Assert.That(s.CanUndoLastConnection, Is.True); s.Begin("C"); s.Cancel();
            Assert.That(s.UndoLastConnection(), Is.False);
            Assert.That(n.Snapshot().Lines.Count, Is.EqualTo(2));
        }
        [Test] public void BandThresholdsMustBeFinitePositiveAndOrdered()
        {
            var stage=Stage(); var n=Network(stage); using var p=new LinePreviewService(n,new LineRoutePlanner(stage,0.5f));
            Assert.Throws<ArgumentOutOfRangeException>(()=>new ConnectionSession(n,p,0,70));
            Assert.Throws<ArgumentOutOfRangeException>(()=>new ConnectionSession(n,p,70,30));
            Assert.Throws<ArgumentOutOfRangeException>(()=>new ConnectionSession(n,p,30,float.PositiveInfinity));
        }
    }
}
