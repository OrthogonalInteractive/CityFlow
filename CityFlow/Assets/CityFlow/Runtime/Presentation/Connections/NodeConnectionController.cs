#nullable enable

using System;
using System.Linq;
using CityFlow.Application.Connections;
using CityFlow.Domain.Spatial;
using CityFlow.Presentation.Overview;
using CityFlow.Presentation.Rendering;
using R3;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CityFlow.Presentation.Connections
{
    public sealed class NodeConnectionController : MonoBehaviour
    {
        private ConnectionSession? session;
        private OverviewController? overview;
        private StageDefinition? stage;
        private Camera? sceneCamera;
        private ValidationCityView? cityView;
        private IDisposable? subscription, clickSubscription;
        private int enteredFrame;
        public int EditingStartedFrame { get; private set; } = -1;
        private InputActionMap? actions;
        private InputAction? lookInput, dragInput, deltaInput, pointerInput;
        private OverviewViewState? bookmark;
        private OverviewTarget previousSelection;
        private bool overviewWasEnabled;
        private float yaw, pitch;
        public bool IsNode360 { get; private set; }
        public bool IsEditing { get; private set; }
        public string? AttentionId { get; private set; }
        public void Initialize(ConnectionSession connection, OverviewController input, StageDefinition definition, Camera camera, ValidationCityView view)
        {
            session = connection; overview = input; stage = definition; sceneCamera = camera;
            cityView = view;
            actions = new InputActionMap("Connection");
            actions.AddAction("Begin",InputActionType.Button,"<Keyboard>/c").performed += _ => BeginSelected();
            actions.AddAction("Confirm",InputActionType.Button,"<Keyboard>/enter").performed += _ => { if (session.IsActive) session.Confirm(); };
            actions.AddAction("Cancel",InputActionType.Button,"<Keyboard>/backspace").performed += _ => { if (session.IsActive) session.Cancel(); };
            actions.AddAction("Review",InputActionType.Button,"<Keyboard>/v").performed += _ => ToggleOverview();
            actions.AddAction("Next",InputActionType.Button,"<Keyboard>/tab").performed += _ => FocusNext();
            actions.AddAction("Focus",InputActionType.Button,"<Keyboard>/f").performed += _ =>
            { if (IsNode360 && AttentionId != null) FocusTarget(AttentionId); };
            actions.AddAction("Choose",InputActionType.Button,"<Keyboard>/space").performed += _ =>
            { if (IsNode360 && AttentionId != null) session.SelectTarget(AttentionId); };
            actions.AddAction("ClickTarget",InputActionType.Button,"<Mouse>/leftButton").performed += context =>
            {
                if (!IsNode360 || Time.frameCount == enteredFrame || pointerInput == null) return;
                Vector2 point=context.control.device is Mouse mouse ? mouse.position.ReadValue() : pointerInput.ReadValue<Vector2>();
                string? id=PickTarget(point);
                if (id != null) ConfirmTarget(id);
            };
            lookInput = actions.AddAction("Look",InputActionType.Value);
            lookInput.AddCompositeBinding("2DVector").With("Up","<Keyboard>/upArrow").With("Down","<Keyboard>/downArrow")
                .With("Left","<Keyboard>/leftArrow").With("Right","<Keyboard>/rightArrow");
            dragInput = actions.AddAction("Drag",InputActionType.Button,"<Mouse>/rightButton");
            deltaInput = actions.AddAction("Delta",InputActionType.Value,"<Mouse>/delta");
            pointerInput = actions.AddAction("Pointer",InputActionType.Value,"<Mouse>/position");
            if (isActiveAndEnabled) Bind();
        }
        private void OnEnable() => Bind();
        private void Bind()
        {
            subscription?.Dispose(); subscription = session?.Changed.Subscribe(_ => Synchronize());
            clickSubscription?.Dispose();
            clickSubscription=overview?.Clicked.Subscribe(click =>
            {
                if (session?.IsActive == true) return;
                if (click.Target.NodeId != null) BeginSelected();
                else if (click.Edit && click.Target.LineId.HasValue) EditSelectedLine();
            });
            actions?.Enable(); Synchronize();
        }
        private void Synchronize()
        {
            if (session == null || overview == null || sceneCamera == null || stage == null) return;
            if (session.IsActive && !bookmark.HasValue)
            {
                bookmark = overview.CaptureView(); overviewWasEnabled = overview.enabled;
                previousSelection = overview.Selected;
                AttentionId = null; enteredFrame=Time.frameCount;
                yaw = 0; pitch = 5; IsNode360 = true;
                overview.enabled = false; ApplyNodePose();
                Vector3 center = new Vector3(stage.WalkableArea.center.x,stage.GroundHeight,stage.WalkableArea.center.y);
                Face(center);
            }
            else if (!session.IsActive && bookmark.HasValue) Restore();
        }
        private void Restore()
        {
            if (overview != null && bookmark.HasValue)
            { overview.RestoreView(bookmark.Value); overview.Select(previousSelection); overview.enabled = overviewWasEnabled; }
            if (overview != null) overview.EditingRoute = false;
            IsEditing = false; bookmark = null; IsNode360 = false; AttentionId = null;
            if (cityView != null) cityView.SetHiddenNode(null);
        }
        private void Update()
        {
            if (!IsNode360 || lookInput == null || deltaInput == null || dragInput == null || pointerInput == null) return;
            Look(lookInput.ReadValue<Vector2>() * (70 * Time.unscaledDeltaTime));
            if (dragInput.IsPressed() && overview != null && overview.IsPointerBlocked?.Invoke(pointerInput.ReadValue<Vector2>()) != true)
                Look(deltaInput.ReadValue<Vector2>() * 0.15f);
            if (Time.frameCount != enteredFrame && deltaInput.ReadValue<Vector2>() != Vector2.zero)
            {
                string? id=PickTarget(pointerInput.ReadValue<Vector2>());
                if(id!=null) SetAttention(id);
            }
        }
        public void BeginSelected()
        { if (overview != null && overview.Selected.NodeId != null) session?.Begin(overview.Selected.NodeId); }
        public void Look(Vector2 delta)
        {
            if (!IsNode360) return;
            yaw = Mathf.Repeat(yaw + delta.x,360); pitch = Mathf.Clamp(pitch - delta.y,-80,80); ApplyNodePose();
        }
        public void ToggleOverview()
        {
            if (IsEditing || session?.IsActive != true || overview == null || !bookmark.HasValue) return;
            IsNode360 = !IsNode360;
            if (IsNode360) { overview.enabled = false; ApplyNodePose(); }
            else
            {
                overview.RestoreView(bookmark.Value); overview.enabled = true;
                if (cityView != null) cityView.SetHiddenNode(null);
            }
        }
        public void EditSelectedLine()
        {
            if (overview?.Selected.LineId is int id && session?.BeginLineEdit(id) == true) BeginEditing();
        }
        public void BeginEditing()
        {
            if (session?.IsActive != true || overview == null) return;
            if (IsNode360) ToggleOverview();
            IsEditing = true; EditingStartedFrame=Time.frameCount; overview.BeginRouteView();
        }
        private void ApplyNodePose()
        {
            if (sceneCamera == null || stage == null || session?.SourceId == null) return;
            Vector3 position = session.Nodes.Single(n => n.Id == session.SourceId).Position + Vector3.up * 3.2f;
            sceneCamera.rect=new Rect(0,0.06f,0.72f,0.94f);
            sceneCamera.orthographic = false; sceneCamera.fieldOfView = 70; sceneCamera.nearClipPlane = 0.1f;
            sceneCamera.transform.SetPositionAndRotation(position,Quaternion.Euler(pitch,yaw,0));
            if (cityView != null) cityView.SetHiddenNode(session.SourceId);
        }
        public void SetAttention(string id)
        {
            if (!IsNode360 || IsEditing || session == null || (AttentionId == id && session.TargetId == id)) return;
            AttentionId=id; session.SelectTarget(id);
        }
        public void ConfirmTarget(string id)
        {
            if (!IsNode360 || IsEditing || session == null) return;
            SetAttention(id); session.Confirm();
        }
        private string? PickTarget(Vector2 point)
        {
            if (sceneCamera == null || session == null || !sceneCamera.pixelRect.Contains(point) ||
                overview?.IsPointerBlocked?.Invoke(point) == true) return null;
            float closest=24; string? result=null;
            foreach(var candidate in session.Candidates())
            {
                if(candidate.Node.Definition.Id==session.SourceId) continue;
                Vector3 p=sceneCamera.WorldToScreenPoint(candidate.Node.Definition.Position+Vector3.up*1.4f);
                float distance=Vector2.Distance(point,p);
                if(p.z>0 && distance<closest) { closest=distance; result=candidate.Node.Definition.Id; }
            }
            return result;
        }
        public void FocusTarget(string id)
        {
            if (!IsNode360 || stage == null || session == null) return;
            NodeDefinition? node = session.Nodes.FirstOrDefault(n => n.Id == id);
            if (node == null) return;
            SetAttention(id);
            if (id != session?.SourceId) Face(node.Position + Vector3.up * 1.4f);
        }
        private void Face(Vector3 point)
        {
            if (sceneCamera == null) return;
            Vector3 direction = point - sceneCamera.transform.position;
            yaw = Mathf.Atan2(direction.x,direction.z) * Mathf.Rad2Deg;
            pitch = -Mathf.Atan2(direction.y,new Vector2(direction.x,direction.z).magnitude) * Mathf.Rad2Deg;
            pitch = Mathf.Clamp(pitch,-80,80); ApplyNodePose();
        }
        private void FocusNext()
        {
            if (!IsNode360 || session == null) return;
            var ids = session.Candidates().Where(c=>c.Node.Definition.Id!=session.SourceId).Select(c => c.Node.Definition.Id).ToArray();
            if (ids.Length > 0) FocusTarget(ids[(Array.IndexOf(ids,AttentionId)+1) % ids.Length]);
        }
        public bool IsOccluded(string id)
        {
            if (sceneCamera == null || stage == null || session == null) return false;
            NodeDefinition? node = session.Nodes.FirstOrDefault(n => n.Id == id);
            if (node == null) return false;
            Vector3 delta = node.Position + Vector3.up * 1.4f - sceneCamera.transform.position;
            var ray = new Ray(sceneCamera.transform.position,delta.normalized);
            // Visual occlusion uses building volumes; route validity remains a separate Ground query.
            return stage.Buildings.Any(b => b.IntersectRay(ray,out float distance) && distance < delta.magnitude);
        }
        private void OnDisable()
        {
            actions?.Disable();
            if (session?.IsActive == true) session.Cancel();
            Restore(); subscription?.Dispose(); subscription = null; clickSubscription?.Dispose(); clickSubscription=null;
        }
        private void OnDestroy() => actions?.Dispose();
    }
}
