#nullable enable

using System;
using System.Collections;
using System.Linq;
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
using VContainer;
using Object = UnityEngine.Object;

namespace CityFlow.Tests.PlayMode
{
    public sealed class RelayHeightVisualTests
    {
        private Mouse[] mice = Array.Empty<Mouse>();
        private static T Resolve<T>() => Object.FindAnyObjectByType<CityFlowLifetimeScope>().Container.Resolve<T>();
        private static Renderer? Height(string id) => Object.FindObjectsByType<MeshRenderer>()
            .SingleOrDefault(renderer => renderer.name == "Relay height " + id);

        [UnitySetUp] public IEnumerator Load()
        {
            mice = InputSystem.devices.OfType<Mouse>().Where(mouse => mouse.enabled).ToArray();
            foreach (var mouse in mice) InputSystem.DisableDevice(mouse);
            yield return SceneManager.LoadSceneAsync("HeightLab");
            yield return null;
            Object.FindAnyObjectByType<SimulationDriver>().enabled = false;
        }

        [TearDown] public void RestoreInput()
        {
            foreach (var mouse in mice) if (mouse.added) InputSystem.EnableDevice(mouse);
        }

        [UnityTest] public IEnumerator HologramsSpanActualRelayLimitsIncludingLaterAndElevatedNodes()
        {
            var network = Resolve<FlowNetwork>();
            var stage = Resolve<StageDefinition>();
            // Later additions use the same path as Wave nodes; the elevated Relay is capped by the stage.
            Assert.That(network.TryAddNodes(new NodeDefinition[] {
                new RelayNodeDefinition("TALL", new Vector3(40, 0, 19), maximumRise: 26),
                new RelayNodeDefinition("ELEVATED", new Vector3(52, 24, 30), maximumRise: 22),
                new RelayNodeDefinition("FIXED", new Vector3(50, 0, 35)) }), Is.True);
            yield return null;
            foreach (var node in network.NodeDefinitions)
            {
                var renderer = Height(node.Id);
                float rise = stage.ConnectionCeiling(node) - node.Position.y;
                if (rise == 0) { Assert.That(renderer, Is.Null, node.Id); continue; }
                Assert.That(renderer, Is.Not.Null, node.Id + " must display its usable height.");
                if (renderer == null) continue;
                Assert.That(renderer.bounds.min.y, Is.EqualTo(node.Position.y).Within(0.001f));
                Assert.That(renderer.bounds.max.y, Is.EqualTo(stage.ConnectionCeiling(node)).Within(0.001f));
                Assert.That(renderer.bounds.center.x, Is.EqualTo(node.Position.x).Within(0.001f));
                Assert.That(renderer.bounds.center.z, Is.EqualTo(node.Position.z).Within(0.001f));
                Assert.That(renderer.GetComponent<Collider>(), Is.Null, "The projection must not block routing or picking.");
                Assert.That(renderer.sharedMaterial.shader.isSupported, Is.True);
                Assert.That(renderer.sharedMaterial.renderQueue, Is.GreaterThanOrEqualTo(3000));
            }
            var overview = Object.FindAnyObjectByType<OverviewController>();
            var relay = network.NodeDefinitions.Single(node => node.Id == "R2");
            Assert.That(overview.Pick(Camera.main.WorldToScreenPoint(relay.Position + Vector3.up * 1.4f)),
                Is.EqualTo(OverviewTarget.Node("R2")));
        }

        [UnityTest] public IEnumerator HologramsFollowConnectionFocusAndNode360Visibility()
        {
            var overview = Object.FindAnyObjectByType<OverviewController>();
            var controller = Object.FindAnyObjectByType<NodeConnectionController>();
            var renderer = Height("R2");
            Assert.That(renderer, Is.Not.Null);
            if (renderer == null) yield break;
            var network = Resolve<FlowNetwork>();
            var before = network.Snapshot();
            Color normal = renderer.sharedMaterial.GetColor("_BaseColor");
            overview.Select(OverviewTarget.Node("R1"));
            overview.FocusSelection();
            yield return null; yield return null;
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            Assert.That(block.GetColor("_BaseColor").r, Is.LessThan(normal.r * 0.3f));
            Assert.That(block.GetColor("_BaseColor").a, Is.EqualTo(normal.a));
            Assert.That(network.Snapshot(), Is.SameAs(before));

            overview.Select(OverviewTarget.Node("R2"));
            controller.BeginSelected();
            yield return null; yield return null;
            Assert.That(renderer.gameObject.activeSelf, Is.False, "The source projection must not cover the Node 360 camera.");
            Assert.That(Height("R1"), Is.Not.Null);
            controller.CancelSelection();
            yield return null; yield return null;
            Assert.That(renderer.gameObject.activeSelf, Is.True);
            renderer.GetPropertyBlock(block);
            Assert.That(block.isEmpty, Is.True);
            Assert.That(renderer.sharedMaterial.GetColor("_BaseColor"), Is.EqualTo(normal));
        }
    }
}
