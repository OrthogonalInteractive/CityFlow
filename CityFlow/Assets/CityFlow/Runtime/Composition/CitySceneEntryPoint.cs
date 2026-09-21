#nullable enable

using System;
using CityFlow.Domain.Spatial;
using CityFlow.Application.UseCases;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Presentation.Rendering;
using UnityEngine;
using VContainer.Unity;

namespace CityFlow.Composition
{
    public sealed class CitySceneEntryPoint : IStartable, IDisposable
    {
        private readonly StageDefinition stage;
        private GameObject? city;
        private readonly FlowNetwork network;
        private readonly FlowSimulation simulation;
        public CitySceneEntryPoint(StageDefinition stage, FlowNetwork network, FlowSimulation simulation)
        { this.stage = stage; this.network = network; this.simulation = simulation; }
        public void Start()
        {
            city = new GameObject("Validation City");
            city.AddComponent<ValidationCityView>().Initialize(stage, network, simulation);
            city.AddComponent<SimulationDriver>().Initialize(simulation);
        }
        public void Dispose() { if (city != null) UnityEngine.Object.Destroy(city); }
    }
}
