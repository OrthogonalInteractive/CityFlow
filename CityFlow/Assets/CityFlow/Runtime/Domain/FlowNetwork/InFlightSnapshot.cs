#nullable enable

namespace CityFlow.Domain.FlowNetwork
{
    public readonly struct InFlightSnapshot
    {
        public Flow Flow { get; }
        public double Distance { get; }
        internal InFlightSnapshot(Flow flow, double distance) { Flow = flow; Distance = distance; }
    }
}
