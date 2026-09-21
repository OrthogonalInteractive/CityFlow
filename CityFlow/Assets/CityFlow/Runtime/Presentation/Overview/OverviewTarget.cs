#nullable enable

using System;

namespace CityFlow.Presentation.Overview
{
    public readonly struct OverviewTarget : IEquatable<OverviewTarget>
    {
        public string? NodeId { get; }
        public int? LineId { get; }
        public bool IsEmpty => NodeId == null && LineId == null;
        private OverviewTarget(string? nodeId, int? lineId) { NodeId = nodeId; LineId = lineId; }
        public static OverviewTarget Node(string id) => new OverviewTarget(id, null);
        public static OverviewTarget Line(int id) => new OverviewTarget(null, id);
        public bool Equals(OverviewTarget other) => NodeId == other.NodeId && LineId == other.LineId;
        public override bool Equals(object? obj) => obj is OverviewTarget other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(NodeId, LineId);
    }
}
