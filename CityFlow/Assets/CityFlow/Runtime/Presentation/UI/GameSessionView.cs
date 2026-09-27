#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using CityFlow.Application.UseCases;
using CityFlow.Application.Connections;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Presentation.Connections;
using CityFlow.Presentation.Rendering;
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
        private NodeConnectionController? connectionCamera;
        private UIDocument? document;
        private VisualElement? root;
        private Button? retry;
        private bool retrying;

        public void Initialize(FlowSimulation clock, FlowNetwork state, ConnectionSession wiring,
            NodeConnectionController cameraController)
        {
            simulation = clock;
            network = state;
            connection = wiring;
            connectionCamera = cameraController;
            document = GetComponent<UIDocument>();
            if (isActiveAndEnabled) Bind();
        }
        private void OnEnable() => Bind();
        private void Bind()
        {
            Unbind();
            if (document == null) return;
            root = document.rootVisualElement;
            if (root == null) return;
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
            if (document == null || simulation == null || network == null || connectionCamera == null) return;
            if (root != document.rootVisualElement) Bind();
            if (root == null) return;
            SessionResult? result = simulation.Result;
            RefreshWaveTransition();
            root.Q("result-overlay").style.display = result != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (result != null)
            {
                if (connection?.IsActive == true) connection.Cancel();
                root.Q<Label>("result-detail").text = $"WAVE {result.Wave}\nSURVIVED {HudClock.Format(result.SurvivalSeconds)}\nDELIVERED {result.Delivered}\nCAUSE / SOURCE {result.SourceId}";
            }
            var state = network.Snapshot();
            root.Q<Label>("context-hint").text = result == null ? Hint(state) : "Retry to build a new network.";
        }
        private void RefreshWaveTransition()
        {
            if (root == null || simulation == null || network == null) return;
            var transition = root.Q("wave-transition");
            var card = root.Q("wave-transition-card");
            // Provisional game seconds: pause and tree reconstruction retain the same Wave phase.
            const float enterSeconds = 0.6f, holdSeconds = 2.4f, fadeSeconds = 0.45f;
            float age = (float)(simulation.ElapsedSeconds - simulation.LastWaveSeconds);
            bool show = simulation.Result == null && age >= 0 && age < enterSeconds + holdSeconds + fadeSeconds;
            transition.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            card.EnableInClassList("layout-obstacle", show);
            if (!show) return;
            root.Q<Label>("wave-transition-title").text = $"WAVE {simulation.Wave}";
            var additions = simulation.Wave == 1 ? network.NodeDefinitions : simulation.LatestAdditions;
            var counts = new[] { NodeKind.Source, NodeKind.Relay, NodeKind.Sink }
                .Select(kind => (Kind: kind, Count: additions.Count(n => n.Kind == kind))).Where(item => item.Count > 0);
            var summary = root.Q<Label>("wave-transition-additions");
            summary.text = string.Join("  ·  ", counts.Select(item => $"{item.Kind.ToString().ToUpperInvariant()} +{item.Count}"));
            summary.style.display = additions.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            var details = new List<string>();
            foreach (var sinks in additions.Where(n => n.SinkColor.HasValue).GroupBy(n => n.SinkColor.GetValueOrDefault()).OrderBy(g => g.Key))
            {
                string tint = ColorUtility.ToHtmlStringRGB(ValidationCityView.ColorFor(sinks.Key));
                string count = sinks.Count() > 1 ? $" ×{sinks.Count()}" : "";
                details.Add($"<color=#{tint}>{sinks.Key.ToString().ToUpperInvariant()}{count}</color>");
            }
            if (simulation.LatestExpandedArea is Rect area) details.Add($"AREA {area.width:0} × {area.height:0} m");
            var detail = root.Q<Label>("wave-transition-details");
            detail.text = string.Join("  ·  ", details);
            detail.style.display = details.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            float remaining = 1 - Mathf.Clamp01(age / enterSeconds);
            float offset = -100 * remaining * remaining * remaining;
            // Translate the full-size track; the card's center and size belong to USS.
            transition.style.translate = new Translate(Length.Percent(offset), 0);
            transition.style.opacity = Mathf.Clamp01(age / 0.18f) *
                (1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01((age - enterSeconds - holdSeconds) / fadeSeconds)));
        }
        private string Hint(NetworkSnapshot state)
        {
            if (simulation == null || network == null || connectionCamera == null) return "";
            string pauseAction = simulation.IsPaused ? "Resume button: continue" : "Pause button: stop time";
            string recovery = simulation.IsPaused ? "Paused · Connect matching Sinks · Click Resume to resume" : "Click Pause and connect matching Sinks";
            var source = state.Nodes.Where(n => n.Definition.Kind == NodeKind.Source && n.BufferCapacity.HasValue)
                .OrderByDescending(n => n.IsBufferFull).ThenByDescending(n => n.OverloadSeconds)
                .ThenByDescending(n => (double)n.Buffer.Count / n.BufferCapacity.GetValueOrDefault(1)).FirstOrDefault();
            if (source?.IsBufferFull == true)
                return $"{source.Definition.Id}: {Math.Max(0, network.Settings.OverloadGrace - source.OverloadSeconds):0.0}s TO GAME OVER · {recovery}";
            if (source != null && source.Buffer.Count >= source.BufferCapacity * 0.8)
                return $"{source.Definition.Id} Buffer nearly full · {recovery}";
            if (connectionCamera.IsEditing) return $"Shift + click: add point · Drag: move · Apply Line button: apply · Esc: cancel · {pauseAction}";
            if (connectionCamera.IsNode360) return $"Hover: preview · Click: connect · Edit route button: edit · Right drag: look · Esc: cancel · {pauseAction}";
            var preparing = state.Nodes.FirstOrDefault(n => simulation.SourceStartRemaining(n.Definition.Id) > 0);
            if (preparing != null) return $"{preparing.Definition.Id} starts in {Math.Ceiling(simulation.SourceStartRemaining(preparing.Definition.Id)):0}s · Click to connect";
            if (state.Lines.Count == 0) return $"Click S1, then a matching Sink to connect · {pauseAction}";
            return $"Hover: inspect · Click Node: connect · Shift + click Line: edit · F: focus hovered item · Esc: clear selection · {pauseAction}";
        }
        private void Unbind()
        {
            if (retry != null) retry.clicked -= Retry;
            retry = null;
            root = null;
        }
        private void OnDisable() => Unbind();
    }
}
