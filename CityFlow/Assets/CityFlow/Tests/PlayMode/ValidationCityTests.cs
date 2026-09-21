#nullable enable

using System.Collections;
using System.Linq;
using CityFlow.Composition;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Domain.Spatial;
using CityFlow.Presentation.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VContainer;

namespace CityFlow.Tests.PlayMode
{
    public sealed class ValidationCityTests
    {
        [UnityTest]
        public IEnumerator BootstrapLoadsGroundNodesAndBothSinkColors()
        {
            yield return SceneManager.LoadSceneAsync("Bootstrap", LoadSceneMode.Single);
            yield return null;
            var scope = Object.FindAnyObjectByType<CityFlowLifetimeScope>();
            StageDefinition stage = scope.Container.Resolve<StageDefinition>();
            Assert.That(stage.Nodes.Count, Is.EqualTo(5));
            Assert.That(stage.Nodes.All(node => node.Position.y == stage.GroundHeight), Is.True);
            Assert.That(stage.Nodes.Where(node => node.Kind == NodeKind.Sink).Select(node => node.SinkColor),
                Is.EquivalentTo(new[] { (FlowColor?)FlowColor.Red, FlowColor.Blue }));
            var view = Object.FindAnyObjectByType<ValidationCityView>();
            Assert.That(view.VisibleNodeCount, Is.EqualTo(stage.Nodes.Count));
            Assert.That(Object.FindObjectsByType<Renderer>().Length, Is.GreaterThan(20));
        }
    }
}
