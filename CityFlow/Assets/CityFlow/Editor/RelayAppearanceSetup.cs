#nullable enable

using System;
using CityFlow.Composition;
using UnityEditor;
using UnityEngine;

namespace CityFlow.Editor
{
    public static class RelayAppearanceSetup
    {
        public static void Configure(CityFlowLifetimeScope scope)
        {
            const string path = "Assets/CityFlow/Art/Materials/RelayHologram.mat";
            var surface = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (surface == null)
            {
                var shader = Shader.Find("CityFlow/Relay Hologram");
                if (shader == null) throw new InvalidOperationException("Import the Relay hologram shader before configuring the scene.");
                surface = new Material(shader) { name = "RelayHologram" };
                AssetDatabase.CreateAsset(surface, path);
            }
            scope.SetRelayAppearance(surface);
        }
    }
}
