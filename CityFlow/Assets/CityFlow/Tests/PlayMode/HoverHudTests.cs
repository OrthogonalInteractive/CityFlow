#nullable enable
using System.Collections;
using System.Linq;
using CityFlow.Application.UseCases;
using CityFlow.Composition;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Infrastructure.Configuration;
using CityFlow.Presentation.Overview;
using CityFlow.Presentation.Rendering;
using CityFlow.Presentation.Connections;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using VContainer;
namespace CityFlow.Tests.PlayMode
{
    public sealed class HoverHudTests
    {
        [UnitySetUp] public IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("WiringLab"); yield return null;
            Object.FindAnyObjectByType<SimulationDriver>().enabled=false;
        }
        [UnityTest] public IEnumerator DetailsOnlyAppearWhileHoveredAndStayWithinTheScreen()
        {
            var scope=Object.FindAnyObjectByType<CityFlowLifetimeScope>();
            var network=scope.Container.Resolve<FlowNetwork>();
            var sim=scope.Container.Resolve<FlowSimulation>(); sim.SetPaused(true);
            var overview=Object.FindAnyObjectByType<OverviewController>();
            Camera.main.orthographicSize=52;
            var root=Object.FindAnyObjectByType<UIDocument>().rootVisualElement;
            var tooltip=root.Q("node-tooltip"); Assert.That(tooltip,Is.Not.Null);
            overview.Hover(new Vector2(-100,-100)); overview.Select(OverviewTarget.Node("S1"));
            yield return null; yield return null;
            Assert.That(tooltip.resolvedStyle.display,Is.EqualTo(DisplayStyle.None),"Selection must not pin the details.");
            foreach(string removed in new[]{"source-monitor","node-rows","preview-panel","network-summary","waiting-value"})
                Assert.That(root.Q(removed),Is.Null,removed+" must be removed.");
            foreach(string id in new[]{"S1","R1","RED"})
            {
                var node=network.NodeDefinitions.Single(n=>n.Id==id);
                Vector2 screen=Camera.main.WorldToScreenPoint(node.Position+Vector3.up*1.4f);
                overview.Hover(screen); yield return null; yield return null;
                Assert.That(tooltip.resolvedStyle.display,Is.EqualTo(DisplayStyle.Flex));
                string detail=HudAssertions.TooltipText(root);
                Assert.That(detail,Does.Contain(id).And.Contain("IN "));
                Assert.That(tooltip.worldBound.xMin,Is.GreaterThanOrEqualTo(0));
                Assert.That(tooltip.worldBound.xMax,Is.LessThanOrEqualTo(root.worldBound.xMax));
                Assert.That(tooltip.worldBound.yMax,Is.LessThanOrEqualTo(root.worldBound.yMax));
                Vector2 anchor=RuntimePanelUtils.ScreenToPanel(root.panel,new Vector2(screen.x,Screen.height-screen.y));
                Assert.That(tooltip.worldBound.Contains(anchor),Is.False,"Hover details must leave the hovered Node visible.");
                foreach(var visibleNode in network.NodeDefinitions)
                {
                    Vector3 projected=Camera.main.WorldToScreenPoint(visibleNode.Position+Vector3.up*1.4f);
                    if(projected.z<=0) continue;
                    Vector2 nodePoint=RuntimePanelUtils.ScreenToPanel(root.panel,new Vector2(projected.x,Screen.height-projected.y));
                    Assert.That(tooltip.worldBound.Contains(nodePoint),Is.False,"Hover details must also avoid neighboring Nodes: "+visibleNode.Id);
                }
                Assert.That(overview.IsPointerBlocked?.Invoke(screen),Is.False);
                if(id=="RED") Assert.That(detail,Does.Not.Contain("BUFFER"));
                if(id=="S1") Assert.That(detail,Does.Contain("GENERATE 3.00s").And.Contain("GENERATED 0").And.Contain("PREPARING"));
            }
            overview.Hover(new Vector2(-100,-100)); yield return null;
            Assert.That(tooltip.resolvedStyle.display,Is.EqualTo(DisplayStyle.None));
        }
        [UnityTest] public IEnumerator BlockedLineChangesColorAndRestoresItAfterReceiverRecovery()
        {
            var network=Object.FindAnyObjectByType<CityFlowLifetimeScope>().Container.Resolve<FlowNetwork>();
            var source=network.NodeDefinitions.Single(n=>n.Id=="S1").Position;
            var relay=network.NodeDefinitions.Single(n=>n.Id=="R1").Position;
            Assert.That(network.TryConnect("S1","R1",new[]{source,relay}).Succeeded,Is.True);
            yield return null; yield return null;
            var line=GameObject.Find("Line 1: S1 -> R1").GetComponent<LineRenderer>();
            Color normal=line.sharedMaterial.GetColor("_BaseColor");
            var random=new SystemRandomSource(1);
            for(int i=0;i<5;i++) { network.GenerateFlow("S1",FlowColor.Red); network.RouteWaitingFlows(random); network.AdvanceInFlight(20); }
            for(int i=0;i<3;i++) network.GenerateFlow("S1",FlowColor.Red);
            network.RouteWaitingFlows(random); network.AdvanceInFlight(20); yield return null;
            Assert.That(network.Snapshot().Lines[0].InFlight.All(f=>f.IsStopped),Is.True);
            Assert.That(line.sharedMaterial.GetColor("_BaseColor"),Is.Not.EqualTo(normal),"A blocked Line needs its own warning color.");
            var red=network.NodeDefinitions.Single(n=>n.Id=="RED").Position;
            Assert.That(network.TryConnect("R1","RED",new[]{relay,source,red}).Succeeded,Is.True);
            network.RouteWaitingFlows(random); network.AdvanceInFlight(20); yield return null;
            Assert.That(line.sharedMaterial.GetColor("_BaseColor"),Is.EqualTo(normal));
        }
        [UnityTest] public IEnumerator BlockedPendingLineUsesDashedDeletionAndDoubleRouteChangeStrokes()
        {
            var network = Object.FindAnyObjectByType<CityFlowLifetimeScope>().Container.Resolve<FlowNetwork>();
            var source = network.NodeDefinitions.Single(n => n.Id == "S1").Position;
            var relay = network.NodeDefinitions.Single(n => n.Id == "R1").Position;
            var created = network.TryConnect("S1", "R1", new[] { source, relay });
            int id = created.LineId.GetValueOrDefault(); Assert.That(created.Succeeded, Is.True);
            var random = new SystemRandomSource(1);
            for (int i = 0; i < 5; i++) { network.GenerateFlow("S1", FlowColor.Red); network.RouteWaitingFlows(random); network.AdvanceInFlight(20); }
            for (int i = 0; i < 3; i++) network.GenerateFlow("S1", FlowColor.Red);
            network.RouteWaitingFlows(random); network.AdvanceInFlight(20);
            var before = network.Snapshot().Lines.Single();
            network.RequestDeletion(id); yield return null; yield return null;
            var dashes = Object.FindObjectsByType<LineRenderer>().Where(l => l.name == "Deletion dash " + id).ToArray();
            Assert.That(dashes.Length, Is.GreaterThan(1));
            Assert.That(dashes.All(l => l.enabled && l.sharedMaterial.GetColor("_BaseColor") == new Color(1, 0.38f, 0.10f)), Is.True);
            network.CancelPending(id);
            Assert.That(network.RequestRouteChange(id, new[] { source, (source + relay) * 0.5f + Vector3.back, relay }), Is.True);
            yield return null; yield return null;
            Assert.That(Object.FindObjectsByType<LineRenderer>().Count(l => l.name == "Route change rail " + id), Is.EqualTo(2));
            Assert.That(network.Snapshot().Lines.Single().InFlight.Select(f => (f.Flow.Id, f.Distance)),
                Is.EqualTo(before.InFlight.Select(f => (f.Flow.Id, f.Distance))));
            var controller = Object.FindAnyObjectByType<NodeConnectionController>();
            Object.FindAnyObjectByType<OverviewController>().Select(OverviewTarget.Node("S1")); controller.BeginSelected();
            yield return null; yield return null;
            foreach (var flight in before.InFlight)
                Assert.That(GameObject.Find($"FLOW {flight.Flow.Id} / {flight.Flow.Color}").transform.localScale, Is.EqualTo(Vector3.one * 1.15f));
        }
        [UnityTest] public IEnumerator Node360ShowsSourceAndCandidateDetailsOnlyUnderThePointer()
        {
            var overview=Object.FindAnyObjectByType<OverviewController>();
            overview.Select(OverviewTarget.Node("S1"));
            Object.FindAnyObjectByType<NodeConnectionController>().BeginSelected();
            yield return null; yield return null;
            var root=Object.FindAnyObjectByType<UIDocument>().rootVisualElement;
            var old=InputSystem.settings.editorInputBehaviorInPlayMode;
            var background=InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            var mouse=InputSystem.AddDevice<Mouse>();
            try
            {
                foreach(var pair in new[]{("connect-selection","S1"),("candidate-option-R1","R1")})
                {
                    Vector2 panel=root.Q(pair.Item1).worldBound.center;
                    Vector2 screen=new Vector2(panel.x/root.layout.width*Screen.width,(1-panel.y/root.layout.height)*Screen.height);
                    InputSystem.QueueStateEvent(mouse,new MouseState { position=screen }); yield return null; yield return null;
                    Assert.That(root.Q("node-tooltip").resolvedStyle.display,Is.EqualTo(DisplayStyle.Flex));
                    Assert.That(HudAssertions.TooltipText(root),Does.Contain(pair.Item2).And.Contain("BUFFER"));
                    Assert.That(root.Q("node-tooltip").worldBound.xMin,Is.GreaterThanOrEqualTo(root.layout.width*0.72f));
                }
                InputSystem.QueueStateEvent(mouse,new MouseState { position=new Vector2(-100,-100) }); yield return null; yield return null;
                Assert.That(root.Q("node-tooltip").resolvedStyle.display,Is.EqualTo(DisplayStyle.None));
            }
            finally
            {
                InputSystem.RemoveDevice(mouse); InputSystem.settings.editorInputBehaviorInPlayMode=old;
                InputSystem.settings.backgroundBehavior=background;
            }
        }
    }
}
