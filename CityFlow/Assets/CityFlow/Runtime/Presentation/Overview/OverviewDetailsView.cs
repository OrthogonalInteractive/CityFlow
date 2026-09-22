#nullable enable

using System;
using System.Linq;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Application.UseCases;
using CityFlow.Presentation.Connections;
using CityFlow.Presentation.Rendering;
using CityFlow.Presentation.UI;
using R3;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace CityFlow.Presentation.Overview
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class OverviewDetailsView : MonoBehaviour
    {
        private OverviewController? controller;
        private NodeConnectionController? connection;
        private FlowNetwork? network;
        private FlowSimulation? simulation;
        private ValidationCityView? city;
        private UIDocument? document;
        private Camera? sceneCamera;
        private IDisposable? selectionSubscription;
        private VisualElement? boundRoot;
        private OverlayLeader? leader;
        public bool HasActiveSubscription => selectionSubscription != null;

        public void Initialize(OverviewController input, FlowNetwork flowNetwork, ValidationCityView view,
            FlowSimulation clock, NodeConnectionController wiring, Camera camera)
        {
            sceneCamera = camera;
            simulation = clock;
            connection = wiring;
            controller = input;
            network = flowNetwork;
            city = view;
            document = GetComponent<UIDocument>();
            if (isActiveAndEnabled) Subscribe();
        }
        private void OnEnable() => Subscribe();
        private void Subscribe()
        {
            selectionSubscription?.Dispose();
            selectionSubscription = null;
            if (controller == null || city == null) return;
            selectionSubscription = controller.SelectionChanged.Subscribe(target =>
            {
                if (city != null) city.SetSelection(target.LineId.HasValue ? target : controller.Focused);
            });
            city.SetSelection(controller.Focused);
        }
        private void OnDisable()
        {
            selectionSubscription?.Dispose();
            selectionSubscription = null;
            leader?.Dispose();
            leader = null;
            boundRoot = null;
            if (city != null) city.SetSelection(default);
        }
        private void LateUpdate()
        {
            if (document == null || controller == null || network == null || simulation == null || connection == null || sceneCamera == null) return;
            var root = document.rootVisualElement;
            if (boundRoot != root)
            {
                leader?.Dispose();
                boundRoot = root;
                leader = new OverlayLeader(root.Q("validation-hud"), "hover-leader");
            }
            var panel = root.Q("node-tooltip");
            Vector2 screen = controller.enabled ? controller.HoverScreenPosition : Mouse.current?.position.ReadValue() ?? Vector2.zero;
            Vector2 point = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(screen.x, Screen.height - screen.y));
            OverviewTarget target = default;
            // Candidate controls share the same Node details as the world markers.
            for (VisualElement? hit = root.panel.Pick(point); hit != null; hit = hit.parent)
                if (hit.userData is OverviewTarget node) { target = node; break; }
            if (target.IsEmpty && controller.IsPointerBlocked?.Invoke(screen) != true)
                target = controller.enabled ? controller.Hovered : controller.Pick(screen);
            bool visible = !target.IsEmpty && !connection.IsEditing && simulation.Result == null;
            panel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (city != null)
            {
                var emphasis = visible ? target : !controller.Focused.IsEmpty ? controller.Focused :
                    controller.Selected.LineId.HasValue ? controller.Selected : default;
                if (connection.IsNode360 || connection.IsEditing || simulation.Result != null) emphasis = default;
                city.SetSelection(emphasis);
            }
            if (!visible) { leader?.Hide(); return; }

            var snapshot = network.Snapshot();
            var detail = OverviewReadout.Read(target, snapshot, network.Settings, simulation.GenerationIntervalScale);
            Set("hover-title", detail.Title);
            Set("hover-primary", detail.Primary);
            Set("hover-metrics", detail.Metrics);
            Set("hover-colors", detail.Colors);
            Set("hover-status", detail.Status);
            Set("hover-warning", detail.Warning);
            Set("hover-connections", detail.Connections);
            string activity = detail.Activity;
            if (target.NodeId != null && simulation.SourceStartRemaining(target.NodeId) > 0)
                activity += $"\nPREPARING · {simulation.SourceStartRemaining(target.NodeId):0.0}s";
            Set("hover-activity", activity);
            panel.EnableInClassList("hover-warning", detail.Warning.Length > 0);
            var gauge = root.Q("hover-buffer");
            var selectedNode = snapshot.Nodes.FirstOrDefault(n => n.Definition.Id == target.NodeId);
            gauge.style.display = selectedNode?.BufferCapacity.HasValue == true ? DisplayStyle.Flex : DisplayStyle.None;
            if (selectedNode != null) BufferGauge.Refresh(gauge, selectedNode);

            Vector3? world = selectedNode?.Definition.Position + Vector3.up * 1.4f;
            if (!world.HasValue)
            {
                var line = snapshot.Lines.FirstOrDefault(l => l.Id == target.LineId);
                if (line != null) world = line.Route.PositionAt(line.Route.Length * 0.5f);
            }
            Vector2 anchor = world.HasValue ? OverlayLayout.Anchor(root, sceneCamera, world.Value) : root.WorldToLocal(point);
            float gap = OverlayLayout.Value(root.Q("validation-hud"), "--overlay-gap", 12);
            Rect area = new Rect(0, 0, root.layout.width, root.layout.height);
            if (connection.IsNode360)
            {
                float edge = OverlayLayout.Viewport(root, sceneCamera).xMax;
                area = new Rect(edge, 0, root.layout.width - edge, root.layout.height);
            }
            // Seed a safe location before the first layout pass measures the new content.
            if (panel.worldBound.xMin < area.xMin)
            {
                panel.style.left = area.xMin + gap;
                panel.style.top = gap;
            }
            panel.style.width = Mathf.Min(OverlayLayout.Value(root.Q("validation-hud"), "--hover-width", 320), area.width - gap * 2);
            // Side-panel details may overlay the candidate list, while the city stays unobstructed.
            var obstacles = connection.IsNode360
                ? root.Query(className: "session-controls").ToList().Select(e => new Rect(root.WorldToLocal(e.worldBound.position), e.worldBound.size)).ToList()
                : OverlayLayout.Obstacles(root, sceneCamera, snapshot, panel);
            Rect placed = OverlayLayout.Place(panel, root, anchor + Vector2.one * gap * 2, obstacles, area);
            leader?.Show(root, anchor, placed);

            void Set(string name, string text)
            {
                var label = root.Q<Label>(name);
                label.text = text;
                label.style.display = text.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }
    }
}
