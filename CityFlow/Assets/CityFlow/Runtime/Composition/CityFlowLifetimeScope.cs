#nullable enable

using System;
using CityFlow.Domain.Spatial;
using CityFlow.Application.UseCases;
using CityFlow.Application.Routing;
using CityFlow.Application.Connections;
using CityFlow.Infrastructure.Routing;
using CityFlow.Infrastructure.Configuration;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;
using VContainer.Unity;

namespace CityFlow.Composition
{
    [DisallowMultipleComponent]
    public sealed class CityFlowLifetimeScope : LifetimeScope
    {
        [SerializeField] private GameplaySettings? gameplaySettings;
        [SerializeField] private StageConfiguration? stageConfiguration;

        [SerializeField] private VisualTreeAsset? hudLayout;
        [SerializeField] private PanelSettings? hudPanelSettings;

        public void SetHudConfiguration(VisualTreeAsset layout, PanelSettings panelSettings)
        { hudLayout = layout; hudPanelSettings = panelSettings; }

        public void SetConfiguration(GameplaySettings settings, StageConfiguration stage)
        {
            gameplaySettings = settings; stageConfiguration = stage;
        }
        protected override void Configure(IContainerBuilder builder)
        {
            if (gameplaySettings == null || stageConfiguration == null)
                throw new InvalidOperationException("Bootstrap requires gameplay and stage configuration assets.");
            if (hudLayout == null || hudPanelSettings == null)
                throw new InvalidOperationException("Bootstrap requires UI Toolkit layout and panel settings assets.");
            builder.RegisterInstance(hudLayout);
            builder.RegisterInstance(hudPanelSettings);
            gameplaySettings.Validate();
            StageDefinition stage = stageConfiguration.Load(gameplaySettings.Clearance);
            builder.RegisterInstance(stage);
            var network = stageConfiguration.LoadNetwork(stage, gameplaySettings.LoadNetworkSettings());
            builder.RegisterInstance(network);
            builder.RegisterInstance<IGroundRoutePlanner>(new GroundRoutePlanner(stage, gameplaySettings.Clearance));
            builder.Register<LinePreviewService>(Lifetime.Singleton);
            // Provisional distance bands scale with the playable area's diagonal [m].
            builder.Register<ConnectionSession>(Lifetime.Singleton)
                .WithParameter("nearLimit",stage.WalkableArea.size.magnitude * 0.25f)
                .WithParameter("midLimit",stage.WalkableArea.size.magnitude * 0.5f);
            builder.RegisterInstance(new FlowSimulation(network, new SystemRandomSource(gameplaySettings.RandomSeed)));
            builder.RegisterEntryPoint<CitySceneEntryPoint>();
        }
    }
}
