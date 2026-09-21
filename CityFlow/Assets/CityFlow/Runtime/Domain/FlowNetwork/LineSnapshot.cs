#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using CityFlow.Domain.Spatial;

namespace CityFlow.Domain.FlowNetwork
{
    public sealed class LineSnapshot
    {
        public int Id { get; }
        public string SourceId { get; }
        public string DestinationId { get; }
        public LineRoute Route { get; }
        public int Capacity { get; }
        public IReadOnlyList<InFlightSnapshot> InFlight { get; }
        internal LineSnapshot(int id, string sourceId, string destinationId, LineRoute route, int capacity,
            IEnumerable<InFlightSnapshot> inFlight)
        { Id = id; SourceId = sourceId; DestinationId = destinationId; Route = route;
            Capacity = capacity; InFlight = Array.AsReadOnly(inFlight.ToArray()); }
    }
}
