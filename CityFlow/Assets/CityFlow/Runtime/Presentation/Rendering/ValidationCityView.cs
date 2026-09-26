#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Domain.Spatial;
using CityFlow.Presentation.Overview;
using UnityEngine;
using UnityEngine.Rendering;

namespace CityFlow.Presentation.Rendering
{
    public sealed class ValidationCityView : MonoBehaviour
    {
        private readonly List<Material> materials = new List<Material>();
        private StageDefinition? stage;
        private Material? relayHeightSurface;
        private GameObject? groundView;
        private Rect drawnArea;
        private readonly List<(GameObject View, bool AlongZ, float Coordinate)> gridViews = new();
        private readonly Dictionary<Renderer, Bounds> buildingBounds = new();
        private readonly List<GameObject> areaBorders = new();
        private readonly List<Renderer> buildings = new();
        private readonly List<Renderer> scenery = new();
        private readonly Dictionary<Renderer,Material[]> opaqueBuildings = new();
        private readonly Dictionary<Renderer,ShadowCastingMode> originalShadows = new();
        private readonly Dictionary<Material,Material> transparentMaterials = new();
        private bool transparentBuildings;
        private Camera? sceneCamera;
        private FlowNetwork? network;
        private readonly Dictionary<long, GameObject> particles = new Dictionary<long, GameObject>();
        private readonly Dictionary<FlowColor, Material> flowMaterials = new Dictionary<FlowColor, Material>();
        private readonly Dictionary<int, List<LineRenderer>> lineViews = new();
        private readonly Dictionary<int, LineRoute> drawnRoutes = new();
        private readonly Dictionary<int, LineStatus> drawnStatuses = new();
        private readonly Dictionary<string, GameObject[]> nodeViews = new();
        private OverviewTarget selected;
        public ConnectionFocus Focus { get; } = new();
        public void SetSelection(OverviewTarget target)
        {
            selected = target;
            if (network != null) Focus.Refresh(network.Snapshot(), target.NodeId);
        }
        private Vector3 RouteLift => stage?.AllowsHeight == true ? Vector3.zero : Vector3.up * 0.2f;
        public int VisibleFlowCount => particles.Count;
        public int VisibleNodeCount => nodeViews.Count;

        public void Initialize(StageDefinition definition, FlowNetwork flowNetwork, Material obstacleSurface,
            VolumeProfile obstacleGlow, Material relayHeightSurface, AuthoredCityScenery? authoredScenery = null)
        {
            stage = definition;
            this.relayHeightSurface = relayHeightSurface;
            network = flowNetwork;
            sceneCamera = Camera.main;
            if (sceneCamera == null) sceneCamera = new GameObject("Overview Camera", typeof(Camera)).GetComponent<Camera>();
            if (authoredScenery == null)
            {
                sceneCamera.transform.position = new Vector3(25, 190, -120);
                sceneCamera.transform.LookAt(new Vector3(0, 0, 2));
                sceneCamera.orthographic = true;
                sceneCamera.orthographicSize = 70;
                sceneCamera.farClipPlane = Mathf.Max(500, definition.MaximumArea.size.magnitude * 2);
                sceneCamera.clearFlags = CameraClearFlags.SolidColor;
                sceneCamera.backgroundColor = new Color(0.035f, 0.052f, 0.082f);
                Material ground = Material(new Color(0.075f, 0.11f, 0.15f));
                var glowObject = new GameObject("Obstacle glow");
                glowObject.transform.SetParent(transform, false);
                var volume = glowObject.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = 10;
                volume.sharedProfile = obstacleGlow;
                Material grid = Material(new Color(0.11f, 0.17f, 0.22f));
                Rect area = definition.WalkableArea;
                groundView = Cube("Ground", new Vector3(area.center.x, definition.GroundHeight - 0.4f, area.center.y),
                    new Vector3(area.width, 0.8f, area.height), ground);
                for (float x = definition.MaximumArea.xMin; x <= definition.MaximumArea.xMax; x += 10)
                    gridViews.Add((Cube("10 m grid", Vector3.zero, Vector3.one, grid), true, x));
                for (float z = definition.MaximumArea.yMin; z <= definition.MaximumArea.yMax; z += 10)
                    gridViews.Add((Cube("10 m grid", Vector3.zero, Vector3.one, grid), false, z));
                if (definition.MaximumArea != area)
                {
                    var border = Material(new Color(0.3f, 0.63f, 0.72f));
                    for (int i = 0; i < 4; i++)
                    {
                        var edge = Cube("Unlocked area boundary", Vector3.zero, Vector3.one, border);
                        var collider = edge.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
                        areaBorders.Add(edge);
                    }
                }
                foreach (Bounds b in definition.Buildings)
                {
                    var renderer = Cube("Building", b.center, b.size, obstacleSurface).GetComponent<Renderer>();
                    buildings.Add(renderer); buildingBounds.Add(renderer, b);
                }
                scenery.AddRange(GetComponentsInChildren<MeshRenderer>(true));
                RefreshArea();
            }
            else
            {
                scenery.AddRange(authoredScenery.EnvironmentRenderers);
                buildings.AddRange(authoredScenery.TransparentRenderers);
            }
            foreach (Renderer renderer in buildings)
            {
                opaqueBuildings.Add(renderer, renderer.sharedMaterials);
                originalShadows.Add(renderer, renderer.shadowCastingMode);
            }
            CreateLines(flowNetwork.Snapshot());
            CreateNodes();
        }

        private void RefreshArea()
        {
            if (stage == null || groundView == null || drawnArea == stage.WalkableArea) return;
            Rect area = stage.WalkableArea; drawnArea = area;
            groundView.transform.position = new Vector3(area.center.x, stage.GroundHeight - 0.4f, area.center.y);
            groundView.transform.localScale = new Vector3(area.width, 0.8f, area.height);
            foreach (var grid in gridViews)
            {
                float min = grid.AlongZ ? area.xMin : area.yMin, max = grid.AlongZ ? area.xMax : area.yMax;
                grid.View.SetActive(grid.Coordinate >= min && grid.Coordinate <= max);
                grid.View.transform.position = grid.AlongZ ? new Vector3(grid.Coordinate, stage.GroundHeight + 0.01f, area.center.y) :
                    new Vector3(area.center.x, stage.GroundHeight + 0.01f, grid.Coordinate);
                grid.View.transform.localScale = grid.AlongZ ? new Vector3(0.06f, 0.02f, area.height) : new Vector3(area.width, 0.02f, 0.06f);
            }
            foreach (var entry in buildingBounds)
            {
                Bounds authored = entry.Value;
                float left = Mathf.Max(area.xMin, authored.min.x), right = Mathf.Min(area.xMax, authored.max.x);
                float bottom = Mathf.Max(area.yMin, authored.min.z), top = Mathf.Min(area.yMax, authored.max.z);
                entry.Key.gameObject.SetActive(right > left && top > bottom);
                if (right <= left || top <= bottom) continue;
                // The hidden part already exists in Domain; clipping only reveals immutable geometry.
                entry.Key.transform.position = new Vector3((left + right) * 0.5f, authored.center.y, (bottom + top) * 0.5f);
                entry.Key.transform.localScale = new Vector3(right - left, authored.size.y, top - bottom);
            }
            for (int i = 0; i < areaBorders.Count; i++)
            {
                bool alongZ = i < 2;
                areaBorders[i].transform.position = new Vector3(alongZ ? (i == 0 ? area.xMin : area.xMax) : area.center.x,
                    stage.GroundHeight + 0.03f, alongZ ? area.center.y : (i == 2 ? area.yMin : area.yMax));
                areaBorders[i].transform.localScale = alongZ ? new Vector3(0.18f, 0.04f, area.height) : new Vector3(area.width, 0.04f, 0.18f);
            }
        }
        private void CreateNodes()
        {
            if (network == null || stage == null || relayHeightSurface == null) return;
            foreach (NodeDefinition node in network.NodeDefinitions)
            {
                if(nodeViews.ContainsKey(node.Id)) continue;
                Color color = node.Kind == NodeKind.Source ? new Color(0.85f, 0.85f, 0.85f) :
                    node.SinkColor.HasValue ? ColorFor(node.SinkColor.Value) : new Color(0.72f, 0.72f, 0.72f);
                var marker = GameObject.CreatePrimitive(node.Kind == NodeKind.Sink ? PrimitiveType.Cylinder :
                    node.Kind == NodeKind.Source ? PrimitiveType.Cube : PrimitiveType.Sphere);
                marker.name = node.Id + " / " + node.Kind;
                marker.transform.SetParent(transform);
                marker.transform.position = node.Position + Vector3.up * 1.4f;
                marker.transform.localScale = new Vector3(3.3f, node.Kind == NodeKind.Sink ? 1.4f : 2.8f, 3.3f);
                marker.GetComponent<Renderer>().sharedMaterial = Material(color);
                GameObject pad = Cube("Node pad", node.Position + Vector3.up * 0.15f, new Vector3(6, 0.3f, 6), Material(color * 0.45f));
                var parts = new List<GameObject> { marker, pad };
                float rise = stage.ConnectionCeiling(node) - node.Position.y;
                if (node is RelayNodeDefinition && rise > 0)
                    parts.Add(CreateRelayHeight(node, rise, relayHeightSurface));
                nodeViews.Add(node.Id, parts.ToArray());
            }
        }

        private GameObject CreateRelayHeight(NodeDefinition node, float rise, Material surface)
        {
            var projection = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            projection.name = "Relay height " + node.Id;
            projection.transform.SetParent(transform, false);
            projection.transform.position = node.Position + Vector3.up * (rise * 0.5f);
            // Unity's cylinder is two units tall. Its cap marks the exact connection ceiling.
            projection.transform.localScale = new Vector3(4.8f, rise * 0.5f, 4.8f);
            var collider = projection.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            var renderer = projection.GetComponent<Renderer>();
            renderer.sharedMaterial = surface;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return projection;
        }

        private void CreateLines(NetworkSnapshot snapshot)
        {
            if (stage == null) return;
            foreach(int id in lineViews.Keys.ToArray())
            {
                LineSnapshot? current=snapshot.Lines.FirstOrDefault(l=>l.Id==id);
                if (current != null && ReferenceEquals(current.Route,drawnRoutes[id]) && current.Status == drawnStatuses[id]) continue;
                Material material=lineViews[id][0].sharedMaterial;
                foreach(var renderer in lineViews[id]) Destroy(renderer.gameObject);
                materials.Remove(material); Destroy(material); lineViews.Remove(id); drawnRoutes.Remove(id); drawnStatuses.Remove(id);
            }
            foreach (LineSnapshot line in snapshot.Lines)
            {
                if (lineViews.ContainsKey(line.Id)) continue;
                var renderers = new List<LineRenderer>();
                lineViews.Add(line.Id, renderers); drawnRoutes.Add(line.Id,line.Route); drawnStatuses.Add(line.Id, line.Status);
                NodeDefinition destination = snapshot.Nodes.Single(node => node.Definition.Id == line.DestinationId).Definition;
                Color color = destination.SinkColor.HasValue ? ColorFor(destination.SinkColor.Value) : new Color(0.72f, 0.72f, 0.72f);
                var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                material.SetColor("_BaseColor", color); materials.Add(material);
                renderers.Add(Stroke($"Line {line.Id}: {line.SourceId} -> {line.DestinationId}",
                    line.Route.Points.Select(p => p + RouteLift).ToArray(), material, 0.5f));
                if (line.Status == LineStatus.DeletePending)
                {
                    renderers[0].enabled = false;
                    for (double distance = 0; distance < line.Route.Length; distance += 4)
                        renderers.Add(Stroke("Deletion dash " + line.Id,
                            PathBetween(line.Route, distance, Math.Min(line.Route.Length, distance + 2.4)), material, 0.5f));
                }
                else if (line.Status == LineStatus.RouteChangePending)
                {
                    renderers[0].enabled = false;
                    foreach (float side in new[] { -0.55f, 0.55f })
                        renderers.Add(Stroke("Route change rail " + line.Id, OffsetPath(line.Route, side), material, 0.35f));
                }
                for (int i = 1; i < line.Route.Points.Count; i++)
                {
                    Vector3 direction = (line.Route.Points[i] - line.Route.Points[i - 1]).normalized;
                    Vector3 side = RouteVisualGeometry.Side(direction);
                    Vector3 center = (line.Route.Points[i] + line.Route.Points[i - 1]) * 0.5f + RouteLift;
                    renderers.Add(Stroke("Direction", new[] { center - direction * 1.8f + side, center, center - direction * 1.8f - side }, material, 0.35f));
                }
            }
        }

        private void LateUpdate()
        {
            if (network == null) return;
            NetworkSnapshot snapshot = network.Snapshot();
            RefreshArea();
            Focus.Refresh(snapshot, selected.NodeId);
            CreateNodes();
            CreateLines(snapshot);
            foreach (var renderer in scenery) Focus.Apply(renderer, false);
            foreach (var node in nodeViews)
                foreach (var part in node.Value) Focus.Apply(part.GetComponent<Renderer>(), Focus.IncludesNode(node.Key));
            foreach (LineSnapshot line in snapshot.Lines)
            {
                var destination=network.NodeDefinitions.Single(n=>n.Id==line.DestinationId);
                bool stopped=line.InFlight.Any(f=>f.IsStopped);
                Color warning=new Color(1,0.38f,0.10f);
                Color tint=stopped ? warning :
                    line.Status==LineStatus.DeletePending ? new Color(0.85f,0.85f,0.85f) :
                    line.Status==LineStatus.RouteChangePending ? new Color(0.8f,0.5f,1) :
                    destination.SinkColor.HasValue ? ColorFor(destination.SinkColor.Value) : new Color(0.72f, 0.72f, 0.72f);
                lineViews[line.Id][0].sharedMaterial.SetColor("_BaseColor",tint);
                bool highlight = selected.LineId == line.Id || (selected.NodeId != null &&
                    (line.SourceId == selected.NodeId || line.DestinationId == selected.NodeId));
                foreach (LineRenderer renderer in lineViews[line.Id])
                {
                    Focus.Apply(renderer, Focus.IncludesLine(line.Id));
                    renderer.widthMultiplier = (highlight ? 1.0f : stopped ? 0.8f : 0.45f) *
                        (renderer.name.StartsWith("Route change rail", StringComparison.Ordinal) ? 0.65f : 1);
                    renderer.startColor = renderer.endColor = Color.white;
                }
            }
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
                    // Preserve the FLOW color and keep close-up particles at their normal size.
                    particle.transform.localScale = flight.IsStopped && !transparentBuildings ? new Vector3(1.5f, 0.45f, 1.5f) : Vector3.one * 1.15f;
                    if (stage?.AllowsHeight == true) particle.transform.localScale = Vector3.one * 0.8f;
                    particle.transform.position = line.Route.PositionAt(flight.Distance) +
                        (stage?.AllowsHeight == true ? Vector3.zero : Vector3.up * 0.9f);
                    Focus.Apply(particle.GetComponent<Renderer>(), Focus.IncludesLine(line.Id));
                }
            foreach (long id in particles.Keys.Where(id => !active.Contains(id)).ToArray())
            { Destroy(particles[id]); particles.Remove(id); }
        }

        private Vector3[] PathBetween(LineRoute route, double from, double to)
        {
            var points = new List<Vector3> { route.PositionAt(from) + RouteLift };
            double distance = 0;
            for (int i = 1; i < route.Points.Count; i++)
            {
                distance += Vector3.Distance(route.Points[i - 1], route.Points[i]);
                if (distance > from && distance < to) points.Add(route.Points[i] + RouteLift);
            }
            points.Add(route.PositionAt(to) + RouteLift);
            return points.ToArray();
        }
        private Vector3[] OffsetPath(LineRoute route, float offset)
        {
            // The two decorative rails straddle the unchanged transport centerline.
            var points = new Vector3[route.Points.Count];
            for (int i = 0; i < points.Length; i++)
            {
                Vector3 incoming = i > 0 ? (route.Points[i] - route.Points[i - 1]).normalized : Vector3.zero;
                Vector3 outgoing = i + 1 < points.Length ? (route.Points[i + 1] - route.Points[i]).normalized : Vector3.zero;
                Vector3 side = RouteVisualGeometry.Side((incoming + outgoing).normalized);
                points[i] = route.Points[i] + side * offset + RouteLift;
            }
            return points;
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
        public void SetHiddenNode(string? id)
        {
            bool transparent=id!=null;
            if (transparent != transparentBuildings)
            {
                transparentBuildings=transparent;
                foreach (Renderer renderer in buildings)
                {
                    if (renderer == null) continue;
                    Material[] original = opaqueBuildings[renderer];
                    if (transparent)
                    {
                        var replacement = new Material[original.Length];
                        for (int i = 0; i < original.Length; i++)
                        {
                            if (!transparentMaterials.TryGetValue(original[i], out Material material))
                            {
                                material = new Material(original[i]);
                                Color color = material.GetColor("_BaseColor"); color.a = 0.18f;
                                material.SetColor("_BaseColor", color);
                                if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1);
                                material.SetFloat("_ZWrite", 0);
                                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                                material.SetOverrideTag("RenderType", "Transparent");
                                material.SetShaderPassEnabled("DepthOnly", false);
                                material.renderQueue = 3000;
                                materials.Add(material);
                                transparentMaterials.Add(original[i], material);
                            }
                            replacement[i] = material;
                        }
                        renderer.sharedMaterials = replacement;
                    }
                    else renderer.sharedMaterials = original;
                    renderer.shadowCastingMode = transparent ? ShadowCastingMode.Off : originalShadows[renderer];
                }
            }
            foreach (var node in nodeViews)
                foreach (GameObject view in node.Value)
                    if (view != null) view.SetActive(node.Key != id);
        }
        private GameObject Cube(string label, Vector3 position, Vector3 scale, Material material)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = label; cube.transform.SetParent(transform);
            cube.transform.position = position; cube.transform.localScale = scale;
            cube.GetComponent<Renderer>().sharedMaterial = material;
            return cube;
        }
        private LineRenderer Stroke(string label, Vector3[] points, Material material, float width)
        {
            var obj = new GameObject(label);
            obj.transform.SetParent(transform);
            var line = obj.AddComponent<LineRenderer>();
            line.sharedMaterial = material; line.positionCount = points.Length;
            line.SetPositions(points); line.startWidth = width; line.endWidth = width;
            line.numCornerVertices = 2; line.numCapVertices = 2;
            return line;
        }
        private void OnDestroy()
        {
            // Authored scenery outlives this view when its gameplay scope is removed.
            foreach (var pair in opaqueBuildings)
                if (pair.Key != null)
                {
                    pair.Key.sharedMaterials = pair.Value;
                    pair.Key.shadowCastingMode = originalShadows[pair.Key];
                }
            foreach (Renderer renderer in scenery) if (renderer != null) renderer.SetPropertyBlock(null);
            foreach (Material material in materials) if (material != null) Destroy(material);
        }
    }
}
