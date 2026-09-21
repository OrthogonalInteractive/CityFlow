#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using CityFlow.Application.Routing;
using CityFlow.Domain.FlowNetwork;
using R3;
using UnityEngine;

namespace CityFlow.Application.Connections
{
    public sealed class ConnectionSession : IDisposable
    {
        private readonly FlowNetwork network;
        private readonly LinePreviewService preview;
        private readonly Subject<Unit> changed = new();
        private readonly IDisposable previewSubscription;
        public Observable<Unit> Changed => changed;
        public string? SourceId { get; private set; }
        public bool IsActive => SourceId != null;
        public DistanceBand Filter { get; private set; }
        public float NearLimit { get; }
        public float MidLimit { get; }
        public int? LastCreatedLineId { get; private set; }
        public ConnectionSession(FlowNetwork network, LinePreviewService preview, float nearLimit = 30, float midLimit = 70)
        {
            if (!(nearLimit > 0) || !(midLimit > nearLimit) || float.IsInfinity(midLimit))
                throw new ArgumentOutOfRangeException(nameof(nearLimit));
            this.network = network; this.preview = preview; NearLimit = nearLimit; MidLimit = midLimit;
            previewSubscription = preview.Changed.Subscribe(_ => changed.OnNext(Unit.Default));
        }
        public bool Begin(string sourceId)
        {
            if (IsActive || !network.NodeDefinitions.Any(n => n.Id == sourceId)) return false;
            preview.Cancel(); SourceId = sourceId; Filter = DistanceBand.All; LastCreatedLineId = null;
            changed.OnNext(Unit.Default); return true;
        }
        public void SetFilter(DistanceBand band)
        { Filter = band; changed.OnNext(Unit.Default); }
        public IReadOnlyList<ConnectionCandidate> Candidates()
        {
            if (SourceId == null) return Array.Empty<ConnectionCandidate>();
            var nodes = network.Snapshot().Nodes;
            NodeSnapshot source = nodes.Single(n => n.Definition.Id == SourceId);
            return nodes.Select(node =>
            {
                Vector3 delta = node.Definition.Position - source.Definition.Position;
                float distance = new Vector2(delta.x,delta.z).magnitude;
                DistanceBand band = distance <= NearLimit ? DistanceBand.Near : distance <= MidLimit ? DistanceBand.Mid : DistanceBand.Far;
                return new ConnectionCandidate(node,distance,band,source,network.CheckConnection(SourceId,node.Definition.Id));
            }).Where(candidate => Filter == DistanceBand.All || candidate.Band == Filter).ToArray();
        }
        public void SelectTarget(string destinationId)
        {
            if (SourceId == null || !network.NodeDefinitions.Any(n => n.Id == destinationId)) return;
            preview.Generate(SourceId,destinationId);
        }
        public ConnectionFailure Confirm()
        {
            if (SourceId == null || preview.Current == null || preview.Current.SourceId != SourceId)
                return ConnectionFailure.InvalidRoute;
            ConnectionFailure failure = preview.TryConfirm(out int? lineId);
            if (failure == ConnectionFailure.None)
            { LastCreatedLineId = lineId; SourceId = null; changed.OnNext(Unit.Default); }
            return failure;
        }
        public void Cancel()
        { SourceId = null; preview.Cancel(); changed.OnNext(Unit.Default); }
        public void Dispose()
        { SourceId = null; previewSubscription.Dispose(); changed.OnCompleted(); changed.Dispose(); }
    }
}
