#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Domain.Spatial;
using CityFlow.Domain.Progression;

namespace CityFlow.Application.UseCases
{
    public sealed class FlowSimulation
    {
        // Provisional deterministic tick duration [s], independent of rendering frame rate.
        public const double StepSeconds = 0.05;
        private readonly FlowNetwork network;
        private readonly IRandomSource random;
        private readonly IReadOnlyList<WaveDefinition> waves;
        private int waveIndex;
        private readonly Dictionary<string,double> readyAt = new();
        private IReadOnlyList<NodeDefinition> latestAdditions = Array.Empty<NodeDefinition>();
        public IReadOnlyList<NodeDefinition> LatestAdditions => latestAdditions;
        public double LastWaveSeconds { get; private set; }
        public double GenerationIntervalScale { get; private set; } = 1;
        public double? NextWaveSeconds => waveIndex < waves.Count ? waves[waveIndex].StartSeconds : (double?)null;
        private readonly Dictionary<string, double> nextGeneration = new Dictionary<string, double>();
        private double remainder;
        private long ticks;
        public bool IsPaused { get; private set; }
        public void SetPaused(bool value) { if (!network.IsGameOver) IsPaused=value; }
        public double ElapsedSeconds => ticks * StepSeconds;

        public int Wave => waveIndex+1;
        public SessionResult? Result { get; private set; }
        public double SourceStartRemaining(string id) => readyAt.TryGetValue(id,out double at) ? Math.Max(0,at-ElapsedSeconds) : 0;
        public FlowSimulation(FlowNetwork network, IRandomSource random, IReadOnlyList<WaveDefinition>? waves = null)
        {
            this.network = network ?? throw new ArgumentNullException(nameof(network));
            this.random = random ?? throw new ArgumentNullException(nameof(random));
            this.waves=Array.AsReadOnly((waves ?? Array.Empty<WaveDefinition>()).ToArray());
            double previous=0;
            foreach(var wave in this.waves)
            {
                if(wave.StartSeconds<=previous) throw new ArgumentException("Wave times must be strictly increasing.",nameof(waves));
                previous=wave.StartSeconds;
            }
            network.ValidateAdditionalNodes(this.waves.SelectMany(w=>w.Additions));
            foreach(NodeDefinition source in network.NodeDefinitions.Where(n=>n.Kind==NodeKind.Source)) RegisterSource(source);
        }

        public void Tick(double deltaSeconds)
        {
            if (double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds) || deltaSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            if(network.IsGameOver) { CaptureResult(); return; }
            if (deltaSeconds == 0 || IsPaused) return;
            if (double.IsInfinity(remainder + deltaSeconds)) throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            remainder += deltaSeconds;
            while (!network.IsGameOver && remainder + 1e-9 >= StepSeconds)
            {
                remainder = Math.Max(0, remainder - StepSeconds);
                ticks++;
                ApplyDueWaves();
                GenerateDueFlows();
                network.AdvanceInFlight(StepSeconds);
                network.RouteWaitingFlows(random);
                network.EvaluateOverload(StepSeconds);
                if(network.IsGameOver) CaptureResult();
            }
        }
        private void CaptureResult()
        {
            if(Result==null && network.GameOverSourceId!=null)
                Result=new SessionResult(Wave,ElapsedSeconds,network.Snapshot().DeliveredCount,network.GameOverSourceId);
        }
        private void RegisterSource(NodeDefinition source)
        {
            readyAt.Add(source.Id,ElapsedSeconds+source.GenerationDelay);
            nextGeneration.Add(source.Id,readyAt[source.Id]+source.GenerationInterval*GenerationIntervalScale);
        }
        private void ApplyDueWaves()
        {
            while(waveIndex<waves.Count && waves[waveIndex].StartSeconds<=ElapsedSeconds+1e-9)
            {
                WaveDefinition wave=waves[waveIndex];
                // Preserve remaining generation phase while keeping source preparation time intact.
                foreach(string id in nextGeneration.Keys.ToArray())
                {
                    double origin=Math.Max(ElapsedSeconds,readyAt[id]);
                    nextGeneration[id]=origin+Math.Max(0,nextGeneration[id]-origin)*wave.IntervalScale/GenerationIntervalScale;
                }
                GenerationIntervalScale=wave.IntervalScale;
                if(!network.TryAddNodes(wave.Additions.OrderBy(n=>n.Kind==NodeKind.Sink ? 0 : 1).ToArray()))
                    throw new InvalidOperationException("The authored Wave could not add its validated Nodes.");
                foreach(var source in wave.Additions.Where(n=>n.Kind==NodeKind.Source)) RegisterSource(source);
                latestAdditions=wave.Additions; LastWaveSeconds=ElapsedSeconds; waveIndex++;
            }
        }
        private void GenerateDueFlows()
        {
            FlowColor[] colors = network.NodeDefinitions.Where(node => node.SinkColor.HasValue)
                .Select(node => node.SinkColor.GetValueOrDefault()).Distinct().OrderBy(color => color).ToArray();
            foreach (NodeDefinition source in network.NodeDefinitions.Where(n=>n.Kind==NodeKind.Source))
            {
                while (nextGeneration[source.Id] <= ElapsedSeconds + 1e-9)
                {
                    int choice = colors.Length == 1 ? 0 : random.NextIndex(colors.Length);
                    if (choice < 0 || choice >= colors.Length)
                        throw new InvalidOperationException("Random source returned an index outside the Sink color range.");
                    network.GenerateFlow(source.Id, colors[choice]);
                    nextGeneration[source.Id] += source.GenerationInterval*GenerationIntervalScale;
                }
            }
        }
    }
}
