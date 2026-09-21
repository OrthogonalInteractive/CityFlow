#nullable enable
using CityFlow.Application.UseCases;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
namespace CityFlow.Presentation.UI
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class PauseView : MonoBehaviour
    {
        private FlowSimulation? simulation;
        private InputAction? pause;
        private UIDocument? document;
        private VisualElement? root;
        private Button? button;
        public void Initialize(FlowSimulation value)
        {
            simulation=value; document=GetComponent<UIDocument>();
            pause=new InputAction("Pause",InputActionType.Button,"<Keyboard>/escape"); pause.performed+=_=>Toggle();
            if(isActiveAndEnabled) Bind();
        }
        private void Toggle() { if(simulation!=null) simulation.SetPaused(!simulation.IsPaused); }
        private void OnEnable()=>Bind();
        private void Bind()
        {
            Unbind(); if(document==null) return; root=document.rootVisualElement;
            button=root.Q<Button>("pause-toggle"); button.clicked+=Toggle; pause?.Enable();
        }
        private void LateUpdate()
        {
            if(document==null||simulation==null) return; if(root!=document.rootVisualElement) Bind(); if(root==null||button==null) return;
            root.Q<Label>("pause-status").text=simulation.IsPaused ? "PAUSED / EDITING AVAILABLE" : "SIMULATION RUNNING";
            button.text=simulation.IsPaused ? "Resume [Esc]" : "Pause [Esc]";
            if(simulation.Result != null) { root.Q<Label>("pause-status").text="SESSION ENDED"; button.SetEnabled(false); }
            button.parent.EnableInClassList("paused",simulation.IsPaused);
        }
        private void Unbind() { pause?.Disable(); if(button!=null) button.clicked-=Toggle; button=null; root=null; }
        private void OnDisable()=>Unbind();
        private void OnDestroy()=>pause?.Dispose();
    }
}
