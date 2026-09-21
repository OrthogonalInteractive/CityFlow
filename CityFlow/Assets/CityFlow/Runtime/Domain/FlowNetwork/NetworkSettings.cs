#nullable enable

using System;

namespace CityFlow.Domain.FlowNetwork
{
    public sealed class NetworkSettings
    {
        public int SourceBufferCapacity { get; }
        public int RelayBufferCapacity { get; }
        public int? BufferCapacity(NodeKind kind) => kind == NodeKind.Sink ? (int?)null :
            kind == NodeKind.Source ? SourceBufferCapacity : RelayBufferCapacity;
        public int MaxInFlight { get; }
        public float FlowSpeed { get; }
        public double OverloadGrace { get; }
        public float Clearance { get; }
        public NetworkSettings(int sourceBufferCapacity, int relayBufferCapacity, int maxInFlight, float flowSpeed, float clearance, double overloadGrace = 5)
        {
            if (sourceBufferCapacity <= 0 || relayBufferCapacity <= 0 || maxInFlight <= 0 || float.IsNaN(flowSpeed) || float.IsInfinity(flowSpeed) ||
                flowSpeed <= 0 || float.IsNaN(clearance) || float.IsInfinity(clearance) || clearance < 0 || double.IsNaN(overloadGrace) || double.IsInfinity(overloadGrace) || overloadGrace <= 0)
                throw new ArgumentException("Network settings must be finite and within their valid ranges.");
            SourceBufferCapacity = sourceBufferCapacity; RelayBufferCapacity = relayBufferCapacity; MaxInFlight = maxInFlight; FlowSpeed = flowSpeed; Clearance = clearance; OverloadGrace = overloadGrace;
        }
    }
}
