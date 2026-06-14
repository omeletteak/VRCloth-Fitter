using NUnit.Framework;
using UnityEngine;

namespace VRClothFitter.Tests
{
    public class CapsuleRadiusEstimatorTests
    {
        const float Eps = 1e-5f;

        // A capsule whose axis is the unit Y segment at x = 0. The closest point
        // on the axis for any (x, 0.5, 0) is (0, 0.5, 0), so a vertex placed at
        // (d, 0.5, 0) sits exactly distance d from the axis — convenient for
        // dialling in the axis distances the estimator buckets and percentiles.
        static BodyCapsule YCapsule(float radius)
        {
            return new BodyCapsule(Vector3.zero, new Vector3(0f, 1f, 0f), radius);
        }

        // The same capsule shifted to x = offset, used to test attribution when
        // several capsules compete for the nearest-axis vote.
        static BodyCapsule YCapsuleAt(float offset, float radius)
        {
            return new BodyCapsule(new Vector3(offset, 0f, 0f), new Vector3(offset, 1f, 0f), radius);
        }

        // A vertex distance d from the YCapsule axis (and d from a YCapsuleAt(x) axis).
        static Vector3 AtDistance(float x, float d)
        {
            return new Vector3(x + d, 0.5f, 0f);
        }

        [Test]
        public void Estimate_NoCapsules_ReturnsEmptyArrays()
        {
            var result = CapsuleRadiusEstimator.Estimate(
                new BodyCapsule[0],
                new[] { Vector3.zero, Vector3.one },
                percentile: 0.5f, minSamples: 1, gateFactor: 2f, minRadius: 0.01f, maxRadius: 1f);

            Assert.AreEqual(0, result.radii.Length);
            Assert.AreEqual(0, result.sampleCounts.Length);
            Assert.AreEqual(0, result.estimated.Length);
        }

        [Test]
        public void Estimate_NullBodyVertices_KeepsFallback()
        {
            var capsules = new[] { YCapsule(0.25f), YCapsuleAt(5f, 0.33f) };

            var result = CapsuleRadiusEstimator.Estimate(
                capsules, null,
                percentile: 0.5f, minSamples: 1, gateFactor: 2f, minRadius: 0.01f, maxRadius: 1f);

            Assert.AreEqual(0.25f, result.radii[0], Eps);
            Assert.AreEqual(0.33f, result.radii[1], Eps);
            CollectionAssert.AreEqual(new[] { 0, 0 }, result.sampleCounts);
            CollectionAssert.AreEqual(new[] { false, false }, result.estimated);
        }

        [Test]
        public void Estimate_EmptyBodyVertices_KeepsFallback()
        {
            var capsules = new[] { YCapsule(0.25f) };

            var result = CapsuleRadiusEstimator.Estimate(
                capsules, new Vector3[0],
                percentile: 0.5f, minSamples: 1, gateFactor: 2f, minRadius: 0.01f, maxRadius: 1f);

            Assert.AreEqual(0.25f, result.radii[0], Eps);
            Assert.AreEqual(0, result.sampleCounts[0]);
            Assert.IsFalse(result.estimated[0]);
        }

        [Test]
        public void Estimate_BelowMinSamples_KeepsFallback()
        {
            var capsules = new[] { YCapsule(0.25f) };
            var verts = new[]
            {
                AtDistance(0f, 0.1f),
                AtDistance(0f, 0.1f),
                AtDistance(0f, 0.1f),
            };

            var result = CapsuleRadiusEstimator.Estimate(
                capsules, verts,
                percentile: 1f, minSamples: 4, gateFactor: 2f, minRadius: 0.001f, maxRadius: 10f);

            // Three vertices were attributed, but fewer than minSamples, so the
            // capsule keeps its fallback radius rather than trusting thin data.
            Assert.AreEqual(3, result.sampleCounts[0]);
            Assert.IsFalse(result.estimated[0]);
            Assert.AreEqual(0.25f, result.radii[0], Eps);
        }

        [Test]
        public void Estimate_AtMinSamples_Estimates()
        {
            var capsules = new[] { YCapsule(0.25f) };
            var verts = new[]
            {
                AtDistance(0f, 0.1f),
                AtDistance(0f, 0.1f),
                AtDistance(0f, 0.1f),
            };

            var result = CapsuleRadiusEstimator.Estimate(
                capsules, verts,
                percentile: 1f, minSamples: 3, gateFactor: 2f, minRadius: 0.001f, maxRadius: 10f);

            // The minSamples gate is inclusive: count == minSamples estimates.
            Assert.AreEqual(3, result.sampleCounts[0]);
            Assert.IsTrue(result.estimated[0]);
            Assert.AreEqual(0.1f, result.radii[0], Eps);
        }

        [Test]
        public void Estimate_UniformDistance_RadiusIsThatDistance()
        {
            var capsules = new[] { YCapsule(0.25f) };
            var verts = new[]
            {
                AtDistance(0f, 0.1f),
                AtDistance(0f, 0.1f),
                AtDistance(0f, 0.1f),
                AtDistance(0f, 0.1f),
                AtDistance(0f, 0.1f),
            };

            var result = CapsuleRadiusEstimator.Estimate(
                capsules, verts,
                percentile: 1f, minSamples: 1, gateFactor: 2f, minRadius: 0.001f, maxRadius: 10f);

            Assert.AreEqual(5, result.sampleCounts[0]);
            Assert.IsTrue(result.estimated[0]);
            Assert.AreEqual(0.1f, result.radii[0], Eps);
        }

        [Test]
        public void Estimate_Percentile_SelectsExpectedSample()
        {
            var capsules = new[] { YCapsule(0.5f) }; // big enough that gate keeps all four
            // Sorted axis distances: 0.05, 0.10, 0.15, 0.20.
            var verts = new[]
            {
                AtDistance(0f, 0.15f),
                AtDistance(0f, 0.05f),
                AtDistance(0f, 0.20f),
                AtDistance(0f, 0.10f),
            };

            // Index = CeilToInt(p * count) - 1, clamped to [0, count-1].
            // p=1.0 -> index 3 -> 0.20 ; p=0.5 -> index 1 -> 0.10 ; p=0 -> index 0 -> 0.05.
            Assert.AreEqual(0.20f, RadiusAt(capsules, verts, 1.0f), Eps);
            Assert.AreEqual(0.10f, RadiusAt(capsules, verts, 0.5f), Eps);
            Assert.AreEqual(0.05f, RadiusAt(capsules, verts, 0.0f), Eps);
        }

        static float RadiusAt(BodyCapsule[] capsules, Vector3[] verts, float percentile)
        {
            return CapsuleRadiusEstimator.Estimate(
                capsules, verts,
                percentile, minSamples: 4, gateFactor: 2f, minRadius: 0.001f, maxRadius: 10f).radii[0];
        }

        [Test]
        public void Estimate_GateExcludesFarVertices()
        {
            var capsules = new[] { YCapsule(0.25f) }; // gateFactor 2 -> gate at 0.5
            var verts = new[]
            {
                AtDistance(0f, 0.1f),
                AtDistance(0f, 0.1f),
                AtDistance(0f, 0.1f),
                AtDistance(0f, 0.6f), // beyond the gate: a region this capsule does not cover
            };

            var result = CapsuleRadiusEstimator.Estimate(
                capsules, verts,
                percentile: 1f, minSamples: 1, gateFactor: 2f, minRadius: 0.001f, maxRadius: 10f);

            // The far vertex is neither counted nor allowed to inflate the radius.
            Assert.AreEqual(3, result.sampleCounts[0]);
            Assert.AreEqual(0.1f, result.radii[0], Eps);
        }

        [Test]
        public void Estimate_ClampsToMinRadius()
        {
            var capsules = new[] { YCapsule(0.25f) };
            var verts = new[]
            {
                AtDistance(0f, 0.02f),
                AtDistance(0f, 0.02f),
                AtDistance(0f, 0.02f),
            };

            var result = CapsuleRadiusEstimator.Estimate(
                capsules, verts,
                percentile: 1f, minSamples: 1, gateFactor: 2f, minRadius: 0.05f, maxRadius: 10f);

            Assert.IsTrue(result.estimated[0]);
            Assert.AreEqual(0.05f, result.radii[0], Eps); // 0.02 clamped up to minRadius
        }

        [Test]
        public void Estimate_ClampsToMaxRadius()
        {
            var capsules = new[] { YCapsule(0.5f) }; // gateFactor 3 -> gate at 1.5 keeps 0.9
            var verts = new[]
            {
                AtDistance(0f, 0.9f),
                AtDistance(0f, 0.9f),
                AtDistance(0f, 0.9f),
            };

            var result = CapsuleRadiusEstimator.Estimate(
                capsules, verts,
                percentile: 1f, minSamples: 1, gateFactor: 3f, minRadius: 0.001f, maxRadius: 0.3f);

            Assert.IsTrue(result.estimated[0]);
            Assert.AreEqual(0.3f, result.radii[0], Eps); // 0.9 clamped down to maxRadius
        }

        [Test]
        public void Estimate_AttributesEachVertexToNearestCapsule()
        {
            var capsules = new[] { YCapsule(0.25f), YCapsuleAt(5f, 0.25f) };
            var verts = new[]
            {
                AtDistance(0f, 0.08f), // -> capsule 0
                AtDistance(0f, 0.12f), // -> capsule 0
                AtDistance(0f, 0.10f), // -> capsule 0
                AtDistance(5f, 0.10f), // -> capsule 1
                AtDistance(5f, 0.15f), // -> capsule 1
            };

            var result = CapsuleRadiusEstimator.Estimate(
                capsules, verts,
                percentile: 1f, minSamples: 1, gateFactor: 2f, minRadius: 0.001f, maxRadius: 10f);

            CollectionAssert.AreEqual(new[] { 3, 2 }, result.sampleCounts);
            CollectionAssert.AreEqual(new[] { true, true }, result.estimated);
            Assert.AreEqual(0.12f, result.radii[0], Eps); // max of {0.08, 0.12, 0.10}
            Assert.AreEqual(0.15f, result.radii[1], Eps); // max of {0.10, 0.15}
        }

        [Test]
        public void Estimate_UncoveredCapsule_KeepsFallbackWhileNeighbourEstimates()
        {
            var capsules = new[] { YCapsule(0.25f), YCapsuleAt(5f, 0.33f) };
            // All vertices sit near capsule 0; capsule 1 gets nothing.
            var verts = new[]
            {
                AtDistance(0f, 0.1f),
                AtDistance(0f, 0.1f),
                AtDistance(0f, 0.1f),
            };

            var result = CapsuleRadiusEstimator.Estimate(
                capsules, verts,
                percentile: 1f, minSamples: 2, gateFactor: 2f, minRadius: 0.001f, maxRadius: 10f);

            Assert.AreEqual(3, result.sampleCounts[0]);
            Assert.IsTrue(result.estimated[0]);
            Assert.AreEqual(0.1f, result.radii[0], Eps);

            Assert.AreEqual(0, result.sampleCounts[1]);
            Assert.IsFalse(result.estimated[1]);
            Assert.AreEqual(0.33f, result.radii[1], Eps); // untouched fallback
        }
    }
}
