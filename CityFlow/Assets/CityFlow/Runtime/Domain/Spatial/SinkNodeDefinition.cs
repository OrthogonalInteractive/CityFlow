#nullable enable

using CityFlow.Domain.FlowNetwork;
using UnityEngine;

namespace CityFlow.Domain.Spatial
{
    public sealed class SinkNodeDefinition : NodeDefinition
    {
        public override NodeKind Kind => NodeKind.Sink;
        public FlowColor Color { get; }
        public override FlowColor? SinkColor => Color;
        public override int MaxIncoming { get; }

        public SinkNodeDefinition(string id, Vector3 position, FlowColor sinkColor, int maxIncoming = 3) : base(id, position)
        {
            Color = sinkColor;
            MaxIncoming = maxIncoming;
        }
    }
}
