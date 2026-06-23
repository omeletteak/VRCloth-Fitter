using System.Collections.Generic;
using UnityEngine;

namespace VRClothDeclipper
{
    /// <summary>
    /// Builds a <see cref="MeshSdfCollider"/> from the avatar's body mesh in
    /// memory at fit time (docs/DESIGN.md §6). The body mesh is baked to world
    /// space with the same convention as <see cref="VRClothMeshCapture"/> and
    /// its triangles are read from the shared mesh; nothing is serialized, so
    /// this stays compatible with No Cache. The body renderer is the one
    /// assigned on the component, otherwise auto-detected the same way radius
    /// estimation resolves it. On a split body (body/head/hair as separate
    /// meshes) this resolves the torso body, not the head — head/face-mounted
    /// cloth is out of scope by design (docs/DESIGN.md §9), so a green there only
    /// means "not targeted", not "no penetration". This is intentional, not a bug.
    /// </summary>
    public static class VRClothBodySdfBuilder
    {
        public static MeshSdfCollider Build(VRClothDeclipper fitter)
        {
            if (fitter == null)
            {
                return null;
            }

            // A split body (torso/head/hair as separate meshes) has no single
            // "body" mesh; combine every body part into one collider so the SDF
            // covers the whole body. Resolving to a single part — in the worst
            // case the hair — leaves the torso and legs with no surface, so
            // garments there read as non-penetrating (a false green, §9).
            List<SkinnedMeshRenderer> bodies = fitter.bodyMesh != null
                ? new List<SkinnedMeshRenderer> { fitter.bodyMesh }
                : VRClothBodyRadiusEstimator.ResolveBodyMeshes(fitter);

            // BakeWorldVertices and sharedMesh.triangles share each mesh's vertex
            // order, so per mesh the triangle indices line up with the baked
            // positions; concatenating shifts each part's indices by the running
            // vertex offset.
            var worldVertices = new List<Vector3>();
            var triangles = new List<int>();
            var used = new List<string>();
            foreach (var body in bodies)
            {
                if (body == null || body.sharedMesh == null) continue;
                Vector3[] bv = VRClothMeshCapture.BakeWorldVertices(body);
                int[] bt = body.sharedMesh.triangles;
                if (bv.Length == 0 || bt.Length == 0) continue;
                int offset = worldVertices.Count;
                worldVertices.AddRange(bv);
                for (int i = 0; i < bt.Length; i++) triangles.Add(bt[i] + offset);
                used.Add(body.name);
            }

            if (worldVertices.Count == 0 || triangles.Count == 0)
            {
                Debug.LogWarning("[VRClothDeclipper] Mesh-SDF collider: body mesh not found — assign 'Body Mesh' on the component, or turn off 'Use Mesh SDF Collider' to fall back to capsules.");
                return null;
            }

            var collider = new MeshSdfCollider(worldVertices.ToArray(), triangles.ToArray());
            if (!collider.IsValid)
            {
                Debug.LogWarning($"[VRClothDeclipper] Mesh-SDF collider: body mesh '{string.Join(", ", used)}' has no triangles — falling back to capsules.");
                return null;
            }

            Debug.Log($"[VRClothDeclipper] Mesh-SDF collider built from {used.Count} mesh(es) ({worldVertices.Count} verts, {triangles.Count / 3} tris): {string.Join(", ", used)}.");
            return collider;
        }
    }
}
