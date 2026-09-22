#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Domain.Spatial;
using NUnit.Framework;
using UnityEngine;

namespace CityFlow.Tests.Fixtures
{
    public static class RelayCongestion
    {
        // Dispatch while a Sink route exists, then disconnect it before arrival. Parallel nearby
        // Sources fill the Relay before the tested incoming Line arrives, using only public commands.
        public static void Prepare(FlowNetwork network, string sourceId, string relayId,
            IReadOnlyList<FlowColor> buffered, IReadOnlyList<FlowColor> incoming,
            Func<string, string, int> connect)
        {
            var relay = network.NodeDefinitions.Single(n => n.Id == relayId);
            var temporaryInputs = new List<int>();
            for (int offset = 0; offset < buffered.Count; offset += network.Settings.MaxInFlight)
            {
                string id = "FixtureSource" + offset;
                Vector3 position = relay.Position + Vector3.right * (0.01f * (offset + 1));
                Assert.That(network.TryAddNodes(new NodeDefinition[] {
                    new SourceNodeDefinition(id, position, generationInterval: 1e6, generationDelay: 1e6) }), Is.True);
                var line = network.TryConnect(id, relayId, new[] { position, relay.Position });
                Assert.That(line.Succeeded, Is.True, line.Failure.ToString());
                temporaryInputs.Add(line.LineId.GetValueOrDefault());
                foreach (FlowColor color in buffered.Skip(offset).Take(network.Settings.MaxInFlight))
                    network.GenerateFlow(id, color);
            }
            int[] exits = buffered.Concat(incoming).Distinct().Select(color => connect(relayId,
                network.NodeDefinitions.First(n => n.SinkColor == color).Id)).ToArray();
            foreach (FlowColor color in incoming) network.GenerateFlow(sourceId, color);
            network.RouteWaitingFlows();
            foreach (int exit in exits) Assert.That(network.RequestDeletion(exit), Is.True);
            network.AdvanceInFlight(0.1);
            foreach (int input in temporaryInputs)
            {
                Assert.That(network.Snapshot().Lines.Single(l => l.Id == input).InFlight, Is.Empty);
                Assert.That(network.RequestDeletion(input), Is.True);
            }
            Assert.That(network.Snapshot().Nodes.Single(n => n.Definition.Id == relayId).Buffer.Select(f => f.Color),
                Is.EqualTo(buffered));
        }
    }
}
