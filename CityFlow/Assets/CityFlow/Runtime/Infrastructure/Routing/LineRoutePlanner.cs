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
        private readonly RouteObstacleIndex obstacles;
        private readonly Bounds[] searchBuildings;
        private readonly Dictionary<float, IReadOnlyList<Vector3>> planeCorners = new();
        private Rect cachedArea;
        public LineRoutePlanner(StageDefinition stage, float clearance)
        {
            this.stage = stage ?? throw new ArgumentNullException(nameof(stage));
            stage.Validate(clearance); this.clearance = clearance;
            searchBuildings = stage.Buildings.Where((b, i) => !stage.Buildings.Where((_, j) => j != i).Any(other =>
                other.min.x <= b.min.x && other.max.x >= b.max.x && other.min.z <= b.min.z && other.max.z >= b.max.z &&
                (!stage.AllowsHeight || other.min.y <= b.min.y && other.max.y >= b.max.y) && other != b)).Distinct().ToArray();
            obstacles = new RouteObstacleIndex(stage.Buildings, clearance, stage.AllowsHeight);
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
            var planeReach = new Dictionary<float, float>();
            if (stage.AllowsHeight)
                foreach (Bounds building in searchBuildings)
                {
                    float reach = DistanceToFootprint(start, building, margin) + DistanceToFootprint(end, building, margin);
                    // Raising a plane past a bottom only adds an obstacle and lift cost; it cannot improve a route.
                    float height = building.max.y + margin;
                    heights.Add(height);
                    if (!planeReach.TryGetValue(height, out float known) || reach < known) planeReach[height] = reach;
                }
            LineRouteResult? best = null;
            float bestHeight = float.PositiveInfinity;
            float horizontalDistance = Vector2.Distance(new Vector2(start.x, start.z), new Vector2(end.x, end.z));
            // Provisional large-city search budget. Small authored stages keep their exhaustive height comparison.
            var interiorHeights = heights.Where(y => y > minimum && y < maximum).Distinct();
            if (stage.Buildings.Count > 256) interiorHeights = interiorHeights.OrderBy(y => planeReach[y]).ThenBy(y => y).Take(16);
            // An upper-plane route often supplies a tight bound before exploring lower city streets.
            foreach (float height in new[] { minimum, maximum }.Distinct().Concat(interiorHeights.OrderBy(y => y)))
            {
                // Even a straight horizontal route cannot beat this lower bound.
                double budget = best?.Route == null ? double.PositiveInfinity : best.Route.Length - (height - start.y) - (height - end.y);
                if (horizontalDistance > budget) continue;
                // A distant building cannot introduce a useful height change inside a shorter route's ellipse.
                if (height != minimum && height != maximum && planeReach[height] > budget + 0.001) continue;
                Vector3 a = new(start.x, height, start.z), b = new(end.x, height, end.z);
                if (start != a && obstacles.Blocked(start, a)) continue;
                if (end != b && obstacles.Blocked(b, end)) continue;
                var horizontal = GeneratePlane(a, b, margin, budget);
                if (horizontal == null) continue;
                var path = new List<Vector3> { start };
                foreach (Vector3 point in horizontal)
                    if (path[path.Count - 1] != point) path.Add(point);
                if (path[path.Count - 1] != end) path.Add(end);
                var result = Validate(source, destination, path);
                if (result.IsValid && (best?.Route == null || result.Route!.Length < best.Route.Length ||
                    result.Route.Length == best.Route.Length && height < bestHeight)) { best = result; bestHeight = height; }
            }
            return best ?? new LineRouteResult(RouteFailure.SearchFailed);
        }

        private static float DistanceToFootprint(Vector3 point, Bounds building, float margin)
        {
            float x = Mathf.Max(building.min.x - margin - point.x, 0, point.x - building.max.x - margin);
            float z = Mathf.Max(building.min.z - margin - point.z, 0, point.z - building.max.z - margin);
            return Mathf.Sqrt(x * x + z * z);
        }

        private IReadOnlyList<Vector3>? GeneratePlane(Vector3 start, Vector3 end, float margin, double budget)
        {
            if (start == end) return new[] { start };
            bool Visible(Vector3 a, Vector3 b) => !obstacles.Blocked(a, b);
            if (Visible(start, end)) return new[] { start, end };
            if (!PlanarFreeSpace.Connected(stage, clearance, start, end)) return null;
            var vertices = new List<Vector3> { start, end };
            // Every point of a shorter route lies inside this distance ellipse.
            vertices.AddRange(Corners(start.y, margin).Where(p => p != start && p != end &&
                (double)Vector3.Distance(start, p) + Vector3.Distance(p, end) <= budget + 0.001));
            int count = vertices.Count;
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
                // Query only edges A* actually needs instead of materializing the full city visibility matrix.
                IEnumerable<int> neighbors = Enumerable.Range(0, count);
                if (count > 256) neighbors = neighbors.Where(i => !closed[i] && i != best)
                    .OrderBy(i => (vertices[best] - vertices[i]).sqrMagnitude).Take(48).Append(1).Distinct();
                foreach (int next in neighbors)
                {
                    if (closed[next] || next == best) continue;
                    double candidate = cost[best] + Vector3.Distance(vertices[best], vertices[next]);
                    if (candidate + Vector3.Distance(vertices[next], end) > Math.Min(budget, cost[1]) + 0.001) continue;
                    if (candidate < cost[next] && Visible(vertices[best], vertices[next]))
                    { cost[next] = candidate; previous[next] = best; open[next] = true; }
                }
            }
        }

        private IReadOnlyList<Vector3> Corners(float height, float margin)
        {
            if (cachedArea != stage.WalkableArea) { planeCorners.Clear(); cachedArea = stage.WalkableArea; }
            if (planeCorners.TryGetValue(height, out var cached)) return cached;
            var vertices = new List<Vector3>();
            var unique = new HashSet<Vector3>();
            foreach (Bounds building in stage.Buildings)
            {
                if (stage.AllowsHeight && (height < building.min.y - clearance || height > building.max.y + clearance)) continue;
                foreach (float x in new[] { building.min.x - margin, building.max.x + margin })
                    foreach (float z in new[] { building.min.z - margin, building.max.z + margin })
                    {
                        var point = new Vector3(x, height, z);
                        if (x >= cachedArea.xMin + clearance && x <= cachedArea.xMax - clearance &&
                            z >= cachedArea.yMin + clearance && z <= cachedArea.yMax - clearance &&
                            unique.Add(point) && !obstacles.Blocked(point, point)) vertices.Add(point);
                    }
            }
            // Bound cache memory when surveying many roof heights. Buildings themselves never change during a session.
            if (planeCorners.Count >= 32) planeCorners.Clear();
            planeCorners[height] = vertices;
            return vertices;
        }
    }
}
