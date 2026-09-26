#nullable enable

using System;
using System.Linq;
using CityFlow.Application.UseCases;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Domain.Spatial;
using CityFlow.Infrastructure.Configuration;
using CityFlow.Infrastructure.Routing;
using NUnit.Framework;
using UnityEditor;

namespace CityFlow.Tests.EditMode
{
    public sealed class TokyoStationStageTests
    {
        private static StageConfiguration Configuration => AssetDatabase.LoadAssetAtPath<StageConfiguration>(
            "Assets/CityFlow/Settings/Gameplay/TokyoStationStage.asset");
        private static GameplaySettings Settings => AssetDatabase.LoadAssetAtPath<GameplaySettings>(
            "Assets/CityFlow/Settings/Gameplay/TokyoStationGameplay.asset");

        // One possible network, extended without removing the player's earlier Lines.
        private static readonly (string From, string To)[] Connections =
        {
            ("S1", "R1"), ("R1", "RED"), ("R1", "BLUE"),
            ("S2", "R2"), ("R1", "R2"), ("R2", "R1"), ("R2", "GREEN"),
            ("S3", "R3"), ("R2", "R3"), ("R3", "R2"), ("R3", "YELLOW"), ("R2", "PURPLE")
        };

        [Test]
        public void StationStartsWithTwoColorsAndUnlocksFiveColorsByItsFinalThirdWave()
        {
            var config = Configuration;
            var stage = config.Load(Settings.Clearance);
            var waves = config.LoadWaves(stage, Settings.Clearance);
            Assert.That(stage.Nodes.Count, Is.EqualTo(4), "The opening teaches two-color wiring instead of displaying every Node.");
            Assert.That(stage.Nodes.OfType<SinkNodeDefinition>().Select(n => n.Color),
                Is.EquivalentTo(new[] { FlowColor.Red, FlowColor.Blue }));
            Assert.That(waves.Count, Is.EqualTo(2), "Wave 3 is the final difficulty tier.");
            Assert.That(waves.Select(w => w.StartSeconds), Is.EqualTo(new[] { 60d, 120d }));
            Assert.That(config.Lines, Is.Empty);
            Assert.That(stage.Buildings, Is.Not.Empty);
            Assert.That(stage.WalkableArea.width, Is.LessThanOrEqualTo(650));
            Assert.That(stage.WalkableArea.height, Is.LessThanOrEqualTo(250));
            var nodes = stage.Nodes.ToList();
            double previousRate = nodes.OfType<SourceNodeDefinition>().Sum(n => 1 / n.GenerationInterval);
            for (int i = 0; i < waves.Count; i++)
            {
                nodes.AddRange(waves[i].Additions);
                Assert.That(nodes.OfType<SinkNodeDefinition>().Select(n => n.Color).Distinct().Count(), Is.EqualTo(i == 0 ? 3 : 5));
                Assert.That(nodes.OfType<SourceNodeDefinition>().Count(), Is.EqualTo(i + 2));
                Assert.That(nodes.OfType<RelayNodeDefinition>().Count(), Is.EqualTo(i + 2));
                Assert.That(waves[i].Additions.OfType<SourceNodeDefinition>().All(n => n.GenerationDelay >= 15), Is.True);
                double rate = nodes.OfType<SourceNodeDefinition>().Sum(n => 1 / (n.GenerationInterval * waves[i].IntervalScale));
                Assert.That(rate, Is.GreaterThan(previousRate));
                previousRate = rate;
            }
            Assert.That(stage.AllowsHeight, Is.True);
            Assert.That(nodes.OfType<SourceNodeDefinition>().Count(n => n.Position.y > stage.GroundHeight + 100), Is.EqualTo(2));
            Assert.That(nodes.OfType<SinkNodeDefinition>().Select(n => n.Color), Is.EquivalentTo(Enum.GetValues(typeof(FlowColor))));
            Assert.That(nodes.Where(n => n.MaxOutgoing > 0).All(n => n.MaxOutgoing <= 4), Is.True,
                "Limited OUT slots require Relay choices as more colors arrive.");
            Assert.That(EditorBuildSettings.scenes.Any(s => s.enabled && s.path.EndsWith("/TokyoStationWiringLab.unity")), Is.True);
        }

        [Test]
        public void EveryWaveCanDeliverEverySourceColorWithLegalSlotsAndBuildingAvoidance()
        {
            var config = Configuration;
            var stage = config.Load(Settings.Clearance);
            var waves = config.LoadWaves(stage, Settings.Clearance);
            Assert.That(waves.Count, Is.EqualTo(2));
            var network = config.LoadNetwork(stage, Settings.LoadNetworkSettings());
            var planner = new LineRoutePlanner(stage, Settings.Clearance);
            for (int wave = 0; wave <= waves.Count; wave++)
            {
                if (wave > 0) Assert.That(network.TryAddNodes(waves[wave - 1].Additions), Is.True);
                ConnectAvailable(network, planner);
                foreach (var source in network.NodeDefinitions.OfType<SourceNodeDefinition>())
                    foreach (var sink in network.NodeDefinitions.OfType<SinkNodeDefinition>())
                    {
                        long before = network.Snapshot().DeliveredCount;
                        network.GenerateFlow(source.Id, sink.Color);
                        for (int second = 0; second < 60 && network.Snapshot().DeliveredCount == before; second++)
                        { network.RouteWaitingFlows(); network.AdvanceInFlight(1); }
                        Assert.That(network.Snapshot().DeliveredCount, Is.EqualTo(before + 1),
                            $"Wave {wave + 1}: {source.Id} -> {sink.Id}");
                    }
            }
        }

        [Test]
        public void RoofNodesUseBothSidesOfTheStationAndRequireRelaysWithEnoughRise()
        {
            var config = Configuration;
            var stage = config.Load(Settings.Clearance);
            var waves = config.LoadWaves(stage, Settings.Clearance);
            var nodes = stage.Nodes.Concat(waves.SelectMany(w => w.Additions)).ToDictionary(n => n.Id);
            Assert.That(stage.AllowsHeight, Is.True, "Tokyo Station gameplay must use building heights.");
            foreach (string id in new[] { "S2", "S3", "GREEN", "YELLOW", "R3" })
            {
                var p = nodes[id].Position;
                Assert.That(stage.Buildings.Any(b => p.x >= b.min.x && p.x <= b.max.x &&
                    p.z >= b.min.z && p.z <= b.max.z && p.y > b.max.y + Settings.Clearance && p.y <= b.max.y + 1.5f),
                    Is.True, id + " must sit just above an imported building roof.");
            }
            Assert.That(nodes["S2"].Position.x, Is.LessThan(-250));
            Assert.That(nodes["S3"].Position.x, Is.GreaterThan(100));
            Assert.That(nodes["YELLOW"].Position.x, Is.GreaterThan(100));
            var planner = new LineRoutePlanner(stage, Settings.Clearance);
            bool Can(string from, string to) => planner.Generate(nodes[from], nodes[to]).IsValid;
            Assert.That(Can("S1", "GREEN"), Is.False, "Sources cannot lift to a rooftop Sink.");
            Assert.That(Can("R1", "GREEN"), Is.False, "The opening Relay is for low routes.");
            Assert.That(Can("S2", "R1"), Is.False);
            Assert.That(Can("S2", "R2"), Is.True, "The west Relay must receive the high rooftop Source.");
            Assert.That(Can("R2", "GREEN"), Is.True);
            Assert.That(Can("R2", "YELLOW"), Is.False, "The east roof needs the higher Relay.");
            Assert.That(Can("S3", "R3"), Is.True);
            Assert.That(Can("R3", "YELLOW"), Is.True);
            var crossing = planner.Generate(nodes["R2"], nodes["R3"]).Route
                ?? throw new AssertionException("The station must be crossable between capable Relays.");
            Assert.That(crossing.Points.Max(p => p.y), Is.GreaterThan(41.8f));
            Assert.That(stage.ValidateConnectionRoute(nodes["R2"], nodes["R3"], crossing.Points,
                Settings.Clearance, out _), Is.EqualTo(RouteFailure.None));
        }

        [TestCase(1337)]
        [TestCase(42)]
        [TestCase(2026)]
        public void ExtendingTheNetworkAfterEachWaveSupportsFiveMinutesWithoutResettingFlows(int seed)
        {
            var config = Configuration;
            var stage = config.Load(Settings.Clearance);
            var network = config.LoadNetwork(stage, Settings.LoadNetworkSettings());
            var clock = new FlowSimulation(network, new SystemRandomSource(seed), config.LoadWaves(stage, Settings.Clearance));
            var planner = new LineRoutePlanner(stage, Settings.Clearance);
            for (int second = 0; second < 300; second++)
            {
                // Allow ten seconds of player response time after each new Wave.
                if (second == 10 || second == 70 || second == 130) ConnectAvailable(network, planner);
                clock.Tick(1);
                Assert.That(network.IsGameOver, Is.False, $"Seed {seed}, second {second + 1}, Source {network.GameOverSourceId}");
                var state = network.Snapshot();
                Assert.That(state.GeneratedCount, Is.EqualTo(state.DeliveredCount +
                    state.Nodes.Sum(n => n.Buffer.Count) + state.Lines.Sum(l => l.InFlight.Count)));
                var colors = network.NodeDefinitions.OfType<SinkNodeDefinition>().Select(n => n.Color).ToArray();
                Assert.That(state.Nodes.SelectMany(n => n.Buffer).Concat(state.Lines.SelectMany(l => l.InFlight.Select(f => f.Flow)))
                    .All(f => colors.Contains(f.Color)), Is.True, "A new color requires its Sink first.");
            }
            Assert.That(clock.Wave, Is.EqualTo(3));
            Assert.That(clock.NextWaveSeconds, Is.Null);
            Assert.That(clock.Result, Is.Null, "Wave 3 continues as survival play.");
            Assert.That(network.Snapshot().DeliveredCount, Is.GreaterThan(70));
            Assert.That(network.Snapshot().Lines.Count, Is.EqualTo(Connections.Length));
        }

        [Test]
        public void IgnoringNewWavesEventuallyOverloadsASourceWhileAnUnwiredStartLosesInWaveOne()
        {
            foreach (bool wireOpening in new[] { false, true })
            {
                var config = Configuration;
                var stage = config.Load(Settings.Clearance);
                var network = config.LoadNetwork(stage, Settings.LoadNetworkSettings());
                var clock = new FlowSimulation(network, new SystemRandomSource(1337), config.LoadWaves(stage, Settings.Clearance));
                if (wireOpening) ConnectAvailable(network, new LineRoutePlanner(stage, Settings.Clearance));
                clock.Tick(300);
                Assert.That(clock.Result, Is.Not.Null);
                if (wireOpening) Assert.That(clock.Wave, Is.GreaterThan(1));
                else Assert.That(clock.Wave, Is.EqualTo(1));
                Assert.That(network.NodeDefinitions.OfType<SourceNodeDefinition>().Any(n => n.Id == clock.Result?.SourceId), Is.True);
            }
        }

        private static void ConnectAvailable(FlowNetwork network, LineRoutePlanner planner)
        {
            var nodes = network.NodeDefinitions.ToDictionary(n => n.Id);
            foreach (var (from, to) in Connections)
            {
                if (!nodes.ContainsKey(from) || !nodes.ContainsKey(to) ||
                    network.Snapshot().Lines.Any(l => l.SourceId == from && l.DestinationId == to)) continue;
                var route = planner.Generate(nodes[from], nodes[to]);
                Assert.That(route.IsValid, Is.True, from + " -> " + to);
                var path = route.Route ?? throw new AssertionException("Expected a valid route.");
                Assert.That(network.TryConnect(from, to, path.Points).Succeeded, Is.True, from + " -> " + to);
            }
        }
    }
}
