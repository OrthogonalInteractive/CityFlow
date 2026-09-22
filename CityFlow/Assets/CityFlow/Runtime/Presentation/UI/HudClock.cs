#nullable enable

using System;

namespace CityFlow.Presentation.UI
{
    public static class HudClock
    {
        public static string Format(double seconds)
        {
            long whole = (long)Math.Max(0, seconds);
            return $"{whole / 60}:{whole % 60:00}";
        }
    }
}
