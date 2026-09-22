#nullable enable

using System;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Domain.Spatial;
using CityFlow.Presentation.Overview;
using NUnit.Framework;
using UnityEngine;

namespace CityFlow.Tests.EditMode
{
    public sealed class OverviewReadoutTests
    {
        [Test] public void NodeReadoutShowsColorBufferAndConnectionUsage()
        {
            var n = Network(); n.GenerateFlow("S", FlowColor.Red);
            string text = OverviewReadout.Describe(OverviewTarget.Node("S"), n.Snapshot(), n.Settings);
            Assert.That(text, Does.Contain("BUFFER 1/4 (25%)").And.Contain("Red: 1").And.Contain("OUT 1/2"));
        }
        [Test] public void SourceReadoutContainsOnlyOutputAndGenerationInformation()
        {
            var n = Network();
            string text = OverviewReadout.Describe(OverviewTarget.Node("S"), n.Snapshot(), n.Settings);
            Assert.That(text, Does.Contain("OUT 1/2").And.Contain("CONNECTED TO: T").And.Contain("GENERATE"));
            Assert.That(text, Does.Not.Contain("IN ").And.Not.Contain("INPUT").And.Not.Contain("INCOMING"));
        }
        [Test] public void SinkReadoutContainsOnlyInputAndConsumptionInformation()
        {
            var n = Network();
            string text = OverviewReadout.Describe(OverviewTarget.Node("T"), n.Snapshot(), n.Settings);
            Assert.That(text, Does.Contain("IN 1/2").And.Contain("CONNECTED FROM: S"));
            Assert.That(text, Does.Not.Contain("OUT ").And.Not.Contain("BUFFER").And.Not.Contain("GENERATE"));
        }
        [Test] public void LineReadoutUsesActualRouteForTimeThroughputAndCapacity()
        {
            var n = Network(); n.GenerateFlow("S", FlowColor.Red); n.RouteWaitingFlows();
            string text = OverviewReadout.Describe(OverviewTarget.Line(1), n.Snapshot(), n.Settings);
            Assert.That(text, Does.Contain("LENGTH 20.0 m").And.Contain("TRAVEL 2.00 s")
                .And.Contain("IN-FLIGHT 1/2 (50%)").And.Contain("THROUGHPUT 1.00 FLOW/s").And.Contain("MOVING 1 · STOPPED 0"));
        }
        private static FlowNetwork Network()
        {
            var stage = new StageDefinition(0, new Rect(-50,-50,100,100), Array.Empty<Bounds>(), new NodeDefinition[] {
                new SourceNodeDefinition("S", Vector3.zero, maxOutgoing: 2),
                new SinkNodeDefinition("T", new Vector3(20,0,0), FlowColor.Red, maxIncoming: 2) });
            var n = new FlowNetwork(stage, new NetworkSettings(4,4,2,10,0));
            Assert.That(n.TryConnect("S","T",new[] { Vector3.zero, new Vector3(20,0,0) }).Succeeded, Is.True);
            return n;
        }
    }
}
