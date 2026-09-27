#nullable enable

using System;
using System.Collections;
using System.Linq;
using CityFlow.Application.Connections;
using CityFlow.Application.UseCases;
using CityFlow.Composition;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Domain.Spatial;
using CityFlow.Presentation.Connections;
using CityFlow.Presentation.Overview;
using CityFlow.Presentation.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using VContainer;
using Object = UnityEngine.Object;

namespace CityFlow.Tests.PlayMode
{
    public sealed class NodeArrivalTests
    {
        private Mouse[] mice = Array.Empty<Mouse>();
        private static T Resolve<T>() => Object.FindAnyObjectByType<CityFlowLifetimeScope>().Container.Resolve<T>();
        private static VisualElement Root => Object.FindAnyObjectByType<UIDocument>().rootVisualElement;
        private static MeshRenderer[] Beacons() => Object.FindObjectsByType<MeshRenderer>()
            .Where(r => r.name.StartsWith("Node beacon ", StringComparison.Ordinal)).ToArray();
        private static void Connect(string from, string to)
        {
            var session = Resolve<ConnectionSession>();
            Assert.That(session.Begin(from), Is.True);
            Assert.That(session.SelectTarget(to), Is.True);
            Assert.That(session.Confirm(), Is.EqualTo(ConnectionFailure.None));
        }

        [UnitySetUp] public IEnumerator Load()
        {
            mice = InputSystem.devices.OfType<Mouse>().Where(m => m.enabled).ToArray();
            foreach (var mouse in mice) InputSystem.DisableDevice(mouse);
            yield return SceneManager.LoadSceneAsync("WiringLab"); yield return null;
            Object.FindAnyObjectByType<SimulationDriver>().enabled = false;
            Resolve<FlowSimulation>().SetPaused(true);
            yield return null; yield return null;
        }
        [TearDown] public void RestoreInput()
        { foreach (var mouse in mice) if (mouse.added) InputSystem.EnableDevice(mouse); }

        [UnityTest] public IEnumerator InitialNodesAppearImmediatelyAndPauseResumesTheSameAppearanceWithoutChangingTransport()
        {
            var network = Resolve<FlowNetwork>(); var sim = Resolve<FlowSimulation>();
            Assert.That(Beacons().Length, Is.EqualTo(network.NodeDefinitions.Count), "Initial Nodes need automatic location beams.");
            Assert.That(Root.Q<Label>("wave-notice").text, Does.Contain("INITIAL NODES"));
            Assert.That(Root.Q("wave-notice").resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(Root.Q<Button>("node-highlight-toggle").ClassListContains("chosen"), Is.False);
            foreach (var node in network.NodeDefinitions)
            {
                var beacon = Beacons().Single(r => r.name == "Node beacon " + node.Id);
                Assert.That(beacon.bounds.min.y, Is.EqualTo(node.Position.y).Within(0.001));
                Assert.That(beacon.GetComponent<Collider>(), Is.Null);
                Color color = node.SinkColor.HasValue ? ValidationCityView.ColorFor(node.SinkColor.Value) : Color.white;
                Color tint = beacon.sharedMaterial.GetColor("_BaseColor");
                Assert.That(Vector3.Distance(new Vector3(tint.r, tint.g, tint.b), new Vector3(color.r, color.g, color.b)), Is.LessThan(0.00001f));
                Assert.That(Root.Q<Button>("arrival-" + node.Id).text, Does.Contain(node.Kind.ToString().ToUpperInvariant()));
            }
            // Connections are usable immediately; the presentation owns no transport state.
            Connect("S1", "RED"); Connect("S1", "BLUE"); yield return null; yield return null;
            var body = GameObject.Find("S1 / Source").transform;
            Vector3 scale = body.localScale;
            Color glow = Beacons().First().sharedMaterial.GetColor("_EdgeColor");
            var before = network.Snapshot(); double ready = sim.SourceStartRemaining("S1");
            sim.Tick(30); yield return null; yield return null;
            Assert.That(body.localScale, Is.EqualTo(scale));
            Assert.That(Beacons().First().sharedMaterial.GetColor("_EdgeColor"), Is.EqualTo(glow));
            Assert.That(network.Snapshot(), Is.SameAs(before));
            Assert.That(sim.SourceStartRemaining("S1"), Is.EqualTo(ready));
            UiPointer.Click(Root.Q<Button>("pause-toggle")); sim.Tick(1); yield return null; yield return null;
            Assert.That(body.localScale, Is.Not.EqualTo(scale));
            Assert.That(sim.SourceStartRemaining("S1"), Is.EqualTo(ready - 1).Within(0.001));
            sim.Tick(6 - sim.ElapsedSeconds); yield return null; yield return null;
            float alpha = Beacons().First().sharedMaterial.GetColor("_BaseColor").a;
            sim.Tick(1); yield return null; yield return null;
            Assert.That(Beacons().First().sharedMaterial.GetColor("_BaseColor").a, Is.LessThan(alpha), "Beams fade before disappearing.");
            sim.Tick(8 - sim.ElapsedSeconds); sim.SetPaused(true); yield return null; yield return null;
            Assert.That(Beacons(), Is.Empty);
            Assert.That(body.localScale, Is.EqualTo(new Vector3(3.3f, 2.8f, 3.3f)));
            UiPointer.Click(Root.Q<Button>("node-highlight-toggle")); yield return null; yield return null;
            Assert.That(Beacons().Length, Is.EqualTo(network.NodeDefinitions.Count), "Manual Nodes mode still works after arrivals finish.");
            UiPointer.Click(Root.Q<Button>("node-highlight-toggle")); yield return null; yield return null;
            Assert.That(Beacons(), Is.Empty, "Turning off manual emphasis must not restart arrivals.");
        }

        [UnityTest] public IEnumerator StationWaveHighlightsOnlyNewElevatedNodesAndKeepsOffscreenFocusAvailable()
        {
            yield return SceneManager.LoadSceneAsync("TokyoStationWiringLab"); yield return null;
            Object.FindAnyObjectByType<SimulationDriver>().enabled = false;
            var sim = Resolve<FlowSimulation>(); var network = Resolve<FlowNetwork>();
            Connect("S1", "R1"); Connect("R1", "RED"); Connect("R1", "BLUE");
            sim.Tick(59.95 - sim.ElapsedSeconds); yield return null; yield return null;
            Assert.That(Beacons(), Is.Empty);
            var oldBody = GameObject.Find("S1 / Source");
            sim.Tick(0.05); sim.SetPaused(true); yield return null; yield return null;
            Assert.That(Beacons().Select(r => r.name), Is.EquivalentTo(new[] { "Node beacon S2", "Node beacon R2", "Node beacon GREEN" }));
            Assert.That(GameObject.Find("S1 / Source"), Is.SameAs(oldBody));
            foreach (var node in sim.LatestAdditions)
            {
                var beacon = Beacons().Single(r => r.name == "Node beacon " + node.Id);
                Assert.That(beacon.bounds.min.y, Is.EqualTo(node.Position.y).Within(0.001));
                Assert.That(beacon.bounds.max.y, Is.GreaterThan(Resolve<StageDefinition>().Buildings.Max(b => b.max.y)));
            }
            Assert.That(sim.SourceStartRemaining("S2"), Is.EqualTo(20).Within(0.001));
            Assert.That(network.Snapshot().Nodes.Single(n => n.Definition.Id == "S2").GeneratedCount, Is.Zero);
            var state = network.Snapshot();
            Assert.That(state.GeneratedCount, Is.EqualTo(state.DeliveredCount + state.Nodes.Sum(n => n.Buffer.Count) + state.Lines.Sum(l => l.InFlight.Count)));
            var camera = Camera.main; camera.transform.rotation = Quaternion.LookRotation(-camera.transform.forward);
            yield return null; yield return null;
            var marker = Root.Q<Button>("arrival-S2");
            Assert.That(marker.text, Does.Contain("OFFSCREEN"));
            Assert.That(Root.worldBound.Contains(marker.worldBound.center), Is.True);
            UiPointer.Click(marker); yield return null; yield return null;
            var overview = Object.FindAnyObjectByType<OverviewController>();
            Assert.That(overview.Selected.NodeId, Is.EqualTo("S2"));
            var controller = Object.FindAnyObjectByType<NodeConnectionController>();
            controller.BeginSelected(); yield return null; yield return null;
            Assert.That(Beacons(), Is.Empty, "Connection views keep their normal preview presentation.");
            controller.CancelSelection(); yield return null; yield return null;
            Assert.That(Beacons().Length, Is.EqualTo(3), "Returning while paused restores the same arrivals.");
            Assert.That(network.Snapshot(), Is.SameAs(state));
        }

        [UnityTest] public IEnumerator RetryReplaysInitialArrivalWithoutKeepingOldBeamsOrWaveMarkers()
        {
            var sim = Resolve<FlowSimulation>();
            Assert.That(Beacons(), Is.Not.Empty);
            sim.SetPaused(false); sim.Tick(1000); yield return null; yield return null;
            Assert.That(sim.Result, Is.Not.Null);
            Assert.That(Beacons(), Is.Empty);
            var oldScope = Object.FindAnyObjectByType<CityFlowLifetimeScope>();
            UiPointer.Click(Root.Q<Button>("retry-session"));
            CityFlowLifetimeScope? next = null;
            for (int frame = 0; frame < 120; frame++)
            {
                yield return null;
                next = Object.FindAnyObjectByType<CityFlowLifetimeScope>();
                if (next != null && next != oldScope && Object.FindAnyObjectByType<SimulationDriver>() != null) break;
            }
            Assert.That(next != null && next != oldScope, Is.True);
            Object.FindAnyObjectByType<SimulationDriver>().enabled = false;
            Resolve<FlowSimulation>().SetPaused(true); yield return null; yield return null;
            Assert.That(Beacons().Length, Is.EqualTo(Resolve<FlowNetwork>().NodeDefinitions.Count));
            Assert.That(Root.Q<Label>("wave-notice").text, Does.Contain("INITIAL NODES"));
            Assert.That(Resolve<FlowNetwork>().Snapshot().Lines, Is.Empty);
        }
    }
}
