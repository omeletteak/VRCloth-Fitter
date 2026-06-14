using UnityEngine;

namespace VRClothFitter
{
    /// <summary>
    /// Builds a <see cref="MeshSdfCollider"/> from the avatar's body mesh in
    /// memory at fit time (docs/DESIGN.md §6). The body mesh is baked to world
    /// space with the same convention as <see cref="VRClothMeshCapture"/> and
    /// its triangles are read from the shared mesh; nothing is serialized, so
    /// this stays compatible with No Cache. The body renderer is the one
    /// assigned on the component, otherwise auto-detected the same way radius
    /// estimation resolves it.
    /// </summary>
    public static class VRClothBodySdfBuilder
    {
        public static MeshSdfCollider Build(VRClothFitter fitter)
        {
            if (fitter == null)
            {
                return null;
            }

            SkinnedMeshRenderer body = fitter.bodyMesh != null
                ? fitter.bodyMesh
                : VRClothBodyRadiusEstimator.ResolveBodyMesh(fitter);
            if (body == null || body.sharedMesh == null)
            {
                Debug.LogWarning("[VRClothFitter] Mesh-SDF collider: body mesh not found — assign 'Body Mesh' on the component, or turn off 'Use Mesh SDF Collider' to fall back to capsules.");
                return null;
            }

            // BakeWorldVertices and sharedMesh.triangles share the mesh's vertex
            // order, so the triangle indices line up with the baked positions.
            Vector3[] vertices = VRClothMeshCapture.BakeWorldVertices(body);
            int[] triangles = body.sharedMesh.triangles;
            var collider = new MeshSdfCollider(vertices, triangles);
            if (!collider.IsValid)
            {
                Debug.LogWarning($"[VRClothFitter] Mesh-SDF collider: body mesh '{body.name}' has no triangles — falling back to capsules.");
                return null;
            }

            Debug.Log($"[VRClothFitter] Mesh-SDF collider built from '{body.name}' ({vertices.Length} verts, {triangles.Length / 3} tris).");
            return collider;
        }
    }
}
