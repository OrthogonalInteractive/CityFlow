#nullable enable

using UnityEngine;

namespace CityFlow.Presentation.Overview
{
    public readonly struct OverviewViewState
    {
        public readonly Vector3 Pivot, Position;
        public readonly Quaternion Rotation;
        public readonly float Yaw, Pitch, Size, FieldOfView, NearClip;
        public readonly bool Orthographic;
        public OverviewViewState(Vector3 pivot, float yaw, float pitch, Camera camera)
        {
            Pivot = pivot; Yaw = yaw; Pitch = pitch; Position = camera.transform.position; Rotation = camera.transform.rotation;
            Size = camera.orthographicSize; FieldOfView = camera.fieldOfView; NearClip = camera.nearClipPlane;
            Orthographic = camera.orthographic;
        }
    }
}
