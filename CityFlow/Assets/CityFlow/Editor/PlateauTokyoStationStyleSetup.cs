#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace CityFlow.Editor
{
    public static class PlateauTokyoStationStyleSetup
    {
        public const string ScenePath = "Assets/CityFlow/Scenes/TokyoStationWiringLab.unity";
        public const string AssetRoot = "Assets/CityFlow/Art/PLATEAU/TokyoStationWiringLab";
        private const string MaterialRoot = "Assets/CityFlow/Art/Materials/TokyoStationWiringLab";

        [MenuItem("City Flow/Create Tokyo Station WiringLab Look")]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || File.Exists(ScenePath))
                throw new InvalidOperationException("Requires Edit Mode and a new output scene.");
            var scene = SceneManager.GetActiveScene();
            if (scene.path != PlateauTokyoStationSetup.ScenePath)
            {
                for (int i = 0; i < SceneManager.sceneCount; i++)
                    if (SceneManager.GetSceneAt(i).isDirty)
                        throw new InvalidOperationException("Save open scenes before creating the styled scene.");
                scene = EditorSceneManager.OpenScene(PlateauTokyoStationSetup.ScenePath);
            }
            // Save to a new path so pending source edits are preserved in the copy.
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Could not save the scene copy.");
            ApplyToCopy();
        }

        public static void ApplyToCopy()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != ScenePath)
                throw new InvalidOperationException("Open the TokyoStationWiringLab scene outside Play Mode.");
            if (AssetDatabase.IsValidFolder(AssetRoot) || AssetDatabase.IsValidFolder(MaterialRoot))
                throw new InvalidOperationException("Style assets already exist; existing work will not be overwritten.");
            var amber = AssetDatabase.LoadAssetAtPath<Material>("Assets/CityFlow/Art/Materials/AmberObstacle.mat");
            var glow = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/CityFlow/Settings/Rendering/ObstacleGlow.asset");
            var surfaceShader = Shader.Find("CityFlow/City Inspection Surface");
            var contourShader = Shader.Find("CityFlow/City Inspection Contour");
            if (amber == null || glow == null || surfaceShader == null || contourShader == null)
                throw new InvalidOperationException("Import the WiringLab appearance and city inspection shaders first.");

            var roots = scene.GetRootGameObjects();
            string[] packages = { "Building", "Bridge", "Road", "Relief", "Vegetation" };
            foreach (string package in packages)
                if (!roots.Any(root => root.name == "TokyoStation_" + package))
                    throw new InvalidOperationException("Missing city model: " + package);
            EnsureFolder(AssetRoot);
            EnsureFolder(MaterialRoot);
            Material buildings = Surface("AmberCity", amber.GetColor("_BaseColor"), amber.GetColor("_GridColor"), false);
            Material ground = Surface("NavyGround", new Color(0.075f, 0.11f, 0.15f), new Color(0.11f, 0.17f, 0.22f), true);
            Material roads = Surface("BlueRoads", new Color(0.12f, 0.18f, 0.23f), new Color(0.055f, 0.085f, 0.105f), true);
            Material vegetation = Surface("MutedVegetation", new Color(0.04f, 0.1f, 0.085f), Color.black, false);
            var contour = new Material(contourShader) { name = "AmberCityContours" };
            contour.SetColor("_BaseColor", amber.GetColor("_EdgeColor"));
            AssetDatabase.CreateAsset(contour, MaterialRoot + "/AmberCityContours.mat");

            var decoration = new GameObject("WiringLab Appearance");
            int rendererCount = 0;
            int lineCount = 0;
            foreach (string package in packages)
            {
                var root = roots.Single(item => item.name == "TokyoStation_" + package);
                var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
                Material surface = package switch
                {
                    "Road" => roads,
                    "Relief" => ground,
                    "Vegetation" => vegetation,
                    _ => buildings
                };
                foreach (var renderer in renderers)
                {
                    renderer.sharedMaterials = Enumerable.Repeat(surface, renderer.sharedMaterials.Length).ToArray();
                    rendererCount++;
                }
                if (package == "Building" || package == "Bridge")
                    lineCount += CreateContours(renderers, package, decoration.transform, contour);
            }
            var volume = decoration.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10;
            volume.sharedProfile = glow;
            ConfigureView(roots);
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save styled scene.");
            FocusStation();
            Debug.Log($"WiringLab look saved: {rendererCount} city renderers, {lineCount} contour segments. Source meshes are shared.");
        }

        public static void FocusStation()
        {
            var view = SceneView.lastActiveSceneView;
            if (view == null) return;
            view.sceneLighting = true;
            view.sceneViewState.showImageEffects = true;
            view.LookAt(new Vector3(80, 30, 160), Quaternion.Euler(55, -25, 0), 650, true, true);
            view.Repaint();
        }

        private static Material Surface(string name, Color tint, Color grid, bool ground)
        {
            var shader = Shader.Find("CityFlow/City Inspection Surface");
            if (shader == null) throw new InvalidOperationException("City inspection surface shader is missing.");
            var material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", tint);
            material.SetColor("_GridColor", grid);
            material.SetVector("_PanelSize", ground ? new Vector4(10, 10, 0, 0) : new Vector4(3.2f, 4, 0, 0));
            material.SetFloat("_GridWidth", 0.04f);
            material.SetFloat("_Ground", ground ? 1 : 0);
            AssetDatabase.CreateAsset(material, MaterialRoot + "/" + name + ".mat");
            return material;
        }

        private static void ConfigureView(GameObject[] roots)
        {
            var camera = roots.SelectMany(root => root.GetComponentsInChildren<Camera>()).Single();
            var rotation = Quaternion.Euler(55, -25, 0);
            camera.transform.SetPositionAndRotation(new Vector3(80, 30, 160) + rotation * Vector3.back * 2500, rotation);
            camera.orthographic = true;
            camera.orthographicSize = 700;
            camera.nearClipPlane = 0.5f;
            camera.farClipPlane = 6000;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.052f, 0.082f);
            camera.allowHDR = true;
            var additional = camera.GetUniversalAdditionalCameraData();
            additional.renderPostProcessing = true;
            additional.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            var sunlight = roots.SelectMany(root => root.GetComponentsInChildren<Light>())
                .First(light => light.type == LightType.Directional);
            sunlight.transform.rotation = Quaternion.Euler(50, -30, 0);
            sunlight.intensity = 1;
            sunlight.color = Color.white;
            RenderSettings.sun = sunlight;
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.2f, 0.24f, 0.3f);
            RenderSettings.fog = false;
        }

        private static int CreateContours(MeshRenderer[] renderers, string package, Transform parent, Material material)
        {
            var points = new List<Vector3>();
            foreach (var renderer in renderers)
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                AppendFeatureEdges(filter.sharedMesh, renderer.transform.localToWorldMatrix, points);
            }
            var mesh = new Mesh { name = package + "Contours", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(points);
            mesh.SetIndices(Enumerable.Range(0, points.Count).ToArray(), MeshTopology.Lines, 0);
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, AssetRoot + "/" + package + "Contours.asset");
            var contours = new GameObject(package + " contours", typeof(MeshFilter), typeof(MeshRenderer));
            contours.transform.SetParent(parent, false);
            contours.GetComponent<MeshFilter>().sharedMesh = mesh;
            var outline = contours.GetComponent<MeshRenderer>();
            outline.sharedMaterial = material;
            outline.shadowCastingMode = ShadowCastingMode.Off;
            outline.receiveShadows = false;
            return points.Count / 2;
        }

        private static void AppendFeatureEdges(Mesh mesh, Matrix4x4 matrix, List<Vector3> output)
        {
            var vertices = mesh.vertices;
            var triangles = mesh.triangles;
            var welded = new Dictionary<Vector3Int, int>();
            var remap = new int[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = matrix.MultiplyPoint3x4(vertices[i]);
                // Weld texture seams to a centimetre; coplanar triangle diagonals are not outlines.
                var key = Vector3Int.RoundToInt(vertices[i] * 100);
                if (!welded.TryGetValue(key, out int index))
                {
                    index = welded.Count;
                    welded.Add(key, index);
                }
                remap[i] = index;
            }
            var edges = new Dictionary<(int, int), Edge>();
            float creaseCosine = Mathf.Cos(35 * Mathf.Deg2Rad);
            for (int i = 0; i + 2 < triangles.Length; i += 3)
            {
                int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                Vector3 normal = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
                if (normal.sqrMagnitude < 0.000001f) continue;
                normal.Normalize();
                AddEdge(a, b, normal);
                AddEdge(b, c, normal);
                AddEdge(c, a, normal);
            }
            foreach (var edge in edges.Values)
                if ((edge.Count == 1 || edge.Crease) && (edge.Start - edge.End).sqrMagnitude >= 0.5625f)
                {
                    output.Add(edge.Start);
                    output.Add(edge.End);
                }

            void AddEdge(int a, int b, Vector3 normal)
            {
                int x = remap[a], y = remap[b];
                if (x == y) return;
                var key = x < y ? (x, y) : (y, x);
                if (edges.TryGetValue(key, out var edge))
                {
                    edge.Crease |= Vector3.Dot(edge.Normal, normal) < creaseCosine;
                    edge.Count++;
                    edges[key] = edge;
                }
                else edges.Add(key, new Edge { Start = vertices[a], End = vertices[b], Normal = normal, Count = 1 });
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/') ??
                throw new ArgumentException("Asset folder requires a parent.", nameof(path));
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private struct Edge
        {
            public Vector3 Start;
            public Vector3 End;
            public Vector3 Normal;
            public int Count;
            public bool Crease;
        }
    }
}
