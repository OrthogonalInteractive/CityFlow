#nullable enable

using System;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Domain.Spatial;
using UnityEngine;

namespace CityFlow.Infrastructure.Configuration
{
    [Serializable]
    public sealed class SourceNodePlacement : NodePlacement
    {
        public override NodeKind Kind => NodeKind.Source;
        [Min(0)] public int MaxOutgoing = 3;
        [Tooltip("Provisional generation interval [s]; must be positive.")]
        public float GenerationInterval = 3;
        [Tooltip("Provisional preparation time before the first generation interval [s].")]
        [Min(0)] public float GenerationDelay;

        public override NodeDefinition ToDefinition() =>
            new SourceNodeDefinition(Id, Position, MaxOutgoing, GenerationInterval, GenerationDelay);
    }
}
