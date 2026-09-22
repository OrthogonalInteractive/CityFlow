#nullable enable

namespace CityFlow.Domain.FlowNetwork
{
    public enum ConnectionFailure { None, UnknownSource, UnknownDestination, SelfConnection,
        DuplicateDirection, OutgoingLimit, IncomingLimit, InvalidRoute, LineUnavailable,
        InputNotSupported, OutputNotSupported }
}
