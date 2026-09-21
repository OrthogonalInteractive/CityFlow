#nullable enable

using CityFlow.Application.Connections;
using CityFlow.Application.Routing;
using CityFlow.Domain.FlowNetwork;

namespace CityFlow.Presentation.UI
{
    public static class ConnectionReadout
    {
        public static string Reason(ConnectionFailure failure) => failure switch
        {
            ConnectionFailure.None => "Slots available / route unchecked",
            ConnectionFailure.DuplicateDirection => "This direction already exists",
            ConnectionFailure.OutgoingLimit => "Source OUT slots are full",
            ConnectionFailure.IncomingLimit => "Destination IN slots are full",
            ConnectionFailure.SelfConnection => "Choose two different Nodes",
            ConnectionFailure.LineUnavailable => "Line removed or another change is pending",
            ConnectionFailure.InvalidRoute => "A valid Ground route is required",
            _ => "Node is unavailable"
        };
        public static string Candidate(ConnectionCandidate candidate, LinePreviewState? preview = null)
        {
            var node = candidate.Node; var definition = node.Definition;
            string color = definition.SinkColor.HasValue ? $" / {definition.SinkColor.Value.ToString().ToUpperInvariant()}" : "";
            string status = Reason(candidate.Failure);
            if (candidate.Failure == ConnectionFailure.None && preview?.DestinationId == definition.Id)
                status = preview.Geometry.IsValid ? "Route preview ready / not connected" : "No valid route / see Preview reason";
            return $"{definition.Id} / {definition.Kind.ToString().ToUpperInvariant()}{color}\n" +
                $"GROUND DISTANCE {candidate.Distance:0.0} m / {candidate.Band.ToString().ToUpperInvariant()}\n" +
                $"SOURCE OUT {candidate.SourceOutgoingUsed}/{candidate.SourceOutgoingLimit}  →  TARGET IN {node.IncomingUsed}/{definition.MaxIncoming}\n" +
                $"TARGET OUT {node.OutgoingUsed}/{definition.MaxOutgoing}\n{status}";
        }
    }
}
