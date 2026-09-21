#nullable enable

using System;
using CityFlow.Application.Routing;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Domain.Spatial;
using R3;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace CityFlow.Presentation.UI
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class GroundPreviewView : MonoBehaviour
    {
        private LinePreviewService? service;
        private UIDocument? document;
        private VisualElement? boundRoot;
        private IDisposable? subscription;
        private GameObject? drawing;
        private Material? validMaterial, invalidMaterial;
        public void Initialize(LinePreviewService preview)
        {
            service = preview; document = GetComponent<UIDocument>();
            if (isActiveAndEnabled) Bind();
        }
        private void OnEnable() => Bind();
        private void OnDisable() { subscription?.Dispose(); subscription = null; boundRoot = null; ClearDrawing(); }
        private void LateUpdate()
        { if (document != null && boundRoot != document.rootVisualElement) Bind(); }
        private void Bind()
        {
            subscription?.Dispose(); subscription = null;
            if (document == null || service == null) return;
            boundRoot = document.rootVisualElement;
            subscription = service.Changed.Subscribe(Render);
            Render(service.Current);
        }
        private void Render(LinePreviewState? state)
        {
            ClearDrawing();
            if (boundRoot == null) return;
            string text = "";
            if (state != null)
            {
                string status = state.ConnectionFailure != ConnectionFailure.None ? ConnectionReason(state.ConnectionFailure) :
                    state.Geometry.IsValid ? "VALID ROUTE" : GeometryReason(state.Geometry.Failure);
                string segment = state.Geometry.InvalidSegment >= 0 ? $" · SEGMENT {state.Geometry.InvalidSegment+1}" : "";
                string metrics = state.Geometry.IsValid ? $"\nLENGTH {state.Length:0.0} m · TRAVEL {state.TravelTime:0.00} s\nCAPACITY {state.Capacity} · THROUGHPUT {state.Throughput:0.00} FLOW/s" : "";
                text = $"{state.SourceId} → {state.DestinationId}\n{status}{segment}{metrics}\nAFTER CONFIRM · OUT {state.OutgoingAfter}/{state.OutgoingLimit} · IN {state.IncomingAfter}/{state.IncomingLimit}";
            }
            foreach(string name in new[] { "route-feedback", "edit-route-feedback" })
            {
                Label label = boundRoot.Q<Label>(name);
                label.text = text; label.EnableInClassList("full",state != null && !state.CanConfirm);
            }
            if (state == null) return;
            drawing = new GameObject("Ground Route Preview"); drawing.transform.SetParent(transform.parent);
            if (state.Geometry.Failure == RouteFailure.SearchFailed || state.Geometry.Failure == RouteFailure.InvalidPoints) return;
            if (validMaterial == null) validMaterial = CreateMaterial(new Color(0.35f,1,0.94f,0.7f));
            if (invalidMaterial == null) invalidMaterial = CreateMaterial(new Color(1,0.32f,0.20f,0.75f));
            for (int i = 1; i < state.Points.Count; i++)
            {
                Vector3 a = state.Points[i-1] + Vector3.up*0.3f, b = state.Points[i] + Vector3.up*0.3f;
                float length = Vector3.Distance(a,b);
                if (float.IsNaN(length) || float.IsInfinity(length) || length == 0) continue;
                Material material = state.CanConfirm || (state.Geometry.InvalidSegment >= 0 && state.Geometry.InvalidSegment != i-1) ? validMaterial : invalidMaterial;
                for (float d = 0; d < length; d += 3)
                    Stroke(new[] { Vector3.Lerp(a,b,d/length),Vector3.Lerp(a,b,Mathf.Min(length,d+1.8f)/length) },material,0.8f);
                Vector3 direction = (b-a).normalized, side = Vector3.Cross(Vector3.up,direction), middle = Vector3.Lerp(a,b,0.6f);
                Stroke(new[] { middle-direction*1.6f+side, middle, middle-direction*1.6f-side },material,0.65f);
            }
        }
        private void Stroke(Vector3[] points, Material material, float width)
        {
            if (drawing == null) return;
            var obj = new GameObject("Preview stroke"); obj.transform.SetParent(drawing.transform);
            var line = obj.AddComponent<LineRenderer>(); line.sharedMaterial = material; line.positionCount = points.Length;
            line.SetPositions(points); line.startWidth = line.endWidth = width; line.numCapVertices = 2;
        }
        private static Material CreateMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) throw new InvalidOperationException("URP Unlit shader is required.");
            var material = new Material(shader);
            material.SetColor("_BaseColor",color); material.SetFloat("_Surface",1);
            material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha); material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite",0); material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.renderQueue = 3000;
            return material;
        }
        private static string GeometryReason(RouteFailure failure) => failure switch
        {
            RouteFailure.Obstacle => "INVALID · Building collision",
            RouteFailure.OutsideArea => "INVALID · Outside walkable area",
            RouteFailure.GroundHeight => "INVALID · Ground height required",
            RouteFailure.EndpointMismatch => "INVALID · Endpoints must stay on Nodes",
            RouteFailure.SearchFailed => "Could not generate an automatic route.\nEndpoints retained for manual editing.",
            _ => "INVALID · Route points are not valid"
        };
        private static string ConnectionReason(ConnectionFailure failure) => "BLOCKED · "+ConnectionReadout.Reason(failure);
        private void ClearDrawing() { if (drawing != null) Destroy(drawing); drawing = null; }
        private void OnDestroy()
        { if (validMaterial != null) Destroy(validMaterial); if (invalidMaterial != null) Destroy(invalidMaterial); }
    }
}
