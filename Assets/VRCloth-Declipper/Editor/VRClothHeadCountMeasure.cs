using System.Text;
using UnityEngine;
using VRClothDeclipper.Core;

namespace VRClothDeclipper
{
    /// <summary>
    /// Measures the target avatar's head-count (頭身) from its body mesh and
    /// logs it. Head-count is the scale-invariant proportion that characterises
    /// a body family (docs/FAMILY_MODEL.md, "頭身=硬い軸") — useful as a first
    /// filter for whether a representative-avatar garment will sit on a
    /// non-supported avatar. Reuses the same body-mesh resolution and world bake
    /// as radius estimation. Reads vertices in memory and emits only scalars to
    /// the log; nothing is serialized (No Cache, docs/INFORMATION_ARCHITECTURE.md).
    /// </summary>
    public static class VRClothHeadCountMeasure
    {
        public static void Measure(VRClothDeclipper fitter)
        {
            if (fitter == null || fitter.targetAvatar == null)
            {
                Debug.LogWarning("[VRClothDeclipper] Head-count: assign a Target Avatar first.");
                return;
            }

            Animator animator = fitter.targetAvatar.GetComponent<Animator>();
            if (animator == null || !animator.isHuman)
            {
                Debug.LogWarning("[VRClothDeclipper] Head-count: target avatar must be Humanoid.");
                return;
            }

            SkinnedMeshRenderer body = fitter.bodyMesh != null
                ? fitter.bodyMesh
                : VRClothBodyRadiusEstimator.ResolveBodyMesh(fitter);
            if (body == null || body.sharedMesh == null)
            {
                Debug.LogWarning("[VRClothDeclipper] Head-count: body mesh not found — assign 'Body Mesh' on the component.");
                return;
            }

            Vector3[] verts = VRClothMeshCapture.BakeWorldVertices(body);
            if (verts.Length == 0)
            {
                Debug.LogWarning($"[VRClothDeclipper] Head-count: body mesh '{body.name}' produced no vertices.");
                return;
            }

            Transform neck = animator.GetBoneTransform(HumanBodyBones.Neck);
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);

            var sb = new StringBuilder();
            sb.AppendLine($"[VRClothDeclipper] Head-count from '{body.name}' ({verts.Length} verts):");

            if (neck != null)
            {
                BodyProportions.HeadCount m = BodyProportions.Measure(verts, neck.position.y);
                sb.AppendLine($"  head-count (Neck ref) = {m.headCount:F2}   (height {m.height:F3} m, head {m.headHeight:F3} m)");
            }
            if (head != null)
            {
                BodyProportions.HeadCount m = BodyProportions.Measure(verts, head.position.y);
                sb.AppendLine($"  head-count (Head ref) = {m.headCount:F2}   (height {m.height:F3} m, head {m.headHeight:F3} m)");
            }
            if (neck == null && head == null)
            {
                sb.AppendLine("  no Neck/Head bone found — cannot estimate head height.");
            }

            sb.Append("  note: mesh-bounds top includes hair/accessories; Neck/Head are bone proxies for the chin. Treat as an estimate to calibrate (±~0.3 head), not a fixed value — docs/DIAGNOSTIC_HONESTY.md §2.");
            Debug.Log(sb.ToString());
        }
    }
}
