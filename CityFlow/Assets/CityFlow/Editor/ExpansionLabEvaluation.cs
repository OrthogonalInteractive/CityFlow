#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using CityFlow.Application.Routing;
using CityFlow.Application.UseCases;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Domain.Spatial;
using CityFlow.Infrastructure.Configuration;
using CityFlow.Infrastructure.Routing;
using UnityEditor;
using UnityEngine;

namespace CityFlow.Editor
{
    public static class ExpansionLabEvaluation
    {
        public static IReadOnlyList<(string From, string To)> ReferenceConnections(bool peripheral)
        {
            var edges = new List<(string, string)>();
            void Ring(string prefix, int count)
            {
                for (int i = 1; i <= count; i++)
                {
                    string a = prefix + i.ToString("00"), b = prefix + (i % count + 1).ToString("00");
                    edges.Add((a, b)); edges.Add((b, a));
                }
            }
            Ring("I", 8); Ring("O", 12);
            string[] destinations = { "C-BLUE", "C-YELLOW", "C-PURPLE", "C-GREEN", "C-RED", "C-RED", "C-PURPLE", "C-BLUE" };
            for (int i = 0; i < destinations.Length; i++) edges.Add(("I" + (i + 1).ToString("00"), destinations[i]));
            edges.AddRange(new[] { ("O02", "I01"), ("O05", "I04"), ("O08", "I05"), ("O11", "I07") });
            string[] feeders = { "O07", "O10", "O11", "O08", "I06", "O01", "O06", "I02", "O02", "O05", "I03", "O03" };
            for (int i = 0; i < feeders.Length; i++) edges.Add(("S" + (i + 1).ToString("00"), feeders[i]));
            if (peripheral) edges.AddRange(new[] { ("O09", "SW-RED"), ("O12", "SE-BLUE"), ("O06", "NW-YELLOW"), ("O03", "NE-GREEN"),
                ("S01", "SW-RED"), ("S03", "SE-BLUE"), ("S10", "NW-YELLOW"), ("S12", "NE-GREEN") });
            return edges;
        }

        public static int ConnectAvailable(FlowNetwork network, ILineRoutePlanner planner, bool peripheral)
        {
            var nodes = network.NodeDefinitions.ToDictionary(n => n.Id);
            int count = 0;
            foreach (var edge in ReferenceConnections(peripheral))
            {
                if (!nodes.ContainsKey(edge.From) || !nodes.ContainsKey(edge.To) ||
                    network.Snapshot().Lines.Any(l => l.SourceId == edge.From && l.DestinationId == edge.To)) continue;
                var route = planner.Generate(nodes[edge.From], nodes[edge.To]);
                if (!route.IsValid) throw new InvalidOperationException($"Unreachable reference Line: {edge.From} -> {edge.To}: {route.Failure}");
                var result = network.TryConnect(edge.From, edge.To, route.Route!.Points);
                if (!result.Succeeded) throw new InvalidOperationException($"Invalid reference slots: {edge.From} -> {edge.To}: {result.Failure}");
                count++;
            }
            return count;
        }

        [Serializable] public sealed class WaveMeasurement
        {
            public int wave, nodes, sources, lines, peakSourceBuffer, peakRelayBuffer, waiting, inFlight;
            public int relays, relayOutgoingSlots, relayBufferCapacity;
            public float width, depth;
            public double seconds;
            public double sourceFlowPerSecond, nominalNetworkCapacity, nominalBottleneckUtilization, meanLineUtilization;
            public double relayForwardingDemand, nominalRelayServiceRate, nominalRelayBottleneckUtilization;
            public double fullRelaySeconds, sourceWarningSeconds;
            public string bottleneck = "";
            public long generated, delivered;
            public bool gameOver;
            public string cause = "";
        }
        [Serializable] public sealed class RunMeasurement
        {
            public string strategy = "";
            public int seed;
            public float generationInterval;
            public long analysisMilliseconds;
            public List<WaveMeasurement> waves = new();
        }
        [Serializable] public sealed class Report
        {
            public string method = "Deterministic Domain simulation; reference wiring is installed at each unlock. No human decision time is modeled. Nominal capacity uses uncongested Domain routes, equal color probabilities, and capacity * speed / length. It is a wiring-specific estimate: equal-length alternatives can relieve it; receiving-buffer contention can worsen it. Relay service sums outgoing Line rates and demand counts each Relay hop.";
            public List<RunMeasurement> runs = new();
        }

        public static string Evaluate(float generationInterval = 0)
        {
            var config = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<StageConfiguration>(ExpansionLabSetup.StagePath));
            try
            {
                if (generationInterval > 0)
                    foreach (var source in config.Nodes.Concat(config.Waves.SelectMany(w => w.Additions)).OfType<SourceNodePlacement>())
                        source.GenerationInterval = generationInterval;
                var settings = AssetDatabase.LoadAssetAtPath<GameplaySettings>(ExpansionLabSetup.SettingsPath);
                var report = new Report();
                foreach (bool peripheral in new[] { false, true })
                    foreach (int seed in new[] { 19010, 19011, 19012 })
                    {
                        var timer = Stopwatch.StartNew();
                        var stage = config.Load(settings.Clearance);
                        var network = config.LoadNetwork(stage, settings.LoadNetworkSettings());
                        var simulation = new FlowSimulation(network, new SystemRandomSource(seed), config.LoadWaves(stage, settings.Clearance));
                        var planner = new LineRoutePlanner(stage, settings.Clearance);
                        var run = new RunMeasurement { strategy = peripheral ? "Peripheral sinks connected" : "Central sinks only", seed = seed,
                            generationInterval = config.Nodes.OfType<SourceNodePlacement>().First().GenerationInterval };
                        for (int wave = 1; wave <= 10 && !network.IsGameOver; wave++)
                        {
                            ConnectAvailable(network, planner, peripheral);
                            var measure = new WaveMeasurement { wave = wave };
                            MeasureCapacity(network, stage, simulation.GenerationIntervalScale, measure);
                            double measuredSeconds = 0;
                            double until = wave < 10 ? wave * 90 - FlowSimulation.StepSeconds : 990;
                            while (simulation.ElapsedSeconds < until - 0.001 && !network.IsGameOver)
                            {
                                double before = simulation.ElapsedSeconds;
                                simulation.Tick(Math.Min(0.5, until - simulation.ElapsedSeconds));
                                double delta = simulation.ElapsedSeconds - before;
                                measuredSeconds += delta;
                                var frame = network.Snapshot();
                                measure.meanLineUtilization += delta * frame.Lines.Sum(l => l.InFlight.Count) /
                                    Math.Max(1, frame.Lines.Count * network.Settings.MaxInFlight);
                                measure.fullRelaySeconds += delta * frame.Nodes.Count(n => n.Definition.Kind == NodeKind.Relay && n.IsBufferFull);
                                if (frame.Nodes.Any(n => n.Definition.Kind == NodeKind.Source && n.Buffer.Count >= network.Settings.SourceBufferCapacity * 0.8))
                                    measure.sourceWarningSeconds += delta;
                                measure.peakSourceBuffer = Math.Max(measure.peakSourceBuffer,
                                    frame.Nodes.Where(n => n.Definition.Kind == NodeKind.Source).Max(n => n.Buffer.Count));
                                measure.peakRelayBuffer = Math.Max(measure.peakRelayBuffer,
                                    frame.Nodes.Where(n => n.Definition.Kind == NodeKind.Relay).Max(n => n.Buffer.Count));
                            }
                            var snapshot = network.Snapshot();
                            measure.meanLineUtilization /= Math.Max(0.001, measuredSeconds);
                            measure.nodes = snapshot.Nodes.Count; measure.sources = snapshot.Nodes.Count(n => n.Definition.Kind == NodeKind.Source);
                            measure.lines = snapshot.Lines.Count; measure.generated = snapshot.GeneratedCount; measure.delivered = snapshot.DeliveredCount;
                            measure.waiting = snapshot.Nodes.Sum(n => n.Buffer.Count); measure.inFlight = snapshot.Lines.Sum(l => l.InFlight.Count);
                            measure.width = stage.WalkableArea.width; measure.depth = stage.WalkableArea.height;
                            measure.seconds = simulation.ElapsedSeconds; measure.gameOver = network.IsGameOver;
                            measure.cause = network.GameOverSourceId ?? "";
                            run.waves.Add(measure);
                            if (wave < 10 && !network.IsGameOver) simulation.Tick(FlowSimulation.StepSeconds);
                        }
                        run.analysisMilliseconds = timer.ElapsedMilliseconds;
                        report.runs.Add(run);
                    }
                return JsonUtility.ToJson(report, true);
            }
            finally { UnityEngine.Object.DestroyImmediate(config); }
        }

        private static void MeasureCapacity(FlowNetwork network, StageDefinition stage, double intervalScale, WaveMeasurement measure)
        {
            var nodes = network.NodeDefinitions;
            var relays = nodes.OfType<RelayNodeDefinition>().ToArray();
            measure.relays = relays.Length;
            measure.relayOutgoingSlots = relays.Sum(n => n.MaxOutgoing);
            measure.relayBufferCapacity = relays.Length * network.Settings.RelayBufferCapacity;
            var probeStage = new StageDefinition(stage.GroundHeight, stage.WalkableArea, stage.Buildings, nodes, stage.MaximumAltitude, stage.MaximumArea);
            var probe = new FlowNetwork(probeStage, network.Settings);
            foreach (var line in network.Snapshot().Lines)
                if (!probe.TryConnect(line.SourceId, line.DestinationId, line.Route.Points).Succeeded)
                    throw new InvalidOperationException("The capacity probe must use the same valid wiring.");
            var loads = probe.Snapshot().Lines.ToDictionary(l => l.Id, _ => 0d);
            var colors = nodes.OfType<SinkNodeDefinition>().Select(n => n.Color).Distinct().ToArray();
            foreach (var source in nodes.OfType<SourceNodeDefinition>())
            {
                double rate = 1 / (source.GenerationInterval * intervalScale);
                measure.sourceFlowPerSecond += rate;
                foreach (FlowColor color in colors)
                {
                    long before = probe.Snapshot().DeliveredCount;
                    var flow = probe.GenerateFlow(source.Id, color);
                    for (int hop = 0; hop < nodes.Count && probe.Snapshot().DeliveredCount == before; hop++)
                    {
                        probe.RouteWaitingFlows();
                        var line = probe.Snapshot().Lines.FirstOrDefault(l => l.InFlight.Any(f => f.Flow.Id == flow.Id));
                        if (line == null) throw new InvalidOperationException($"No capacity path for {source.Id} / {color}.");
                        loads[line.Id] += rate / colors.Length;
                        probe.AdvanceInFlight(line.Route.Length / network.Settings.FlowSpeed + 0.01);
                    }
                    if (probe.Snapshot().DeliveredCount == before) throw new InvalidOperationException("Capacity probe did not reach a Sink.");
                }
            }
            foreach (var line in probe.Snapshot().Lines)
            {
                double utilization = loads[line.Id] * line.Route.Length / (network.Settings.MaxInFlight * network.Settings.FlowSpeed);
                if (relays.Any(n => n.Id == line.SourceId))
                {
                    measure.relayForwardingDemand += loads[line.Id];
                    measure.nominalRelayServiceRate += network.Settings.MaxInFlight * network.Settings.FlowSpeed / line.Route.Length;
                    measure.nominalRelayBottleneckUtilization = Math.Max(measure.nominalRelayBottleneckUtilization, utilization);
                }
                if (utilization <= measure.nominalBottleneckUtilization) continue;
                measure.nominalBottleneckUtilization = utilization;
                measure.bottleneck = line.SourceId + " -> " + line.DestinationId;
            }
            measure.nominalNetworkCapacity = measure.sourceFlowPerSecond / Math.Max(0.00001, measure.nominalBottleneckUtilization);
        }
    }
}
