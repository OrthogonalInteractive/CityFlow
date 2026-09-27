#nullable enable
using System;
using UnityEngine;

namespace CityFlow.Presentation.Audio
{
    [CreateAssetMenu(menuName = "City Flow/Gameplay Audio")]
    public sealed class GameplayAudioSettings : ScriptableObject
    {
        [SerializeField] private AudioClip? wave, source, sink;
        [SerializeField, Range(0, 1)] private float masterVolume = 0.8f;
        public AudioClip Wave => wave != null ? wave : throw new InvalidOperationException("Wave SE is missing.");
        public AudioClip Source => source != null ? source : throw new InvalidOperationException("Source SE is missing.");
        public AudioClip Sink => sink != null ? sink : throw new InvalidOperationException("Sink SE is missing.");
        public float MasterVolume => masterVolume;
        public void Configure(AudioClip waveClip, AudioClip sourceClip, AudioClip sinkClip)
        { wave = waveClip; source = sourceClip; sink = sinkClip; Validate(); }
        public void Validate() { _ = Wave; _ = Source; _ = Sink; }
    }
}
