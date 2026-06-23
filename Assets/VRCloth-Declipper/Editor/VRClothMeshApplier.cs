using UnityEditor;
using UnityEngine;

namespace VRClothDeclipper
{
    /// <summary>
    /// Non-destructive apply: writes the solved world-space positions into a
    /// duplicate of the renderer's mesh and swaps the renderer over to it.
    /// The original mesh asset is never modified, no asset is written to disk
    /// (No Cache), and the swap is a single Undo step.
    /// </summary>
    public static class VRClothMeshApplier
    {
        /// <summary>
        /// Builds the fitted mesh copy from a solved snapshot, without touching
        /// the renderer (no swap, no Undo, no SetDirty). This is the pure,
        /// side-effect-free core shared by the edit-time <see cref="Apply"/>, the
        /// NDMF build pass (<see cref="VRClothDeclipperPass"/>) and the live
        /// preview (<see cref="VRClothDeclipperPreview"/>), so all three produce
        /// an identical mesh — "edit-time == build-time". The caller owns the
        /// returned mesh and must assign or destroy it.
        /// </summary>
        public static Mesh BuildFittedMesh(ClothSnapshot snapshot)
        {
            var renderer = snapshot.renderer;
            var source = renderer.sharedMesh;

            Mesh copy = Object.Instantiate(source);
            copy.name = source.name + " (VRClothFitted)";

            var weights = source.boneWeights;
            var bindPoses = source.bindposes;
            var bones = renderer.bones;
            var boneToWorld = new Matrix4x4[bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                boneToWorld[i] = bones[i] != null ? bones[i].localToWorldMatrix : Matrix4x4.identity;
            }

            // Write the fit back as a delta on the source's own base vertices
            // rather than replacing them with the baked positions. Replacing
            // would fold shape-key displacement (non-zero under Modular Avatar's
            // Blendshape Sync) into the base, and the renderer would then apply
            // the blendshape a second time at runtime — a double-apply
            // (docs/ROADMAP.md phase 3). bakedWorld is the pre-solve capture,
            // worldVertices the post-solve result; their difference is the
            // correction, added on top of the unchanged base shape.
            copy.vertices = SkinningMath.WorldDeltaToMeshLocal(
                source.vertices, snapshot.bakedWorld, snapshot.worldVertices,
                weights, boneToWorld, bindPoses,
                renderer.transform.worldToLocalMatrix);
            // Normals are kept from the original on purpose: for the small
            // corrections we apply, stale shading reads better than the seam
            // splits RecalculateNormals would introduce.
            copy.RecalculateBounds();
            return copy;
        }

        public static void Apply(ClothSnapshot snapshot)
        {
            var renderer = snapshot.renderer;
            Mesh copy = BuildFittedMesh(snapshot);

            Undo.RecordObject(renderer, "Apply VRCloth Fitting");
            renderer.sharedMesh = copy;
            EditorUtility.SetDirty(renderer);
        }

        /// <summary>
        /// Non-destructive apply of a multi-pose composition: adds a
        /// bind-pose-local delta (from <see cref="MultiPoseComposer"/>) onto the
        /// mesh's base vertices and swaps the renderer to the copy. The delta is
        /// already in mesh-local space — the same space as <c>Mesh.vertices</c> —
        /// because linear-blend skinning is linear, so it adds directly. And
        /// because it is a correction <em>increment</em> rather than a baked
        /// replacement, blendshapes still apply exactly once at runtime, the same
        /// double-apply guard as <see cref="Apply"/>
        /// (docs/MULTIPOSE_COMPOSITION.md §1, docs/ROADMAP.md phase 3).
        /// </summary>
        public static void ApplyBindLocalDelta(SkinnedMeshRenderer renderer, Vector3[] bindLocalDelta)
        {
            if (renderer == null || renderer.sharedMesh == null)
            {
                return;
            }
            var source = renderer.sharedMesh;
            var baseVertices = source.vertices;
            if (bindLocalDelta == null || bindLocalDelta.Length != baseVertices.Length)
            {
                Debug.LogWarning($"[VRClothDeclipper] ApplyBindLocalDelta: delta length ({(bindLocalDelta == null ? 0 : bindLocalDelta.Length)}) "
                    + $"does not match vertex count ({baseVertices.Length}) on '{renderer.name}' — skipped.");
                return;
            }

            Mesh copy = Object.Instantiate(source);
            copy.name = source.name + " (VRClothFitted)";
            var fitted = new Vector3[baseVertices.Length];
            for (int v = 0; v < baseVertices.Length; v++)
            {
                fitted[v] = baseVertices[v] + bindLocalDelta[v];
            }
            copy.vertices = fitted;
            // Normals kept from the original on purpose, same as Apply.
            copy.RecalculateBounds();

            Undo.RecordObject(renderer, "Apply VRCloth Multi-Pose Fitting");
            renderer.sharedMesh = copy;
            EditorUtility.SetDirty(renderer);
        }
    }
}
