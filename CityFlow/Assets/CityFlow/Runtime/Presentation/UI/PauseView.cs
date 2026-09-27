#nullable enable

using CityFlow.Application.UseCases;
using CityFlow.Application.Connections;
using CityFlow.Presentation.Rendering;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityFlow.Presentation.UI
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class PauseView : MonoBehaviour
    {
        private FlowSimulation? simulation;
        private ConnectionSession? connection;
        private ValidationCityView? city;
        private UIDocument? document;
        private VisualElement? root;
        private Button? button;
        private Button? nodeButton;
        private bool highlightRequested;

        public void Initialize(FlowSimulation value, ConnectionSession wiring, ValidationCityView view)
        {
            simulation = value;
            connection = wiring;
            city = view;
            document = GetComponent<UIDocument>();
            if (isActiveAndEnabled) Bind();
        }

        private void Toggle()
        {
            if (simulation != null) simulation.SetPaused(!simulation.IsPaused);
            Refresh();
        }

        private bool CanHighlight => simulation?.IsPaused == true && simulation.Result == null && connection?.IsActive != true;
        private void ToggleNodes()
        {
            if (!CanHighlight) return;
            highlightRequested = !highlightRequested;
            Refresh();
        }

        private void OnEnable() => Bind();
        private void Bind()
        {
            Unbind();
            if (document == null) return;
            root = document.rootVisualElement;
            if (root == null) return;
            button = root.Q<Button>("pause-toggle");
            button.clicked += Toggle;
            nodeButton = root.Q<Button>("node-highlight-toggle");
            nodeButton.clicked += ToggleNodes;
            Refresh();
        }

        private void Update()
        {
            if (document == null || simulation == null) return;
            if (root != document.rootVisualElement) Bind();
            Refresh();
        }

        private void Refresh()
        {
            if (simulation == null) return;
            if (!simulation.IsPaused || simulation.Result != null) highlightRequested = false;
            bool highlight = highlightRequested && CanHighlight;
            if (city != null) city.SetNodeHighlight(highlight);
            if (root == null || button == null) return;
            root.Q<Label>("pause-status").text = simulation.IsPaused ? "PAUSED / EDITING AVAILABLE" : "SIMULATION RUNNING";
            button.text = simulation.IsPaused ? "Resume" : "Pause";
            button.SetEnabled(simulation.Result == null);
            if (nodeButton != null)
            {
                nodeButton.SetEnabled(CanHighlight);
                nodeButton.EnableInClassList("chosen", highlight);
                nodeButton.text = highlight ? "Nodes: ON" : "Nodes";
            }
            if (simulation.Result != null)
            {
                root.Q<Label>("pause-status").text = "SESSION ENDED";
                button.SetEnabled(false);
            }
            root.Q(className: "session-controls").EnableInClassList("paused", simulation.IsPaused);
        }

        private void Unbind()
        {
            if (button != null) button.clicked -= Toggle;
            if (nodeButton != null) nodeButton.clicked -= ToggleNodes;
            button = null;
            nodeButton = null;
            root = null;
        }
        private void OnDisable()
        {
            highlightRequested = false;
            if (city != null) city.SetNodeHighlight(false);
            Unbind();
        }
    }
}
