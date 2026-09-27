#nullable enable
using UnityEngine;

namespace CityFlow.Presentation.Audio
{
    public static class GameplaySoundMix
    {
        public static (float Volume, float Pan) At(Camera camera, Vector3 position)
        {
            // Provisional mix: orthographic height is a framing choice, not listener distance.
            if (camera.orthographic)
            {
                Vector3 viewport = camera.WorldToViewportPoint(position);
                if (viewport.z <= 0) return (0, 0);
                var offset = new Vector2(viewport.x - 0.5f, viewport.y - 0.5f) * 2;
                float radius = offset.magnitude;
                float edgeFade = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1.15f, 2.2f, radius));
                float zoomGain = Mathf.Sqrt(48 / Mathf.Max(48, camera.orthographicSize));
                return (zoomGain * edgeFade / (1 + 3 * radius * radius), Mathf.Clamp(offset.x, -1, 1) * 0.7f);
            }
            Vector3 delta = position - camera.transform.position;
            float distance = delta.magnitude;
            float fade = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(400, 600, distance));
            float volume = fade / (1 + Mathf.Max(0, distance - 12) / 65);
            return (volume, Vector3.Dot(camera.transform.right, delta.normalized) * 0.7f);
        }
    }
}
