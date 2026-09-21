#nullable enable

using System;
using CityFlow.Domain.FlowNetwork;

namespace CityFlow.Infrastructure.Configuration
{
    public sealed class SystemRandomSource : IRandomSource
    {
        private readonly Random random;
        public SystemRandomSource(int seed) => random = new Random(seed);
        public int NextIndex(int exclusiveMax)
        {
            if (exclusiveMax <= 0) throw new ArgumentOutOfRangeException(nameof(exclusiveMax));
            return random.Next(exclusiveMax);
        }
    }
}
