#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Domain.Spatial;

namespace CityFlow.Application.UseCases
{
    public sealed class FlowSimulation
    {
        // Provisional deterministic tick duration [s], independent of rendering frame rate.
        public const double StepSeconds = 0.05;
        private readonly FlowNetwork network;
        private readonly IRandomSource random;
        private readonly NodeDefinition[] sources;
        private readonly Dictionary<string, double> nextGeneration = new Dictionary<string, double>();
        private double remainder;
        private long ticks;
        public double ElapsedSeconds => ticks * StepSeconds;

        public FlowSimulation(FlowNetwork network, IRandomSource random)
        {
            this.network = network ?? throw new ArgumentNullException(nameof(network));
            this.random = random ?? throw new ArgumentNullException(nameof(random));
            sources = network.NodeDefinitions.Where(node => node.Kind == NodeKind.Source).ToArray();
            foreach (NodeDefinition source in sources) nextGeneration.Add(source.Id, source.GenerationInterval);
        }

        public void Tick(double deltaSeconds)
        {
            if (double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds) || deltaSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            if (deltaSeconds == 0 || network.IsGameOver) return;
            if (double.IsInfinity(remainder + deltaSeconds)) throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            remainder += deltaSeconds;
            while (!network.IsGameOver && remainder + 1e-9 >= StepSeconds)
            {
                remainder = Math.Max(0, remainder - StepSeconds);
                ticks++;
                GenerateDueFlows();
                network.AdvanceInFlight(StepSeconds);
                network.RouteWaitingFlows(random);
                network.EvaluateOverload(StepSeconds);
            }
        }
        private void GenerateDueFlows()
        {
            FlowColor[] colors = network.NodeDefinitions.Where(node => node.SinkColor.HasValue)
                .Select(node => node.SinkColor.GetValueOrDefault()).Distinct().OrderBy(color => color).ToArray();
            foreach (NodeDefinition source in sources)
            {
                while (nextGeneration[source.Id] <= ElapsedSeconds + 1e-9)
                {
                    int choice = colors.Length == 1 ? 0 : random.NextIndex(colors.Length);
                    if (choice < 0 || choice >= colors.Length)
                        throw new InvalidOperationException("Random source returned an index outside the Sink color range.");
                    network.GenerateFlow(source.Id, colors[choice]);
                    nextGeneration[source.Id] += source.GenerationInterval;
                }
            }
        }
    }
}
