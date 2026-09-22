#nullable enable

using System.Collections.Generic;
using System.Linq;
using CityFlow.Domain.FlowNetwork;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityFlow.Presentation.UI
{
    public static class OverlayLayout
    {
        public static float Value(VisualElement root, string property, float fallback) =>
            root.customStyle.TryGetValue(new CustomStyleProperty<float>(property), out float value) ? value : fallback;

        public static Vector2 Project(VisualElement root, Camera camera, Vector3 position)
        {
            Vector3 screen = camera.WorldToScreenPoint(position);
            return root.WorldToLocal(RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(screen.x, Screen.height - screen.y)));
        }
        public static Vector2 Anchor(VisualElement root, Camera camera, Vector3 world)
        {
            Rect viewport = Viewport(root, camera);
            Vector2 point = Project(root, camera, world);
            if (camera.WorldToScreenPoint(world).z <= 0) point = viewport.center * 2 - point;
            float gap = Value(root.Q("validation-hud"), "--overlay-gap", 12);
            return new Vector2(Mathf.Clamp(point.x, viewport.xMin + gap, viewport.xMax - gap),
                Mathf.Clamp(point.y, viewport.yMin + gap, viewport.yMax - gap));
        }
        public static Rect Viewport(VisualElement root, Camera camera) => new Rect(
            camera.rect.x * root.layout.width, (1 - camera.rect.yMax) * root.layout.height,
            camera.rect.width * root.layout.width, camera.rect.height * root.layout.height);

        public static List<Rect> Obstacles(VisualElement root, Camera camera, NetworkSnapshot state, VisualElement? exclude = null, bool includeMarkers = true)
        {
            var result = new List<Rect>();
            var labels = root.Q("node-labels");
            if (labels != null && labels.resolvedStyle.display != DisplayStyle.None)
                foreach (var label in labels.Children().Where(e => e.resolvedStyle.display != DisplayStyle.None))
                    Add(label);
            foreach (var element in root.Query(className: "layout-obstacle").ToList())
                if (element != exclude && element.resolvedStyle.display != DisplayStyle.None) Add(element);
            if (includeMarkers)
                foreach (var element in root.Query().ToList().Where(e => e.ClassListContains("arrival-marker") || e.ClassListContains("candidate-marker")))
                    if (element.resolvedStyle.display != DisplayStyle.None) Add(element);
            float clearance = Value(root.Q("validation-hud"), "--node-clearance", 24);
            foreach (var node in state.Nodes)
            {
                Vector3 screen = camera.WorldToScreenPoint(node.Definition.Position + Vector3.up * 1.4f);
                if (screen.z <= 0 || !camera.pixelRect.Contains(screen)) continue;
                Vector2 point = Project(root, camera, node.Definition.Position + Vector3.up * 1.4f);
                result.Add(new Rect(point - Vector2.one * clearance, Vector2.one * clearance * 2));
            }
            return result;

            void Add(VisualElement element)
            {
                Rect rect = element.worldBound;
                if (float.IsNaN(rect.width) || rect.width <= 0 || rect.height <= 0) return;
                result.Add(new Rect(root.WorldToLocal(rect.position), rect.size));
            }
        }

        public static Rect Place(VisualElement element, VisualElement root, Vector2 preferred,
            IReadOnlyList<Rect> obstacles, Rect? area = null)
        {
            float width = element.resolvedStyle.width, height = element.resolvedStyle.height;
            if (float.IsNaN(width) || float.IsNaN(height) || width <= 0 || height <= 0)
            {
                element.style.visibility = Visibility.Hidden;
                return default;
            }
            element.style.visibility = Visibility.Visible;
            float gap = Value(root.Q("validation-hud"), "--overlay-gap", 12);
            Rect region = area ?? new Rect(0, 0, root.layout.width, root.layout.height);
            region = new Rect(region.x + gap, region.y + gap, Mathf.Max(1, region.width - 2 * gap), Mathf.Max(1, region.height - 2 * gap));
            var candidates = new List<Vector2> { preferred, new(preferred.x - width - gap * 2, preferred.y),
                new(preferred.x, preferred.y - height - gap * 2), new(preferred.x - width - gap * 2, preferred.y - height - gap * 2) };
            foreach (Rect obstacle in obstacles)
            {
                candidates.Add(new Vector2(obstacle.xMax + gap, preferred.y));
                candidates.Add(new Vector2(obstacle.xMin - width - gap, preferred.y));
                candidates.Add(new Vector2(preferred.x, obstacle.yMax + gap));
                candidates.Add(new Vector2(preferred.x, obstacle.yMin - height - gap));
            }
            for (float y = region.yMin; y <= Mathf.Max(region.yMin, region.yMax - height); y += height + gap)
                for (float x = region.xMin; x <= Mathf.Max(region.xMin, region.xMax - width); x += width + gap)
                    candidates.Add(new Vector2(x, y));
            candidates.Add(new Vector2(region.xMax - width, region.yMax - height));
            float best = float.PositiveInfinity;
            Rect chosen = default;
            foreach (Vector2 candidate in candidates)
            {
                Vector2 point = new Vector2(Mathf.Clamp(candidate.x, region.xMin, Mathf.Max(region.xMin, region.xMax - width)),
                    Mathf.Clamp(candidate.y, region.yMin, Mathf.Max(region.yMin, region.yMax - height)));
                var bounds = new Rect(point, new Vector2(width, height));
                float score = Vector2.Distance(point, preferred);
                foreach (Rect obstacle in obstacles)
                {
                    float overlap = Mathf.Max(0, Mathf.Min(bounds.xMax, obstacle.xMax) - Mathf.Max(bounds.xMin, obstacle.xMin)) *
                        Mathf.Max(0, Mathf.Min(bounds.yMax, obstacle.yMax) - Mathf.Max(bounds.yMin, obstacle.yMin));
                    if (overlap > 0) score += 100000 + overlap * 100;
                }
                if (score < best) { best = score; chosen = bounds; }
            }
            Vector2 local = element.parent.WorldToLocal(root.LocalToWorld(chosen.position));
            element.style.left = local.x; element.style.top = local.y; element.style.bottom = StyleKeyword.Auto;
            return chosen;
        }
    }
}
