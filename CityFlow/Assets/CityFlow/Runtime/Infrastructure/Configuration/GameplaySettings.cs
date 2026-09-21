#nullable enable

using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace CityFlow.Infrastructure.Configuration
{
    [CreateAssetMenu(menuName = "City Flow/Gameplay Settings")]
    public sealed class GameplaySettings : ScriptableObject
    {
        [FormerlySerializedAs("MaxBuffer")]
        [Tooltip("Basic Source buffer capacity [FLOW].")]
        public int SourceBufferCapacity = 10;
        [Tooltip("Basic Relay buffer capacity [FLOW]. Sinks have no buffer.")]
        public int RelayBufferCapacity = 5;
        [Tooltip("Repeatable random seed for the validation scene.")]
        public int RandomSeed = 1337;
        [Tooltip("Provisional common capacity per line [FLOW].")]
        public int MaxInFlight = 10;
        [Tooltip("Provisional movement speed [m/s].")]
        public float FlowSpeed = 8;
        [Tooltip("Provisional Source overload grace [s].")]
        public float OverloadGrace = 5;
        [Tooltip("Provisional footprint clearance [m].")]
        public float Clearance = 0.5f;
        public CityFlow.Domain.FlowNetwork.NetworkSettings LoadNetworkSettings()
        {
            Validate();
            return new CityFlow.Domain.FlowNetwork.NetworkSettings(SourceBufferCapacity, RelayBufferCapacity, MaxInFlight, FlowSpeed, Clearance, OverloadGrace);
        }
        public void Validate()
        {
            if (SourceBufferCapacity <= 0 || RelayBufferCapacity <= 0 || MaxInFlight <= 0 || !Positive(FlowSpeed) || !Positive(OverloadGrace) ||
                float.IsNaN(Clearance) || float.IsInfinity(Clearance) || Clearance < 0)
                throw new ArgumentException("Gameplay settings require positive finite capacities, speed and grace; clearance must be nonnegative.");
        }
        private static bool Positive(float value) => value > 0 && !float.IsInfinity(value);
    }
}
