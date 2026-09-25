#nullable enable

using System.Linq;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Domain.Spatial;
using CityFlow.Infrastructure.Configuration;
using CityFlow.Infrastructure.Routing;
using NUnit.Framework;
using UnityEditor;

namespace CityFlow.Tests.EditMode
{
    public sealed class RelayHeightStageTests
    {
        private static StageConfiguration Configuration => AssetDatabase.LoadAssetAtPath<StageConfiguration>("Assets/CityFlow/Settings/Gameplay/HeightStage.asset");

        [Test] public void HeightLabStartsEmptyAndItsWallsRequireDifferentRelayCapabilities()
        {
            var config = Configuration;
            var stage = config.Load(0.5f);
            Assert.That(stage.Nodes.Count, Is.EqualTo(6));
            Assert.That(config.Lines, Is.Empty);
            Assert.That(stage.MaximumAltitude, Is.EqualTo(30));
            var planner = new LineRoutePlanner(stage, 0.5f);
            bool Can(string from, string to) => planner.Generate(stage.Nodes.Single(n => n.Id == from), stage.Nodes.Single(n => n.Id == to)).IsValid;
            Assert.That(Can("S1", "RED"), Is.True);
            Assert.That(Can("S1", "BLUE"), Is.False, "Sources cannot lift over the low wall.");
            Assert.That(Can("S1", "R1"), Is.True);
            Assert.That(Can("R1", "R3"), Is.False, "A 6 m Relay cannot cross the 8 m wall.");
            Assert.That(Can("R2", "R3"), Is.True, "Both 10 m Relays can cross the low wall.");
            Assert.That(Can("R3", "BLUE"), Is.True);
        }

        [Test] public void WaveRelaysUnlockRoofSinksAndTheHigherWallRegardlessOfAdditionOrder()
        {
            var config = Configuration;
            var stage = config.Load(0.5f);
            var waves = config.LoadWaves(stage, 0.5f);
            var nodes = stage.Nodes.Concat(waves.SelectMany(w => w.Additions)).ToDictionary(n => n.Id);
            var planner = new LineRoutePlanner(stage, 0.5f);
            bool Can(string from, string to) => planner.Generate(nodes[from], nodes[to]).IsValid;
            Assert.That(Can("R3", "GREEN"), Is.False);
            Assert.That(Can("R4", "GREEN"), Is.True);
            Assert.That(Can("R3", "R5"), Is.False);
            Assert.That(Can("R4", "R5"), Is.True);
            Assert.That(Can("S2", "R3"), Is.False);
            Assert.That(Can("S2", "R4"), Is.True);
            Assert.That(Can("R5", "PURPLE"), Is.True);
            Assert.That(nodes.Values.OfType<RelayNodeDefinition>().OrderBy(n => n.Id).Select(n => n.MaximumRise), Is.EqualTo(new[] { 6f, 10f, 10f, 22f, 26f }));
        }

        [Test] public void EveryWaveHasASlotValidNetworkDeliveringEverySourceColor()
        {
            var config = Configuration;
            var stage = config.Load(0.5f);
            var waves = config.LoadWaves(stage, 0.5f);
            var network = config.LoadNetwork(stage, new NetworkSettings(10, 5, 3, 8, 0.5f));
            var planner = new LineRoutePlanner(stage, 0.5f);
            var edges = new[] { ("S1","RED"), ("S1","R2"), ("R2","R3"), ("R3","BLUE"),
                ("R3","R4"), ("R4","GREEN"), ("R4","R5"), ("R5","YELLOW"), ("R5","PURPLE"),
                ("S2","R4"), ("R4","R3"), ("R3","R2"), ("R2","RED") };
            for (int wave = -1; wave < waves.Count; wave++)
            {
                if (wave >= 0) Assert.That(network.TryAddNodes(waves[wave].Additions), Is.True);
                var nodes = network.NodeDefinitions.ToDictionary(n => n.Id);
                foreach (var (from, to) in edges)
                {
                    if (!nodes.ContainsKey(from) || !nodes.ContainsKey(to) || network.Snapshot().Lines.Any(l => l.SourceId == from && l.DestinationId == to)) continue;
                    var route = planner.Generate(nodes[from], nodes[to]);
                    Assert.That(route.IsValid, Is.True, from + " -> " + to);
                    Assert.That(network.TryConnect(from, to, route.Route!.Points).Succeeded, Is.True, from + " -> " + to);
                }
                foreach (var source in nodes.Values.OfType<SourceNodeDefinition>())
                    foreach (var sink in nodes.Values.OfType<SinkNodeDefinition>())
                    {
                        long before = network.Snapshot().DeliveredCount;
                        network.GenerateFlow(source.Id, sink.Color);
                        for (int i = 0; i < 150 && network.Snapshot().DeliveredCount == before; i++)
                        { network.RouteWaitingFlows(); network.AdvanceInFlight(1); }
                        Assert.That(network.Snapshot().DeliveredCount, Is.EqualTo(before + 1), $"Wave {wave + 2}: {source.Id} -> {sink.Id}");
                    }
            }
        }
    }
}
