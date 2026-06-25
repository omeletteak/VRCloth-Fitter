using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace VRClothDeclipper
{
    /// <summary>
    /// Writes one avatar's body measurement (採寸表 row) — the predict layer of
    /// docs/ECOSYSTEM_VISION.md §5. Reuses what is already computed: the proxy
    /// capsules' per-capsule radii from <see cref="VRClothBodyRadiusEstimator"/>
    /// (radius ≈ girth/2π, §5 "実装の合流") plus head-count
    /// (<see cref="VRClothHeadCountMeasure"/>). Run it per avatar to build a table
    /// that powers family matching ("closest representative avatar", docs/
    /// FAMILY_MODEL.md §7).
    ///
    /// Records ONLY scalars and bone/mesh names — radii, lengths, head-count,
    /// coverage. A handful of girth scalars cannot reconstruct the body shape, so
    /// this stays within No Cache (docs/INFORMATION_ARCHITECTURE.md, ECOSYSTEM_VISION
    /// §5 権利安全性): the same information granularity a real avatar product page
    /// already lists. The file is a local, gitignored dev artifact (sibling of the
    /// run log), never distributed.
    /// </summary>
    public static class VRClothMeasurementDump
    {
        public const string FileName = "vrcloth-body-measurements.jsonl";
        public const string GarmentFileName = "vrcloth-garment-measurements.jsonl";

        /// <summary>Project-root path (sibling of Assets/) of the 採寸表 file.</summary>
        public static string FilePath()
        {
            return Path.Combine(Directory.GetParent(Application.dataPath).FullName, FileName);
        }

        /// <summary>Project-root path of the garment 仕上がり寸法 file (docs/MEASUREMENT_SPEC.md §4).</summary>
        public static string GarmentFilePath()
        {
            return Path.Combine(Directory.GetParent(Application.dataPath).FullName, GarmentFileName);
        }

        /// <summary>
        /// Measures the fitter's target avatar and returns one JSONL row (no disk
        /// write), logging a per-avatar summary. Returns null after logging when
        /// the avatar can't be measured. Shared by the inspector button
        /// (<see cref="Dump"/>) and the headless CLI
        /// (<see cref="VRClothMeasureCli"/>).
        /// </summary>
        public static string Measure(VRClothDeclipper fitter)
        {
            if (fitter == null || fitter.targetAvatar == null)
            {
                Debug.LogWarning("[VRClothDeclipper] 採寸: assign a Target Avatar first.");
                return null;
            }

            List<BodyCapsule> capsules = VRClothProxyGenerator.Generate(fitter.targetAvatar);
            if (capsules == null || capsules.Count == 0)
            {
                Debug.LogWarning("[VRClothDeclipper] 採寸: could not generate proxy capsules (target avatar must be Humanoid).");
                return null;
            }

            VRClothBodyRadiusEstimator.Outcome outcome = VRClothBodyRadiusEstimator.Apply(fitter, capsules);
            capsules = outcome.capsules;
            float coverage = BodyModelConfidence.Coverage(outcome.estimated);
            VRClothHeadCountMeasure.Result hc = VRClothHeadCountMeasure.Compute(fitter);
            string meshHash = ComputeBodyHash(outcome.bodyMeshes);

            var dto = BuildDto(fitter, capsules, outcome, coverage, hc, meshHash);
            Debug.Log($"[VRClothDeclipper] 採寸 '{dto.avatar}': "
                + $"head-count {dto.headCount_neckRef:F2} (Neck) / {dto.headCount_headRef:F2} (Head), "
                + $"height {dto.height_m:F3} m, {dto.capsuleCount} capsules, coverage {coverage:P0}.");
            return JsonUtility.ToJson(dto, false);
        }

        /// <summary>
        /// Measures the fitter's target avatar and appends one JSONL row to
        /// <see cref="FileName"/> (the incremental, button-driven path).
        /// </summary>
        public static void Dump(VRClothDeclipper fitter)
        {
            string json = Measure(fitter);
            if (json == null)
            {
                return;
            }
            try
            {
                string path = FilePath();
                File.AppendAllText(path, json + "\n");
                Debug.Log($"[VRClothDeclipper] 採寸 appended -> {path}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[VRClothDeclipper] 採寸: could not write measurement: {e.Message}");
            }
        }

        /// <summary>
        /// Measures the garment's finished inner dimensions (docs/MEASUREMENT_SPEC.md
        /// §4) and returns one JSONL row, or null after logging. The garment is
        /// skinned to the avatar skeleton, so the same proxy capsules apply; the
        /// radius estimator pointed at the garment meshes gives the inner radius per
        /// capsule. Only capsules the garment spans are estimated (a top → torso/arms);
        /// `coverage` is that fraction. Requires the garment worn on its design
        /// avatar (Target Avatar = skeleton, clothRoot = garment).
        /// </summary>
        public static string MeasureGarment(VRClothDeclipper fitter)
        {
            if (fitter == null || fitter.targetAvatar == null)
            {
                Debug.LogWarning("[VRClothDeclipper] 衣装採寸: assign a Target Avatar (for the skeleton) first.");
                return null;
            }
            GameObject clothRoot = fitter.clothRoot != null ? fitter.clothRoot
                : (fitter.clothToDeform != null ? fitter.clothToDeform.gameObject : null);
            if (clothRoot == null)
            {
                Debug.LogWarning("[VRClothDeclipper] 衣装採寸: no cloth root/renderer to measure.");
                return null;
            }
            List<BodyCapsule> capsules = VRClothProxyGenerator.Generate(fitter.targetAvatar);
            if (capsules == null || capsules.Count == 0)
            {
                Debug.LogWarning("[VRClothDeclipper] 衣装採寸: could not generate proxy capsules (Target Avatar must be Humanoid).");
                return null;
            }
            var garmentMeshes = new List<SkinnedMeshRenderer>();
            foreach (var smr in clothRoot.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (smr.sharedMesh != null && smr.gameObject.activeInHierarchy && smr.enabled)
                {
                    garmentMeshes.Add(smr);
                }
            }
            if (garmentMeshes.Count == 0)
            {
                Debug.LogWarning("[VRClothDeclipper] 衣装採寸: no active SkinnedMeshRenderer under the cloth root.");
                return null;
            }

            // The garment is skinned to ITS OWN Armature, so its baked vertices sit
            // in a different bone space than the body capsules — co-locating the
            // roots is not enough (the design body itself fails the sanity check).
            // Re-bind the garment renderers' bones to the body's same-named bones
            // (the core of MA Merge Armature, done just for the measurement) so the
            // garment meshes skin on the BODY skeleton and line up with the capsules.
            // Non-destructive: the caller's scene/garment is restored afterwards.
            var savedBones = new Transform[garmentMeshes.Count][];
            var savedRoots = new Transform[garmentMeshes.Count];
            var savedMeshes = new Mesh[garmentMeshes.Count];
            for (int i = 0; i < garmentMeshes.Count; i++)
            {
                savedBones[i] = garmentMeshes[i].bones;
                savedRoots[i] = garmentMeshes[i].rootBone;
                savedMeshes[i] = garmentMeshes[i].sharedMesh;
            }
            CapsuleRadiusEstimator.Result est;
            try
            {
                int remapped = RetargetGarmentToBody(garmentMeshes, fitter.targetAvatar, out int totalBones);
                Debug.Log($"[VRClothDeclipper] 衣装採寸: re-bound {remapped}/{totalBones} garment bone(s) to '{fitter.targetAvatar.name}' skeleton (MA-merge core for alignment).");
                est = VRClothBodyRadiusEstimator.EstimateFromMeshes(
                    capsules, garmentMeshes, fitter.radiusPercentile);
            }
            finally
            {
                for (int i = 0; i < garmentMeshes.Count; i++)
                {
                    garmentMeshes[i].bones = savedBones[i];
                    garmentMeshes[i].rootBone = savedRoots[i];
                    // RetargetGarmentToBody may have swapped in a bind-pose-corrected mesh
                    // clone; restore the original shared asset and free the clone (No Cache).
                    Mesh cur = garmentMeshes[i].sharedMesh;
                    if (cur != savedMeshes[i])
                    {
                        garmentMeshes[i].sharedMesh = savedMeshes[i];
                        if (cur != null) UnityEngine.Object.DestroyImmediate(cur);
                    }
                }
            }
            string meshHash = ComputeBodyHash(garmentMeshes);

            int n = capsules.Count;
            int covered = 0;
            var capDtos = new CapsuleMeasureDto[n];
            for (int i = 0; i < n; i++)
            {
                BodyCapsule c = capsules[i];
                bool estimated = est.estimated != null && est.estimated[i];
                if (estimated) covered++;
                capDtos[i] = new CapsuleMeasureDto
                {
                    label = c.label ?? "",
                    radius_m = est.radii[i], // garment INNER radius at this capsule
                    length_m = Vector3.Distance(c.start, c.end),
                    sampleCount = (est.sampleCounts != null && i < est.sampleCounts.Length) ? est.sampleCounts[i] : 0,
                    estimated = estimated,
                };
            }

            var dto = new GarmentMeasurementDto
            {
                timestamp = DateTime.Now.ToString("o"),
                garment = clothRoot.name,
                onAvatar = fitter.targetAvatar.name,
                coverage = (float)covered / n,
                meshHash = meshHash,
                conditions = new MeasurementConditions { radiusPercentile = fitter.radiusPercentile },
                capsuleCount = n,
                capsules = capDtos,
            };
            Debug.Log($"[VRClothDeclipper] 衣装採寸 '{dto.garment}' on '{dto.onAvatar}': "
                + $"{covered}/{n} capsules spanned (coverage {dto.coverage:P0}).");
            return JsonUtility.ToJson(dto, false);
        }

        /// <summary>Measures the garment and appends one JSONL row to <see cref="GarmentFileName"/>.</summary>
        public static void DumpGarment(VRClothDeclipper fitter)
        {
            string json = MeasureGarment(fitter);
            if (json == null)
            {
                return;
            }
            try
            {
                string path = GarmentFilePath();
                File.AppendAllText(path, json + "\n");
                Debug.Log($"[VRClothDeclipper] 衣装採寸 appended -> {path}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[VRClothDeclipper] 衣装採寸: could not write measurement: {e.Message}");
            }
        }

        /// <summary>
        /// Version key of the measured body (docs/MEASUREMENT_SPEC.md §6): the
        /// order-independent combination of each body-part mesh's content hash.
        /// Uses bind-pose <c>sharedMesh</c> so it is stable across scene pose/scale.
        /// </summary>
        static string ComputeBodyHash(List<SkinnedMeshRenderer> bodies)
        {
            if (bodies == null || bodies.Count == 0)
            {
                return "";
            }
            var hashes = new List<string>(bodies.Count);
            foreach (var b in bodies)
            {
                if (b == null || b.sharedMesh == null) continue;
                hashes.Add(MeshFingerprint.Compute(b.sharedMesh.vertices, b.sharedMesh.triangles));
            }
            return MeshFingerprint.Combine(hashes);
        }

        const float MaxBoneSnap = 0.05f; // 5 cm — body bones are cm-apart; accessories sit farther

        /// <summary>
        /// Re-binds each garment renderer's bones onto the body skeleton — the core of
        /// MA Merge Armature, applied only to align the garment meshes with the body
        /// capsules for measurement (docs/MEASUREMENT_SPEC.md §4). Three passes per bone:
        /// (1) exact same-name; (2) <see cref="BoneNameThesaurus"/> → Humanoid bone →
        /// the body's actual bone via <c>Animator.GetBoneTransform</c> (handles a garment
        /// and body that use different naming conventions, e.g. "Foot_L" vs the body's
        /// own name — this is what MA's HeuristicBoneMapper does); (3) nearest body bone
        /// by world position (garment is co-located on the body), covering accessories
        /// and fingers not in the thesaurus. Bones matching none keep their original.
        /// Returns the count re-bound; <paramref name="totalBones"/> is the total seen.
        /// Mutates the passed renderers — callers back up/restore for non-destructive use.
        /// </summary>
        public static int RetargetGarmentToBody(
            IReadOnlyList<SkinnedMeshRenderer> garmentMeshes, GameObject bodyAvatar, out int totalBones)
        {
            totalBones = 0;
            int remapped = 0;
            if (bodyAvatar == null)
            {
                return remapped;
            }
            Transform[] bodyBones = bodyAvatar.GetComponentsInChildren<Transform>(true);
            Animator avatarAnimator = bodyAvatar.GetComponentInChildren<Animator>();
            if (avatarAnimator != null && !avatarAnimator.isHuman)
            {
                avatarAnimator = null; // the thesaurus pass needs a Humanoid Animator
            }
            var byName = new Dictionary<string, Transform>();
            foreach (Transform t in bodyBones)
            {
                if (!byName.ContainsKey(t.name))
                {
                    byName[t.name] = t; // first wins; names may repeat on non-Humanoid rigs
                }
            }
            foreach (var smr in garmentMeshes)
            {
                if (smr == null)
                {
                    continue;
                }
                Transform[] src = smr.bones;
                var dst = new Transform[src.Length];

                // Re-point each bone AND re-derive the garment mesh's bind pose onto the
                // body bone, so the mesh skins WITHOUT distortion when the garment bone and
                // the body bone differ in position/rotation/scale (their world transforms
                // are absorbed). Same math as MA's MeshRetargeter.RetargetBones — MIT,
                // (c) bd_ (https://github.com/bdunderscore/modular-avatar). Re-pointing
                // bones alone (leaving the garment's own bind pose) is what made the cloth
                // shrink/clip the body, so every body read minClear-negative.
                Mesh mesh = smr.sharedMesh;
                Matrix4x4[] srcBP = mesh != null ? mesh.bindposes : null;
                Matrix4x4[] dstBP = (srcBP != null && srcBP.Length > 0) ? (Matrix4x4[])srcBP.Clone() : null;
                bool bindposeChanged = false;

                for (int i = 0; i < src.Length; i++)
                {
                    totalBones++;
                    dst[i] = ResolveBodyBone(src[i], byName, bodyBones, avatarAnimator, ref remapped);

                    if (dstBP != null && i < srcBP.Length
                        && src[i] != null && dst[i] != null && dst[i] != src[i])
                    {
                        dstBP[i] = dst[i].worldToLocalMatrix * src[i].localToWorldMatrix * srcBP[i];
                        bindposeChanged = true;
                    }
                }
                smr.bones = dst;

                if (bindposeChanged)
                {
                    // sharedMesh is a shared asset: clone it and swap only the bind poses,
                    // so the source asset stays untouched (non-destructive) and the clone
                    // lives only in memory until the caller restores/frees it (No Cache).
                    Mesh clone = UnityEngine.Object.Instantiate(mesh);
                    clone.name = "MEASURE_RETARGET__" + mesh.name;
                    clone.bindposes = dstBP;
                    smr.sharedMesh = clone;
                }

                if (smr.rootBone != null)
                {
                    int ignore = 0;
                    smr.rootBone = ResolveBodyBone(smr.rootBone, byName, bodyBones, avatarAnimator, ref ignore);
                }
            }
            return remapped;
        }

        /// <summary>Resolves one garment bone to a body bone in three passes: exact
        /// name, then <see cref="BoneNameThesaurus"/>→Humanoid→body bone, then nearest
        /// by world position (within <see cref="MaxBoneSnap"/>); otherwise the garment
        /// bone is kept. Increments <paramref name="remapped"/> when re-bound.</summary>
        static Transform ResolveBodyBone(
            Transform garmentBone, Dictionary<string, Transform> byName, Transform[] bodyBones,
            Animator avatarAnimator, ref int remapped)
        {
            if (garmentBone == null)
            {
                return null;
            }
            // (1) exact same-name match.
            if (byName.TryGetValue(garmentBone.name, out Transform named))
            {
                remapped++;
                return named;
            }
            // (2) name thesaurus → Humanoid bone → the body's actual bone, regardless of
            // the body's naming (garment "Foot_L" → LeftFoot → body's real foot bone).
            if (avatarAnimator != null && BoneNameThesaurus.TryResolve(garmentBone.name, out HumanBodyBones hb))
            {
                Transform bodyBone = avatarAnimator.GetBoneTransform(hb);
                if (bodyBone != null)
                {
                    remapped++;
                    return bodyBone;
                }
            }
            // (3) nearest body bone by world position (garment co-located on the body;
            // covers accessories/fingers not in the thesaurus).
            Transform best = null;
            float bestSq = MaxBoneSnap * MaxBoneSnap;
            Vector3 p = garmentBone.position;
            foreach (Transform bb in bodyBones)
            {
                float d = (bb.position - p).sqrMagnitude;
                if (d < bestSq)
                {
                    bestSq = d;
                    best = bb;
                }
            }
            if (best != null)
            {
                remapped++;
                return best;
            }
            return garmentBone; // garment-only bone with no nearby body counterpart
        }

        static MeasurementDto BuildDto(
            VRClothDeclipper fitter,
            IReadOnlyList<BodyCapsule> capsules,
            VRClothBodyRadiusEstimator.Outcome outcome,
            float coverage,
            VRClothHeadCountMeasure.Result hc,
            string meshHash)
        {
            int n = capsules != null ? capsules.Count : 0;
            var capDtos = new CapsuleMeasureDto[n];
            for (int i = 0; i < n; i++)
            {
                BodyCapsule c = capsules[i];
                capDtos[i] = new CapsuleMeasureDto
                {
                    label = c.label ?? "",
                    radius_m = c.radius,
                    length_m = Vector3.Distance(c.start, c.end),
                    sampleCount = (outcome.sampleCounts != null && i < outcome.sampleCounts.Length) ? outcome.sampleCounts[i] : 0,
                    estimated = (outcome.estimated != null && i < outcome.estimated.Length) && outcome.estimated[i],
                };
            }

            return new MeasurementDto
            {
                timestamp = DateTime.Now.ToString("o"),
                avatar = fitter.targetAvatar != null ? fitter.targetAvatar.name : "",
                headCount_neckRef = hc.hasNeck ? hc.neckRef.headCount : 0f,
                headCount_headRef = hc.hasHead ? hc.headRef.headCount : 0f,
                height_m = hc.ok ? hc.Height : 0f,
                bodyCoverage = coverage,
                meshHash = meshHash,
                conditions = new MeasurementConditions
                {
                    radiusPercentile = fitter.radiusPercentile,
                },
                capsuleCount = n,
                capsules = capDtos,
            };
        }

        [Serializable]
        class MeasurementDto
        {
            public string schema = "vrcloth-body-measurement/2";
            public string timestamp;
            public string avatar;

            /// <summary>Scale-invariant body-family descriptor (docs/FAMILY_MODEL.md §2). 0 when the chin bone is absent.</summary>
            public float headCount_neckRef;
            public float headCount_headRef;

            /// <summary>Crown-to-sole height in meters (scale-dependent; use head-count for family, radii ratios for shape).</summary>
            public float height_m;

            /// <summary>Fraction of capsules measured from the body; low = an incomplete body model (docs/DIAGNOSTIC_HONESTY.md §1).</summary>
            public float bodyCoverage;

            /// <summary>
            /// Version key of the measured body asset (docs/MEASUREMENT_SPEC.md §6,
            /// schema /2): SHA-256 over the quantized body meshes. Provenance only —
            /// "same exact asset", not "same avatar" across users. Empty when no
            /// body mesh was found.
            /// </summary>
            public string meshHash;

            /// <summary>Measurement conditions, paired with the hash to reproduce the result (§6).</summary>
            public MeasurementConditions conditions;
            public int capsuleCount;
            public CapsuleMeasureDto[] capsules;
        }

        /// <summary>
        /// The measurement's conditions snapshot (docs/MEASUREMENT_SPEC.md §6).
        /// v1 records the radius percentile (the one parameter that shapes the
        /// numbers); pose, shape-key state, scale and tool/threshold versions are
        /// 保留 — see the §6 "v1 と保留" note.
        /// </summary>
        [Serializable]
        class MeasurementConditions
        {
            public float radiusPercentile;
        }

        [Serializable]
        class CapsuleMeasureDto
        {
            public string label;       // e.g. "Hips→Spine"

            /// <summary>Measured limb radius (circular-section approx; true slice girth ≈ 2π·radius is the §5 refinement).</summary>
            public float radius_m;
            public float length_m;
            public int sampleCount;    // body vertices attributed to this capsule
            public bool estimated;     // false = kept its fallback radius (no body data here)
        }

        [Serializable]
        class GarmentMeasurementDto
        {
            public string schema = "vrcloth-garment-measurement/1";
            public string timestamp;
            public string garment;

            /// <summary>The avatar whose skeleton/pose the garment was measured on.</summary>
            public string onAvatar;

            /// <summary>Fraction of proxy capsules the garment spans (a top covers torso/arms, not legs).</summary>
            public float coverage;

            /// <summary>Version key of the garment meshes (docs/MEASUREMENT_SPEC.md §6).</summary>
            public string meshHash;
            public MeasurementConditions conditions;
            public int capsuleCount;

            /// <summary>Per capsule, <c>radius_m</c> is the garment INNER radius (≈ finished girth / 2π); <c>estimated</c>=false means the garment does not cover this capsule.</summary>
            public CapsuleMeasureDto[] capsules;
        }
    }
}
