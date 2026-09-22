#nullable enable

using System;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Presentation.Rendering;
using UnityEngine.UIElements;

namespace CityFlow.Presentation.UI
{
    public static class BufferGauge
    {
        public static void Refresh(VisualElement gauge, NodeSnapshot node)
        {
            if (!node.BufferCapacity.HasValue) { gauge.Clear(); return; }
            int count = Math.Max(node.BufferCapacity.Value, node.Buffer.Count);
            while (gauge.childCount > count) gauge.RemoveAt(gauge.childCount - 1);
            while (gauge.childCount < count)
            {
                var slot = new Label { pickingMode = PickingMode.Ignore };
                slot.AddToClassList("node-buffer-slot"); gauge.Add(slot);
            }
            for (int i = 0; i < count; i++)
            {
                var slot = (Label)gauge[i];
                bool empty = i >= node.Buffer.Count;
                slot.text = empty ? "" : node.Buffer[i].Color.ToString().Substring(0, 1);
                slot.EnableInClassList("empty", empty);
                slot.style.backgroundColor = empty ? new StyleColor(StyleKeyword.Null) :
                    new StyleColor(ValidationCityView.ColorFor(node.Buffer[i].Color));
            }
        }
    }
}
