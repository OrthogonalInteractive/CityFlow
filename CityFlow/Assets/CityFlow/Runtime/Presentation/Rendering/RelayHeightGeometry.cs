#nullable enable

using CityFlow.Domain.Spatial;
using UnityEngine;

namespace CityFlow.Presentation.Rendering
{
    public static class RelayHeightGeometry
    {
        public const float Diameter = 4.8f;

        public static bool TryPick(Camera camera, Vector2 screen, StageDefinition stage, NodeDefinition node, out float distance)
        {
            distance = 0;
            float height = stage.ConnectionCeiling(node) - node.Position.y;
            if (node is not RelayNodeDefinition || height <= 0) return false;
            return TryPickCylinder(camera, screen, node.Position, height, Diameter, out distance);
        }

        public static bool TryPickCylinder(Camera camera, Vector2 screen, Vector3 bottomCenter, float height, float diameter, out float distance)
        {
            distance = 0;
            if (height <= 0 || diameter <= 0) return false;
            Ray ray = camera.ScreenPointToRay(screen);
            Vector3 origin = ray.origin - bottomCenter;
            Vector3 direction = ray.direction;
            float exit = float.PositiveInfinity;

            // Intersect the radial and vertical intervals of the finite cylinder, including its caps.
            float a = direction.x * direction.x + direction.z * direction.z;
            float b = origin.x * direction.x + origin.z * direction.z;
            float c = origin.x * origin.x + origin.z * origin.z - diameter * diameter * 0.25f;
            if (a < 0.00000001f)
            {
                if (c > 0) return false;
            }
            else
            {
                float discriminant = b * b - a * c;
                if (discriminant < 0) return false;
                float root = Mathf.Sqrt(discriminant);
                distance = Mathf.Max(0, (-b - root) / a);
                exit = (-b + root) / a;
            }
            if (Mathf.Abs(direction.y) < 0.00000001f)
            {
                if (origin.y < 0 || origin.y > height) return false;
            }
            else
            {
                float bottom = -origin.y / direction.y;
                float top = (height - origin.y) / direction.y;
                distance = Mathf.Max(distance, Mathf.Min(bottom, top));
                exit = Mathf.Min(exit, Mathf.Max(bottom, top));
            }
            // ScreenPointToRay starts on the near plane, so an inside origin is a valid visible hit.
            return distance <= exit && camera.WorldToViewportPoint(ray.GetPoint(distance)).z <= camera.farClipPlane;
        }
    }
}
