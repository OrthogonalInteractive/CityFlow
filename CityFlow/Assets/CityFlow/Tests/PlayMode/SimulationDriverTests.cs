#nullable enable

using System;
using System.Collections;
using System.Linq;
using CityFlow.Application.UseCases;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Domain.Progression;
using CityFlow.Domain.Spatial;
using CityFlow.Presentation.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CityFlow.Tests.PlayMode
{
    public sealed class SimulationDriverTests
    {
        private sealed class FirstColor : IRandomSource { public int NextIndex(int count) => 0; }

#if UNITY_EDITOR
        [UnityTest] public IEnumerator EditorPauseAndResumeDoNotCatchUpGenerationMovementWavesOrOverload()
        {
            yield return SceneManager.LoadSceneAsync("WiringLab"); yield return null;
            var driver = Object.FindAnyObjectByType<SimulationDriver>();
            driver.enabled = false;
            var network = new FlowNetwork(new StageDefinition(0, new Rect(-50, -50, 100, 100), Array.Empty<Bounds>(), new NodeDefinition[]
            {
                new SourceNodeDefinition("S", Vector3.zero, generationInterval: 1),
                new SourceNodeDefinition("MOVING", new Vector3(-20, 0, 0), generationInterval: 1000),
                new SinkNodeDefinition("T", new Vector3(20, 0, 0), FlowColor.Red)
            }), new NetworkSettings(1, 5, 3, 1, 0, 1.5));
            var simulation = new FlowSimulation(network, new FirstColor(), new[]
            {
                new WaveDefinition(1, 1, new NodeDefinition[] { new RelayNodeDefinition("R", new Vector3(0, 0, 20)) })
            });
            network.TryConnect("MOVING", "T", new[] { new Vector3(-20, 0, 0), new Vector3(20, 0, 0) });
            network.GenerateFlow("S", FlowColor.Red); network.GenerateFlow("MOVING", FlowColor.Red);
            simulation.Tick(0.05);
            driver.Initialize(simulation);
            var before = network.Snapshot(); double elapsed = simulation.ElapsedSeconds;
            double gameTime = Time.timeAsDouble;
            bool changedWhilePaused = false, resumed = false;
            double pausedAt = UnityEditor.EditorApplication.timeSinceStartup;
            UnityEditor.EditorApplication.CallbackFunction? resume = null;
            resume = () =>
            {
                changedWhilePaused |= !ReferenceEquals(before, network.Snapshot()) || simulation.ElapsedSeconds != elapsed;
                // This integration test needs a real Editor suspension to exercise Unity's resume-frame clock.
                if (UnityEditor.EditorApplication.timeSinceStartup - pausedAt < 2) return;
                UnityEditor.EditorApplication.update -= resume;
                resumed = true;
                UnityEditor.EditorApplication.isPaused = false;
            };
            try
            {
                UnityEditor.EditorApplication.update += resume;
                driver.enabled = true;
                UnityEditor.EditorApplication.isPaused = true;
                yield return null; yield return null;
                while (Time.timeAsDouble - gameTime < 0.1) yield return null;
                driver.enabled = false;
                var after = network.Snapshot();
                Assert.That(resumed, Is.True);
                Assert.That(changedWhilePaused, Is.False);
                Assert.That(simulation.ElapsedSeconds, Is.GreaterThan(elapsed), "Normal simulation updates must continue after resuming.");
                Assert.That(simulation.ElapsedSeconds - elapsed,
                    Is.LessThanOrEqualTo(Time.timeAsDouble - gameTime + FlowSimulation.StepSeconds),
                    "The resume frame must use elapsed game time, not the Editor's suspended wall time.");
                Assert.That(network.IsGameOver, Is.False);
                Assert.That(simulation.Wave, Is.EqualTo(1));
                Assert.That(after.GeneratedCount, Is.EqualTo(before.GeneratedCount));
                Assert.That(after.Nodes.Single(n => n.Definition.Id == "S").OverloadSeconds, Is.LessThan(1));
                Assert.That(after.Lines.Single().InFlight.Single().Distance, Is.LessThan(1));
                Assert.That(simulation.IsPaused, Is.False, "Editor pause must not toggle the in-game Pause state.");
            }
            finally
            {
                driver.enabled = false;
                UnityEditor.EditorApplication.update -= resume;
                UnityEditor.EditorApplication.isPaused = false;
            }
        }
#endif
    }
}
