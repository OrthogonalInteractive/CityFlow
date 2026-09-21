#nullable enable

namespace CityFlow.Domain.FlowNetwork
{
    public sealed class Flow
    {
        public long Id { get; }
        public FlowColor Color { get; }
        internal Flow(long id, FlowColor color) { Id = id; Color = color; }
    }
}
