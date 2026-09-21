#nullable enable

using System;
using System.Linq;
using CityFlow.Domain.FlowNetwork;

namespace CityFlow.Presentation.Overview
{
    public static class OverviewReadout
    {
        public static string Describe(OverviewTarget target, NetworkSnapshot state, NetworkSettings settings)
        {
            NodeSnapshot? node = state.Nodes.FirstOrDefault(n=>n.Definition.Id == target.NodeId);
            if (node != null)
            {
                string colors = string.Join("  ", node.Buffer.GroupBy(f=>f.Color).OrderBy(g=>g.Key).Select(g=>$"{g.Key}: {g.Count()}"));
                string sink = node.Definition.SinkColor.HasValue ? $" · {node.Definition.SinkColor} SINK" : "";
                string source = node.Definition.Kind == NodeKind.Source ? $"\nGENERATE {node.Definition.GenerationInterval:0.00}s · GRACE {Math.Max(0,settings.OverloadGrace-node.OverloadSeconds):0.0}s" : "";
                string incoming = string.Join(", ", state.Lines.Where(l=>l.DestinationId==node.Definition.Id && l.InFlight.Any(f=>f.IsStopped)).Select(l=>l.SourceId));
                string outgoing = string.Join(", ", state.Lines.Where(l=>l.SourceId==node.Definition.Id).Select(l=>l.DestinationId));
                return $"{node.Definition.Id} · {node.Definition.Kind.ToString().ToUpperInvariant()}{sink}\nBUFFER {node.Buffer.Count}/{settings.MaxBuffer} ({100d*node.Buffer.Count/settings.MaxBuffer:0}%)\n{(colors.Length==0 ? "No waiting FLOW" : colors)}\nIN {node.IncomingUsed}/{node.Definition.MaxIncoming} · OUT {node.OutgoingUsed}/{node.Definition.MaxOutgoing}\nINPUT {(node.IsInputStopped ? "STOPPED" : "OPEN")}{source}\nWAITING FROM {(incoming.Length==0 ? "—" : incoming)}\nOUTPUT TO {(outgoing.Length==0 ? "—" : outgoing)}";
            }
            LineSnapshot? line = state.Lines.FirstOrDefault(l=>l.Id == target.LineId);
            if (line == null) return "Hover a Node or Line for details.\nClick to select · F to focus · Home for city view.";
            int stopped = line.InFlight.Count(f=>f.IsStopped);
            double travel = line.Route.Length / settings.FlowSpeed;
            return $"{line.SourceId} → {line.DestinationId}\n{line.Status.ToString().ToUpperInvariant()}\nLENGTH {line.Route.Length:0.0} m · TRAVEL {travel:0.00} s\nIN-FLIGHT {line.InFlight.Count}/{line.Capacity} ({100d*line.InFlight.Count/line.Capacity:0}%)\nMOVING {line.InFlight.Count-stopped} · STOPPED {stopped}\nTHROUGHPUT {line.Capacity/travel:0.00} FLOW/s (unblocked)\n{(stopped>0 ? $"WAITING · {line.DestinationId} Buffer space" : line.InFlight.Count==line.Capacity ? "FULL · waiting for capacity release" : "RUNNING")}";
        }
    }
}
