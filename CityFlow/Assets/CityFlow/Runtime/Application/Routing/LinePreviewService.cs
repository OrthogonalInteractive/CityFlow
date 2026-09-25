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
            LineRouteResult result = planner.Generate(from,to);
            Publish(sourceId,destinationId,result.Route?.Points ?? new[] { from.Position,to.Position },result);
        }
        public bool BeginLineEdit(int lineId)
        {
            LineSnapshot? line=network.Snapshot().Lines.FirstOrDefault(l=>l.Id==lineId);
            if (line == null || line.Status != LineStatus.Running) return false;
            EditingLineId=lineId;
            var from = network.NodeDefinitions.Single(n => n.Id == line.SourceId);
            var to = network.NodeDefinitions.Single(n => n.Id == line.DestinationId);
            Publish(line.SourceId,line.DestinationId,line.Route.Points,planner.Validate(from, to, line.Route.Points)); return true;
        }
        public void UpdatePoints(IReadOnlyList<Vector3> points)
        {
            if (Current == null) throw new InvalidOperationException("Select endpoints before editing a Preview.");
            NodeDefinition? from = network.NodeDefinitions.FirstOrDefault(n=>n.Id == Current.SourceId);
            NodeDefinition? to = network.NodeDefinitions.FirstOrDefault(n=>n.Id == Current.DestinationId);
            LineRouteResult result = points.Count >= 2 && from != null && to != null &&
                points[0] == from.Position && points[points.Count-1] == to.Position ? planner.Validate(from, to, points) :
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
        public float MinimumRouteHeight => Source is NodeDefinition from && Destination is NodeDefinition to ? Mathf.Max(from.Position.y, to.Position.y) : 0;
        public float MaximumRouteHeight => Source is NodeDefinition from && Destination is NodeDefinition to
            ? Mathf.Min(network.CeilingHeight, Mathf.Min(Top(from), Top(to))) : 0;
        public bool CanAdjustRouteHeight => SupportsHeight && MaximumRouteHeight > MinimumRouteHeight;
        public float RouteHeight => Current == null || Current.Points.Count < 2 ? MinimumRouteHeight :
            Current.Points.Count == 2 ? Mathf.Max(Current.Points[0].y, Current.Points[1].y) :
            Current.Points[FirstPlanarIndex].y;

        private static float Top(NodeDefinition node) => node.Position.y + (node is RelayNodeDefinition relay ? relay.MaximumRise : 0);
        private NodeDefinition? Source => network.NodeDefinitions.FirstOrDefault(n => n.Id == Current?.SourceId);
        private NodeDefinition? Destination => network.NodeDefinitions.FirstOrDefault(n => n.Id == Current?.DestinationId);
        private int FirstPlanarIndex => Current != null && Current.Points.Count > 2 && Current.Points[0].y != Current.Points[1].y ? 1 : 0;
        private int LastPlanarIndex => Current == null ? 0 : Current.Points.Count > 2 &&
            Current.Points[Current.Points.Count - 1].y != Current.Points[Current.Points.Count - 2].y ? Current.Points.Count - 2 : Current.Points.Count - 1;

        public bool CanMovePoint(int index) => Current != null && index > FirstPlanarIndex && index < LastPlanarIndex;
        public bool CanInsertPoint(int segment) => Current != null && segment >= FirstPlanarIndex && segment < LastPlanarIndex &&
            Current.Points[segment].y == Current.Points[segment + 1].y;

        public bool SetRouteHeight(float height)
        {
            if (Current == null || !CanAdjustRouteHeight || Source is not NodeDefinition from || Destination is not NodeDefinition to ||
                float.IsNaN(height) || float.IsInfinity(height)) return false;
            var points = new List<Vector3> { from.Position };
            void Add(Vector3 point) { if (points[points.Count - 1] != point) points.Add(point); }
            Add(new Vector3(from.Position.x, height, from.Position.z));
            for (int i = FirstPlanarIndex + 1; i < LastPlanarIndex; i++)
                Add(new Vector3(Current.Points[i].x, height, Current.Points[i].z));
            Add(new Vector3(to.Position.x, height, to.Position.z));
            Add(to.Position);
            UpdatePoints(points);
            return true;
        }

        public bool InsertPoint(int segment, Vector3 position)
        {
            if (Current == null || !CanInsertPoint(segment)) return false;
            var points = Current.Points.ToList();
            position.y = points[segment].y;
            points.Insert(segment + 1, position);
            UpdatePoints(points);
            return true;
        }
        public bool MovePoint(int index, Vector3 position)
        {
            if (Current == null || !CanMovePoint(index)) return false;
            var points = Current.Points.ToArray();
            position.y = points[index].y;
            points[index] = position;
            UpdatePoints(points);
            return true;
        }
        public bool RemovePoint(int index)
        {
            if (Current == null || !CanMovePoint(index)) return false;
            var points = Current.Points.ToList(); points.RemoveAt(index); UpdatePoints(points); return true;
        }
        public void Regenerate()
        {
            if (Current == null) return;
            var from=network.NodeDefinitions.Single(n=>n.Id==Current.SourceId);
            var to=network.NodeDefinitions.Single(n=>n.Id==Current.DestinationId);
            var result=planner.Generate(from,to);
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
