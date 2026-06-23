using NUnit.Framework;

namespace VRClothDeclipper.Tests
{
    /// <summary>
    /// Pins the false-green honesty guard (docs/DIAGNOSTIC_HONESTY.md §1): when
    /// the body proxy covers too little of the skeleton — the signature of a
    /// split body resolved to one part (e.g. only the hair) — a GREEN verdict is
    /// not trustworthy and must be flagged, not reported as confident.
    /// </summary>
    public class BodyModelConfidenceTests
    {
        const float Eps = 1e-5f;

        static bool[] WithMeasured(int measured, int total)
        {
            var flags = new bool[total];
            for (int i = 0; i < measured && i < total; i++) flags[i] = true;
            return flags;
        }

        [Test]
        public void Coverage_IsMeasuredFraction()
        {
            Assert.AreEqual(3f / 15f, BodyModelConfidence.Coverage(WithMeasured(3, 15)), Eps);
            Assert.AreEqual(1f, BodyModelConfidence.Coverage(WithMeasured(15, 15)), Eps);
            Assert.AreEqual(0f, BodyModelConfidence.Coverage(WithMeasured(0, 15)), Eps);
        }

        [Test]
        public void Coverage_EmptyOrNull_IsZero()
        {
            Assert.AreEqual(0f, BodyModelConfidence.Coverage(new bool[0]), Eps);
            Assert.AreEqual(0f, BodyModelConfidence.Coverage(null), Eps);
        }

        [Test]
        public void UnktHairOnlyBody_IsLowConfidence()
        {
            // The real failure: only 3 of 15 capsules (head + shoulders) found
            // body geometry because the proxy was built from the hair mesh.
            Assert.IsTrue(BodyModelConfidence.IsLowConfidence(WithMeasured(3, 15)));
        }

        [Test]
        public void WellCoveredBody_IsNotLowConfidence()
        {
            Assert.IsFalse(BodyModelConfidence.IsLowConfidence(WithMeasured(13, 15)));
        }

        [Test]
        public void ZeroMeasured_IsLowConfidence_WhenEstimationRan()
        {
            // 15 capsules, none measured: a body model that the estimator saw but
            // could measure nothing from — definitely not trustworthy.
            Assert.IsTrue(BodyModelConfidence.IsLowConfidence(WithMeasured(0, 15)));
        }

        [Test]
        public void EmptyOrNull_IsNotLowConfidence_NotJudgeable()
        {
            // No estimation outcome at all = "we don't know", not "low": the
            // guard must not cry wolf when radius estimation never ran.
            Assert.IsFalse(BodyModelConfidence.IsLowConfidence(new bool[0]));
            Assert.IsFalse(BodyModelConfidence.IsLowConfidence(null));
        }

        [Test]
        public void Threshold_SitsAtHalfCoverage()
        {
            Assert.IsTrue(BodyModelConfidence.IsLowConfidence(WithMeasured(4, 10)), "0.4 < 0.5 → low");
            Assert.IsFalse(BodyModelConfidence.IsLowConfidence(WithMeasured(5, 10)), "0.5 is not below 0.5");
        }
    }
}
