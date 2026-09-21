#nullable enable

using System;
using CityFlow.Composition;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityFlow.Editor
{
    public static class ValidationHudSetup
    {
        public static void Configure(CityFlowLifetimeScope scope)
        {
            const string ui = "Assets/CityFlow/Runtime/Presentation/UI/";
            const string settings = "Assets/CityFlow/Settings/UI";
            var layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(ui + "ValidationHud.uxml");
            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ui + "ValidationTheme.tss");
            if (layout == null || theme == null) throw new InvalidOperationException("HUD source assets must be imported first.");
            if (!AssetDatabase.IsValidFolder(settings)) AssetDatabase.CreateFolder("Assets/CityFlow/Settings", "UI");
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(settings + "/ValidationPanelSettings.asset");
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                panel.themeStyleSheet = theme;
                panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                panel.referenceResolution = new Vector2Int(1600, 900);
                panel.screenMatchMode = PanelScreenMatchMode.Expand;
                AssetDatabase.CreateAsset(panel, settings + "/ValidationPanelSettings.asset");
            }
            scope.SetHudConfiguration(layout, panel);
        }
    }
}
