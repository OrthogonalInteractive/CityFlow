#nullable enable

using System.Collections;
using System.Linq;
using CityFlow.Application.Connections;
using CityFlow.Application.UseCases;
using CityFlow.Composition;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Domain.Spatial;
using CityFlow.Presentation.Overview;
using CityFlow.Presentation.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VContainer;

namespace CityFlow.Tests.PlayMode
{
    public sealed class TokyoStationWiringLabTests
    {
        [UnityTest]
        public IEnumerator StationSceneSupportsPausedWiringTransportAndRestoresCityAppearance()
        {
            yield return SceneManager.LoadSceneAsync("TokyoStationWiringLab", LoadSceneMode.Single);
            yield return null;
            var scope = Object.FindAnyObjectByType<CityFlowLifetimeScope>();
            Assert.That(scope, Is.Not.Null);
            Object.FindAnyObjectByType<SimulationDriver>().enabled = false;
            var network = scope.Container.Resolve<FlowNetwork>();
            var clock = scope.Container.Resolve<FlowSimulation>();
            var session = scope.Container.Resolve<ConnectionSession>();
            var stage = scope.Container.Resolve<StageDefinition>();
            var view = Object.FindAnyObjectByType<ValidationCityView>();
            Assert.That(view.VisibleNodeCount, Is.EqualTo(7));
            Assert.That(network.Snapshot().Lines, Is.Empty);
            Assert.That(view.transform.Find("Ground"), Is.Null, "Keep the imported plaza instead of drawing a validation board.");
            Assert.That(view.transform.Find("Building"), Is.Null);
            clock.SetPaused(true);
            var elapsed = clock.ElapsedSeconds;
            Connect("S1", "R1");
            foreach (var sink in stage.Nodes.Where(node => node.Kind == NodeKind.Sink)) Connect("R1", sink.Id);
            foreach (FlowColor color in System.Enum.GetValues(typeof(FlowColor))) network.GenerateFlow("S1", color);
            clock.Tick(20);
            Assert.That(clock.ElapsedSeconds, Is.EqualTo(elapsed));
            Assert.That(network.Snapshot().DeliveredCount, Is.Zero);
            clock.SetPaused(false);
            clock.Tick(20);
            Assert.That(network.Snapshot().DeliveredCount, Is.EqualTo(5));
            Assert.That(network.Snapshot().Lines.Count, Is.EqualTo(6));
            Assert.That(network.IsGameOver, Is.False);

            var building = GameObject.Find("TokyoStation_Building").GetComponentsInChildren<Renderer>().First();
            var original = building.sharedMaterials;
            var originalShadows = building.shadowCastingMode;
            int colliders = Object.FindObjectsByType<MeshCollider>().Length;
            view.SetHiddenNode("S1");
            foreach (var material in building.sharedMaterials)
            {
                Assert.That(material.GetColor("_BaseColor").a, Is.LessThan(0.3f));
                Assert.That(material.GetFloat("_DstBlend"), Is.GreaterThan(0));
                Assert.That(material.GetFloat("_ZWrite"), Is.Zero);
            }
            view.SetHiddenNode(null);
            Assert.That(building.sharedMaterials, Is.EqualTo(original));
            Assert.That(building.shadowCastingMode, Is.EqualTo(originalShadows));
            Assert.That(Object.FindObjectsByType<MeshCollider>().Length, Is.EqualTo(colliders));
            var overview = Object.FindAnyObjectByType<OverviewController>();
            var home = overview.CaptureView();
            overview.Pan(new Vector2(10, 10));
            overview.Orbit(new Vector2(20, 10));
            overview.ResetView();
            Assert.That(Camera.main.transform.position, Is.EqualTo(home.Position));
            Assert.That(Camera.main.transform.rotation, Is.EqualTo(home.Rotation));

            void Connect(string from, string to)
            {
                Assert.That(session.Begin(from), Is.True);
                Assert.That(session.SelectTarget(to), Is.True);
                Assert.That(session.Confirm(), Is.EqualTo(ConnectionFailure.None), from + " -> " + to);
            }
        }
    }
}
