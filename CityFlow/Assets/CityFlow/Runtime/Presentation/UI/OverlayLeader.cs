#nullable enable

using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityFlow.Presentation.UI
{
    public sealed class OverlayLeader : IDisposable
    {
        private readonly VisualElement element;
        private Vector2 start, end;
        public OverlayLeader(VisualElement parent, string name)
        {
            element = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            element.AddToClassList("overlay-leader"); parent.Add(element); element.SendToBack();
            element.generateVisualContent += Draw;
        }
        public void Hide() => element.style.display = DisplayStyle.None;
        public void Show(VisualElement root, Vector2 target, Rect panel)
        {
            if (panel.width <= 0 || panel.height <= 0) { Hide(); return; }
            start = element.WorldToLocal(root.LocalToWorld(target));
            end = element.WorldToLocal(root.LocalToWorld(new Vector2(Mathf.Clamp(target.x, panel.xMin, panel.xMax),
                Mathf.Clamp(target.y, panel.yMin, panel.yMax))));
            element.style.display = DisplayStyle.Flex;
            element.MarkDirtyRepaint();
        }
        private void Draw(MeshGenerationContext context)
        {
            var painter = context.painter2D;
            painter.strokeColor = new Color(0.7f, 0.78f, 0.86f, 0.75f); painter.lineWidth = 1.3f;
            painter.BeginPath(); painter.MoveTo(start); painter.LineTo(end); painter.Stroke();
        }
        public void Dispose()
        {
            element.generateVisualContent -= Draw; element.RemoveFromHierarchy();
        }
    }
}
