#nullable enable

using System;
using System.Linq;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Infrastructure.Configuration;
using CityFlow.Infrastructure.Routing;
using NUnit.Framework;
using UnityEditor;

namespace CityFlow.Tests.EditMode
{
    public sealed class TokyoStationStageTests
    {
        [Test]
        public void CompactStationStageStartsUnwiredAndSupportsEverySinkThroughTheRelay()
        {
            var configuration = AssetDatabase.LoadAssetAtPath<StageConfiguration>(
                "Assets/CityFlow/Settings/Gameplay/TokyoStationStage.asset");
            Assert.That(configuration, Is.Not.Null, "The station scene needs a playable stage asset.");
            var settings = AssetDatabase.LoadAssetAtPath<GameplaySettings>(
                "Assets/CityFlow/Settings/Gameplay/TokyoStationGameplay.asset");
            Assert.That(settings, Is.Not.Null);
            var stage = configuration.Load(settings.Clearance);
            Assert.That(stage.Nodes.Count, Is.EqualTo(7));
            Assert.That(stage.Nodes.Select(node => node.Kind).Distinct(), Is.EquivalentTo(Enum.GetValues(typeof(NodeKind))));
            Assert.That(stage.Nodes.Where(node => node.Kind == NodeKind.Sink).Select(node => node.SinkColor.GetValueOrDefault()),
                Is.EquivalentTo(Enum.GetValues(typeof(FlowColor))));
            Assert.That(stage.WalkableArea.width, Is.LessThanOrEqualTo(150));
            Assert.That(stage.WalkableArea.height, Is.LessThanOrEqualTo(150));
            Assert.That(stage.Nodes.All(node => node.Position.y == stage.GroundHeight), Is.True);
            Assert.That(stage.Buildings, Is.Not.Empty, "Imported buildings must constrain routing.");
            var network = configuration.LoadNetwork(stage, settings.LoadNetworkSettings());
            Assert.That(network.Snapshot().Lines, Is.Empty);
            var planner = new LineRoutePlanner(stage, settings.Clearance);
            var source = stage.Nodes.Single(node => node.Kind == NodeKind.Source);
            var relay = stage.Nodes.Single(node => node.Kind == NodeKind.Relay);
            Assert.That(relay.MaxOutgoing, Is.GreaterThanOrEqualTo(5));
            Assert.That(planner.Generate(source, relay).IsValid, Is.True);
            foreach (var sink in stage.Nodes.Where(node => node.Kind == NodeKind.Sink))
                Assert.That(planner.Generate(relay, sink).IsValid, Is.True, sink.Id);
            Assert.That(EditorBuildSettings.scenes.Any(scene => scene.enabled && scene.path.EndsWith("/TokyoStationWiringLab.unity")),
                Is.True, "Retry must be able to reload this scene.");
        }
    }
}
