#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using CityFlow.Composition;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Infrastructure.Configuration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CityFlow.Editor
{
    public static class ExpansionLabSetup
    {
        public const string ScenePath = "Assets/CityFlow/Scenes/ExpansionLab.unity";
        public const string StagePath = "Assets/CityFlow/Settings/Gameplay/ExpansionStage.asset";
        public const string SettingsPath = "Assets/CityFlow/Settings/Gameplay/ExpansionGameplay.asset";
        public static readonly Vector2[] Sizes = {
            new(100, 84), new(128, 108), new(156, 132), new(184, 156), new(212, 180),
            new(240, 204), new(270, 230), new(300, 256), new(330, 280), new(360, 300) };
        public static Rect Area(int waveIndex) => new(-Sizes[waveIndex] * 0.5f, Sizes[waveIndex]);

        [MenuItem("City Flow/Create Expansion Lab")]
        public static void Create()
        {
            if (EditorApplication.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Stop play and save the current scene before creating ExpansionLab.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                throw new InvalidOperationException("ExpansionLab already exists. Edit its authored assets instead of overwriting the level.");
            var stage = ScriptableObject.CreateInstance<StageConfiguration>();
            Configure(stage);
            var settings = ScriptableObject.CreateInstance<GameplaySettings>();
            settings.RandomSeed = 19010;
            settings.OverloadGrace = 8;
            stage.LoadWaves(stage.Load(settings.Clearance), settings.Clearance);
            AssetDatabase.CreateAsset(stage, StagePath);
            AssetDatabase.CreateAsset(settings, SettingsPath);
            var scene = EditorSceneManager.OpenScene("Assets/CityFlow/Scenes/Bootstrap.unity");
            var scope = UnityEngine.Object.FindAnyObjectByType<CityFlowLifetimeScope>();
            scope.SetConfiguration(settings, stage);
            RelayAppearanceSetup.Configure(scope);
            EditorUtility.SetDirty(scope);
            EditorSceneManager.SaveScene(scene, ScenePath);
            if (!EditorBuildSettings.scenes.Any(s => s.path == ScenePath))
                EditorBuildSettings.scenes = EditorBuildSettings.scenes.Concat(new[] { new EditorBuildSettingsScene(ScenePath, true) }).ToArray();
            AssetDatabase.SaveAssets();
        }

        public static void Configure(StageConfiguration stage)
        {
            stage.GroundHeight = 0; stage.MaximumAltitude = 40;
            stage.WalkableArea = Area(0); stage.ExpandsWithWaves = true; stage.MaximumArea = Area(9);
            stage.Lines = Array.Empty<StageConfiguration.LinePlacement>();
            var nodes = new List<NodePlacement>();
            void Sink(string id, float x, float y, float z, FlowColor color) => nodes.Add(new SinkNodePlacement {
                Id = id, Position = new Vector3(x, y, z), SinkColor = color, MaxIncoming = 3 });
            Sink("C-RED", -15, 0, -11, FlowColor.Red);
            Sink("C-BLUE", 15, 0, -12, FlowColor.Blue);
            Sink("C-YELLOW", 17, 6, 14, FlowColor.Yellow);
            Sink("C-GREEN", -17, 6, 15, FlowColor.Green);
            Sink("C-PURPLE", 0, 12, 0, FlowColor.Purple);
            Sink("SW-RED", -145, 0, -120, FlowColor.Red);
            Sink("SE-BLUE", 156, 12, -127, FlowColor.Blue);
            Sink("NW-YELLOW", -157, 18, 137, FlowColor.Yellow);
            Sink("NE-GREEN", 168, 24, 139, FlowColor.Green);
            // Dense sectors and deliberate gaps replace uniform stepping stones. The two rings have different weak directions.
            AddRing("I", new[] { 8f, 27, 49, 151, 175, 199, 273, 310 },
                new[] { 43f, 39, 44, 41, 44, 40, 40, 45 }, new[] { 10f, 18, 24, 14, 10, 22, 26, 16 });
            AddRing("O", new[] { 30f, 42, 67, 89, 153, 172, 193, 215, 235, 258, 278, 300 },
                new[] { 110f, 91, 119, 121, 98, 114, 120, 96, 123, 115, 100, 125 },
                new[] { 30f, 30, 24, 24, 30, 18, 18, 24, 28, 24, 32, 30 });
            void AddRing(string prefix, float[] angles, float[] radii, float[] rises)
            {
                for (int i = 0; i < angles.Length; i++)
                {
                    float angle = angles[i] * Mathf.Deg2Rad;
                    nodes.Add(new RelayNodePlacement { Id = prefix + (i + 1).ToString("00"),
                        Position = new Vector3(Mathf.Cos(angle) * radii[i], 0, Mathf.Sin(angle) * radii[i]),
                        MaximumRise = rises[i], MaxIncoming = 3, MaxOutgoing = 3 });
                }
            }
            // One Source in each of twelve equal-area cells (3 columns by 4 rows).
            Vector3[] sources = { new(-131, 0, -103), new(12, 0, -133), new(132, 12, -109),
                new(-88, 0, -48), new(-5, 0, -30), new(154, 0, -23),
                new(-144, 12, 24), new(26, 0, 46), new(100, 0, 68),
                new(-122, 18, 131), new(-18, 0, 91), new(143, 24, 122) };
            for (int i = 0; i < sources.Length; i++)
                nodes.Add(new SourceNodePlacement { Id = "S" + (i + 1).ToString("00"), Position = sources[i],
                    MaxOutgoing = 3, GenerationInterval = 12, GenerationDelay = 40 });
            var buildings = new List<Bounds> {
                new(new Vector3(-29, 4, 3), new Vector3(8, 8, 14)),
                new(new Vector3(30, 5, 4), new Vector3(8, 10, 14)),
                new(new Vector3(-68, 6, 0), new Vector3(10, 12, 38)),
                new(new Vector3(68, 6, 0), new Vector3(10, 12, 38)),
                new(new Vector3(0, 9, -71), new Vector3(42, 18, 8)),
                new(new Vector3(0, 9, 71), new Vector3(42, 18, 8)),
                new(new Vector3(-94, 12, 88), new Vector3(12, 24, 14)),
                new(new Vector3(94, 12, -88), new Vector3(12, 24, 14)) };
            foreach (var node in nodes.Where(n => n.Position.y > 0))
            {
                float roof = node.Position.y - 1;
                buildings.Add(new Bounds(new Vector3(node.Position.x, roof * 0.5f, node.Position.z), new Vector3(8, roof, 8)));
            }
            stage.Buildings = buildings.ToArray();
            int Unlock(NodePlacement node)
            {
                for (int i = 0; i < Sizes.Length; i++)
                {
                    Rect area = Area(i);
                    if (node.Position.x >= area.xMin + 4 && node.Position.x <= area.xMax - 4 &&
                        node.Position.z >= area.yMin + 4 && node.Position.z <= area.yMax - 4)
                        return node.Id == "S12" ? Math.Max(i, 9) : i;
                }
                throw new InvalidOperationException("Node needs space inside the maximum area: " + node.Id);
            }
            stage.Nodes = nodes.Where(n => Unlock(n) == 0).ToArray();
            // Provisional pressure curve: additional Sources and shorter intervals outpace forwarding relief.
            float[] intervals = { 12, 7.8f, 7.2f, 7, 6.5f, 6.2f, 6, 5.2f, 5.1f, 5 };
            stage.Waves = Enumerable.Range(1, 9).Select(i => new StageConfiguration.WavePlacement {
                StartSeconds = i * 90, IntervalScale = intervals[i] / 12, ExpandsArea = true, WalkableArea = Area(i),
                Additions = nodes.Where(n => Unlock(n) == i).ToArray() }).ToArray();
        }
    }
}
