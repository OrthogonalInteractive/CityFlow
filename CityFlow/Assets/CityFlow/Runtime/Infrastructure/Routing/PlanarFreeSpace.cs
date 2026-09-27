#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using CityFlow.Domain.Spatial;
using UnityEngine;

namespace CityFlow.Infrastructure.Routing
{
    // A sweep of free intervals proves connectivity before attempting a visibility search in a large city.
    internal static class PlanarFreeSpace
    {
        private readonly struct Interval
        {
            public readonly float Min, Max;
            public readonly int Id;
            public Interval(float min, float max, int id) { Min = min; Max = max; Id = id; }
        }

        public static bool Connected(StageDefinition stage, float clearance, Vector3 from, Vector3 to)
        {
            Rect area = stage.WalkableArea;
            float left = area.xMin + clearance, right = area.xMax - clearance;
            float bottom = area.yMin + clearance, top = area.yMax - clearance;
            var rectangles = stage.Buildings.Where(b => !stage.AllowsHeight ||
                from.y >= b.min.y - clearance && from.y <= b.max.y + clearance)
                .Select(b => Rect.MinMaxRect(Mathf.Max(left, b.min.x - clearance), Mathf.Max(bottom, b.min.z - clearance),
                    Mathf.Min(right, b.max.x + clearance), Mathf.Min(top, b.max.z + clearance)))
                .Where(r => r.width > 0 && r.height > 0).ToArray();
            var events = new SortedDictionary<float, List<(int Index, bool Add)>>();
            void AddEvent(float x, int index, bool add)
            {
                if (!events.TryGetValue(x, out var list)) events.Add(x, list = new List<(int, bool)>());
                list.Add((index, add));
            }
            AddEvent(left, -1, false); AddEvent(right, -1, false);
            for (int i = 0; i < rectangles.Length; i++) { AddEvent(rectangles[i].xMin, i, true); AddEvent(rectangles[i].xMax, i, false); }
            var cuts = events.Keys.ToArray();
            var active = new HashSet<int>();
            var parents = new List<int>();
            var ranks = new List<byte>();
            int Root(int id)
            {
                while (parents[id] != id) { parents[id] = parents[parents[id]]; id = parents[id]; }
                return id;
            }
            void Join(int a, int b)
            {
                a = Root(a); b = Root(b); if (a == b) return;
                if (ranks[a] < ranks[b]) parents[a] = b;
                else { parents[b] = a; if (ranks[a] == ranks[b]) ranks[a]++; }
            }
            var previous = new List<Interval>();
            int fromRegion = -1, toRegion = -1;
            for (int slab = 0; slab + 1 < cuts.Length; slab++)
            {
                float x = cuts[slab], nextX = cuts[slab + 1];
                foreach (var change in events[x])
                    if (change.Index >= 0) { if (change.Add) active.Add(change.Index); else active.Remove(change.Index); }
                var current = new List<Interval>();
                void Free(float min, float max)
                {
                    if (max <= min) return;
                    int id = parents.Count; parents.Add(id); ranks.Add(0);
                    current.Add(new Interval(min, max, id));
                    if (from.x >= x && from.x <= nextX && from.z >= min && from.z <= max) fromRegion = id;
                    if (to.x >= x && to.x <= nextX && to.z >= min && to.z <= max) toRegion = id;
                }
                float cursor = bottom;
                foreach (var rectangle in active.Select(i => rectangles[i]).OrderBy(r => r.yMin))
                {
                    Free(cursor, rectangle.yMin);
                    cursor = Mathf.Max(cursor, rectangle.yMax);
                }
                Free(cursor, top);
                int a = 0, b = 0;
                while (a < previous.Count && b < current.Count)
                {
                    var p = previous[a]; var c = current[b];
                    if (Mathf.Max(p.Min, c.Min) < Mathf.Min(p.Max, c.Max)) Join(p.Id, c.Id);
                    if (p.Max < c.Max) a++; else b++;
                }
                previous = current;
            }
            return fromRegion >= 0 && toRegion >= 0 && Root(fromRegion) == Root(toRegion);
        }
    }
}
