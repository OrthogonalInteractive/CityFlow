#nullable enable

using System;
using System.Linq;
using CityFlow.Composition;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Infrastructure.Configuration;
using CityFlow.Presentation.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CityFlow.Editor
{
    public static class TokyoStationGameplaySetup
    {
        private const string SettingsRoot = "Assets/CityFlow/Settings/Gameplay/";
        // Provisional planar plaza for the small wiring prototype; imported terrain remains visual scenery.
        private const float GroundHeight = 3.7f;
        private static readonly Rect PlayArea = new Rect(-230f, -40f, 105f, 125f);
        private static readonly Vector3 OverviewFocus = new Vector3(-150f, GroundHeight, 20f);
        private static readonly Quaternion OverviewRotation = Quaternion.Euler(75f, 80f, 0f);

        [MenuItem("City Flow/Tokyo Station/Set Up Playable WiringLab")]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Configure the station game outside Play Mode.");
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != PlateauTokyoStationStyleSetup.ScenePath)
                throw new InvalidOperationException("Open TokyoStationWiringLab first.");
            if (scene.GetRootGameObjects().Any(root => root.GetComponent<CityFlowLifetimeScope>() != null))
                throw new InvalidOperationException("This scene already has gameplay; edit its settings directly.");
            GameObject[] roots = scene.GetRootGameObjects();
            string[] packages = { "Building", "Road", "Relief", "Vegetation", "Bridge" };
            GameObject[] city = packages.Select(package => roots.Single(root => root.name == "TokyoStation_" + package)).ToArray();
            var decoration = roots.Single(root => root.name == "WiringLab Appearance");
            var settings = ScriptableObject.CreateInstance<GameplaySettings>();
            settings.FlowSpeed = 12;
            var stage = ScriptableObject.CreateInstance<StageConfiguration>();
            stage.GroundHeight = GroundHeight;
            stage.WalkableArea = PlayArea;
            stage.Buildings = city.Where(root => root.name == "TokyoStation_Building" || root.name == "TokyoStation_Bridge")
                .SelectMany(root => root.GetComponentsInChildren<MeshRenderer>())
                .Where(renderer => renderer.enabled).Select(renderer => renderer.bounds)
                .Where(bounds => new Rect(bounds.min.x, bounds.min.z, bounds.size.x, bounds.size.z).Overlaps(PlayArea)).ToArray();
            stage.Nodes = new NodePlacement[]
            {
                new SourceNodePlacement { Id = "S1", Position = Position(-210, -20), MaxOutgoing = 6,
                    GenerationDelay = 45, GenerationInterval = 3 },
                new RelayNodePlacement { Id = "R1", Position = Position(-210, 10), MaxIncoming = 3, MaxOutgoing = 5 },
                Sink("RED", FlowColor.Red, -180, -5), Sink("BLUE", FlowColor.Blue, -155, 10),
                Sink("YELLOW", FlowColor.Yellow, -155, 40), Sink("GREEN", FlowColor.Green, -180, 60),
                Sink("PURPLE", FlowColor.Purple, -210, 40)
            };
            settings.Validate();
            stage.Load(settings.Clearance);
            SaveNewAsset(settings, SettingsRoot + "TokyoStationGameplay.asset");
            SaveNewAsset(stage, SettingsRoot + "TokyoStationStage.asset");

            var game = new GameObject("Tokyo Station Gameplay");
            var scenery = game.AddComponent<AuthoredCityScenery>();
            scenery.Configure(city.Append(decoration).ToArray(),
                city.Where(root => root.name == "TokyoStation_Building" || root.name == "TokyoStation_Bridge")
                    .Append(decoration).ToArray(), OverviewFocus);
            var scope = game.AddComponent<CityFlowLifetimeScope>();
            scope.SetConfiguration(settings, stage);
            scope.SetScenery(scenery);
            ValidationHudSetup.Configure(scope);
            ObstacleAppearanceSetup.Configure(scope);
            RelayAppearanceSetup.Configure(scope);

            var camera = roots.SelectMany(root => root.GetComponentsInChildren<Camera>()).Single();
            camera.transform.SetPositionAndRotation(OverviewFocus + OverviewRotation * Vector3.back * 700f, OverviewRotation);
            camera.orthographic = true;
            camera.orthographicSize = 110;
            camera.nearClipPlane = 0.5f;
            camera.farClipPlane = 6000f;
            if (!EditorBuildSettings.scenes.Any(item => item.path == scene.path))
                EditorBuildSettings.scenes = EditorBuildSettings.scenes.Append(new EditorBuildSettingsScene(scene.path, true)).ToArray();
            EditorUtility.SetDirty(scope);
            EditorUtility.SetDirty(scenery);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save the playable station scene.");
            FocusStation();
            Debug.Log("TokyoStationWiringLab is playable: seven Nodes, five Sink colors, no initial Lines.");
        }

        public static void FocusStation()
        {
            var view = SceneView.lastActiveSceneView;
            if (view == null) return;
            view.LookAt(OverviewFocus, OverviewRotation, 145f, true, true);
            view.Repaint();
        }

        private static Vector3 Position(float x, float z) => new Vector3(x, GroundHeight, z);
        private static SinkNodePlacement Sink(string id, FlowColor color, float x, float z) =>
            new SinkNodePlacement { Id = id, Position = Position(x, z), SinkColor = color, MaxIncoming = 3 };

        private static void SaveNewAsset(UnityEngine.Object asset, string path)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                throw new InvalidOperationException("Existing gameplay settings will not be overwritten: " + path);
            AssetDatabase.CreateAsset(asset, path);
        }
    }
}
