#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace CityFlow.Domain.FlowNetwork
{
    public sealed class NetworkSnapshot
    {
        public IReadOnlyList<NodeSnapshot> Nodes { get; }
        public IReadOnlyList<LineSnapshot> Lines { get; }
        public long GeneratedCount { get; }
        public long DeliveredCount { get; }
        internal NetworkSnapshot(IEnumerable<NodeSnapshot> nodes, IEnumerable<LineSnapshot> lines, long generated, long delivered)
        { Nodes = Array.AsReadOnly(nodes.ToArray()); Lines = Array.AsReadOnly(lines.ToArray());
            GeneratedCount = generated; DeliveredCount = delivered; }
    }
}
