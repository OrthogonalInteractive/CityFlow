#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using CityFlow.Application.Routing;
using CityFlow.Domain.Spatial;
using UnityEngine;

namespace CityFlow.Infrastructure.Routing
{
    public sealed class LineRoutePlanner : ILineRoutePlanner
    {
        private readonly StageDefinition stage;
        private readonly float clearance;
        public LineRoutePlanner(StageDefinition stage, float clearance)
        {
            this.stage = stage ?? throw new ArgumentNullException(nameof(stage));
            stage.Validate(clearance); this.clearance = clearance;
        }
        public LineRouteResult Validate(NodeDefinition source, NodeDefinition destination, IReadOnlyList<Vector3> points)
        {
            RouteFailure failure = stage.ValidateConnectionRoute(source, destination, points, clearance, out int segment);
            if (failure != RouteFailure.None) return new LineRouteResult(failure, segment);
            return new LineRouteResult(new LineRoute(points));
        }

        public LineRouteResult Generate(NodeDefinition source, NodeDefinition destination)
        {
            Vector3 start = source.Position, end = destination.Position;
            RouteFailure startFailure = stage.ValidatePoint(start, clearance), endFailure = stage.ValidatePoint(end, clearance);
            if (startFailure != RouteFailure.None) return new LineRouteResult(startFailure);
            if (endFailure != RouteFailure.None) return new LineRouteResult(endFailure);
            float minimum = Mathf.Max(start.y, end.y);
            float maximum = Mathf.Min(stage.ConnectionCeiling(source), stage.ConnectionCeiling(destination));
            if (minimum > maximum)
                return new LineRouteResult(source is RelayNodeDefinition || destination is RelayNodeDefinition
                    ? RouteFailure.RelayHeightLimit : RouteFailure.VerticalAtRelayOnly);

            // Each candidate has one horizontal plane and vertical legs only at endpoint Relays.
            const float cornerMargin = 0.002f; // [m], provisional numerical clearance.
            float margin = clearance + cornerMargin;
            var heights = new List<float> { minimum, maximum };
            if (stage.AllowsHeight)
                foreach (Bounds building in stage.Buildings)
                {
                    heights.Add(building.max.y + margin);
                    heights.Add(building.min.y - margin);
                }
            LineRouteResult? best = null;
            foreach (float height in heights.Where(y => y >= minimum && y <= maximum).Distinct().OrderBy(y => y))
            {
                Vector3 a = new(start.x, height, start.z), b = new(end.x, height, end.z);
                if (start != a && stage.ValidateRoute(new[] { start, a }, clearance, out _) != RouteFailure.None) continue;
                if (end != b && stage.ValidateRoute(new[] { b, end }, clearance, out _) != RouteFailure.None) continue;
                var horizontal = GeneratePlane(a, b, margin);
                if (horizontal == null) continue;
                var path = new List<Vector3> { start };
                foreach (Vector3 point in horizontal)
                    if (path[path.Count - 1] != point) path.Add(point);
                if (path[path.Count - 1] != end) path.Add(end);
                var result = Validate(source, destination, path);
                if (result.IsValid && (best?.Route == null || result.Route!.Length < best.Route.Length)) best = result;
            }
            return best ?? new LineRouteResult(RouteFailure.SearchFailed);
        }

        private IReadOnlyList<Vector3>? GeneratePlane(Vector3 start, Vector3 end, float margin)
        {
            if (start == end) return new[] { start };
            bool Visible(Vector3 a, Vector3 b) => stage.ValidateRoute(new[] { a, b }, clearance, out _) == RouteFailure.None;
            if (Visible(start, end)) return new[] { start, end };
            var vertices = new List<Vector3> { start, end };
            foreach (Bounds building in stage.Buildings)
            {
                if (stage.AllowsHeight && (start.y < building.min.y - clearance || start.y > building.max.y + clearance)) continue;
                foreach (float x in new[] { building.min.x - margin, building.max.x + margin })
                    foreach (float z in new[] { building.min.z - margin, building.max.z + margin })
                    {
                        var point = new Vector3(x, start.y, z);
                        if (stage.IsWalkable(point, clearance) && !vertices.Contains(point)) vertices.Add(point);
                    }
            }
            int count = vertices.Count;
            var edges = new double[count,count];
            for (int i = 0; i < count; i++)
                for (int j = i+1; j < count; j++)
                    if (Visible(vertices[i], vertices[j]))
                        edges[i,j] = edges[j,i] = Vector3.Distance(vertices[i],vertices[j]);
            var cost = Enumerable.Repeat(double.PositiveInfinity,count).ToArray();
            var previous = Enumerable.Repeat(-1,count).ToArray();
            var open = new bool[count]; var closed = new bool[count];
            cost[0] = 0; open[0] = true;
            while (true)
            {
                int best = -1; double bestScore = double.PositiveInfinity;
                // Stable index order breaks equal-cost ties deterministically.
                for (int i = 0; i < count; i++)
                    if (open[i])
                    {
                        double score = cost[i] + Vector3.Distance(vertices[i],end);
                        if (score < bestScore) { best = i; bestScore = score; }
                    }
                if (best < 0) return null;
                if (best == 1)
                {
                    var path = new List<Vector3>();
                    for (int i = best; i >= 0; i = previous[i]) path.Add(vertices[i]);
                    path.Reverse();
                    for (int i = 0; i+2 < path.Count;)
                    {
                        if (Visible(path[i], path[i+2])) path.RemoveAt(i+1);
                        else i++;
                    }
                    return path;
                }
                open[best] = false; closed[best] = true;
                for (int next = 0; next < count; next++)
                {
                    if (closed[next] || edges[best,next] == 0) continue;
                    double candidate = cost[best] + edges[best,next];
                    if (candidate < cost[next]) { cost[next] = candidate; previous[next] = best; open[next] = true; }
                }
            }
        }
    }
}
