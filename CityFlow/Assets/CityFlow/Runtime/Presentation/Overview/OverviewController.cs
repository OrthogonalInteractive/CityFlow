#nullable enable

using System;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Domain.Spatial;
using R3;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CityFlow.Presentation.Overview
{
    public sealed class OverviewController : MonoBehaviour
    {
        private readonly Subject<OverviewTarget> selectionChanged = new();
        private InputActionMap? actions;
        private InputAction? panInput, pointerInput, deltaInput, zoomInput, orbitInput, dragInput;
        private StageDefinition? stage;
        private FlowNetwork? network;
        private Camera? sceneCamera;
        private Vector3 pivot;
        private float yaw = -10, pitch = 60;
        private Vector2 lastPointer;
        public Observable<OverviewTarget> SelectionChanged => selectionChanged;
        public OverviewTarget Selected { get; private set; }
        public OverviewTarget Hovered { get; private set; }
        public bool EditingRoute { get; set; }
        public Func<Vector2, bool>? IsPointerBlocked { get; set; }
        public void Initialize(StageDefinition definition, FlowNetwork flowNetwork, Camera camera)
        {
            stage = definition; network = flowNetwork; sceneCamera = camera;
            actions?.Dispose();
            actions = new InputActionMap("Overview");
            panInput = actions.AddAction("Pan", InputActionType.Value);
            panInput.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            pointerInput = actions.AddAction("Pointer", InputActionType.Value, "<Mouse>/position");
            deltaInput = actions.AddAction("Delta", InputActionType.Value, "<Mouse>/delta");
            zoomInput = actions.AddAction("Zoom", InputActionType.Value, "<Mouse>/scroll/y");
            orbitInput = actions.AddAction("Orbit", InputActionType.Button, "<Mouse>/rightButton");
            dragInput = actions.AddAction("Drag", InputActionType.Button, "<Mouse>/middleButton");
            actions.AddAction("Select", InputActionType.Button, "<Mouse>/leftButton").performed += _ =>
            {
                Vector2 point = pointerInput.ReadValue<Vector2>();
                if (!EditingRoute && IsPointerBlocked?.Invoke(point) != true) Select(Pick(point));
            };
            actions.AddAction("Focus", InputActionType.Button, "<Keyboard>/f").performed += _ => FocusSelection();
            actions.AddAction("Home", InputActionType.Button, "<Keyboard>/home").performed += _ => ResetView();
            if (isActiveAndEnabled) actions.Enable();
            lastPointer = pointerInput.ReadValue<Vector2>();
            ResetView();
        }
        private void OnEnable() => actions?.Enable();
        private void OnDisable() => actions?.Disable();
        private void Update()
        {
            if (sceneCamera == null || actions == null || panInput == null || pointerInput == null ||
                deltaInput == null || zoomInput == null || orbitInput == null || dragInput == null) return;
            Vector2 point = pointerInput.ReadValue<Vector2>();
            Vector2 delta = deltaInput.ReadValue<Vector2>();
            bool blocked = IsPointerBlocked?.Invoke(point) == true;
            Vector2 pan = panInput.ReadValue<Vector2>();
            if (pan != Vector2.zero) Pan(pan * (sceneCamera.orthographicSize * Time.unscaledDeltaTime));
            float zoom = zoomInput.ReadValue<float>();
            if (!blocked && zoom != 0) Zoom(zoom / 120f);
            if (!EditingRoute && !blocked && orbitInput.IsPressed() && delta != Vector2.zero) Orbit(delta * 0.2f);
            if (!blocked && dragInput.IsPressed() && delta != Vector2.zero)
                Pan(-delta * (2 * sceneCamera.orthographicSize / Mathf.Max(1, Screen.height)));
            if (point != lastPointer || pan != Vector2.zero || zoom != 0 || delta != Vector2.zero)
            { Hovered = blocked ? default : Pick(point); lastPointer = point; }
        }
        public void Select(OverviewTarget target)
        {
            if (Selected.Equals(target)) return;
            Selected = target; selectionChanged.OnNext(target);
        }
        public void Hover(Vector2 screen) => Hovered = Pick(screen);
        public OverviewTarget Pick(Vector2 screen)
        {
            if (stage == null || network == null || sceneCamera == null || !sceneCamera.pixelRect.Contains(screen)) return default;
            float closest = 22f;
            OverviewTarget result = default;
            foreach (NodeDefinition node in stage.Nodes)
            {
                Vector3 point = sceneCamera.WorldToScreenPoint(node.Position + Vector3.up * 1.4f);
                float distance = Vector2.Distance(point, screen);
                if (point.z > 0 && distance < closest) { closest = distance; result = OverviewTarget.Node(node.Id); }
            }
            if (!result.IsEmpty) return result;
            closest = 9;
            foreach (LineSnapshot line in network.Snapshot().Lines)
                for (int i = 1; i < line.Route.Points.Count; i++)
                {
                    Vector3 a = sceneCamera.WorldToScreenPoint(line.Route.Points[i-1] + Vector3.up * 0.2f);
                    Vector3 b = sceneCamera.WorldToScreenPoint(line.Route.Points[i] + Vector3.up * 0.2f);
                    if (a.z <= 0 || b.z <= 0) continue;
                    Vector2 segment = (Vector2)(b-a);
                    float t = segment.sqrMagnitude == 0 ? 0 : Mathf.Clamp01(Vector2.Dot(screen-(Vector2)a, segment)/segment.sqrMagnitude);
                    float distance = Vector2.Distance(screen, (Vector2)a + segment*t);
                    if (distance < closest) { closest = distance; result = OverviewTarget.Line(line.Id); }
                }
            return result;
        }
        public void Pan(Vector2 delta)
        {
            if (stage == null || sceneCamera == null) return;
            Vector3 right = Quaternion.Euler(0,yaw,0) * Vector3.right;
            Vector3 forward = Quaternion.Euler(0,yaw,0) * Vector3.forward;
            pivot += right*delta.x + forward*delta.y;
            pivot.x = Mathf.Clamp(pivot.x, stage.WalkableArea.xMin, stage.WalkableArea.xMax);
            pivot.z = Mathf.Clamp(pivot.z, stage.WalkableArea.yMin, stage.WalkableArea.yMax);
            ApplyPose();
        }
        public void Zoom(float delta)
        {
            if (sceneCamera == null) return;
            sceneCamera.orthographicSize = Mathf.Clamp(sceneCamera.orthographicSize * Mathf.Exp(-delta * 0.15f), 8, 180);
        }
        public void Orbit(Vector2 delta)
        { yaw = (yaw + delta.x) % 360; pitch = Mathf.Clamp(pitch - delta.y, 25, 85); ApplyPose(); }
        public void FocusSelection()
        {
            if (EditingRoute) return;
            if (network == null || sceneCamera == null) return;
            foreach (NodeDefinition node in network.NodeDefinitions)
                if (node.Id == Selected.NodeId) { pivot = node.Position; sceneCamera.orthographicSize = 24; ApplyPose(); return; }
            foreach (LineSnapshot line in network.Snapshot().Lines)
                if (line.Id == Selected.LineId) { pivot = line.Route.PositionAt(line.Route.Length*0.5f); ApplyPose(); return; }
        }
        public void ResetView()
        {
            if (stage == null || sceneCamera == null) return;
            pivot = new Vector3(stage.WalkableArea.center.x, stage.GroundHeight, stage.WalkableArea.center.y);
            yaw = EditingRoute ? 0 : -10; pitch = EditingRoute ? 90 : 60;
            sceneCamera.orthographicSize = Mathf.Max(stage.WalkableArea.height * 0.7f, stage.WalkableArea.width / sceneCamera.aspect * 0.7f);
            ApplyPose();
        }
        private void ApplyPose()
        {
            if (sceneCamera == null) return;
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0);
            sceneCamera.transform.SetPositionAndRotation(pivot + rotation * Vector3.back * 220, rotation);
        }
        public void BeginRouteView()
        {
            EditingRoute = true;
            if (sceneCamera != null) sceneCamera.orthographic = true;
            ResetView();
        }
        public OverviewViewState CaptureView()
        {
            if (sceneCamera == null) throw new InvalidOperationException("Overview is not initialized.");
            return new OverviewViewState(pivot,yaw,pitch,sceneCamera);
        }
        public void RestoreView(OverviewViewState state)
        {
            if (sceneCamera == null) return;
            pivot = state.Pivot; yaw = state.Yaw; pitch = state.Pitch;
            sceneCamera.transform.SetPositionAndRotation(state.Position,state.Rotation);
            sceneCamera.orthographic = state.Orthographic; sceneCamera.orthographicSize = state.Size;
            sceneCamera.fieldOfView = state.FieldOfView; sceneCamera.nearClipPlane = state.NearClip;
            Hovered = default;
        }
        private void OnDestroy()
        { actions?.Dispose(); selectionChanged.OnCompleted(); selectionChanged.Dispose(); }
    }
}
