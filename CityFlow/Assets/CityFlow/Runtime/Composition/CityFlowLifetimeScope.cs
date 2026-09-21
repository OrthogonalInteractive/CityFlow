#nullable enable

using System;
using CityFlow.Domain.Spatial;
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
            builder.RegisterInstance(stageConfiguration.LoadNetwork(stage, gameplaySettings.LoadNetworkSettings()));
            builder.RegisterEntryPoint<CitySceneEntryPoint>();
        }
    }
}
