#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CityFlow.Infrastructure.Routing
{
    // Immutable bounding-volume tree for the authored obstacles. Exact slab tests match the Domain validator.
    internal sealed class RouteObstacleIndex
    {
        private readonly Vector3 min, max;
        private readonly RouteObstacleIndex? left, right;
        private readonly bool empty;

        public RouteObstacleIndex(IReadOnlyList<Bounds> buildings, float clearance, bool height)
            : this(buildings.Select(b => (Min: new Vector3(b.min.x - clearance,
                height ? b.min.y - clearance : float.NegativeInfinity, b.min.z - clearance),
                Max: new Vector3(b.max.x + clearance, height ? b.max.y + clearance : float.PositiveInfinity,
                    b.max.z + clearance))).ToArray()) { }

        private RouteObstacleIndex((Vector3 Min, Vector3 Max)[] bounds)
        {
            empty = bounds.Length == 0;
            if (empty) return;
            min = bounds[0].Min; max = bounds[0].Max;
            foreach (var b in bounds) { min = Vector3.Min(min, b.Min); max = Vector3.Max(max, b.Max); }
            if (bounds.Length == 1) return;
            bool splitX = max.x - min.x >= max.z - min.z;
            var sorted = bounds.OrderBy(b => splitX ? b.Min.x + b.Max.x : b.Min.z + b.Max.z).ToArray();
            int middle = sorted.Length / 2;
            left = new RouteObstacleIndex(sorted.Take(middle).ToArray());
            right = new RouteObstacleIndex(sorted.Skip(middle).ToArray());
        }

        public bool Blocked(Vector3 from, Vector3 to)
        {
            if (empty) return false;
            Vector3 delta = to - from;
            double enter = 0, exit = 1;
            if (!Clip(from.x, delta.x, min.x, max.x, ref enter, ref exit) ||
                !Clip(from.z, delta.z, min.z, max.z, ref enter, ref exit) ||
                !Clip(from.y, delta.y, min.y, max.y, ref enter, ref exit)) return false;
            return left == null || left.Blocked(from, to) || (right != null && right.Blocked(from, to));
        }

        private static bool Clip(double start, double delta, double min, double max, ref double enter, ref double exit)
        {
            if (delta == 0) return start >= min && start <= max;
            double a = (min - start) / delta, b = (max - start) / delta;
            enter = Math.Max(enter, Math.Min(a, b)); exit = Math.Min(exit, Math.Max(a, b));
            return enter <= exit;
        }
    }
}
