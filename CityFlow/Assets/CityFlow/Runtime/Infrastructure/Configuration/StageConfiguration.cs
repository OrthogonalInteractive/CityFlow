#nullable enable

using System;
using System.Linq;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Domain.Spatial;
using UnityEngine;

namespace CityFlow.Infrastructure.Configuration
{
    [CreateAssetMenu(menuName = "City Flow/Stage Configuration")]
    public sealed class StageConfiguration : ScriptableObject
    {
        [Serializable]
        public struct NodePlacement
        {
            public string Id;
            public NodeKind Kind;
            public FlowColor SinkColor;
            public Vector3 Position;
            public int MaxIncoming;
            public int MaxOutgoing;
            [Tooltip("Provisional generation interval per Source [s].")]
            public float GenerationInterval;
            public NodeDefinition ToDefinition() => new NodeDefinition(Id, Kind, Position, MaxIncoming,
                MaxOutgoing, Kind == NodeKind.Sink ? SinkColor : (FlowColor?)null, GenerationInterval);
        }

        [Tooltip("Shared Ground Y coordinate [m]. One Unity unit equals one meter.")]
        public float GroundHeight;
        public Rect WalkableArea = new Rect(-60, -45, 120, 90);
        public Bounds[] Buildings = Array.Empty<Bounds>();
        public NodePlacement[] Nodes = Array.Empty<NodePlacement>();

        public StageDefinition Load(float clearance)
        {
            if (Buildings == null || Nodes == null || Nodes.Length == 0)
                throw new ArgumentException("Stage arrays must be present and contain initial Nodes.");
            var stage = new StageDefinition(GroundHeight, WalkableArea, Buildings, Nodes.Select(node => node.ToDefinition()));
            stage.Validate(clearance);
            return stage;
        }
    }
}
