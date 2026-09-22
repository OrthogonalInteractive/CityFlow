#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using CityFlow.Application.Connections;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Presentation.Connections;
using CityFlow.Presentation.Overview;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityFlow.Presentation.UI
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class LineActionsView : MonoBehaviour
    {
        private FlowNetwork? network;
        private ConnectionSession? session;
        private OverviewController? overview;
        private NodeConnectionController? controller;
        private UIDocument? document;
        private VisualElement? root;
        private readonly List<(Button button, Action action)> bindings = new();

        public void Initialize(FlowNetwork state, ConnectionSession connection, OverviewController input, NodeConnectionController cameraController)
        {
            network = state;
            session = connection;
            overview = input;
            controller = cameraController;
            document = GetComponent<UIDocument>();
            if (isActiveAndEnabled) Bind();
        }
        private void OnEnable() => Bind();
        private void Bind()
        {
            Unbind();
            if (document == null || network == null || overview == null || controller == null) return;
            root = document.rootVisualElement;
            Button("line-edit", controller.EditSelectedLine);
            Button("line-delete", () =>
            {
                if (overview.Selected.LineId is int id) network.RequestDeletion(id);
            });
            Button("line-cancel", () =>
            {
                if (overview.Selected.LineId is int id) network.CancelPending(id);
            });
        }
        private void Button(string name, Action action)
        {
            if (root == null) return;
            var button = root.Q<Button>(name);
            button.clicked += action;
            bindings.Add((button, action));
        }
        private void LateUpdate()
        {
            if (document == null || network == null || overview == null) return;
            if (root != document.rootVisualElement) Bind();
            if (root == null) return;
            LineSnapshot? line = network.Snapshot().Lines.FirstOrDefault(l => l.Id == overview.Selected.LineId);
            root.Q("line-actions").style.display = line != null && session?.IsActive != true ? DisplayStyle.Flex : DisplayStyle.None;
            if (line == null) return;
            bool running = line.Status == LineStatus.Running;
            root.Q<Button>("line-edit").SetEnabled(running);
            root.Q<Button>("line-delete").SetEnabled(running);
            root.Q<Button>("line-cancel").SetEnabled(!running);
            string state = line.Status == LineStatus.DeletePending ? "DELETE PENDING" :
                line.Status == LineStatus.RouteChangePending ? "ROUTE CHANGE PENDING" : "RUNNING";
            string wait = line.InFlight.Any(f => f.IsStopped) ? $"WAITING · {line.DestinationId} Buffer space" : "Draining along the current route";
            root.Q<Label>("line-action-detail").text = $"{line.SourceId} → {line.DestinationId} / {state}\nIN-FLIGHT {line.InFlight.Count}/{line.Capacity}" +
                (!running ? $"\n{wait}\nConnection slots stay occupied." : "");
        }
        private void Unbind()
        {
            foreach (var binding in bindings) binding.button.clicked -= binding.action;
            bindings.Clear();
            root = null;
        }
        private void OnDisable() => Unbind();
    }
}
