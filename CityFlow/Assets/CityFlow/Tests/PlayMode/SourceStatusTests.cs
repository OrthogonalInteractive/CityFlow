#nullable enable
using System.Collections;
using System.Linq;
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
    public sealed class SourceStatusTests
    {
        [UnitySetUp] public IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("WiringLab"); yield return null;
            Object.FindAnyObjectByType<SimulationDriver>().enabled=false;
        }
        [UnityTest] public IEnumerator GenerationVisualsAndHoveredDetailsFreezeWithPause()
        {
            var scope=Object.FindAnyObjectByType<CityFlowLifetimeScope>(); var sim=scope.Container.Resolve<FlowSimulation>();
            var network=scope.Container.Resolve<FlowNetwork>(); sim.Tick(18-sim.ElapsedSeconds); yield return null; yield return null;
            var root=Object.FindAnyObjectByType<UIDocument>().rootVisualElement;
            var overview=Object.FindAnyObjectByType<OverviewController>();
            overview.Hover(Camera.main.WorldToScreenPoint(network.NodeDefinitions.Single(n=>n.Id=="S1").Position+Vector3.up*1.4f));
            yield return null;
            var detail=root.Q<Label>("hover-detail");
            Assert.That(detail.text,Does.Contain("BUFFER 1/10").And.Contain("GENERATED 1"));
            Assert.That(root.Q("source-monitor"),Is.Null);
            Assert.That(GameObject.Find("Source buffer S1").transform.Cast<Transform>().Count(t=>t.gameObject.activeSelf),Is.EqualTo(1));
            var pulse=GameObject.Find("Source generation S1"); Assert.That(pulse.GetComponent<LineRenderer>().enabled,Is.True);
            sim.SetPaused(true); var scale=pulse.transform.localScale; sim.Tick(10); yield return null; yield return null;
            Assert.That(detail.text,Does.Contain("BUFFER 1/10")); Assert.That(pulse.transform.localScale,Is.EqualTo(scale));
            Assert.That(network.Snapshot().GeneratedCount,Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator SinkHasNoBufferGaugeOrBufferReadout()
        {
            var scope=Object.FindAnyObjectByType<CityFlowLifetimeScope>();
            var network=scope.Container.Resolve<FlowNetwork>();
            var root=Object.FindAnyObjectByType<UIDocument>().rootVisualElement;
            yield return null;
            Assert.That(root.Q("node-rows"),Is.Null);
            Assert.That(root.Q("node-label-RED").Q(className:"node-gauge"),Is.Null);
            Assert.That(root.Q<Label>("node-label-RED").text,Does.Not.Contain("0/"));
            string detail=OverviewReadout.Describe(OverviewTarget.Node("RED"),network.Snapshot(),network.Settings);
            Assert.That(detail,Does.Not.Contain("BUFFER").And.Contain("CONSUME"));
        }
        [UnityTest] public IEnumerator HoveredOverloadCountdownFreezesAndWiringEndsInGameOver()
        {
            var scope=Object.FindAnyObjectByType<CityFlowLifetimeScope>(); var sim=scope.Container.Resolve<FlowSimulation>();
            var network=scope.Container.Resolve<FlowNetwork>();
            for(int i=0;i<10;i++) network.GenerateFlow("S1",FlowColor.Red);
            var overview=Object.FindAnyObjectByType<OverviewController>();
            overview.Hover(Camera.main.WorldToScreenPoint(network.NodeDefinitions.Single(n=>n.Id=="S1").Position+Vector3.up*1.4f));
            sim.Tick(1); yield return null; yield return null;
            var root=Object.FindAnyObjectByType<UIDocument>().rootVisualElement;
            var warning=root.Q<Label>("hover-detail"); Assert.That(warning,Is.Not.Null);
            Assert.That(warning.text,Does.Contain("4.0s").And.Contain("GAME OVER"));
            Assert.That(warning.text,Does.Contain("BUFFER 10/10"));
            sim.SetPaused(true); sim.Tick(20); yield return null;
            Assert.That(warning.text,Does.Contain("4.0s")); Assert.That(network.IsGameOver,Is.False);
            overview.Select(OverviewTarget.Node("S1")); Object.FindAnyObjectByType<NodeConnectionController>().BeginSelected();
            sim.SetPaused(false); sim.Tick(4); yield return null; yield return null;
            Assert.That(network.IsGameOver,Is.True); Assert.That(root.Q("result-overlay").resolvedStyle.display,Is.EqualTo(DisplayStyle.Flex));
            Assert.That(root.Q<Label>("result-title").text,Does.Contain("GAME OVER"));
            Assert.That(root.Q<Label>("result-detail").text,Does.Contain("S1"));
            Assert.That(Object.FindAnyObjectByType<NodeConnectionController>().IsNode360,Is.False);
        }
    }
}
