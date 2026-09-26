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
        // Building roofs use surveyed mesh heights; the plaza keeps its shared Ground datum.
        private const float GroundHeight = 3.7f;
        private static readonly Rect PlayArea = new Rect(-355f, -90f, 600f, 195f);
        private static readonly Vector3 OverviewFocus = new Vector3(-55f, 60f, 10f);
        private static readonly Quaternion OverviewRotation = Quaternion.Euler(60f, -10f, 0f);

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
            settings.FlowSpeed = 24;
            var stage = ScriptableObject.CreateInstance<StageConfiguration>();
            stage.GroundHeight = GroundHeight;
            stage.WalkableArea = PlayArea;
            stage.Buildings = ReadBuildings(city);
            ConfigureThreeWaveLayout(stage);
            settings.Validate();
            stage.LoadWaves(stage.Load(settings.Clearance), settings.Clearance);
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
            camera.transform.SetPositionAndRotation(OverviewFocus + OverviewRotation * Vector3.back * 850f, OverviewRotation);
            camera.orthographic = true;
            camera.orthographicSize = 220;
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
            Debug.Log("TokyoStationWiringLab: four opening Nodes, three Waves, no initial Lines.");
        }

        [MenuItem("City Flow/Tokyo Station/Apply Three-Wave Game Layout")]
        public static void ApplyThreeWaveLayout()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Configure the station game outside Play Mode.");
            var stage = AssetDatabase.LoadAssetAtPath<StageConfiguration>(SettingsRoot + "TokyoStationStage.asset");
            var settings = AssetDatabase.LoadAssetAtPath<GameplaySettings>(SettingsRoot + "TokyoStationGameplay.asset");
            if (stage == null || settings == null)
                throw new InvalidOperationException("Create the station gameplay assets first.");
            var scene = SceneManager.GetActiveScene();
            if (scene.path != PlateauTokyoStationStyleSetup.ScenePath)
                throw new InvalidOperationException("Open TokyoStationWiringLab to capture its building volumes.");
            Undo.RecordObject(stage, "Apply station three-Wave layout");
            Undo.RecordObject(settings, "Tune station transport speed");
            settings.FlowSpeed = 24;
            stage.Buildings = ReadBuildings(scene.GetRootGameObjects());
            ConfigureThreeWaveLayout(stage);
            stage.LoadWaves(stage.Load(settings.Clearance), settings.Clearance);
            EditorUtility.SetDirty(stage);
            AssetDatabase.SaveAssetIfDirty(stage);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssetIfDirty(settings);
        }

        private static void ConfigureThreeWaveLayout(StageConfiguration stage)
        {
            stage.GroundHeight = GroundHeight;
            stage.MaximumAltitude = 220;
            stage.WalkableArea = PlayArea;
            stage.ExpandsWithWaves = false;
            stage.MaximumArea = PlayArea;
            stage.Lines = Array.Empty<StageConfiguration.LinePlacement>();
            stage.Nodes = new NodePlacement[]
            {
                Source("S1", -207, -24, 15), Relay("R1", -192, 4, 12),
                Sink("RED", FlowColor.Red, -178, -3), Sink("BLUE", FlowColor.Blue, -156, 20)
            };
            // Authored pacing [s]: two colors first, then new Sources and Relay decisions.
            stage.Waves = new[]
            {
                new StageConfiguration.WavePlacement
                {
                    StartSeconds = 60, IntervalScale = 0.95f,
                    Additions = new NodePlacement[]
                    {
                        Sink("GREEN", FlowColor.Green, -125.25f, -79.1f, 42.4f),
                        Source("S2", -324.2f, -26.7f, 20, 186.1f, 12),
                        Relay("R2", -183, 40, 190, GroundHeight, 4)
                    }
                },
                new StageConfiguration.WavePlacement
                {
                    StartSeconds = 120, IntervalScale = 0.9f,
                    Additions = new NodePlacement[]
                    {
                        Sink("YELLOW", FlowColor.Yellow, 190.8f, 11.5f, 209.4f),
                        Sink("PURPLE", FlowColor.Purple, -210, 65),
                        Source("S3", 151.7f, 85.5f, 20, 209.4f, 12),
                        Relay("R3", 125.3f, -75.15f, 185, 30.2f)
                    }
                }
            };
        }

        public static void FocusStation()
        {
            var view = SceneView.lastActiveSceneView;
            if (view == null) return;
            view.LookAt(OverviewFocus, OverviewRotation, 300f, true, true);
            view.Repaint();
        }

        private static Bounds[] ReadBuildings(GameObject[] roots) => roots
            .Where(root => root.name == "TokyoStation_Building" || root.name == "TokyoStation_Bridge")
            .SelectMany(root => root.GetComponentsInChildren<MeshRenderer>())
            .Where(renderer => renderer.enabled).Select(renderer => renderer.bounds)
            .Where(bounds => new Rect(bounds.min.x, bounds.min.z, bounds.size.x, bounds.size.z).Overlaps(PlayArea)).ToArray();

        private static Vector3 Position(float x, float z, float height = GroundHeight) => new Vector3(x, height, z);
        private static SourceNodePlacement Source(string id, float x, float z, float delay,
            float height = GroundHeight, float interval = 3.6f) =>
            new SourceNodePlacement { Id = id, Position = Position(x, z, height), MaxOutgoing = 2,
                GenerationInterval = interval, GenerationDelay = delay };
        private static RelayNodePlacement Relay(string id, float x, float z, float rise,
            float height = GroundHeight, int outgoing = 3) =>
            new RelayNodePlacement { Id = id, Position = Position(x, z, height), MaxIncoming = 3,
                MaxOutgoing = outgoing, MaximumRise = rise };
        private static SinkNodePlacement Sink(string id, FlowColor color, float x, float z, float height = GroundHeight) =>
            new SinkNodePlacement { Id = id, Position = Position(x, z, height), SinkColor = color, MaxIncoming = 3 };

        private static void SaveNewAsset(UnityEngine.Object asset, string path)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                throw new InvalidOperationException("Existing gameplay settings will not be overwritten: " + path);
            AssetDatabase.CreateAsset(asset, path);
        }
    }
}
