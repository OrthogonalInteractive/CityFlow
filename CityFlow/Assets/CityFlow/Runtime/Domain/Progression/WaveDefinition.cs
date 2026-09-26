#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using CityFlow.Domain.Spatial;
using UnityEngine;
namespace CityFlow.Domain.Progression
{
    public sealed class WaveDefinition
    {
        public double StartSeconds { get; }
        public double IntervalScale { get; }
        public IReadOnlyList<NodeDefinition> Additions { get; }
        public Rect? ExpandedArea { get; }
        public WaveDefinition(double startSeconds, double intervalScale, IEnumerable<NodeDefinition> additions, Rect? expandedArea = null)
        {
            if(double.IsNaN(startSeconds)||double.IsInfinity(startSeconds)||startSeconds<=0||
                double.IsNaN(intervalScale)||double.IsInfinity(intervalScale)||intervalScale<=0) throw new ArgumentOutOfRangeException(nameof(startSeconds));
            StartSeconds=startSeconds; IntervalScale=intervalScale; Additions=Array.AsReadOnly(additions.ToArray());
            ExpandedArea = expandedArea;
        }
    }
}
