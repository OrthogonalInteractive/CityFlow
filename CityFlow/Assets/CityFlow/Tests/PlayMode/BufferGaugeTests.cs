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
using UnityEngine.UIElements;
using VContainer;

namespace CityFlow.Tests.PlayMode
{
    public sealed class BufferGaugeTests
    {
        [UnitySetUp] public IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("WiringLab"); yield return null;
            Object.FindAnyObjectByType<SimulationDriver>().enabled = false;
        }

        private static void AssertGauge(VisualElement root, string id, int capacity, NodeKind kind, params FlowColor[] colors)
        {
            var label = root.Q<Label>($"node-label-{id}");
            Assert.That(label.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(label.text, Does.Contain($"{colors.Length}/{capacity}"));
            Assert.That(label.text, Does.Not.Contain(id).And.Not.Contain(kind.ToString().ToUpperInvariant()));
            var gauge = label.Q(className: "node-gauge");
            Assert.That(gauge, Is.Not.Null);
            var slots = gauge.Children().ToArray();
            Assert.That(slots.Length, Is.EqualTo(System.Math.Max(capacity, colors.Length)));
            for (int i = 0; i < slots.Length; i++)
            {
                Assert.That(slots[i].pickingMode, Is.EqualTo(PickingMode.Ignore));
                Assert.That(slots[i].resolvedStyle.width, Is.GreaterThan(0));
                Assert.That(slots[i].resolvedStyle.height, Is.GreaterThanOrEqualTo(6));
                if (i < colors.Length)
                    Assert.That(slots[i].resolvedStyle.backgroundColor, Is.EqualTo(ValidationCityView.ColorFor(colors[i])),
                        "Full Buffer warning must preserve each FLOW's color.");
                else Assert.That(slots[i].ClassListContains("empty"), Is.True);
            }
            Assert.That(label.ClassListContains(kind == NodeKind.Source ? "source-overload" : "input-stopped"),
                Is.EqualTo(colors.Length >= capacity));
            if (kind == NodeKind.Source) Assert.That(label.ClassListContains("input-stopped"), Is.False);
        }

        [UnityTest] public IEnumerator WorldGaugesAppearOnlyWhileFlowsWaitInTheBuffer()
        {
            var scope = Object.FindAnyObjectByType<CityFlowLifetimeScope>();
            var network = scope.Container.Resolve<FlowNetwork>();
            var root = Object.FindAnyObjectByType<UIDocument>().rootVisualElement;
            foreach (var marker in root.Q("node-labels").Children())
                Assert.That(marker.resolvedStyle.display, Is.EqualTo(DisplayStyle.None), "Empty Nodes have no standing information card.");

            network.GenerateFlow("S1", FlowColor.Red);
            yield return null; yield return null;
            AssertGauge(root, "S1", 10, NodeKind.Source, FlowColor.Red);
            var source = network.NodeDefinitions.Single(n => n.Id == "S1").Position;
            var sink = network.NodeDefinitions.Single(n => n.Id == "RED").Position;
            Assert.That(network.TryConnect("S1", "RED", new[] { source, sink }).Succeeded, Is.True);
            network.RouteWaitingFlows(new SystemRandomSource(1));
            yield return null; yield return null;
            Assert.That(network.Snapshot().Lines.Single().InFlight.Count, Is.EqualTo(1));
            Assert.That(root.Q("node-label-S1").resolvedStyle.display, Is.EqualTo(DisplayStyle.None),
                "In-Flight FLOW must not keep an empty Buffer gauge visible.");
            Assert.That(root.Q("node-label-RED").resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
        }

        [UnityTest] public IEnumerator SourceGaugeShowsOrderedFlowColorsEmptyCapacityAndOverflowAfterRebinding()
        {
            var scope = Object.FindAnyObjectByType<CityFlowLifetimeScope>();
            var network = scope.Container.Resolve<FlowNetwork>();
            scope.Container.Resolve<FlowSimulation>().SetPaused(true);
            var document = Object.FindAnyObjectByType<UIDocument>();
            var colors = new[] { FlowColor.Red, FlowColor.Blue, FlowColor.Red };
            foreach (var color in colors) network.GenerateFlow("S1", color);
            yield return null; yield return null;
            AssertGauge(document.rootVisualElement, "S1", 10, NodeKind.Source, colors);
            Assert.That(document.rootVisualElement.Q("node-tooltip").resolvedStyle.display, Is.EqualTo(DisplayStyle.None));

            for (int i = 0; i < 7; i++) network.GenerateFlow("S1", FlowColor.Blue);
            network.GenerateFlow("S1", FlowColor.Red);
            document.gameObject.SetActive(false); document.gameObject.SetActive(true);
            yield return null; yield return null;
            AssertGauge(document.rootVisualElement, "S1", 10, NodeKind.Source,
                colors.Concat(Enumerable.Repeat(FlowColor.Blue, 7)).Append(FlowColor.Red).ToArray());
            Assert.That(document.rootVisualElement.Q("node-label-S1").Query(className: "node-gauge").ToList().Count, Is.EqualTo(1));
            Assert.That(document.rootVisualElement.Q("node-label-RED").Q(className: "node-gauge"), Is.Null);
        }

        [UnityTest] public IEnumerator FullRelayGaugePreservesColorsAndRefreshesAfterBufferedFlowsDepart()
        {
            var scope = Object.FindAnyObjectByType<CityFlowLifetimeScope>();
            var network = scope.Container.Resolve<FlowNetwork>();
            var source = network.NodeDefinitions.Single(n => n.Id == "S1").Position;
            var relay = network.NodeDefinitions.Single(n => n.Id == "R1").Position;
            var red = network.NodeDefinitions.Single(n => n.Id == "RED").Position;
            Assert.That(network.TryConnect("S1", "R1", new[] { source, relay }).Succeeded, Is.True);
            var random = new SystemRandomSource(1);
            var colors = new[] { FlowColor.Blue, FlowColor.Red, FlowColor.Blue, FlowColor.Red, FlowColor.Blue };
            foreach (var color in colors)
            {
                network.GenerateFlow("S1", color); network.RouteWaitingFlows(random); network.AdvanceInFlight(20);
            }
            yield return null; yield return null;
            var root = Object.FindAnyObjectByType<UIDocument>().rootVisualElement;
            AssertGauge(root, "R1", 5, NodeKind.Relay, colors);

            Assert.That(network.TryConnect("R1", "RED", new[] { relay, source, red }).Succeeded, Is.True);
            scope.Container.Resolve<FlowSimulation>().Tick(FlowSimulation.StepSeconds);
            yield return null; yield return null;
            AssertGauge(root, "R1", 5, NodeKind.Relay, FlowColor.Blue, FlowColor.Blue, FlowColor.Blue);
            var outgoing = network.Snapshot().Lines.Single(l => l.SourceId == "R1");
            Assert.That(outgoing.InFlight.Select(f => f.Flow.Color), Is.EqualTo(new[] { FlowColor.Red, FlowColor.Red }));
        }
    }
}
