#nullable enable

using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace CityFlow.Composition
{
    [DisallowMultipleComponent]
    public sealed class CityFlowLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            // Register use cases and adapters here as each feature is implemented.
        }
    }
}
