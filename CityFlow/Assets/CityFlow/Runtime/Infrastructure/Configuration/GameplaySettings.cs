#nullable enable

using System;
using UnityEngine;

namespace CityFlow.Infrastructure.Configuration
{
    [CreateAssetMenu(menuName = "City Flow/Gameplay Settings")]
    public sealed class GameplaySettings : ScriptableObject
    {
        [Tooltip("Provisional common buffer threshold [FLOW].")]
        public int MaxBuffer = 50;
        [Tooltip("Provisional common capacity per line [FLOW].")]
        public int MaxInFlight = 10;
        [Tooltip("Provisional movement speed [m/s].")]
        public float FlowSpeed = 20;
        [Tooltip("Provisional Source overload grace [s]; used by a later step.")]
        public float OverloadGrace = 5;
        [Tooltip("Provisional footprint clearance [m].")]
        public float Clearance = 0.5f;
        public CityFlow.Domain.FlowNetwork.NetworkSettings LoadNetworkSettings()
        {
            Validate();
            return new CityFlow.Domain.FlowNetwork.NetworkSettings(MaxBuffer, MaxInFlight, FlowSpeed, Clearance);
        }
        public void Validate()
        {
            if (MaxBuffer <= 0 || MaxInFlight <= 0 || !Positive(FlowSpeed) || !Positive(OverloadGrace) ||
                float.IsNaN(Clearance) || float.IsInfinity(Clearance) || Clearance < 0)
                throw new ArgumentException("Gameplay settings require positive finite capacities, speed and grace; clearance must be nonnegative.");
        }
        private static bool Positive(float value) => value > 0 && !float.IsInfinity(value);
    }
}
