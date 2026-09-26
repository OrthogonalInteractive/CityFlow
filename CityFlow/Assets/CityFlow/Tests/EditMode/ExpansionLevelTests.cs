#nullable enable

using System;
using System.Linq;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Domain.Spatial;
using CityFlow.Editor;
using CityFlow.Infrastructure.Configuration;
using CityFlow.Infrastructure.Routing;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CityFlow.Tests.EditMode
{
    public sealed class ExpansionLevelTests
    {
        private static StageConfiguration Configuration => AssetDatabase.LoadAssetAtPath<StageConfiguration>(ExpansionLabSetup.StagePath);

        [Test] public void AuthoredLayoutHasCentralColorsOffsetCornerSinksTwoRingsAndUniformSourceCoverage()
        {
            var config = Configuration;
            var stage = config.Load(0.5f);
            var nodes = stage.Nodes.Concat(config.LoadWaves(stage, 0.5f).SelectMany(w => w.Additions)).ToArray();
            float Radius(NodeDefinition n) => new Vector2(n.Position.x, n.Position.z).magnitude;
            var sinks = nodes.OfType<SinkNodeDefinition>().ToArray();
            Assert.That(sinks.Where(n => Radius(n) < 30).Select(n => n.Color), Is.EquivalentTo(Enum.GetValues(typeof(FlowColor))));
            var corners = sinks.Where(n => Radius(n) > 150).ToArray();
            Assert.That(corners.Length, Is.EqualTo(4));
            Assert.That(corners.Select(n => (Math.Sign(n.Position.x), Math.Sign(n.Position.z))).Distinct().Count(), Is.EqualTo(4));
            Assert.That(corners.Select(n => n.Color).Distinct().Count(), Is.EqualTo(4));
            Assert.That(corners.All(n => Math.Abs(n.Position.x) < 176 && Math.Abs(n.Position.z) < 146), Is.True);
            var relays = nodes.OfType<RelayNodeDefinition>().ToArray();
            Assert.That(relays.Count(n => Radius(n) >= 35 && Radius(n) <= 55), Is.EqualTo(8));
            Assert.That(relays.Count(n => Radius(n) >= 90 && Radius(n) <= 130), Is.EqualTo(12));
            Assert.That(relays.Select(n => n.MaximumRise).Distinct().Count(), Is.GreaterThanOrEqualTo(5));
            foreach (var ring in new[] { relays.Where(n => Radius(n) < 70), relays.Where(n => Radius(n) > 70) })
            {
                var angles = ring.Select(n => (Mathf.Atan2(n.Position.z, n.Position.x) * Mathf.Rad2Deg + 360) % 360).OrderBy(a => a).ToArray();
                var gaps = angles.Select((a, i) => (angles[(i + 1) % angles.Length] - a + 360) % 360).ToArray();
                Assert.That(gaps.Max(), Is.GreaterThanOrEqualTo(60), "Each ring needs a sparse sector, not uniform stepping stones.");
                Assert.That(gaps.Max() / gaps.Min(), Is.GreaterThanOrEqualTo(3), "Curated clusters must differ materially from the large gaps.");
            }
            var sources = nodes.OfType<SourceNodeDefinition>().ToArray();
            Assert.That(sources.Length, Is.EqualTo(12));
            Assert.That(sources.Select(n => (Mathf.FloorToInt((n.Position.x + 180) / 120),
                Mathf.FloorToInt((n.Position.z + 150) / 75))).Distinct().Count(), Is.EqualTo(12));
            Assert.That(sources.Count(n => n.Position.y > 0), Is.GreaterThanOrEqualTo(3));
            foreach (var a in nodes)
                foreach (var b in nodes.Where(n => string.CompareOrdinal(n.Id, a.Id) > 0))
                    Assert.That(Vector3.Distance(a.Position, b.Position), Is.GreaterThan(8), a.Id + " / " + b.Id);
        }

        [Test] public void EveryUnlockSupportsSlotValidDeliveryOfEverySourceColor()
        {
            var config = Configuration; var stage = config.Load(0.5f);
            var waves = config.LoadWaves(stage, 0.5f);
            var network = config.LoadNetwork(stage, new NetworkSettings(10, 5, 3, 8, 0.5f));
            var planner = new LineRoutePlanner(stage, 0.5f);
            Assert.That(waves.Count, Is.EqualTo(9));
            Assert.That(config.Lines, Is.Empty, "Evaluation wiring must not become the player's starting network.");
            for (int i = -1; i < waves.Count; i++)
            {
                if (i >= 0)
                {
                    Assert.That(waves[i].Additions, Is.Not.Empty, "Each unlock should introduce a new wiring decision.");
                    float previousArea = stage.WalkableArea.width * stage.WalkableArea.height;
                    Assert.That(network.TryAddNodes(waves[i].Additions, waves[i].ExpandedArea), Is.True);
                    Assert.That(stage.WalkableArea.width * stage.WalkableArea.height, Is.GreaterThan(previousArea));
                }
                ExpansionLabEvaluation.ConnectAvailable(network, planner, true);
                foreach (var source in network.NodeDefinitions.OfType<SourceNodeDefinition>())
                    foreach (FlowColor color in Enum.GetValues(typeof(FlowColor)))
                    {
                        long before = network.Snapshot().DeliveredCount;
                        network.GenerateFlow(source.Id, color);
                        for (int seconds = 0; seconds < 600 && network.Snapshot().DeliveredCount == before; seconds++)
                        { network.RouteWaitingFlows(); network.AdvanceInFlight(1); }
                        Assert.That(network.Snapshot().DeliveredCount, Is.EqualTo(before + 1), $"Wave {i + 2}: {source.Id} / {color}");
                    }
            }
            Assert.That(stage.WalkableArea, Is.EqualTo(stage.MaximumArea));
            Assert.That(stage.MaximumArea.width * stage.MaximumArea.height, Is.GreaterThan(120 * 90 * 5));
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(ExpansionLabSetup.ScenePath), Is.Not.Null);
        }

        [Test] public void AuthoredWavesIncreasePressureWhilePeripheralWiringRemainsViableThroughEvaluationWindow()
        {
            // Provisional level-design acceptance, not a claim about optimal wiring or human difficulty.
            var report = JsonUtility.FromJson<ExpansionLabEvaluation.Report>(ExpansionLabEvaluation.Evaluate());
            foreach (var run in report.runs)
            {
                Assert.That(run.waves.Count, Is.EqualTo(10), "Early Waves must leave time to build the network.");
                double previous = 0, previousRelayPressure = 0, previousAggregateLoad = 0;
                foreach (var wave in run.waves)
                {
                    Assert.That(wave.nominalBottleneckUtilization, Is.GreaterThan(previous), $"{run.strategy}, Wave {wave.wave}");
                    previous = wave.nominalBottleneckUtilization;
                    Assert.That(wave.nominalRelayBottleneckUtilization, Is.GreaterThan(previousRelayPressure), $"Relay pressure: Wave {wave.wave}");
                    previousRelayPressure = wave.nominalRelayBottleneckUtilization;
                    double aggregateLoad = wave.relayForwardingDemand / wave.nominalRelayServiceRate;
                    Assert.That(aggregateLoad, Is.GreaterThan(previousAggregateLoad), $"Aggregate Relay load: Wave {wave.wave}");
                    previousAggregateLoad = aggregateLoad;
                    Assert.That(wave.generated, Is.EqualTo(wave.delivered + wave.waiting + wave.inFlight));
                }
                Assert.That(run.waves[0].nominalBottleneckUtilization, Is.LessThan(0.25));
                Assert.That(run.waves[9].nominalRelayBottleneckUtilization, Is.GreaterThan(1.2));
                Assert.That(run.waves[9].fullRelaySeconds, Is.GreaterThan(0), "Late pressure must appear in actual queues too.");
                if (run.strategy == "Peripheral sinks connected")
                    Assert.That(run.waves[9].gameOver, Is.False, "This reference should survive the 990-second comparison window.");
            }
        }
    }
}
