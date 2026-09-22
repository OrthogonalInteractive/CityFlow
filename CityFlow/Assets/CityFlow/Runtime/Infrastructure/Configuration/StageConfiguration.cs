#nullable enable

using System;
using System.Linq;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Domain.Spatial;
using CityFlow.Domain.Progression;
using CityFlow.Infrastructure.Routing;
using System.Collections.Generic;
using UnityEngine;

namespace CityFlow.Infrastructure.Configuration
{
    [CreateAssetMenu(menuName = "City Flow/Stage Configuration")]
    public sealed class StageConfiguration : ScriptableObject
    {
        [Serializable]
        public struct LinePlacement
        {
            public string SourceId;
            public string DestinationId;
            public Vector3[] Points;
        }
        [Serializable]
        public struct WavePlacement
        {
            [Tooltip("Provisional elapsed game time at which the next Wave starts [s].")]
            public float StartSeconds;
            [Tooltip("Provisional multiplier of all Source generation intervals; lower means more FLOW.")]
            public float IntervalScale;
            [SerializeReference] public NodePlacement[] Additions;
        }
        public WavePlacement[] Waves = Array.Empty<WavePlacement>();
        public IReadOnlyList<WaveDefinition> LoadWaves(StageDefinition initial, float clearance)
        {
            if(Waves==null) throw new ArgumentException("Wave schedule must be present.");
            var waves=Waves.Select(w=>new WaveDefinition(w.StartSeconds,w.IntervalScale,
                (w.Additions ?? throw new ArgumentException("Wave additions must be present.")).Select(n => (n ?? throw new ArgumentException("Wave Node placement must have a type.")).ToDefinition()))).ToArray();
            var known=initial.Nodes.ToList(); var planner=new GroundRoutePlanner(initial,clearance); double previous=0;
            foreach(var wave in waves)
            {
                if(wave.StartSeconds<=previous) throw new ArgumentException("Wave times must increase."); previous=wave.StartSeconds;
                new StageDefinition(initial.GroundHeight,initial.WalkableArea,initial.Buildings,known.Concat(wave.Additions)).Validate(clearance);
                foreach(var node in wave.Additions.OrderBy(n=>n.Kind==NodeKind.Sink ? 0 : 1))
                {
                    if(!known.Any(n=>planner.Generate(n.Position,node.Position).IsValid))
                        throw new ArgumentException($"Node {node.Id} requires a Ground route to an existing Node.");
                    known.Add(node);
                }
            }
            return Array.AsReadOnly(waves);
        }
        public LinePlacement[] Lines = Array.Empty<LinePlacement>();

        public FlowNetwork LoadNetwork(StageDefinition stage, NetworkSettings settings)
        {
            var network = new FlowNetwork(stage, settings);
            if (Lines == null) throw new ArgumentException("Initial Lines must be present.");
            foreach (LinePlacement line in Lines)
            {
                ConnectionResult result = network.TryConnect(line.SourceId, line.DestinationId, line.Points);
                if (!result.Succeeded) throw new ArgumentException($"Invalid initial Line {line.SourceId} -> {line.DestinationId}: {result.Failure}");
            }
            return network;
        }

        [Tooltip("Shared Ground Y coordinate [m]. One Unity unit equals one meter.")]
        public float GroundHeight;
        public Rect WalkableArea = new Rect(-60, -45, 120, 90);
        public Bounds[] Buildings = Array.Empty<Bounds>();
        [SerializeReference] public NodePlacement[] Nodes = Array.Empty<NodePlacement>();

        public StageDefinition Load(float clearance)
        {
            if (Buildings == null || Nodes == null || Nodes.Length == 0)
                throw new ArgumentException("Stage arrays must be present and contain initial Nodes.");
            var stage = new StageDefinition(GroundHeight, WalkableArea, Buildings, Nodes.Select(node => (node ?? throw new ArgumentException("Node placement must have a type.")).ToDefinition()));
            stage.Validate(clearance);
            return stage;
        }
    }
}
