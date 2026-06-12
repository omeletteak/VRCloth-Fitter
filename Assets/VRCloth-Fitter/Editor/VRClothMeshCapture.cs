using System.Collections.Generic;
using UnityEngine;

namespace VRClothFitter
{
    /// <summary>
    /// A cloth renderer's current-pose geometry in world space. Captured at
    /// pipeline start, consumed by the later stages (detection, push-out,
    /// smoothing) and finally written back to a mesh copy. Lives only in
    /// memory for the duration of one pipeline run — never serialized
    /// (No Cache principle).
    /// </summary>
    public class ClothSnapshot
    {
        public SkinnedMeshRenderer renderer;

        /// <summary>Current-pose vertex positions in world space.</summary>
        public Vector3[] worldVertices;

        /// <summary>
        /// Triangle index list of the baked mesh, kept so the smoothing stage
        /// can build vertex adjacency without re-baking.
        /// </summary>
        public int[] triangles;

        /// <summary>
        /// Vertices of this renderer found inside the body, with indices into
        /// <see cref="worldVertices"/>. Null until the detection stage runs.
        /// </summary>
        public List<PenetrationHit> hits;

        public int VertexCount => worldVertices != null ? worldVertices.Length : 0;
    }

    public static class VRClothMeshCapture
    {
        /// <summary>
        /// Captures every active SkinnedMeshRenderer under
        /// <paramref name="clothRoot"/> in its current pose, with vertices
        /// converted to world space.
        /// </summary>
        public static List<ClothSnapshot> Capture(GameObject clothRoot)
        {
            var snapshots = new List<ClothSnapshot>();
            if (clothRoot == null)
            {
                return snapshots;
            }

            foreach (var renderer in clothRoot.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (renderer.sharedMesh == null || !renderer.gameObject.activeInHierarchy || !renderer.enabled)
                {
                    continue;
                }
                snapshots.Add(CaptureRenderer(renderer));
            }
            return snapshots;
        }

        static ClothSnapshot CaptureRenderer(SkinnedMeshRenderer renderer)
        {
            // BakeMesh(useScale: false) leaves every scale effect baked into
            // the vertices and divides out only the renderer's rotation and
            // translation, so a rigid TRS(position, rotation, 1) — always
            // invertible, even for degenerate renderer scales — reaches world
            // space. (useScale: true would instead require the full
            // localToWorldMatrix; verified empirically on 2022.3.)
            var baked = new Mesh();
            try
            {
                renderer.BakeMesh(baked, false);

                Vector3[] vertices = baked.vertices;
                Matrix4x4 toWorld = Matrix4x4.TRS(
                    renderer.transform.position, renderer.transform.rotation, Vector3.one);
                for (int i = 0; i < vertices.Length; i++)
                {
                    vertices[i] = toWorld.MultiplyPoint3x4(vertices[i]);
                }

                return new ClothSnapshot
                {
                    renderer = renderer,
                    worldVertices = vertices,
                    triangles = baked.triangles,
                };
            }
            finally
            {
                Object.DestroyImmediate(baked);
            }
        }
    }
}
