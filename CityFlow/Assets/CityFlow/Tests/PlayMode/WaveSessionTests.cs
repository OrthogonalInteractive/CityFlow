#nullable enable
using System;
using System.Collections;
using System.Linq;
using CityFlow.Application.Connections;
using CityFlow.Application.Routing;
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
using Object=UnityEngine.Object;
namespace CityFlow.Tests.PlayMode
{
    public sealed class WaveSessionTests
    {
        [UnitySetUp] public IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("WiringLab"); yield return null;
            Object.FindAnyObjectByType<SimulationDriver>().enabled=false;
        }
        private static int Connect(ConnectionSession s,string from,string to)
        { Assert.That(s.Begin(from),Is.True); s.SelectTarget(to); Assert.That(s.Confirm(),Is.EqualTo(ConnectionFailure.None)); return s.LastCreatedLineId!.Value; }
        [UnityTest] public IEnumerator WaveCandidateListKeepsGreenSelectableDespiteOverlappingMarkers()
        {
            var scope=Object.FindAnyObjectByType<CityFlowLifetimeScope>();
            var sim=scope.Container.Resolve<FlowSimulation>(); var network=scope.Container.Resolve<FlowNetwork>();
            var session=scope.Container.Resolve<ConnectionSession>(); var preview=scope.Container.Resolve<LinePreviewService>();
            Connect(session,"S1","RED"); Connect(session,"S1","BLUE");
            Object.FindAnyObjectByType<OverviewController>().Select(OverviewTarget.Node("S1"));
            var controller=Object.FindAnyObjectByType<NodeConnectionController>(); controller.BeginSelected();
            sim.Tick(60-sim.ElapsedSeconds); sim.SetPaused(true); yield return null; yield return null;
            var root=Object.FindAnyObjectByType<UIDocument>().rootVisualElement;
            Assert.That(root.Q("wave-notice").resolvedStyle.display,Is.EqualTo(DisplayStyle.None),"Node 360 must keep Wave notices outside the city view.");
            var list=root.Q<ScrollView>("connection-candidates");
            Assert.That(list,Is.Not.Null,"Every candidate needs a discoverable mouse target when world markers overlap.");
            var green=list.Q<Button>("candidate-option-GREEN");
            Assert.That(green,Is.Not.Null,"A new Wave Node must appear while Node 360 is already open.");
            Assert.That(green.resolvedStyle.display,Is.EqualTo(DisplayStyle.Flex));
            Assert.That(green.enabledInHierarchy,Is.True);
            Assert.That(green.text,Does.Contain("GREEN").And.Contain("55 m"));
            list.ScrollTo(green); yield return null; yield return null;
            Assert.That(list.contentViewport.worldBound.Contains(green.worldBound.center),Is.True);
            var hit=root.panel.Pick(green.worldBound.center);
            Assert.That(hit==green || green.Contains(hit),Is.True,"GREEN must be clickable without a hidden-marker keyboard workaround.");
            Assert.That(list.Query<Button>().ToList().Count(b=>b.resolvedStyle.display==DisplayStyle.Flex),Is.EqualTo(session.Candidates().Count));
            session.SetFilter(DistanceBand.Near); yield return null; yield return null;
            Assert.That(green.resolvedStyle.display,Is.EqualTo(DisplayStyle.None));
            session.SetFilter(DistanceBand.Mid); controller.Look(new Vector2(180,0)); yield return null; yield return null;
            Assert.That(green.resolvedStyle.display,Is.EqualTo(DisplayStyle.Flex));
            controller.FocusTarget("GREEN");
            yield return null;
            Assert.That(controller.AttentionId,Is.EqualTo("GREEN"));
            Assert.That(preview.Current?.DestinationId,Is.EqualTo("GREEN")); Assert.That(preview.Current!.CanConfirm,Is.True);
            Assert.That(network.Snapshot().Lines.Count,Is.EqualTo(2),"Selecting a candidate must only create a Preview.");
            UiPointer.Click(green); yield return null;
            Assert.That(network.Snapshot().Lines.Any(l=>l.SourceId=="S1" && l.DestinationId=="GREEN"),Is.True);
            Assert.That(root.Q("candidate-list-panel").resolvedStyle.display,Is.EqualTo(DisplayStyle.None));
        }
        [UnityTest] public IEnumerator WaveAddsVisibleSelectableNodesWithoutReplacingNetworkOrReservations()
        {
            var scope=Object.FindAnyObjectByType<CityFlowLifetimeScope>(); var n=scope.Container.Resolve<FlowNetwork>();
            var sim=scope.Container.Resolve<FlowSimulation>(); var s=scope.Container.Resolve<ConnectionSession>();
            var scene=SceneManager.GetActiveScene().handle; Connect(s,"S1","RED"); int blue=Connect(s,"S1","BLUE");
            sim.Tick(59.95-sim.ElapsedSeconds); Assert.That(n.IsGameOver,Is.False); n.GenerateFlow("S1",FlowColor.Blue); n.RouteWaitingFlows();
            Assert.That(n.Snapshot().Lines.Single(x=>x.Id==blue).InFlight,Is.Not.Empty); n.RequestDeletion(blue);
            var before=n.Snapshot().Lines.Single(x=>x.Id==blue); sim.Tick(0.05); yield return null; yield return null;
            Assert.That(sim.Wave,Is.EqualTo(2)); Assert.That(SceneManager.GetActiveScene().handle,Is.EqualTo(scene));
            Assert.That(Object.FindAnyObjectByType<CityFlowLifetimeScope>().Container.Resolve<FlowNetwork>(),Is.SameAs(n));
            Assert.That(n.Snapshot().Lines.Single(x=>x.Id==blue).Status,Is.EqualTo(LineStatus.DeletePending));
            Assert.That(n.Snapshot().Lines.Single(x=>x.Id==blue).Route,Is.SameAs(before.Route));
            Assert.That(Object.FindAnyObjectByType<ValidationCityView>().VisibleNodeCount,Is.EqualTo(7));
            var root=Object.FindAnyObjectByType<UIDocument>().rootVisualElement;
            Assert.That(root.Q<Label>("wave-notice").text,Does.Contain("WAVE 2"));
            Assert.That(root.Q("node-label-GREEN"),Is.Not.Null); Assert.That(root.Q("arrival-S2"),Is.Not.Null);
            Assert.That(sim.SourceStartRemaining("S2"),Is.GreaterThan(19));
            Assert.That(root.Q<Button>("arrival-GREEN").text,Does.Not.Contain("OFFSCREEN"),"A visible Node must not be labelled offscreen just because its label avoids a panel.");
            sim.SetPaused(true); var overview=Object.FindAnyObjectByType<OverviewController>();
            overview.Select(OverviewTarget.Node("S1")); overview.FocusSelection(); yield return null;
            Assert.That(root.Q<Button>("arrival-S2").text,Does.Contain("OFFSCREEN"));
            Assert.That(root.Q("arrival-GREEN").worldBound.Overlaps(root.Q(className:"session-controls").worldBound),Is.False,
                "Arrival markers must not hide Pause or Resume.");
            overview.Select(OverviewTarget.Node("S2")); var controller=Object.FindAnyObjectByType<NodeConnectionController>(); controller.BeginSelected();
            Assert.That(controller.IsNode360,Is.True); controller.FocusTarget("GREEN"); s.SelectTarget("GREEN");
            Assert.That(scope.Container.Resolve<LinePreviewService>().Current!.CanConfirm,Is.True);
            Assert.That(s.Confirm(),Is.EqualTo(ConnectionFailure.None)); Assert.That(n.Snapshot().Lines.Count,Is.EqualTo(3));
        }
        [UnityTest] public IEnumerator GameOverShowsMatchingResultAndRetryStartsWithZeroLines()
        {
            var scope=Object.FindAnyObjectByType<CityFlowLifetimeScope>(); var sim=scope.Container.Resolve<FlowSimulation>();
            var n=scope.Container.Resolve<FlowNetwork>(); Assert.That(n.Snapshot().Lines,Is.Empty); sim.Tick(1000); yield return null;
            var result=sim.Result ?? throw new AssertionException("Missing result"); Assert.That(result.SourceId,Is.EqualTo("S1"));
            Assert.That(result.Wave,Is.EqualTo(1)); var root=Object.FindAnyObjectByType<UIDocument>().rootVisualElement;
            Assert.That(root.Q("result-overlay").resolvedStyle.display,Is.EqualTo(DisplayStyle.Flex));
            string text=root.Q<Label>("result-detail").text;
            Assert.That(text,Does.Contain($"WAVE {result.Wave}").And.Contain(CityFlow.Presentation.UI.HudClock.Format(result.SurvivalSeconds)).And.Contain($"DELIVERED {result.Delivered}").And.Contain("S1"));
            Assert.That(root.Q<Label>("elapsed-value").text,Is.EqualTo(CityFlow.Presentation.UI.HudClock.Format(result.SurvivalSeconds)));
            var oldScope=scope.GetEntityId(); var button=root.Q<Button>("retry-session");
            UiPointer.Click(button);
            CityFlowLifetimeScope? next=null;
            for(int i=0;i<120;i++)
            {
                yield return null; next=Object.FindAnyObjectByType<CityFlowLifetimeScope>();
                if(next!=null && next.GetEntityId()!=oldScope && Object.FindAnyObjectByType<SimulationDriver>()!=null) break;
            }
            Assert.That(next,Is.Not.Null); Assert.That(next!.GetEntityId(),Is.Not.EqualTo(oldScope));
            Object.FindAnyObjectByType<SimulationDriver>().enabled=false;
            Assert.That(next.Container.Resolve<FlowNetwork>().Snapshot().Lines,Is.Empty);
            Assert.That(next.Container.Resolve<FlowNetwork>().Snapshot().GeneratedCount,Is.Zero);
            Assert.That(next.Container.Resolve<FlowSimulation>().Wave,Is.EqualTo(1));
            Assert.That(next.Container.Resolve<FlowSimulation>().Result,Is.Null);
            yield return null; Assert.That(Object.FindAnyObjectByType<UIDocument>().rootVisualElement.Q("result-overlay").resolvedStyle.display,Is.EqualTo(DisplayStyle.None));
        }
        [UnityTest] public IEnumerator AuthoredStageCanExpandFromZeroLinesToAllFiveColors()
        {
            var scope=Object.FindAnyObjectByType<CityFlowLifetimeScope>(); var n=scope.Container.Resolve<FlowNetwork>();
            var sim=scope.Container.Resolve<FlowSimulation>(); var s=scope.Container.Resolve<ConnectionSession>();
            Connect(s,"S1","RED"); Connect(s,"S1","BLUE"); sim.Tick(60-sim.ElapsedSeconds);
            Connect(s,"S1","R1"); Connect(s,"S2","RED"); Connect(s,"S2","BLUE"); Connect(s,"S2","R1"); Connect(s,"R1","GREEN");
            sim.Tick(60); Assert.That(sim.Wave,Is.EqualTo(3));
            Connect(s,"R1","YELLOW");
            sim.Tick(60); Assert.That(sim.Wave,Is.EqualTo(4));
            Connect(s,"R1","PURPLE"); Connect(s,"S3","RED"); Connect(s,"S3","BLUE"); Connect(s,"S3","R1");
            sim.Tick(60); yield return null; yield return null;
            Assert.That(n.IsGameOver,Is.False); Assert.That(n.NodeDefinitions.Select(x=>x.SinkColor).Where(x=>x.HasValue).Distinct().Count(),Is.EqualTo(5));
            Assert.That(Object.FindAnyObjectByType<ValidationCityView>().VisibleNodeCount,Is.EqualTo(11));
            Assert.That(n.Snapshot().DeliveredCount,Is.GreaterThan(100)); Assert.That(n.Snapshot().Lines.Count,Is.EqualTo(12));
            var state=n.Snapshot();
            Assert.That(state.Nodes.Where(x=>x.Definition.Kind==NodeKind.Sink).All(x=>x.Buffer.Count==0 && !x.BufferCapacity.HasValue),Is.True);
            Assert.That(state.Nodes.Where(x=>x.Definition.Kind==NodeKind.Relay).All(x=>x.Buffer.Count<=5),Is.True);
            Assert.That(state.GeneratedCount,Is.EqualTo(state.DeliveredCount+state.Nodes.Sum(x=>x.Buffer.Count)+state.Lines.Sum(x=>x.InFlight.Count)));
        }
    }
}
