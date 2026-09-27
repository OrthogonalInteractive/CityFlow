#nullable enable

using CityFlow.Application.UseCases;
using UnityEngine;

namespace CityFlow.Presentation.Rendering
{
    public sealed class SimulationDriver : MonoBehaviour
    {
        private FlowSimulation? simulation;
        public void Initialize(FlowSimulation value) => simulation = value;
        // Unscaled delta can include suspended wall time on the first frame after an Editor pause.
        private void Update() => simulation?.Tick(Time.deltaTime);
    }
}
