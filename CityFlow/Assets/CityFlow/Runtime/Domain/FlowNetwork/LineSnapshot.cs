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
        public LineStatus Status { get; }
        public LineRoute? PendingRoute { get; }
        public IReadOnlyList<InFlightSnapshot> InFlight { get; }
        internal LineSnapshot(int id, string sourceId, string destinationId, LineRoute route, int capacity,
            IEnumerable<InFlightSnapshot> inFlight, LineStatus status = LineStatus.Running, LineRoute? pendingRoute = null)
        { Id = id; SourceId = sourceId; DestinationId = destinationId; Route = route;
            Status = status; PendingRoute = pendingRoute; Capacity = capacity; InFlight = Array.AsReadOnly(inFlight.ToArray()); }
    }
}
