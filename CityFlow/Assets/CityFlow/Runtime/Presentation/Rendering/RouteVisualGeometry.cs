#nullable enable

using UnityEngine;

namespace CityFlow.Presentation.Rendering
{
    public static class RouteVisualGeometry
    {
        public static Vector3 Side(Vector3 direction)
        {
            Vector3 axis = Mathf.Abs(Vector3.Dot(direction.normalized, Vector3.up)) > 0.99f
                ? Vector3.forward : Vector3.up;
            return Vector3.Cross(axis, direction).normalized;
        }
    }
}
