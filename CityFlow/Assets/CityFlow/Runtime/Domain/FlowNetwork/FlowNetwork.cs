#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using CityFlow.Domain.Spatial;
using UnityEngine;

namespace CityFlow.Domain.FlowNetwork
{
    public sealed partial class FlowNetwork
    {
        private sealed class NodeState
        {
            public double OverloadSeconds { get; set; }
            public NodeDefinition Definition { get; }
            public List<Flow> Buffer { get; } = new List<Flow>();
            public List<LineState> Incoming { get; } = new List<LineState>();
            public List<LineState> Outgoing { get; } = new List<LineState>();
            public NodeState(NodeDefinition definition) => Definition = definition;
        }
        private sealed class InFlightState
        {
            public bool IsStopped { get; set; }
            public Flow Flow { get; }
            public double Distance { get; set; }
            public InFlightState(Flow flow) => Flow = flow;
        }
        private sealed class LineState
        {
            public int Id { get; }
            public NodeState Source { get; }
            public NodeState Destination { get; }
            public LineRoute Route { get; }
            public List<InFlightState> InFlight { get; } = new List<InFlightState>();
            public LineState(int id, NodeState source, NodeState destination, LineRoute route)
            { Id = id; Source = source; Destination = destination; Route = route; }
        }

        private readonly StageDefinition stage;
        private readonly Dictionary<string, NodeState> nodes;
        private readonly List<LineState> lines = new List<LineState>();
        private long nextFlowId = 1;
        private long deliveredCount = 0;
        public bool IsGameOver { get; private set; }
        public string? GameOverSourceId { get; private set; }
        public void EvaluateOverload(double deltaSeconds)
        {
            if (double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds) || deltaSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            if (IsGameOver || deltaSeconds == 0) return;
            foreach (NodeState node in nodes.Values)
            {
                if (node.Definition.Kind != NodeKind.Source) continue;
                node.OverloadSeconds = node.Buffer.Count >= Settings.MaxBuffer ? node.OverloadSeconds + deltaSeconds : 0;
                // Specification 5.2 proposal: continuous overload, including equality, consumes the grace.
                if (node.OverloadSeconds + 1e-9 >= Settings.OverloadGrace && !IsGameOver)
                { IsGameOver = true; GameOverSourceId = node.Definition.Id; }
            }
        }
        public NetworkSettings Settings { get; }
        public IReadOnlyList<NodeDefinition> NodeDefinitions => stage.Nodes;
        public FlowNetwork(StageDefinition stage, NetworkSettings settings)
        {
            this.stage = stage ?? throw new ArgumentNullException(nameof(stage));
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            stage.Validate(settings.Clearance);
            nodes = stage.Nodes.ToDictionary(n => n.Id, n => new NodeState(n));
        }
        public ConnectionFailure CheckConnection(string sourceId, string destinationId)
        {
            if (string.IsNullOrEmpty(sourceId) || !nodes.TryGetValue(sourceId, out NodeState source))
                return ConnectionFailure.UnknownSource;
            if (string.IsNullOrEmpty(destinationId) || !nodes.TryGetValue(destinationId, out NodeState destination))
                return ConnectionFailure.UnknownDestination;
            if (source == destination) return ConnectionFailure.SelfConnection;
            if (source.Outgoing.Any(line => line.Destination == destination))
                return ConnectionFailure.DuplicateDirection;
            if (source.Outgoing.Count >= source.Definition.MaxOutgoing)
                return ConnectionFailure.OutgoingLimit;
            if (destination.Incoming.Count >= destination.Definition.MaxIncoming)
                return ConnectionFailure.IncomingLimit;
            return ConnectionFailure.None;
        }
        public ConnectionResult TryConnect(string sourceId, string destinationId, IReadOnlyList<Vector3> points)
        {
            ConnectionFailure failure = CheckConnection(sourceId, destinationId);
            if (failure != ConnectionFailure.None) return new ConnectionResult(failure);
            NodeState source = nodes[sourceId], destination = nodes[destinationId];
            LineRoute route;
            try { route = new LineRoute(points); }
            catch (ArgumentException) { return new ConnectionResult(ConnectionFailure.InvalidRoute); }
            if (route.Points[0] != source.Definition.Position ||
                route.Points[route.Points.Count - 1] != destination.Definition.Position ||
                !stage.IsRouteWalkable(route, Settings.Clearance))
                return new ConnectionResult(ConnectionFailure.InvalidRoute);
            var created = new LineState(lines.Count + 1, source, destination, route);
            lines.Add(created); source.Outgoing.Add(created); destination.Incoming.Add(created);
            return new ConnectionResult(ConnectionFailure.None, created.Id);
        }
        public Flow GenerateFlow(string sourceId, FlowColor color)
        {
            if (!nodes.TryGetValue(sourceId, out NodeState source) || source.Definition.Kind != NodeKind.Source)
                throw new ArgumentException("Only an existing Source may generate FLOW.", nameof(sourceId));
            if (!stage.Nodes.Any(n => n.SinkColor == color))
                throw new ArgumentException("Generated colors require an existing Sink.", nameof(color));
            var flow = new Flow(nextFlowId++, color);
            // Specification 5.2 proposal: Source generation retains overflow instead of dropping FLOW.
            source.Buffer.Add(flow);
            return flow;
        }
        public NetworkSnapshot Snapshot() => new NetworkSnapshot(stage.Nodes.Select(definition =>
        {
            NodeState node = nodes[definition.Id];
            return new NodeSnapshot(definition, node.Incoming.Count, node.Outgoing.Count, node.Buffer, Settings.MaxBuffer, node.OverloadSeconds);
        }), lines.Select(line => new LineSnapshot(line.Id, line.Source.Definition.Id, line.Destination.Definition.Id,
            line.Route, Settings.MaxInFlight, line.InFlight.Select(flow => new InFlightSnapshot(flow.Flow, flow.Distance, flow.IsStopped)))),
            nextFlowId - 1, deliveredCount);
    }
}
