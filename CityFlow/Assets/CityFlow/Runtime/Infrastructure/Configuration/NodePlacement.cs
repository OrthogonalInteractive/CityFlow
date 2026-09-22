#nullable enable

using System;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Domain.Spatial;
using UnityEngine;

namespace CityFlow.Infrastructure.Configuration
{
    [Serializable]
    public abstract class NodePlacement
    {
        public string Id = string.Empty;
        public Vector3 Position;
        public abstract NodeKind Kind { get; }
        public abstract NodeDefinition ToDefinition();
    }
}
