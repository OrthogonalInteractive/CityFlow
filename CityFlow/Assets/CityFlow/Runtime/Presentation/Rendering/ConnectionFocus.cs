#nullable enable

using System.Collections.Generic;
using System.Linq;
using CityFlow.Domain.FlowNetwork;
using UnityEngine;

namespace CityFlow.Presentation.Rendering
{
    public sealed class ConnectionFocus
    {
        private readonly HashSet<string> nodes = new();
        private readonly HashSet<int> lines = new();
        private NetworkSnapshot? snapshot;
        private string? nodeId;
        private MaterialPropertyBlock? tint;
        private static readonly int[] colors = {
            Shader.PropertyToID("_BaseColor"), Shader.PropertyToID("_EmissionColor"),
            Shader.PropertyToID("_EdgeColor"), Shader.PropertyToID("_GridColor")
        };
        // Linear color multiplier; alpha and all transport state remain unchanged.
        private const float DimBrightness = 0.08f;
        public bool IsActive => nodeId != null;
        public bool IncludesNode(string id) => !IsActive || nodes.Contains(id);
        public bool IncludesLine(int id) => !IsActive || lines.Contains(id);

        public void Refresh(NetworkSnapshot state, string? focus)
        {
            if (ReferenceEquals(snapshot, state) && nodeId == focus) return;
            snapshot = state;
            nodeId = focus != null && state.Nodes.Any(node => node.Definition.Id == focus) ? focus : null;
            nodes.Clear();
            lines.Clear();
            if (nodeId == null) return;
            nodes.Add(nodeId);
            foreach (var line in state.Lines)
            {
                if (line.SourceId != nodeId && line.DestinationId != nodeId) continue;
                lines.Add(line.Id);
                nodes.Add(line.SourceId);
                nodes.Add(line.DestinationId);
            }
        }

        public void Apply(Renderer renderer, bool emphasized)
        {
            if (!IsActive || emphasized)
            {
                renderer.SetPropertyBlock(null);
                return;
            }
            tint ??= new MaterialPropertyBlock();
            tint.Clear();
            var material = renderer.sharedMaterial;
            foreach (int property in colors)
            {
                if (!material.HasProperty(property)) continue;
                Color original = material.GetColor(property);
                tint.SetColor(property, new Color(original.r * DimBrightness,
                    original.g * DimBrightness, original.b * DimBrightness, original.a));
            }
            renderer.SetPropertyBlock(tint);
        }
    }
}
