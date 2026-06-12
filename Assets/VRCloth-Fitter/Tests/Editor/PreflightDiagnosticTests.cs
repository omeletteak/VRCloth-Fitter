using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace VRClothFitter.Tests
{
    /// <summary>
    /// Pins the support-envelope judgment of docs/DESIGN.md §9: depths are
    /// measured below the body surface (margin excluded), and the verdict
    /// follows the green/yellow/red table.
    /// </summary>
    public class PreflightDiagnosticTests
    {
        const float Margin = 0.005f;
        const float Eps = 1e-5f;

        // Capsule along the Y axis from origin to (0,1,0), radius 0.25.
        static List<BodyCapsule> SingleCapsule(float radius = 0.25f)
        {
            return new List<BodyCapsule> { new BodyCapsule(Vector3.zero, new Vector3(0f, 1f, 0f), radius) };
        }

        // A position whose depth below the capsule surface is exactly `depth`
        // (placed sideways on the cylinder section at y=0.5).
        static Vector3 AtSurfaceDepth(float depth, float radius = 0.25f, float z = 0f)
        {
            return new Vector3(radius - depth, 0.5f, z);
        }

        static Vector3 FarOutside(int i)
        {
            return new Vector3(2f + i * 0.1f, 0.5f, 0f);
        }

        static PreflightReport Evaluate(Vector3[] positions, List<BodyCapsule> capsules, int[] triangles = null)
        {
            var hits = PenetrationDetection.Scan(positions, capsules, Margin);
            return PreflightDiagnostic.Evaluate(positions, triangles, hits, capsules, Margin);
        }

        [Test]
        public void NoHits_IsGreenWithZeroStats()
        {
            var positions = new[] { FarOutside(0), FarOutside(1) };

            var report = Evaluate(positions, SingleCapsule());

            Assert.AreEqual(PreflightVerdict.Green, report.verdict);
            Assert.AreEqual(0, report.hitCount);
            Assert.AreEqual(0, report.penetratingCount);
            Assert.AreEqual(0f, report.maxDepth);
        }

        [Test]
        public void MarginZoneGrazeOnly_IsGreen_NullTestAnalog()
        {
            // Vertices hugging the body inside the margin zone but above the
            // actual surface: a well-fitting outfit.
            var positions = new[]
            {
                new Vector3(0.252f, 0.5f, 0f), // sd = +0.002 < margin: hit, no penetration
                new Vector3(0.253f, 0.4f, 0f),
                FarOutside(0),
            };

            var report = Evaluate(positions, SingleCapsule());

            Assert.AreEqual(PreflightVerdict.Green, report.verdict);
            Assert.AreEqual(2, report.hitCount, "margin-zone hits should be counted");
            Assert.AreEqual(0, report.penetratingCount, "no vertex is below the surface");
            Assert.AreEqual(0f, report.maxDepth);
        }

        [Test]
        public void ShallowSparsePenetration_IsGreen()
        {
            // One vertex 7 mm deep out of 12 (ratio 8.3% ≤ 10%, relative
            // depth 2.8% ≤ 15%).
            var positions = new Vector3[12];
            positions[0] = AtSurfaceDepth(0.007f);
            for (int i = 1; i < positions.Length; i++)
            {
                positions[i] = FarOutside(i);
            }

            var report = Evaluate(positions, SingleCapsule());

            Assert.AreEqual(PreflightVerdict.Green, report.verdict);
            Assert.AreEqual(1, report.penetratingCount);
            Assert.AreEqual(0.007f, report.maxDepth, Eps);
        }

        [Test]
        public void MidDepth_IsYellow()
        {
            var positions = new Vector3[12];
            positions[0] = AtSurfaceDepth(0.02f); // 2 cm: beyond green, below red
            for (int i = 1; i < positions.Length; i++)
            {
                positions[i] = FarOutside(i);
            }

            var report = Evaluate(positions, SingleCapsule());

            Assert.AreEqual(PreflightVerdict.Yellow, report.verdict);
        }

        [Test]
        public void RelativeDepthBeyondGreen_IsYellow_OnThinCapsule()
        {
            // 8 mm is green in absolute terms, but 20% of a 4 cm radius limb.
            var capsules = SingleCapsule(0.04f);
            var positions = new Vector3[12];
            positions[0] = AtSurfaceDepth(0.008f, 0.04f);
            for (int i = 1; i < positions.Length; i++)
            {
                positions[i] = FarOutside(i);
            }

            var report = Evaluate(positions, capsules);

            Assert.AreEqual(PreflightVerdict.Yellow, report.verdict);
            Assert.AreEqual(0.2f, report.maxDepthOverRadius, 1e-3f);
        }

        [Test]
        public void DeepPenetration_IsRed()
        {
            var positions = new Vector3[100];
            positions[0] = AtSurfaceDepth(0.04f); // 4 cm > 3 cm
            for (int i = 1; i < positions.Length; i++)
            {
                positions[i] = FarOutside(i);
            }

            var report = Evaluate(positions, SingleCapsule());

            Assert.AreEqual(PreflightVerdict.Red, report.verdict);
        }

        [Test]
        public void HighPenetratingRatio_IsRed_EvenWhenShallow()
        {
            // 4 of 10 vertices penetrate 6 mm: shallow, but 40% > 30% means
            // the garment globally does not fit (size-class mismatch).
            var positions = new Vector3[10];
            for (int i = 0; i < 4; i++)
            {
                positions[i] = AtSurfaceDepth(0.006f, 0.25f, i * 0.01f);
            }
            for (int i = 4; i < positions.Length; i++)
            {
                positions[i] = FarOutside(i);
            }

            var report = Evaluate(positions, SingleCapsule());

            Assert.AreEqual(PreflightVerdict.Red, report.verdict);
            Assert.AreEqual(0.4f, report.penetratingRatio, Eps);
        }

        [Test]
        public void P95Depth_IgnoresTheDeepestOutlier()
        {
            // 19 vertices at exactly 2 mm deep, spread along the cylinder
            // axis so the depth stays constant, and one outlier at 9 mm.
            var positions = new Vector3[20];
            for (int i = 0; i < 19; i++)
            {
                positions[i] = new Vector3(0.248f, 0.3f + i * 0.02f, 0f);
            }
            positions[19] = new Vector3(0.241f, 0.55f, 0f);

            var report = Evaluate(positions, SingleCapsule());

            Assert.AreEqual(0.002f, report.p95Depth, 1e-4f);
            Assert.AreEqual(0.009f, report.maxDepth, 1e-4f);
        }

        [Test]
        public void LargestPatchRatio_CountsOnlyTheBiggestConnectedComponent()
        {
            // Strip mesh: triangles (0,1,2), (1,3,2), (3,4,5).
            // Vertices 0 and 1 penetrate (connected by an edge); vertex 4
            // penetrates in isolation (its neighbors 3 and 5 stay outside).
            var positions = new[]
            {
                AtSurfaceDepth(0.01f, 0.25f, 0f),
                AtSurfaceDepth(0.01f, 0.25f, 0.01f),
                FarOutside(2),
                FarOutside(3),
                AtSurfaceDepth(0.01f, 0.25f, 0.05f),
                FarOutside(5),
            };
            var triangles = new[] { 0, 1, 2, 1, 3, 2, 3, 4, 5 };

            var report = Evaluate(positions, SingleCapsule(), triangles);

            Assert.AreEqual(2f / 6f, report.largestPatchRatio, Eps);
        }

        [Test]
        public void GreenYellowBoundary_SitsAtOneCentimeter()
        {
            var capsules = SingleCapsule();
            var deep = new Vector3[12];
            var shallow = new Vector3[12];
            shallow[0] = AtSurfaceDepth(0.0099f);
            deep[0] = AtSurfaceDepth(0.0101f);
            for (int i = 1; i < 12; i++)
            {
                shallow[i] = FarOutside(i);
                deep[i] = FarOutside(i);
            }

            Assert.AreEqual(PreflightVerdict.Green, Evaluate(shallow, capsules).verdict);
            Assert.AreEqual(PreflightVerdict.Yellow, Evaluate(deep, capsules).verdict);
        }
    }
}
