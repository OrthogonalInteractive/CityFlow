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
using UnityEngine.UIElements;
using VContainer;

namespace CityFlow.Tests.PlayMode
{
    public sealed class TokyoStationWiringLabTests
    {
        [UnityTest]
        public IEnumerator ThreeWavesKeepTheNetworkAndRetryRestartsTheTwoColorOpening()
        {
            yield return SceneManager.LoadSceneAsync("TokyoStationWiringLab", LoadSceneMode.Single);
            yield return null;
            Object.FindAnyObjectByType<SimulationDriver>().enabled = false;
            var scope = Object.FindAnyObjectByType<CityFlowLifetimeScope>();
            var network = scope.Container.Resolve<FlowNetwork>();
            var clock = scope.Container.Resolve<FlowSimulation>();
            var session = scope.Container.Resolve<ConnectionSession>();
            var view = Object.FindAnyObjectByType<ValidationCityView>();
            var sceneHandle = SceneManager.GetActiveScene().handle;
            Connect("S1", "R1"); Connect("R1", "RED"); Connect("R1", "BLUE");
            var openingLines = network.Snapshot().Lines.ToArray();
            clock.Tick(59.95 - clock.ElapsedSeconds);
            clock.SetPaused(true);
            clock.Tick(30);
            Assert.That(clock.Wave, Is.EqualTo(1), "Pause must stop the Wave clock.");
            clock.SetPaused(false);
            clock.Tick(0.05);
            yield return null; yield return null;
            Assert.That(clock.Wave, Is.EqualTo(2));
            Assert.That(view.VisibleNodeCount, Is.EqualTo(7));
            Assert.That(clock.SourceStartRemaining("S2"), Is.EqualTo(20).Within(0.01));
            Connect("S2", "R2"); Connect("R1", "R2"); Connect("R2", "R1"); Connect("R2", "GREEN");
            clock.Tick(60);
            yield return null; yield return null;
            Assert.That(clock.Wave, Is.EqualTo(3));
            Assert.That(view.VisibleNodeCount, Is.EqualTo(11));
            Assert.That(SceneManager.GetActiveScene().handle, Is.EqualTo(sceneHandle));
            Assert.That(scope.Container.Resolve<FlowNetwork>(), Is.SameAs(network));
            foreach (var line in openingLines)
                Assert.That(network.Snapshot().Lines.Single(l => l.Id == line.Id).Route, Is.SameAs(line.Route));
            var root = Object.FindAnyObjectByType<UIDocument>().rootVisualElement;
            Assert.That(root.Q<Label>("wave-value").text, Is.EqualTo("WAVE 3"));
            Assert.That(root.Q<Label>("wave-notice").text, Does.Contain("WAVE 3"));
            Assert.That(root.Q<Button>("arrival-S3"), Is.Not.Null);
            Connect("S3", "R3"); Connect("R2", "R3"); Connect("R3", "R2");
            Connect("R3", "YELLOW"); Connect("R2", "PURPLE");
            clock.Tick(180);
            Assert.That(clock.Wave, Is.EqualTo(3));
            Assert.That(clock.NextWaveSeconds, Is.Null);
            Assert.That(clock.Result, Is.Null);
            Assert.That(network.Snapshot().DeliveredCount, Is.GreaterThan(70));
            var feed = network.Snapshot().Lines.Single(l => l.SourceId == "S3");
            network.RequestDeletion(feed.Id);
            clock.Tick(120);
            yield return null;
            Assert.That(clock.Result?.Wave, Is.EqualTo(3));
            Assert.That(clock.Result?.SourceId, Is.EqualTo("S3"));
            Assert.That(root.Q("result-overlay").resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            UiPointer.Click(root.Q<Button>("retry-session"));
            CityFlowLifetimeScope? next = null;
            for (int frame = 0; frame < 180; frame++)
            {
                yield return null;
                next = Object.FindAnyObjectByType<CityFlowLifetimeScope>();
                if (next != null && next != scope && Object.FindAnyObjectByType<SimulationDriver>() != null) break;
            }
            if (next == null || next == scope) throw new AssertionException("Retry did not load a new station session.");
            Object.FindAnyObjectByType<SimulationDriver>().enabled = false;
            Assert.That(next.Container.Resolve<FlowSimulation>().Wave, Is.EqualTo(1));
            Assert.That(next.Container.Resolve<FlowSimulation>().Result, Is.Null);
            Assert.That(next.Container.Resolve<FlowNetwork>().NodeDefinitions.Count, Is.EqualTo(4));
            Assert.That(next.Container.Resolve<FlowNetwork>().Snapshot().Lines, Is.Empty);

            void Connect(string from, string to)
            {
                Assert.That(session.Begin(from), Is.True);
                Assert.That(session.SelectTarget(to), Is.True);
                Assert.That(session.Confirm(), Is.EqualTo(ConnectionFailure.None), from + " -> " + to);
            }
        }

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
            Assert.That(view.VisibleNodeCount, Is.EqualTo(4));
            Assert.That(network.Snapshot().Lines, Is.Empty);
            Assert.That(view.transform.Find("Ground"), Is.Null, "Keep the imported plaza instead of drawing a validation board.");
            Assert.That(view.transform.Find("Building"), Is.Null);
            clock.SetPaused(true);
            var elapsed = clock.ElapsedSeconds;
            Connect("S1", "R1");
            foreach (var sink in stage.Nodes.Where(node => node.Kind == NodeKind.Sink)) Connect("R1", sink.Id);
            foreach (var sink in stage.Nodes.OfType<SinkNodeDefinition>()) network.GenerateFlow("S1", sink.Color);
            clock.Tick(20);
            Assert.That(clock.ElapsedSeconds, Is.EqualTo(elapsed));
            Assert.That(network.Snapshot().DeliveredCount, Is.Zero);
            clock.SetPaused(false);
            clock.Tick(10);
            Assert.That(network.Snapshot().DeliveredCount, Is.EqualTo(2));
            Assert.That(network.Snapshot().Lines.Count, Is.EqualTo(3));
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
