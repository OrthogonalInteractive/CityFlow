#nullable enable

using System.Collections;
using System.Linq;
using CityFlow.Application.Routing;
using CityFlow.Composition;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Presentation.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using VContainer;

namespace CityFlow.Tests.PlayMode
{
    public sealed class GroundPreviewTests
    {
        [UnitySetUp] public IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("Bootstrap",LoadSceneMode.Single);
            yield return null;
            Object.FindAnyObjectByType<SimulationDriver>().enabled = false;
        }
        [UnityTest] public IEnumerator GenerateButtonShowsPreviewWithoutCreatingLineAndCancelRemovesIt()
        {
            var root = Object.FindAnyObjectByType<UIDocument>().rootVisualElement;
            Button generate = root.Q<Button>("preview-generate");
            Assert.That(generate, Is.Not.Null, "Ground Preview controls must be composed.");
            var scope = Object.FindAnyObjectByType<CityFlowLifetimeScope>();
            var n = scope.Container.Resolve<FlowNetwork>(); var preview = scope.Container.Resolve<LinePreviewService>();
            int count = n.Snapshot().Lines.Count;
            generate.Focus();
            using (var submit = NavigationSubmitEvent.GetPooled()) generate.SendEvent(submit);
            yield return null;
            Assert.That(preview.Current, Is.Not.Null);
            Assert.That(root.Q<Label>("preview-detail").text, Does.Contain("R1 → BLUE").And.Contain("LENGTH").And.Contain("AFTER"));
            var drawing = GameObject.Find("Ground Route Preview");
            Assert.That(drawing, Is.Not.Null); Assert.That(drawing.GetComponentsInChildren<LineRenderer>().Length, Is.GreaterThan(0));
            Assert.That(n.Snapshot().Lines.Count, Is.EqualTo(count));
            var cancel = root.Q<Button>("preview-cancel");
            cancel.Focus();
            using (var submit = NavigationSubmitEvent.GetPooled()) cancel.SendEvent(submit);
            yield return null;
            Assert.That(preview.Current, Is.Null); Assert.That(GameObject.Find("Ground Route Preview"), Is.Null);
            Assert.That(n.Snapshot().Lines.Count, Is.EqualTo(count));
        }
        [UnityTest] public IEnumerator InvalidGeometryAndConnectionExplainWhyPreviewCannotBeConfirmed()
        {
            var root = Object.FindAnyObjectByType<UIDocument>().rootVisualElement;
            Assert.That(root.Q<Button>("preview-generate"), Is.Not.Null);
            var preview = Object.FindAnyObjectByType<CityFlowLifetimeScope>().Container.Resolve<LinePreviewService>();
            preview.Generate("S1","BLUE"); yield return null;
            Assert.That(root.Q<Label>("preview-detail").text, Does.Contain("already exists"));
            preview.Generate("R1","BLUE");
            var current = preview.Current ?? throw new AssertionException("Preview missing");
            preview.UpdatePoints(new[] { current.Points[0],current.Points.Last() }); yield return null;
            Assert.That(root.Q<Label>("preview-detail").text, Does.Contain("Building collision"));
        }
    }
}
