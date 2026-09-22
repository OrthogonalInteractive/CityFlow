#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using CityFlow.Domain.Spatial;
using UnityEngine;
namespace CityFlow.Domain.FlowNetwork
{
    public sealed partial class FlowNetwork
    {
        public bool RequestDeletion(int lineId)
        {
            LineState? line=lines.FirstOrDefault(l=>l.Id==lineId);
            if (line == null || line.Status != LineStatus.Running) return false;
            InvalidateSnapshot();
            InvalidateRouting();
            line.Status=LineStatus.DeletePending; CompleteDrainedLines(); return true;
        }
        public bool CancelPending(int lineId)
        {
            LineState? line=lines.FirstOrDefault(l=>l.Id==lineId);
            if (line == null || line.Status == LineStatus.Running) return false;
            InvalidateSnapshot();
            InvalidateRouting();
            line.Status=LineStatus.Running; line.PendingRoute=null; return true;
        }
        public bool RequestRouteChange(int lineId, IReadOnlyList<Vector3> points)
        {
            LineState? line=lines.FirstOrDefault(l=>l.Id==lineId);
            if (line == null || line.Status != LineStatus.Running) return false;
            LineRoute route;
            try { route=new LineRoute(points); } catch(ArgumentException) { return false; }
            if (route.Points[0] != line.Source.Definition.Position || route.Points[route.Points.Count-1] != line.Destination.Definition.Position ||
                !stage.IsRouteWalkable(route,Settings.Clearance)) return false;
            InvalidateSnapshot();
            InvalidateRouting();
            line.PendingRoute=route; line.Status=LineStatus.RouteChangePending; CompleteDrainedLines(); return true;
        }
        private void CompleteDrainedLines()
        {
            foreach(LineState line in lines.Where(l=>l.Status!=LineStatus.Running && l.InFlight.Count==0).ToArray())
            {
                InvalidateRouting();
                if (line.Status==LineStatus.DeletePending)
                { line.Source.Outgoing.Remove(line); line.Destination.Incoming.Remove(line); lines.Remove(line); }
                else if (line.PendingRoute != null)
                { line.Route=line.PendingRoute; line.PendingRoute=null; line.Status=LineStatus.Running; }
            }
        }
    }
}
