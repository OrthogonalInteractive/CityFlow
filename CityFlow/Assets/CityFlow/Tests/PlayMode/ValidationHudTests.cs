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
    public sealed class ValidationHudTests
    {
        [UnitySetUp]
        public IEnumerator LoadCity()
        {
            yield return SceneManager.LoadSceneAsync("Bootstrap", LoadSceneMode.Single);
            yield return null;
            Object.FindAnyObjectByType<SimulationDriver>().enabled = false;
        }

        private static UIDocument Document()
        {
            UIDocument document = Object.FindAnyObjectByType<UIDocument>();
            Assert.That(document, Is.Not.Null, "The runtime HUD must use UI Toolkit.");
            return document;
        }

        private static VisualElement SourceRow(VisualElement root, string id) =>
            root.Q("source-activity-" + id) ?? throw new AssertionException("Missing Source activity row: " + id);

        [UnityTest]
        public IEnumerator SourceListShowsLastGeneratedFlowBelowDeliveredAndRetainsItAfterDepartureAndPause()
        {
            var scope = Object.FindAnyObjectByType<CityFlowLifetimeScope>();
            var network = scope.Container.Resolve<FlowNetwork>();
            var simulation = scope.Container.Resolve<FlowSimulation>();
            simulation.SetPaused(true);
            yield return null; yield return null;
            var root = Document().rootVisualElement;
            var row = SourceRow(root, "S1");
            Assert.That(root.Q("source-activity-rows").childCount, Is.EqualTo(1));
            Assert.That(row.Q<Label>("source-name").text, Is.EqualTo("S1"));
            Assert.That(row.Q<Label>("source-latest-flow").text, Is.EqualTo("—"));
            Assert.That(row.worldBound.yMin, Is.GreaterThan(root.Q("delivered-value").worldBound.yMax));
            network.GenerateFlow("S1", FlowColor.Red);
            yield return null;
            Assert.That(row.Q<Label>("source-latest-flow").text, Is.EqualTo("Red"));
            network.GenerateFlow("S1", FlowColor.Blue);
            yield return null; yield return null;
            Assert.That(row.Q<Label>("source-latest-flow").text, Is.EqualTo("Blue"), "Use the latest generation, not the head of the waiting queue.");
            Assert.That(row.Q<Label>("source-flow-badge").text, Is.EqualTo("B"));
            Assert.That(row.Q("source-flow-badge").resolvedStyle.backgroundColor, Is.EqualTo(ValidationCityView.ColorFor(FlowColor.Blue)));
            network.RouteWaitingFlows();
            Assert.That(network.Snapshot().Nodes.Single(n => n.Definition.Id == "S1").Buffer, Is.Empty);
            var before = network.Snapshot();
            double elapsed = simulation.ElapsedSeconds;
            simulation.Tick(30); yield return null;
            Assert.That(row.Q<Label>("source-latest-flow").text, Is.EqualTo("Blue"));
            Assert.That(network.Snapshot(), Is.SameAs(before));
            Assert.That(simulation.ElapsedSeconds, Is.EqualTo(elapsed));
            Assert.That(root.Q("source-activity").pickingMode, Is.EqualTo(PickingMode.Ignore));
            Assert.That(row.Query<Label>().ToList().All(e => e.pickingMode == PickingMode.Ignore), Is.True);
            Assert.That(row.Q<Button>("source-focus"), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator SourceRowClickAnimatesCameraWhilePausedWithoutStartingWiring()
        {
            var scope = Object.FindAnyObjectByType<CityFlowLifetimeScope>();
            var simulation = scope.Container.Resolve<FlowSimulation>();
            var network = scope.Container.Resolve<FlowNetwork>();
            var session = scope.Container.Resolve<ConnectionSession>();
            var overview = Object.FindAnyObjectByType<OverviewController>();
            simulation.SetPaused(true);
            yield return null; yield return null;
            var row = SourceRow(Document().rootVisualElement, "S1");
            var button = row.Q<Button>("source-focus");
            Assert.That(button, Is.Not.Null, "Each Source row must offer camera focus.");
            Vector2 panel = button.worldBound.center;
            Vector2 screen = RuntimePanelUtils.ScreenToPanel(button.panel, new Vector2(Screen.width, Screen.height));
            screen = new Vector2(panel.x / screen.x * Screen.width, Screen.height - panel.y / screen.y * Screen.height);
            Assert.That(overview.IsPointerBlocked?.Invoke(screen), Is.True, "Source row clicks must not reach world wiring input.");
            using (var submit = NavigationSubmitEvent.GetPooled()) button.SendEvent(submit);
            Assert.That(overview.Focused.IsEmpty, Is.True);
            var camera = Camera.main;
            Vector3 before = camera.transform.position;
            Quaternion rotation = camera.transform.rotation;
            float size = camera.orthographicSize;
            var snapshot = network.Snapshot(); double elapsed = simulation.ElapsedSeconds;
            UiPointer.Click(button);
            Assert.That(camera.transform.position, Is.EqualTo(before), "Click starts an animation without teleporting.");
            Assert.That(overview.Focused.NodeId, Is.EqualTo("S1"));
            overview.AdvanceFocus(0.2f);
            Assert.That(Vector3.Distance(camera.transform.position, before), Is.GreaterThan(0.1f));
            Vector3 target = network.NodeDefinitions.Single(n => n.Id == "S1").Position + Vector3.up * 1.4f;
            Assert.That(Vector2.Distance(camera.WorldToViewportPoint(target), new Vector2(0.5f, 0.5f)), Is.GreaterThan(0.01f));
            overview.AdvanceFocus(1);
            Assert.That(Vector2.Distance(camera.WorldToViewportPoint(target), new Vector2(0.5f, 0.5f)), Is.LessThan(0.001f));
            Assert.That(camera.transform.rotation, Is.EqualTo(rotation));
            Assert.That(camera.orthographicSize, Is.EqualTo(size));
            Assert.That(simulation.ElapsedSeconds, Is.EqualTo(elapsed));
            Assert.That(simulation.IsPaused, Is.True);
            Assert.That(network.Snapshot(), Is.SameAs(snapshot));
            Assert.That(session.IsActive, Is.False);
        }

        [UnityTest]
        public IEnumerator WaveAddsAnUnfiredSourceAndHudRebuildKeepsEachSourcesOwnLatestFlow()
        {
            yield return SceneManager.LoadSceneAsync("WiringLab"); yield return null;
            Object.FindAnyObjectByType<SimulationDriver>().enabled = false;
            var scope = Object.FindAnyObjectByType<CityFlowLifetimeScope>();
            var network = scope.Container.Resolve<FlowNetwork>();
            var simulation = scope.Container.Resolve<FlowSimulation>();
            var session = scope.Container.Resolve<ConnectionSession>();
            foreach (string destination in new[] { "RED", "BLUE" })
            {
                Assert.That(session.Begin("S1"), Is.True);
                Assert.That(session.SelectTarget(destination), Is.True);
                Assert.That(session.Confirm(), Is.EqualTo(ConnectionFailure.None));
            }
            simulation.SetPaused(false); simulation.Tick(60 - simulation.ElapsedSeconds); simulation.SetPaused(true);
            yield return null; yield return null;
            var root = Document().rootVisualElement;
            Assert.That(root.Q("source-activity-rows").Children().Select(e => e.name),
                Is.EqualTo(new[] { "source-activity-S1", "source-activity-S2" }));
            Assert.That(SourceRow(root, "S2").Q<Label>("source-latest-flow").text, Is.EqualTo("—"));
            string first = SourceRow(root, "S1").Q<Label>("source-latest-flow").text;
            network.GenerateFlow("S2", FlowColor.Green); yield return null;
            Assert.That(SourceRow(root, "S1").Q<Label>("source-latest-flow").text, Is.EqualTo(first));
            Assert.That(SourceRow(root, "S2").Q<Label>("source-latest-flow").text, Is.EqualTo("Green"));
            var document = Document(); document.gameObject.SetActive(false); document.gameObject.SetActive(true);
            yield return null; yield return null;
            root = document.rootVisualElement;
            Assert.That(root.Q("source-activity-rows").childCount, Is.EqualTo(2));
            Assert.That(SourceRow(root, "S1").Q<Label>("source-latest-flow").text, Is.EqualTo(first));
            Assert.That(SourceRow(root, "S2").Q<Label>("source-flow-badge").text, Is.EqualTo("G"));
            var overview = Object.FindAnyObjectByType<OverviewController>();
            UiPointer.Click(SourceRow(root, "S2").Q<Button>("source-focus"));
            Assert.That(overview.Focused.NodeId, Is.EqualTo("S2"), "Rows added by a Wave and rebuilt with the HUD stay clickable.");
            overview.AdvanceFocus(1);
            Vector3 target = network.NodeDefinitions.Single(n => n.Id == "S2").Position + Vector3.up * 1.4f;
            Assert.That(Vector2.Distance(Camera.main.WorldToViewportPoint(target), new Vector2(0.5f, 0.5f)), Is.LessThan(0.001f));
        }

        [UnityTest]
        public IEnumerator HudShowsLiveNetworkCountsAndDoesNotInterceptWorldInput()
        {
            UIDocument document = Document();
            var scope = Object.FindAnyObjectByType<CityFlowLifetimeScope>();
            var network = scope.Container.Resolve<FlowNetwork>();
            scope.Container.Resolve<FlowSimulation>().Tick(20);
            yield return null;
            VisualElement root = document.rootVisualElement;
            NetworkSnapshot state = network.Snapshot();
            Assert.That(root.Q<Label>("delivered-value").text, Is.EqualTo(state.DeliveredCount.ToString()));
            Assert.That(root.Q("node-rows"),Is.Null);
            Assert.That(root.Q<Label>("elapsed-value").text,Is.EqualTo(CityFlow.Presentation.UI.HudClock.Format(scope.Container.Resolve<FlowSimulation>().ElapsedSeconds)));
            Assert.That(root.Q("line-rows"), Is.Null);
            Assert.That(root.Query().ToList().Where(element=>!root.Query(className:"interactive").ToList().Any(panel=>element==panel || panel.Contains(element))).All(element => element.pickingMode == PickingMode.Ignore), Is.True,
                "Read-only overlays must leave world interaction available.");
        }

        [UnityTest]
        public IEnumerator NewOutgoingLineForRecoveryAppearsWithoutBreakingHud()
        {
            var network = Object.FindAnyObjectByType<CityFlowLifetimeScope>().Container.Resolve<FlowNetwork>();
            Assert.That(network.TryConnect("R1", "RED", new[] { new Vector3(-12,0,-20),
                new Vector3(-38,0,-20), new Vector3(-38,0,22) }).Succeeded, Is.True);
            yield return null;
            yield return null;
            Assert.That(Document().rootVisualElement.Q("node-labels").childCount, Is.EqualTo(5));
        }

        [UnityTest]
        public IEnumerator WaitingBufferGaugesFollowTheCameraAndHideBehindIt()
        {
            UIDocument document = Document();
            Object.FindAnyObjectByType<CityFlowLifetimeScope>().Container.Resolve<FlowNetwork>().GenerateFlow("S1", FlowColor.Red);
            yield return null;
            yield return null;
            Label label = document.rootVisualElement.Q<Label>("node-label-S1");
            Assert.That(label, Is.Not.Null);
            Assert.That(label.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            Vector2 before = label.worldBound.center;
            Camera camera = Camera.main;
            Vector3 initialPosition = camera.transform.position;
            Quaternion initialRotation = camera.transform.rotation;
            camera.transform.position += Vector3.right * 10;
            yield return null;
            yield return null;
            Assert.That(Vector2.Distance(before, label.worldBound.center), Is.GreaterThan(1));
            StageDefinition stage = Object.FindAnyObjectByType<CityFlowLifetimeScope>().Container.Resolve<StageDefinition>();
            Vector3 source = stage.Nodes.Single(n => n.Id == "S1").Position;
            Vector3 screen = camera.WorldToScreenPoint(source + Vector3.up * 5);
            Vector2 panel = RuntimePanelUtils.ScreenToPanel(document.rootVisualElement.panel, new Vector2(screen.x, Screen.height - screen.y));
            Assert.That(label.worldBound.center.x, Is.EqualTo(panel.x).Within(1));
            Assert.That(label.worldBound.yMax, Is.EqualTo(panel.y).Within(1));
            camera.transform.rotation = Quaternion.LookRotation(-camera.transform.forward);
            yield return null;
            yield return null;
            Assert.That(label.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            camera.transform.SetPositionAndRotation(initialPosition, initialRotation);
        }

        [UnityTest]
        public IEnumerator ReenablingTheDocumentRebindsCurrentValuesWithoutDuplicateRows()
        {
            UIDocument document = Document();
            document.gameObject.SetActive(false);
            var scope = Object.FindAnyObjectByType<CityFlowLifetimeScope>();
            scope.Container.Resolve<FlowSimulation>().Tick(20);
            document.gameObject.SetActive(true);
            yield return null;
            yield return null;
            Assert.That(Object.FindObjectsByType<UIDocument>().Length, Is.EqualTo(1));
            Assert.That(document.rootVisualElement.Q("node-labels").childCount, Is.EqualTo(5));
            Assert.That(document.rootVisualElement.Q<Label>("delivered-value").text,
                Is.EqualTo(scope.Container.Resolve<FlowNetwork>().Snapshot().DeliveredCount.ToString()));
        }
    }
}
