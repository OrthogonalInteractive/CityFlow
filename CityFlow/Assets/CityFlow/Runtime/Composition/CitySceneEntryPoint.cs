#nullable enable

using System;
using CityFlow.Domain.Spatial;
using CityFlow.Application.UseCases;
using CityFlow.Application.Routing;
using CityFlow.Application.Connections;
using CityFlow.Presentation.Connections;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Presentation.Rendering;
using CityFlow.Presentation.UI;
using CityFlow.Presentation.Overview;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using VContainer.Unity;

namespace CityFlow.Composition
{
    public sealed class CitySceneEntryPoint : IStartable, IDisposable
    {
        private readonly LinePreviewService preview;
        private readonly ConnectionSession connection;
        private readonly StageDefinition stage;
        private GameObject? city;
        private readonly FlowNetwork network;
        private readonly FlowSimulation simulation;
        private readonly VisualTreeAsset hudLayout;
        private readonly PanelSettings panelSettings;
        private readonly Material obstacleSurface;
        private readonly VolumeProfile obstacleGlow;
        public CitySceneEntryPoint(StageDefinition stage, FlowNetwork network, FlowSimulation simulation,
            VisualTreeAsset hudLayout, PanelSettings panelSettings, LinePreviewService preview, ConnectionSession connection,
            Material obstacleSurface, VolumeProfile obstacleGlow)
        {
            this.obstacleSurface = obstacleSurface;
            this.obstacleGlow = obstacleGlow;
            this.connection = connection; this.preview = preview; this.stage = stage; this.network = network; this.simulation = simulation;
            this.hudLayout = hudLayout; this.panelSettings = panelSettings;
        }
        public void Start()
        {
            city = new GameObject("Validation City");
            var view = city.AddComponent<ValidationCityView>();
            view.Initialize(stage, network, obstacleSurface, obstacleGlow);
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
            var connectionController = city.AddComponent<NodeConnectionController>();
            connectionController.Initialize(connection, overview, stage, camera, view, document);
            hud.AddComponent<OverviewDetailsView>().Initialize(overview, network, view, simulation, connectionController, camera);
            hud.AddComponent<ValidationHud>().Initialize(stage, network, simulation, camera, overview, view.Focus);
            hud.AddComponent<GroundPreviewView>().Initialize(preview);
            hud.AddComponent<NodeConnectionView>().Initialize(connection, preview, overview, connectionController, camera, network);
            hud.AddComponent<RouteEditView>().Initialize(preview, connection, connectionController, overview, camera);
            hud.AddComponent<LineActionsView>().Initialize(network, connection, overview, connectionController);
            hud.AddComponent<PauseView>().Initialize(simulation);
            hud.AddComponent<GameSessionView>().Initialize(simulation, network, connection, overview, connectionController, camera);
            hud.AddComponent<SourceStatusView>().Initialize(network, simulation, connection, connectionController, view.Focus);
            hud.SetActive(true);
        }
        public void Dispose() { if (city != null) UnityEngine.Object.Destroy(city); }
    }
}
