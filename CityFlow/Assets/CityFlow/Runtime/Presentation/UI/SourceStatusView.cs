#nullable enable

using System;
using System.Collections.Generic;
using CityFlow.Application.Connections;
using CityFlow.Application.UseCases;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Presentation.Connections;
using CityFlow.Presentation.Rendering;
using UnityEngine;

namespace CityFlow.Presentation.UI
{
    public sealed class SourceStatusView : MonoBehaviour
    {
        private sealed class SourceView
        {
            public readonly GameObject Waiting;
            public readonly LineRenderer Pulse;
            public readonly List<Renderer> Dots = new();
            public long Generated;
            public double LastGeneration = double.NegativeInfinity;
            public SourceView(string id, Transform parent, Material pulseMaterial, Vector3 position)
            {
                Waiting = new GameObject("Source buffer " + id);
                Waiting.transform.SetParent(parent); Waiting.transform.position = position;
                var pulseObject = new GameObject("Source generation " + id);
                pulseObject.transform.SetParent(parent); pulseObject.transform.position = position + Vector3.up * 0.35f;
                Pulse = pulseObject.AddComponent<LineRenderer>();
                Pulse.sharedMaterial = pulseMaterial; Pulse.useWorldSpace = false; Pulse.loop = true;
                Pulse.positionCount = 48; Pulse.widthMultiplier = 0.35f;
                for (int i = 0; i < Pulse.positionCount; i++)
                {
                    float angle = i * 2 * Mathf.PI / Pulse.positionCount;
                    Pulse.SetPosition(i, new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 3.5f);
                }
            }
        }

        private FlowNetwork? network;
        private FlowSimulation? simulation;
        private ConnectionSession? connection;
        private NodeConnectionController? cameraController;
        private readonly Dictionary<string, SourceView> sources = new();
        private readonly Dictionary<FlowColor, Material> materials = new();
        private Material? pulseMaterial;

        public void Initialize(FlowNetwork state, FlowSimulation clock, ConnectionSession wiring, NodeConnectionController controller)
        {
            network = state; simulation = clock; connection = wiring; cameraController = controller;
        }

        private void LateUpdate()
        {
            if (network == null || simulation == null || cameraController == null) return;
            if (pulseMaterial == null)
            {
                pulseMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                pulseMaterial.SetColor("_BaseColor", Color.white);
            }
            foreach (NodeSnapshot node in network.Snapshot().Nodes)
            {
                if (node.Definition.Kind != NodeKind.Source) continue;
                string id = node.Definition.Id;
                if (!sources.TryGetValue(id, out SourceView view))
                {
                    view = new SourceView(id, transform, pulseMaterial, node.Definition.Position);
                    sources.Add(id, view);
                }
                if (node.GeneratedCount != view.Generated)
                {
                    view.Generated = node.GeneratedCount;
                    view.LastGeneration = simulation.ElapsedSeconds;
                }
                int count = node.Buffer.Count, capacity = network.Settings.SourceBufferCapacity;
                // This display is bounded; hover details retain the exact count, including overflow.
                int visible = Math.Min(count, Math.Min(capacity, 50));
                while (view.Dots.Count < visible)
                {
                    var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    dot.name = "Waiting FLOW"; dot.transform.SetParent(view.Waiting.transform, false);
                    int index = view.Dots.Count;
                    dot.transform.localPosition = new Vector3((index % 10 - 4.5f) * 0.9f, 0.6f, 4 + index / 10 * 0.9f);
                    dot.transform.localScale = Vector3.one * 0.7f;
                    Destroy(dot.GetComponent<Collider>());
                    view.Dots.Add(dot.GetComponent<Renderer>());
                }
                bool hidden = cameraController.IsNode360 && connection?.SourceId == id;
                for (int i = 0; i < view.Dots.Count; i++)
                {
                    Renderer dot = view.Dots[i]; dot.gameObject.SetActive(i < visible && !hidden);
                    if (i < visible) dot.sharedMaterial = FlowMaterial(node.Buffer[i].Color);
                }
                double age = simulation.ElapsedSeconds - view.LastGeneration;
                view.Pulse.enabled = age < 0.75 && !hidden;
                if (age < 0.75)
                {
                    view.Pulse.transform.localScale = Vector3.one * (1 + (float)age);
                    Color color = node.LastGeneratedColor.HasValue ? ValidationCityView.ColorFor(node.LastGeneratedColor.Value) : Color.white;
                    if (node.LastGeneratedColor.HasValue) view.Pulse.sharedMaterial = FlowMaterial(node.LastGeneratedColor.Value);
                    view.Pulse.startColor = view.Pulse.endColor = Color.Lerp(color, Color.white, 0.3f);
                }
            }
        }

        private Material FlowMaterial(FlowColor color)
        {
            if (materials.TryGetValue(color, out Material material)) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.SetColor("_BaseColor", Color.Lerp(ValidationCityView.ColorFor(color), Color.white, 0.25f));
            materials.Add(color, material); return material;
        }
        private void Clear()
        {
            foreach (SourceView view in sources.Values)
            {
                if (view.Waiting != null) Destroy(view.Waiting);
                if (view.Pulse != null) Destroy(view.Pulse.gameObject);
            }
            sources.Clear();
        }
        private void OnDisable() => Clear();
        private void OnDestroy()
        {
            foreach (Material material in materials.Values) if (material != null) Destroy(material);
            if (pulseMaterial != null) Destroy(pulseMaterial);
        }
    }
}
