using System.Collections.Generic;
using UnityEngine;

namespace VRClothDeclipper
{
    /// <summary>
    /// Linear-blend skinning math used to write deformed world-space
    /// positions back into a mesh's bind-pose local space.
    /// </summary>
    public static class SkinningMath
    {
        /// <summary>
        /// Per-vertex skinning matrix: the weighted sum of
        /// boneToWorld[i] * bindPoses[i] over the vertex's bone weights.
        /// Maps bind-pose local positions to world space for the current pose.
        /// </summary>
        public static Matrix4x4 BlendedSkinMatrix(BoneWeight weight, Matrix4x4[] boneToWorld, Matrix4x4[] bindPoses)
        {
            var blended = Matrix4x4.zero;
            AddScaled(ref blended, weight.boneIndex0, weight.weight0, boneToWorld, bindPoses);
            AddScaled(ref blended, weight.boneIndex1, weight.weight1, boneToWorld, bindPoses);
            AddScaled(ref blended, weight.boneIndex2, weight.weight2, boneToWorld, bindPoses);
            AddScaled(ref blended, weight.boneIndex3, weight.weight3, boneToWorld, bindPoses);
            return blended;
        }

        static void AddScaled(ref Matrix4x4 target, int boneIndex, float weight, Matrix4x4[] boneToWorld, Matrix4x4[] bindPoses)
        {
            if (weight <= 0f || boneIndex < 0 || boneIndex >= boneToWorld.Length || boneIndex >= bindPoses.Length)
            {
                return;
            }
            Matrix4x4 skin = boneToWorld[boneIndex] * bindPoses[boneIndex];
            for (int i = 0; i < 16; i++)
            {
                target[i] += skin[i] * weight;
            }
        }

        /// <summary>
        /// Inverse-skins world-space positions back to mesh-local (bind-pose)
        /// space, the coordinate system of <c>Mesh.vertices</c>. Vertices
        /// without usable weights fall back to
        /// <paramref name="rendererWorldToLocal"/>.
        /// </summary>
        public static Vector3[] WorldToMeshLocal(
            IReadOnlyList<Vector3> worldPositions,
            BoneWeight[] weights,
            Matrix4x4[] boneToWorld,
            Matrix4x4[] bindPoses,
            Matrix4x4 rendererWorldToLocal)
        {
            int count = worldPositions != null ? worldPositions.Count : 0;
            var local = new Vector3[count];
            bool hasWeights = weights != null && weights.Length == count
                && boneToWorld != null && bindPoses != null;

            for (int v = 0; v < count; v++)
            {
                if (hasWeights)
                {
                    Matrix4x4 skin = BlendedSkinMatrix(weights[v], boneToWorld, bindPoses);
                    // Total weight ~0 leaves a zero matrix with no inverse.
                    if (skin.determinant != 0f)
                    {
                        local[v] = skin.inverse.MultiplyPoint3x4(worldPositions[v]);
                        continue;
                    }
                }
                local[v] = rendererWorldToLocal.MultiplyPoint3x4(worldPositions[v]);
            }
            return local;
        }

        /// <summary>
        /// Writes a fitting correction back as a <em>delta</em> on the mesh's
        /// own base vertices instead of replacing them. For each vertex the
        /// world-space correction (<paramref name="fittedWorld"/> minus
        /// <paramref name="originalWorld"/>) is inverse-skinned into mesh-local
        /// space and added to <paramref name="baseVertices"/>.
        ///
        /// This is what keeps blendshapes correct. Replacing a vertex with the
        /// inverse-skinned <i>baked</i> position folds the current shape-key
        /// displacement (driven non-zero by Modular Avatar's Blendshape Sync)
        /// into the base, and the renderer then adds the blendshape again at
        /// runtime — a double-apply. Adding only the delta leaves the base
        /// shape-key-free, so each blendshape contributes exactly once. Both
        /// worlds are inverse-skinned through the same per-vertex matrix (or the
        /// same fallback), so the subtraction cancels the shape-key part and
        /// leaves the pure correction. See docs/ROADMAP.md phase 3.
        /// </summary>
        public static Vector3[] WorldDeltaToMeshLocal(
            IReadOnlyList<Vector3> baseVertices,
            IReadOnlyList<Vector3> originalWorld,
            IReadOnlyList<Vector3> fittedWorld,
            BoneWeight[] weights,
            Matrix4x4[] boneToWorld,
            Matrix4x4[] bindPoses,
            Matrix4x4 rendererWorldToLocal)
        {
            int count = baseVertices != null ? baseVertices.Count : 0;
            var local = new Vector3[count];
            // Without a matching before/after pair there is no delta to add;
            // fall back to the untouched base vertices.
            if (originalWorld == null || fittedWorld == null
                || originalWorld.Count != count || fittedWorld.Count != count)
            {
                for (int v = 0; v < count; v++)
                {
                    local[v] = baseVertices[v];
                }
                return local;
            }

            var localOriginal = WorldToMeshLocal(originalWorld, weights, boneToWorld, bindPoses, rendererWorldToLocal);
            var localFitted = WorldToMeshLocal(fittedWorld, weights, boneToWorld, bindPoses, rendererWorldToLocal);
            for (int v = 0; v < count; v++)
            {
                local[v] = baseVertices[v] + (localFitted[v] - localOriginal[v]);
            }
            return local;
        }
    }
}
