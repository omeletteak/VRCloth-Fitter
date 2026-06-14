using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace VRClothFitter
{
    /// <summary>
    /// Replaces the proxy capsules' fixed default radii with per-capsule radii
    /// measured from the avatar's actual body mesh (docs/ROADMAP.md phase 3,
    /// radius auto-estimation). Bakes the body mesh to world space with the same
    /// convention as <see cref="VRClothMeshCapture"/> and hands the vertices to
    /// the pure <see cref="CapsuleRadiusEstimator"/>. The body mesh comes from
    /// <see cref="VRClothFitter.bodyMesh"/> when set, otherwise it is auto-detected.
    ///
    /// Radius signal must come from the body, not the cloth: a garment only
    /// covers part of the body and its tightest point reflects the garment, not
    /// the body (E2E across こまど/ミルティナ showed the same neck reading 0.018 vs
    /// 0.046 m purely because one wore a tie and the other a swimsuit strap).
    /// </summary>
    public static class VRClothBodyRadiusEstimator
    {
        const int MinSamples = 8;
        const float GateFactor = 2.0f;
        const float MinRadius = 0.005f;
        const float MaxRadius = 0.30f;

        public struct Outcome
        {
            public List<BodyCapsule> capsules;
            public int[] sampleCounts;
            public bool[] estimated;
            public SkinnedMeshRenderer bodyMesh;
        }

        public static Outcome Apply(VRClothFitter fitter, List<BodyCapsule> capsules)
        {
            var outcome = new Outcome { capsules = capsules };
            if (fitter == null || capsules == null || capsules.Count == 0)
            {
                return outcome;
            }

            SkinnedMeshRenderer body = fitter.bodyMesh != null ? fitter.bodyMesh : DetectBodyMesh(fitter);
            outcome.bodyMesh = body;
            if (body == null || body.sharedMesh == null)
            {
                Debug.LogWarning("[VRClothFitter] Body mesh for radius estimation not found — keeping default capsule radii. Assign 'Body Mesh' on the component to override.");
                return outcome;
            }

            Vector3[] bodyVertices = VRClothMeshCapture.BakeWorldVertices(body);
            float percentile = Mathf.Clamp(fitter.radiusPercentile, 0.5f, 1f);
            CapsuleRadiusEstimator.Result est = CapsuleRadiusEstimator.Estimate(
                capsules, bodyVertices, percentile, MinSamples, GateFactor, MinRadius, MaxRadius);

            var updated = new List<BodyCapsule>(capsules.Count);
            int estimatedCount = 0;
            var sb = new StringBuilder();
            sb.AppendLine($"[VRClothFitter] Body radius estimate from '{body.name}' ({bodyVertices.Length} verts), p{Mathf.RoundToInt(percentile * 100f)}:");
            for (int i = 0; i < capsules.Count; i++)
            {
                BodyCapsule c = capsules[i];
                updated.Add(new BodyCapsule(c.start, c.end, est.radii[i], c.label));
                if (est.estimated[i])
                {
                    estimatedCount++;
                }
                string label = string.IsNullOrEmpty(c.label) ? $"capsule{i}" : c.label;
                sb.AppendLine($"  {label,-28} {c.radius:F3} -> {est.radii[i]:F3} m  (n={est.sampleCounts[i]}{(est.estimated[i] ? "" : ", fallback")})");
            }
            sb.Append($"  => {estimatedCount}/{capsules.Count} capsules estimated from the body.");
            Debug.Log(sb.ToString());

            outcome.capsules = updated;
            outcome.sampleCounts = est.sampleCounts;
            outcome.estimated = est.estimated;
            return outcome;
        }

        /// <summary>
        /// Best-guess body mesh: among active skinned meshes under the avatar
        /// that are not part of the cloth being fitted, the one skinned to the
        /// Hips bone with the most vertices. Logged so the user can correct it.
        /// </summary>
        static SkinnedMeshRenderer DetectBodyMesh(VRClothFitter fitter)
        {
            GameObject avatar = fitter.targetAvatar;
            if (avatar == null)
            {
                return null;
            }
            Animator animator = avatar.GetComponent<Animator>();
            Transform hips = (animator != null && animator.isHuman)
                ? animator.GetBoneTransform(HumanBodyBones.Hips)
                : null;
            Transform clothRoot = fitter.clothRoot != null
                ? fitter.clothRoot.transform
                : (fitter.clothToDeform != null ? fitter.clothToDeform.transform : null);

            SkinnedMeshRenderer best = null;
            bool bestOnHips = false;
            int bestVerts = -1;
            foreach (SkinnedMeshRenderer smr in avatar.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (smr.sharedMesh == null || !smr.gameObject.activeInHierarchy || !smr.enabled)
                {
                    continue;
                }
                if (clothRoot != null && IsUnder(smr.transform, clothRoot))
                {
                    continue; // the cloth being fitted, not the body
                }
                bool onHips = hips != null && System.Array.IndexOf(smr.bones, hips) >= 0;
                int verts = smr.sharedMesh.vertexCount;
                bool better = best == null
                    || (onHips && !bestOnHips)
                    || (onHips == bestOnHips && verts > bestVerts);
                if (better)
                {
                    best = smr;
                    bestOnHips = onHips;
                    bestVerts = verts;
                }
            }
            return best;
        }

        static bool IsUnder(Transform t, Transform root)
        {
            for (Transform p = t; p != null; p = p.parent)
            {
                if (p == root)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
