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
        private static readonly Rect PlayArea = new Rect(-530f, -860f, 1280f, 2090f);
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
        private const float WestTowerRoof = 186.1f;     // West high-rise (185.1 m Bounds)
        private const float EastTowerRoof = 209.4f;     // East high-rise (208.4 m Bounds)
        private const float NorthWestRoof = 205.68f;    // Northern Marunouchi tower (204.577 m Bounds)
        private const float NorthPurpleRoof = 191.5f;   // Northern west tower (190.395 m Bounds)
        private const float NorthEastRoof = 203.72f;    // Northern east tower (202.614 m Bounds)
        private const float SouthTowerRoof = 210.76f;   // Southern tower (209.655 m Bounds)
        private const float FarEastRoof = 187.46f;      // Eastern tower (186.353 m Bounds)
        private const float SouthRoof = 32.54f;         // Southern street roof (31.438 m Bounds)
        private const float NorthRoof = 43.29f;         // Northern street roof (42.191 m Bounds)
        private const float NorthRelayRoof = 34.18f;    // Northern relay roof (33.076 m Bounds)

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
                    Sink("RED-M", FlowColor.Red, -130, -75, DomeRoof), Sink("BLUE-M", FlowColor.Blue, -120, 100, DomeRoof),
                    Sink("GREEN-M", FlowColor.Green, 135, -50, AnnexRoof), Sink("YELLOW-M", FlowColor.Yellow, -50, -652, SouthRoof)),
                // Wave 5: middle Sources and the first middle hub.
                Wave(240, 0.85f,
                    Source("SM1", -130, -85, interval: 18, height: DomeRoof), Source("SM2", 125, -60, interval: 18, height: AnnexRoof),
                    Relay("RM1", 120, -70, AnnexRoof, MiddleCeiling)),
                // Wave 6: more middle capacity and the first east ground Sinks.
                Wave(300, 0.85f,
                    Source("SM3", 429, 1033, interval: 45, height: NorthRoof),
                    Relay("RM2", 99.6f, 1016.7f, NorthRelayRoof, MiddleCeiling),
                    Relay("RE1", 215, -60, GroundHeight, GroundHeight + LowerRise),
                    Sink("RED-E", FlowColor.Red, 190, -30), Sink("BLUE-E", FlowColor.Blue, 205, -85)),
                // Wave 7: the upper tier. Purple exists only up there; the bridge sits on the middle annex roof.
                Wave(360, 0.8f,
                    Bridge("RU", 125, -75, AnnexRoof, UpperCeiling),
                    Sink("PURPLE-U", FlowColor.Purple, 195, 45, EastTowerRoof),
                    Source("SU1", -330, -20, interval: 20, height: WestTowerRoof)),
                Wave(420, 0.8f,
                    Source("SU2", -400, 710, interval: 45, height: NorthWestRoof),
                    Source("SE2", 670, 950, interval: 45),
                    Sink("GREEN-E", FlowColor.Green, 675, 1080), Sink("YELLOW-E", FlowColor.Yellow, 660, -505),
                    Relay("RE2", 430, 730, GroundHeight, GroundHeight + LowerRise)),
                Wave(480, 0.8f,
                    Source("SU3", 540, 620, interval: 45, height: NorthEastRoof),
                    Relay("RU2", -350, 800, NorthPurpleRoof, UpperCeiling),
                    Sink("PURPLE-U2", FlowColor.Purple, -335, 830, NorthPurpleRoof),
                    Relay("R4", -400, -350, GroundHeight, GroundHeight + LowerRise),
                    Source("S7", -450, 1000, interval: 45)),
                Wave(540, 0.75f,
                    Source("SU4", 15, -300, interval: 45, height: SouthTowerRoof),
                    Source("SU5", 650, 65, interval: 45, height: FarEastRoof),
                    Source("SM4", -52, -660, interval: 45, height: SouthRoof),
                    Source("SE3", 660, -625, interval: 45))
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
