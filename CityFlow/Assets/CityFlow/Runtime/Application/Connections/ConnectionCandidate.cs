#nullable enable

using CityFlow.Domain.FlowNetwork;

namespace CityFlow.Application.Connections
{
    public sealed class ConnectionCandidate
    {
        public NodeSnapshot Node { get; }
        public float Distance { get; }
        public DistanceBand Band { get; }
        public int SourceOutgoingUsed { get; }
        public int SourceOutgoingLimit { get; }
        public ConnectionFailure Failure { get; }
        public ConnectionCandidate(NodeSnapshot node, float distance, DistanceBand band, NodeSnapshot source, ConnectionFailure failure)
        {
            Node = node; Distance = distance; Band = band; Failure = failure;
            SourceOutgoingUsed = source.OutgoingUsed; SourceOutgoingLimit = source.Definition.MaxOutgoing;
        }
    }
}
