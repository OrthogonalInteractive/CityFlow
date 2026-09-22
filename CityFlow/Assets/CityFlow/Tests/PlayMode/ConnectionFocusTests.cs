#nullable enable

using System;
using System.Collections;
using System.Linq;
using CityFlow.Application.UseCases;
using CityFlow.Composition;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Infrastructure.Configuration;
using CityFlow.Presentation.Connections;
using CityFlow.Presentation.Overview;
using CityFlow.Presentation.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;
using VContainer;

namespace CityFlow.Tests.PlayMode
{
    public sealed class ConnectionFocusTests
    {
        private Mouse[] mice = Array.Empty<Mouse>();
        private FlowNetwork Network => Object.FindAnyObjectByType<CityFlowLifetimeScope>().Container.Resolve<FlowNetwork>();
        private OverviewController Overview => Object.FindAnyObjectByType<OverviewController>();

        [UnitySetUp] public IEnumerator Load()
        {
            // Real pointer movement must not replace the deterministic hover under test.
            mice = InputSystem.devices.OfType<Mouse>().Where(mouse => mouse.enabled).ToArray();
            foreach (var mouse in mice) InputSystem.DisableDevice(mouse);
            yield return SceneManager.LoadSceneAsync("Bootstrap");
            yield return null;
            Object.FindAnyObjectByType<SimulationDriver>().enabled = false;
            Object.FindAnyObjectByType<CityFlowLifetimeScope>().Container.Resolve<FlowSimulation>().SetPaused(true);
        }
        [TearDown] public void RestoreInput()
        {
            foreach (var mouse in mice) if (mouse.added) InputSystem.EnableDevice(mouse);
        }
        private static Renderer Node(string id) => Object.FindObjectsByType<MeshRenderer>()
            .Single(renderer => renderer.name.StartsWith(id + " / ", StringComparison.Ordinal));
        private static Renderer Line(int id) => Object.FindObjectsByType<LineRenderer>()
            .Single(renderer => renderer.name.StartsWith("Line " + id + ":", StringComparison.Ordinal));
        private void Hover(string id) => Overview.Hover(Camera.main.WorldToScreenPoint(
            Network.NodeDefinitions.Single(node => node.Id == id).Position + Vector3.up * 1.4f));
        private static Color DisplayColor(Renderer renderer, string property = "_BaseColor")
        {
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            return block.isEmpty ? renderer.sharedMaterial.GetColor(property) : block.GetColor(property);
        }
        private static void AssertDimmed(Renderer renderer, bool dimmed, string property = "_BaseColor")
        {
            float baseline = renderer.sharedMaterial.GetColor(property).maxColorComponent;
            float displayed = DisplayColor(renderer, property).maxColorComponent;
            if (dimmed) Assert.That(displayed, Is.LessThan(baseline * 0.3f), renderer.name + " must recede from focus.");
            else Assert.That(displayed, Is.EqualTo(baseline).Within(0.001f), renderer.name + " must retain its original color.");
        }

        [UnityTest] public IEnumerator HoverKeepsOnlyIncidentLinesAndAdjacentNodesAndTracksDisconnection()
        {
            var before = Network.Snapshot();
            var building = Object.FindObjectsByType<MeshRenderer>().First(renderer => renderer.name == "Building");
            Color emission = building.sharedMaterial.GetColor("_EdgeColor");
            Hover("R1");
            yield return null; yield return null;
            foreach (string id in new[] { "S1", "R1", "R2" }) AssertDimmed(Node(id), false);
            foreach (string id in new[] { "RED", "BLUE" }) AssertDimmed(Node(id), true);
            foreach (var line in before.Lines)
                AssertDimmed(Line(line.Id), line.SourceId != "R1" && line.DestinationId != "R1");
            AssertDimmed(building, true, "_EdgeColor");
            AssertDimmed(GameObject.Find("Ground").GetComponent<Renderer>(), true);
            Assert.That(building.sharedMaterial.GetColor("_EdgeColor"), Is.EqualTo(emission), "Shared assets must not be recolored.");
            Assert.That(Network.Snapshot(), Is.SameAs(before), "Inspecting a Node must not mutate transport.");

            int outgoing = before.Lines.Single(line => line.SourceId == "R1" && line.DestinationId == "R2").Id;
            Assert.That(Network.RequestDeletion(outgoing), Is.True);
            yield return null; yield return null;
            AssertDimmed(Node("R2"), true);
            int incoming = before.Lines.Single(line => line.SourceId == "S1" && line.DestinationId == "R1").Id;
            Assert.That(Network.RequestDeletion(incoming), Is.True);
            yield return null; yield return null;
            AssertDimmed(Node("R1"), false);
            foreach (string id in new[] { "S1", "R2", "RED", "BLUE" }) AssertDimmed(Node(id), true);
            Overview.Hover(new Vector2(-100, -100));
            yield return null; yield return null;
            foreach (var node in Network.NodeDefinitions) AssertDimmed(Node(node.Id), false);
            AssertDimmed(building, false, "_EdgeColor");
        }

        [UnityTest] public IEnumerator FocusAlsoDimsUnrelatedInFlightAndSourceEffects()
        {
            var flow = Network.GenerateFlow("S1", FlowColor.Red);
            Network.RouteWaitingFlows();
            Network.GenerateFlow("S1", FlowColor.Blue);
            yield return null; yield return null;
            Hover("R2");
            yield return null; yield return null;
            AssertDimmed(GameObject.Find($"FLOW {flow.Id} / Red").GetComponent<Renderer>(), true);
            var waiting = GameObject.Find("Source buffer S1").GetComponentInChildren<Renderer>();
            AssertDimmed(waiting, true);
            AssertDimmed(GameObject.Find("Source generation S1").GetComponent<Renderer>(), true);
            var gauge = Object.FindAnyObjectByType<UIDocument>().rootVisualElement.Q("node-label-S1");
            Assert.That(gauge.resolvedStyle.opacity, Is.LessThan(0.3f));
            Hover("S1");
            yield return null; yield return null;
            AssertDimmed(waiting, false);
            AssertDimmed(GameObject.Find($"FLOW {flow.Id} / Red").GetComponent<Renderer>(), false);
            Assert.That(gauge.resolvedStyle.opacity, Is.EqualTo(1));
        }

        [UnityTest] public IEnumerator ExplicitFocusClearsOnHomeEmptySelectionAndConnectionMode()
        {
            Overview.Select(OverviewTarget.Node("R1"));
            Overview.FocusSelection();
            Overview.Hover(new Vector2(-100, -100));
            yield return null; yield return null;
            AssertDimmed(Node("RED"), true);
            Overview.ResetView();
            yield return null; yield return null;
            AssertDimmed(Node("RED"), false);
            Overview.FocusSelection();
            yield return null; yield return null;
            AssertDimmed(Node("RED"), true);
            Overview.Select(default);
            yield return null; yield return null;
            AssertDimmed(Node("RED"), false);
            Overview.Select(OverviewTarget.Node("R1"));
            Overview.FocusSelection();
            Object.FindAnyObjectByType<NodeConnectionController>().BeginSelected();
            yield return null; yield return null;
            AssertDimmed(Node("RED"), false, "_BaseColor");
            var building = Object.FindObjectsByType<MeshRenderer>().First(renderer => renderer.name == "Building");
            Assert.That(building.sharedMaterial.GetColor("_BaseColor").a, Is.LessThan(0.5f));
            AssertDimmed(building, false, "_EdgeColor");
        }
    }
}
