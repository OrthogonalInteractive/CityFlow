#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using CityFlow.Application.UseCases;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Domain.Spatial;
using CityFlow.Presentation.Rendering;
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
            public Label Delivered { get; }
            public Label Elapsed { get; }
            public Label Wave { get; }
            public Elements(VisualElement root)
            {
                Root = root;
                Delivered = Required<Label>(root, "delivered-value");
                Elapsed = Required<Label>(root, "elapsed-value");
                Wave = Required<Label>(root, "wave-value");
            }
        }

        private UIDocument? document;
        private StageDefinition? stage;
        private FlowNetwork? network;
        private FlowSimulation? simulation;
        private Camera? sceneCamera;
        private Elements? elements;
        private ConnectionFocus? focus;
        private CityFlow.Presentation.Overview.OverviewController? overview;
        private readonly Dictionary<string, Label> nodeLabels = new();

        public void Initialize(StageDefinition definition, FlowNetwork flowNetwork, FlowSimulation flowSimulation, Camera camera,
            CityFlow.Presentation.Overview.OverviewController input, ConnectionFocus connectionFocus)
        {
            focus = connectionFocus;
            overview = input;
            stage = definition; network = flowNetwork; simulation = flowSimulation; sceneCamera = camera;
            document = GetComponent<UIDocument>();
            if (isActiveAndEnabled) Bind();
        }
        private void OnEnable() => Bind();
        private void OnDisable()
        {
            if (overview != null) overview.IsPointerBlocked = null;
            if (elements != null) elements.Root.UnregisterCallback<NavigationSubmitEvent>(BlockActionSubmission, TrickleDown.TrickleDown);
            elements = null;
            nodeLabels.Clear();
        }
        private void Bind()
        {
            if (document == null || stage == null || network == null) return;
            VisualElement root = document.rootVisualElement;
            if (root == null) return;
            if (elements != null) elements.Root.UnregisterCallback<NavigationSubmitEvent>(BlockActionSubmission, TrickleDown.TrickleDown);
            // Actions require a pointer click; focused buttons must not execute through Enter or gamepad submit.
            root.RegisterCallback<NavigationSubmitEvent>(BlockActionSubmission, TrickleDown.TrickleDown);
            elements = new Elements(root);
            if (overview != null) overview.IsPointerBlocked = PointerBlocked;
            VisualElement labels = Required<VisualElement>(root, "node-labels");
            labels.Clear();
            nodeLabels.Clear();
            NetworkSnapshot snapshot = network.Snapshot();
            foreach (NodeSnapshot node in snapshot.Nodes)
            {
                string id = node.Definition.Id;
                Label label = Cell(labels, "", "node-label", $"node-label-{id}");
                if (node.BufferCapacity.HasValue)
                {
                    label.AddToClassList("has-buffer");
                    var gauge = new VisualElement { name = $"node-gauge-{id}", pickingMode = PickingMode.Ignore };
                    gauge.AddToClassList("node-gauge"); label.Add(gauge);
                }
                nodeLabels.Add(id, label);
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
        private static void BlockActionSubmission(NavigationSubmitEvent e) => e.StopImmediatePropagation();

        private bool PointerBlocked(Vector2 screen)
        {
            if (document == null || document.rootVisualElement.panel == null) return false;
            var panel = document.rootVisualElement.panel;
            Vector2 point = RuntimePanelUtils.ScreenToPanel(panel,new Vector2(screen.x,Screen.height-screen.y));
            for (VisualElement? element = panel.Pick(point); element != null; element = element.parent)
                if (element.ClassListContains("interactive")) return true;
            return false;
        }
        private void LateUpdate()
        {
            if (document == null || network == null) return;
            // UIDocument recreates its visual tree when disabled and enabled again.
            NetworkSnapshot snapshot = network.Snapshot();
            if (elements == null || elements.Root != document.rootVisualElement ||
                snapshot.Nodes.Count != nodeLabels.Count) Bind();
            Refresh(snapshot);
            PositionNodeLabels(snapshot);
        }
        private void Refresh(NetworkSnapshot snapshot)
        {
            if (elements == null || stage == null || network == null || simulation == null) return;
            elements.Delivered.text = snapshot.DeliveredCount.ToString();
            elements.Elapsed.text = HudClock.Format(simulation.ElapsedSeconds);
            elements.Wave.text = $"WAVE {simulation.Wave}" + (simulation.NextWaveSeconds.HasValue ?
                $" · NEXT {Math.Ceiling(Math.Max(0, simulation.NextWaveSeconds.Value - simulation.ElapsedSeconds))}s" : "");
            foreach (NodeSnapshot node in snapshot.Nodes)
            {
                Label marker = nodeLabels[node.Definition.Id];
                marker.text = "";
                marker.EnableInClassList("unfocused", focus?.IncludesNode(node.Definition.Id) == false);
                marker.EnableInClassList("input-stopped", node.IsInputStopped);
                if (node.BufferCapacity.HasValue)
                {
                    int capacity = node.BufferCapacity.Value;
                    marker.text = $"{node.Buffer.Count}/{capacity}";
                    BufferGauge.Refresh(marker.Q<VisualElement>($"node-gauge-{node.Definition.Id}"), node);
                    bool source = node.Definition.Kind == NodeKind.Source;
                    bool overload = source && node.IsBufferFull;
                    marker.EnableInClassList("source-warning", source && node.Buffer.Count >= capacity * 0.8);
                    marker.EnableInClassList("source-overload", overload);
                    marker.EnableInClassList("source-flash", overload && (int)(simulation.ElapsedSeconds * 2) % 2 == 0);
                    if (overload) marker.text += $"\n{Math.Max(0, network.Settings.OverloadGrace - node.OverloadSeconds):0.0}s TO GAME OVER";
                }
            }
        }
        private void PositionNodeLabels(NetworkSnapshot snapshot)
        {
            if (elements == null || network == null || sceneCamera == null || elements.Root.panel == null) return;
            VisualElement overlay = Required<VisualElement>(elements.Root, "node-labels");
            foreach (NodeSnapshot node in snapshot.Nodes)
            {
                Label label = nodeLabels[node.Definition.Id];
                Vector3 screen = sceneCamera.WorldToScreenPoint(node.Definition.Position + Vector3.up * 5);
                bool visible = node.Buffer.Count > 0 && node.BufferCapacity.HasValue &&
                    screen.z > 0 && sceneCamera.pixelRect.Contains(new Vector2(screen.x, screen.y));
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
