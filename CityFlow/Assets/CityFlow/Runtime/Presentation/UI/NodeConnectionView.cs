#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using CityFlow.Application.Connections;
using CityFlow.Application.Routing;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Presentation.Connections;
using CityFlow.Presentation.Overview;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityFlow.Presentation.UI
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class NodeConnectionView : MonoBehaviour
    {
        private ConnectionSession? session;
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
            NodeConnectionController cameraController, Camera camera)
        {
            session = connection; preview = linePreview; overview = input; controller = cameraController;
            sceneCamera = camera; document = GetComponent<UIDocument>(); if (isActiveAndEnabled) Bind();
        }
        private void OnEnable() => Bind();
        private void Bind()
        {
            Unbind();
            if (document == null || session == null || controller == null) return;
            root = document.rootVisualElement;
            ButtonAction("connect-cancel",session.Cancel);
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
            markers.Clear(); candidateOptions.Clear(); root = null;
        }
        private void LateUpdate()
        {
            if (document == null || session == null || preview == null || overview == null || controller == null || sceneCamera == null) return;
            if (root != document.rootVisualElement) Bind();
            if (root == null) return;
            VisualElement hud = root.Q("validation-hud");
            hud.EnableInClassList("connection-active",session.IsActive);
            hud.EnableInClassList("node-360",controller.IsNode360);
            hud.EnableInClassList("route-editing",controller.IsEditing);
            root.Q<Button>("route-edit").SetEnabled(preview.Current != null);
            root.Q<Label>("connect-selection").text = session.IsActive ? $"FROM {session.SourceId} / SELECT A TARGET" :
                overview.Selected.NodeId != null ? $"{overview.Selected.NodeId} → NEW CONNECTION" : "Click a Node to start wiring";
            root.Q<Label>("connect-mode").text = controller.IsNode360 ? "NODE 360 / CONNECTION" : "OVERVIEW / CONNECTION";
            root.Q<Button>("connect-review").text = controller.IsNode360 ? "Review in Overview [V]" : "Return to Node 360 [V]";
            var state = preview.Current;
            root.Q<Button>("connect-confirm").SetEnabled(session.IsActive && state?.CanConfirm == true);
            root.Q<Label>("connection-bands").text = $"NEAR ≤ {session.NearLimit:0.#} m / MID ≤ {session.MidLimit:0.#} m / FAR > {session.MidLimit:0.#} m";
            foreach (DistanceBand band in Enum.GetValues(typeof(DistanceBand)))
                root.Q<Button>("band-"+band.ToString().ToLowerInvariant()).EnableInClassList("chosen",session.Filter == band);
            foreach (Button marker in markers.Values) marker.style.display = DisplayStyle.None;
            foreach (Button option in candidateOptions.Values) option.style.display = DisplayStyle.None;
            if (!session.IsActive) return;
            var candidates = session.Candidates();
            ConnectionCandidate? attention = candidates.FirstOrDefault(c => c.Node.Definition.Id == controller.AttentionId);
            root.Q<Label>("candidate-detail").text = attention != null ? ConnectionReadout.Candidate(attention,state) :
                "Hover a marker or press Tab to inspect a candidate.\nHover previews / Click connects.";
            root.Q<Label>("connection-count").text = $"{candidates.Count} CANDIDATES / {session.Filter.ToString().ToUpperInvariant()}";
            if (!controller.IsNode360) return;
            RenderCandidateList(candidates,state);
            float width = root.layout.width, height = root.layout.height;
            if (width <= 0 || height <= 0) return;
            Rect cameraRect=sceneCamera.rect;
            var viewport=new Rect(cameraRect.x*width,(1-cameraRect.yMax)*height,cameraRect.width*width,cameraRect.height*height);
            var safe=new Rect(viewport.x+104,viewport.y+40,Mathf.Max(1,viewport.width-208),Mathf.Max(1,viewport.height-80));
            var occupied = new List<Rect>();
            foreach (var candidate in candidates.OrderBy(c => c.Node.Definition.Id == controller.AttentionId ? 0 :
                         c.Node.Definition.Id == state?.DestinationId ? 1 : 2).ThenBy(c => c.Distance))
            {
                string id = candidate.Node.Definition.Id;
                if (!markers.TryGetValue(id,out Button marker))
                {
                    marker = new Button(() => controller.ConfirmTarget(id)) { name = "candidate-"+id };
                    marker.AddToClassList("candidate-marker"); marker.AddToClassList("interactive");
                    marker.RegisterCallback<PointerEnterEvent>(_ => controller.SetAttention(id));
                    root.Q("connection-markers").Add(marker); markers.Add(id,marker);
                }
                Vector3 projected = sceneCamera.WorldToViewportPoint(candidate.Node.Definition.Position + Vector3.up*1.4f);
                Vector2 point = new Vector2(viewport.x+projected.x*viewport.width,viewport.y+(1-projected.y)*viewport.height);
                bool outside = projected.z <= 0 || projected.x < 0 || projected.x > 1 || projected.y < 0 || projected.y > 1;
                Vector2 direction = point - viewport.center;
                if (projected.z <= 0) direction = -direction;
                if (direction.sqrMagnitude < 0.001f) direction = Vector2.right;
                if (outside)
                {
                    float scale = Mathf.Min(safe.width*0.5f/Mathf.Max(0.001f,Mathf.Abs(direction.x)),
                        safe.height*0.5f/Mathf.Max(0.001f,Mathf.Abs(direction.y)));
                    point = safe.center + direction*scale;
                }
                else
                {
                    // Leave the actual Node visible below (or above) its label.
                    point.y += point.y-60>=safe.yMin ? -60 : 60;
                    point.x=Mathf.Clamp(point.x,safe.xMin,safe.xMax);
                    point.y=Mathf.Clamp(point.y,safe.yMin,safe.yMax);
                }
                string arrow = Mathf.Abs(direction.x) > Mathf.Abs(direction.y) ? direction.x > 0 ? ">" : "<" : direction.y > 0 ? "v" : "^";
                bool occluded = controller.IsOccluded(id);
                string visibility = (outside ? arrow+" OFFSCREEN " : "") + (occluded ? "OCCLUDED" : "");
                string kind = candidate.Node.Definition.Kind.ToString().ToUpperInvariant() +
                    (candidate.Node.Definition.SinkColor.HasValue ? " "+candidate.Node.Definition.SinkColor.Value.ToString().ToUpperInvariant() : "");
                string status = candidate.Failure != ConnectionFailure.None ? "BLOCKED / INSPECT" : state?.DestinationId == id ?
                    state.Geometry.IsValid ? "PREVIEW READY" : "ROUTE INVALID / INSPECT" : "SLOTS OPEN / ROUTE ?";
                marker.text = $"{id} / {kind} / {candidate.Distance:0} m\n{visibility}\n{status}";
                marker.EnableInClassList("chosen",id == controller.AttentionId);
                marker.EnableInClassList("blocked",candidate.Failure != ConnectionFailure.None);
                marker.EnableInClassList("occluded",occluded);
                var bounds = new Rect(point.x-100,point.y-34,200,68);
                marker.style.left = bounds.x; marker.style.top = bounds.y;
                bool overlaps = occupied.Any(rect => rect.Overlaps(bounds));
                marker.style.display = overlaps ? DisplayStyle.None : DisplayStyle.Flex;
                if (!overlaps) occupied.Add(bounds);
            }
            if (controller.AttentionId != null && markers.TryGetValue(controller.AttentionId,out Button focused)) focused.BringToFront();
        }
        private void RenderCandidateList(IReadOnlyList<ConnectionCandidate> candidates,LinePreviewState? state)
        {
            if (root == null || session == null || controller == null) return;
            var list = root.Q<ScrollView>("connection-candidates");
            // Keep rows in roster order so hovering, turning, and filtering cannot move click targets.
            foreach (var node in session.Nodes)
            {
                string id = node.Id;
                if (candidateOptions.ContainsKey(id)) continue;
                var option = new Button(() => controller.ConfirmTarget(id))
                    { name = "candidate-option-"+id };
                option.AddToClassList("candidate-option");
                option.RegisterCallback<PointerEnterEvent>(_ => controller.FocusTarget(id));
                option.style.display = DisplayStyle.None;
                list.Add(option); candidateOptions.Add(id,option);
            }
            root.Q<Label>("candidate-list-count").text = $"{candidates.Count} NODES / {session.Filter.ToString().ToUpperInvariant()} · CLICK TO CONNECT";
            foreach (var candidate in candidates)
            {
                var node = candidate.Node;
                string id = node.Definition.Id;
                var option = candidateOptions[id];
                string status = candidate.Failure != ConnectionFailure.None ? "BLOCKED / INSPECT" :
                    state?.DestinationId == id ? state.Geometry.IsValid ? "PREVIEW READY" : "ROUTE INVALID" : "SLOTS OPEN / ROUTE ?";
                option.text = $"{id} · {node.Definition.Kind.ToString().ToUpperInvariant()} · {candidate.Distance:0} m\nIN {node.IncomingUsed}/{node.Definition.MaxIncoming} · {status}";
                option.tooltip = ConnectionReadout.Candidate(candidate,state);
                option.EnableInClassList("chosen",id == controller.AttentionId);
                option.EnableInClassList("blocked",candidate.Failure != ConnectionFailure.None);
                option.style.display = DisplayStyle.Flex;
            }
        }
    }
}
