#nullable enable

using System;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Application.Routing;
using CityFlow.Infrastructure.Routing;
using System.Linq;
using CityFlow.Domain.Spatial;
using NUnit.Framework;
using UnityEngine;

namespace CityFlow.Tests.EditMode
{
    public sealed class RelayLiftTests
    {
        private static FlowNetwork Network(params NodeDefinition[] nodes) => new(
            new StageDefinition(0, new Rect(-40, -40, 80, 80), Array.Empty<Bounds>(), nodes, 40),
            new NetworkSettings(10, 5, 3, 1, 0.5f));

        [Test] public void DiagonalHeightChangesCannotBecomeLines()
        {
            var start = new Vector3(-10, 0, 0);
            var end = new Vector3(10, 10, 0);
            var network = Network(new RelayNodeDefinition("R", start), new SinkNodeDefinition("S", end, FlowColor.Red));
            Assert.That(network.TryConnect("R", "S", new[] { start, end }).Failure,
                Is.EqualTo(ConnectionFailure.InvalidRoute));
        }

        [Test] public void SourceCannotProvideTheVerticalPartOfALine()
        {
            var start = new Vector3(-10, 0, 0);
            var end = new Vector3(10, 10, 0);
            var network = Network(new SourceNodeDefinition("A", start), new SinkNodeDefinition("B", end, FlowColor.Red));
            Assert.That(network.TryConnect("A", "B", new[] { start, start + Vector3.up * 10, end }).Failure,
                Is.EqualTo(ConnectionFailure.InvalidRoute));
        }

        [Test] public void RelayWithoutLiftCapacityCannotRaiseItsLine()
        {
            var start = new Vector3(-10, 0, 0);
            var end = new Vector3(10, 0, 0);
            var network = Network(new RelayNodeDefinition("A", start), new RelayNodeDefinition("B", end));
            Assert.That(network.TryConnect("A", "B", new[] { start, start + Vector3.up * 8, end + Vector3.up * 8, end }).Failure,
                Is.EqualTo(ConnectionFailure.InvalidRoute));
        }
        [Test] public void ManualHeightMovesTheWholePlaneAndKeepsRelayColumnsFixed()
        {
            var a = new RelayNodeDefinition("A", new Vector3(-10, 0, 0), maximumRise: 10);
            var b = new RelayNodeDefinition("B", new Vector3(10, 0, 0), maximumRise: 20);
            var stage = new StageDefinition(0, new Rect(-40, -40, 80, 80), Array.Empty<Bounds>(), new NodeDefinition[] { a, b }, 40);
            var network = new FlowNetwork(stage, new NetworkSettings(10, 5, 3, 1, 0.5f));
            using var preview = new LinePreviewService(network, new LineRoutePlanner(stage, 0.5f));
            preview.Generate("A", "B");
            Assert.That(preview.MinimumRouteHeight, Is.Zero);
            Assert.That(preview.MaximumRouteHeight, Is.EqualTo(10));
            Assert.That(preview.SetRouteHeight(8), Is.True);
            Assert.That(preview.Current!.Points, Is.EqualTo(new[] { a.Position, a.Position + Vector3.up * 8, b.Position + Vector3.up * 8, b.Position }));
            Assert.That(preview.InsertPoint(0, a.Position + Vector3.up * 4), Is.False);
            Assert.That(preview.InsertPoint(1, new Vector3(0, 99, 5)), Is.True);
            Assert.That(preview.Current.Points[2].y, Is.EqualTo(8));
            Assert.That(preview.MovePoint(2, new Vector3(1, -10, 6)), Is.True);
            Assert.That(preview.Current.Points[2], Is.EqualTo(new Vector3(1, 8, 6)));
            Assert.That(preview.MovePoint(1, Vector3.one), Is.False);
            Assert.That(preview.RemovePoint(1), Is.False);
            Assert.That(preview.SetRouteHeight(12), Is.True);
            Assert.That(preview.Current.CanConfirm, Is.False);
            Assert.That(preview.TryConfirm(out _), Is.EqualTo(ConnectionFailure.InvalidRoute));
            Assert.That(network.Snapshot().Lines, Is.Empty);
            preview.SetRouteHeight(9);
            Assert.That(preview.Current.Points[2], Is.EqualTo(new Vector3(1, 9, 6)));
            Assert.That(preview.TryConfirm(out _), Is.EqualTo(ConnectionFailure.None));
            var line = network.Snapshot().Lines.Single();
            Assert.That(network.RequestRouteChange(line.Id,
                new[] { a.Position, a.Position + Vector3.up * 11, b.Position + Vector3.up * 11, b.Position }), Is.False);
            Assert.That(network.Snapshot().Lines.Single().Route, Is.SameAs(line.Route));
        }

        [Test] public void SourceAndSinkFixTheHorizontalPlaneAtTheirPlacementHeight()
        {
            var a = new RelayNodeDefinition("A", Vector3.zero, maximumRise: 20);
            var b = new SinkNodeDefinition("B", new Vector3(10, 12, 0), FlowColor.Red);
            var stage = new StageDefinition(0, new Rect(-40, -40, 80, 80), Array.Empty<Bounds>(), new NodeDefinition[] { a, b }, 40);
            using var preview = new LinePreviewService(new FlowNetwork(stage, new NetworkSettings(10, 5, 3, 1, 0.5f)), new LineRoutePlanner(stage, 0.5f));
            preview.Generate("A", "B");
            Assert.That(preview.Current!.CanConfirm, Is.True);
            Assert.That(preview.MinimumRouteHeight, Is.EqualTo(12));
            Assert.That(preview.MaximumRouteHeight, Is.EqualTo(12));
            Assert.That(preview.CanAdjustRouteHeight, Is.False);
            Assert.That(preview.SetRouteHeight(14), Is.False);
        }
    }
}
