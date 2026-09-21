#nullable enable

using System;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Application.UseCases;
using CityFlow.Presentation.Rendering;
using R3;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityFlow.Presentation.Overview
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class OverviewDetailsView : MonoBehaviour
    {
        private OverviewController? controller;
        private FlowNetwork? network;
        private FlowSimulation? simulation;
        private ValidationCityView? city;
        private UIDocument? document;
        private IDisposable? selectionSubscription;
        public bool HasActiveSubscription => selectionSubscription != null;
        public void Initialize(OverviewController input, FlowNetwork flowNetwork, ValidationCityView view, FlowSimulation? clock = null)
        { simulation=clock; controller = input; network = flowNetwork; city = view; document = GetComponent<UIDocument>(); if (isActiveAndEnabled) Subscribe(); }
        private void OnEnable() => Subscribe();
        private void Subscribe()
        {
            selectionSubscription?.Dispose(); selectionSubscription = null;
            if (controller == null || city == null) return;
            selectionSubscription = controller.SelectionChanged.Subscribe(target=> { if (city != null) city.SetSelection(target); });
            city.SetSelection(controller.Selected);
        }
        private void OnDisable() { selectionSubscription?.Dispose(); selectionSubscription = null; }
        private void LateUpdate()
        {
            if (document == null || controller == null || network == null) return;
            Label label = document.rootVisualElement.Q<Label>("overview-detail");
            if (label == null) return;
            OverviewTarget target = controller.Hovered.IsEmpty ? controller.Selected : controller.Hovered;
            label.text = OverviewReadout.Describe(target, network.Snapshot(), network.Settings, simulation?.GenerationIntervalScale ?? 1);
            if(target.NodeId != null && simulation?.SourceStartRemaining(target.NodeId) > 0)
                label.text += $"\nPREPARING · {simulation.SourceStartRemaining(target.NodeId):0.0}s";
        }
    }
}
