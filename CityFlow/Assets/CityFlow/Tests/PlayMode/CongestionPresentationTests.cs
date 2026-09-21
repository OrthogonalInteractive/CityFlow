#nullable enable

using System.Collections;
using System.Linq;
using CityFlow.Application.UseCases;
using CityFlow.Composition;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Presentation.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using VContainer;

namespace CityFlow.Tests.PlayMode
{
    public sealed class CongestionPresentationTests
    {
        [UnityTest]
        public IEnumerator SourceWarningBecomesGameOverAndSimulationFreezes()
        {
            yield return SceneManager.LoadSceneAsync("Bootstrap", LoadSceneMode.Single);
            yield return null;
            Object.FindAnyObjectByType<SimulationDriver>().enabled = false;
            var scope = Object.FindAnyObjectByType<CityFlowLifetimeScope>();
            var network = scope.Container.Resolve<FlowNetwork>();
            var simulation = scope.Container.Resolve<FlowSimulation>();
            for (int i = 0; i < 100; i++) network.GenerateFlow("S1", FlowColor.Blue);
            simulation.Tick(4.9);
            yield return null;
            var label = Object.FindAnyObjectByType<UIDocument>().rootVisualElement.Q<Label>("source-status");
            Assert.That(label.text, Does.Contain("OVERLOAD S1"));
            Assert.That(network.IsGameOver, Is.False);
            simulation.Tick(0.1);
            yield return null;
            Assert.That(network.IsGameOver, Is.True);
            Assert.That(label.text, Is.EqualTo("GAME OVER · SOURCE S1"));
            double elapsed = simulation.ElapsedSeconds;
            var flights = network.Snapshot().Lines.SelectMany(l => l.InFlight).Select(f => (f.Flow.Id, f.Distance)).ToArray();
            simulation.Tick(10);
            yield return null;
            Assert.That(simulation.ElapsedSeconds, Is.EqualTo(elapsed));
            Assert.That(network.Snapshot().Lines.SelectMany(l => l.InFlight).Select(f => (f.Flow.Id, f.Distance)), Is.EqualTo(flights));
        }
    }
}
