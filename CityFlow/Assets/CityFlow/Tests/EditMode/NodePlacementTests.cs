#nullable enable

using System;
using System.Linq;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Domain.Spatial;
using CityFlow.Infrastructure.Configuration;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CityFlow.Tests.EditMode
{
    public sealed class NodePlacementTests
    {
        [Test] public void PolymorphicPlacementsRetainTheirRoleAndSettingsThroughSerialization()
        {
            var asset = ScriptableObject.CreateInstance<StageConfiguration>();
            var copy = ScriptableObject.CreateInstance<StageConfiguration>();
            try
            {
                asset.Nodes = new NodePlacement[] {
                    new SourceNodePlacement { Id = "S", Position = Vector3.zero, MaxOutgoing = 2, GenerationInterval = 4, GenerationDelay = 7 },
                    new RelayNodePlacement { Id = "R", Position = Vector3.right * 5, MaxIncoming = 4, MaxOutgoing = 6 },
                    new SinkNodePlacement { Id = "T", Position = Vector3.right * 10, MaxIncoming = 8, SinkColor = FlowColor.Blue }
                };
                asset.Waves = new[] { new StageConfiguration.WavePlacement {
                    StartSeconds = 60, IntervalScale = 0.9f,
                    Additions = new NodePlacement[] { new SourceNodePlacement { Id = "S2", GenerationInterval = 5, GenerationDelay = 20 } }
                } };
                EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(asset), copy);
                var stage = copy.Load(0);
                var source = (SourceNodeDefinition)stage.Nodes[0];
                Assert.That(source.MaxIncoming, Is.Zero);
                Assert.That(source.MaxOutgoing, Is.EqualTo(2));
                Assert.That(source.GenerationInterval, Is.EqualTo(4));
                Assert.That(source.GenerationDelay, Is.EqualTo(7));
                Assert.That(stage.Nodes[1], Is.TypeOf<RelayNodeDefinition>());
                Assert.That(stage.Nodes[1].MaxIncoming, Is.EqualTo(4));
                Assert.That(stage.Nodes[1].MaxOutgoing, Is.EqualTo(6));
                var sink = (SinkNodeDefinition)stage.Nodes[2];
                Assert.That(sink.Color, Is.EqualTo(FlowColor.Blue));
                Assert.That(sink.MaxIncoming, Is.EqualTo(8));
                Assert.That(sink.MaxOutgoing, Is.Zero);
                Assert.That(sink.Position, Is.EqualTo(Vector3.right * 10));
                var addition = (SourceNodeDefinition)copy.Waves[0].Additions[0].ToDefinition();
                Assert.That(addition.GenerationDelay, Is.EqualTo(20));
                Assert.That(addition.MaxIncoming, Is.Zero);
                Assert.That(copy.Nodes[0], Is.Not.SameAs(asset.Nodes[0]));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
                UnityEngine.Object.DestroyImmediate(copy);
            }
        }

        [TestCase("ValidationStage", 5, 0)]
        [TestCase("WiringStage", 5, 6)]
        public void AuthoredInitialAndWavePlacementsUseOnlyTheirSupportedDirections(string name, int initialCount, int additions)
        {
            var asset = AssetDatabase.LoadAssetAtPath<StageConfiguration>($"Assets/CityFlow/Settings/Gameplay/{name}.asset");
            var stage = asset.Load(0.5f);
            var waves = asset.LoadWaves(stage, 0.5f);
            Assert.That(stage.Nodes.Count, Is.EqualTo(initialCount));
            Assert.That(waves.Sum(w => w.Additions.Count), Is.EqualTo(additions));
            foreach (var node in stage.Nodes.Concat(waves.SelectMany(w => w.Additions)))
            {
                if (node is SourceNodeDefinition) Assert.That(node.MaxIncoming, Is.Zero, node.Id);
                if (node is SinkNodeDefinition) Assert.That(node.MaxOutgoing, Is.Zero, node.Id);
            }
            Assert.DoesNotThrow(() => asset.LoadNetwork(stage, new NetworkSettings(10, 5, 3, 8, 0.5f)));
        }

        [TestCase(0, 0)] [TestCase(-1, 0)] [TestCase(double.NaN, 0)]
        [TestCase(double.PositiveInfinity, 0)] [TestCase(1, -1)] [TestCase(1, double.NaN)]
        public void SourceGenerationSettingsAreValidated(double interval, double delay)
        {
            var stage = new StageDefinition(0, new Rect(-20, -20, 40, 40), Array.Empty<Bounds>(), new NodeDefinition[] {
                new SourceNodeDefinition("S", Vector3.zero, generationInterval: interval, generationDelay: delay),
                new SinkNodeDefinition("T", Vector3.right, FlowColor.Red)
            });
            Assert.Throws<ArgumentException>(() => stage.Validate(0));
        }
    }
}
