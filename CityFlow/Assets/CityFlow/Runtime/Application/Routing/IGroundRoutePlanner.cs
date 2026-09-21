#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace CityFlow.Application.Routing
{
    public interface IGroundRoutePlanner
    {
        GroundRouteResult Generate(Vector3 start, Vector3 end);
        GroundRouteResult Validate(IReadOnlyList<Vector3> points);
    }
}
