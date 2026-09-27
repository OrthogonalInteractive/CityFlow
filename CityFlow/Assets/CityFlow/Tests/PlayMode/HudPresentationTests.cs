#nullable enable

using System.Collections;
using System.Linq;
using CityFlow.Application.Connections;
using CityFlow.Application.UseCases;
using CityFlow.Composition;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Presentation.Connections;
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
    public sealed class HudPresentationTests
    {
        [UnitySetUp] public IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("WiringLab"); yield return null;
            Object.FindAnyObjectByType<SimulationDriver>().enabled = false;
        }
        private static void Connect(ConnectionSession session, string from, string to)
        {
            Assert.That(session.Begin(from), Is.True); session.SelectTarget(to);
            Assert.That(session.Confirm(), Is.EqualTo(ConnectionFailure.None));
        }
        [UnityTest] public IEnumerator WaveClockAndPreparationHintRemainAfterTheArrivalNotice()
        {
            var scope = Object.FindAnyObjectByType<CityFlowLifetimeScope>();
            var simulation = scope.Container.Resolve<FlowSimulation>();
            var session = scope.Container.Resolve<ConnectionSession>();
            Connect(session, "S1", "RED"); Connect(session, "S1", "BLUE");
            simulation.Tick(60 - simulation.ElapsedSeconds); simulation.Tick(13); simulation.SetPaused(true);
            yield return null; yield return null;
            var root = Object.FindAnyObjectByType<UIDocument>().rootVisualElement;
            Assert.That(root.Q("line-rows"), Is.Null);
            Assert.That(root.Q<Label>("elapsed-value").text, Is.EqualTo("1:13"));
            Assert.That(root.Q<Label>("wave-value").text, Is.EqualTo("2"));
            Assert.That(root.Q<Label>("wave-next").text, Is.EqualTo("NEXT 47s"));
            var delivered = root.Q("delivered-value"); var elapsed = root.Q("elapsed-value"); var wave = root.Q("wave-value");
            Assert.That(wave.resolvedStyle.fontSize, Is.EqualTo(delivered.resolvedStyle.fontSize));
            Assert.That(wave.resolvedStyle.fontSize, Is.EqualTo(elapsed.resolvedStyle.fontSize));
            Assert.That(wave.worldBound.center.y, Is.EqualTo(delivered.worldBound.center.y).Within(1));
            Assert.That(wave.worldBound.center.y, Is.EqualTo(elapsed.worldBound.center.y).Within(1));
            Assert.That(delivered.worldBound.xMax, Is.LessThan(elapsed.worldBound.xMin));
            Assert.That(elapsed.worldBound.xMax, Is.LessThan(wave.worldBound.xMin));
            Assert.That(root.Q<Label>("context-hint").text, Does.Contain("S2").And.Contain("7s"));
            Assert.That(root.Q("wave-transition").resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            string clock = root.Q<Label>("wave-value").text;
            simulation.Tick(100); yield return null;
            Assert.That(root.Q<Label>("wave-value").text, Is.EqualTo(clock));
        }
        [UnityTest] public IEnumerator HoverUsesStructuredSectionsColorInitialsAndATargetLeader()
        {
            var scope = Object.FindAnyObjectByType<CityFlowLifetimeScope>();
            var network = scope.Container.Resolve<FlowNetwork>();
            network.GenerateFlow("S1", FlowColor.Red); network.GenerateFlow("S1", FlowColor.Blue);
            var overview = Object.FindAnyObjectByType<OverviewController>();
            overview.Hover(Camera.main.WorldToScreenPoint(network.NodeDefinitions.Single(n => n.Id == "S1").Position + Vector3.up * 1.4f));
            yield return null; yield return null;
            var root = Object.FindAnyObjectByType<UIDocument>().rootVisualElement;
            Assert.That(root.Q<Label>("hover-title"), Is.Not.Null);
            Assert.That(root.Q<Label>("hover-title").text, Does.Contain("S1"));
            Assert.That(root.Q<Label>("hover-primary").text, Does.Contain("BUFFER 2/10"));
            Assert.That(root.Q<Label>("hover-metrics").text, Does.Contain("OUT ").And.Not.Contain("IN "));
            Assert.That(root.Q("hover-leader").resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(root.Q("hover-buffer").Children().OfType<Label>().Take(2).Select(l => l.text), Is.EqualTo(new[] { "R", "B" }));
            Assert.That(root.Q("node-gauge-S1").Children().OfType<Label>().Take(2).Select(l => l.text), Is.EqualTo(new[] { "R", "B" }));
            overview.Hover(new Vector2(-100, -100)); yield return null;
            Assert.That(root.Q("node-tooltip").resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            Assert.That(root.Q("hover-leader").resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
        }
        [UnityTest] public IEnumerator WaveSummaryReplacesNodePanelsAndKeepsPauseClickable()
        {
            var scope = Object.FindAnyObjectByType<CityFlowLifetimeScope>();
            var session = scope.Container.Resolve<ConnectionSession>();
            Connect(session, "S1", "RED"); Connect(session, "S1", "BLUE");
            var simulation = scope.Container.Resolve<FlowSimulation>(); simulation.Tick(60.7 - simulation.ElapsedSeconds);
            simulation.SetPaused(true); yield return null; yield return null;
            var root = Object.FindAnyObjectByType<UIDocument>().rootVisualElement;
            Assert.That(root.Q("wave-notice"), Is.Null);
            Assert.That(root.Q("arrival-markers"), Is.Null);
            Assert.That(root.Query(className: "arrival-marker").ToList(), Is.Empty);
            var summary = root.Q<Label>("wave-transition-additions");
            Assert.That(summary, Is.Not.Null);
            Assert.That(summary.text, Is.EqualTo("SOURCE +1  ·  SINK +1"));
            Assert.That(root.Q<Label>("wave-transition-details").text, Does.Contain("GREEN").And.Not.Contain("RED"));
            var card = root.Q("wave-transition-card");
            Assert.That(card.worldBound.Overlaps(root.Q(className: "session-controls").worldBound), Is.False);
            Assert.That(summary.pickingMode, Is.EqualTo(PickingMode.Ignore));
            var pause = root.Q<Button>("pause-toggle");
            var hit = root.panel.Pick(pause.worldBound.center);
            Assert.That(hit == pause || pause.Contains(hit), Is.True);
            UiPointer.Click(pause); yield return null;
            Assert.That(simulation.IsPaused, Is.False);
        }
        [UnityTest] public IEnumerator CandidateGroupsExcludeSelfAndAllMarkersRemainVisible()
        {
            var scope = Object.FindAnyObjectByType<CityFlowLifetimeScope>();
            var session = scope.Container.Resolve<ConnectionSession>();
            var controller = Object.FindAnyObjectByType<NodeConnectionController>();
            Object.FindAnyObjectByType<OverviewController>().Select(OverviewTarget.Node("S1"));
            controller.BeginSelected(); controller.FocusTarget("BLUE"); yield return null; yield return null; yield return null;
            var root = Object.FindAnyObjectByType<UIDocument>().rootVisualElement;
            Assert.That(root.Query<Label>(className: "candidate-group-title").ToList().Count, Is.GreaterThan(0));
            foreach (var candidate in session.Candidates())
            {
                string id = candidate.Node.Definition.Id;
                Assert.That(id, Is.Not.EqualTo("S1"));
                Assert.That(root.Q("candidate-" + id).resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            }
            var swatch = root.Q("candidate-option-BLUE").Q<Label>(className: "candidate-swatch");
            Assert.That(swatch.text, Is.EqualTo("B"));
            Assert.That(swatch.resolvedStyle.backgroundColor, Is.EqualTo(ValidationCityView.ColorFor(FlowColor.Blue)));
        }
    }
}
