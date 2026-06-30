using System.Collections.Generic;
using UnityEngine;

namespace VRClothDeclipper
{
    /// <summary>
    /// Standardized torso measurement points (bust / waist / hips) from the avatar's
    /// body mesh — the §2 "計測点の標準化" refinement of the per-capsule radii
    /// (docs/MEASUREMENT_SPEC.md §2, §8). Bust and hips are girth maxima and waist a
    /// girth minimum along the Hips→Chest axis, so they track the actual anatomical
    /// bulges instead of a bone-segment's representative radius. The geometry lives in
    /// the pure, unit-tested <see cref="GirthProfile"/>; this editor glue only resolves
    /// the torso axis from the Humanoid skeleton and feeds it world-space body vertices.
    ///
    /// Records ONLY three girth scalars + their axis fractions — like the rest of 採寸,
    /// a handful of girths cannot reconstruct the body (No Cache, docs/
    /// INFORMATION_ARCHITECTURE.md). The numbers are shape-key sensitive (the bust
    /// moves with chest blendshapes), so measure at shape-key 0 for a reproducible
    /// snapshot (docs/MEASUREMENT_SPEC.md §6). Thresholds here are uncalibrated — E2E
    /// calibration is a phase-5 follow-up.
    /// </summary>
    public static class VRClothGirthMeasure
    {
        // Uncalibrated defaults (E2E follow-up). The band count trades axial resolution
        // for samples-per-band; the prominence gate drops ripples that are not a real
        // bust/waist/hips turn.
        const int BandCount = 24;
        const int SectorCount = 36;
        const int MinSamplesPerBand = 8;
        const int SmoothWindow = 3;
        const float MinProminenceM = 0.03f;
        // Extend the axis above Chest so the bust bulge sits in the band interior
        // (FindExtrema only turns on interior bands), while staying below the
        // shoulders/arms (which sit near the Neck and would inflate the top band).
        const float TopExtendFraction = 0.15f;

        public struct Result
        {
            /// <summary>True when at least one of bust/waist/hips was identified.</summary>
            public bool ok;
            public bool hasBust;
            public bool hasWaist;
            public bool hasHips;
            public float bustGirth_m;
            public float waistGirth_m;
            public float hipsGirth_m;
            public float bustAxisT;
            public float waistAxisT;
            public float hipsAxisT;
            /// <summary>How many girth extrema the profile produced (diagnostic).</summary>
            public int extremaCount;
        }

        /// <summary>
        /// Measures bust/waist/hips girths from the body world-space vertices (already
        /// baked by the caller, same convention as <see cref="VRClothMeshCapture"/>).
        /// Returns a result with <c>ok = false</c> when the avatar is not Humanoid or
        /// the Hips/Chest axis cannot be resolved.
        /// </summary>
        public static Result Measure(VRClothDeclipper fitter, IReadOnlyList<Vector3> bodyWorldVerts)
        {
            var r = new Result();
            if (fitter == null || fitter.targetAvatar == null
                || bodyWorldVerts == null || bodyWorldVerts.Count == 0)
            {
                return r;
            }
            Animator animator = fitter.targetAvatar.GetComponent<Animator>();
            if (animator == null || !animator.isHuman)
            {
                return r;
            }
            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            Transform top = animator.GetBoneTransform(HumanBodyBones.UpperChest);
            if (top == null) top = animator.GetBoneTransform(HumanBodyBones.Chest);
            if (top == null) top = animator.GetBoneTransform(HumanBodyBones.Spine);
            if (hips == null || top == null)
            {
                return r;
            }

            Vector3 axisStart = hips.position;
            Vector3 axisEnd = axisStart + (top.position - axisStart) * (1f + TopExtendFraction);

            List<GirthProfile.Band> bands = GirthProfile.Compute(
                axisStart, axisEnd, bodyWorldVerts, BandCount, SectorCount, MinSamplesPerBand);
            List<GirthProfile.Extremum> ex = GirthProfile.FindExtrema(bands, SmoothWindow, MinProminenceM);
            GirthProfile.TorsoPoints tp = GirthProfile.ClassifyTorso(ex);

            r.extremaCount = ex.Count;
            r.hasBust = tp.hasBust;
            r.hasWaist = tp.hasWaist;
            r.hasHips = tp.hasHips;
            r.ok = tp.hasBust || tp.hasWaist || tp.hasHips;
            if (tp.hasBust) { r.bustGirth_m = tp.bust.girthM; r.bustAxisT = tp.bust.axisT; }
            if (tp.hasWaist) { r.waistGirth_m = tp.waist.girthM; r.waistAxisT = tp.waist.axisT; }
            if (tp.hasHips) { r.hipsGirth_m = tp.hips.girthM; r.hipsAxisT = tp.hips.axisT; }
            return r;
        }
    }
}
