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
        public static void Apply(ClothSnapshot snapshot)
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

            Undo.RecordObject(renderer, "Apply VRCloth Fitting");
            renderer.sharedMesh = copy;
            EditorUtility.SetDirty(renderer);
        }
    }
}
