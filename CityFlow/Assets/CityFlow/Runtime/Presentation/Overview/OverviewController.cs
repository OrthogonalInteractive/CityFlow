#nullable enable

using System;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Domain.Spatial;
using CityFlow.Presentation.Rendering;
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
        private ValidationCityView? cityView;
        private Vector3 pivot;
        private float yaw = -10, pitch = 60;
        private float cameraDistance = 220;
        private OverviewViewState? homeView;
        private Vector2 lastPointer;
        // Presentation time keeps focus motion independent of the simulation's Pause state.
        private const float FocusDurationSeconds = 0.65f;
        private bool movingToFocus;
        private float focusElapsed;
        private Vector3 focusStartPosition, focusStartPivot, focusEndPosition, focusEndPivot;
        // Provisional presentation timing [UI seconds] and closest orthographic half-height [m].
        private const float ZoomDurationSeconds = 0.3f, ZoomGestureGapSeconds = 0.18f, ClosestZoomSize = 8;
        private bool zooming;
        private int lastZoomDirection;
        private float zoomQuietSeconds = ZoomGestureGapSeconds, zoomElapsed, zoomStartSize, zoomEndSize;
        private Vector3 zoomViewportAnchor = new(0.5f, 0.5f, 0);
        private readonly Subject<(OverviewTarget Target, bool Edit)> clicked = new();
        public Observable<(OverviewTarget Target, bool Edit)> Clicked => clicked;
        public Observable<OverviewTarget> SelectionChanged => selectionChanged;
        public OverviewTarget Selected { get; private set; }
        public OverviewTarget Focused { get; private set; }
        public OverviewTarget Hovered { get; private set; }
        public Vector2 HoverScreenPosition { get; private set; }
        public bool EditingRoute { get; set; }
        public Func<Vector2, bool>? IsPointerBlocked { get; set; }
        public void Initialize(StageDefinition definition, FlowNetwork flowNetwork, Camera camera, OverviewViewState? home = null,
            ValidationCityView? view = null)
        {
            stage = definition; network = flowNetwork; sceneCamera = camera;
            cityView = view;
            homeView = home;
            cameraDistance = home.HasValue ? Vector3.Distance(home.Value.Position, home.Value.Pivot) : 220;
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
            actions.AddAction("Select", InputActionType.Button, "<Mouse>/leftButton").performed += context =>
            {
                Vector2 point = context.control.device is Mouse mouse ? mouse.position.ReadValue() : pointerInput.ReadValue<Vector2>();
                if (!EditingRoute && IsPointerBlocked?.Invoke(point) != true)
                {
                    bool edit = Keyboard.current?.shiftKey.isPressed == true;
                    OverviewTarget target = Pick(point, edit); Select(target);
                    clicked.OnNext((target, edit));
                }
            };
            actions.AddAction("Focus", InputActionType.Button, "<Keyboard>/f").performed += _ =>
            {
                if (!Hovered.IsEmpty) Select(Hovered);
                FocusSelection();
            };
            actions.AddAction("Home", InputActionType.Button, "<Keyboard>/home").performed += _ => ResetView();
            if (isActiveAndEnabled) actions.Enable();
            lastPointer = pointerInput.ReadValue<Vector2>();
            if (homeView.HasValue) RestoreView(homeView.Value);
            else ResetView();
        }
        private void OnEnable() => actions?.Enable();
        private void OnDisable()
        {
            actions?.Disable();
            movingToFocus = false;
            CancelZoom();
            Focused = default;
        }
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
            if (!blocked && zoom != 0) Zoom(zoom, point);
            if ((!EditingRoute || stage?.AllowsHeight == true) && !blocked && orbitInput.IsPressed() && delta != Vector2.zero) Orbit(delta * 0.2f);
            if (!blocked && dragInput.IsPressed() && delta != Vector2.zero)
                Pan(-delta * (2 * sceneCamera.orthographicSize / Mathf.Max(1, Screen.height)));
            bool cameraMoved = movingToFocus || zooming;
            AdvanceFocus(Time.unscaledDeltaTime);
            AdvanceZoom(Time.unscaledDeltaTime);
            if (cameraMoved || point != lastPointer || pan != Vector2.zero || zoom != 0 || delta != Vector2.zero)
            { HoverScreenPosition = point; Hovered = blocked ? default : Pick(point); lastPointer = point; }
        }
        public void ClearSelection()
        {
            movingToFocus = false;
            CancelZoom();
            Focused = default;
            Hovered = default;
            Select(default);
        }

        public void Select(OverviewTarget target)
        {
            if (!Focused.Equals(target)) { Focused = default; movingToFocus = false; }
            if (Selected.Equals(target)) return;
            Selected = target; selectionChanged.OnNext(target);
        }
        public void Hover(Vector2 screen) { HoverScreenPosition = screen; Hovered = Pick(screen); }
        public bool FocusNodeSmooth(string id)
        {
            if (!isActiveAndEnabled || EditingRoute || network == null || sceneCamera == null) return false;
            foreach (NodeDefinition node in network.NodeDefinitions)
            {
                if (node.Id != id) continue;
                CancelZoom();
                Select(OverviewTarget.Node(id));
                Focused = Selected;
                Hovered = default;
                focusStartPosition = sceneCamera.transform.position;
                focusStartPivot = pivot;
                focusEndPivot = node.Position + Vector3.up * 1.4f;
                focusEndPosition = focusEndPivot - sceneCamera.transform.forward * cameraDistance;
                focusElapsed = 0;
                movingToFocus = true;
                return true;
            }
            return false;
        }

        public void AdvanceFocus(float deltaSeconds)
        {
            if (!movingToFocus) return;
            if (!isActiveAndEnabled || EditingRoute || sceneCamera == null) { movingToFocus = false; return; }
            if (deltaSeconds <= 0 || float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds)) return;
            focusElapsed = Mathf.Min(FocusDurationSeconds, focusElapsed + deltaSeconds);
            float blend = Mathf.SmoothStep(0, 1, focusElapsed / FocusDurationSeconds);
            pivot = Vector3.Lerp(focusStartPivot, focusEndPivot, blend);
            sceneCamera.transform.position = Vector3.Lerp(focusStartPosition, focusEndPosition, blend);
            movingToFocus = focusElapsed < FocusDurationSeconds;
        }
        public OverviewTarget Pick(Vector2 screen, bool preferLine = false)
        {
            if (stage == null || network == null || sceneCamera == null || !sceneCamera.pixelRect.Contains(screen)) return default;
            float closest = 22f;
            OverviewTarget result = default;
            float closestColumn = float.PositiveInfinity;
            OverviewTarget column = default;
            foreach (NodeDefinition node in network.NodeDefinitions)
            {
                Vector3 point = sceneCamera.WorldToScreenPoint(node.Position + Vector3.up * 1.4f);
                float distance = Vector2.Distance(point, screen);
                if (point.z > 0 && distance < closest) { closest = distance; result = OverviewTarget.Node(node.Id); }
                if (RelayHeightGeometry.TryPick(sceneCamera, screen, stage, node, out float depth) && depth < closestColumn)
                { closestColumn = depth; column = OverviewTarget.Node(node.Id); }
                if (cityView != null && cityView.TryPickNodeBeacon(sceneCamera, screen, node, out depth) && depth < closestColumn)
                { closestColumn = depth; column = OverviewTarget.Node(node.Id); }
            }
            if (!result.IsEmpty) return result;
            // Keep marker priority; Shift-click must still reach Lines inside a Relay column.
            if (preferLine)
            {
                result = PickLine(screen);
                return result.IsEmpty ? column : result;
            }
            return column.IsEmpty ? PickLine(screen) : column;
        }
        private OverviewTarget PickLine(Vector2 screen)
        {
            if (stage == null || network == null || sceneCamera == null) return default;
            float closest = 9;
            OverviewTarget result = default;
            foreach (LineSnapshot line in network.Snapshot().Lines)
                for (int i = 1; i < line.Route.Points.Count; i++)
                {
                    Vector3 lift = stage.AllowsHeight ? Vector3.zero : Vector3.up * 0.2f;
                    Vector3 a = sceneCamera.WorldToScreenPoint(line.Route.Points[i-1] + lift);
                    Vector3 b = sceneCamera.WorldToScreenPoint(line.Route.Points[i] + lift);
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
            movingToFocus = false;
            if (stage == null || sceneCamera == null) return;
            Vector3 right = Quaternion.Euler(0,yaw,0) * Vector3.right;
            Vector3 forward = Quaternion.Euler(0,yaw,0) * Vector3.forward;
            Vector3 movement = right*delta.x + forward*delta.y;
            // Cursor-centered zoom may move the pivot outside the pan bounds; allow a gradual return.
            pivot.x = Mathf.Clamp(pivot.x + movement.x, Mathf.Min(pivot.x, stage.WalkableArea.xMin), Mathf.Max(pivot.x, stage.WalkableArea.xMax));
            pivot.z = Mathf.Clamp(pivot.z + movement.z, Mathf.Min(pivot.z, stage.WalkableArea.yMin), Mathf.Max(pivot.z, stage.WalkableArea.yMax));
            ApplyPose();
        }
        public void Zoom(float delta, Vector2? screenPosition = null)
        {
            if (!isActiveAndEnabled || sceneCamera == null || !sceneCamera.orthographic ||
                delta == 0 || float.IsNaN(delta) || float.IsInfinity(delta)) return;
            if (screenPosition.HasValue && !sceneCamera.pixelRect.Contains(screenPosition.Value)) return;
            int direction = delta > 0 ? 1 : -1;
            bool continuingGesture = direction == lastZoomDirection && zoomQuietSeconds < ZoomGestureGapSeconds;
            lastZoomDirection = direction;
            zoomQuietSeconds = 0;
            if (continuingGesture) return;
            movingToFocus = false;
            float reference = zooming ? zoomEndSize : sceneCamera.orthographicSize;
            float tolerance = Mathf.Max(0.0001f, reference * 0.0001f);
            for (int i = 0; i < 5; i++)
            {
                float size = ZoomSize(direction > 0 ? 4 - i : i);
                if (direction > 0 ? size >= reference - tolerance : size <= reference + tolerance) continue;
                zoomStartSize = sceneCamera.orthographicSize;
                zoomEndSize = size;
                zoomViewportAnchor = screenPosition.HasValue ? sceneCamera.ScreenToViewportPoint(screenPosition.Value) : new Vector3(0.5f, 0.5f, 0);
                zoomElapsed = 0;
                zooming = true;
                return;
            }
        }
        private float ZoomSize(int level)
        {
            float home = homeView.HasValue && !EditingRoute ? homeView.Value.Size :
                stage != null && sceneCamera != null ? Mathf.Max(stage.WalkableArea.height * 0.7f,
                    stage.WalkableArea.width / sceneCamera.aspect * 0.7f) : 63;
            home = Mathf.Max(ClosestZoomSize * 2, home);
            if (level == 4) return Mathf.Max(home * 2, FullAreaSize());
            if (level == 3) return home;
            return ClosestZoomSize * Mathf.Pow(home / ClosestZoomSize, level / 3f);
        }
        private float FullAreaSize() => stage != null && sceneCamera != null
            ? Mathf.Max(stage.WalkableArea.height * 0.7f, stage.WalkableArea.width / sceneCamera.aspect * 0.7f) : 63;
        public void AdvanceZoom(float deltaSeconds)
        {
            if (!isActiveAndEnabled || sceneCamera == null || deltaSeconds <= 0 ||
                float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds)) return;
            zoomQuietSeconds = Mathf.Min(ZoomGestureGapSeconds, zoomQuietSeconds + deltaSeconds);
            if (!zooming) return;
            float previousSize = sceneCamera.orthographicSize;
            zoomElapsed = Mathf.Min(ZoomDurationSeconds, zoomElapsed + deltaSeconds);
            float blend = Mathf.SmoothStep(0, 1, zoomElapsed / ZoomDurationSeconds);
            sceneCamera.orthographicSize = zoomElapsed >= ZoomDurationSeconds ? zoomEndSize :
                Mathf.Exp(Mathf.Lerp(Mathf.Log(zoomStartSize), Mathf.Log(zoomEndSize), blend));
            Vector3 shift = 2 * (previousSize - sceneCamera.orthographicSize) *
                (sceneCamera.transform.right * ((zoomViewportAnchor.x - 0.5f) * sceneCamera.aspect) +
                 sceneCamera.transform.up * (zoomViewportAnchor.y - 0.5f));
            Vector3 forward = sceneCamera.transform.forward;
            // Parallel orthographic rays keep every depth under the cursor fixed without a physics hit.
            // Move along the horizontal plane to preserve camera height and the orbit pivot's altitude.
            if (Mathf.Abs(forward.y) > 0.0001f)
            {
                shift -= forward * (shift.y / forward.y);
                shift.y = 0;
            }
            sceneCamera.transform.position += shift;
            pivot += shift;
            zooming = zoomElapsed < ZoomDurationSeconds;
        }
        private void CancelZoom()
        {
            zooming = false;
            lastZoomDirection = 0;
            zoomQuietSeconds = ZoomGestureGapSeconds;
        }
        public void Orbit(Vector2 delta)
        { movingToFocus = false; yaw = (yaw + delta.x) % 360; pitch = Mathf.Clamp(pitch - delta.y, 25, 85); ApplyPose(); }
        public void FocusSelection()
        {
            movingToFocus = false;
            CancelZoom();
            if (EditingRoute) return;
            if (network == null || sceneCamera == null) return;
            foreach (NodeDefinition node in network.NodeDefinitions)
                if (node.Id == Selected.NodeId)
                {
                    Focused = Selected;
                    pivot = node.Position; sceneCamera.orthographicSize = 24; ApplyPose(); return;
                }
            foreach (LineSnapshot line in network.Snapshot().Lines)
                if (line.Id == Selected.LineId) { pivot = line.Route.PositionAt(line.Route.Length*0.5f); ApplyPose(); return; }
        }
        public void ResetView()
        {
            movingToFocus = false;
            CancelZoom();
            Focused = default;
            Hovered = default;
            if (stage == null || sceneCamera == null) return;
            pivot = new Vector3(stage.WalkableArea.center.x,
                stage.GroundHeight + (homeView.HasValue && !EditingRoute ? stage.MaximumAltitude * 0.5f : 0), stage.WalkableArea.center.y);
            yaw = EditingRoute ? 0 : -10; pitch = EditingRoute && !stage.AllowsHeight ? 90 : 60;
            sceneCamera.orthographicSize = FullAreaSize();
            ApplyPose();
        }
        private void ApplyPose()
        {
            if (sceneCamera == null) return;
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0);
            sceneCamera.transform.SetPositionAndRotation(pivot + rotation * Vector3.back * cameraDistance, rotation);
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
            movingToFocus = false;
            CancelZoom();
            if (sceneCamera == null) return;
            pivot = state.Pivot; yaw = state.Yaw; pitch = state.Pitch;
            sceneCamera.transform.SetPositionAndRotation(state.Position,state.Rotation);
            sceneCamera.rect=state.Viewport;
            sceneCamera.orthographic = state.Orthographic; sceneCamera.orthographicSize = state.Size;
            sceneCamera.fieldOfView = state.FieldOfView; sceneCamera.nearClipPlane = state.NearClip;
            Hovered = default;
        }
        private void OnDestroy()
        { actions?.Dispose(); selectionChanged.OnCompleted(); selectionChanged.Dispose(); clicked.OnCompleted(); clicked.Dispose(); }
    }
}
