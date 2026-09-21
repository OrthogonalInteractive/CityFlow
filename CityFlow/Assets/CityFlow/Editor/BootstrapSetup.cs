#nullable enable

using System;
using System.Linq;
using CityFlow.Composition;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CityFlow.Editor
{
    public static class BootstrapSetup
    {
        // This explicit batch entry point never runs automatically during import.
        public static void EnsureCompositionRoot()
        {
            const string scenePath = "Assets/CityFlow/Scenes/Bootstrap.unity";
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
            {
                throw new InvalidOperationException($"The bootstrap scene is missing: {scenePath}");
            }

            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            bool hasScope = scene.GetRootGameObjects()
                .Any(root => root.GetComponentInChildren<CityFlowLifetimeScope>(true) != null);
            if (!hasScope)
            {
                var root = new GameObject("City Flow Lifetime Scope");
                root.AddComponent<CityFlowLifetimeScope>();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            AssetDatabase.SaveAssets();
        }
    }
}
