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
                string connectedFrom = string.Join(", ", state.Lines.Where(l => l.DestinationId == node.Definition.Id).Select(l => l.SourceId));
                if (!node.BufferCapacity.HasValue)
                    return new OverviewDetailText(title, "CONSUME MATCHING FLOW ON ARRIVAL", $"IN {node.IncomingUsed}/{node.Definition.MaxIncoming}",
                        colors: node.Definition.SinkColor?.ToString() ?? "",
                        connections: $"CONNECTED FROM: {(connectedFrom.Length == 0 ? "None" : connectedFrom)}");
                string colors = string.Join("  ", node.Buffer.GroupBy(f => f.Color).OrderBy(g => g.Key).Select(g => $"{g.Key}: {g.Count()}"));
                string activity = "", warning = node.IsInputStopped ? "INPUT STOPPED · BUFFER FULL" : "";
                if (node.Definition is SourceNodeDefinition source)
                {
                    string color = node.LastGeneratedColor.HasValue ? " · " + node.LastGeneratedColor.Value.ToString().ToUpperInvariant() : "";
                    activity = $"GENERATE {source.GenerationInterval * intervalScale:0.00}s\nGENERATED {node.GeneratedCount}{color}\n{Math.Max(0, node.BufferCapacity.Value - node.Buffer.Count)} FREE";
                    warning = node.IsBufferFull ? $"{Math.Max(0, settings.OverloadGrace - node.OverloadSeconds):0.0}s TO GAME OVER" :
                        node.Buffer.Count >= node.BufferCapacity.Value * 0.8 ? "BUFFER NEARLY FULL · ADD AN EXIT" : "";
                }
                string incoming = string.Join(", ", state.Lines.Where(l => l.DestinationId == node.Definition.Id && l.InFlight.Any(f => f.IsStopped)).Select(l => l.SourceId));
                string outgoing = string.Join(", ", state.Lines.Where(l => l.SourceId == node.Definition.Id).Select(l => l.DestinationId));
                bool isSource = node.Definition.Kind == NodeKind.Source;
                string metrics = $"OUT {node.OutgoingUsed}/{node.Definition.MaxOutgoing}";
                string connections = $"CONNECTED TO: {(outgoing.Length == 0 ? "None" : outgoing)}";
                if (!isSource)
                {
                    metrics = $"IN {node.IncomingUsed}/{node.Definition.MaxIncoming} · " + metrics;
                    connections = $"INCOMING BLOCKED: {(incoming.Length == 0 ? "None" : incoming)}\n" + connections;
                }
                return new OverviewDetailText(title,
                    $"BUFFER {node.Buffer.Count}/{node.BufferCapacity.Value} ({100d * node.Buffer.Count / node.BufferCapacity.Value:0}%)",
                    metrics, colors.Length == 0 ? "No waiting FLOW" : colors,
                    isSource ? "" : $"INPUT {(node.IsInputStopped ? "STOPPED" : "OPEN")}", connections, activity, warning);
            }
            LineSnapshot? line = state.Lines.FirstOrDefault(l => l.Id == target.LineId);
            if (line == null) return new OverviewDetailText("Hover a Node or Line for details.");
            int stopped = line.InFlight.Count(f => f.IsStopped);
            double travel = line.Route.Length / settings.FlowSpeed;
            string status = line.Status == LineStatus.DeletePending ? "DELETING · DASHED" :
                line.Status == LineStatus.RouteChangePending ? "ROUTE CHANGE PENDING · DOUBLE LINE" : "RUNNING";
            return new OverviewDetailText($"{line.SourceId} → {line.DestinationId}",
                $"IN-FLIGHT {line.InFlight.Count}/{line.Capacity} ({100d * line.InFlight.Count / line.Capacity:0}%)",
                $"LENGTH {line.Route.Length:0.0} m · TRAVEL {travel:0.00} s\nMOVING {line.InFlight.Count - stopped} · STOPPED {stopped}",
                status: status, activity: $"THROUGHPUT {line.Capacity / travel:0.00} FLOW/s (unblocked)",
                warning: stopped > 0 ? $"WAITING · {line.DestinationId} Buffer space" : line.InFlight.Count == line.Capacity ? "FULL · waiting for capacity release" : "");
        }
    }
}
