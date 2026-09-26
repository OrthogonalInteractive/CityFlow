#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CityFlow.Presentation.Rendering
{
    public sealed class AuthoredCityScenery : MonoBehaviour
    {
        [SerializeField] private GameObject[] environmentRoots = Array.Empty<GameObject>();
        [SerializeField] private GameObject[] transparentRoots = Array.Empty<GameObject>();
        [SerializeField] private Vector3 overviewFocus;
        public Vector3 OverviewFocus => overviewFocus;
        public IEnumerable<Renderer> EnvironmentRenderers => environmentRoots.Where(root => root != null)
            .SelectMany(root => root.GetComponentsInChildren<Renderer>(true)).Distinct();
        public IEnumerable<Renderer> TransparentRenderers => transparentRoots.Where(root => root != null)
            .SelectMany(root => root.GetComponentsInChildren<Renderer>(true)).Distinct();

        public void Configure(GameObject[] environment, GameObject[] buildings, Vector3 focus)
        {
            environmentRoots = environment ?? throw new ArgumentNullException(nameof(environment));
            transparentRoots = buildings ?? throw new ArgumentNullException(nameof(buildings));
            overviewFocus = focus;
        }
    }
}
