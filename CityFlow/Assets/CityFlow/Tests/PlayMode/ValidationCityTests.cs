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
        public IEnumerator ObstacleSurfacesAndCollidersMatchTheAuthoredRoutingVolumes()
        {
            yield return SceneManager.LoadSceneAsync("WiringLab", LoadSceneMode.Single);
            yield return null;
            var stage = Object.FindAnyObjectByType<CityFlowLifetimeScope>().Container.Resolve<StageDefinition>();
            var colliders = Object.FindObjectsByType<BoxCollider>()
                .Where(c => c.name == "Building" || c.name == "Roof").ToArray();
            // Runtime primitives are positioned after creation; sync before querying physics bounds.
            Physics.SyncTransforms();
            Assert.That(colliders.Length, Is.EqualTo(stage.Buildings.Count), "Facade decoration must not add obstacle volumes.");
            foreach (var bounds in stage.Buildings)
            {
                var collider = colliders.Single(c => Vector3.Distance(c.bounds.center, bounds.center) < 0.001f);
                Assert.That(Vector3.Distance(collider.bounds.size, bounds.size), Is.LessThan(0.001f));
                var renderer = collider.GetComponent<Renderer>();
                Assert.That(Vector3.Distance(renderer.bounds.center, bounds.center), Is.LessThan(0.001f));
                Assert.That(Vector3.Distance(renderer.bounds.size, bounds.size), Is.LessThan(0.001f));
                Assert.That(renderer.sharedMaterial.shader.isSupported, Is.True);
            }
        }

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
            var network = scope.Container.Resolve<FlowNetwork>();
            var snapshot = network.Snapshot();
            Assert.That(snapshot.Lines.Count, Is.EqualTo(5));
            Assert.That(snapshot.Nodes.Single(node => node.Definition.Id == "S1").OutgoingUsed, Is.EqualTo(3));
            Assert.That(snapshot.Nodes.Sum(node => node.IncomingUsed), Is.EqualTo(5));
            Assert.That(snapshot.Nodes.Sum(node => node.OutgoingUsed), Is.EqualTo(5));

            var view = Object.FindAnyObjectByType<ValidationCityView>();
            Assert.That(view.VisibleNodeCount, Is.EqualTo(stage.Nodes.Count));
            Assert.That(Object.FindObjectsByType<Renderer>().Length, Is.GreaterThan(20));
        }
    }
}
