using System.Collections.Generic;
using UnityEngine;

namespace VRClothDeclipper
{
    /// <summary>
    /// Push-out step: accumulates, into a per-vertex displacement field, the
    /// motion that puts each penetrating vertex on its closest capsule's
    /// margin surface, along the capsule SDF gradient. Vertices are never
    /// moved directly — the solver composes original + displacement, which
    /// lets the smoothing stage operate on the displacement field without
    /// touching the original geometry.
    /// </summary>
    public static class PenetrationPushOut
    {
        /// <summary>
        /// For every hit vertex, rewrites <paramref name="displacements"/> so
        /// that original + displacement sits <paramref name="margin"/> above
        /// the hit capsule's surface. The push starts from the vertex's
        /// current displaced position (not the position recorded in the hit),
        /// so the same hit list can drive a re-push after smoothing changed
        /// the field. Entries for non-hit vertices are left untouched.
        /// </summary>
        public static void Apply(
            IReadOnlyList<Vector3> originals,
            Vector3[] displacements,
            IReadOnlyList<PenetrationHit> hits,
            IReadOnlyList<BodyCapsule> capsules,
            float margin)
        {
            if (originals == null || displacements == null || hits == null || capsules == null)
            {
                return;
            }

            foreach (var hit in hits)
            {
                Vector3 current = originals[hit.vertexIndex] + displacements[hit.vertexIndex];
                Vector3 target = capsules[hit.capsuleIndex].PushOut(current, margin);
                displacements[hit.vertexIndex] = target - originals[hit.vertexIndex];
            }
        }

        /// <summary>
        /// Collider-backend push-out: same accumulation, but the target comes
        /// from the body collider's SDF gradient at the vertex's current
        /// displaced position, so no per-hit capsule index is needed.
        /// </summary>
        public static void Apply(
            IReadOnlyList<Vector3> originals,
            Vector3[] displacements,
            IReadOnlyList<PenetrationHit> hits,
            IBodyCollider collider,
            float margin)
        {
            if (originals == null || displacements == null || hits == null || collider == null)
            {
                return;
            }

            foreach (var hit in hits)
            {
                Vector3 current = originals[hit.vertexIndex] + displacements[hit.vertexIndex];
                Vector3 target = collider.PushOut(current, margin);
                displacements[hit.vertexIndex] = target - originals[hit.vertexIndex];
            }
        }
    }
}
