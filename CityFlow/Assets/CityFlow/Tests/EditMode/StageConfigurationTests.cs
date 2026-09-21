#nullable enable

using System;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Domain.Spatial;
using CityFlow.Infrastructure.Configuration;
using NUnit.Framework;
using UnityEngine;

namespace CityFlow.Tests.EditMode
{
    public sealed class StageConfigurationTests
    {
        private static StageDefinition Stage(Vector3 position) => new StageDefinition(0,
            new Rect(-20, -20, 40, 40), new[] { new Bounds(new Vector3(5, 3, 5), new Vector3(4, 6, 4)) },
            new[] { new NodeDefinition("source", NodeKind.Source, position),
                new NodeDefinition("red", NodeKind.Sink, new Vector3(-10, 0, 10), sinkColor: FlowColor.Red) });

        [Test] public void ValidGroundPlacementIsAccepted() => Assert.DoesNotThrow(() => Stage(Vector3.zero).Validate(0.5f));
        [TestCase(30, 0, 0)] [TestCase(0, 1, 0)] [TestCase(5, 0, 5)] [TestCase(2.6f, 0, 5)]
        public void OutsideGroundOrInsideInflatedFootprintIsRejected(float x, float y, float z) =>
            Assert.Throws<ArgumentException>(() => Stage(new Vector3(x, y, z)).Validate(0.5f));

        [TestCase("SourceBufferCapacity", 0)] [TestCase("RelayBufferCapacity", 0)] [TestCase("MaxInFlight", -1)]
        public void NonPositiveCapacitiesAreRejected(string field, int value)
        {
            var settings = ScriptableObject.CreateInstance<GameplaySettings>();
            try { typeof(GameplaySettings).GetField(field).SetValue(settings, value);
                Assert.Throws<ArgumentException>(() => settings.Validate()); }
            finally { UnityEngine.Object.DestroyImmediate(settings); }
        }
        [TestCase("FlowSpeed", 0)] [TestCase("FlowSpeed", float.NaN)]
        [TestCase("OverloadGrace", -1)] [TestCase("Clearance", -1)] [TestCase("Clearance", float.PositiveInfinity)]
        public void InvalidPhysicalSettingsAreRejected(string field, float value)
        {
            var settings = ScriptableObject.CreateInstance<GameplaySettings>();
            try { typeof(GameplaySettings).GetField(field).SetValue(settings, value);
                Assert.Throws<ArgumentException>(() => settings.Validate()); }
            finally { UnityEngine.Object.DestroyImmediate(settings); }
        }
        [Test] public void MissingSinkColorIsRejected()
        {
            var stage = new StageDefinition(0, new Rect(-10, -10, 20, 20), Array.Empty<Bounds>(),
                new[] { new NodeDefinition("sink", NodeKind.Sink, Vector3.zero) });
            Assert.Throws<ArgumentException>(() => stage.Validate(0));
        }
        [Test] public void SourceRequiresAnExistingSink()
        {
            var stage = new StageDefinition(0, new Rect(-10, -10, 20, 20), Array.Empty<Bounds>(),
                new[] { new NodeDefinition("source", NodeKind.Source, Vector3.zero) });
            Assert.Throws<ArgumentException>(() => stage.Validate(0));
        }
        [Test] public void LoadedStageDoesNotAliasEditableAssetArrays()
        {
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<StageConfiguration>(
                "Assets/CityFlow/Settings/Gameplay/ValidationStage.asset");
            var copy = UnityEngine.Object.Instantiate(asset);
            try
            {
                StageDefinition stage = copy.Load(0.5f);
                Vector3 initial = stage.Nodes[0].Position;
                copy.Nodes[0].Position = new Vector3(999, 999, 999);
                Assert.That(stage.Nodes[0].Position, Is.EqualTo(initial));
                Assert.That(asset.Nodes[0].Position, Is.EqualTo(initial));
            }
            finally { UnityEngine.Object.DestroyImmediate(copy); }
        }
    }
}
