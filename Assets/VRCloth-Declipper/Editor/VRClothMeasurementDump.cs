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

        /// <summary>Project-root path (sibling of Assets/) of the 採寸表 file.</summary>
        public static string FilePath()
        {
            return Path.Combine(Directory.GetParent(Application.dataPath).FullName, FileName);
        }

        /// <summary>
        /// Measures the fitter's target avatar and appends one JSONL row to
        /// <see cref="FileName"/>. Logs a summary either way; returns silently
        /// after logging when the avatar can't be measured.
        /// </summary>
        public static void Dump(VRClothDeclipper fitter)
        {
            if (fitter == null || fitter.targetAvatar == null)
            {
                Debug.LogWarning("[VRClothDeclipper] 採寸: assign a Target Avatar first.");
                return;
            }

            List<BodyCapsule> capsules = VRClothProxyGenerator.Generate(fitter.targetAvatar);
            if (capsules == null || capsules.Count == 0)
            {
                Debug.LogWarning("[VRClothDeclipper] 採寸: could not generate proxy capsules (target avatar must be Humanoid).");
                return;
            }

            VRClothBodyRadiusEstimator.Outcome outcome = VRClothBodyRadiusEstimator.Apply(fitter, capsules);
            capsules = outcome.capsules;
            float coverage = BodyModelConfidence.Coverage(outcome.estimated);
            VRClothHeadCountMeasure.Result hc = VRClothHeadCountMeasure.Compute(fitter);

            var dto = BuildDto(fitter, capsules, outcome, coverage, hc);
            try
            {
                string json = JsonUtility.ToJson(dto, false);
                string path = FilePath();
                File.AppendAllText(path, json + "\n");
                Debug.Log($"[VRClothDeclipper] 採寸 appended for '{dto.avatar}': "
                    + $"head-count {dto.headCount_neckRef:F2} (Neck) / {dto.headCount_headRef:F2} (Head), "
                    + $"height {dto.height_m:F3} m, {dto.capsuleCount} capsules, coverage {coverage:P0}. -> {path}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[VRClothDeclipper] 採寸: could not write measurement: {e.Message}");
            }
        }

        static MeasurementDto BuildDto(
            VRClothDeclipper fitter,
            IReadOnlyList<BodyCapsule> capsules,
            VRClothBodyRadiusEstimator.Outcome outcome,
            float coverage,
            VRClothHeadCountMeasure.Result hc)
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
                capsuleCount = n,
                capsules = capDtos,
            };
        }

        [Serializable]
        class MeasurementDto
        {
            public string schema = "vrcloth-body-measurement/1";
            public string timestamp;
            public string avatar;

            /// <summary>Scale-invariant body-family descriptor (docs/FAMILY_MODEL.md §2). 0 when the chin bone is absent.</summary>
            public float headCount_neckRef;
            public float headCount_headRef;

            /// <summary>Crown-to-sole height in meters (scale-dependent; use head-count for family, radii ratios for shape).</summary>
            public float height_m;

            /// <summary>Fraction of capsules measured from the body; low = an incomplete body model (docs/DIAGNOSTIC_HONESTY.md §1).</summary>
            public float bodyCoverage;
            public int capsuleCount;
            public CapsuleMeasureDto[] capsules;
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
    }
}
