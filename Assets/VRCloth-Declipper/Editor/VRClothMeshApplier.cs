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

            copy.vertices = SkinningMath.WorldToMeshLocal(
                snapshot.worldVertices, weights, boneToWorld, bindPoses,
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
