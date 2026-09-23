#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CityFlow.Domain.Spatial
{
    public sealed class LineRoute
    {
        public IReadOnlyList<Vector3> Points { get; }
        public double Length { get; }
        public LineRoute(IEnumerable<Vector3> points)
        {
            Vector3[] copy = points.ToArray();
            if (copy.Length < 2) throw new ArgumentException("A route needs at least two points.");
            double length = 0;
            for (int i = 0; i < copy.Length; i++)
            {
                Vector3 p = copy[i];
                if (float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z) || float.IsInfinity(p.x) ||
                    float.IsInfinity(p.y) || float.IsInfinity(p.z))
                    throw new ArgumentException("Route points must be finite.");
                if (i == 0) continue;
                double segment = Vector3.Distance(p, copy[i - 1]);
                if (segment <= 0) throw new ArgumentException("Consecutive route points must differ.");
                length += segment;
            }
            Points = Array.AsReadOnly(copy); Length = length;
        }
        public Vector3 PositionAt(double distance)
        {
            if (double.IsNaN(distance)) throw new ArgumentException("Distance cannot be NaN.");
            double remaining = Math.Max(0, distance);
            for (int i = 1; i < Points.Count; i++)
            {
                double segment = Vector3.Distance(Points[i - 1], Points[i]);
                if (remaining <= segment) return Vector3.Lerp(Points[i - 1], Points[i], (float)(remaining / segment));
                remaining -= segment;
            }
            return Points[Points.Count - 1];
        }
    }
}
