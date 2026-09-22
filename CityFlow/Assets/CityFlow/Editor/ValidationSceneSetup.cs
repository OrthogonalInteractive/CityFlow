#nullable enable

using CityFlow.Composition;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Infrastructure.Configuration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CityFlow.Editor
{
    public static class ValidationSceneSetup
    {
        [MenuItem("City Flow/Set Up Validation City")]
        public static void Create()
        {
            const string directory = "Assets/CityFlow/Settings/Gameplay/";
            var settings = AssetDatabase.LoadAssetAtPath<GameplaySettings>(directory + "ValidationGameplay.asset");
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<GameplaySettings>();
                AssetDatabase.CreateAsset(settings, directory + "ValidationGameplay.asset");
            }
            var stage = AssetDatabase.LoadAssetAtPath<StageConfiguration>(directory + "ValidationStage.asset");
            if (stage == null)
            {
                stage = ScriptableObject.CreateInstance<StageConfiguration>();
                stage.Buildings = new[] {
                    new Bounds(new Vector3(0, 6, 0), new Vector3(18, 12, 16)),
                    new Bounds(new Vector3(20, 4, 19), new Vector3(10, 8, 22)),
                    new Bounds(new Vector3(36, 7, 19), new Vector3(10, 14, 22)),
                    new Bounds(new Vector3(-19, 5, 17), new Vector3(12, 10, 17)),
                    new Bounds(new Vector3(-17, 3, -31), new Vector3(12, 6, 8)),
                    new Bounds(new Vector3(13, 4, -31), new Vector3(16, 8, 8)) };
                stage.Nodes = new[] {
                    Node("S1", NodeKind.Source, -38, -20), Node("R1", NodeKind.Relay, -12, -20),
                    Node("R2", NodeKind.Relay, 28, 18), Node("RED", NodeKind.Sink, -38, 22, FlowColor.Red),
                    Node("BLUE", NodeKind.Sink, 49, 22, FlowColor.Blue) };
                AssetDatabase.CreateAsset(stage, directory + "ValidationStage.asset");
            }
            var scene = EditorSceneManager.OpenScene("Assets/CityFlow/Scenes/Bootstrap.unity", OpenSceneMode.Single);
            var scope = Object.FindAnyObjectByType<CityFlowLifetimeScope>();
            scope.SetConfiguration(settings, stage);
            ValidationHudSetup.Configure(scope);
            EditorUtility.SetDirty(scope);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }
        [MenuItem("City Flow/Set Up Fixed Validation Network")]
        public static void CreateNetwork()
        {
            Create();
            var stage = AssetDatabase.LoadAssetAtPath<StageConfiguration>("Assets/CityFlow/Settings/Gameplay/ValidationStage.asset");
            if (stage.Lines.Length != 0) return;
            stage.Lines = new[] {
                Line("S1", "RED", new Vector3(-38, 0, -20), new Vector3(-38, 0, 22)),
                Line("S1", "BLUE", new Vector3(-38, 0, -20), new Vector3(-38, 0, -40), new Vector3(49, 0, -40), new Vector3(49, 0, 22)),
                Line("S1", "R1", new Vector3(-38, 0, -20), new Vector3(-12, 0, -20)),
                Line("R1", "R2", new Vector3(-12, 0, -20), new Vector3(28, 0, -20), new Vector3(28, 0, 18)),
                Line("R2", "BLUE", new Vector3(28, 0, 18), new Vector3(28, 0, 35), new Vector3(49, 0, 35), new Vector3(49, 0, 22)) };
            EditorUtility.SetDirty(stage); AssetDatabase.SaveAssets();
        }
        private static StageConfiguration.LinePlacement Line(string source, string destination, params Vector3[] points) =>
            new StageConfiguration.LinePlacement { SourceId = source, DestinationId = destination, Points = points };

        private static NodePlacement Node(string id, NodeKind kind, float x, float z,
            FlowColor color = FlowColor.Red)
        {
            NodePlacement placement = kind switch
            {
                NodeKind.Source => new SourceNodePlacement { GenerationInterval = 0.75f },
                NodeKind.Relay => new RelayNodePlacement(),
                NodeKind.Sink => new SinkNodePlacement { SinkColor = color },
                _ => throw new System.ArgumentOutOfRangeException(nameof(kind))
            };
            placement.Id = id;
            placement.Position = new Vector3(x, 0, z);
            return placement;
        }
    }
}
