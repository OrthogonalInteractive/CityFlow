#nullable enable
using System;
using System.Linq;
using CityFlow.Application.Routing;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Domain.Spatial;
using CityFlow.Infrastructure.Routing;
using NUnit.Framework;
using UnityEngine;
namespace CityFlow.Tests.EditMode
{
    public sealed class ManualRouteTests
    {
        private static (FlowNetwork, LinePreviewService) Create()
        {
            var nodes = new[] { new NodeDefinition("A", NodeKind.Source, Vector3.zero, 3, 3, null, 1),
                new NodeDefinition("B", NodeKind.Sink, new Vector3(20,0,0), 3, 3, FlowColor.Red) };
            var stage = new StageDefinition(0,new Rect(-10,-20,50,40),Array.Empty<Bounds>(),nodes);
            var network = new FlowNetwork(stage,new NetworkSettings(10,10,20,0.5f));
            var preview = new LinePreviewService(network,new GroundRoutePlanner(stage,0.5f));
            preview.Generate("A","B"); return (network,preview);
        }
        [Test] public void EditingProjectsToGroundUpdatesMetricsAndDoesNotReserveSlots()
        {
            var (network,service)=Create(); using var preview=service;
            Assert.That(preview.InsertPoint(0,new Vector3(10,50,10)),Is.True);
            Assert.That(preview.Current!.Points[1].y,Is.Zero);
            Assert.That(preview.Current.Length,Is.EqualTo(Math.Sqrt(200)*2).Within(0.001));
            Assert.That(preview.Current.TravelTime,Is.EqualTo(preview.Current.Length/20).Within(0.001));
            Assert.That(preview.Current.Throughput,Is.EqualTo(10/preview.Current.TravelTime).Within(0.001));
            Assert.That(network.Snapshot().Lines,Is.Empty);
            Assert.That(network.Snapshot().Nodes.All(n=>n.IncomingUsed+n.OutgoingUsed==0),Is.True);
            Assert.That(preview.MovePoint(1,new Vector3(10,-30,15)),Is.True);
            Assert.That(preview.Current.Length,Is.GreaterThan(30));
            Assert.That(preview.RemovePoint(1),Is.True); Assert.That(preview.Current.Length,Is.EqualTo(20));
        }
        [Test] public void EndpointsStayLockedAndInvalidEditCannotConfirm()
        {
            var (network,service)=Create(); using var preview=service;
            Assert.That(preview.MovePoint(0,Vector3.one),Is.False);
            Assert.That(preview.RemovePoint(1),Is.False);
            Assert.That(preview.InsertPoint(-1,Vector3.zero),Is.False);
            Assert.That(preview.InsertPoint(0,new Vector3(10,0,99)),Is.True);
            Assert.That(preview.Current!.Geometry.Failure,Is.EqualTo(RouteFailure.OutsideArea));
            Assert.That(preview.TryConfirm(out _),Is.EqualTo(ConnectionFailure.InvalidRoute));
            Assert.That(network.Snapshot().Lines,Is.Empty);
            preview.Regenerate(); Assert.That(preview.Current!.CanConfirm,Is.True);
            Assert.That(preview.Current.Points.Count,Is.EqualTo(2));
            preview.Cancel(); Assert.That(preview.Current,Is.Null); Assert.That(network.Snapshot().Lines,Is.Empty);
        }
        [Test] public void AppliedLineUsesExactlyTheEditedPolyline()
        {
            var (network,service)=Create(); using var preview=service;
            preview.InsertPoint(0,new Vector3(10,0,10));
            var points=preview.Current!.Points.ToArray();
            Assert.That(points.Length,Is.EqualTo(3));
            Assert.That(preview.TryConfirm(out _),Is.EqualTo(ConnectionFailure.None));
            Assert.That(network.Snapshot().Lines.Single().Route.Points,Is.EqualTo(points));
        }
    }
}
