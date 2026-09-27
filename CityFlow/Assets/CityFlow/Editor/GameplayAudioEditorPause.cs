#nullable enable
using CityFlow.Presentation.Audio;
using UnityEditor;
using UnityEngine;

namespace CityFlow.Editor
{
    [InitializeOnLoad]
    public static class GameplayAudioEditorPause
    {
        static GameplayAudioEditorPause() => EditorApplication.pauseStateChanged += OnPause;
        private static void OnPause(PauseState state)
        {
            if (!EditorApplication.isPlaying) return;
            foreach (var audio in Object.FindObjectsByType<GameplayAudioView>())
                audio.SetSuspended(state == PauseState.Paused);
        }
    }
}
