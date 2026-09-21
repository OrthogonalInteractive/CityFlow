#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Domain.Spatial;
using UnityEngine;

namespace CityFlow.Presentation.Rendering
{
    public sealed class ValidationCityView : MonoBehaviour
    {
        private readonly List<Material> materials = new List<Material>();
        private StageDefinition? stage;
        private Camera? sceneCamera;
        private FlowNetwork? network;
        private readonly Dictionary<long, GameObject> particles = new Dictionary<long, GameObject>();
        private readonly Dictionary<FlowColor, Material> flowMaterials = new Dictionary<FlowColor, Material>();
        public int VisibleFlowCount => particles.Count;
        public int VisibleNodeCount => stage?.Nodes.Count ?? 0;

        public void Initialize(StageDefinition definition, FlowNetwork flowNetwork)
        {
            stage = definition;
            network = flowNetwork;
            sceneCamera = Camera.main;
            if (sceneCamera == null) sceneCamera = new GameObject("Overview Camera", typeof(Camera)).GetComponent<Camera>();
            sceneCamera.transform.position = new Vector3(25, 190, -120);
            sceneCamera.transform.LookAt(new Vector3(0, 0, 2));
            sceneCamera.orthographic = true;
            sceneCamera.orthographicSize = 70;
            sceneCamera.farClipPlane = 500;
            sceneCamera.clearFlags = CameraClearFlags.SolidColor;
            sceneCamera.backgroundColor = new Color(0.035f, 0.052f, 0.082f);
            Material ground = Material(new Color(0.075f, 0.11f, 0.15f));
            Material building = Material(new Color(0.24f, 0.32f, 0.40f));
            Material roof = Material(new Color(0.36f, 0.48f, 0.56f));
            Material grid = Material(new Color(0.11f, 0.17f, 0.22f));
            Rect area = definition.WalkableArea;
            Cube("Ground", new Vector3(area.center.x, definition.GroundHeight - 0.4f, area.center.y),
                new Vector3(area.width, 0.8f, area.height), ground);
            for (float x = area.xMin; x <= area.xMax; x += 10)
                Cube("10 m grid", new Vector3(x, definition.GroundHeight + 0.01f, area.center.y), new Vector3(0.06f, 0.02f, area.height), grid);
            for (float z = area.yMin; z <= area.yMax; z += 10)
                Cube("10 m grid", new Vector3(area.center.x, definition.GroundHeight + 0.01f, z), new Vector3(area.width, 0.02f, 0.06f), grid);
            foreach (Bounds b in definition.Buildings)
            {
                Cube("Building", b.center, b.size, building);
                Cube("Roof", new Vector3(b.center.x, b.max.y + 0.08f, b.center.z), new Vector3(b.size.x + 0.15f, 0.16f, b.size.z + 0.15f), roof);
            }
            foreach (LineSnapshot line in flowNetwork.Snapshot().Lines)
            {
                NodeDefinition destination = definition.Nodes.Single(node => node.Id == line.DestinationId);
                Color color = destination.SinkColor.HasValue ? ColorFor(destination.SinkColor.Value) : new Color(0.3f, 0.65f, 0.55f);
                var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                material.SetColor("_BaseColor", color); materials.Add(material);
                Stroke($"Line {line.Id}: {line.SourceId} -> {line.DestinationId}",
                    line.Route.Points.Select(p => p + Vector3.up * 0.2f).ToArray(), material, 0.5f);
                for (int i = 1; i < line.Route.Points.Count; i++)
                {
                    Vector3 direction = (line.Route.Points[i] - line.Route.Points[i - 1]).normalized;
                    Vector3 side = Vector3.Cross(Vector3.up, direction);
                    Vector3 center = (line.Route.Points[i] + line.Route.Points[i - 1]) * 0.5f + Vector3.up * 0.22f;
                    Stroke("Direction", new[] { center - direction * 1.8f + side, center, center - direction * 1.8f - side }, material, 0.35f);
                }
            }
            foreach (NodeDefinition node in definition.Nodes)
            {
                Color color = node.Kind == NodeKind.Source ? new Color(1, 0.76f, 0.32f) :
                    node.SinkColor.HasValue ? ColorFor(node.SinkColor.Value) : new Color(0.40f, 0.90f, 0.73f);
                var marker = GameObject.CreatePrimitive(node.Kind == NodeKind.Sink ? PrimitiveType.Cylinder :
                    node.Kind == NodeKind.Source ? PrimitiveType.Cube : PrimitiveType.Sphere);
                marker.name = node.Id + " / " + node.Kind;
                marker.transform.SetParent(transform);
                marker.transform.position = node.Position + Vector3.up * 1.4f;
                marker.transform.localScale = new Vector3(3.3f, node.Kind == NodeKind.Sink ? 1.4f : 2.8f, 3.3f);
                marker.GetComponent<Renderer>().sharedMaterial = Material(color);
                Cube("Node pad", node.Position + Vector3.up * 0.15f, new Vector3(6, 0.3f, 6), Material(color * 0.45f));
            }
        }

        private void LateUpdate()
        {
            if (network == null) return;
            NetworkSnapshot snapshot = network.Snapshot();
            var active = new HashSet<long>();
            foreach (LineSnapshot line in snapshot.Lines)
                foreach (InFlightSnapshot flight in line.InFlight)
                {
                    active.Add(flight.Flow.Id);
                    if (!particles.TryGetValue(flight.Flow.Id, out GameObject particle))
                    {
                        particle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                        particle.name = $"FLOW {flight.Flow.Id} / {flight.Flow.Color}";
                        particle.transform.SetParent(transform);
                        particle.transform.localScale = Vector3.one * 1.15f;
                        Destroy(particle.GetComponent<Collider>());
                        if (!flowMaterials.TryGetValue(flight.Flow.Color, out Material material))
                        {
                            material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                            material.SetColor("_BaseColor", Color.Lerp(ColorFor(flight.Flow.Color), Color.white, 0.4f));
                            materials.Add(material); flowMaterials.Add(flight.Flow.Color, material);
                        }
                        particle.GetComponent<Renderer>().sharedMaterial = material;
                        particles.Add(flight.Flow.Id, particle);
                    }
                    particle.transform.position = line.Route.PositionAt(flight.Distance) + Vector3.up * 0.9f;
                }
            foreach (long id in particles.Keys.Where(id => !active.Contains(id)).ToArray())
            { Destroy(particles[id]); particles.Remove(id); }
        }

        public static Color ColorFor(FlowColor color) => color switch
        {
            FlowColor.Red => new Color(1, 0.26f, 0.35f),
            FlowColor.Blue => new Color(0.25f, 0.65f, 1),
            FlowColor.Yellow => Color.yellow,
            FlowColor.Green => Color.green,
            _ => new Color(0.7f, 0.35f, 1)
        };

        private Material Material(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit shader is required.");
            var material = new Material(shader);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.3f);
            materials.Add(material);
            return material;
        }
        private void Cube(string label, Vector3 position, Vector3 scale, Material material)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = label; cube.transform.SetParent(transform);
            cube.transform.position = position; cube.transform.localScale = scale;
            cube.GetComponent<Renderer>().sharedMaterial = material;
        }
        private void Stroke(string label, Vector3[] points, Material material, float width)
        {
            var obj = new GameObject(label);
            obj.transform.SetParent(transform);
            var line = obj.AddComponent<LineRenderer>();
            line.sharedMaterial = material; line.positionCount = points.Length;
            line.SetPositions(points); line.startWidth = width; line.endWidth = width;
            line.numCornerVertices = 2; line.numCapVertices = 2;
        }
        private void OnDestroy()
        {
            foreach (Material material in materials) if (material != null) Destroy(material);
        }
    }
}
