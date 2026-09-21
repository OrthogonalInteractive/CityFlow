#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using CityFlow.Application.UseCases;
using CityFlow.Application.Connections;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Presentation.Connections;
using CityFlow.Presentation.Overview;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
namespace CityFlow.Presentation.UI
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class GameSessionView : MonoBehaviour
    {
        private FlowSimulation? simulation;
        private FlowNetwork? network;
        private ConnectionSession? connection;
        private OverviewController? overview;
        private NodeConnectionController? connectionCamera;
        private Camera? sceneCamera;
        private UIDocument? document;
        private VisualElement? root;
        private Button? retry;
        private bool retrying;
        private int initialLineCount;
        private readonly Dictionary<string,Button> markers=new();
        public void Initialize(FlowSimulation clock,FlowNetwork state,ConnectionSession wiring,OverviewController input,
            NodeConnectionController cameraController,Camera camera)
        {
            initialLineCount=state.Snapshot().Lines.Count; simulation=clock; network=state; connection=wiring; overview=input; connectionCamera=cameraController; sceneCamera=camera;
            document=GetComponent<UIDocument>(); if(isActiveAndEnabled) Bind();
        }
        private void OnEnable()=>Bind();
        private void Bind()
        {
            Unbind(); if(document==null) return; root=document.rootVisualElement;
            retry=root.Q<Button>("retry-session"); retry.text=$"Retry / {initialLineCount} initial Lines"; retry.clicked+=Retry;
        }
        private void Retry()
        {
            if(retrying || simulation?.Result==null) return;
            retrying=true; retry?.SetEnabled(false);
            // The scene owns cancellation; expected destruction is handled inside the task.
            RetryAsync().Forget();
        }
        private async UniTask RetryAsync()
        {
            try
            {
                await SceneManager.LoadSceneAsync(SceneManager.GetActiveScene().buildIndex)
                    .ToUniTask(cancellationToken:this.GetCancellationTokenOnDestroy());
            }
            catch(OperationCanceledException) { }
            catch(Exception error) { retrying=false; if(retry!=null) retry.SetEnabled(true); Debug.LogException(error); }
        }
        private void LateUpdate()
        {
            if(document==null||simulation==null||network==null||overview==null||connectionCamera==null||sceneCamera==null) return;
            if(root!=document.rootVisualElement) Bind(); if(root==null) return;
            SessionResult? result=simulation.Result;
            root.Q("result-overlay").style.display=result!=null ? DisplayStyle.Flex : DisplayStyle.None;
            if(result!=null)
            {
                if(connection?.IsActive==true) connection.Cancel();
                root.Q<Label>("result-detail").text=$"WAVE {result.Wave}\nSURVIVED {result.SurvivalSeconds:0.0} s\nDELIVERED {result.Delivered}\nCAUSE / SOURCE {result.SourceId}";
            }
            bool recent=simulation.Wave>1 && simulation.ElapsedSeconds-simulation.LastWaveSeconds<12 && result==null;
            Label notice=root.Q<Label>("wave-notice");
            bool intro=initialLineCount==0 && simulation.Wave==1 && simulation.ElapsedSeconds<15 && result==null;
            notice.style.display=(recent||intro) && !connectionCamera.IsNode360 ? DisplayStyle.Flex : DisplayStyle.None;
            notice.text=recent ? $"WAVE {simulation.Wave} / NEW NODES\n"+string.Join(" · ",simulation.LatestAdditions.Select(n=>n.Id)) :
                "Start with 0 Lines\nClick S1 / Hover a target / Click to connect\nEsc pauses while you plan.";
            foreach(var marker in markers.Values) marker.style.display=DisplayStyle.None;
            if(!recent || connectionCamera.IsEditing || connectionCamera.IsNode360) return;
            float width=root.layout.width,height=root.layout.height; if(width<=0||height<=0) return;
            float markerTop=Mathf.Max(200,root.Q(className:"session-controls").worldBound.yMax+36);
            markerTop=Mathf.Max(markerTop,notice.worldBound.yMax+36);
            var safe=new Rect(Mathf.Min(width*0.32f,480),markerTop,Mathf.Max(180,width-860),Mathf.Max(120,height-markerTop-240));
            int index=0;
            foreach(var node in simulation.LatestAdditions)
            {
                string id=node.Id;
                if(!markers.TryGetValue(id,out Button marker))
                {
                    marker=new Button(()=>
                    {
                        if(connectionCamera.IsNode360) connectionCamera.FocusTarget(id);
                        else { overview.Select(OverviewTarget.Node(id)); overview.FocusSelection(); }
                    }) { name="arrival-"+id };
                    marker.AddToClassList("arrival-marker"); marker.AddToClassList("interactive");
                    root.Q("arrival-markers").Add(marker); markers.Add(id,marker);
                }
                Vector3 projected=sceneCamera.WorldToViewportPoint(node.Position+Vector3.up*3);
                Vector2 point=new Vector2(projected.x*width,(1-projected.y)*height);
                bool outside=projected.z<=0||projected.x<0||projected.x>1||projected.y<0||projected.y>1;
                bool displaced=outside||!safe.Contains(point);
                Vector2 direction=point-safe.center; if(projected.z<=0) direction=-direction;
                string arrow=Mathf.Abs(direction.x)>Mathf.Abs(direction.y) ? direction.x>0 ? ">" : "<" : direction.y>0 ? "v" : "^";
                if(displaced)
                {
                    if(direction.sqrMagnitude<0.001f) direction=Vector2.right;
                    float scale=Mathf.Min(safe.width*0.5f/Mathf.Max(0.001f,Mathf.Abs(direction.x)),safe.height*0.5f/Mathf.Max(0.001f,Mathf.Abs(direction.y)));
                    point=safe.center+direction*scale;
                }
                point.y=Mathf.Clamp(point.y+index*52,safe.yMin,safe.yMax); index++;
                marker.text=$"NEW {id} / {node.Kind.ToString().ToUpperInvariant()}\n"+(outside ? arrow+" OFFSCREEN / FOCUS" : "CLICK TO FOCUS");
                marker.style.left=point.x-80; marker.style.top=point.y-24; marker.style.display=DisplayStyle.Flex;
            }
        }
        private void Unbind()
        {
            if(retry!=null) retry.clicked-=Retry; retry=null;
            foreach(var marker in markers.Values) marker.RemoveFromHierarchy(); markers.Clear(); root=null;
        }
        private void OnDisable()=>Unbind();
    }
}
