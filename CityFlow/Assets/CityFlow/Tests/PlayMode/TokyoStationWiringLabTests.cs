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
        public IEnumerator OverviewCanPanAcrossTheWholeCityAndZoomOutToSeeItsBoundary()
        {
            yield return SceneManager.LoadSceneAsync("TokyoStationWiringLab", LoadSceneMode.Single);
            yield return null;
            Object.FindAnyObjectByType<SimulationDriver>().enabled = false;
            var overview = Object.FindAnyObjectByType<OverviewController>();
            var stage = Object.FindAnyObjectByType<CityFlowLifetimeScope>().Container.Resolve<StageDefinition>();
            var opening = overview.CaptureView();
            Assert.That(opening.Size, Is.EqualTo(220), "The opening still starts close to the station.");
            overview.Pan(new Vector2(10000, 10000));
            var northEast = overview.CaptureView().Pivot;
            Assert.That(northEast.x, Is.EqualTo(stage.WalkableArea.xMax).Within(0.01));
            Assert.That(northEast.z, Is.EqualTo(stage.WalkableArea.yMax).Within(0.01));
            overview.Pan(new Vector2(-20000, -20000));
            var southWest = overview.CaptureView().Pivot;
            Assert.That(southWest.x, Is.EqualTo(stage.WalkableArea.xMin).Within(0.01));
            Assert.That(southWest.z, Is.EqualTo(stage.WalkableArea.yMin).Within(0.01));
            overview.ResetView();
            var whole = overview.CaptureView();
            Assert.That(whole.Pivot.x, Is.EqualTo(stage.WalkableArea.center.x).Within(0.01));
            Assert.That(whole.Pivot.z, Is.EqualTo(stage.WalkableArea.center.y).Within(0.01));
            Assert.That(whole.Size, Is.GreaterThanOrEqualTo(stage.WalkableArea.height * 0.7f));
            overview.RestoreView(opening);
            for (int i = 0; i < 5; i++) { overview.Zoom(-1); overview.AdvanceZoom(1); }
            Assert.That(Camera.main.orthographicSize, Is.GreaterThanOrEqualTo(whole.Size));
        }

        [UnityTest]
        public IEnumerator WaveTwoKeepsTheWestNetworkAndRetryRestartsTheFiveNodeOpening()
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
            Assert.That(clock.NextWaveSeconds, Is.EqualTo(60));
            Connect("S1", "R1"); Connect("S2", "R1"); Connect("R1", "RED"); Connect("R1", "BLUE");
            var openingLines = network.Snapshot().Lines.ToArray();
            clock.Tick(59.95 - clock.ElapsedSeconds);
            clock.SetPaused(true);
            clock.Tick(30);
            Assert.That(clock.Wave, Is.EqualTo(1), "Pause must stop the Wave clock.");
            clock.SetPaused(false);
            clock.Tick(0.05);
            yield return null; yield return null;
            Assert.That(clock.Wave, Is.EqualTo(2));
            Assert.That(view.VisibleNodeCount, Is.EqualTo(9));
            Assert.That(clock.SourceStartRemaining("S3"), Is.EqualTo(20).Within(0.01));
            Assert.That(clock.NextWaveSeconds, Is.EqualTo(120), "Ten authored Waves keep arriving every 60 s.");
            Assert.That(network.NodeDefinitions.All(n => n.Position.x < -151 && n.Position.y == 3.7f), Is.True,
                "Waves 1-3 stay on the west plaza ground.");
            Assert.That(SceneManager.GetActiveScene().handle, Is.EqualTo(sceneHandle));
            Assert.That(scope.Container.Resolve<FlowNetwork>(), Is.SameAs(network));
            foreach (var line in openingLines)
                Assert.That(network.Snapshot().Lines.Single(l => l.Id == line.Id).Route, Is.SameAs(line.Route));
            var root = Object.FindAnyObjectByType<UIDocument>().rootVisualElement;
            Assert.That(root.Q<Label>("wave-value").text, Is.EqualTo("2"));
            Assert.That(root.Q<Label>("wave-transition-title").text, Does.Contain("WAVE 2"));
            Assert.That(root.Q("arrival-markers"), Is.Null);
            Assert.That(root.Q<Label>("wave-transition-additions").text, Is.EqualTo("SOURCE +2  ·  RELAY +1  ·  SINK +1"));
            Connect("S3", "R2"); Connect("S4", "R2"); Connect("R2", "GREEN"); Connect("R2", "R1"); Connect("R1", "R2");
            clock.Tick(50);
            Assert.That(clock.Wave, Is.EqualTo(2));
            Assert.That(clock.Result, Is.Null);
            Assert.That(network.Snapshot().DeliveredCount, Is.GreaterThan(10));
            // Leaving the Wave 3 Sources unwired loses the session through a Source Overload, not through Relays.
            clock.Tick(240);
            yield return null;
            Assert.That(clock.Result, Is.Not.Null);
            Assert.That(clock.Result?.Wave, Is.GreaterThanOrEqualTo(3));
            Assert.That(network.NodeDefinitions.OfType<SourceNodeDefinition>().Any(n => n.Id == clock.Result?.SourceId), Is.True);
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
            Assert.That(next.Container.Resolve<FlowNetwork>().NodeDefinitions.Count, Is.EqualTo(5));
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
            Assert.That(view.VisibleNodeCount, Is.EqualTo(5));
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
            overview.ResetView();
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
