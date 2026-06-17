using System.Collections.Generic;
using UnityEngine;

namespace VRClothDeclipper
{
    /// <summary>
    /// Estimates each proxy capsule's radius from the avatar's actual body
    /// surface, replacing the fixed defaults baked into
    /// <see cref="VRClothProxyGenerator"/>.
    ///
    /// Every body-surface vertex is attributed to the nearest capsule axis; a
    /// capsule's radius becomes a percentile of the axis distances of the
    /// vertices attributed to it. Pure geometry over baked world-space vertices
    /// — it keeps nothing (No Cache, docs/DESIGN.md §5) and has no editor
    /// dependency, so it is unit testable like the rest of Core.
    ///
    /// A vertex farther than <paramref name="gateFactor"/> times a capsule's
    /// fallback radius is ignored, so body regions a capsule does not cover
    /// (hands, feet, the top of the head — where the bone chains stop) cannot
    /// inflate a neighbour. Capsules with fewer than
    /// <paramref name="minSamples"/> attributed vertices keep their fallback.
    /// </summary>
    public static class CapsuleRadiusEstimator
    {
        public struct Result
        {
            /// <summary>Radius per capsule: estimated where possible, else fallback.</summary>
            public float[] radii;

            /// <summary>Body vertices attributed to each capsule.</summary>
            public int[] sampleCounts;

            /// <summary>True where the radius came from samples, false where it fell back.</summary>
            public bool[] estimated;
        }

        /// <param name="percentile">0..1 axis-distance percentile used as the radius.</param>
        /// <param name="minSamples">Capsules with fewer attributed vertices keep their fallback radius.</param>
        /// <param name="gateFactor">Vertices beyond this multiple of the fallback radius are ignored.</param>
        /// <param name="minRadius">Lower clamp on an estimated radius, in meters.</param>
        /// <param name="maxRadius">Upper clamp on an estimated radius, in meters.</param>
        public static Result Estimate(
            IReadOnlyList<BodyCapsule> capsules,
            IReadOnlyList<Vector3> bodyVertices,
            float percentile,
            int minSamples,
            float gateFactor,
            float minRadius,
            float maxRadius)
        {
            int n = capsules != null ? capsules.Count : 0;
            var result = new Result
            {
                radii = new float[n],
                sampleCounts = new int[n],
                estimated = new bool[n],
            };
            for (int i = 0; i < n; i++)
            {
                result.radii[i] = capsules[i].radius; // fallback until a sample says otherwise
            }
            if (n == 0 || bodyVertices == null || bodyVertices.Count == 0)
            {
                return result;
            }

            var buckets = new List<float>[n];
            for (int i = 0; i < n; i++)
            {
                buckets[i] = new List<float>();
            }

            for (int v = 0; v < bodyVertices.Count; v++)
            {
                Vector3 p = bodyVertices[v];
                int best = -1;
                float bestDist = float.MaxValue;
                for (int i = 0; i < n; i++)
                {
                    float d = Vector3.Distance(p, capsules[i].ClosestPointOnAxis(p));
                    if (d < bestDist)
                    {
                        bestDist = d;
                        best = i;
                    }
                }
                if (best < 0)
                {
                    continue;
                }
                if (bestDist > gateFactor * capsules[best].radius)
                {
                    continue; // a body region this capsule does not cover
                }
                buckets[best].Add(bestDist);
            }

            for (int i = 0; i < n; i++)
            {
                result.sampleCounts[i] = buckets[i].Count;
                if (buckets[i].Count >= minSamples)
                {
                    float r = Percentile(buckets[i], percentile);
                    result.radii[i] = Mathf.Clamp(r, minRadius, maxRadius);
                    result.estimated[i] = true;
                }
            }
            return result;
        }

        static float Percentile(List<float> values, float p)
        {
            if (values.Count == 0)
            {
                return 0f;
            }
            values.Sort();
            int index = Mathf.CeilToInt(Mathf.Clamp01(p) * values.Count) - 1;
            return values[Mathf.Clamp(index, 0, values.Count - 1)];
        }
    }
}
