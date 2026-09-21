#nullable enable

using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CityFlow.Tests.EditMode
{
    public sealed class ProjectConfigurationTests
    {
        [Test]
        public void DefaultRenderPipelineUsesUrp()
        {
            Assert.That(GraphicsSettings.defaultRenderPipeline, Is.TypeOf<UniversalRenderPipelineAsset>());
        }

        [Test]
        public void EveryQualityLevelUsesUrp()
        {
            for (int index = 0; index < QualitySettings.names.Length; index++)
            {
                RenderPipelineAsset pipeline = QualitySettings.GetRenderPipelineAssetAt(index);
                Assert.That(pipeline, Is.TypeOf<UniversalRenderPipelineAsset>(), QualitySettings.names[index]);
            }
        }

        [Test]
        public void BootstrapIsTheFirstEnabledBuildScene()
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).ToArray();
            Assert.That(scenes, Is.Not.Empty);
            Assert.That(scenes[0].path, Is.EqualTo("Assets/CityFlow/Scenes/Bootstrap.unity"));
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(scenes[0].path), Is.Not.Null);
        }

        [Test]
        public void OnlyTheNewInputSystemIsEnabled()
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            Assert.Pass();
#else
            Assert.Fail("Active Input Handling must be set to Input System Package (New).");
#endif
        }
    }
}
