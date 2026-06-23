namespace VRClothDeclipper
{
    /// <summary>
    /// Honesty guard for the preflight diagnostic (docs/DESIGN.md §9,
    /// docs/DIAGNOSTIC_HONESTY.md §1). On a split-body avatar — body, head and
    /// hair authored as separate meshes, e.g. the YOYOGI MORI "YM Body" standard —
    /// the body proxy can be built from only part of the body, in the worst case
    /// the hair alone. The collider then has no surface over the torso and legs,
    /// penetration there goes undetected, and every garment reads GREEN: a
    /// <em>false</em> green that looks identical to "no penetration".
    ///
    /// The tell is that few proxy capsules found any body geometry to measure a
    /// radius from (the radius estimator's per-capsule "estimated" flags). This
    /// judges, purely from those flags, whether the body model is too incomplete
    /// to trust a non-penetrating result — so the pipeline can say "GREEN here
    /// means 'could not see the body', not 'no penetration'" instead of emitting
    /// false confidence. Pure and editor-independent, so it is unit-testable like
    /// the rest of Core.
    /// </summary>
    public static class BodyModelConfidence
    {
        /// <summary>
        /// Below this fraction of capsules measured from the body, a GREEN /
        /// low-penetration verdict is not trustworthy. Provisional — calibrated
        /// against real avatars during E2E, like the §9 verdict thresholds.
        /// </summary>
        public const float MinCoverage = 0.5f;

        /// <summary>
        /// Fraction of proxy capsules whose radius was measured from body
        /// geometry (vs fell back to a default) — the body model's coverage of
        /// the proxy skeleton. 0 when there is nothing to judge.
        /// </summary>
        public static float Coverage(bool[] estimatedPerCapsule)
        {
            if (estimatedPerCapsule == null || estimatedPerCapsule.Length == 0)
            {
                return 0f;
            }
            int measured = 0;
            for (int i = 0; i < estimatedPerCapsule.Length; i++)
            {
                if (estimatedPerCapsule[i]) measured++;
            }
            return (float)measured / estimatedPerCapsule.Length;
        }

        /// <summary>
        /// True when the body model covers too little of the proxy skeleton to
        /// trust a GREEN / low-penetration verdict (<see cref="Coverage"/> below
        /// <paramref name="minCoverage"/>). An empty/absent outcome is treated as
        /// not-judgeable (false): no data is "we don't know", not "low" — the
        /// pipeline only consults this when radius estimation actually ran.
        /// </summary>
        public static bool IsLowConfidence(bool[] estimatedPerCapsule, float minCoverage = MinCoverage)
        {
            if (estimatedPerCapsule == null || estimatedPerCapsule.Length == 0)
            {
                return false;
            }
            return Coverage(estimatedPerCapsule) < minCoverage;
        }
    }
}
