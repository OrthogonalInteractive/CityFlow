#nullable enable
using System;
using System.Collections.Generic;
using CityFlow.Application.Connections;
using CityFlow.Application.Routing;
using CityFlow.Presentation.Connections;
using CityFlow.Presentation.Overview;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
namespace CityFlow.Presentation.UI
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class RouteEditView : MonoBehaviour
    {
        private LinePreviewService? preview;
        private ConnectionSession? session;
        private NodeConnectionController? controller;
        private OverviewController? overview;
        private Camera? sceneCamera;
        private UIDocument? document;
        private VisualElement? root;
        private InputActionMap? actions;
        private readonly List<VisualElement> handles = new();
        private readonly List<(Button button, Action callback)> bindings = new();
        private int selected = -1;
        public void Initialize(LinePreviewService service, ConnectionSession connection, NodeConnectionController cameraController,
            OverviewController input, Camera camera)
        {
            preview=service; session=connection; controller=cameraController; overview=input; sceneCamera=camera;
            document=GetComponent<UIDocument>(); actions=new InputActionMap("Route editing");
            actions.AddAction("Edit",InputActionType.Button,"<Keyboard>/e").performed += _ =>
            { if (preview.Current != null) controller.BeginEditing(); };
            actions.AddAction("Remove",InputActionType.Button,"<Keyboard>/delete").performed += _ => Remove();
            actions.AddAction("Insert",InputActionType.Button,"<Mouse>/leftButton").performed += _ =>
            {
                if (controller.IsEditing && Time.frameCount > controller.EditingStartedFrame && Mouse.current != null && Keyboard.current?.shiftKey.isPressed == true)
                    InsertAtScreen(Mouse.current.position.ReadValue());
            };
            if (isActiveAndEnabled) Bind();
        }
        private void OnEnable() => Bind();
        private void Bind()
        {
            Unbind(); if (document == null || preview == null || session == null) return;
            root=document.rootVisualElement;
            Button("route-remove",Remove); Button("route-regenerate",()=>{ preview.Regenerate(); selected=-1; });
            Button("route-apply",()=>session.Confirm()); Button("route-cancel",session.Cancel);
            actions?.Enable();
        }
        private void Button(string name,Action callback)
        { if (root == null) return; var b=root.Q<Button>(name); b.clicked+=callback; bindings.Add((b,callback)); }
        private void Remove()
        { if (controller?.IsEditing == true && preview?.RemovePoint(selected) == true) selected=-1; }
        public void InsertAtScreen(Vector2 screen)
        {
            if (controller?.IsEditing != true || overview?.IsPointerBlocked?.Invoke(screen) == true ||
                sceneCamera == null || preview?.Current == null) return;
            var points=preview.Current.Points; var plane=new Plane(Vector3.up,points[0]);
            Ray ray=sceneCamera.ScreenPointToRay(screen); if (!plane.Raycast(ray,out float distance)) return;
            Vector3 position=ray.GetPoint(distance); int nearest=0; float best=float.PositiveInfinity;
            for (int i=0;i<points.Count-1;i++)
            {
                Vector3 d=points[i+1]-points[i]; float t=d.sqrMagnitude>0 ? Mathf.Clamp01(Vector3.Dot(position-points[i],d)/d.sqrMagnitude) : 0;
                float error=(position-(points[i]+d*t)).sqrMagnitude; if (error<best) { best=error; nearest=i; }
            }
            if (preview.InsertPoint(nearest,position)) selected=nearest+1;
        }
        private void Move(int index,Vector2 panelPoint)
        {
            if (root == null || sceneCamera == null || preview?.Current == null) return;
            var screen=new Vector2(panelPoint.x/root.layout.width*sceneCamera.pixelWidth,
                (1-panelPoint.y/root.layout.height)*sceneCamera.pixelHeight);
            var plane=new Plane(Vector3.up,preview.Current.Points[0]); var ray=sceneCamera.ScreenPointToRay(screen);
            if (plane.Raycast(ray,out float distance)) preview.MovePoint(index,ray.GetPoint(distance));
        }
        private void LateUpdate()
        {
            if (document == null || preview == null || controller == null || sceneCamera == null) return;
            if (root != document.rootVisualElement) Bind(); if (root == null) return;
            bool active=controller.IsEditing && preview.Current != null;
            root.Q("route-handles").style.display=active ? DisplayStyle.Flex : DisplayStyle.None;
            if (!active) return;
            var state=preview.Current; if (state == null) return;
            while (handles.Count>state.Points.Count) { handles[handles.Count-1].RemoveFromHierarchy(); handles.RemoveAt(handles.Count-1); }
            while (handles.Count<state.Points.Count)
            {
                int index=handles.Count; var handle=new Label { name="route-point-"+index };
                handle.AddToClassList("route-handle"); handle.AddToClassList("interactive");
                handle.RegisterCallback<PointerDownEvent>(e=>
                {
                    if (e.button!=0 || preview.Current == null || index==0 || index==preview.Current.Points.Count-1) return;
                    selected=index; handle.CapturePointer(e.pointerId); e.StopPropagation();
                });
                handle.RegisterCallback<PointerMoveEvent>(e=>{ if (handle.HasPointerCapture(e.pointerId)) Move(index,e.position); });
                handle.RegisterCallback<PointerUpEvent>(e=>handle.ReleasePointer(e.pointerId));
                handles.Add(handle); root.Q("route-handles").Add(handle);
            }
            for (int i=0;i<handles.Count;i++)
            {
                Vector3 point=sceneCamera.WorldToViewportPoint(state.Points[i]+Vector3.up*0.4f);
                var label=(Label)handles[i]; label.text=i==0 ? "A" : i==handles.Count-1 ? "B" : i.ToString();
                label.style.left=point.x*root.layout.width-15; label.style.top=(1-point.y)*root.layout.height-15;
                label.EnableInClassList("chosen",i==selected); label.EnableInClassList("endpoint",i==0||i==handles.Count-1);
            }
            bool editable=selected>0 && selected<handles.Count-1;
            root.Q<Button>("route-remove").SetEnabled(editable);
            root.Q<Button>("route-apply").SetEnabled(state.CanConfirm);
            root.Q<Label>("route-point-detail").text=editable ? $"POINT {selected} / X {state.Points[selected].x:0.0} / Z {state.Points[selected].z:0.0}" : "Select an interior handle to move or remove.";
        }
        private void Unbind()
        {
            actions?.Disable(); foreach(var b in bindings) b.button.clicked-=b.callback; bindings.Clear();
            foreach(var h in handles) h.RemoveFromHierarchy(); handles.Clear(); root=null;
        }
        private void OnDisable() => Unbind();
        private void OnDestroy() => actions?.Dispose();
    }
}
