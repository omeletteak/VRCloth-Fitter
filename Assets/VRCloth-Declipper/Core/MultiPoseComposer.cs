using System.Collections.Generic;
using UnityEngine;

namespace VRClothDeclipper
{
    /// <summary>
    /// One representative pose's view of a cloth renderer: the baked cloth world
    /// positions at this pose, the per-vertex skin matrix that maps the mesh's
    /// bind-pose-local space to this pose's world space, and the body collider at
    /// this pose. All in-memory for one run (No Cache, docs/DESIGN.md §5).
    /// </summary>
    public struct PoseCapture
    {
        public Vector3[] originalWorld;
        public Matrix4x4[] skinMatrices;
        public IBodyCollider collider;
    }

    /// <summary>
    /// Composes corrections from several representative poses into a single
    /// static bind-pose-local delta, so one mesh edit is (near) penetration-free
    /// at every supplied pose. The composition model is documented in
    /// docs/MULTIPOSE_COMPOSITION.md.
    ///
    /// The problem is over-constrained — one 3-DOF delta per vertex must satisfy
    /// N posed constraints — so this does <em>not</em> solve the poses
    /// independently and average them. It sweeps the poses with a shared,
    /// accumulating delta: at each pose it skins the current delta into that
    /// pose's world space, runs the existing per-pose solver, and folds the
    /// additional outward push back into the delta through the inverse skin
    /// matrix. Push-out only ever moves vertices outward, so the accumulation is
    /// monotone and bounded by the union of every pose's worst-case push; a few
    /// sweeps drive every pose to clear (Gauss-Seidel over the pose constraints).
    /// Linear-blend skinning is linear, so a bind-local delta re-skins to each
    /// pose exactly: world = originalWorld + skin · delta.
    /// </summary>
    public static class MultiPoseComposer
    {
        public struct Result
        {
            /// <summary>Pose sweeps executed (one sweep = all poses once).</summary>
            public int rounds;

            /// <summary>
            /// Vertices still penetrating at the final delta, summed over poses
            /// (measured with the same small tolerance as the solver). Expected 0
            /// when the poses are jointly satisfiable.
            /// </summary>
            public int remainingPenetrating;
        }

        /// <summary>
        /// Accumulates into <paramref name="bindLocalDelta"/> (length =
        /// vertex count, typically zero-initialized) the composed correction.
        /// Reuses <see cref="PenetrationSolver.Solve"/> per pose, so the solver
        /// tuning is shared; only the outer sweep is new.
        /// </summary>
        public static Result Compose(
            Vector3[] bindLocalDelta,
            int[] triangles,
            IReadOnlyList<PoseCapture> poses,
            float margin,
            int maxRounds = 4,
            float lambda = 0.5f,
            int smoothingIterations = 2,
            int rings = 2,
            int maxPasses = 3)
        {
            var result = new Result();
            if (bindLocalDelta == null || poses == null || poses.Count == 0)
            {
                return result;
            }
            int n = bindLocalDelta.Length;
            var worldPos = new Vector3[n];
            var before = new Vector3[n];

            for (int round = 0; round < maxRounds; round++)
            {
                result.rounds++;
                bool anyChange = false;
                foreach (var pose in poses)
                {
                    if (!IsUsable(pose, n))
                    {
                        continue;
                    }
                    // Skin the current accumulated delta into this pose's world.
                    for (int v = 0; v < n; v++)
                    {
                        worldPos[v] = pose.originalWorld[v] + SkinVector(pose.skinMatrices[v], bindLocalDelta[v]);
                        before[v] = worldPos[v];
                    }

                    PenetrationSolver.Solve(worldPos, triangles, pose.collider, margin,
                        lambda, smoothingIterations, rings, maxPasses);

                    // Fold the additional world push back into the bind delta.
                    for (int v = 0; v < n; v++)
                    {
                        Vector3 inc = worldPos[v] - before[v];
                        if (inc.sqrMagnitude <= 0f)
                        {
                            continue;
                        }
                        bindLocalDelta[v] += InverseSkinVector(pose.skinMatrices[v], inc);
                        anyChange = true;
                    }
                }
                if (!anyChange)
                {
                    break; // every pose already clear: converged
                }
            }

            foreach (var pose in poses)
            {
                if (!IsUsable(pose, n))
                {
                    continue;
                }
                for (int v = 0; v < n; v++)
                {
                    Vector3 w = pose.originalWorld[v] + SkinVector(pose.skinMatrices[v], bindLocalDelta[v]);
                    if (pose.collider.SignedDistance(w) < margin - 1e-4f)
                    {
                        result.remainingPenetrating++;
                    }
                }
            }
            return result;
        }

        static bool IsUsable(PoseCapture pose, int n)
        {
            return pose.originalWorld != null && pose.originalWorld.Length == n
                && pose.skinMatrices != null && pose.skinMatrices.Length == n
                && pose.collider != null;
        }

        // A bind-local displacement maps to world by the skin matrix's linear
        // part (translation drops out of a difference of two skinned points).
        // A weightless vertex leaves a non-invertible matrix; fall back to
        // identity so the delta is neither lost on the way out nor on the way in.
        static Vector3 SkinVector(Matrix4x4 m, Vector3 v)
        {
            return m.determinant != 0f ? m.MultiplyVector(v) : v;
        }

        static Vector3 InverseSkinVector(Matrix4x4 m, Vector3 v)
        {
            return m.determinant != 0f ? m.inverse.MultiplyVector(v) : v;
        }
    }
}
