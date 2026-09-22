#nullable enable

using System;
using CityFlow.Composition;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CityFlow.Editor
{
    public static class ObstacleAppearanceSetup
    {
        public static void Configure(CityFlowLifetimeScope scope)
        {
            const string surfacePath = "Assets/CityFlow/Art/Materials/AmberObstacle.mat";
            const string glowPath = "Assets/CityFlow/Settings/Rendering/ObstacleGlow.asset";
            var surface = AssetDatabase.LoadAssetAtPath<Material>(surfacePath);
            if (surface == null)
            {
                var shader = Shader.Find("CityFlow/Amber Obstacle");
                if (shader == null) throw new InvalidOperationException("Import the amber obstacle shader before configuring the scene.");
                surface = new Material(shader) { name = "AmberObstacle" };
                AssetDatabase.CreateAsset(surface, surfacePath);
            }
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(glowPath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = "ObstacleGlow";
                AssetDatabase.CreateAsset(profile, glowPath);
                var bloom = profile.Add<Bloom>(true);
                bloom.threshold.value = 1;
                bloom.intensity.value = 0.3f;
                bloom.scatter.value = 0.55f;
                bloom.clamp.value = 12;
                AssetDatabase.AddObjectToAsset(bloom, profile);
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssetIfDirty(profile);
            }
            scope.SetObstacleAppearance(surface, profile);
        }
    }
}
