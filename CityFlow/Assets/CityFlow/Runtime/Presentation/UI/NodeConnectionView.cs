#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using CityFlow.Application.Connections;
using CityFlow.Application.Routing;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Presentation.Connections;
using CityFlow.Presentation.Overview;
using CityFlow.Presentation.Rendering;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;

namespace CityFlow.Presentation.UI
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class NodeConnectionView : MonoBehaviour
    {
        private FlowNetwork? network;
        private ConnectionSession? session;
        private readonly Dictionary<string, OverlayLeader> leaders = new();
        private readonly List<Label> groupLabels = new();
        private string listOrder = "";
        private LinePreviewService? preview;
        private OverviewController? overview;
        private NodeConnectionController? controller;
        private Camera? sceneCamera;
        private UIDocument? document;
        private VisualElement? root;
        private readonly Dictionary<string,Button> markers = new();
        private readonly Dictionary<string,Button> candidateOptions = new();
        private readonly List<(Button button,Action handler)> handlers = new();
        public void Initialize(ConnectionSession connection, LinePreviewService linePreview, OverviewController input,
            NodeConnectionController cameraController, Camera camera, FlowNetwork state)
        {
            network = state;
            session = connection; preview = linePreview; overview = input; controller = cameraController;
            sceneCamera = camera; document = GetComponent<UIDocument>(); if (isActiveAndEnabled) Bind();
        }
        private void OnEnable() => Bind();
        private void Bind()
        {
            Unbind();
            if (document == null || network == null || session == null || controller == null) return;
            root = document.rootVisualElement;
            ButtonAction("undo-connection", controller.UndoConnection);
            ButtonAction("connect-cancel",controller.CancelSelection);
            ButtonAction("connect-confirm",() => session.Confirm());
            ButtonAction("connect-review",controller.ToggleOverview);
            ButtonAction("route-edit",controller.BeginEditing);
            foreach (DistanceBand band in Enum.GetValues(typeof(DistanceBand)))
                ButtonAction("band-"+band.ToString().ToLowerInvariant(),() => session.SetFilter(band));
        }
        private void ButtonAction(string name,Action handler)
        {
            if (root == null) return;
            var button = root.Q<Button>(name) ?? throw new InvalidOperationException("Missing connection control: "+name);
            button.clicked += handler; handlers.Add((button,handler));
        }
        private void OnDisable() => Unbind();
        private void Unbind()
        {
            foreach (var binding in handlers) binding.button.clicked -= binding.handler;
            handlers.Clear();
            foreach (var marker in markers.Values) marker.RemoveFromHierarchy();
            foreach (var option in candidateOptions.Values) option.RemoveFromHierarchy();
            foreach (var leader in leaders.Values) leader.Dispose();
            foreach (var label in groupLabels) label.RemoveFromHierarchy();
            leaders.Clear(); groupLabels.Clear(); listOrder = "";
            markers.Clear(); candidateOptions.Clear(); root = null;
        }
        private void LateUpdate()
        {
            if (document == null || network == null || session == null || preview == null || overview == null || controller == null || sceneCamera == null) return;
            if (root != document.rootVisualElement) Bind();
            if (root == null) return;
            VisualElement hud = root.Q("validation-hud");
            hud.EnableInClassList("connection-active",session.IsActive);
            hud.EnableInClassList("node-360",controller.IsNode360);
            hud.EnableInClassList("route-editing",controller.IsEditing);
            bool undoAvailable = controller.CanUndo;
            root.Q("connection-toast").style.display = !session.IsActive && (undoAvailable || controller.Notice.Length > 0)
                ? DisplayStyle.Flex : DisplayStyle.None;
            root.Q<Label>("connection-notice").text = undoAvailable ? "Line connected." : controller.Notice;
            root.Q("undo-connection").style.display = undoAvailable ? DisplayStyle.Flex : DisplayStyle.None;
            root.Q<Button>("route-edit").SetEnabled(preview.Current != null);
            root.Q<Label>("connect-selection").text = session.IsActive ? $"FROM {session.SourceId} / SELECT A TARGET" :
                overview.Selected.NodeId != null ? $"{overview.Selected.NodeId} → NEW CONNECTION" : "Click a Node to start wiring";
            root.Q("connect-selection").userData = session.IsActive && session.SourceId != null ? OverviewTarget.Node(session.SourceId) : default(OverviewTarget);
            root.Q("connect-selection").pickingMode = PickingMode.Position;
            root.Q("connection-panel").style.display = session.IsActive || overview.Selected.LineId.HasValue
                ? new StyleEnum<DisplayStyle>(StyleKeyword.Null) : DisplayStyle.None;
            root.Q<Label>("connect-mode").text = controller.IsNode360 ? "NODE 360 / CONNECTION" : "OVERVIEW / CONNECTION";
            root.Q<Button>("connect-review").text = controller.IsNode360 ? "Review in Overview" : "Return to Node 360";
            var state = preview.Current;
            root.Q<Button>("connect-confirm").SetEnabled(session.IsActive && state?.CanConfirm == true);
            root.Q<Label>("connection-bands").text = $"NEAR ≤ {session.NearLimit:0.#} m / MID ≤ {session.MidLimit:0.#} m / FAR > {session.MidLimit:0.#} m";
            foreach (DistanceBand band in Enum.GetValues(typeof(DistanceBand)))
                root.Q<Button>("band-"+band.ToString().ToLowerInvariant()).EnableInClassList("chosen",session.Filter == band);
            foreach (Button marker in markers.Values) marker.style.display = DisplayStyle.None;
            foreach (var leader in leaders.Values) leader.Hide();
            foreach (Button option in candidateOptions.Values) option.style.display = DisplayStyle.None;
            if (!session.IsActive) return;
            var candidates = session.Candidates();
            ConnectionCandidate? attention = candidates.FirstOrDefault(c => c.Node.Definition.Id == controller.AttentionId);
            root.Q<Label>("candidate-detail").text = attention != null ? ConnectionReadout.Candidate(attention,state) :
                "Hover a marker or candidate to inspect it.\nHover previews / Click connects.";
            root.Q<Label>("connection-count").text = $"{candidates.Count} CANDIDATES / {session.Filter.ToString().ToUpperInvariant()}";
            if (!controller.IsNode360) return;
            RenderCandidateList(candidates,state);
            if (root.layout.width <= 0 || root.layout.height <= 0) return;
            var viewport = OverlayLayout.Viewport(root, sceneCamera);
            var occupied = OverlayLayout.Obstacles(root, sceneCamera, network.Snapshot(), includeMarkers: false);
            foreach (var candidate in candidates.OrderBy(c => c.Distance).ThenBy(c => c.Node.Definition.Id))
            {
                string id = candidate.Node.Definition.Id;
                if (!markers.TryGetValue(id, out Button marker))
                {
                    marker = new Button(() => controller.ConfirmTarget(id)) { name = "candidate-" + id };
                    marker.userData = OverviewTarget.Node(id);
                    marker.AddToClassList("candidate-marker");
                    marker.AddToClassList("interactive");
                    marker.RegisterCallback<PointerEnterEvent>(_ => controller.SetAttention(id));
                    root.Q("connection-markers").Add(marker);
                    markers.Add(id, marker);
                    leaders.Add(id, new OverlayLeader(root.Q("connection-markers"), "candidate-leader-" + id));
                }
                var definition = candidate.Node.Definition;
                Vector3 world = definition.Position + Vector3.up * 1.4f;
                Vector3 projected = sceneCamera.WorldToScreenPoint(world);
                bool outside = projected.z <= 0 || !sceneCamera.pixelRect.Contains(projected);
                bool occluded = controller.IsOccluded(id);
                string visibility = (outside ? "OFFSCREEN " : "") + (occluded ? "OCCLUDED" : "");
                string kind = definition.Kind.ToString().ToUpperInvariant();
                marker.text = $"{id} / {kind} / {candidate.Distance:0} m\n{visibility}\n{Status(candidate, state)}";
                marker.EnableInClassList("chosen", id == controller.AttentionId);
                marker.EnableInClassList("blocked", Group(candidate, state) == 2);
                marker.EnableInClassList("occluded", occluded);
                marker.style.display = DisplayStyle.Flex;
                Vector2 anchor = OverlayLayout.Anchor(root, sceneCamera, world);
                Rect bounds = OverlayLayout.Place(marker, root, anchor + Vector2.one * 24, occupied, viewport);
                occupied.Add(bounds);
                leaders[id].Show(root, anchor, bounds);
            }
        }
        private static int Group(ConnectionCandidate candidate, LinePreviewState? state) =>
            candidate.Failure != ConnectionFailure.None || (state?.DestinationId == candidate.Node.Definition.Id && !state.CanConfirm)
                ? 2 : state?.DestinationId == candidate.Node.Definition.Id && state.CanConfirm ? 0 : 1;

        private static string Status(ConnectionCandidate candidate, LinePreviewState? state) =>
            Group(candidate, state) == 0 ? "PREVIEW READY" : Group(candidate, state) == 2 ? "BLOCKED / INSPECT" : "ROUTE UNCHECKED";

        private void RenderCandidateList(IReadOnlyList<ConnectionCandidate> candidates,LinePreviewState? state)
        {
            if (root == null || session == null || controller == null) return;
            var list = root.Q<ScrollView>("connection-candidates");
            foreach (var candidate in candidates)
            {
                var node = candidate.Node.Definition;
                string id = node.Id;
                if (candidateOptions.ContainsKey(id)) continue;
                var option = new Button(() => controller.ConfirmTarget(id)) { name = "candidate-option-" + id };
                // The fixed candidate readout supplies details; list rows must not open a floating tooltip.
                option.AddToClassList("candidate-option");
                option.RegisterCallback<PointerEnterEvent>(_ => controller.FocusTarget(id));
                var swatch = new Label(node.SinkColor.HasValue ? node.SinkColor.Value.ToString().Substring(0, 1) : "•")
                    { pickingMode = PickingMode.Ignore };
                swatch.AddToClassList("candidate-swatch");
                swatch.style.backgroundColor = node.SinkColor.HasValue ? ValidationCityView.ColorFor(node.SinkColor.Value) : new Color(0.72f, 0.75f, 0.78f);
                option.Add(swatch);
                list.Add(option);
                candidateOptions.Add(id, option);
            }
            var ordered = candidates.OrderBy(c => Group(c, state)).ThenBy(c => c.Distance).ThenBy(c => c.Node.Definition.Id).ToArray();
            string order = string.Join("|", ordered.Select(c => Group(c, state) + ":" + c.Node.Definition.Id));
            Vector2 mouse = Mouse.current?.position.ReadValue() ?? new Vector2(-100, -100);
            Vector2 point = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(mouse.x, Screen.height - mouse.y));
            // Keep click targets stable while the pointer is inside the list.
            if (listOrder.Length == 0 || (order != listOrder && !list.worldBound.Contains(point)))
            {
                foreach (var label in groupLabels) label.RemoveFromHierarchy();
                groupLabels.Clear();
                int previous = -1;
                foreach (var candidate in ordered)
                {
                    int group = Group(candidate, state);
                    if (group != previous)
                    {
                        var label = new Label(group == 0 ? "READY TO CONNECT" : group == 1 ? "ROUTE UNCHECKED" : "BLOCKED");
                        label.AddToClassList("candidate-group-title");
                        list.Add(label); groupLabels.Add(label); previous = group;
                    }
                    list.Add(candidateOptions[candidate.Node.Definition.Id]);
                }
                listOrder = order;
            }
            root.Q<Label>("candidate-list-count").text = $"{candidates.Count} NODES / {session.Filter.ToString().ToUpperInvariant()} · CLICK TO CONNECT";
            foreach (var candidate in candidates)
            {
                var node = candidate.Node;
                string id = node.Definition.Id;
                var option = candidateOptions[id];
                string status = Status(candidate, state);
                option.text = $"{id} · {node.Definition.Kind.ToString().ToUpperInvariant()} · {candidate.Distance:0} m\nIN {node.IncomingUsed}/{node.Definition.MaxIncoming} · {status}";
                option.EnableInClassList("chosen",id == controller.AttentionId);
                option.EnableInClassList("blocked",candidate.Failure != ConnectionFailure.None);
                option.style.display = DisplayStyle.Flex;
            }
        }
    }
}
