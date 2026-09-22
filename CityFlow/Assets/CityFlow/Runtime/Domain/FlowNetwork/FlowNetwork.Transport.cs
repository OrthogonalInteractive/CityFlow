#nullable enable

using System;
using System.Linq;

namespace CityFlow.Domain.FlowNetwork
{
    public sealed partial class FlowNetwork
    {
        public void RouteWaitingFlows()
        {
            InvalidateSnapshot();
            foreach (var definition in NodeDefinitions)
            {
                NodeState node = nodes[definition.Id];
                for (int index = 0; index < node.Buffer.Count;)
                {
                    Flow flow = node.Buffer[index];
                    LineState? selected = RoutesFor(flow.Color)[node]
                        .FirstOrDefault(line => line.InFlight.Count < Settings.MaxInFlight);
                    if (selected == null) { index++; continue; }
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
                    bool canReceive = matchingSink || (line.Destination.Definition.Kind == NodeKind.Relay &&
                        capacity.HasValue && line.Destination.Buffer.Count < capacity.Value);
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
