#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace CityFlow.Domain.FlowNetwork
{
    public sealed partial class FlowNetwork
    {
        private readonly Dictionary<FlowColor, Dictionary<NodeState, LineState[]>> routingTables =
            new Dictionary<FlowColor, Dictionary<NodeState, LineState[]>>();

        private void InvalidateRouting() => routingTables.Clear();

        private Dictionary<NodeState, LineState[]> RoutesFor(FlowColor color)
        {
            if (routingTables.TryGetValue(color, out var cached)) return cached;

            // Preserve direct Sink priority, including the existing connection-order tie-break.
            // Capacity is deliberately absent: full Lines remain reachable and cause waiting.
            var direct = nodes.Values.ToDictionary(node => node, node => node.Outgoing
                .Where(line => line.Status == LineStatus.Running && line.Destination.Definition.SinkColor == color)
                .ToArray());
            var costs = new Dictionary<NodeState, (double Distance, int Steps)>();
            var frontier = new SortedSet<(double Distance, int Steps, string NodeId)>();
            foreach (NodeState sink in nodes.Values.Where(node => node.Definition.SinkColor == color))
            {
                costs[sink] = (0, 0);
                frontier.Add((0, 0, sink.Definition.Id));
            }

            // Multi-source Dijkstra on the reverse directed graph, constrained by direct priority.
            while (frontier.Count > 0)
            {
                var current = frontier.Min;
                frontier.Remove(current);
                NodeState destination = nodes[current.NodeId];
                if (costs[destination] != (current.Distance, current.Steps)) continue;
                foreach (LineState line in destination.Incoming)
                {
                    if (line.Status != LineStatus.Running) continue;
                    LineState[] preferred = direct[line.Source];
                    if (preferred.Length > 0 ? preferred[0] != line : destination.Definition.Kind != NodeKind.Relay)
                        continue;
                    var candidate = (Distance: current.Distance + line.Route.Length, Steps: current.Steps + 1);
                    if (costs.TryGetValue(line.Source, out var previous) &&
                        (previous.Distance < candidate.Distance ||
                         (previous.Distance == candidate.Distance && previous.Steps <= candidate.Steps))) continue;
                    costs[line.Source] = candidate;
                    frontier.Add((candidate.Distance, candidate.Steps, line.Source.Definition.Id));
                }
            }

            var routes = new Dictionary<NodeState, LineState[]>();
            foreach (NodeState node in nodes.Values)
            {
                if (direct[node].Length > 0)
                {
                    routes[node] = direct[node];
                    continue;
                }
                if (!costs.TryGetValue(node, out var current))
                {
                    routes[node] = Array.Empty<LineState>();
                    continue;
                }
                routes[node] = node.Outgoing.Where(line => line.Status == LineStatus.Running &&
                        line.Destination.Definition.Kind == NodeKind.Relay &&
                        costs.TryGetValue(line.Destination, out var remaining) &&
                        // Strict progress prevents cycles even within the numeric tie tolerance.
                        remaining.Distance < current.Distance &&
                        Math.Abs(line.Route.Length + remaining.Distance - current.Distance) <= 1e-6)
                    .OrderBy(line => costs[line.Destination].Steps)
                    .ThenBy(line => line.Id)
                    .ToArray();
            }
            routingTables.Add(color, routes);
            return routes;
        }
    }
}
