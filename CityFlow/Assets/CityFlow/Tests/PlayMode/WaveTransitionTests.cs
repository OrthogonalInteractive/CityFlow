#nullable enable

using System.Collections;
using CityFlow.Application.Connections;
using CityFlow.Application.UseCases;
using CityFlow.Composition;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Domain.Spatial;
using CityFlow.Infrastructure.Routing;
using CityFlow.Presentation.Rendering;
using CityFlow.Presentation.Overview;
using CityFlow.Presentation.Connections;
using CityFlow.Tests.Fixtures;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using VContainer;
using Object = UnityEngine.Object;

namespace CityFlow.Tests.PlayMode
{
    public sealed class WaveTransitionTests
    {
        private static T Resolve<T>() => Object.FindAnyObjectByType<CityFlowLifetimeScope>().Container.Resolve<T>();
        private static UIDocument Document => Object.FindAnyObjectByType<UIDocument>();
        private static VisualElement Root => Document.rootVisualElement;
        private static VisualElement Transition => Root.Q("wave-transition");
        private static VisualElement Card => Root.Q("wave-transition-card");
        private static Label Title => Root.Q<Label>("wave-transition-title");
        [UnitySetUp] public IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("WiringLab"); yield return null;
            Object.FindAnyObjectByType<SimulationDriver>().enabled = false;
        }
        private static void Wire()
        {
            var planner = new LineRoutePlanner(Resolve<StageDefinition>(), Resolve<FlowNetwork>().Settings.Clearance);
            new GreedyNetworkWiring((a, b) => planner.Generate(a, b).Route).Extend(Resolve<FlowNetwork>());
        }
        private static IEnumerator Advance(double seconds)
        {
            Resolve<FlowSimulation>().Tick(seconds); yield return null; yield return null;
        }

        [UnityTest] public IEnumerator WaveTitleSlidesToCenterAndPauseAndRebindKeepItsProgress()
        {
            var sim = Resolve<FlowSimulation>(); var network = Resolve<FlowNetwork>();
            Wire(); yield return Advance(60 - sim.ElapsedSeconds);
            Assert.That(Transition, Is.Not.Null, "A Wave change needs a dedicated large title.");
            Assert.That(Title.text, Is.EqualTo("WAVE 2"));
            Assert.That(Card.worldBound.xMax, Is.LessThanOrEqualTo(Root.worldBound.xMin), "The title starts outside the left edge.");
            float startX = Card.worldBound.center.x;
            yield return Advance(0.2);
            Assert.That(Card.worldBound.center.x, Is.GreaterThan(startX));
            Assert.That(Card.worldBound.center.x, Is.LessThan(Root.worldBound.center.x));
            sim.SetPaused(true);
            Vector2 pausedPosition = Card.worldBound.center;
            float opacity = Transition.resolvedStyle.opacity;
            var snapshot = network.Snapshot(); double elapsed = sim.ElapsedSeconds;
            yield return Advance(20);
            Assert.That(Card.worldBound.center, Is.EqualTo(pausedPosition));
            Assert.That(Transition.resolvedStyle.opacity, Is.EqualTo(opacity));
            Assert.That(network.Snapshot(), Is.SameAs(snapshot)); Assert.That(sim.ElapsedSeconds, Is.EqualTo(elapsed));
            var hud = Document.gameObject;
            hud.SetActive(false); yield return null; hud.SetActive(true);
            yield return null; yield return null; yield return null;
            Assert.That(Vector2.Distance(Card.worldBound.center, pausedPosition), Is.LessThan(1));
            sim.SetPaused(false); yield return Advance(0.5);
            Assert.That(Vector2.Distance(Card.worldBound.center, Root.worldBound.center), Is.LessThan(1));
            Assert.That(Title.resolvedStyle.fontSize, Is.GreaterThan(Root.Q("wave-value").resolvedStyle.fontSize));
            Assert.That(Title.pickingMode, Is.EqualTo(PickingMode.Ignore));
            var picked = Root.panel.Pick(Card.worldBound.center);
            Assert.That(picked == Transition || (picked != null && Transition.Contains(picked)), Is.False);
            sim.SetPaused(true);
            Object.FindAnyObjectByType<OverviewController>().Select(OverviewTarget.Node("S1"));
            var controller = Object.FindAnyObjectByType<NodeConnectionController>();
            controller.BeginSelected(); yield return null; yield return null;
            Assert.That(controller.IsNode360, Is.True);
            Assert.That(Transition.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(Vector2.Distance(Card.worldBound.center, Root.worldBound.center), Is.LessThan(1));
            controller.CancelSelection(); sim.SetPaused(false);
            yield return Advance(2.5);
            Assert.That(Transition.resolvedStyle.opacity, Is.InRange(0.01f, 0.99f));
            yield return Advance(0.3);
            Assert.That(Transition.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
        }

        [UnityTest] public IEnumerator EveryWaveIncludingFinalAnnouncesOnceAndGameOverAndRetryResetTheTitle()
        {
            var sim = Resolve<FlowSimulation>(); var network = Resolve<FlowNetwork>();
            Assert.That(Title, Is.Not.Null);
            Assert.That(Title.text, Is.EqualTo("WAVE 1"));
            Wire();
            while (sim.NextWaveSeconds.HasValue)
            {
                yield return Advance(sim.NextWaveSeconds.Value - sim.ElapsedSeconds + 0.7);
                Assert.That(Title.text, Is.EqualTo($"WAVE {sim.Wave}"));
                Assert.That(Transition.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
                Assert.That(Vector2.Distance(Card.worldBound.center, Root.worldBound.center), Is.LessThan(1));
                Wire();
            }
            Assert.That(Root.Q<Label>("wave-value").text, Is.EqualTo("4"));
            Assert.That(Root.Q("wave-next").resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            // End the session during the final announcement; no Wave title may cover the result.
            for (int i = 0; i < network.Settings.SourceBufferCapacity; i++) network.GenerateFlow("S1", FlowColor.Red);
            network.EvaluateOverload(network.Settings.OverloadGrace); sim.Tick(0);
            yield return null; yield return null;
            Assert.That(Transition.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            Assert.That(Root.Q("result-overlay").resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            var scope = Object.FindAnyObjectByType<CityFlowLifetimeScope>();
            UiPointer.Click(Root.Q<Button>("retry-session"));
            for (int i = 0; i < 120; i++)
            {
                yield return null;
                var next = Object.FindAnyObjectByType<CityFlowLifetimeScope>();
                if (next != null && next != scope && Object.FindAnyObjectByType<SimulationDriver>() != null) break;
            }
            Object.FindAnyObjectByType<SimulationDriver>().enabled = false;
            yield return null; yield return null;
            Assert.That(Resolve<FlowSimulation>().Wave, Is.EqualTo(1));
            Assert.That(Title.text, Is.EqualTo("WAVE 1"));
            Assert.That(Transition.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
        }
    }
}
