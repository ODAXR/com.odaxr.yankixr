#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace ODAXR.YankiXR.Tool
{
    // Pure bake-time graph operations; no Unity physics or runtime allocations here.
    internal static class YankiPropagation
    {
        internal struct Edge
        {
            internal int Target;
            internal float Distance;

            internal Edge(int target, float distance)
            {
                Target = target;
                Distance = distance;
            }
        }

        internal static float[] ShortestPaths(List<Edge>[] graph, int source)
        {
            var distances = new float[graph.Length];
            for (int i = 0; i < distances.Length; i++) distances[i] = float.PositiveInfinity;
            distances[source] = 0f;

            var queue = new SortedSet<KeyValuePair<float, int>>(
                Comparer<KeyValuePair<float, int>>.Create((a, b) =>
                {
                    int order = a.Key.CompareTo(b.Key);
                    return order != 0 ? order : a.Value.CompareTo(b.Value);
                }));
            queue.Add(new KeyValuePair<float, int>(0f, source));
            while (queue.Count > 0)
            {
                var current = queue.Min;
                queue.Remove(current);
                foreach (var edge in graph[current.Value])
                {
                    float candidate = current.Key + edge.Distance;
                    if (candidate >= distances[edge.Target]) continue;
                    queue.Remove(new KeyValuePair<float, int>(distances[edge.Target], edge.Target));
                    distances[edge.Target] = candidate;
                    queue.Add(new KeyValuePair<float, int>(candidate, edge.Target));
                }
            }
            return distances;
        }

        internal static float Combine(float directOcclusion, float directDistance,
            float pathDistance, float detourLossPerUnit)
        {
            if (float.IsPositiveInfinity(pathDistance)) return directOcclusion;
            float extraDistance = Math.Max(0f, pathDistance - directDistance);
            float pathOcclusion = (float)(1.0 - Math.Exp(-Math.Max(0f, detourLossPerUnit) * extraDistance));
            // Keep the strongest available route, without boosting an unobstructed source.
            return Math.Min(directOcclusion, pathOcclusion);
        }
    }
}
#endif
