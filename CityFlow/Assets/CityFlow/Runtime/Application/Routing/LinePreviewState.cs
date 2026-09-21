#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using CityFlow.Domain.FlowNetwork;
using UnityEngine;

namespace CityFlow.Application.Routing
{
    public sealed class LinePreviewState
    {
        public string SourceId { get; }
        public string DestinationId { get; }
        public IReadOnlyList<Vector3> Points { get; }
        public GroundRouteResult Geometry { get; }
        public ConnectionFailure ConnectionFailure { get; }
        public bool CanConfirm => Geometry.IsValid && ConnectionFailure == ConnectionFailure.None;
        public int OutgoingAfter { get; }
        public int IncomingAfter { get; }
        public int OutgoingLimit { get; }
        public int IncomingLimit { get; }
        public int Capacity { get; }
        public double Length => Geometry.Route?.Length ?? 0;
        public double TravelTime { get; }
        public double Throughput => TravelTime > 0 ? Capacity / TravelTime : 0;
        public LinePreviewState(string source, string destination, IReadOnlyList<Vector3> points, GroundRouteResult geometry,
            ConnectionFailure connectionFailure, NodeSnapshot? from, NodeSnapshot? to, NetworkSettings settings)
        {
            SourceId = source; DestinationId = destination; Points = Array.AsReadOnly(points.ToArray());
            Geometry = geometry; ConnectionFailure = connectionFailure;
            OutgoingAfter = (from?.OutgoingUsed ?? 0) + 1; IncomingAfter = (to?.IncomingUsed ?? 0) + 1;
            OutgoingLimit = from?.Definition.MaxOutgoing ?? 0; IncomingLimit = to?.Definition.MaxIncoming ?? 0;
            Capacity = settings.MaxInFlight; TravelTime = Length / settings.FlowSpeed;
        }
    }
}
