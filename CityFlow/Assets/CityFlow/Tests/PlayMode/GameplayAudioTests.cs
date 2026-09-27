#nullable enable
using System;
using System.Collections;
using System.Linq;
using CityFlow.Application.UseCases;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Domain.Progression;
using CityFlow.Domain.Spatial;
using CityFlow.Presentation.Audio;
using CityFlow.Presentation.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CityFlow.Tests.PlayMode
{
    public sealed class GameplayAudioTests
    {
        private sealed class FirstColor : IRandomSource { public int NextIndex(int count) => 0; }
        private GameObject? root;
        private GameplayAudioSettings? settings;
        private AudioClip[] clips = Array.Empty<AudioClip>();
        private GameplayAudioView View => root != null ? root.GetComponent<GameplayAudioView>() : throw new InvalidOperationException();
        private AudioSource[] Voices => root != null ? root.GetComponentsInChildren<AudioSource>() : Array.Empty<AudioSource>();

        [UnitySetUp] public IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("WiringLab"); yield return null;
            Object.FindAnyObjectByType<SimulationDriver>().enabled = false;
            clips = new[] { "Wave", "Source", "Sink" }.Select(n => AudioClip.Create(n, 48000, 1, 48000, false)).ToArray();
            settings = ScriptableObject.CreateInstance<GameplayAudioSettings>(); settings.Configure(clips[0], clips[1], clips[2]);
            Camera.main.transform.SetPositionAndRotation(new Vector3(0, 100, 0), Quaternion.Euler(90, 0, 0));
            Camera.main.orthographic = true; Camera.main.orthographicSize = 40;
        }
        [TearDown] public void CleanUp()
        {
            if (root != null) Object.DestroyImmediate(root);
            if (settings != null) Object.DestroyImmediate(settings);
            foreach (var clip in clips) Object.DestroyImmediate(clip);
        }
        private FlowSimulation Bind(FlowNetwork network)
        {
            var simulation = new FlowSimulation(network, new FirstColor(), new[] { new WaveDefinition(2, 1, Array.Empty<NodeDefinition>()) });
            root = new GameObject("Audio test");
            root.AddComponent<GameplayAudioView>().Initialize(network, simulation, Camera.main, settings ?? throw new InvalidOperationException());
            View.Refresh();
            return simulation;
        }
        private static FlowNetwork Network() => new(new StageDefinition(0, new Rect(-100, -100, 200, 200), Array.Empty<Bounds>(), new NodeDefinition[]
        {
            new SourceNodeDefinition("S", Vector3.zero, generationInterval: 10000),
            new SinkNodeDefinition("T", new Vector3(0, 0, 20), FlowColor.Red)
        }), new NetworkSettings(10, 5, 3, 10, 0));

        [UnityTest] public IEnumerator ActualSourceDepartureAndSinkConsumptionPlayOnceWithoutChangingNetwork()
        {
            var network = Network(); Bind(network);
            Assert.That(Voices.Count(v => v.clip == clips[0]), Is.EqualTo(1), "The initial Wave announces the new session.");
            network.GenerateFlow("S", FlowColor.Red); View.Refresh();
            Assert.That(Voices.Any(v => v.clip == clips[1]), Is.False, "Generation into a waiting Buffer is silent.");
            network.TryConnect("S", "T", new[] { Vector3.zero, new Vector3(0, 0, 20) });
            network.RouteWaitingFlows(); var departed = network.Snapshot(); View.Refresh();
            var source = Voices.Single(v => v.clip == clips[1]);
            Assert.That(source.volume, Is.GreaterThan(0));
            Assert.That(network.Snapshot(), Is.SameAs(departed));
            source.timeSamples = 512; View.Refresh();
            Assert.That(source.timeSamples, Is.GreaterThanOrEqualTo(512), "Reading the same count must not restart the clip.");
            float centeredVolume = source.volume;
            Camera.main.transform.position += Vector3.right * 30; View.Refresh();
            Assert.That(source.volume, Is.LessThan(centeredVolume));
            Assert.That(source.panStereo, Is.LessThan(0));
            network.AdvanceInFlight(3); var delivered = network.Snapshot(); View.Refresh();
            Assert.That(Voices.Count(v => v.clip == clips[2]), Is.EqualTo(1));
            Assert.That(network.Snapshot(), Is.SameAs(delivered));
            View.SetSuspended(true); View.SetSuspended(false); View.Refresh();
            Assert.That(Voices.All(v => v.clip == null), Is.True, "Old cues must not replay after suspension.");
            yield return null;
        }

        [UnityTest] public IEnumerator PauseWaveAndDisableDoNotQueueOldSoundsAndDestructionReleasesVoices()
        {
            var network = Network(); var simulation = Bind(network);
            simulation.SetPaused(true); View.Refresh();
            Assert.That(Voices.All(v => v.clip == null), Is.True);
            simulation.Tick(20); View.Refresh();
            Assert.That(Voices.All(v => v.clip == null), Is.True);
            simulation.SetPaused(false); View.Refresh();
            Assert.That(Voices.All(v => v.clip == null), Is.True);
            simulation.Tick(2); View.Refresh();
            Assert.That(Voices.Count(v => v.clip == clips[0]), Is.EqualTo(1));
            View.enabled = false;
            network.TryConnect("S", "T", new[] { Vector3.zero, new Vector3(0, 0, 20) });
            network.GenerateFlow("S", FlowColor.Red); network.RouteWaitingFlows();
            View.enabled = true; View.Refresh();
            Assert.That(Voices.All(v => v.clip == null), Is.True);
            var voices = Voices; Object.Destroy(root); yield return null;
            Assert.That(voices.All(v => v == null), Is.True);
        }

        [UnityTest] public IEnumerator BurstsHaveBoundedVoicesAndNearerNewCuesReplaceDistantOnes()
        {
            var nodes = Enumerable.Range(0, 12).Select(i => (NodeDefinition)new SourceNodeDefinition("S" + i,
                new Vector3((i - 6) * 12, 0, -25), generationInterval: 10000)).ToList();
            nodes.Add(new SourceNodeDefinition("CENTER", Vector3.zero, generationInterval: 10000));
            nodes.Add(new SinkNodeDefinition("T", new Vector3(0, 0, 40), FlowColor.Red, maxIncoming: 20));
            var network = new FlowNetwork(new StageDefinition(0, new Rect(-100, -100, 200, 200), Array.Empty<Bounds>(), nodes), new NetworkSettings(10, 5, 3, 10, 0));
            Bind(network); View.SetSuspended(true); View.SetSuspended(false);
            foreach (var node in nodes.OfType<SourceNodeDefinition>())
            {
                network.TryConnect(node.Id, "T", new[] { node.Position, new Vector3(0, 0, 40) });
                if (node.Id == "CENTER") continue;
                for (int i = 0; i < 3; i++) network.GenerateFlow(node.Id, FlowColor.Red);
            }
            network.RouteWaitingFlows(); View.Refresh();
            Assert.That(Voices.Count(v => v.clip == clips[1]), Is.EqualTo(8), "Many departures are coalesced and voice count stays bounded.");
            Assert.That(Voices.Count(), Is.EqualTo(9), "One dedicated Wave voice remains available.");
            network.GenerateFlow("CENTER", FlowColor.Red); network.RouteWaitingFlows(); View.Refresh();
            Assert.That(Voices.Any(v => v.clip == clips[1] && v.transform.position == Vector3.up * 1.4f), Is.True);
            Assert.That(Voices.Count(v => v.clip == clips[1]), Is.EqualTo(8));
            for (int i = 0; i < 10; i++) network.GenerateFlow("CENTER", FlowColor.Red);
            network.EvaluateOverload(10);
            Assert.That(network.IsGameOver, Is.True);
            View.Refresh();
            Assert.That(Voices.All(v => v.clip == null), Is.True);
            yield return null;
        }

        [UnityTest] public IEnumerator SceneComposesApprovedAudioAndKeepsOneListener()
        {
            var audio = Object.FindObjectsByType<GameplayAudioView>();
            Assert.That(audio.Length, Is.EqualTo(1));
            var config = Resources.FindObjectsOfTypeAll<GameplayAudioSettings>().Single(s => s.name == "GameplayAudio");
            Assert.That(config.Wave.length, Is.EqualTo(1.45f).Within(0.001f));
            Assert.That(config.Source.length, Is.EqualTo(0.18f).Within(0.001f));
            Assert.That(config.Sink.length, Is.EqualTo(0.4f).Within(0.001f));
            Assert.That(config.Source.channels, Is.EqualTo(1)); Assert.That(config.Sink.channels, Is.EqualTo(1));
            foreach (var clip in new[] { config.Wave, config.Source, config.Sink })
            {
                var samples = new float[clip.samples * clip.channels];
                Assert.That(clip.GetData(samples, 0), Is.True);
                Assert.That(samples.Max(v => Mathf.Abs(v)), Is.InRange(0.2f, 0.6f), "Approved clips must contain audible PCM data with headroom.");
            }
            Assert.That(Object.FindObjectsByType<AudioListener>().Count(l => l.enabled), Is.EqualTo(1));
            yield return null;
        }

#if UNITY_EDITOR
        [UnityTest] public IEnumerator EditorPauseStopsVoicesAndResumeDoesNotReplayThem()
        {
            Bind(Network());
            bool stopped = false;
            UnityEditor.EditorApplication.CallbackFunction? resume = null;
            resume = () =>
            {
                if (!UnityEditor.EditorApplication.isPaused) return;
                stopped = Voices.All(v => v.clip == null && !v.isPlaying);
                UnityEditor.EditorApplication.update -= resume;
                UnityEditor.EditorApplication.isPaused = false;
            };
            try
            {
                UnityEditor.EditorApplication.update += resume;
                UnityEditor.EditorApplication.isPaused = true;
                yield return null; yield return null;
                Assert.That(stopped, Is.True);
                Assert.That(Voices.All(v => v.clip == null), Is.True);
            }
            finally
            {
                UnityEditor.EditorApplication.update -= resume;
                UnityEditor.EditorApplication.isPaused = false;
            }
        }
#endif
    }
}
