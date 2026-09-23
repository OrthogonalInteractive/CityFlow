#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Domain.Spatial;
using R3;
using UnityEngine;

namespace CityFlow.Application.Routing
{
    public sealed class LinePreviewService : IDisposable
    {
        private readonly FlowNetwork network;
        private readonly ILineRoutePlanner planner;
        private readonly Subject<LinePreviewState?> changed = new();
        public Observable<LinePreviewState?> Changed => changed;
        public LinePreviewState? Current { get; private set; }
        public int? EditingLineId { get; private set; }
        public bool SupportsHeight => network.AllowsHeight;
        public LinePreviewService(FlowNetwork network, ILineRoutePlanner planner)
        { this.network = network; this.planner = planner; }
        public void Generate(string sourceId, string destinationId)
        {
            EditingLineId = null;
            NodeDefinition? from = network.NodeDefinitions.FirstOrDefault(n=>n.Id == sourceId);
            NodeDefinition? to = network.NodeDefinitions.FirstOrDefault(n=>n.Id == destinationId);
            if (from == null || to == null)
            { Publish(sourceId,destinationId,Array.Empty<Vector3>(),new LineRouteResult(RouteFailure.InvalidPoints)); return; }
            LineRouteResult result = planner.Generate(from.Position,to.Position);
            Publish(sourceId,destinationId,result.Route?.Points ?? new[] { from.Position,to.Position },result);
        }
        public bool BeginLineEdit(int lineId)
        {
            LineSnapshot? line=network.Snapshot().Lines.FirstOrDefault(l=>l.Id==lineId);
            if (line == null || line.Status != LineStatus.Running) return false;
            EditingLineId=lineId;
            Publish(line.SourceId,line.DestinationId,line.Route.Points,planner.Validate(line.Route.Points)); return true;
        }
        public void UpdatePoints(IReadOnlyList<Vector3> points)
        {
            if (Current == null) throw new InvalidOperationException("Select endpoints before editing a Preview.");
            NodeDefinition? from = network.NodeDefinitions.FirstOrDefault(n=>n.Id == Current.SourceId);
            NodeDefinition? to = network.NodeDefinitions.FirstOrDefault(n=>n.Id == Current.DestinationId);
            LineRouteResult result = points.Count >= 2 && from != null && to != null &&
                points[0] == from.Position && points[points.Count-1] == to.Position ? planner.Validate(points) :
                new LineRouteResult(RouteFailure.EndpointMismatch);
            Publish(Current.SourceId,Current.DestinationId,points,result);
        }
        private void Publish(string sourceId, string destinationId, IReadOnlyList<Vector3> points, LineRouteResult geometry)
        {
            NetworkSnapshot snapshot = network.Snapshot();
            ConnectionFailure connection = EditingLineId.HasValue ?
                snapshot.Lines.Any(l=>l.Id==EditingLineId && l.Status==LineStatus.Running) ? ConnectionFailure.None : ConnectionFailure.LineUnavailable :
                network.CheckConnection(sourceId,destinationId);
            Current = new LinePreviewState(sourceId,destinationId,points,geometry,connection,
                snapshot.Nodes.FirstOrDefault(n=>n.Definition.Id==sourceId),snapshot.Nodes.FirstOrDefault(n=>n.Definition.Id==destinationId),network.Settings,EditingLineId.HasValue);
            changed.OnNext(Current);
        }
        public bool InsertPoint(int segment, Vector3 position)
        {
            if (Current == null || segment < 0 || segment >= Current.Points.Count-1) return false;
            var points = Current.Points.ToList(); if (!SupportsHeight) position.y = points[0].y;
            points.Insert(segment+1,position); UpdatePoints(points); return true;
        }
        public bool MovePoint(int index, Vector3 position)
        {
            if (Current == null || index <= 0 || index >= Current.Points.Count-1) return false;
            var points = Current.Points.ToArray(); if (!SupportsHeight) position.y = points[0].y;
            points[index] = position; UpdatePoints(points); return true;
        }
        public bool RemovePoint(int index)
        {
            if (Current == null || index <= 0 || index >= Current.Points.Count-1) return false;
            var points = Current.Points.ToList(); points.RemoveAt(index); UpdatePoints(points); return true;
        }
        public void Regenerate()
        {
            if (Current == null) return;
            var from=network.NodeDefinitions.Single(n=>n.Id==Current.SourceId);
            var to=network.NodeDefinitions.Single(n=>n.Id==Current.DestinationId);
            var result=planner.Generate(from.Position,to.Position);
            Publish(from.Id,to.Id,result.Route?.Points ?? new[]{from.Position,to.Position},result);
        }
        public void Cancel() { EditingLineId = null; Current = null; changed.OnNext(null); }
        public ConnectionFailure TryConfirm(out int? lineId)
        {
            lineId = null;
            if (Current == null) return ConnectionFailure.InvalidRoute;
            // Recheck the same points and both endpoints immediately before the atomic domain operation.
            UpdatePoints(Current.Points);
            if (Current.ConnectionFailure != ConnectionFailure.None) return Current.ConnectionFailure;
            if (!Current.Geometry.IsValid) return ConnectionFailure.InvalidRoute;
            if (EditingLineId.HasValue)
            {
                int id=EditingLineId.Value;
                if (!network.RequestRouteChange(id,Current.Points)) return ConnectionFailure.LineUnavailable;
                lineId=id; Cancel(); return ConnectionFailure.None;
            }
            ConnectionResult result = network.TryConnect(Current.SourceId,Current.DestinationId,Current.Points);
            if (result.Succeeded) { lineId = result.LineId; Cancel(); }
            return result.Failure;
        }
        public void Dispose() { changed.OnCompleted(); changed.Dispose(); }
    }
}
