#nullable enable

using CityFlow.Domain.FlowNetwork;
using UnityEngine;

namespace CityFlow.Domain.Spatial
{
    public sealed class RelayNodeDefinition : NodeDefinition
    {
        public override NodeKind Kind => NodeKind.Relay;
        public override int MaxIncoming { get; }
        public override int MaxOutgoing { get; }

        public RelayNodeDefinition(string id, Vector3 position, int maxIncoming = 3, int maxOutgoing = 3) : base(id, position)
        {
            MaxIncoming = maxIncoming;
            MaxOutgoing = maxOutgoing;
        }
    }
}
