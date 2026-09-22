#nullable enable

using System.Linq;
using UnityEngine.UIElements;

namespace CityFlow.Tests.PlayMode
{
    internal static class HudAssertions
    {
        public static string TooltipText(VisualElement root) => string.Join("\n",
            root.Q("node-tooltip").Query<Label>().ToList().Where(label => label.resolvedStyle.display != DisplayStyle.None).Select(label => label.text));
    }
}
