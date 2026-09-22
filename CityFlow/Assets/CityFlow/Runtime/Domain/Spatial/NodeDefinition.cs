#nullable enable

using CityFlow.Domain.FlowNetwork;
using UnityEngine;

namespace CityFlow.Domain.Spatial
{
    public abstract class NodeDefinition
    {
        public string Id { get; }
        public Vector3 Position { get; }
        public abstract NodeKind Kind { get; }
        // Unsupported directions have no configurable capacity.
        public virtual int MaxIncoming => 0;
        public virtual int MaxOutgoing => 0;
        public virtual FlowColor? SinkColor => null;

        private protected NodeDefinition(string id, Vector3 position)
        {
            Id = id;
            Position = position;
        }
    }
}
