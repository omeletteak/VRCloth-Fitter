using System.Text;
using UnityEngine;
using VRClothDeclipper.Core;

namespace VRClothDeclipper
{
    /// <summary>
    /// Measures the target avatar's head-count (頭身) and logs it. Head-count is
    /// the scale-invariant proportion that characterises a body family
    /// (docs/FAMILY_MODEL.md, "頭身=硬い軸") — a first filter for whether a
    /// representative-avatar garment will sit on a non-supported avatar.
    ///
    /// The body is often split into several skinned meshes (body / head / hair —
    /// e.g. after AAO splits an avatar), so no single mesh spans crown-to-sole.
    /// The bounds are therefore unioned across every active skinned mesh on the
    /// avatar EXCEPT the cloth being fitted (a hat or heels in the outfit would
    /// otherwise move the crown/sole). Measuring one mesh that omits the head
    /// collapses the head height and blows the count up (the bug this fixes).
    /// Reads vertices in memory and emits only scalars to the log; nothing is
    /// serialized (No Cache, docs/INFORMATION_ARCHITECTURE.md).
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

            // Union the vertical bounds over every active body mesh except the
            // outfit, so a split body+head+hair still yields the true crown/sole.
            Transform clothRoot = fitter.clothRoot != null ? fitter.clothRoot.transform : null;
            float top = float.NegativeInfinity;
            float bottom = float.PositiveInfinity;
            int meshCount = 0;
            int vertCount = 0;
            foreach (SkinnedMeshRenderer smr in fitter.targetAvatar.GetComponentsInChildren<SkinnedMeshRenderer>(false))
            {
                if (smr.sharedMesh == null)
                {
                    continue;
                }
                if (clothRoot != null && smr.transform.IsChildOf(clothRoot))
                {
                    continue; // part of the outfit being fitted, not the body
                }
                Vector3[] verts = VRClothMeshCapture.BakeWorldVertices(smr);
                for (int i = 0; i < verts.Length; i++)
                {
                    float y = verts[i].y;
                    if (y > top) top = y;
                    if (y < bottom) bottom = y;
                }
                meshCount++;
                vertCount += verts.Length;
            }

            if (meshCount == 0 || vertCount == 0)
            {
                Debug.LogWarning("[VRClothDeclipper] Head-count: no body meshes found on the target avatar (every skinned mesh is under the cloth root or inactive).");
                return;
            }

            Transform neck = animator.GetBoneTransform(HumanBodyBones.Neck);
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);

            var sb = new StringBuilder();
            sb.AppendLine($"[VRClothDeclipper] Head-count over {meshCount} body mesh(es), {vertCount} verts (crown {top:F3} m, sole {bottom:F3} m):");

            if (neck != null)
            {
                BodyProportions.HeadCount m = BodyProportions.Measure(top, bottom, neck.position.y);
                sb.AppendLine($"  head-count (Neck ref) = {m.headCount:F2}   (height {m.height:F3} m, head {m.headHeight:F3} m)");
            }
            if (head != null)
            {
                BodyProportions.HeadCount m = BodyProportions.Measure(top, bottom, head.position.y);
                sb.AppendLine($"  head-count (Head ref) = {m.headCount:F2}   (height {m.height:F3} m, head {m.headHeight:F3} m)");
            }
            if (neck == null && head == null)
            {
                sb.AppendLine("  no Neck/Head bone found — cannot estimate head height.");
            }

            sb.Append("  note: bounds top includes hair/accessories; Neck/Head are bone proxies for the chin. Treat as an estimate to calibrate (±~0.3 head), not a fixed value — docs/DIAGNOSTIC_HONESTY.md §2.");
            Debug.Log(sb.ToString());
        }
    }
}
