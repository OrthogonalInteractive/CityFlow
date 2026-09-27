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
            ConfigureTierLayout(stage);
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
            Debug.Log("TokyoStationWiringLab: five opening Nodes on the west plaza, ten tiered Waves, no initial Lines.");
        }

        [MenuItem("City Flow/Tokyo Station/Apply Tier Game Layout")]
        public static void ApplyTierLayout()
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
            Undo.RecordObject(stage, "Apply station tier layout");
            Undo.RecordObject(settings, "Tune station transport speed");
            settings.FlowSpeed = 24;
            stage.Buildings = ReadBuildings(scene.GetRootGameObjects());
            ConfigureTierLayout(stage);
            stage.LoadWaves(stage.Load(settings.Clearance), settings.Clearance);
            EditorUtility.SetDirty(stage);
            AssetDatabase.SaveAssetIfDirty(stage);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssetIfDirty(settings);
        }

        // v0.2 Δ1.3 tiers. Heights are bands, not single planes: Relays reach from their placement Y to the band ceiling.
        private const float LowerRise = 12f;            // [m] Lower tier band: Ground .. Ground + 12
        private const float MiddleCeiling = 50f;        // [m] Middle tier band: 25 .. 50 (absolute Y)
        private const float UpperCeiling = 215f;        // [m] Upper tier band: 180 .. 215 (absolute Y)
        // Surveyed roofs: Renderer Bounds top + about 1 m. The station dome mesh peaks near 38 m under its 41.3 m Bounds.
        private const float DomeRoof = 42.4f;           // Marunouchi station building (south dome area)
        private const float AnnexRoof = 30.2f;          // East annex beside the station (29.1 m Bounds)
        private const float EastEdgeRoof = 47.6f;       // Small building at the east edge (46.5 m Bounds)
        private const float WestTowerRoof = 186.1f;     // West high-rise (185.1 m Bounds)
        private const float EastTowerRoof = 209.4f;     // East high-rise (208.4 m Bounds)
        private const float EastTower2Roof = 184.2f;    // Second east high-rise, only its south end lies inside the area (183.2 m Bounds)

        private static void ConfigureTierLayout(StageConfiguration stage)
        {
            stage.GroundHeight = GroundHeight;
            stage.MaximumAltitude = 220;
            stage.WalkableArea = PlayArea;
            stage.ExpandsWithWaves = false;
            stage.MaximumArea = PlayArea;
            stage.Lines = Array.Empty<StageConfiguration.LinePlacement>();
            // Waves 1-3: the west (Marunouchi) plaza only. The station complex blocks every ground path to the east side.
            stage.Nodes = new NodePlacement[]
            {
                Source("S1", -207, -24, 15, interval: 6),
                Source("S2", -340, 60, 15, interval: 8),
                Relay("R1", -192, 4, GroundHeight, GroundHeight + LowerRise),
                Sink("RED", FlowColor.Red, -178, -3), Sink("BLUE", FlowColor.Blue, -156, 20)
            };
            stage.Waves = new[]
            {
                Wave(60, 0.95f,
                    Source("S3", -300, -85, interval: 8), Source("S4", -240, 90, interval: 10),
                    Relay("R2", -185, 45, GroundHeight, GroundHeight + LowerRise),
                    Sink("GREEN", FlowColor.Green, -160, 60)),
                Wave(120, 0.9f,
                    Source("S5", -160, 95, interval: 10), Source("S6", -200, -85, interval: 10),
                    Relay("R3", -190, -50, GroundHeight, GroundHeight + LowerRise),
                    Sink("YELLOW", FlowColor.Yellow, -180, -65)),
                // Wave 4: bridges to the middle tier on both sides, the first east Source, four middle Sinks.
                Wave(180, 0.9f,
                    Bridge("WB", -175, 80, GroundHeight, MiddleCeiling), Bridge("EB", 205, -40, GroundHeight, MiddleCeiling),
                    Source("SE1", 185, -85, interval: 18),
                    Sink("RED-M", FlowColor.Red, -130, -75, DomeRoof), Sink("BLUE-M", FlowColor.Blue, -120, -85, DomeRoof),
                    Sink("GREEN-M", FlowColor.Green, 135, -50, AnnexRoof), Sink("YELLOW-M", FlowColor.Yellow, 115, -85, AnnexRoof)),
                // Wave 5: middle Sources and the first middle hub.
                Wave(240, 0.85f,
                    Source("SM1", -130, -85, interval: 18, height: DomeRoof), Source("SM2", 125, -60, interval: 18, height: AnnexRoof),
                    Relay("RM1", 120, -70, AnnexRoof, MiddleCeiling)),
                // Wave 6: more middle capacity and the first east ground Sinks.
                Wave(300, 0.85f,
                    Source("SM3", 237, -82, interval: 18, height: EastEdgeRoof),
                    Relay("RM2", 130, -55, AnnexRoof, MiddleCeiling),
                    Relay("RE1", 215, -60, GroundHeight, GroundHeight + LowerRise),
                    Sink("RED-E", FlowColor.Red, 190, -30), Sink("BLUE-E", FlowColor.Blue, 205, -85)),
                // Wave 7: the upper tier. Purple exists only up there; the bridge sits on the middle annex roof.
                Wave(360, 0.8f,
                    Bridge("RU", 125, -75, AnnexRoof, UpperCeiling),
                    Sink("PURPLE-U", FlowColor.Purple, 195, 45, EastTowerRoof),
                    Source("SU1", -330, -20, interval: 20, height: WestTowerRoof)),
                Wave(420, 0.8f,
                    Source("SU2", 150, 75, interval: 20, height: EastTowerRoof),
                    Source("SE2", 240, 70, interval: 18),
                    Sink("GREEN-E", FlowColor.Green, 240, 30), Sink("YELLOW-E", FlowColor.Yellow, 200, -70),
                    Relay("RE2", 235, 20, GroundHeight, GroundHeight + LowerRise)),
                Wave(480, 0.8f,
                    Source("SU3", -285, -20, interval: 20, height: WestTowerRoof),
                    Relay("RU2", -300, 5, WestTowerRoof, UpperCeiling),
                    Sink("PURPLE-U2", FlowColor.Purple, -330, 10, WestTowerRoof),
                    Relay("R4", -230, -40, GroundHeight, GroundHeight + LowerRise),
                    Source("S7", -300, 90, interval: 12)),
                Wave(540, 0.75f,
                    Source("SU4", 195, 25, interval: 20, height: EastTowerRoof),
                    Source("SU5", 190, 95, interval: 20, height: EastTower2Roof),
                    Source("SM4", -120, -75, interval: 18, height: DomeRoof),
                    Source("SE3", 240, 50, interval: 18))
            };
        }

        private static StageConfiguration.WavePlacement Wave(float startSeconds, float intervalScale, params NodePlacement[] additions) =>
            new StageConfiguration.WavePlacement { StartSeconds = startSeconds, IntervalScale = intervalScale, Additions = additions };

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
        // Provisional pacing [s]: new Sources prepare for 20 s, then wait one generation interval.
        private static SourceNodePlacement Source(string id, float x, float z, float delay = 20,
            float height = GroundHeight, float interval = 6) =>
            new SourceNodePlacement { Id = id, Position = Position(x, z, height), MaxOutgoing = 2,
                GenerationInterval = interval, GenerationDelay = delay };
        // Relays reach from their placement height up to the tier ceiling; provisional IN 4 / OUT 5 for hubs
        // (four ground colors plus one link to another Relay).
        private static RelayNodePlacement Relay(string id, float x, float z, float height, float ceiling, int incoming = 4) =>
            new RelayNodePlacement { Id = id, Position = Position(x, z, height), MaxIncoming = incoming,
                MaxOutgoing = 5, MaximumRise = ceiling - height };
        // Bridges collect traffic from a whole tier; provisional IN 8 / OUT 5. Their Buffer and Line capacity stay the bottleneck.
        private static RelayNodePlacement Bridge(string id, float x, float z, float height, float ceiling) =>
            Relay(id, x, z, height, ceiling, incoming: 8);
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
