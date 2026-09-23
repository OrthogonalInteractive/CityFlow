#nullable enable

using System;
using CityFlow.Domain.Spatial;

namespace CityFlow.Application.Routing
{
    public sealed class LineRouteResult
    {
        public LineRoute? Route { get; }
        public RouteFailure Failure { get; }
        public int InvalidSegment { get; }
        public bool IsValid => Route != null && Failure == RouteFailure.None;
        public LineRouteResult(LineRoute route) { Route = route ?? throw new ArgumentNullException(nameof(route)); InvalidSegment = -1; }
        public LineRouteResult(RouteFailure failure, int invalidSegment = -1)
        {
            if (failure == RouteFailure.None) throw new ArgumentException("A failed route needs a reason.");
            Failure = failure; InvalidSegment = invalidSegment;
        }
    }
}
