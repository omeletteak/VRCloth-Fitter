using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace VRClothDeclipper
{
    /// <summary>
    /// Replaces the proxy capsules' fixed default radii with per-capsule radii
    /// measured from the avatar's actual body mesh (docs/ROADMAP.md phase 3,
    /// radius auto-estimation). Bakes the body mesh to world space with the same
    /// convention as <see cref="VRClothMeshCapture"/> and hands the vertices to
    /// the pure <see cref="CapsuleRadiusEstimator"/>. The body mesh comes from
    /// <see cref="VRClothDeclipper.bodyMesh"/> when set, otherwise it is auto-detected.
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

            /// <summary>First resolved body mesh, for logging/back-compat.</summary>
            public SkinnedMeshRenderer bodyMesh;

            /// <summary>
            /// Every body-part mesh the radii were measured from. More than one on
            /// a split body (torso/head/hair as separate meshes); aggregating them
            /// is what stops a split body resolving to a single part (see
            /// <see cref="ResolveBodyMeshes"/>).
            /// </summary>
            public List<SkinnedMeshRenderer> bodyMeshes;
        }

        public static Outcome Apply(VRClothDeclipper fitter, List<BodyCapsule> capsules)
        {
            var outcome = new Outcome { capsules = capsules };
            if (fitter == null || capsules == null || capsules.Count == 0)
            {
                return outcome;
            }

            // A split body (torso/head/hair as separate meshes) has no single
            // "body" mesh; measure from every body part so a capsule near the
            // torso isn't left to fall back just because the torso lives in a
            // different renderer than the one auto-detect happened to pick.
            List<SkinnedMeshRenderer> bodies = fitter.bodyMesh != null
                ? new List<SkinnedMeshRenderer> { fitter.bodyMesh }
                : ResolveBodyMeshes(fitter);
            outcome.bodyMeshes = bodies;
            outcome.bodyMesh = bodies.Count > 0 ? bodies[0] : null;

            var bodyVertices = new List<Vector3>();
            foreach (var b in bodies)
            {
                if (b == null || b.sharedMesh == null) continue;
                bodyVertices.AddRange(VRClothMeshCapture.BakeWorldVertices(b));
            }
            if (bodyVertices.Count == 0)
            {
                Debug.LogWarning("[VRClothDeclipper] Body mesh for radius estimation not found — keeping default capsule radii. Assign 'Body Mesh' on the component to override.");
                return outcome;
            }

            float percentile = Mathf.Clamp(fitter.radiusPercentile, 0.5f, 1f);
            CapsuleRadiusEstimator.Result est = CapsuleRadiusEstimator.Estimate(
                capsules, bodyVertices, percentile, MinSamples, GateFactor, MinRadius, MaxRadius);

            var updated = new List<BodyCapsule>(capsules.Count);
            int estimatedCount = 0;
            var sb = new StringBuilder();
            sb.AppendLine($"[VRClothDeclipper] Body radius estimate from {DescribeBodies(bodies)} ({bodyVertices.Count} verts), p{Mathf.RoundToInt(percentile * 100f)}:");
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
        public static SkinnedMeshRenderer ResolveBodyMesh(VRClothDeclipper fitter)
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

        /// <summary>
        /// Per-capsule radii from an arbitrary set of skinned meshes — the body,
        /// or a <em>garment</em> for finished-dimension 採寸 (docs/MEASUREMENT_SPEC.md
        /// §4: the garment is skinned to the same skeleton, so the nearest garment
        /// vertices to each capsule axis give the garment's inner radius). Bakes each
        /// mesh to world space (same convention as the body path) and runs the pure
        /// <see cref="CapsuleRadiusEstimator"/>. Capsules the meshes don't cover keep
        /// their fallback (estimated=false) — for a garment that marks which capsules
        /// it spans (a top covers torso/arms, not legs).
        /// </summary>
        public static CapsuleRadiusEstimator.Result EstimateFromMeshes(
            IReadOnlyList<BodyCapsule> capsules, IReadOnlyList<SkinnedMeshRenderer> meshes, float percentile)
        {
            var verts = new List<Vector3>();
            if (meshes != null)
            {
                foreach (var m in meshes)
                {
                    if (m != null && m.sharedMesh != null)
                    {
                        verts.AddRange(VRClothMeshCapture.BakeWorldVertices(m));
                    }
                }
            }
            percentile = Mathf.Clamp(percentile, 0.5f, 1f);
            return CapsuleRadiusEstimator.Estimate(
                capsules, verts, percentile, MinSamples, GateFactor, MinRadius, MaxRadius);
        }

        /// <summary>
        /// Best-guess body mesh <em>set</em>: every active skinned mesh under the
        /// avatar that is not part of the cloth being fitted and is skinned to the
        /// Hips bone — i.e. all parts of a split body (torso/head/hair authored as
        /// separate meshes), not just the largest single one. Aggregating them is
        /// what keeps a split body (e.g. the YOYOGI MORI "YM Body" standard) from
        /// resolving to one part — in the worst case the hair — which leaves the
        /// collider with no surface over the torso and legs and produces
        /// false-green preflights. Falls back to all non-cloth candidates when
        /// nothing is Hips-skinned (non-humanoid rigs). Logged by the callers so
        /// the user can correct it via the Body Mesh field.
        /// </summary>
        public static List<SkinnedMeshRenderer> ResolveBodyMeshes(VRClothDeclipper fitter)
        {
            var candidates = new List<SkinnedMeshRenderer>();
            var onHips = new List<SkinnedMeshRenderer>();
            GameObject avatar = fitter != null ? fitter.targetAvatar : null;
            if (avatar == null)
            {
                return candidates;
            }
            Animator animator = avatar.GetComponent<Animator>();
            Transform hips = (animator != null && animator.isHuman)
                ? animator.GetBoneTransform(HumanBodyBones.Hips)
                : null;
            Transform clothRoot = fitter.clothRoot != null
                ? fitter.clothRoot.transform
                : (fitter.clothToDeform != null ? fitter.clothToDeform.transform : null);

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
                candidates.Add(smr);
                if (hips != null && System.Array.IndexOf(smr.bones, hips) >= 0)
                {
                    onHips.Add(smr);
                }
            }
            // Prefer the Hips-skinned meshes (the body skeleton); fall back to all
            // non-cloth candidates only when nothing is Hips-skinned.
            return onHips.Count > 0 ? onHips : candidates;
        }

        static string DescribeBodies(List<SkinnedMeshRenderer> bodies)
        {
            if (bodies == null || bodies.Count == 0)
            {
                return "(none)";
            }
            if (bodies.Count == 1)
            {
                return $"'{bodies[0].name}'";
            }
            var names = new List<string>(bodies.Count);
            foreach (var b in bodies)
            {
                names.Add(b != null ? b.name : "(null)");
            }
            return $"{bodies.Count} meshes ('" + string.Join("', '", names) + "')";
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
