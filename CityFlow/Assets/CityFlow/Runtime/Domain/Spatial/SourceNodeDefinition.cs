#nullable enable

using CityFlow.Domain.FlowNetwork;
using UnityEngine;

namespace CityFlow.Domain.Spatial
{
    public sealed class SourceNodeDefinition : NodeDefinition
    {
        public override NodeKind Kind => NodeKind.Source;
        public override int MaxOutgoing { get; }
        public double GenerationInterval { get; }
        public double GenerationDelay { get; }

        public SourceNodeDefinition(string id, Vector3 position, int maxOutgoing = 3, double generationInterval = 1, double generationDelay = 0) : base(id, position)
        {
            MaxOutgoing = maxOutgoing;
            GenerationInterval = generationInterval;
            GenerationDelay = generationDelay;
        }
    }
}
