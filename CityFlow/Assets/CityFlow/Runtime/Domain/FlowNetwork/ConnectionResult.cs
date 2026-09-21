#nullable enable

namespace CityFlow.Domain.FlowNetwork
{
    public readonly struct ConnectionResult
    {
        public ConnectionFailure Failure { get; }
        public int? LineId { get; }
        public bool Succeeded => Failure == ConnectionFailure.None;
        internal ConnectionResult(ConnectionFailure failure, int? lineId = null)
        { Failure = failure; LineId = lineId; }
    }
}
