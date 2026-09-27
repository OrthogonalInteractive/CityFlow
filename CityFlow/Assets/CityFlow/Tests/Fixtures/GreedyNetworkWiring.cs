#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Domain.Spatial;
using UnityEngine;

namespace CityFlow.Tests.Fixtures
{
    /// <summary>
    /// Test helper that extends a network until every Source can reach every currently available Sink color.
    /// It reuses existing Lines first and otherwise creates the shortest legal chain of new Lines, respecting
    /// slot limits, static altitude compatibility and the supplied route generator. It never deletes Lines.
    /// This is one possible player network for level validation, not an optimal or recommended one.
    /// </summary>
    public sealed class GreedyNetworkWiring
    {
        // Provisional heuristic weights [m-equivalent]; they shape which of several valid networks the helper builds.
        private const double SlotPenalty = 150, DirectSinkPenalty = 400;
        private readonly Func<NodeDefinition, NodeDefinition, LineRoute?> routes;
        private readonly Dictionary<(string, string), LineRoute?> cache = new();
        public GreedyNetworkWiring(Func<NodeDefinition, NodeDefinition, LineRoute?> routes) => this.routes = routes;

        /// <summary>Returns the Lines created (source, destination) in creation order. Throws when a color stays unreachable.</summary>
        public IReadOnlyList<(string From, string To)> Extend(FlowNetwork network)
        {
            var created = new List<(string, string)>();
            var colors = network.NodeDefinitions.Where(n => n.SinkColor.HasValue).Select(n => n.SinkColor!.Value).Distinct().ToArray();
            foreach (var source in network.NodeDefinitions.OfType<SourceNodeDefinition>())
                foreach (var color in colors)
                {
                    var snapshot = network.Snapshot();
                    if (Reaches(snapshot, source.Id, color)) continue;
                    var path = ShortestChain(network, snapshot, source, color)
                        ?? throw new InvalidOperationException($"{source.Id} cannot reach any {color} Sink with the available slots and routes.\n" + Describe(snapshot));
                    for (int i = 1; i < path.Count; i++)
                    {
                        if (snapshot.Lines.Any(l => l.Status == LineStatus.Running && l.SourceId == path[i - 1].Id && l.DestinationId == path[i].Id)) continue;
                        var route = Route(path[i - 1], path[i]) ?? throw new InvalidOperationException("Chain used a missing route.");
                        var result = network.TryConnect(path[i - 1].Id, path[i].Id, route.Points);
                        if (!result.Succeeded) throw new InvalidOperationException($"{path[i - 1].Id} -> {path[i].Id}: {result.Failure}");
                        created.Add((path[i - 1].Id, path[i].Id));
                    }
                }
            return created;
        }

        /// <summary>Slot usage per Node and every running Line, for diagnosing an unreachable color.</summary>
        public static string Describe(NetworkSnapshot snapshot) =>
            string.Join(" ", snapshot.Nodes.Select(n => $"{n.Definition.Id}[{n.IncomingUsed}/{n.Definition.MaxIncoming}>{n.OutgoingUsed}/{n.Definition.MaxOutgoing}]")) + "\n" +
            string.Join(" ", snapshot.Lines.Where(l => l.Status == LineStatus.Running).Select(l => $"{l.SourceId}>{l.DestinationId}"));

        private static bool Reaches(NetworkSnapshot snapshot, string sourceId, FlowColor color)
        {
            var visited = new HashSet<string> { sourceId };
            var queue = new Queue<string>(); queue.Enqueue(sourceId);
            while (queue.Count > 0)
            {
                string current = queue.Dequeue();
                foreach (var line in snapshot.Lines.Where(l => l.Status == LineStatus.Running && l.SourceId == current))
                {
                    var node = snapshot.Nodes.Single(n => n.Definition.Id == line.DestinationId);
                    if (node.Definition.SinkColor == color) return true;
                    if (node.Definition.Kind == NodeKind.Relay && visited.Add(node.Definition.Id)) queue.Enqueue(node.Definition.Id);
                }
            }
            return false;
        }

        private LineRoute? Route(NodeDefinition from, NodeDefinition to)
        {
            if (!cache.TryGetValue((from.Id, to.Id), out var route)) cache[(from.Id, to.Id)] = route = routes(from, to);
            return route;
        }

        // Dijkstra over Nodes; existing Lines are nearly free so the player's network is reused before new slots are spent.
        private List<NodeDefinition>? ShortestChain(FlowNetwork network, NetworkSnapshot snapshot, SourceNodeDefinition source, FlowColor color)
        {
            var nodes = snapshot.Nodes.ToDictionary(n => n.Definition.Id);
            var cost = new Dictionary<string, double> { [source.Id] = 0 };
            var previous = new Dictionary<string, string>();
            var open = new HashSet<string> { source.Id };
            var closed = new HashSet<string>();
            while (open.Count > 0)
            {
                string currentId = open.OrderBy(id => cost[id]).First();
                open.Remove(currentId); closed.Add(currentId);
                var current = nodes[currentId];
                if (current.Definition.SinkColor == color) return Unwind(currentId, previous, nodes);
                if (current.Definition.Kind == NodeKind.Sink) continue;
                foreach (var next in nodes.Values)
                {
                    string nextId = next.Definition.Id;
                    if (nextId == currentId || closed.Contains(nextId) || next.Definition.Kind == NodeKind.Source) continue;
                    if (next.Definition.Kind == NodeKind.Sink && next.Definition.SinkColor != color) continue;
                    var existing = snapshot.Lines.FirstOrDefault(l => l.Status == LineStatus.Running && l.SourceId == currentId && l.DestinationId == nextId);
                    double edge;
                    if (existing != null) edge = existing.Route.Length * 0.01;
                    else
                    {
                        int outLeft = current.Definition.MaxOutgoing - current.OutgoingUsed;
                        int inLeft = next.Definition.MaxIncoming - next.IncomingUsed;
                        if (outLeft <= 0 || inLeft <= 0) continue;
                        if (!network.SharesAltitude(currentId, nextId)) continue;
                        var route = Route(current.Definition, next.Definition);
                        if (route == null) continue;
                        // Scarce slots cost extra so hubs are preferred over spending a Source's few OUT slots on single Sinks.
                        edge = route.Length + SlotPenalty / outLeft + SlotPenalty / inLeft +
                            (current.Definition.Kind == NodeKind.Source && next.Definition.Kind == NodeKind.Sink ? DirectSinkPenalty : 0);
                    }
                    double candidate = cost[currentId] + edge;
                    if (cost.TryGetValue(nextId, out double known) && known <= candidate) continue;
                    cost[nextId] = candidate; previous[nextId] = currentId; open.Add(nextId);
                }
            }
            return null;
        }

        private static List<NodeDefinition> Unwind(string end, Dictionary<string, string> previous, Dictionary<string, NodeSnapshot> nodes)
        {
            var path = new List<NodeDefinition> { nodes[end].Definition };
            while (previous.TryGetValue(end, out string? before)) { end = before; path.Insert(0, nodes[end].Definition); }
            return path;
        }
    }
}
