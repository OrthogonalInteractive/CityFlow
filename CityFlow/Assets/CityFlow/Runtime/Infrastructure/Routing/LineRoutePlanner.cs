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
        public LineRouteResult Validate(IReadOnlyList<Vector3> points)
        {
            RouteFailure failure = stage.ValidateRoute(points, clearance, out int segment);
            if (failure != RouteFailure.None) return new LineRouteResult(failure, segment);
            try { return new LineRouteResult(new LineRoute(points)); }
            catch (ArgumentException) { return new LineRouteResult(RouteFailure.InvalidPoints); }
        }
        public LineRouteResult Generate(Vector3 start, Vector3 end)
        {
            RouteFailure startFailure = stage.ValidatePoint(start,clearance), endFailure = stage.ValidatePoint(end,clearance);
            if (startFailure != RouteFailure.None) return new LineRouteResult(startFailure);
            if (endFailure != RouteFailure.None) return new LineRouteResult(endFailure);
            LineRouteResult direct = Validate(new[] { start,end });
            if (direct.IsValid || direct.Failure == RouteFailure.InvalidPoints) return direct;
            var vertices = new List<Vector3> { start,end };
            // A small numerical margin keeps visibility edges outside inclusive collision boundaries.
            const float cornerMargin = 0.002f; // [m], provisional and covered by narrow-passage tests.
            float margin = clearance + cornerMargin;
            var heights = new List<float> { stage.GroundHeight };
            if (stage.AllowsHeight)
            {
                heights.Add(start.y); heights.Add(end.y);
                foreach (Bounds building in stage.Buildings)
                {
                    heights.Add(building.max.y + margin);
                    heights.Add(building.min.y - margin);
                }
            }
            float[] levels = heights.Where(y => y >= stage.GroundHeight && y <= stage.CeilingHeight).Distinct().ToArray();
            void Add(Vector3 point)
            {
                if (stage.IsWalkable(point, clearance) && !vertices.Contains(point)) vertices.Add(point);
            }
            foreach (Bounds building in stage.Buildings)
            {
                foreach (float y in levels)
                foreach (float x in new[] { building.min.x-margin, building.max.x+margin })
                    foreach (float z in new[] { building.min.z-margin, building.max.z+margin })
                        Add(new Vector3(x, y, z));
                if (!stage.AllowsHeight) continue;
                // Sample roof and underside edges as well as corners. A 3D shortest path can
                // cross an edge away from its endpoints; these samples keep straight overpasses short.
                foreach (float y in new[] { building.max.y + margin, building.min.y - margin })
                {
                    float left = building.min.x - margin, right = building.max.x + margin;
                    float back = building.min.z - margin, front = building.max.z + margin;
                    foreach (Vector3 endpoint in new[] { start, end })
                    {
                        foreach (float x in new[] { left, right }) Add(new Vector3(x, y, Mathf.Clamp(endpoint.z, back, front)));
                        foreach (float z in new[] { back, front }) Add(new Vector3(Mathf.Clamp(endpoint.x, left, right), y, z));
                    }
                    Vector3 delta = end - start;
                    if (Mathf.Abs(delta.x) > 1e-6f)
                        foreach (float x in new[] { left, right })
                            Add(new Vector3(x, y, Mathf.Clamp(start.z + delta.z * (x - start.x) / delta.x, back, front)));
                    if (Mathf.Abs(delta.z) > 1e-6f)
                        foreach (float z in new[] { back, front })
                            Add(new Vector3(Mathf.Clamp(start.x + delta.x * (z - start.z) / delta.z, left, right), y, z));
                }
            }
            int count = vertices.Count;
            var edges = new double[count,count];
            for (int i = 0; i < count; i++)
                for (int j = i+1; j < count; j++)
                    if (Validate(new[] { vertices[i],vertices[j] }).IsValid)
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
                if (best < 0) return new LineRouteResult(RouteFailure.SearchFailed);
                if (best == 1)
                {
                    var path = new List<Vector3>();
                    for (int i = best; i >= 0; i = previous[i]) path.Add(vertices[i]);
                    path.Reverse();
                    for (int i = 0; i+2 < path.Count;)
                    {
                        if (Validate(new[] { path[i],path[i+2] }).IsValid) path.RemoveAt(i+1);
                        else i++;
                    }
                    return Validate(path);
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
