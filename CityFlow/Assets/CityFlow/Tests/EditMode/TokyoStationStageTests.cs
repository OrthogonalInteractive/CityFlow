#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using CityFlow.Application.Connections;
using CityFlow.Application.Routing;
using CityFlow.Application.UseCases;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Domain.Spatial;
using CityFlow.Infrastructure.Configuration;
using CityFlow.Infrastructure.Routing;
using CityFlow.Tests.Fixtures;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CityFlow.Tests.EditMode
{
    /// <summary>v0.2 Δ1.3: the tiered Tokyo Station level (lower / middle / upper) over ten Waves.</summary>
    public sealed class TokyoStationStageTests
    {
        private const float Ground = 3.7f;
        private const float LowerCeiling = Ground + 12f;
        private const float MiddleFloor = 25f, MiddleCeiling = 50f;
        private const float UpperFloor = 180f, UpperCeiling = 215f;
        private const int FinalWave = 10;

        private static StageConfiguration Configuration => AssetDatabase.LoadAssetAtPath<StageConfiguration>(
            "Assets/CityFlow/Settings/Gameplay/TokyoStationStage.asset");
        private static GameplaySettings Settings => AssetDatabase.LoadAssetAtPath<GameplaySettings>(
            "Assets/CityFlow/Settings/Gameplay/TokyoStationGameplay.asset");

        private static string Tier(NodeDefinition node) => node.Position.y <= LowerCeiling ? "lower"
            : node.Position.y >= MiddleFloor && node.Position.y <= MiddleCeiling ? "middle"
            : node.Position.y >= UpperFloor && node.Position.y <= UpperCeiling ? "upper" : "outside";

        private static GreedyNetworkWiring Wiring(StageDefinition stage)
        {
            var planner = new LineRoutePlanner(stage, Settings.Clearance);
            return new GreedyNetworkWiring((from, to) => planner.Generate(from, to).Route);
        }

        [Test]
        public void WestPlazaOpensWithTwoColorsAndStaysLowerOnlyUntilWaveFour()
        {
            var config = Configuration;
            var stage = config.Load(Settings.Clearance);
            var waves = config.LoadWaves(stage, Settings.Clearance);
            Assert.That(stage.Nodes.Count, Is.EqualTo(5), "Two Sources, one hub Relay and two Sinks open the level.");
            Assert.That(stage.Nodes.OfType<SinkNodeDefinition>().Select(n => n.Color), Is.EquivalentTo(new[] { FlowColor.Red, FlowColor.Blue }));
            Assert.That(waves.Count, Is.EqualTo(FinalWave - 1), "Wave 10 is the final difficulty tier.");
            Assert.That(waves.Select(w => w.StartSeconds), Is.EqualTo(Enumerable.Range(1, FinalWave - 1).Select(i => 60d * i)));
            Assert.That(config.Lines, Is.Empty);
            Assert.That(stage.AllowsHeight, Is.True);
            Assert.That(stage.WalkableArea, Is.EqualTo(new Rect(-355, -90, 600, 195)), "The area is not expanded for tiers.");
            var nodes = stage.Nodes.ToList();
            double previousRate = nodes.OfType<SourceNodeDefinition>().Sum(n => 1 / n.GenerationInterval);
            for (int i = 0; i < waves.Count; i++)
            {
                nodes.AddRange(waves[i].Additions);
                int wave = i + 2;
                var colors = nodes.OfType<SinkNodeDefinition>().Select(n => n.Color).Distinct().ToArray();
                Assert.That(colors.Length, Is.EqualTo(wave <= 2 ? 3 : wave <= 6 ? 4 : 5), $"Wave {wave} colors");
                if (wave <= 3)
                {
                    Assert.That(waves[i].Additions.All(n => n.Position.x < -151 && n.Position.y == Ground), Is.True,
                        $"Wave {wave} stays on the west plaza ground.");
                }
                if (wave == 3) Assert.That(nodes.OfType<SourceNodeDefinition>().Count(), Is.EqualTo(6), "Six Sources span the west side by Wave 3.");
                if (wave == 4)
                {
                    Assert.That(waves[i].Additions.OfType<RelayNodeDefinition>().Count(r => r.Position.y == Ground && r.Position.y + r.MaximumRise >= MiddleCeiling - 0.01f),
                        Is.EqualTo(2), "One lower-middle bridge on each side of the station.");
                    Assert.That(waves[i].Additions.OfType<SinkNodeDefinition>().All(s => Tier(s) == "middle"), Is.True);
                    Assert.That(waves[i].Additions.OfType<SourceNodeDefinition>().Single().Position.x, Is.GreaterThan(176), "The first east Source arrives with the bridges.");
                }
                if (wave == 7) Assert.That(waves[i].Additions.OfType<SinkNodeDefinition>().Single().Color, Is.EqualTo(FlowColor.Purple));
                Assert.That(waves[i].Additions.OfType<SourceNodeDefinition>().All(n => n.GenerationDelay >= 15), Is.True);
                double rate = nodes.OfType<SourceNodeDefinition>().Sum(n => 1 / (n.GenerationInterval * waves[i].IntervalScale));
                Assert.That(rate, Is.GreaterThan(previousRate), $"Wave {wave} must raise the total demand.");
                previousRate = rate;
            }
            Assert.That(nodes.Select(n => n.Id).Distinct().Count(), Is.EqualTo(nodes.Count));
            Assert.That(nodes.OfType<SinkNodeDefinition>().Select(n => n.Color).Distinct(), Is.EquivalentTo(Enum.GetValues(typeof(FlowColor)).Cast<FlowColor>()));
            Assert.That(nodes.Where(n => n.MaxOutgoing > 0).All(n => n.MaxOutgoing <= 5), Is.True, "Limited OUT slots keep Relay choices meaningful.");
            Assert.That(nodes.OfType<SourceNodeDefinition>().All(n => n.MaxOutgoing == 2), Is.True);
            Assert.That(EditorBuildSettings.scenes.Any(s => s.enabled && s.path.EndsWith("/TokyoStationWiringLab.unity")), Is.True);
        }

        [Test]
        public void EveryNodeBelongsToOneTierAndPurpleSinksExistOnlyInTheUpperTier()
        {
            var config = Configuration;
            var stage = config.Load(Settings.Clearance);
            var nodes = stage.Nodes.Concat(config.LoadWaves(stage, Settings.Clearance).SelectMany(w => w.Additions)).ToArray();
            Assert.That(nodes.Select(Tier), Has.None.EqualTo("outside"), string.Join(", ", nodes.Where(n => Tier(n) == "outside").Select(n => n.Id)));
            foreach (var group in nodes.GroupBy(Tier))
                Assert.That(group.Select(n => n.Kind).Distinct().Count(), Is.EqualTo(3), group.Key + " has Sources, Relays and Sinks.");
            var sinks = nodes.OfType<SinkNodeDefinition>().ToArray();
            Assert.That(sinks.Where(s => s.Color == FlowColor.Purple).Select(Tier), Has.All.EqualTo("upper"));
            Assert.That(sinks.Where(s => Tier(s) == "upper").Select(s => s.Color), Has.All.EqualTo(FlowColor.Purple), "The upper tier consumes only purple.");
            foreach (var tier in new[] { "lower", "middle" })
                Assert.That(sinks.Where(s => Tier(s) == tier).Select(s => s.Color).Distinct(),
                    Is.EquivalentTo(new[] { FlowColor.Red, FlowColor.Blue, FlowColor.Green, FlowColor.Yellow }), tier + " offers the four ground colors.");
            // East ground Sinks arrive later and include the four colors as well.
            Assert.That(sinks.Where(s => Tier(s) == "lower" && s.Position.x > 176).Select(s => s.Color).Distinct(),
                Is.EquivalentTo(new[] { FlowColor.Red, FlowColor.Blue, FlowColor.Green, FlowColor.Yellow }));
            // Same colors sit on different sides of the station between lower west, lower east and middle.
            foreach (var color in new[] { FlowColor.Red, FlowColor.Blue, FlowColor.Green, FlowColor.Yellow })
            {
                var west = sinks.Single(s => s.Color == color && Tier(s) == "lower" && s.Position.x < -151);
                var middle = sinks.Where(s => s.Color == color && Tier(s) == "middle");
                Assert.That(middle.All(m => Vector3.Distance(new Vector3(m.Position.x, 0, m.Position.z), new Vector3(west.Position.x, 0, west.Position.z)) > 60), Is.True,
                    color + " middle Sinks are offset from the west Sink.");
            }
            // Elevated Nodes sit just above a surveyed building roof (Bounds approximation of the imported mesh).
            foreach (var node in nodes.Where(n => n.Position.y > Ground + 0.01f))
            {
                var p = node.Position;
                Assert.That(stage.Buildings.Any(b => p.x >= b.min.x && p.x <= b.max.x && p.z >= b.min.z && p.z <= b.max.z &&
                    p.y > b.max.y + Settings.Clearance && p.y <= b.max.y + 1.5f), Is.True, node.Id + " must sit just above an imported roof.");
            }
        }

        [Test]
        public void TiersConnectOnlyThroughBridgeRelaysPlacedOnTheLowerSide()
        {
            var config = Configuration;
            var stage = config.Load(Settings.Clearance);
            var nodes = stage.Nodes.Concat(config.LoadWaves(stage, Settings.Clearance).SelectMany(w => w.Additions)).ToDictionary(n => n.Id);
            var relays = nodes.Values.OfType<RelayNodeDefinition>().ToArray();
            // Sources and Sinks have fixed heights, so they define which tiers a Relay can serve.
            var endpoints = nodes.Values.Where(n => n.Kind != NodeKind.Relay).ToArray();
            bool Bridge(RelayNodeDefinition r) => endpoints.Any(n => Tier(n) != Tier(r) && stage.SharesAltitude(r, n));
            var bridges = relays.Where(Bridge).Select(r => r.Id).OrderBy(id => id).ToArray();
            Assert.That(bridges, Is.EqualTo(new[] { "EB", "RU", "WB" }), "Exactly three bridge Relays.");
            foreach (var relay in relays.Where(r => !Bridge(r)))
                Assert.That(endpoints.Where(n => Tier(n) != Tier(relay)).Any(n => stage.SharesAltitude(relay, n)), Is.False, relay.Id + " stays inside its tier.");
            Assert.That(Tier(nodes["WB"]), Is.EqualTo("lower")); Assert.That(Tier(nodes["EB"]), Is.EqualTo("lower")); Assert.That(Tier(nodes["RU"]), Is.EqualTo("middle"));
            Assert.That(nodes["WB"].Position.x, Is.LessThan(-151)); Assert.That(nodes["EB"].Position.x, Is.GreaterThan(176));
            foreach (var id in new[] { "WB", "EB" })
            {
                Assert.That(nodes.Values.Where(n => Tier(n) == "middle").All(n => stage.SharesAltitude(nodes[id], n)), Is.True, id + " reaches the whole middle band.");
                Assert.That(nodes.Values.Where(n => Tier(n) == "upper").Any(n => stage.SharesAltitude(nodes[id], n)), Is.False, id + " never reaches the upper tier.");
            }
            Assert.That(nodes.Values.Where(n => Tier(n) == "upper").All(n => stage.SharesAltitude(nodes["RU"], n)), Is.True);
            Assert.That(endpoints.Where(n => Tier(n) == "lower").Any(n => stage.SharesAltitude(nodes["RU"], n)), Is.False, "A middle Relay never descends to the ground.");
            Assert.That(stage.SharesAltitude(nodes["RU"], nodes["WB"]), Is.True, "Bridges meet each other inside the middle band.");
            // No single Relay spans lower and upper endpoints.
            Assert.That(relays.Any(r => endpoints.Any(a => Tier(a) == "lower" && stage.SharesAltitude(r, a)) &&
                endpoints.Any(b => Tier(b) == "upper" && stage.SharesAltitude(r, b))), Is.False);

            var planner = new LineRoutePlanner(stage, Settings.Clearance);
            bool Can(string from, string to) => planner.Generate(nodes[from], nodes[to]).IsValid;
            Assert.That(Can("R3", "RE1"), Is.False, "The station complex separates the west and east ground.");
            Assert.That(Can("R1", "RED-M"), Is.False, "Lower hubs cannot lift to the roofs.");
            Assert.That(Can("WB", "RED-M"), Is.True, "The west bridge reaches the dome Sinks.");
            Assert.That(Can("WB", "GREEN-M"), Is.False, "A 30 m annex Sink is hidden behind the 41 m station from the west.");
            Assert.That(Can("EB", "GREEN-M"), Is.True); Assert.That(Can("EB", "RED-M"), Is.True);
            var crossing = planner.Generate(nodes["WB"], nodes["RM1"]).Route ?? throw new AssertionException("Relay to Relay must cross above the station.");
            Assert.That(crossing.Points.Max(p => p.y), Is.GreaterThan(41.7f));
            Assert.That(Can("SU1", "RU"), Is.True); Assert.That(Can("RU", "PURPLE-U"), Is.True); Assert.That(Can("RU", "RM1"), Is.True);
            Assert.That(Can("SU1", "WB"), Is.False); Assert.That(Can("RU", "R1"), Is.False);
            Assert.That(Can("SM1", "RM1"), Is.True, "A dome Source sends horizontally to the annex hub.");
            Assert.That(Can("SM2", "WB"), Is.False, "An annex Source cannot reach the west bridge behind the station.");
        }

        [Test]
        public void Node360CandidatesHideNodesOutsideTheReachableAltitudeBand()
        {
            var config = Configuration;
            var stage = config.Load(Settings.Clearance);
            var waves = config.LoadWaves(stage, Settings.Clearance);
            var network = config.LoadNetwork(stage, Settings.LoadNetworkSettings());
            foreach (var wave in waves) Assert.That(network.TryAddNodes(wave.Additions), Is.True);
            var nodes = network.NodeDefinitions.ToDictionary(n => n.Id);
            using var preview = new LinePreviewService(network, new LineRoutePlanner(stage, Settings.Clearance));
            using var session = new ConnectionSession(network, preview);
            IEnumerable<string> Candidates(string from)
            {
                Assert.That(session.Begin(from), Is.True);
                var ids = session.Candidates().Select(c => c.Node.Definition.Id).ToArray();
                session.Cancel();
                return ids;
            }
            var r1 = Candidates("R1").ToArray();
            Assert.That(r1.Select(id => Tier(nodes[id])), Has.All.EqualTo("lower"));
            Assert.That(r1, Does.Contain("RE1"), "Same-tier Nodes stay listed even when the route is blocked; the route reason explains it.");
            var wb = Candidates("WB").ToArray();
            Assert.That(wb, Does.Contain("RED-M").And.Contain("RM1").And.Contain("RED").And.Not.Contain("PURPLE-U").And.Not.Contain("RU2"));
            var ru = Candidates("RU").ToArray();
            Assert.That(ru, Does.Contain("PURPLE-U").And.Contain("RM1").And.Contain("RED-M").And.Contain("WB").And.Contain("EB")
                .And.Not.Contain("RED").And.Not.Contain("R1").And.Not.Contain("RE1"), "Bridges list each other; ground endpoints stay hidden.");
            var su1 = Candidates("SU1").ToArray();
            Assert.That(su1, Is.EquivalentTo(new[] { "RU", "RU2", "PURPLE-U2" }),
                "A west tower Source sends at its own height: the higher east tower Sink is out of reach without a Relay.");
            var sm1 = Candidates("SM1").ToArray();
            Assert.That(sm1, Does.Contain("RM1").And.Contain("WB").And.Contain("EB").And.Contain("RU").And.Not.Contain("RED").And.Not.Contain("PURPLE-U"));
        }

        [Test]
        public void EveryWaveCanDeliverEverySourceColorWithLegalSlotsAndBuildingAvoidance()
        {
            var config = Configuration;
            var stage = config.Load(Settings.Clearance);
            var waves = config.LoadWaves(stage, Settings.Clearance);
            var network = config.LoadNetwork(stage, Settings.LoadNetworkSettings());
            var wiring = Wiring(stage);
            int created = 0;
            for (int wave = 0; wave <= waves.Count; wave++)
            {
                if (wave > 0) Assert.That(network.TryAddNodes(waves[wave - 1].Additions), Is.True);
                created += wiring.Extend(network).Count;
                foreach (var source in network.NodeDefinitions.OfType<SourceNodeDefinition>())
                    foreach (var color in network.NodeDefinitions.OfType<SinkNodeDefinition>().Select(s => s.Color).Distinct())
                    {
                        long before = network.Snapshot().DeliveredCount;
                        network.GenerateFlow(source.Id, color);
                        for (int second = 0; second < 120 && network.Snapshot().DeliveredCount == before; second++)
                        { network.RouteWaitingFlows(); network.AdvanceInFlight(1); }
                        Assert.That(network.Snapshot().DeliveredCount, Is.EqualTo(before + 1), $"Wave {wave + 1}: {source.Id} -> {color}");
                    }
            }
            Assert.That(created, Is.EqualTo(network.Snapshot().Lines.Count));
            // Purple must climb through the middle-upper bridge from every tier; nothing else reaches the upper Sinks.
            Assert.That(network.Snapshot().Lines.Where(l => l.DestinationId.StartsWith("PURPLE")).Select(l => l.SourceId).Distinct(),
                Is.SubsetOf(new[] { "RU", "RU2", "SU1", "SU2", "SU3", "SU4", "SU5" }));
            Assert.That(network.Snapshot().Lines.Any(l => l.SourceId == "WB" && l.DestinationId == "RU" || l.SourceId == "EB" && l.DestinationId == "RU" ||
                l.DestinationId == "RU" && l.SourceId.StartsWith("RM")), Is.True, "Ground purple climbs via the middle tier.");
        }

        [TestCase(1337)]
        [TestCase(42)]
        [TestCase(2026)]
        public void ExtendingTheNetworkAfterEachWaveSurvivesIntoTheFinalWaveWithoutResettingFlows(int seed)
        {
            var config = Configuration;
            var stage = config.Load(Settings.Clearance);
            var network = config.LoadNetwork(stage, Settings.LoadNetworkSettings());
            var waves = config.LoadWaves(stage, Settings.Clearance);
            var clock = new FlowSimulation(network, new SystemRandomSource(seed), waves);
            var wiring = Wiring(stage);
            var lineIds = new HashSet<int>();
            for (int second = 0; second < 600; second++)
            {
                // Allow ten seconds of player response time after each new Wave.
                if (second == 10 || waves.Any(w => Math.Abs(w.StartSeconds + 10 - second) < 0.5))
                {
                    wiring.Extend(network);
                    foreach (var line in network.Snapshot().Lines) lineIds.Add(line.Id);
                }
                clock.Tick(1);
                Assert.That(network.IsGameOver, Is.False, $"Seed {seed}, second {second + 1}, Wave {clock.Wave}, Source {network.GameOverSourceId}");
                var state = network.Snapshot();
                Assert.That(state.GeneratedCount, Is.EqualTo(state.DeliveredCount +
                    state.Nodes.Sum(n => n.Buffer.Count) + state.Lines.Sum(l => l.InFlight.Count)));
                var colors = network.NodeDefinitions.OfType<SinkNodeDefinition>().Select(n => n.Color).ToArray();
                Assert.That(state.Nodes.SelectMany(n => n.Buffer).Concat(state.Lines.SelectMany(l => l.InFlight.Select(f => f.Flow)))
                    .All(f => colors.Contains(f.Color)), Is.True, "A new color requires its Sink first.");
                Assert.That(state.Lines.Select(l => l.Id), Is.SupersetOf(lineIds), "Earlier Lines survive every Wave.");
            }
            Assert.That(clock.Wave, Is.EqualTo(FinalWave));
            Assert.That(clock.NextWaveSeconds, Is.Null);
            Assert.That(clock.Result, Is.Null, "Wave 10 continues as survival play.");
            Assert.That(network.Snapshot().DeliveredCount, Is.GreaterThan(300));
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
                if (wireOpening) Wiring(stage).Extend(network);
                clock.Tick(600);
                Assert.That(clock.Result, Is.Not.Null);
                if (wireOpening) Assert.That(clock.Wave, Is.GreaterThan(2));
                else Assert.That(clock.Wave, Is.LessThanOrEqualTo(2), "An unwired opening overloads S1 before Wave 3.");
                Assert.That(network.NodeDefinitions.OfType<SourceNodeDefinition>().Any(n => n.Id == clock.Result?.SourceId), Is.True);
            }
        }
    }
}
