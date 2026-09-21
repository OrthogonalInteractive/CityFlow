#nullable enable

using System.Collections;
using System.Linq;
using CityFlow.Application.UseCases;
using CityFlow.Composition;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Presentation.Rendering;
using CityFlow.Presentation.Overview;
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
            var overview=Object.FindAnyObjectByType<OverviewController>();
            overview.Hover(Camera.main.WorldToScreenPoint(network.NodeDefinitions.Single(n=>n.Id=="S1").Position+Vector3.up*1.4f));
            simulation.Tick(4.9);
            yield return null;
            var label = Object.FindAnyObjectByType<UIDocument>().rootVisualElement.Q<Label>("hover-detail");
            Assert.That(label.text, Does.Contain("S1").And.Contain("TO GAME OVER"));
            Assert.That(network.IsGameOver, Is.False);
            simulation.Tick(0.1);
            yield return null;
            Assert.That(network.IsGameOver, Is.True);
            Assert.That(Object.FindAnyObjectByType<UIDocument>().rootVisualElement.Q<Label>("result-title").text,Does.Contain("GAME OVER"));
            double elapsed = simulation.ElapsedSeconds;
            var flights = network.Snapshot().Lines.SelectMany(l => l.InFlight).Select(f => (f.Flow.Id, f.Distance)).ToArray();
            simulation.Tick(10);
            yield return null;
            Assert.That(simulation.ElapsedSeconds, Is.EqualTo(elapsed));
            Assert.That(network.Snapshot().Lines.SelectMany(l => l.InFlight).Select(f => (f.Flow.Id, f.Distance)), Is.EqualTo(flights));
        }
    }
}
