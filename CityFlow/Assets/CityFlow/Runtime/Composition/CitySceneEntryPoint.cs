#nullable enable

using System;
using CityFlow.Domain.Spatial;
using CityFlow.Application.UseCases;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Presentation.Rendering;
using CityFlow.Presentation.UI;
using CityFlow.Presentation.Overview;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer.Unity;

namespace CityFlow.Composition
{
    public sealed class CitySceneEntryPoint : IStartable, IDisposable
    {
        private readonly StageDefinition stage;
        private GameObject? city;
        private readonly FlowNetwork network;
        private readonly FlowSimulation simulation;
        private readonly VisualTreeAsset hudLayout;
        private readonly PanelSettings panelSettings;
        public CitySceneEntryPoint(StageDefinition stage, FlowNetwork network, FlowSimulation simulation,
            VisualTreeAsset hudLayout, PanelSettings panelSettings)
        {
            this.stage = stage; this.network = network; this.simulation = simulation;
            this.hudLayout = hudLayout; this.panelSettings = panelSettings;
        }
        public void Start()
        {
            city = new GameObject("Validation City");
            var view = city.AddComponent<ValidationCityView>();
            view.Initialize(stage, network);
            city.AddComponent<SimulationDriver>().Initialize(simulation);
            var hud = new GameObject("Validation HUD");
            hud.SetActive(false);
            hud.transform.SetParent(city.transform);
            var document = hud.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            document.visualTreeAsset = hudLayout;
            Camera camera = Camera.main;
            if (camera == null) throw new InvalidOperationException("The HUD requires an overview camera.");
            var overview = city.AddComponent<OverviewController>();
            overview.Initialize(stage, network, camera);
            hud.AddComponent<OverviewDetailsView>().Initialize(overview, network, view);
            hud.AddComponent<ValidationHud>().Initialize(stage, network, simulation, camera);
            hud.SetActive(true);
        }
        public void Dispose() { if (city != null) UnityEngine.Object.Destroy(city); }
    }
}
