#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CityFlow.Domain.Spatial
{
    public sealed class StageDefinition
    {
        public float GroundHeight { get; }
        public Rect WalkableArea { get; }
        public IReadOnlyList<Bounds> Buildings { get; }
        public IReadOnlyList<NodeDefinition> Nodes { get; }
        public StageDefinition(float groundHeight, Rect walkableArea, IEnumerable<Bounds> buildings,
            IEnumerable<NodeDefinition> nodes)
        {
            GroundHeight = groundHeight; WalkableArea = walkableArea;
            Buildings = Array.AsReadOnly(buildings.ToArray());
            Nodes = Array.AsReadOnly(nodes.ToArray());
        }
        public void Validate(float clearance)
        {
            if (!Finite(GroundHeight) || !Finite(clearance) || clearance < 0 ||
                !Finite(WalkableArea.x) || !Finite(WalkableArea.y) || !Finite(WalkableArea.width) ||
                !Finite(WalkableArea.height) || WalkableArea.width <= 0 || WalkableArea.height <= 0)
                throw new ArgumentException("Ground and clearance must be finite and define a positive area.");
            foreach (Bounds building in Buildings)
                if (!Finite(building.center) || !Finite(building.size) ||
                    building.size.x <= 0 || building.size.y <= 0 || building.size.z <= 0)
                    throw new ArgumentException("Buildings require finite positive dimensions.");
            var ids = new HashSet<string>();
            foreach (NodeDefinition node in Nodes)
            {
                if (node == null || string.IsNullOrWhiteSpace(node.Id) || !ids.Add(node.Id) ||
                    !Enum.IsDefined(typeof(FlowNetwork.NodeKind), node.Kind) ||
                    node.MaxIncoming < 0 || node.MaxOutgoing < 0 ||
                    double.IsNaN(node.GenerationInterval) || double.IsInfinity(node.GenerationInterval) ||
                    node.GenerationInterval <= 0)
                    throw new ArgumentException("Nodes require unique IDs and valid connection/generation settings.");
                bool sink = node.Kind == FlowNetwork.NodeKind.Sink;
                if (sink != node.SinkColor.HasValue || (node.SinkColor.HasValue &&
                    !Enum.IsDefined(typeof(FlowNetwork.FlowColor), node.SinkColor.Value)))
                    throw new ArgumentException("Only Sinks must have one valid color.");
                if (!IsWalkable(node.Position, clearance))
                    throw new ArgumentException($"Node {node.Id} is outside walkable Ground.");
            }
            if (Nodes.Any(node => node.Kind == FlowNetwork.NodeKind.Source) &&
                !Nodes.Any(node => node.Kind == FlowNetwork.NodeKind.Sink))
                throw new ArgumentException("Sources require at least one existing Sink color.");
        }

        public bool IsWalkable(Vector3 point, float clearance) => ValidatePoint(point, clearance) == RouteFailure.None;

        public RouteFailure ValidatePoint(Vector3 point, float clearance)
        {
            if (!Finite(point)) return RouteFailure.InvalidPoints;
            if (Mathf.Abs(point.y - GroundHeight) > 0.0001f) return RouteFailure.GroundHeight;
            if (point.x < WalkableArea.xMin + clearance || point.x > WalkableArea.xMax - clearance ||
                point.z < WalkableArea.yMin + clearance || point.z > WalkableArea.yMax - clearance)
                return RouteFailure.OutsideArea;
            return Buildings.Any(b => point.x >= b.min.x-clearance && point.x <= b.max.x+clearance &&
                point.z >= b.min.z-clearance && point.z <= b.max.z+clearance) ? RouteFailure.Obstacle : RouteFailure.None;
        }

        public bool IsRouteWalkable(LineRoute route, float clearance) =>
            ValidateRoute(route.Points, clearance, out _) == RouteFailure.None;

        public RouteFailure ValidateRoute(IReadOnlyList<Vector3> points, float clearance, out int invalidSegment)
        {
            invalidSegment = -1;
            if (points == null || points.Count < 2) return RouteFailure.InvalidPoints;
            for (int i = 0; i < points.Count; i++)
            {
                RouteFailure failure = ValidatePoint(points[i], clearance);
                if (failure != RouteFailure.None) { invalidSegment = Math.Max(0, i-1); return failure; }
                if (i > 0 && (points[i]-points[i-1]).sqrMagnitude <= 0)
                { invalidSegment = i-1; return RouteFailure.InvalidPoints; }
            }
            for (int i = 1; i < points.Count; i++)
                foreach (Bounds building in Buildings)
                {
                    Vector3 start = points[i-1];
                    Vector3 delta = points[i]-start;
                    double enter = 0, exit = 1;
                    if (ClipAxis(start.x, delta.x, building.min.x-clearance, building.max.x+clearance, ref enter, ref exit) &&
                        ClipAxis(start.z, delta.z, building.min.z-clearance, building.max.z+clearance, ref enter, ref exit))
                    { invalidSegment = i-1; return RouteFailure.Obstacle; }
                }
            return RouteFailure.None;
        }
        private static bool ClipAxis(double start, double delta, double min, double max, ref double enter, ref double exit)
        {
            if (delta == 0) return start >= min && start <= max;
            double a = (min - start) / delta, b = (max - start) / delta;
            enter = Math.Max(enter, Math.Min(a, b));
            exit = Math.Min(exit, Math.Max(a, b));
            return enter <= exit;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
    }
}
