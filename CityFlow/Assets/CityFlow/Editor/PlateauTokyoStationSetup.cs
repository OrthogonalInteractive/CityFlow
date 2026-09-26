#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using PLATEAU.CityAdjust.ChangeActive;
using PLATEAU.CityAdjust.ConvertToAsset;
using PLATEAU.CityImport.AreaSelector;
using PLATEAU.CityImport.Config;
using PLATEAU.CityImport.Config.PackageImportConfigs;
using PLATEAU.CityImport.Import;
using PLATEAU.CityInfo;
using PLATEAU.Dataset;
using PLATEAU.Geometries;
using PLATEAU.Native;
using PLATEAU.Util;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CityFlow.Editor
{
    public static class PlateauTokyoStationSetup
    {
        public const string ScenePath = "Assets/CityFlow/Scenes/TokyoStationInspection.unity";
        public const string AssetRoot = "Assets/CityFlow/Art/PLATEAU/TokyoStation";
        public static string Status { get; private set; } = "Idle";
        public static bool IsRunning => cancellation != null;

        private static CancellationTokenSource? cancellation;
        private static readonly string[] MeshCodes = { "53394611", "53394621" };
        private static readonly PredefinedCityModelPackage[] Packages =
        {
            PredefinedCityModelPackage.Building, PredefinedCityModelPackage.Road,
            PredefinedCityModelPackage.Relief, PredefinedCityModelPackage.Vegetation,
            PredefinedCityModelPackage.Bridge
        };

        public static void Start(string sourceDirectory)
        {
            if (IsRunning || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Import requires an idle Editor outside Play Mode.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save open scenes before importing.");
            if (!File.Exists(Path.Combine(sourceDirectory, "udx/bldg/53394611_bldg_6697_op.gml")))
                throw new DirectoryNotFoundException("Extract the Tokyo Station CityGML subset first.");
            if (File.Exists(ScenePath) || AssetDatabase.IsValidFolder(AssetRoot))
                throw new InvalidOperationException("The inspection scene or output assets already exist.");

            EnsureFolder(AssetRoot);
            cancellation = new CancellationTokenSource();
            AssemblyReloadEvents.beforeAssemblyReload += Cancel;
            EditorApplication.quitting += Cancel;
            // This Editor operation owns its token, completion state, and exception reporting.
            ImportAsync(Path.GetFullPath(sourceDirectory), cancellation.Token).Forget(exception =>
            {
                Status = "Failed: " + exception.Message;
                Debug.LogException(exception);
            });
        }

        public static void Cancel()
        {
            cancellation?.Cancel();
        }

        private static async UniTask ImportAsync(string sourceDirectory, CancellationToken token)
        {
            try
            {
                Status = "Preparing import configuration";
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                ConfigureView();
                EditorSceneManager.SaveScene(scene, ScenePath);
                var before = new ConfigBeforeAreaSelect(new DatasetSourceConfigLocal(sourceDirectory), 9);
                var area = new AreaSelectResult(before, GridCodeList.CreateFromGridCodesStr(MeshCodes),
                    AreaSelectResult.ResultReason.Confirm);
                var config = CityImportConfig.CreateWithAreaSelectResult(area);
                using (var reference = GeoReference.Create(new PlateauVector3d(0, 0, 0), 1f, CoordinateSystem.EUN, 9))
                    config.ReferencePoint = reference.Project(new GeoCoordinate(35.681236, 139.767125, 0));

                foreach (var pair in config.PackageImportConfigDict.ForEachPackagePair)
                {
                    var settings = pair.Value;
                    settings.ImportPackage = false;
                    settings.IncludeTexture = true;
                    settings.EnableTexturePacking = true;
                    settings.TexturePackingResolution = TexturePackingResolution.W4096H4096;
                    settings.DoSetMeshCollider = true;
                    settings.DoSetAttrInfo = true;
                    int maximum = pair.Key == PredefinedCityModelPackage.Road ||
                                  pair.Key == PredefinedCityModelPackage.Vegetation ? 3 : 2;
                    maximum = Math.Min(maximum, settings.LODRange.AvailableMaxLOD);
                    if (maximum >= 1) settings.LODRange = new LODRange(1, maximum, settings.LODRange.AvailableMaxLOD);
                }

                // The SDK exposes this option on an internal configuration type.
                var relief = config.GetConfigForPackage(PredefinedCityModelPackage.Relief);
                var attachMapTile = relief.GetType().GetProperty("AttachMapTile") ??
                    throw new InvalidOperationException("SDK relief configuration no longer exposes AttachMapTile.");
                attachMapTile.SetValue(relief, false);

                foreach (var package in Packages)
                {
                    token.ThrowIfCancellationRequested();
                    foreach (var pair in config.PackageImportConfigDict.ForEachPackagePair)
                        pair.Value.ImportPackage = pair.Key == package && pair.Value.LODRange.AvailableMaxLOD >= 1;
                    Status = "Importing " + package;
                    var existingRoots = Object.FindObjectsByType<PLATEAUInstancedCityModel>();
                    await CityImporter.ImportAsync(config, new ImportProgress(package.ToString()), token,
                        new IPostGmlImportProcessor[] { new CityDuplicateProcessor() }).AsUniTask();
                    token.ThrowIfCancellationRequested();
                    var root = Object.FindObjectsByType<PLATEAUInstancedCityModel>()
                        .Single(model => !existingRoots.Contains(model));
                    if (root.GetComponentsInChildren<MeshRenderer>(true).Length == 0)
                        throw new InvalidOperationException("No geometry was imported for " + package);
                    root.name = "TokyoStation_" + package;
                    Status = "Saving " + package + " assets";
                    string folder = AssetRoot + "/" + package;
                    EnsureFolder(folder);
                    using (var progress = new DummyProgressBar())
                        new ConvertToAsset().ConvertCore(new ConvertToAssetConfig(root.gameObject, folder), progress);
                    AssetDatabase.SaveAssets();
                    EditorSceneManager.SaveScene(scene, ScenePath);
                    await Resources.UnloadUnusedAssets().ToUniTask(cancellationToken: token);
                }

                // Texture loading creates an SDK coroutine host that is not part of the city data.
                foreach (var root in scene.GetRootGameObjects())
                    if (root.name == "CoroutineDispatcher" && root.GetComponents<MonoBehaviour>().Any(component =>
                            component != null && component.GetType().FullName == "PLATEAU.Util.Async.CoroutineDispatcher"))
                        Object.DestroyImmediate(root);
                FocusStation();
                EditorSceneManager.SaveScene(scene, ScenePath);
                Status = "Completed: " + ScenePath;
                Debug.Log(Status);
            }
            catch (OperationCanceledException)
            {
                Status = "Cancelled";
            }
            finally
            {
                AssemblyReloadEvents.beforeAssemblyReload -= Cancel;
                EditorApplication.quitting -= Cancel;
                cancellation?.Dispose();
                cancellation = null;
            }
        }

        private static void ConfigureView()
        {
            var cameraObject = new GameObject("Inspection Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>();
            camera.transform.position = new Vector3(-600f, 500f, -550f);
            camera.transform.LookAt(new Vector3(0f, 25f, 100f));
            camera.nearClipPlane = 0.5f;
            camera.farClipPlane = 6000f;
            camera.fieldOfView = 55f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.68f, 0.76f, 0.83f);

            var sunlight = new GameObject("Sunlight", typeof(Light)).GetComponent<Light>();
            sunlight.type = LightType.Directional;
            sunlight.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            sunlight.intensity = 1.4f;
            sunlight.shadows = LightShadows.Soft;
            RenderSettings.sun = sunlight;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.68f, 0.74f, 0.82f);
            RenderSettings.ambientEquatorColor = new Color(0.5f, 0.53f, 0.58f);
            RenderSettings.ambientGroundColor = new Color(0.3f, 0.32f, 0.34f);
            RenderSettings.fog = false;
            new GameObject("Tokyo Station Reference - 35.681236N 139.767125E").transform.position = Vector3.zero;
        }

        public static void FocusStation()
        {
            var view = SceneView.lastActiveSceneView;
            if (view == null) return;
            view.LookAt(new Vector3(0f, 20f, 0f), Quaternion.Euler(38f, 55f, 0f), 500f, false, true);
            view.Repaint();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/') ??
                throw new ArgumentException("Asset folder requires a parent.", nameof(path));
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private sealed class ImportProgress : IProgressDisplay
        {
            private readonly string package;
            public ImportProgress(string package) => this.package = package;

            public void SetProgress(string progressName, float percentage, string message)
            {
                // SDK progress may arrive from worker threads; do not call Unity APIs here.
                Status = $"{package}: {progressName} {percentage:0}% {message}";
            }
        }
    }
}
