#nullable enable

namespace CityFlow.Domain.Spatial
{
    public enum RouteFailure { None, InvalidPoints, GroundHeight, OutsideArea, Obstacle, SearchFailed, EndpointMismatch, HeightRange, SlopedSegment, VerticalAtRelayOnly, RelayHeightLimit }
}
