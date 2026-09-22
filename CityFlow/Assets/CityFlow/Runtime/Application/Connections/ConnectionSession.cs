#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using CityFlow.Application.Routing;
using CityFlow.Domain.FlowNetwork;
using CityFlow.Domain.Spatial;
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
        public IReadOnlyList<NodeDefinition> Nodes => network.NodeDefinitions;
        public string? SourceId { get; private set; }
        public bool IsActive => SourceId != null;
        public string? TargetId => preview.Current?.DestinationId;
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
        private LineRoute? undoRoute;
        public bool CanUndoLastConnection => !network.IsGameOver && !IsActive && undoRoute != null &&
            network.Snapshot().Lines.Any(line => line.Id == LastCreatedLineId && line.Status == LineStatus.Running &&
                line.InFlight.Count == 0 && ReferenceEquals(line.Route, undoRoute));
        public bool UndoLastConnection()
        {
            if (!CanUndoLastConnection || !LastCreatedLineId.HasValue) return false;
            if (!network.RequestDeletion(LastCreatedLineId.Value)) return false;
            DismissUndo(); LastCreatedLineId = null; changed.OnNext(Unit.Default);
            return true;
        }
        public void DismissUndo() => undoRoute = null;

        public bool Begin(string sourceId)
        {
            if (network.IsGameOver || IsActive || !network.NodeDefinitions.Any(n => n.Id == sourceId && n.MaxOutgoing > 0)) return false;
            DismissUndo();
            preview.Cancel(); SourceId = sourceId; Filter = DistanceBand.All; LastCreatedLineId = null;
            changed.OnNext(Unit.Default); return true;
        }
        public bool BeginLineEdit(int lineId)
        {
            if (network.IsGameOver || IsActive) return false;
            var line=network.Snapshot().Lines.FirstOrDefault(l=>l.Id==lineId);
            if (line == null || line.Status != LineStatus.Running) return false;
            DismissUndo();
            SourceId=line.SourceId; LastCreatedLineId=null;
            if (!preview.BeginLineEdit(lineId)) { SourceId=null; return false; }
            changed.OnNext(Unit.Default); return true;
        }
        public void SetFilter(DistanceBand band)
        { Filter = band; changed.OnNext(Unit.Default); }
        public IReadOnlyList<ConnectionCandidate> Candidates()
        {
            if (SourceId == null) return Array.Empty<ConnectionCandidate>();
            var nodes = network.Snapshot().Nodes;
            NodeSnapshot source = nodes.Single(n => n.Definition.Id == SourceId);
            return nodes.Where(node => node.Definition.Id != SourceId && node.Definition.Kind != NodeKind.Source).Select(node =>
            {
                Vector3 delta = node.Definition.Position - source.Definition.Position;
                float distance = new Vector2(delta.x,delta.z).magnitude;
                DistanceBand band = distance <= NearLimit ? DistanceBand.Near : distance <= MidLimit ? DistanceBand.Mid : DistanceBand.Far;
                return new ConnectionCandidate(node,distance,band,source,network.CheckConnection(SourceId,node.Definition.Id));
            }).Where(candidate => Filter == DistanceBand.All || candidate.Band == Filter).ToArray();
        }
        public bool SelectTarget(string destinationId)
        {
            if (preview.EditingLineId.HasValue || SourceId == null || destinationId == SourceId ||
                !network.NodeDefinitions.Any(n => n.Id == destinationId && n.Kind != NodeKind.Source)) return false;
            preview.Generate(SourceId,destinationId);
            return true;
        }
        public ConnectionFailure Confirm()
        {
            if (network.IsGameOver || SourceId == null || preview.Current == null || preview.Current.SourceId != SourceId)
                return ConnectionFailure.InvalidRoute;
            bool editing = preview.EditingLineId.HasValue;
            ConnectionFailure failure = preview.TryConfirm(out int? lineId);
            if (failure == ConnectionFailure.None)
            {
                LastCreatedLineId = lineId;
                undoRoute = editing ? null : network.Snapshot().Lines.FirstOrDefault(line => line.Id == lineId)?.Route;
                SourceId = null; changed.OnNext(Unit.Default);
            }
            return failure;
        }
        public void Cancel()
        { SourceId = null; preview.Cancel(); changed.OnNext(Unit.Default); }
        public void Dispose()
        { SourceId = null; previewSubscription.Dispose(); changed.OnCompleted(); changed.Dispose(); }
    }
}
