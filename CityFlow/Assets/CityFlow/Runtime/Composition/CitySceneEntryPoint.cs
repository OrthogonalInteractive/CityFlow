#nullable enable

using System;
using CityFlow.Domain.Spatial;
using CityFlow.Presentation.Rendering;
using UnityEngine;
using VContainer.Unity;

namespace CityFlow.Composition
{
    public sealed class CitySceneEntryPoint : IStartable, IDisposable
    {
        private readonly StageDefinition stage;
        private GameObject? city;
        public CitySceneEntryPoint(StageDefinition stage) => this.stage = stage;
        public void Start()
        {
            city = new GameObject("Validation City");
            city.AddComponent<ValidationCityView>().Initialize(stage);
        }
        public void Dispose() { if (city != null) UnityEngine.Object.Destroy(city); }
    }
}
