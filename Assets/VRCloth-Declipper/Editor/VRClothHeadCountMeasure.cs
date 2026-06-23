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
        /// <summary>
        /// Head-count measurement of one avatar: the unioned crown/sole bounds and
        /// the head-count under both bone-proxy chin references (Neck and Head),
        /// either of which may be absent. <see cref="ok"/> is false when the avatar
        /// is missing/non-Humanoid or has no body mesh. Scalars only — No Cache.
        /// Shared by the inspector log (<see cref="Measure"/>) and the body
        /// measurement dump (<see cref="VRClothMeasurementDump"/>) so both read the
        /// same numbers.
        /// </summary>
        public struct Result
        {
            public bool ok;
            public int meshCount;
            public int vertCount;
            public float top;     // crown (includes hair/accessories)
            public float bottom;  // sole
            public bool hasNeck;
            public bool hasHead;
            public BodyProportions.HeadCount neckRef; // valid when hasNeck
            public BodyProportions.HeadCount headRef; // valid when hasHead

            public float Height => top - bottom;
        }

        /// <summary>
        /// Computes the head-count Result for the fitter's target avatar without
        /// logging. Bounds are unioned over every active body mesh except the cloth
        /// being fitted, so a split body/head/hair still yields the true crown/sole
        /// (measuring one mesh that omits the head collapses the head height and
        /// blows the count up). Reads vertices in memory and keeps only scalars.
        /// </summary>
        public static Result Compute(VRClothDeclipper fitter)
        {
            var result = new Result();
            if (fitter == null || fitter.targetAvatar == null)
            {
                return result;
            }
            Animator animator = fitter.targetAvatar.GetComponent<Animator>();
            if (animator == null || !animator.isHuman)
            {
                return result;
            }

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
                return result;
            }

            Transform neck = animator.GetBoneTransform(HumanBodyBones.Neck);
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);

            result.ok = true;
            result.meshCount = meshCount;
            result.vertCount = vertCount;
            result.top = top;
            result.bottom = bottom;
            if (neck != null)
            {
                result.hasNeck = true;
                result.neckRef = BodyProportions.Measure(top, bottom, neck.position.y);
            }
            if (head != null)
            {
                result.hasHead = true;
                result.headRef = BodyProportions.Measure(top, bottom, head.position.y);
            }
            return result;
        }

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

            Result r = Compute(fitter);
            if (!r.ok)
            {
                Debug.LogWarning("[VRClothDeclipper] Head-count: no body meshes found on the target avatar (every skinned mesh is under the cloth root or inactive).");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"[VRClothDeclipper] Head-count over {r.meshCount} body mesh(es), {r.vertCount} verts (crown {r.top:F3} m, sole {r.bottom:F3} m):");
            if (r.hasNeck)
            {
                sb.AppendLine($"  head-count (Neck ref) = {r.neckRef.headCount:F2}   (height {r.neckRef.height:F3} m, head {r.neckRef.headHeight:F3} m)");
            }
            if (r.hasHead)
            {
                sb.AppendLine($"  head-count (Head ref) = {r.headRef.headCount:F2}   (height {r.headRef.height:F3} m, head {r.headRef.headHeight:F3} m)");
            }
            if (!r.hasNeck && !r.hasHead)
            {
                sb.AppendLine("  no Neck/Head bone found — cannot estimate head height.");
            }
            sb.Append("  note: bounds top includes hair/accessories; Neck/Head are bone proxies for the chin. Treat as an estimate to calibrate (±~0.3 head), not a fixed value — docs/DIAGNOSTIC_HONESTY.md §2.");
            Debug.Log(sb.ToString());
        }
    }
}
