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
    [RequireComponent(typeof(UIDocument))]
    public sealed class SourceStatusView : MonoBehaviour
    {
        private sealed class SourceView
        {
            public readonly VisualElement Card;
            public readonly ProgressBar Buffer;
            public readonly Label Generation, Warning;
            public readonly GameObject Waiting;
            public readonly LineRenderer Pulse;
            public readonly List<Renderer> Dots = new();
            public long Generated;
            public double LastGeneration = double.NegativeInfinity;
            public SourceView(string id, Transform parent, VisualElement monitor, Material pulseMaterial, Vector3 position)
            {
                Card = new VisualElement(); Card.AddToClassList("source-card"); Card.pickingMode = PickingMode.Ignore;
                Card.Add(new Label("SOURCE " + id) { pickingMode = PickingMode.Ignore });
                Buffer = new ProgressBar { name = "source-buffer-" + id, pickingMode = PickingMode.Ignore };
                Generation = new Label { name = "source-generation-" + id, pickingMode = PickingMode.Ignore };
                Warning = new Label { name = "source-warning-" + id, pickingMode = PickingMode.Ignore };
                Generation.AddToClassList("source-generation"); Warning.AddToClassList("source-warning");
                Card.Add(Buffer); Card.Add(Generation); Card.Add(Warning); monitor.Add(Card);
                // ProgressBar creates internal elements that must also pass through world input.
                Card.Query().ForEach(element => element.pickingMode = PickingMode.Ignore);
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
        private UIDocument? document;
        private VisualElement? root;
        private readonly Dictionary<string, SourceView> sources = new();
        private readonly Dictionary<FlowColor, Material> materials = new();
        private Material? pulseMaterial;

        public void Initialize(FlowNetwork state, FlowSimulation clock, ConnectionSession wiring, NodeConnectionController controller)
        {
            network = state; simulation = clock; connection = wiring; cameraController = controller;
            document = GetComponent<UIDocument>();
        }

        private void LateUpdate()
        {
            if (document == null || network == null || simulation == null || cameraController == null) return;
            if (root != document.rootVisualElement) { Clear(); root = document.rootVisualElement; }
            VisualElement monitor = root.Q("source-monitor");
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
                    view = new SourceView(id, transform, monitor, pulseMaterial, node.Definition.Position);
                    sources.Add(id, view);
                }
                if (node.GeneratedCount != view.Generated)
                {
                    view.Generated = node.GeneratedCount;
                    view.LastGeneration = simulation.ElapsedSeconds;
                }
                int count = node.Buffer.Count, capacity = network.Settings.MaxBuffer;
                view.Buffer.highValue = capacity; view.Buffer.value = Mathf.Min(count, capacity);
                view.Buffer.title = $"BUFFER  {count} / {capacity}";
                double preparing = simulation.SourceStartRemaining(id);
                view.Generation.text = node.LastGeneratedColor.HasValue
                    ? $"GENERATED {node.GeneratedCount} · {node.LastGeneratedColor.Value.ToString().ToUpperInvariant()}"
                    : "GENERATED 0";
                bool overloaded = count >= capacity;
                // Provisional warning threshold is 80%; it does not change the loss condition.
                view.Card.EnableInClassList("source-warning-active", count >= capacity * 0.8f);
                view.Card.EnableInClassList("source-overloaded", overloaded);
                view.Warning.text = network.GameOverSourceId == id ? "GAME OVER · SOURCE OVERLOAD" : overloaded
                    ? $"{Math.Max(0, network.Settings.OverloadGrace - node.OverloadSeconds):0.0}s TO GAME OVER"
                    : preparing > 0 ? $"PREPARING {preparing:0.0}s"
                    : $"{(count >= capacity * 0.8f ? "WARNING · " : "")}{capacity - count} FREE";

                // This display is bounded; the HUD retains the exact count, including overflow.
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
                view.Card.RemoveFromHierarchy();
                if (view.Waiting != null) Destroy(view.Waiting);
                if (view.Pulse != null) Destroy(view.Pulse.gameObject);
            }
            sources.Clear(); root = null;
        }
        private void OnDisable() => Clear();
        private void OnDestroy()
        {
            foreach (Material material in materials.Values) if (material != null) Destroy(material);
            if (pulseMaterial != null) Destroy(pulseMaterial);
        }
    }
}
