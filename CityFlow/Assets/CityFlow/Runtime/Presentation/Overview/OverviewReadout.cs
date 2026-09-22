#nullable enable

using System;
using System.Linq;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Domain.Spatial;

namespace CityFlow.Presentation.Overview
{
    public static class OverviewReadout
    {
        public static string Describe(OverviewTarget target, NetworkSnapshot state, NetworkSettings settings, double intervalScale = 1) =>
            Read(target, state, settings, intervalScale).ToString();

        public static OverviewDetailText Read(OverviewTarget target, NetworkSnapshot state, NetworkSettings settings, double intervalScale = 1)
        {
            NodeSnapshot? node = state.Nodes.FirstOrDefault(n => n.Definition.Id == target.NodeId);
            if (node != null)
            {
                string title = $"{node.Definition.Id} · {node.Definition.Kind.ToString().ToUpperInvariant()}";
                if (!node.BufferCapacity.HasValue)
                    return new OverviewDetailText(title, "CONSUME MATCHING FLOW ON ARRIVAL", $"IN {node.IncomingUsed}/{node.Definition.MaxIncoming}",
                        colors: node.Definition.SinkColor?.ToString() ?? "");
                string colors = string.Join("  ", node.Buffer.GroupBy(f => f.Color).OrderBy(g => g.Key).Select(g => $"{g.Key}: {g.Count()}"));
                string activity = "", warning = node.IsInputStopped ? "INPUT STOPPED · BUFFER FULL" : "";
                if (node.Definition is SourceNodeDefinition source)
                {
                    string color = node.LastGeneratedColor.HasValue ? " · " + node.LastGeneratedColor.Value.ToString().ToUpperInvariant() : "";
                    activity = $"GENERATE {source.GenerationInterval * intervalScale:0.00}s\nGENERATED {node.GeneratedCount}{color}\n{Math.Max(0, node.BufferCapacity.Value - node.Buffer.Count)} FREE";
                    warning = node.IsInputStopped ? $"{Math.Max(0, settings.OverloadGrace - node.OverloadSeconds):0.0}s TO GAME OVER" :
                        node.Buffer.Count >= node.BufferCapacity.Value * 0.8 ? "BUFFER NEARLY FULL · ADD AN EXIT" : "";
                }
                string incoming = string.Join(", ", state.Lines.Where(l => l.DestinationId == node.Definition.Id && l.InFlight.Any(f => f.IsStopped)).Select(l => l.SourceId));
                string outgoing = string.Join(", ", state.Lines.Where(l => l.SourceId == node.Definition.Id).Select(l => l.DestinationId));
                return new OverviewDetailText(title,
                    $"BUFFER {node.Buffer.Count}/{node.BufferCapacity.Value} ({100d * node.Buffer.Count / node.BufferCapacity.Value:0}%)",
                    $"IN {node.IncomingUsed}/{node.Definition.MaxIncoming} · OUT {node.OutgoingUsed}/{node.Definition.MaxOutgoing}",
                    colors.Length == 0 ? "No waiting FLOW" : colors, $"INPUT {(node.IsInputStopped ? "STOPPED" : "OPEN")}",
                    $"INCOMING BLOCKED: {(incoming.Length == 0 ? "None" : incoming)}\nCONNECTED TO: {(outgoing.Length == 0 ? "None" : outgoing)}", activity, warning);
            }
            LineSnapshot? line = state.Lines.FirstOrDefault(l => l.Id == target.LineId);
            if (line == null) return new OverviewDetailText("Hover a Node or Line for details.");
            int stopped = line.InFlight.Count(f => f.IsStopped);
            double travel = line.Route.Length / settings.FlowSpeed;
            string status = line.Status == LineStatus.DeletePending ? "DELETION PENDING · DASHED" :
                line.Status == LineStatus.RouteChangePending ? "ROUTE CHANGE PENDING · DOUBLE LINE" : "RUNNING";
            return new OverviewDetailText($"{line.SourceId} → {line.DestinationId}",
                $"IN-FLIGHT {line.InFlight.Count}/{line.Capacity} ({100d * line.InFlight.Count / line.Capacity:0}%)",
                $"LENGTH {line.Route.Length:0.0} m · TRAVEL {travel:0.00} s\nMOVING {line.InFlight.Count - stopped} · STOPPED {stopped}",
                status: status, activity: $"THROUGHPUT {line.Capacity / travel:0.00} FLOW/s (unblocked)",
                warning: stopped > 0 ? $"WAITING · {line.DestinationId} Buffer space" : line.InFlight.Count == line.Capacity ? "FULL · waiting for capacity release" : "");
        }
    }
}
