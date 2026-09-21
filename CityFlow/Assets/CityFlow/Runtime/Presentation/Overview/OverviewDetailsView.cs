#nullable enable

using System;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Application.UseCases;
using CityFlow.Presentation.Connections;
using CityFlow.Presentation.Rendering;
using R3;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace CityFlow.Presentation.Overview
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class OverviewDetailsView : MonoBehaviour
    {
        private OverviewController? controller;
        private NodeConnectionController? connection;
        private FlowNetwork? network;
        private FlowSimulation? simulation;
        private ValidationCityView? city;
        private UIDocument? document;
        private Camera? sceneCamera;
        private IDisposable? selectionSubscription;
        public bool HasActiveSubscription => selectionSubscription != null;
        public void Initialize(OverviewController input, FlowNetwork flowNetwork, ValidationCityView view,
            FlowSimulation clock, NodeConnectionController wiring, Camera camera)
        { sceneCamera=camera; simulation=clock; connection=wiring; controller=input; network=flowNetwork; city=view; document=GetComponent<UIDocument>(); if(isActiveAndEnabled) Subscribe(); }
        private void OnEnable() => Subscribe();
        private void Subscribe()
        {
            selectionSubscription?.Dispose(); selectionSubscription=null;
            if(controller==null || city==null) return;
            selectionSubscription=controller.SelectionChanged.Subscribe(target=> { if(city!=null) city.SetSelection(target); });
            city.SetSelection(controller.Selected);
        }
        private void OnDisable() { selectionSubscription?.Dispose(); selectionSubscription=null; }
        private void LateUpdate()
        {
            if(document==null || controller==null || network==null || simulation==null || connection==null || sceneCamera==null) return;
            var root=document.rootVisualElement;
            var panel=root.Q("node-tooltip");
            Vector2 screen=controller.enabled ? controller.HoverScreenPosition : Mouse.current?.position.ReadValue() ?? Vector2.zero;
            Vector2 point=RuntimePanelUtils.ScreenToPanel(root.panel,new Vector2(screen.x,Screen.height-screen.y));
            OverviewTarget target=default;
            // Connection candidate controls expose the same Node details as world markers.
            for(VisualElement? hit=root.panel.Pick(point);hit!=null;hit=hit.parent)
                if(hit.userData is OverviewTarget node) { target=node; break; }
            if(target.IsEmpty && controller.IsPointerBlocked?.Invoke(screen)!=true)
                target=controller.enabled ? controller.Hovered : controller.Pick(screen);
            bool visible=!target.IsEmpty && !connection.IsEditing && simulation.Result==null;
            panel.style.display=visible ? DisplayStyle.Flex : DisplayStyle.None;
            if(city!=null) city.SetSelection(target.IsEmpty ? controller.Selected : target);
            if(!visible) return;
            var snapshot=network.Snapshot();
            string detail=OverviewReadout.Describe(target,snapshot,network.Settings,simulation.GenerationIntervalScale);
            if(target.NodeId!=null && simulation.SourceStartRemaining(target.NodeId)>0)
                detail+=$"\nPREPARING · {simulation.SourceStartRemaining(target.NodeId):0.0}s";
            root.Q<Label>("hover-detail").text=detail;
            panel.EnableInClassList("hover-warning",detail.Contains("TO GAME OVER") || detail.Contains("INPUT STOPPED"));
            var parent=panel.parent;
            point=parent.WorldToLocal(point);
            float width=Mathf.Min(320,parent.layout.width-24);
            panel.style.width=width;
            float height=float.IsNaN(panel.layout.height) ? 230 : panel.layout.height;
            Vector2 position=new Vector2(point.x+30,point.y+20);
            if(connection.IsNode360)
            {
                // Keep hover details outside the city viewport, including when a world Node is hovered.
                width=Mathf.Min(width,parent.layout.width*0.28f-24);
                panel.style.width=width;
                position.x=parent.layout.width*0.72f+12;
            }
            else
            {
                // Try both sides and vertical directions before covering any nearby Node.
                float best=float.PositiveInfinity;
                foreach(Vector2 candidate in new[] {
                    new Vector2(point.x+30,point.y+20), new Vector2(point.x-width-30,point.y+20),
                    new Vector2(point.x+30,point.y-height-20), new Vector2(point.x-width-30,point.y-height-20) })
                {
                    Vector2 bounded=Clamp(candidate,parent,width,height);
                    var bounds=new Rect(bounded,new Vector2(width,height));
                    float score=0;
                    foreach(var node in snapshot.Nodes)
                    {
                        Vector3 projected=sceneCamera.WorldToScreenPoint(node.Definition.Position+Vector3.up*1.4f);
                        if(projected.z<=0) continue;
                        Vector2 center=parent.WorldToLocal(RuntimePanelUtils.ScreenToPanel(root.panel,new Vector2(projected.x,Screen.height-projected.y)));
                        var obstacle=new Rect(center-Vector2.one*24,Vector2.one*48);
                        if(bounds.Overlaps(obstacle)) score+=1;
                    }
                    if(score<best) { best=score; position=bounded; }
                    if(score==0) break;
                }
            }
            position=Clamp(position,parent,width,height);
            panel.style.left=position.x; panel.style.top=position.y;
        }
        private static Vector2 Clamp(Vector2 position,VisualElement parent,float width,float height) => new Vector2(
            Mathf.Clamp(position.x,12,Mathf.Max(12,parent.layout.width-width-12)),
            Mathf.Clamp(position.y,12,Mathf.Max(12,parent.layout.height-height-12)));
    }
}
