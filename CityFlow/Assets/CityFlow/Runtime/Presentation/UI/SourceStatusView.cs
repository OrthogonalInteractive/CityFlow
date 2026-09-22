#nullable enable

using System;
using System.Collections.Generic;
using CityFlow.Application.Connections;
using CityFlow.Application.UseCases;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Presentation.Connections;
using CityFlow.Presentation.Rendering;
using UnityEngine;
using UnityEngine.UIElements;

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
        private Material? warningMaterial;
        private VisualElement? vignette;
        private static readonly CustomStyleProperty<float> edgeSize = new("--edge-size");

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
                warningMaterial = new Material(pulseMaterial);
                warningMaterial.SetColor("_BaseColor", new Color(1, 0.2f, 0.1f));
            }
            var edge = GetComponent<UIDocument>().rootVisualElement.Q("source-danger");
            if (vignette != edge)
            {
                if (vignette != null) vignette.generateVisualContent -= DrawWarning;
                vignette = edge;
                if (vignette != null) vignette.generateVisualContent += DrawWarning;
            }
            bool overloaded = false;
            double urgency = 0;
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
                if (node.IsInputStopped)
                {
                    overloaded = true;
                    double progress = Math.Min(1, node.OverloadSeconds / network.Settings.OverloadGrace);
                    urgency = Math.Max(urgency, progress);
                    view.Pulse.enabled = !hidden;
                    view.Pulse.loop = false;
                    view.Pulse.transform.localScale = Vector3.one;
                    view.Pulse.sharedMaterial = warningMaterial;
                    view.Pulse.startColor = view.Pulse.endColor = Color.white;
                    for (int i = 0; i < view.Pulse.positionCount; i++)
                    {
                        float angle = -Mathf.PI * 0.5f - (float)(1 - progress) * 2 * Mathf.PI * i / (view.Pulse.positionCount - 1);
                        view.Pulse.SetPosition(i, new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 4.3f);
                    }
                    continue;
                }
                view.Pulse.loop = true;
                view.Pulse.enabled = age < 0.75 && !hidden;
                if (age < 0.75)
                {
                    for (int i = 0; i < view.Pulse.positionCount; i++)
                    {
                        float angle = i * 2 * Mathf.PI / view.Pulse.positionCount;
                        view.Pulse.SetPosition(i, new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 3.5f);
                    }
                    view.Pulse.transform.localScale = Vector3.one * (1 + (float)age);
                    Color color = node.LastGeneratedColor.HasValue ? ValidationCityView.ColorFor(node.LastGeneratedColor.Value) : Color.white;
                    if (node.LastGeneratedColor.HasValue) view.Pulse.sharedMaterial = FlowMaterial(node.LastGeneratedColor.Value);
                    view.Pulse.startColor = view.Pulse.endColor = Color.Lerp(color, Color.white, 0.3f);
                }
            }
            if (vignette != null) vignette.style.opacity = overloaded && !network.IsGameOver ? 0.08f + (float)urgency * 0.12f : 0;
        }

        private static void DrawWarning(MeshGenerationContext context)
        {
            Rect bounds = context.visualElement.contentRect;
            if (bounds.width <= 0 || bounds.height <= 0) return;
            float size = context.visualElement.customStyle.TryGetValue(edgeSize, out float configured) ? configured : 48;
            size = Mathf.Min(size, Mathf.Min(bounds.width, bounds.height) * 0.25f);
            MeshWriteData mesh = context.Allocate(16, 24);
            ushort offset = 0;
            void Quad(Rect rect, byte a, byte b, byte c, byte d)
            {
                var points = new[] { new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMax, rect.yMin),
                    new Vector2(rect.xMax, rect.yMax), new Vector2(rect.xMin, rect.yMax) };
                var alpha = new[] { a, b, c, d };
                for (int i = 0; i < 4; i++) mesh.SetNextVertex(new Vertex {
                    position = new Vector3(points[i].x, points[i].y, Vertex.nearZ), tint = new Color32(255, 60, 35, alpha[i]) });
                foreach (int index in new[] { 0, 1, 2, 2, 3, 0 }) mesh.SetNextIndex((ushort)(offset + index));
                offset += 4;
            }
            Quad(new Rect(0, 0, bounds.width, size), 255, 255, 0, 0);
            Quad(new Rect(0, bounds.height - size, bounds.width, size), 0, 0, 255, 255);
            Quad(new Rect(0, size, size, bounds.height - 2 * size), 255, 0, 0, 255);
            Quad(new Rect(bounds.width - size, size, size, bounds.height - 2 * size), 0, 255, 255, 0);
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
        private void OnDisable()
        {
            Clear();
            if (vignette != null) vignette.generateVisualContent -= DrawWarning;
            vignette = null;
        }
        private void OnDestroy()
        {
            foreach (Material material in materials.Values) if (material != null) Destroy(material);
            if (pulseMaterial != null) Destroy(pulseMaterial);
            if (warningMaterial != null) Destroy(warningMaterial);
        }
    }
}
