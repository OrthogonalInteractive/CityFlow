#nullable enable

using UnityEngine;
using UnityEngine.UIElements;

namespace CityFlow.Tests.PlayMode
{
    internal static class UiPointer
    {
        public static void Click(Button button)
        {
            var position = button.worldBound.center;
            using (var down = PointerDownEvent.GetPooled(new Event { type = EventType.MouseDown, button = 0, mousePosition = position }))
                button.SendEvent(down);
            using (var up = PointerUpEvent.GetPooled(new Event { type = EventType.MouseUp, button = 0, mousePosition = position }))
                button.SendEvent(up);
        }
    }
}
