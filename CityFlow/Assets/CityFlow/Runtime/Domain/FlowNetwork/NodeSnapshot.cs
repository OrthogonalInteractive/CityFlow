#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using CityFlow.Domain.Spatial;

namespace CityFlow.Domain.FlowNetwork
{
    public sealed class NodeSnapshot
    {
        public NodeDefinition Definition { get; }
        public int IncomingUsed { get; }
        public int OutgoingUsed { get; }
        public IReadOnlyList<Flow> Buffer { get; }
        internal NodeSnapshot(NodeDefinition definition, int incoming, int outgoing, IEnumerable<Flow> buffer)
        { Definition = definition; IncomingUsed = incoming; OutgoingUsed = outgoing; Buffer = Array.AsReadOnly(buffer.ToArray()); }
    }
}
