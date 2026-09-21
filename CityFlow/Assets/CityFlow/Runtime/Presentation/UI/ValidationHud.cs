#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using CityFlow.Application.UseCases;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Domain.Spatial;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityFlow.Presentation.UI
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class ValidationHud : MonoBehaviour
    {
        private sealed class Elements
        {
            public VisualElement Root { get; }
            public Label Summary { get; }
            public Label Capacity { get; }
            public Label Delivered { get; }
            public Label Elapsed { get; }
            public Label Waiting { get; }
            public Label InFlight { get; }
            public Elements(VisualElement root)
            {
                Root = root;
                Summary = Required<Label>(root, "network-summary");
                Capacity = Required<Label>(root, "capacity-summary");
                Delivered = Required<Label>(root, "delivered-value");
                Elapsed = Required<Label>(root, "elapsed-value");
                Waiting = Required<Label>(root, "waiting-value");
                InFlight = Required<Label>(root, "inflight-value");
            }
        }

        private UIDocument? document;
        private StageDefinition? stage;
        private FlowNetwork? network;
        private FlowSimulation? simulation;
        private Camera? sceneCamera;
        private Elements? elements;
        private readonly Dictionary<string, (Label incoming, Label outgoing, Label buffer)> nodeRows = new();
        private readonly Dictionary<int, Label> lineLoads = new();
        private readonly Dictionary<string, Label> nodeLabels = new();

        public void Initialize(StageDefinition definition, FlowNetwork flowNetwork, FlowSimulation flowSimulation, Camera camera)
        {
            stage = definition; network = flowNetwork; simulation = flowSimulation; sceneCamera = camera;
            document = GetComponent<UIDocument>();
            if (isActiveAndEnabled) Bind();
        }
        private void OnEnable() => Bind();
        private void OnDisable()
        {
            elements = null;
            nodeRows.Clear(); lineLoads.Clear(); nodeLabels.Clear();
        }
        private void Bind()
        {
            if (document == null || stage == null || network == null) return;
            VisualElement root = document.rootVisualElement;
            if (root == null) return;
            elements = new Elements(root);
            VisualElement rows = Required<VisualElement>(root, "node-rows");
            VisualElement lines = Required<VisualElement>(root, "line-rows");
            VisualElement labels = Required<VisualElement>(root, "node-labels");
            rows.Clear(); lines.Clear(); labels.Clear();
            nodeRows.Clear(); lineLoads.Clear(); nodeLabels.Clear();
            NetworkSnapshot snapshot = network.Snapshot();
            foreach (NodeSnapshot node in snapshot.Nodes)
            {
                string id = node.Definition.Id;
                var row = new VisualElement(); row.AddToClassList("table-row"); rows.Add(row);
                Cell(row, id, "node-name");
                Label incoming = Cell(row, "", "connection");
                Label outgoing = Cell(row, "", "connection");
                Label buffer = Cell(row, "", "buffer", $"node-buffer-{id}");
                nodeRows.Add(id, (incoming, outgoing, buffer));
                Label label = Cell(labels, $"{id.ToUpperInvariant()} · {node.Definition.Kind.ToString().ToUpperInvariant()}",
                    "node-label", $"node-label-{id}");
                var gauge = new VisualElement(); gauge.AddToClassList("node-gauge");
                var fill = new VisualElement { name = $"node-fill-{id}" }; fill.AddToClassList("node-fill");
                gauge.Add(fill); label.Add(gauge);
                nodeLabels.Add(id, label);
            }
            foreach (LineSnapshot line in snapshot.Lines)
            {
                var row = new VisualElement(); row.AddToClassList("table-row"); lines.Add(row);
                Cell(row, $"{line.SourceId}>{line.DestinationId}", "line-name");
                Cell(row, $"{line.Route.Length:0}m", "length");
                Cell(row, $"{line.Route.Length / network.Settings.FlowSpeed:0.0}s", "duration");
                lineLoads.Add(line.Id, Cell(row, "", "load", $"line-load-{line.Id}"));
            }
            // This read-only HUD must not block future world selection or wiring gestures.
            root.pickingMode = PickingMode.Ignore;
            root.Query().ForEach(element =>
            {
                for (VisualElement? parent = element; parent != null; parent = parent.parent)
                    if (parent.ClassListContains("interactive")) return;
                element.pickingMode = PickingMode.Ignore;
            });
            Refresh(snapshot);
        }
        private void LateUpdate()
        {
            if (document == null || network == null) return;
            // UIDocument recreates its visual tree when disabled and enabled again.
            NetworkSnapshot snapshot = network.Snapshot();
            if (elements == null || elements.Root != document.rootVisualElement ||
                snapshot.Lines.Count != lineLoads.Count || snapshot.Nodes.Count != nodeRows.Count) Bind();
            Refresh(snapshot);
            PositionNodeLabels();
        }
        private void Refresh(NetworkSnapshot snapshot)
        {
            if (elements == null || stage == null || network == null || simulation == null) return;
            int colors = stage.Nodes.Where(node => node.SinkColor.HasValue).Select(node => node.SinkColor).Distinct().Count();
            elements.Summary.text = $"{snapshot.Nodes.Count} NODES   /   {colors} SINK COLORS   /   10 m GRID";
            elements.Capacity.text = $"{snapshot.Lines.Count} DIRECTED LINES   /   CAPACITY {network.Settings.MaxInFlight}";
            Required<Label>(elements.Root, "congestion-status").text = $"INPUT STOPPED {snapshot.Nodes.Count(n => n.IsInputStopped)}   /   STOPPED FLOW {snapshot.Lines.Sum(l => l.InFlight.Count(f => f.IsStopped))}";
            Label sourceStatus = Required<Label>(elements.Root, "source-status");
            NodeSnapshot? warning = snapshot.Nodes.Where(n => n.Definition.Kind == NodeKind.Source && n.IsInputStopped)
                .OrderByDescending(n => n.OverloadSeconds).FirstOrDefault();
            sourceStatus.text = network.IsGameOver ? $"GAME OVER · SOURCE {network.GameOverSourceId}" :
                warning != null ? $"OVERLOAD {warning.Definition.Id} · {Math.Max(0, network.Settings.OverloadGrace - warning.OverloadSeconds):0.0}s LEFT" : "SOURCE STATUS · NORMAL";
            sourceStatus.EnableInClassList("full", warning != null || network.IsGameOver);
            elements.Delivered.text = snapshot.DeliveredCount.ToString("0000");
            elements.Elapsed.text = $"{simulation.ElapsedSeconds:0.0} s";
            elements.Waiting.text = snapshot.Nodes.Sum(node => node.Buffer.Count).ToString("000");
            elements.InFlight.text = snapshot.Lines.Sum(line => line.InFlight.Count).ToString("000");
            foreach (NodeSnapshot node in snapshot.Nodes)
            {
                var row = nodeRows[node.Definition.Id];
                row.incoming.text = $"{node.IncomingUsed}/{node.Definition.MaxIncoming}";
                row.outgoing.text = $"{node.OutgoingUsed}/{node.Definition.MaxOutgoing}";
                row.buffer.text = node.Buffer.Count.ToString();
                Label marker = nodeLabels[node.Definition.Id];
                marker.text = $"{node.Definition.Id} · {node.Definition.Kind.ToString().ToUpperInvariant()}  {node.Buffer.Count}/{network.Settings.MaxBuffer}";
                marker.EnableInClassList("input-stopped", node.IsInputStopped);
                marker.Q<VisualElement>($"node-fill-{node.Definition.Id}").style.width = Length.Percent(Mathf.Min(100, 100f * node.Buffer.Count / network.Settings.MaxBuffer));
                row.buffer.EnableInClassList("full", node.Buffer.Count >= network.Settings.MaxBuffer);
            }
            foreach (LineSnapshot line in snapshot.Lines)
            {
                Label load = lineLoads[line.Id];
                load.text = $"{line.InFlight.Count}/{line.Capacity}";
                load.EnableInClassList("full", line.InFlight.Count >= line.Capacity);
            }
        }
        private void PositionNodeLabels()
        {
            if (elements == null || stage == null || sceneCamera == null || elements.Root.panel == null) return;
            VisualElement overlay = Required<VisualElement>(elements.Root, "node-labels");
            foreach (NodeDefinition node in stage.Nodes)
            {
                Label label = nodeLabels[node.Id];
                Vector3 screen = sceneCamera.WorldToScreenPoint(node.Position + Vector3.up * 5);
                bool visible = screen.z > 0 && sceneCamera.pixelRect.Contains(new Vector2(screen.x, screen.y));
                label.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                if (!visible) continue;
                Vector2 point = RuntimePanelUtils.ScreenToPanel(elements.Root.panel,
                    new Vector2(screen.x, Screen.height - screen.y));
                point = overlay.WorldToLocal(point);
                label.style.left = point.x; label.style.top = point.y;
            }
        }
        private static T Required<T>(VisualElement root, string name) where T : VisualElement =>
            root.Q<T>(name) ?? throw new InvalidOperationException($"HUD element is missing: {name}");
        private static Label Cell(VisualElement row, string text, string className, string name = "")
        {
            var label = new Label(text) { name = name, pickingMode = PickingMode.Ignore };
            label.AddToClassList(className); row.Add(label);
            return label;
        }
    }
}
