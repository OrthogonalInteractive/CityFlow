#nullable enable

using System;
using CityFlow.Domain.Spatial;
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
        public CitySceneEntryPoint(StageDefinition stage, FlowNetwork network) { this.stage = stage; this.network = network; }
        public void Start()
        {
            city = new GameObject("Validation City");
            city.AddComponent<ValidationCityView>().Initialize(stage, network);
        }
        public void Dispose() { if (city != null) UnityEngine.Object.Destroy(city); }
    }
}
