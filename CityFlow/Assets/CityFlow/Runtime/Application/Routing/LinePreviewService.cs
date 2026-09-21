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
        private readonly IGroundRoutePlanner planner;
        private readonly Subject<LinePreviewState?> changed = new();
        public Observable<LinePreviewState?> Changed => changed;
        public LinePreviewState? Current { get; private set; }
        public LinePreviewService(FlowNetwork network, IGroundRoutePlanner planner)
        { this.network = network; this.planner = planner; }
        public void Generate(string sourceId, string destinationId)
        {
            NodeDefinition? from = network.NodeDefinitions.FirstOrDefault(n=>n.Id == sourceId);
            NodeDefinition? to = network.NodeDefinitions.FirstOrDefault(n=>n.Id == destinationId);
            if (from == null || to == null)
            { Publish(sourceId,destinationId,Array.Empty<Vector3>(),new GroundRouteResult(RouteFailure.InvalidPoints)); return; }
            GroundRouteResult result = planner.Generate(from.Position,to.Position);
            Publish(sourceId,destinationId,result.Route?.Points ?? new[] { from.Position,to.Position },result);
        }
        public void UpdatePoints(IReadOnlyList<Vector3> points)
        {
            if (Current == null) throw new InvalidOperationException("Select endpoints before editing a Preview.");
            NodeDefinition? from = network.NodeDefinitions.FirstOrDefault(n=>n.Id == Current.SourceId);
            NodeDefinition? to = network.NodeDefinitions.FirstOrDefault(n=>n.Id == Current.DestinationId);
            GroundRouteResult result = points.Count >= 2 && from != null && to != null &&
                points[0] == from.Position && points[points.Count-1] == to.Position ? planner.Validate(points) :
                new GroundRouteResult(RouteFailure.EndpointMismatch);
            Publish(Current.SourceId,Current.DestinationId,points,result);
        }
        private void Publish(string sourceId, string destinationId, IReadOnlyList<Vector3> points, GroundRouteResult geometry)
        {
            NetworkSnapshot snapshot = network.Snapshot();
            Current = new LinePreviewState(sourceId,destinationId,points,geometry,network.CheckConnection(sourceId,destinationId),
                snapshot.Nodes.FirstOrDefault(n=>n.Definition.Id==sourceId),snapshot.Nodes.FirstOrDefault(n=>n.Definition.Id==destinationId),network.Settings);
            changed.OnNext(Current);
        }
        public void Cancel() { Current = null; changed.OnNext(null); }
        public void Dispose() { changed.OnCompleted(); changed.Dispose(); }
    }
}
