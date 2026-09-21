#nullable enable

namespace CityFlow.Domain.FlowNetwork
{
    public interface IRandomSource
    {
        // Return a uniformly distributed index in [0, exclusiveMax).
        int NextIndex(int exclusiveMax);
    }
}
