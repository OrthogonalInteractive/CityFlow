#nullable enable
using System.Linq;
using CityFlow.Application.UseCases;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Infrastructure.Configuration;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
namespace CityFlow.Tests.EditMode
{
    public sealed class PlayPacingTests
    {
        [Test] public void BasicLineCapacityIsThreeAndSourcesGenerateAtOneThirdThePreviousRate()
        {
            var defaults=ScriptableObject.CreateInstance<GameplaySettings>();
            try { Assert.That(defaults.MaxInFlight,Is.EqualTo(3)); }
            finally { Object.DestroyImmediate(defaults); }
            var settings=AssetDatabase.LoadAssetAtPath<GameplaySettings>("Assets/CityFlow/Settings/Gameplay/ValidationGameplay.asset");
            Assert.That(settings.MaxInFlight,Is.EqualTo(3));
            var config=AssetDatabase.LoadAssetAtPath<StageConfiguration>("Assets/CityFlow/Settings/Gameplay/WiringStage.asset");
            var stage=config.Load(settings.Clearance);
            Assert.That(stage.Nodes.Single(n=>n.Id=="S1").GenerationInterval,Is.EqualTo(3));
            var waves=config.LoadWaves(stage,settings.Clearance);
            Assert.That(waves.SelectMany(w=>w.Additions).Single(n=>n.Id=="S2").GenerationInterval,Is.EqualTo(3.9).Within(0.0001));
            Assert.That(waves.SelectMany(w=>w.Additions).Single(n=>n.Id=="S3").GenerationInterval,Is.EqualTo(3));
            var network=config.LoadNetwork(stage,settings.LoadNetworkSettings());
            var sim=new FlowSimulation(network,new SystemRandomSource(1),waves);
            sim.Tick(17.95); Assert.That(network.Snapshot().GeneratedCount,Is.Zero);
            sim.Tick(0.05); Assert.That(network.Snapshot().GeneratedCount,Is.EqualTo(1));
            sim.Tick(3); Assert.That(network.Snapshot().GeneratedCount,Is.EqualTo(2));
        }
    }
}
