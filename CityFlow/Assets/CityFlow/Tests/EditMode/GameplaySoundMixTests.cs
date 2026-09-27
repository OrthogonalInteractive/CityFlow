#nullable enable
using CityFlow.Presentation.Audio;
using NUnit.Framework;
using UnityEngine;

namespace CityFlow.Tests.EditMode
{
    public sealed class GameplaySoundMixTests
    {
        [Test] public void OverviewFavorsViewCenterAndCloseZoomAtAnyHeight()
        {
            var go = new GameObject("Audio mix camera");
            try
            {
                var camera = go.AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 40;
                camera.transform.SetPositionAndRotation(new Vector3(0, 220, -120), Quaternion.Euler(60, 0, 0));
                Vector3 center = camera.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, 200));
                Vector3 edge = camera.ViewportToWorldPoint(new Vector3(0.95f, 0.5f, 200));
                Vector3 left = camera.ViewportToWorldPoint(new Vector3(0.05f, 0.5f, 200));
                var near = GameplaySoundMix.At(camera, center);
                Assert.That(near.Volume, Is.GreaterThan(GameplaySoundMix.At(camera, edge).Volume));
                Assert.That(GameplaySoundMix.At(camera, left).Pan, Is.LessThan(0));
                Assert.That(GameplaySoundMix.At(camera, edge).Pan, Is.GreaterThan(0));
                Assert.That(GameplaySoundMix.At(camera, center + camera.transform.forward * 80).Volume, Is.EqualTo(near.Volume).Within(0.001f));
                Assert.That(GameplaySoundMix.At(camera, camera.ViewportToWorldPoint(new Vector3(2, 0.5f, 200))).Volume, Is.Zero);
                camera.orthographicSize = 220;
                Assert.That(GameplaySoundMix.At(camera, center).Volume, Is.LessThan(near.Volume));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test] public void Node360UsesWorldDistanceAndCameraRelativeStereo()
        {
            var go = new GameObject("Audio mix camera");
            try
            {
                var camera = go.AddComponent<Camera>(); camera.orthographic = false;
                var near = GameplaySoundMix.At(camera, new Vector3(0, 0, 5));
                Assert.That(near.Volume, Is.GreaterThan(GameplaySoundMix.At(camera, new Vector3(0, 0, 200)).Volume));
                Assert.That(GameplaySoundMix.At(camera, new Vector3(0, 0, 800)).Volume, Is.Zero);
                Assert.That(GameplaySoundMix.At(camera, new Vector3(10, 0, 10)).Pan, Is.GreaterThan(0));
                camera.transform.rotation = Quaternion.Euler(0, 180, 0);
                Assert.That(GameplaySoundMix.At(camera, new Vector3(10, 0, 10)).Pan, Is.LessThan(0));
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
