#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using CityFlow.Application.UseCases;
using CityFlow.Application.Connections;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Presentation.Connections;
using CityFlow.Presentation.Overview;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace CityFlow.Presentation.UI
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class GameSessionView : MonoBehaviour
    {
        private FlowSimulation? simulation;
        private FlowNetwork? network;
        private ConnectionSession? connection;
        private OverviewController? overview;
        private NodeConnectionController? connectionCamera;
        private Camera? sceneCamera;
        private UIDocument? document;
        private VisualElement? root;
        private Button? retry;
        private bool retrying;
        private readonly Dictionary<string, Button> markers = new();
        private readonly Dictionary<string, OverlayLeader> leaders = new();

        public void Initialize(FlowSimulation clock, FlowNetwork state, ConnectionSession wiring, OverviewController input,
            NodeConnectionController cameraController, Camera camera)
        {
            simulation = clock;
            network = state;
            connection = wiring;
            overview = input;
            connectionCamera = cameraController;
            sceneCamera = camera;
            document = GetComponent<UIDocument>();
            if (isActiveAndEnabled) Bind();
        }
        private void OnEnable() => Bind();
        private void Bind()
        {
            Unbind();
            if (document == null) return;
            root = document.rootVisualElement;
            retry = root.Q<Button>("retry-session");
            retry.text = "Retry";
            retry.clicked += Retry;
        }
        private void Retry()
        {
            if (retrying || simulation?.Result == null) return;
            retrying = true;
            retry?.SetEnabled(false);
            // The scene owns cancellation; expected destruction is handled inside the task.
            RetryAsync().Forget();
        }
        private async UniTask RetryAsync()
        {
            try
            {
                await SceneManager.LoadSceneAsync(SceneManager.GetActiveScene().buildIndex)
                    .ToUniTask(cancellationToken: this.GetCancellationTokenOnDestroy());
            }
            catch (OperationCanceledException) { }
            catch (Exception error)
            {
                retrying = false;
                if (retry != null) retry.SetEnabled(true);
                Debug.LogException(error);
            }
        }
        private void LateUpdate()
        {
            if (document == null || simulation == null || network == null || overview == null || connectionCamera == null || sceneCamera == null) return;
            if (root != document.rootVisualElement) Bind();
            if (root == null) return;
            SessionResult? result = simulation.Result;
            root.Q("result-overlay").style.display = result != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (result != null)
            {
                if (connection?.IsActive == true) connection.Cancel();
                root.Q<Label>("result-detail").text = $"WAVE {result.Wave}\nSURVIVED {HudClock.Format(result.SurvivalSeconds)}\nDELIVERED {result.Delivered}\nCAUSE / SOURCE {result.SourceId}";
            }
            var state = network.Snapshot();
            root.Q<Label>("context-hint").text = result == null ? Hint(state) : "Retry to build a new network.";
            bool recent = simulation.Wave > 1 && simulation.ElapsedSeconds - simulation.LastWaveSeconds < 12 && result == null;
            bool show = recent && !connectionCamera.IsEditing && !connectionCamera.IsNode360;
            var notice = root.Q<Label>("wave-notice");
            notice.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            notice.text = $"WAVE {simulation.Wave} / NEW NODES\n" + string.Join(" · ", simulation.LatestAdditions.Select(n => n.Id));
            foreach (var marker in markers.Values) marker.style.display = DisplayStyle.None;
            foreach (var leader in leaders.Values) leader.Hide();
            if (!show || root.layout.width <= 0 || root.layout.height <= 0) return;

            var obstacles = OverlayLayout.Obstacles(root, sceneCamera, state, notice, includeMarkers: false);
            float bottom = OverlayLayout.Value(root.Q("validation-hud"), "--notice-bottom", 88);
            Rect noticeBounds = OverlayLayout.Place(notice, root,
                new Vector2((root.layout.width - notice.resolvedStyle.width) * 0.5f, root.layout.height - bottom - notice.resolvedStyle.height), obstacles);
            obstacles.Add(noticeBounds);
            foreach (var node in simulation.LatestAdditions)
            {
                string id = node.Id;
                if (!markers.TryGetValue(id, out Button marker))
                {
                    marker = new Button(() =>
                    {
                        overview.Select(OverviewTarget.Node(id));
                        overview.FocusSelection();
                    }) { name = "arrival-" + id };
                    marker.AddToClassList("arrival-marker");
                    marker.AddToClassList("interactive");
                    root.Q("arrival-markers").Add(marker);
                    markers.Add(id, marker);
                    leaders.Add(id, new OverlayLeader(root.Q("arrival-markers"), "arrival-leader-" + id));
                }
                Vector3 world = node.Position + Vector3.up * 1.4f;
                Vector3 screen = sceneCamera.WorldToScreenPoint(world);
                bool outside = screen.z <= 0 || !sceneCamera.pixelRect.Contains(screen);
                marker.text = $"NEW {id} / {node.Kind.ToString().ToUpperInvariant()}\n" + (outside ? "OFFSCREEN / CLICK TO FOCUS" : "CLICK TO FOCUS");
                marker.style.display = DisplayStyle.Flex;
                Vector2 anchor = OverlayLayout.Anchor(root, sceneCamera, world);
                Rect placed = OverlayLayout.Place(marker, root, anchor + Vector2.one * 24, obstacles);
                obstacles.Add(placed);
                leaders[id].Show(root, anchor, placed);
            }
        }
        private string Hint(NetworkSnapshot state)
        {
            if (simulation == null || network == null || connectionCamera == null) return "";
            string pauseAction = simulation.IsPaused ? "Esc: resume" : "Esc: pause";
            string recovery = simulation.IsPaused ? "Paused · Connect matching Sinks · Esc to resume" : "Esc to pause and connect matching Sinks";
            var source = state.Nodes.Where(n => n.Definition.Kind == NodeKind.Source && n.BufferCapacity.HasValue)
                .OrderByDescending(n => n.IsInputStopped).ThenByDescending(n => n.OverloadSeconds)
                .ThenByDescending(n => (double)n.Buffer.Count / n.BufferCapacity.GetValueOrDefault(1)).FirstOrDefault();
            if (source?.IsInputStopped == true)
                return $"{source.Definition.Id}: {Math.Max(0, network.Settings.OverloadGrace - source.OverloadSeconds):0.0}s TO GAME OVER · {recovery}";
            if (source != null && source.Buffer.Count >= source.BufferCapacity * 0.8)
                return $"{source.Definition.Id} Buffer nearly full · {recovery}";
            if (connectionCamera.IsEditing) return $"Shift + click: add point · Drag: move · Enter: apply · Backspace: cancel · {pauseAction}";
            if (connectionCamera.IsNode360) return $"Hover: preview · Click: connect · E: edit route · Right drag: look · Backspace: cancel · {pauseAction}";
            var preparing = state.Nodes.FirstOrDefault(n => simulation.SourceStartRemaining(n.Definition.Id) > 0);
            if (preparing != null) return $"{preparing.Definition.Id} starts in {Math.Ceiling(simulation.SourceStartRemaining(preparing.Definition.Id)):0}s · Click to connect";
            if (state.Lines.Count == 0) return $"Click S1, then a matching Sink to connect · {pauseAction}";
            return $"Hover: inspect · Click Node: connect · Shift + click Line: edit · F: focus hovered item · {pauseAction}";
        }
        private void Unbind()
        {
            if (retry != null) retry.clicked -= Retry;
            retry = null;
            foreach (var marker in markers.Values) marker.RemoveFromHierarchy();
            foreach (var leader in leaders.Values) leader.Dispose();
            markers.Clear();
            leaders.Clear();
            root = null;
        }
        private void OnDisable() => Unbind();
    }
}
