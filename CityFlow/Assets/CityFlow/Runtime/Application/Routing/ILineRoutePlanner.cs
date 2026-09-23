#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace CityFlow.Application.Routing
{
    public interface ILineRoutePlanner
    {
        LineRouteResult Generate(Vector3 start, Vector3 end);
        LineRouteResult Validate(IReadOnlyList<Vector3> points);
    }
}
