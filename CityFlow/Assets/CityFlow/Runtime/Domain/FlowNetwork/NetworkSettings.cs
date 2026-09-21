#nullable enable

using System;

namespace CityFlow.Domain.FlowNetwork
{
    public sealed class NetworkSettings
    {
        public int MaxBuffer { get; }
        public int MaxInFlight { get; }
        public float FlowSpeed { get; }
        public float Clearance { get; }
        public NetworkSettings(int maxBuffer, int maxInFlight, float flowSpeed, float clearance)
        {
            if (maxBuffer <= 0 || maxInFlight <= 0 || float.IsNaN(flowSpeed) || float.IsInfinity(flowSpeed) ||
                flowSpeed <= 0 || float.IsNaN(clearance) || float.IsInfinity(clearance) || clearance < 0)
                throw new ArgumentException("Network settings must be finite and within their valid ranges.");
            MaxBuffer = maxBuffer; MaxInFlight = maxInFlight; FlowSpeed = flowSpeed; Clearance = clearance;
        }
    }
}
