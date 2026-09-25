#nullable enable

using System.Collections.Generic;
using UnityEngine;
using CityFlow.Domain.Spatial;

namespace CityFlow.Application.Routing
{
    public interface ILineRoutePlanner
    {
        LineRouteResult Generate(NodeDefinition source, NodeDefinition destination);
        LineRouteResult Validate(NodeDefinition source, NodeDefinition destination, IReadOnlyList<Vector3> points);
    }
}
