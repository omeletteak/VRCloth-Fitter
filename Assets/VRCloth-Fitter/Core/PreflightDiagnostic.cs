using System.Collections.Generic;
using UnityEngine;

namespace VRClothFitter
{
    public enum PreflightVerdict
    {
        /// <summary>Within the designed envelope (docs/DESIGN.md §9).</summary>
        Green,

        /// <summary>Best effort: bulges, texture stretch or proxy shape may show.</summary>
        Yellow,

        /// <summary>
        /// Retargeting-class body difference, out of scope: the pipeline
        /// refuses to apply unless explicitly forced.
        /// </summary>
        Red,
    }

    public struct PreflightReport
    {
        public int vertexCount;

        /// <summary>Vertices within the margin zone — what the solver acts on.</summary>
        public int hitCount;

        /// <summary>Vertices actually below the body surface (margin excluded).</summary>
        public int penetratingCount;

        /// <summary><see cref="penetratingCount"/> / <see cref="vertexCount"/>.</summary>
        public float penetratingRatio;

        /// <summary>Deepest point below the body surface, in meters.</summary>
        public float maxDepth;

        /// <summary>95th-percentile depth below the surface, in meters.</summary>
        public float p95Depth;

        /// <summary>Worst depth relative to the hit capsule's radius.</summary>
        public float maxDepthOverRadius;

        /// <summary>Largest connected penetrating patch / all vertices.</summary>
        public float largestPatchRatio;

        public PreflightVerdict verdict;
    }

    /// <summary>
    /// Judges, before anything is applied, whether the detected penetration
    /// is inside the body-shape-difference envelope this tool supports
    /// (docs/DESIGN.md §9). Depths are measured below the actual body
    /// surface — not the margin surface — so a well-fitting outfit that
    /// merely grazes the margin zone stays green.
    /// </summary>
    public static class PreflightDiagnostic
    {
        // Initial thresholds from the DESIGN.md §9 table; to be calibrated
        // against real avatars during E2E.
        public const float GreenMaxDepth = 0.01f;
        public const float GreenMaxDepthOverRadius = 0.15f;
        public const float GreenMaxPenetratingRatio = 0.10f;
        public const float RedDepth = 0.03f;
        public const float RedPenetratingRatio = 0.30f;

        public static PreflightReport Evaluate(
            Vector3[] positions,
            int[] triangles,
            IReadOnlyList<PenetrationHit> hits,
            IReadOnlyList<BodyCapsule> capsules,
            float margin)
        {
            if (capsules == null)
            {
                return new PreflightReport
                {
                    vertexCount = positions != null ? positions.Length : 0,
                    hitCount = hits != null ? hits.Count : 0,
                    verdict = PreflightVerdict.Green,
                };
            }
            // The closest capsule's radius is the local thickness; depth/radius
            // keeps its capsule meaning.
            return Evaluate(positions, triangles, hits, margin,
                hit => capsules[hit.capsuleIndex].radius);
        }

        /// <summary>
        /// Collider-backend preflight. Depth is measured the same way; the
        /// local thickness used to normalize it comes from the collider
        /// (capsule radius or a nominal mesh thickness, docs/DESIGN.md §9).
        /// </summary>
        public static PreflightReport Evaluate(
            Vector3[] positions,
            int[] triangles,
            IReadOnlyList<PenetrationHit> hits,
            IBodyCollider collider,
            float margin)
        {
            if (collider == null)
            {
                return new PreflightReport
                {
                    vertexCount = positions != null ? positions.Length : 0,
                    hitCount = hits != null ? hits.Count : 0,
                    verdict = PreflightVerdict.Green,
                };
            }
            return Evaluate(positions, triangles, hits, margin,
                hit => collider.LocalThickness(hit.position));
        }

        static PreflightReport Evaluate(
            Vector3[] positions,
            int[] triangles,
            IReadOnlyList<PenetrationHit> hits,
            float margin,
            System.Func<PenetrationHit, float> localThicknessOf)
        {
            var report = new PreflightReport
            {
                vertexCount = positions != null ? positions.Length : 0,
                hitCount = hits != null ? hits.Count : 0,
                verdict = PreflightVerdict.Green,
            };
            if (report.vertexCount == 0 || hits == null)
            {
                return report;
            }

            var depths = new List<float>(hits.Count);
            var penetratingVertices = new HashSet<int>();
            foreach (var hit in hits)
            {
                float surfaceDepth = hit.depth - margin;
                if (surfaceDepth <= 0f)
                {
                    continue; // margin-zone graze, not a body penetration
                }
                depths.Add(surfaceDepth);
                penetratingVertices.Add(hit.vertexIndex);
                report.maxDepth = Mathf.Max(report.maxDepth, surfaceDepth);

                float thickness = localThicknessOf(hit);
                if (thickness > 1e-6f)
                {
                    report.maxDepthOverRadius = Mathf.Max(report.maxDepthOverRadius, surfaceDepth / thickness);
                }
            }

            report.penetratingCount = penetratingVertices.Count;
            report.penetratingRatio = (float)report.penetratingCount / report.vertexCount;
            report.p95Depth = Percentile95(depths);
            report.largestPatchRatio = LargestPatchRatio(positions, triangles, penetratingVertices);
            report.verdict = Judge(report);
            return report;
        }

        static PreflightVerdict Judge(PreflightReport report)
        {
            if (report.maxDepth > RedDepth || report.penetratingRatio > RedPenetratingRatio)
            {
                return PreflightVerdict.Red;
            }
            if (report.maxDepth <= GreenMaxDepth
                && report.maxDepthOverRadius <= GreenMaxDepthOverRadius
                && report.penetratingRatio <= GreenMaxPenetratingRatio)
            {
                return PreflightVerdict.Green;
            }
            return PreflightVerdict.Yellow;
        }

        static float Percentile95(List<float> depths)
        {
            if (depths.Count == 0)
            {
                return 0f;
            }
            depths.Sort();
            int index = Mathf.CeilToInt(0.95f * depths.Count) - 1;
            return depths[Mathf.Clamp(index, 0, depths.Count - 1)];
        }

        /// <summary>
        /// Size of the largest edge-connected component among penetrating
        /// vertices, as a fraction of all vertices. Vertex count stands in
        /// for surface area — adequate for a coarse scope gate.
        /// </summary>
        static float LargestPatchRatio(Vector3[] positions, int[] triangles, HashSet<int> penetratingVertices)
        {
            if (penetratingVertices.Count == 0 || triangles == null)
            {
                return 0f;
            }

            var adjacency = VertexAdjacency.Build(positions, triangles);
            var penetratingReps = new HashSet<int>();
            foreach (int vertex in penetratingVertices)
            {
                penetratingReps.Add(adjacency.RepresentativeOf(vertex));
            }

            int largest = 0;
            var visited = new HashSet<int>();
            var stack = new Stack<int>();
            foreach (int start in penetratingReps)
            {
                if (!visited.Add(start))
                {
                    continue;
                }
                int memberCount = 0;
                stack.Push(start);
                while (stack.Count > 0)
                {
                    int rep = stack.Pop();
                    memberCount += adjacency.MembersOf(rep).Count;
                    foreach (int neighbor in adjacency.NeighborsOf(rep))
                    {
                        if (penetratingReps.Contains(neighbor) && visited.Add(neighbor))
                        {
                            stack.Push(neighbor);
                        }
                    }
                }
                largest = Mathf.Max(largest, memberCount);
            }
            return (float)largest / positions.Length;
        }
    }
}
