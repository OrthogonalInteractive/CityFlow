#nullable enable
using System;
using System.Collections.Generic;
using CityFlow.Application.UseCases;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Domain.Spatial;
using UnityEngine;

namespace CityFlow.Presentation.Audio
{
    public sealed class GameplayAudioView : MonoBehaviour
    {
        private const int VoiceLimit = 8;
        private const double NodeCooldownSeconds = 0.08;
        private FlowNetwork? network;
        private FlowSimulation? simulation;
        private Camera? viewCamera;
        private GameplayAudioSettings? settings;
        private AudioSource? waveVoice;
        private readonly List<AudioSource> nodeVoices = new();
        private readonly Dictionary<string, (long Departed, long Delivered)> observed = new();
        private readonly Dictionary<string, double> lastPlayed = new();
        private readonly List<(string Id, AudioClip Clip, Vector3 Position, float Gain)> candidates = new();
        private int lastWave;
        private bool initialWavePending, suspended, applicationPaused;

        public void Initialize(FlowNetwork network, FlowSimulation simulation, Camera camera, GameplayAudioSettings settings)
        {
            if (this.network != null) throw new InvalidOperationException("Gameplay audio is already initialized.");
            settings.Validate();
            this.network = network; this.simulation = simulation; viewCamera = camera; this.settings = settings;
            waveVoice = CreateVoice("Wave SE");
            for (int i = 0; i < VoiceLimit; i++) nodeVoices.Add(CreateVoice("Node SE " + (i + 1)));
            CaptureBaseline();
            initialWavePending = simulation.ElapsedSeconds < FlowSimulation.StepSeconds;
        }

        private AudioSource CreateVoice(string voiceName)
        {
            var child = new GameObject(voiceName);
            child.transform.SetParent(transform, false);
            var voice = child.AddComponent<AudioSource>();
            voice.playOnAwake = false; voice.loop = false; voice.spatialBlend = 0; voice.dopplerLevel = 0;
            return voice;
        }

        private void LateUpdate() => Refresh();
        public void Refresh()
        {
            if (network == null || simulation == null || viewCamera == null || settings == null || waveVoice == null) return;
            if (!isActiveAndEnabled || suspended || applicationPaused || simulation.IsPaused || network.IsGameOver)
            {
                StopVoices(); CaptureBaseline(); initialWavePending = false;
                return;
            }
            foreach (var voice in nodeVoices)
                if (voice.clip != null && !voice.isPlaying) voice.clip = null;
            if (waveVoice.clip != null && !waveVoice.isPlaying) waveVoice.clip = null;
            if (initialWavePending || simulation.Wave > lastWave)
            {
                waveVoice.clip = settings.Wave; waveVoice.volume = settings.MasterVolume * 0.85f; waveVoice.Play();
            }
            initialWavePending = false; lastWave = simulation.Wave;
            candidates.Clear();
            foreach (NodeSnapshot node in network.Snapshot().Nodes)
            {
                string id = node.Definition.Id;
                observed.TryGetValue(id, out var previous);
                observed[id] = (node.DepartedCount, node.DeliveredCount);
                AudioClip? clip = node.Definition.Kind == NodeKind.Source && node.DepartedCount > previous.Departed ? settings.Source
                    : node.Definition.Kind == NodeKind.Sink && node.DeliveredCount > previous.Delivered ? settings.Sink : null;
                if (clip == null || (lastPlayed.TryGetValue(id, out double at) && simulation.ElapsedSeconds - at < NodeCooldownSeconds)) continue;
                Vector3 position = node.Definition.Position + Vector3.up * 1.4f;
                float gain = GameplaySoundMix.At(viewCamera, position).Volume;
                if (gain > 0.001f) candidates.Add((id, clip, position, gain));
            }
            candidates.Sort((a, b) => b.Gain.CompareTo(a.Gain));
            foreach (var cue in candidates)
            {
                AudioSource? selected = null;
                float quietest = cue.Gain;
                foreach (var voice in nodeVoices)
                {
                    if (voice.clip == null) { selected = voice; break; }
                    float gain = GameplaySoundMix.At(viewCamera, voice.transform.position).Volume;
                    if (gain < quietest) { quietest = gain; selected = voice; }
                }
                // Dropped cues are already observed, so they cannot become a delayed backlog.
                if (selected == null) continue;
                selected.Stop(); selected.clip = cue.Clip; selected.transform.position = cue.Position;
                selected.volume = 0; selected.Play(); lastPlayed[cue.Id] = simulation.ElapsedSeconds;
            }
            int activeCount = 0;
            foreach (var voice in nodeVoices) if (voice.clip != null) activeCount++;
            float headroom = settings.MasterVolume * 0.65f / Mathf.Sqrt(Mathf.Max(1, activeCount));
            foreach (var voice in nodeVoices)
            {
                if (voice.clip == null) continue;
                var mix = GameplaySoundMix.At(viewCamera, voice.transform.position);
                voice.volume = mix.Volume * headroom; voice.panStereo = mix.Pan;
            }
        }

        private void CaptureBaseline()
        {
            if (network == null || simulation == null) return;
            foreach (NodeSnapshot node in network.Snapshot().Nodes)
                observed[node.Definition.Id] = (node.DepartedCount, node.DeliveredCount);
            lastWave = simulation.Wave;
        }
        private void StopVoices()
        {
            if (waveVoice != null) { waveVoice.Stop(); waveVoice.clip = null; }
            foreach (var voice in nodeVoices)
                if (voice != null) { voice.Stop(); voice.clip = null; }
        }
        public void SetSuspended(bool value)
        {
            suspended = value; StopVoices(); CaptureBaseline(); initialWavePending = false;
        }
        private void OnApplicationPause(bool value)
        {
            applicationPaused = value;
            if (value) { StopVoices(); CaptureBaseline(); initialWavePending = false; }
        }
        private void OnEnable() => CaptureBaseline();
        private void OnDisable() { StopVoices(); CaptureBaseline(); initialWavePending = false; }
    }
}
