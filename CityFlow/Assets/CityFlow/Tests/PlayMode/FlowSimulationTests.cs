#nullable enable

using System.Collections;
using System.Linq;
using CityFlow.Application.UseCases;
using CityFlow.Composition;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Infrastructure.Configuration;
using CityFlow.Presentation.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VContainer;

namespace CityFlow.Tests.PlayMode
{
    public sealed class FlowSimulationTests
    {
        [UnityTest]
        public IEnumerator BootstrapGeneratesMovesAndConsumesFlowsWithoutMutatingAssets()
        {
            yield return SceneManager.LoadSceneAsync("Bootstrap", LoadSceneMode.Single);
            yield return null;
            var driver = Object.FindAnyObjectByType<SimulationDriver>();
            Assert.That(driver, Is.Not.Null);
            driver.enabled = false;
            var scope = Object.FindAnyObjectByType<CityFlowLifetimeScope>();
            var simulation = scope.Container.Resolve<FlowSimulation>();
            var network = scope.Container.Resolve<FlowNetwork>();
            var settings = Resources.FindObjectsOfTypeAll<GameplaySettings>().Single(s => s.name == "ValidationGameplay");
            var stage = Resources.FindObjectsOfTypeAll<StageConfiguration>().Single(s => s.name == "ValidationStage");
            string initialSettings = JsonUtility.ToJson(settings), initialStage = JsonUtility.ToJson(stage);

            simulation.Tick(12);
            yield return null;
            NetworkSnapshot state = network.Snapshot();
            Assert.That(state.GeneratedCount, Is.GreaterThanOrEqualTo(16));
            Assert.That(state.DeliveredCount, Is.GreaterThan(0));
            Assert.That(state.Nodes.Single(n => n.Definition.Id == "S1").Buffer.Count, Is.GreaterThan(0));
            Assert.That(state.Lines.All(l => l.InFlight.Count <= l.Capacity), Is.True);
            Assert.That(state.Nodes.Sum(n => n.Buffer.Count) + state.Lines.Sum(l => l.InFlight.Count) + state.DeliveredCount,
                Is.EqualTo(state.GeneratedCount));
            var view = Object.FindAnyObjectByType<ValidationCityView>();
            Assert.That(view.VisibleFlowCount, Is.EqualTo(state.Lines.Sum(l => l.InFlight.Count)));
            var line = state.Lines.First(l => l.InFlight.Count > 0);
            InFlightSnapshot flight = line.InFlight[0];
            GameObject particle = GameObject.Find($"FLOW {flight.Flow.Id} / {flight.Flow.Color}");
            Assert.That(particle.transform.position, Is.EqualTo(line.Route.PositionAt(flight.Distance) + Vector3.up * 0.9f));
            Assert.That(JsonUtility.ToJson(settings), Is.EqualTo(initialSettings));
            Assert.That(JsonUtility.ToJson(stage), Is.EqualTo(initialStage));

            simulation.Tick(10);
            yield return null;
            Assert.That(network.Snapshot().DeliveredCount, Is.GreaterThan(state.DeliveredCount));
            Assert.That(view.VisibleFlowCount, Is.EqualTo(network.Snapshot().Lines.Sum(l => l.InFlight.Count)));
            Assert.That(GameObject.Find($"FLOW {flight.Flow.Id} / {flight.Flow.Color}"), Is.Null);
        }
    }
}
