#nullable enable

using System;

namespace CityFlow.Presentation.Overview
{
    public sealed class OverviewDetailText
    {
        public string Title { get; }
        public string Primary { get; }
        public string Metrics { get; }
        public string Colors { get; }
        public string Status { get; }
        public string Connections { get; }
        public string Activity { get; }
        public string Warning { get; }
        public OverviewDetailText(string title, string primary = "", string metrics = "", string colors = "",
            string status = "", string connections = "", string activity = "", string warning = "")
        {
            Title = title; Primary = primary; Metrics = metrics; Colors = colors;
            Status = status; Connections = connections; Activity = activity; Warning = warning;
        }
        public override string ToString() => string.Join("\n", new[] { Title, Primary, Colors, Metrics, Status, Activity, Warning, Connections });
    }
}
