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
        [UnityTest] public IEnumerator GenerationBufferAndPauseRemainVisibleInNode360()
        {
            var scope=Object.FindAnyObjectByType<CityFlowLifetimeScope>(); var sim=scope.Container.Resolve<FlowSimulation>();
            var network=scope.Container.Resolve<FlowNetwork>(); sim.Tick(16-sim.ElapsedSeconds); yield return null; yield return null;
            var root=Object.FindAnyObjectByType<UIDocument>().rootVisualElement;
            var bar=root.Q<ProgressBar>("source-buffer-S1"); Assert.That(bar,Is.Not.Null,"Source needs a persistent buffer display.");
            Assert.That(bar.value,Is.EqualTo(1)); Assert.That(bar.title,Does.Contain("1 / 50"));
            Assert.That(root.Q<Label>("source-generation-S1").text,Does.Contain("GENERATED").And.Contain("1"));
            Assert.That(GameObject.Find("Source buffer S1").transform.Cast<Transform>().Count(t=>t.gameObject.activeSelf),Is.EqualTo(1));
            var pulse=GameObject.Find("Source generation S1"); Assert.That(pulse,Is.Not.Null); Assert.That(pulse.GetComponent<LineRenderer>().enabled,Is.True);
            sim.SetPaused(true); var scale=pulse.transform.localScale; sim.Tick(10); yield return null; yield return null;
            Assert.That(bar.value,Is.EqualTo(1)); Assert.That(pulse.transform.localScale,Is.EqualTo(scale));
            Object.FindAnyObjectByType<OverviewController>().Select(OverviewTarget.Node("S1"));
            Object.FindAnyObjectByType<NodeConnectionController>().BeginSelected(); yield return null; yield return null;
            Assert.That(root.Q("source-monitor").resolvedStyle.display,Is.EqualTo(DisplayStyle.Flex));
            Assert.That(bar.worldBound.height,Is.GreaterThan(0));
            Rect view=new Rect(0,(1-Camera.main.rect.yMax)*root.layout.height,Camera.main.rect.width*root.layout.width,Camera.main.rect.height*root.layout.height);
            Assert.That(root.Q("source-monitor").worldBound.Overlaps(view),Is.False,"Source status must not hide the city during wiring.");
            Assert.That(network.Snapshot().GeneratedCount,Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator OverloadCountdownIsVisibleDuringWiringAndEndsInGameOver()
        {
            var scope=Object.FindAnyObjectByType<CityFlowLifetimeScope>(); var sim=scope.Container.Resolve<FlowSimulation>();
            var network=scope.Container.Resolve<FlowNetwork>();
            for(int i=0;i<50;i++) network.GenerateFlow("S1",FlowColor.Red);
            Object.FindAnyObjectByType<OverviewController>().Select(OverviewTarget.Node("S1"));
            Object.FindAnyObjectByType<NodeConnectionController>().BeginSelected(); sim.Tick(1); yield return null; yield return null;
            var root=Object.FindAnyObjectByType<UIDocument>().rootVisualElement;
            var warning=root.Q<Label>("source-warning-S1"); Assert.That(warning,Is.Not.Null,"Wiring must retain the Source overload warning.");
            Assert.That(warning.text,Does.Contain("4.0s").And.Contain("GAME OVER"));
            Assert.That(root.Q<ProgressBar>("source-buffer-S1").title,Does.Contain("50 / 50"));
            sim.SetPaused(true); sim.Tick(20); yield return null;
            Assert.That(warning.text,Does.Contain("4.0s")); Assert.That(network.IsGameOver,Is.False);
            sim.SetPaused(false); sim.Tick(4); yield return null; yield return null;
            Assert.That(network.IsGameOver,Is.True); Assert.That(root.Q("result-overlay").resolvedStyle.display,Is.EqualTo(DisplayStyle.Flex));
            Assert.That(root.Q<Label>("result-title").text,Does.Contain("GAME OVER"));
            Assert.That(root.Q<Label>("result-detail").text,Does.Contain("S1"));
            Assert.That(Object.FindAnyObjectByType<NodeConnectionController>().IsNode360,Is.False);
        }
    }
}
