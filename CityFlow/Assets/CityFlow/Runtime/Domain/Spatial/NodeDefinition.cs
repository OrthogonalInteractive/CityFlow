#nullable enable

using CityFlow.Domain.FlowNetwork;
using UnityEngine;

namespace CityFlow.Domain.Spatial
{
    public sealed class NodeDefinition
    {
        public string Id { get; }
        public NodeKind Kind { get; }
        public FlowColor? SinkColor { get; }
        public Vector3 Position { get; }
        public int MaxIncoming { get; }
        public int MaxOutgoing { get; }
        public double GenerationInterval { get; }
        public double GenerationDelay { get; }

        public NodeDefinition(string id, NodeKind kind, Vector3 position, int maxIncoming = 3,
            int maxOutgoing = 3, FlowColor? sinkColor = null, double generationInterval = 1, double generationDelay = 0)
        {
            Id = id; Kind = kind; Position = position; MaxIncoming = maxIncoming;
            MaxOutgoing = maxOutgoing; SinkColor = sinkColor; GenerationInterval = generationInterval; GenerationDelay = generationDelay;
        }
    }
}
