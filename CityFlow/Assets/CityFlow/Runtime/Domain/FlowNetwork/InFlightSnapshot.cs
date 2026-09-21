#nullable enable

namespace CityFlow.Domain.FlowNetwork
{
    public readonly struct InFlightSnapshot
    {
        public bool IsStopped { get; }
        public Flow Flow { get; }
        public double Distance { get; }
        internal InFlightSnapshot(Flow flow, double distance, bool isStopped) { Flow = flow; Distance = distance; IsStopped = isStopped; }
    }
}
