#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using CityFlow.Domain.Spatial;

namespace CityFlow.Domain.FlowNetwork
{
    public sealed class NodeSnapshot
    {
        public bool IsBufferFull => BufferCapacity.HasValue && Buffer.Count >= BufferCapacity.Value;
        public bool IsInputStopped => Definition.Kind == NodeKind.Relay && IsBufferFull;
        public int? BufferCapacity { get; }
        public double OverloadSeconds { get; }
        public long GeneratedCount { get; }
        public FlowColor? LastGeneratedColor { get; }
        public NodeDefinition Definition { get; }
        public int IncomingUsed { get; }
        public int OutgoingUsed { get; }
        public IReadOnlyList<Flow> Buffer { get; }
        internal NodeSnapshot(NodeDefinition definition, int incoming, int outgoing, IEnumerable<Flow> buffer, int? bufferCapacity, double overloadSeconds,
            long generatedCount, FlowColor? lastGeneratedColor)
        { Definition = definition; IncomingUsed = incoming; OutgoingUsed = outgoing; Buffer = Array.AsReadOnly(buffer.ToArray());
          BufferCapacity = bufferCapacity; OverloadSeconds = overloadSeconds;
          GeneratedCount = generatedCount; LastGeneratedColor = lastGeneratedColor; }
    }
}
