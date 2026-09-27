#nullable enable
using System;
using CityFlow.Composition;
using CityFlow.Presentation.Audio;
using UnityEditor;

namespace CityFlow.Editor
{
    public static class GameplayAudioSetup
    {
        public const string SettingsPath = "Assets/CityFlow/Settings/Audio/GameplayAudio.asset";
        public static void Configure(CityFlowLifetimeScope scope)
        {
            var settings = AssetDatabase.LoadAssetAtPath<GameplayAudioSettings>(SettingsPath);
            if (settings == null) throw new InvalidOperationException("Gameplay audio assets must be imported first.");
            scope.SetAudioConfiguration(settings);
        }
    }
}
