#nullable enable

using System;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Domain.Spatial;
using UnityEngine;

namespace CityFlow.Infrastructure.Configuration
{
    [Serializable]
    public sealed class SinkNodePlacement : NodePlacement
    {
        public override NodeKind Kind => NodeKind.Sink;
        [Min(0)] public int MaxIncoming = 3;
        public FlowColor SinkColor;

        public override NodeDefinition ToDefinition() =>
            new SinkNodeDefinition(Id, Position, SinkColor, MaxIncoming);
    }
}
