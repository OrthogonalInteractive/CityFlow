#nullable enable

using System;
using System.Linq;

namespace CityFlow.Domain.FlowNetwork
{
    public sealed partial class FlowNetwork
    {
        public void RouteWaitingFlows(IRandomSource random)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            InvalidateSnapshot();
            foreach (var definition in NodeDefinitions)
            {
                NodeState node = nodes[definition.Id];
                for (int index = 0; index < node.Buffer.Count;)
                {
                    Flow flow = node.Buffer[index];
                    // Specification 7: direct matching Sinks define the candidate set even when full.
                    LineState[] open = node.Outgoing.Where(line => line.Status == LineStatus.Running).ToArray();
                    LineState[] direct = open.Where(line => line.Destination.Definition.SinkColor == flow.Color).ToArray();
                    LineState[] candidates = (direct.Length > 0 ? direct : open.Where(line => line.Destination.Definition.Kind == NodeKind.Relay))
                        .Where(line => line.InFlight.Count < Settings.MaxInFlight).ToArray();
                    if (candidates.Length == 0) { index++; continue; }
                    // Matching Sinks use stable connection order; only Relay choices consume randomness.
                    int choice = direct.Length > 0 || candidates.Length == 1 ? 0 : random.NextIndex(candidates.Length);
                    if (choice < 0 || choice >= candidates.Length)
                        throw new InvalidOperationException("Random source returned an index outside the candidate range.");
                    LineState selected = candidates[choice];
                    selected.InFlight.Add(new InFlightState(flow));
                    node.Buffer.RemoveAt(index);
                }
            }
        }

        public void AdvanceInFlight(double deltaSeconds)
        {
            if (double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds) || deltaSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            if (deltaSeconds == 0) return;
            InvalidateSnapshot();
            foreach (LineState line in lines)
            {
                // Equal capacity slots span the actual route; following spacing also applies before a blockage.
                double spacing = line.Route.Length / Settings.MaxInFlight;
                double frontLimit = line.Route.Length;
                bool blockedAhead = false;
                for (int index = 0; index < line.InFlight.Count;)
                {
                    InFlightState flight = line.InFlight[index];
                    flight.Distance = Math.Max(flight.Distance,
                        Math.Min(frontLimit, flight.Distance + Settings.FlowSpeed * deltaSeconds));
                    bool atEnd = flight.Distance >= line.Route.Length;
                    bool matchingSink = line.Destination.Definition.SinkColor == flight.Flow.Color;
                    int? capacity = Settings.BufferCapacity(line.Destination.Definition.Kind);
                    bool canReceive = matchingSink || (capacity.HasValue && line.Destination.Buffer.Count < capacity.Value);
                    if (atEnd && canReceive)
                    {
                        if (matchingSink) deliveredCount++;
                        else line.Destination.Buffer.Add(flight.Flow);
                        // Capacity is released only after consumption or a successful Buffer transfer.
                        line.InFlight.RemoveAt(index);
                        continue;
                    }
                    flight.IsStopped = (atEnd && !canReceive) || (blockedAhead && flight.Distance + 1e-8 >= frontLimit);
                    blockedAhead |= atEnd && !canReceive;
                    frontLimit = Math.Max(0, flight.Distance - spacing);
                    index++;
                }
            }
            CompleteDrainedLines();
        }
    }
}
