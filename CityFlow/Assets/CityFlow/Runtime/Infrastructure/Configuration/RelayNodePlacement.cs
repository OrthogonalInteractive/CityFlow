#nullable enable

using System;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Domain.Spatial;
using UnityEngine;

namespace CityFlow.Infrastructure.Configuration
{
    [Serializable]
    public sealed class RelayNodePlacement : NodePlacement
    {
        public override NodeKind Kind => NodeKind.Relay;
        [Min(0)] public int MaxIncoming = 3;
        [Min(0)] public int MaxOutgoing = 3;

        public override NodeDefinition ToDefinition() =>
            new RelayNodeDefinition(Id, Position, MaxIncoming, MaxOutgoing);
    }
}
