#nullable enable

using System;
using System.Collections;
using System.Linq;
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
    public sealed class NodeHighlightTests
    {
        private Mouse[] mice = Array.Empty<Mouse>();
        private static T Resolve<T>() => Object.FindAnyObjectByType<CityFlowLifetimeScope>().Container.Resolve<T>();
        private static VisualElement Root => Object.FindAnyObjectByType<UIDocument>().rootVisualElement;
        private static MeshRenderer[] Beacons() => Object.FindObjectsByType<MeshRenderer>()
            .Where(r => r.name.StartsWith("Node beacon ", StringComparison.Ordinal)).ToArray();
        private static Button Toggle => Root.Q<Button>("node-highlight-toggle") ?? throw new AssertionException("Pause controls need a Node highlight button.");

        [UnitySetUp] public IEnumerator Load()
        {
            mice = InputSystem.devices.OfType<Mouse>().Where(m => m.enabled).ToArray();
            foreach (var mouse in mice) InputSystem.DisableDevice(mouse);
            yield return SceneManager.LoadSceneAsync("Bootstrap"); yield return null;
            Object.FindAnyObjectByType<SimulationDriver>().enabled = false;
            // Test the manual mode after automatic initial arrivals have finished.
            Resolve<FlowSimulation>().Tick(8);
            yield return null;
        }

        [TearDown] public void RestoreInput()
        { foreach (var mouse in mice) if (mouse.added) InputSystem.EnableDevice(mouse); }

        private static void AssertDimmed(Renderer renderer, bool dimmed, string property = "_BaseColor")
        {
            var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block);
            Color baseline = renderer.sharedMaterial.GetColor(property);
            Color displayed = block.isEmpty ? baseline : block.GetColor(property);
            if (dimmed) Assert.That(displayed.maxColorComponent, Is.LessThan(baseline.maxColorComponent * 0.3f), renderer.name);
            else Assert.That(displayed, Is.EqualTo(baseline), renderer.name);
        }

        [UnityTest] public IEnumerator PausedToggleHighlightsEveryNodeAndDimsTransportWithoutChangingTheNetwork()
        {
            var network = Resolve<FlowNetwork>(); var sim = Resolve<FlowSimulation>();
            sim.SetPaused(false); yield return null;
            Assert.That(Toggle.enabledInHierarchy, Is.False);
            var flow = network.Snapshot().Lines.SelectMany(line => line.InFlight).First().Flow;
            sim.SetPaused(true); yield return null;
            Assert.That(Toggle.enabledInHierarchy, Is.True);
            var before = network.Snapshot(); double elapsed = sim.ElapsedSeconds;
            UiPointer.Click(Toggle); yield return null; yield return null;
            Assert.That(Toggle.ClassListContains("chosen"), Is.True);
            Assert.That(Beacons().Length, Is.EqualTo(before.Nodes.Count));
            var overview = Object.FindAnyObjectByType<OverviewController>();
            overview.Hover(Camera.main.WorldToScreenPoint(network.NodeDefinitions.Single(n => n.Id == "R1").Position));
            yield return null; yield return null;
            foreach (var node in network.NodeDefinitions)
                AssertDimmed(GameObject.Find(node.Id + " / " + node.Kind).GetComponent<Renderer>(), false);
            AssertDimmed(GameObject.Find("Ground").GetComponent<Renderer>(), true);
            AssertDimmed(GameObject.Find("Building").GetComponent<Renderer>(), true, "_EdgeColor");
            foreach (var line in before.Lines)
                AssertDimmed(GameObject.Find($"Line {line.Id}: {line.SourceId} -> {line.DestinationId}").GetComponent<Renderer>(), true);
            AssertDimmed(GameObject.Find($"FLOW {flow.Id} / {flow.Color}").GetComponent<Renderer>(), true);
            sim.Tick(10);
            Assert.That(network.Snapshot(), Is.SameAs(before));
            Assert.That(sim.ElapsedSeconds, Is.EqualTo(elapsed));
            using (var submit = NavigationSubmitEvent.GetPooled()) Toggle.SendEvent(submit);
            Assert.That(Toggle.ClassListContains("chosen"), Is.True, "Keyboard submit must not toggle the display.");

            overview.Hover(new Vector2(-100, -100));
            UiPointer.Click(Toggle); yield return null; yield return null;
            Assert.That(Beacons(), Is.Empty);
            AssertDimmed(GameObject.Find("Ground").GetComponent<Renderer>(), false);
            UiPointer.Click(Toggle); yield return null;
            UiPointer.Click(Root.Q<Button>("pause-toggle")); yield return null; yield return null;
            Assert.That(sim.IsPaused, Is.False);
            Assert.That(Beacons(), Is.Empty);
            Assert.That(Toggle.enabledInHierarchy, Is.False);
            UiPointer.Click(Root.Q<Button>("pause-toggle")); yield return null;
            Assert.That(Toggle.ClassListContains("chosen"), Is.False, "A later Pause starts with the normal display.");
            UiPointer.Click(Toggle); yield return null;
            foreach (var line in network.Snapshot().Lines) network.RequestDeletion(line.Id);
            sim.SetPaused(false); sim.Tick(1000); yield return null; yield return null;
            Assert.That(sim.Result, Is.Not.Null);
            Assert.That(Beacons(), Is.Empty);
            Assert.That(Toggle.enabledInHierarchy, Is.False);
        }

        [UnityTest] public IEnumerator ElevatedAndLaterNodesGetSelectableBeaconsAndWiringTemporarilyRestoresNormalDisplay()
        {
            yield return SceneManager.LoadSceneAsync("HeightLab"); yield return null;
            Object.FindAnyObjectByType<SimulationDriver>().enabled = false;
            var network = Resolve<FlowNetwork>(); var sim = Resolve<FlowSimulation>();
            sim.SetPaused(true); yield return null;
            UiPointer.Click(Toggle); yield return null; yield return null;
            var roof = new SourceNodeDefinition("ROOF", new Vector3(52, 24, 30));
            Assert.That(network.TryAddNodes(new NodeDefinition[] { roof,
                new SinkNodeDefinition("ROOF-SINK", new Vector3(40, 24, 19), FlowColor.Red) }), Is.True);
            yield return null; yield return null;
            Assert.That(Beacons().Length, Is.EqualTo(network.NodeDefinitions.Count));
            foreach (var node in network.NodeDefinitions)
            {
                var beacon = Beacons().Single(r => r.name == "Node beacon " + node.Id);
                Assert.That(beacon.bounds.min.y, Is.EqualTo(node.Position.y).Within(0.001f));
                Assert.That(beacon.bounds.max.y, Is.GreaterThan(Resolve<StageDefinition>().CeilingHeight));
                Assert.That(beacon.GetComponent<Collider>(), Is.Null);
            }
            var camera = Camera.main;
            camera.transform.SetPositionAndRotation(new Vector3(52, 36, -70), Quaternion.identity);
            camera.orthographicSize = 10;
            var overview = Object.FindAnyObjectByType<OverviewController>();
            var target = overview.Pick(camera.WorldToScreenPoint(new Vector3(52, 36, 30)));
            Assert.That(target.NodeId, Is.EqualTo(roof.Id), "The beacon selects the fixed-height Source itself.");
            overview.Select(target);
            var controller = Object.FindAnyObjectByType<NodeConnectionController>();
            controller.BeginSelected(); yield return null; yield return null;
            Assert.That(controller.IsNode360, Is.True);
            Assert.That(Beacons(), Is.Empty);
            Assert.That(Toggle.enabledInHierarchy, Is.False);
            controller.CancelSelection(); yield return null; yield return null;
            Assert.That(Beacons().Length, Is.EqualTo(network.NodeDefinitions.Count));
        }

        [UnityTest] public IEnumerator RecreatedHudRebindsTheButtonWithoutDuplicateClicks()
        {
            Resolve<FlowSimulation>().SetPaused(true); yield return null;
            UiPointer.Click(Toggle); yield return null; yield return null;
            var document = Object.FindAnyObjectByType<UIDocument>();
            var previousRoot = document.rootVisualElement;
            // Recreate the tree atomically; sibling presenters require an enabled document.
            document.enabled = false;
            document.enabled = true; yield return null; yield return null;
            Assert.That(document.rootVisualElement, Is.Not.SameAs(previousRoot));
            UiPointer.Click(Toggle); yield return null; yield return null;
            Assert.That(Beacons(), Is.Empty, "One click after rebinding must turn the mode off.");
            UiPointer.Click(Toggle); yield return null; yield return null;
            Assert.That(Beacons().Length, Is.EqualTo(Resolve<FlowNetwork>().NodeDefinitions.Count));
            document.gameObject.SetActive(false); yield return null; yield return null;
            Assert.That(Beacons(), Is.Empty, "Disabling the HUD must not leave the city highlighted.");
        }
    }
}
