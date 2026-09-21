#nullable enable
using System;
using System.Linq;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Domain.Spatial;
using CityFlow.Infrastructure.Configuration;
using NUnit.Framework;
using UnityEngine;
namespace CityFlow.Tests.EditMode
{
    public sealed class NodeBufferCapacityTests
    {
        private sealed class First : IRandomSource { public int NextIndex(int count) => 0; }
        [TestCase(false)] [TestCase(true)]
        public void DefaultAndAuthoredSettingsGiveSourceTenAndRelayFive(bool authored)
        {
            GameplaySettings settings = authored
                ? UnityEditor.AssetDatabase.LoadAssetAtPath<GameplaySettings>("Assets/CityFlow/Settings/Gameplay/ValidationGameplay.asset")
                : ScriptableObject.CreateInstance<GameplaySettings>();
            try
            {
                var network = new FlowNetwork(new StageDefinition(0, new Rect(-30,-30,60,60), Array.Empty<Bounds>(), new[] {
                    new NodeDefinition("S",NodeKind.Source,Vector3.zero),
                    new NodeDefinition("R",NodeKind.Relay,new Vector3(10,0,0)),
                    new NodeDefinition("RED",NodeKind.Sink,new Vector3(20,0,0),sinkColor:FlowColor.Red) }), settings.LoadNetworkSettings());
                Assert.That(network.TryConnect("S","R",new[] { Vector3.zero,new Vector3(10,0,0) }).Succeeded,Is.True);
                for(int i=0;i<6;i++) network.GenerateFlow("S",FlowColor.Red);
                network.RouteWaitingFlows(new First()); network.AdvanceInFlight(10);
                Assert.That(network.Snapshot().Nodes.Single(n=>n.Definition.Id=="R").Buffer.Count,Is.EqualTo(5));
                Assert.That(network.Snapshot().Lines.Single().InFlight.Count,Is.EqualTo(1));
                for(int i=0;i<9;i++) network.GenerateFlow("S",FlowColor.Red);
                Assert.That(network.Snapshot().Nodes.Single(n=>n.Definition.Id=="S").IsInputStopped,Is.False);
                network.GenerateFlow("S",FlowColor.Red);
                Assert.That(network.Snapshot().Nodes.Single(n=>n.Definition.Id=="S").IsInputStopped,Is.True);
                Assert.That(network.IsGameOver,Is.False);
                Assert.That(network.Snapshot().GeneratedCount,Is.EqualTo(16));
            }
            finally { if(!authored) UnityEngine.Object.DestroyImmediate(settings); }
        }
    }
}
