using System.Collections.Generic;
using UnityEngine;

namespace VRClothFitter
{
    /// <summary>
    /// The fitting core. Keeps the original geometry untouched and builds a
    /// per-vertex displacement field: push-out writes SDF-gradient
    /// displacements into the field, Laplacian smoothing blends the field
    /// (never the positions, so cloth detail survives), and the
    /// smooth → re-detect → re-push cycle repeats a few times, always ending
    /// on a push so the final state sits on or above the margin surface.
    /// The result, original + displacement, is composed into the caller's
    /// position array; nothing is persisted. See docs/DESIGN.md §6.
    /// </summary>
    public static class PenetrationSolver
    {
        public struct Result
        {
            /// <summary>Penetrating vertices before the first pass.</summary>
            public int initialHitCount;

            /// <summary>Smooth → re-detect → re-push cycles executed.</summary>
            public int passes;

            /// <summary>
            /// Vertices still meaningfully penetrating at the end (measured
            /// with a small tolerance below the margin surface). Expected 0.
            /// </summary>
            public int finalHitCount;
        }

        // MVP tuning, exposed as parameters only for tests. Defaults follow
        // the design: 2-3 rings, a few iterations, 2-3 smooth/re-push cycles.
        public static Result Solve(
            Vector3[] positions,
            int[] triangles,
            IReadOnlyList<BodyCapsule> capsules,
            float margin,
            float lambda = 0.5f,
            int smoothingIterations = 2,
            int rings = 2,
            int maxPasses = 3)
        {
            if (capsules == null || capsules.Count == 0)
            {
                return new Result();
            }
            return Solve(positions, triangles, new CapsuleBodyCollider(capsules), margin,
                lambda, smoothingIterations, rings, maxPasses);
        }

        /// <summary>
        /// Collider-backend solve. The capsule overload routes here through
        /// <see cref="CapsuleBodyCollider"/>, so this is the single
        /// implementation; only the body representation differs (bone capsules
        /// or a mesh SDF, docs/DESIGN.md §6).
        /// </summary>
        public static Result Solve(
            Vector3[] positions,
            int[] triangles,
            IBodyCollider collider,
            float margin,
            float lambda = 0.5f,
            int smoothingIterations = 2,
            int rings = 2,
            int maxPasses = 3)
        {
            var result = new Result();
            if (positions == null || positions.Length == 0 || collider == null)
            {
                return result;
            }

            var hits = PenetrationDetection.Scan(positions, collider, margin);
            result.initialHitCount = hits.Count;
            if (hits.Count == 0)
            {
                return result;
            }

            // From here on, `positions` serves as the scratch buffer for
            // original + displacement; the untouched copy is the reference.
            var originals = (Vector3[])positions.Clone();
            var displacements = new Vector3[originals.Length];
            var adjacency = VertexAdjacency.Build(originals, triangles);
            var seeds = new HashSet<int>();

            PenetrationPushOut.Apply(originals, displacements, hits, collider, margin);
            AddSeeds(seeds, hits);

            while (result.passes < maxPasses)
            {
                result.passes++;
                var region = LaplacianSmoothing.ExpandRegion(adjacency, seeds, rings);
                LaplacianSmoothing.Smooth(displacements, adjacency, region, lambda, smoothingIterations);

                Compose(originals, displacements, positions);
                hits = PenetrationDetection.Scan(positions, collider, margin);
                if (hits.Count == 0)
                {
                    break;
                }

                // Smoothing sank these below the surface again; push them
                // back out so every cycle (and the solve) ends on a push.
                PenetrationPushOut.Apply(originals, displacements, hits, collider, margin);
                AddSeeds(seeds, hits);
            }

            Compose(originals, displacements, positions);
            result.finalHitCount = PenetrationDetection.Scan(positions, collider, margin - 1e-4f).Count;
            return result;
        }

        static void AddSeeds(HashSet<int> seeds, List<PenetrationHit> hits)
        {
            foreach (var hit in hits)
            {
                seeds.Add(hit.vertexIndex);
            }
        }

        static void Compose(Vector3[] originals, Vector3[] displacements, Vector3[] target)
        {
            for (int v = 0; v < target.Length; v++)
            {
                target[v] = originals[v] + displacements[v];
            }
        }
    }
}
