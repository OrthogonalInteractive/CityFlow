#nullable enable

using System;
using CityFlow.Domain.Spatial;
using CityFlow.Application.UseCases;
using CityFlow.Infrastructure.Configuration;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace CityFlow.Composition
{
    [DisallowMultipleComponent]
    public sealed class CityFlowLifetimeScope : LifetimeScope
    {
        [SerializeField] private GameplaySettings? gameplaySettings;
        [SerializeField] private StageConfiguration? stageConfiguration;

        public void SetConfiguration(GameplaySettings settings, StageConfiguration stage)
        {
            gameplaySettings = settings; stageConfiguration = stage;
        }
        protected override void Configure(IContainerBuilder builder)
        {
            if (gameplaySettings == null || stageConfiguration == null)
                throw new InvalidOperationException("Bootstrap requires gameplay and stage configuration assets.");
            gameplaySettings.Validate();
            StageDefinition stage = stageConfiguration.Load(gameplaySettings.Clearance);
            builder.RegisterInstance(stage);
            var network = stageConfiguration.LoadNetwork(stage, gameplaySettings.LoadNetworkSettings());
            builder.RegisterInstance(network);
            builder.RegisterInstance(new FlowSimulation(network, new SystemRandomSource(gameplaySettings.RandomSeed)));
            builder.RegisterEntryPoint<CitySceneEntryPoint>();
        }
    }
}
