#nullable enable

using CityFlow.Application.UseCases;
using UnityEngine;

namespace CityFlow.Presentation.Rendering
{
    public sealed class SimulationDriver : MonoBehaviour
    {
        private FlowSimulation? simulation;
        public void Initialize(FlowSimulation value) => simulation = value;
        private void Update() => simulation?.Tick(Time.unscaledDeltaTime);
    }
}
