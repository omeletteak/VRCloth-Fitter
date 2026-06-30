using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace VRClothDeclipper.Tests
{
    public class GirthProfileTests
    {
        static readonly Vector3 Start = Vector3.zero;
        static Vector3 End(float h) => new Vector3(0f, h, 0f);

        // A tube around the +Y axis whose radius varies with the height fraction t.
        static List<Vector3> Tube(System.Func<float, float> radiusAtT, float height, int rings, int perRing)
        {
            var v = new List<Vector3>(rings * perRing);
            for (int ri = 0; ri < rings; ri++)
            {
                float t = (ri + 0.5f) / rings;
                float y = t * height;
                float r = radiusAtT(t);
                for (int pi = 0; pi < perRing; pi++)
                {
                    float a = 2f * Mathf.PI * pi / perRing;
                    v.Add(new Vector3(r * Mathf.Cos(a), y, r * Mathf.Sin(a)));
                }
            }
            return v;
        }

        static List<Vector3> Cylinder(float radius, float height, int rings, int perRing)
            => Tube(_ => radius, height, rings, perRing);

        [Test]
        public void Compute_Cylinder_GirthApproxTwoPiR()
        {
            float r = 0.2f;
            var verts = Cylinder(r, 1f, rings: 20, perRing: 120);
            var bands = GirthProfile.Compute(Start, End(1f), verts, bandCount: 10, sectorCount: 36, minSamplesPerBand: 8);

            float expected = 2f * Mathf.PI * r;
            Assert.AreEqual(10, bands.Count);
            foreach (var b in bands)
            {
                Assert.Greater(b.girthM, 0f, "every band of a full cylinder should have an outline");
                // A 36-gon inscribes ~99.9% of 2πr; allow 3% for band/sector discretization.
                Assert.AreEqual(expected, b.girthM, expected * 0.03f, "polygon girth within 3% of 2πr");
            }
        }

        [Test]
        public void Compute_Degenerate_ReturnsZeroGirthBands()
        {
            var bands = GirthProfile.Compute(Start, Start, new List<Vector3>(), 5, 36, 8);
            Assert.AreEqual(5, bands.Count);
            foreach (var b in bands)
            {
                Assert.AreEqual(0f, b.girthM);
            }
        }

        [Test]
        public void FindExtrema_Barrel_FindsSingleMaximumNearCentre()
        {
            // Fattest at t = 0.5.
            var verts = Tube(t => 0.2f + 0.1f * Mathf.Sin(Mathf.PI * t), 1f, 24, 120);
            var bands = GirthProfile.Compute(Start, End(1f), verts, 20, 36, 8);
            var ex = GirthProfile.FindExtrema(bands, smoothWindow: 3, minProminenceM: 0.05f);

            var maxima = ex.FindAll(e => e.isMaximum);
            Assert.AreEqual(1, maxima.Count, "a barrel has one girth peak");
            Assert.AreEqual(0.5f, maxima[0].axisT, 0.12f, "peak near the middle");
        }

        [Test]
        public void FindExtrema_Hourglass_FindsSingleMinimumNearCentre()
        {
            var verts = Tube(t => 0.3f - 0.15f * Mathf.Sin(Mathf.PI * t), 1f, 24, 120);
            var bands = GirthProfile.Compute(Start, End(1f), verts, 20, 36, 8);
            var ex = GirthProfile.FindExtrema(bands, 3, 0.05f);

            var minima = ex.FindAll(e => !e.isMaximum);
            Assert.AreEqual(1, minima.Count, "an hourglass has one girth valley");
            Assert.AreEqual(0.5f, minima[0].axisT, 0.12f, "valley near the middle");
        }

        [Test]
        public void FindExtrema_BustWaistHips_FindsMaxMinMaxInOrder()
        {
            // Two girth peaks (bust at t≈0.3, hips at t≈0.7) with a valley (waist) between.
            System.Func<float, float> profile = t =>
                0.16f
                + 0.12f * Mathf.Exp(-Mathf.Pow((t - 0.3f) / 0.12f, 2f))
                + 0.12f * Mathf.Exp(-Mathf.Pow((t - 0.7f) / 0.12f, 2f));
            var verts = Tube(profile, 1f, 30, 120);
            var bands = GirthProfile.Compute(Start, End(1f), verts, 24, 36, 8);
            var ex = GirthProfile.FindExtrema(bands, 3, 0.08f);

            Assert.AreEqual(3, ex.Count, "expected bust / waist / hips");
            Assert.IsTrue(ex[0].isMaximum, "bust is a maximum");
            Assert.IsFalse(ex[1].isMaximum, "waist is a minimum");
            Assert.IsTrue(ex[2].isMaximum, "hips is a maximum");
            Assert.Less(ex[0].axisT, ex[1].axisT, "bust above waist along the axis");
            Assert.Less(ex[1].axisT, ex[2].axisT, "waist above hips along the axis");
            Assert.Less(ex[1].girthM, ex[0].girthM, "waist tighter than bust");
            Assert.Less(ex[1].girthM, ex[2].girthM, "waist tighter than hips");
        }

        [Test]
        public void FindExtrema_LowProminenceRipple_Ignored()
        {
            // A ~2 mm ripple is below the prominence gate and must not be a measurement point.
            var verts = Tube(t => 0.2f + 0.002f * Mathf.Sin(3f * Mathf.PI * t), 1f, 24, 120);
            var bands = GirthProfile.Compute(Start, End(1f), verts, 20, 36, 8);
            var ex = GirthProfile.FindExtrema(bands, 3, 0.05f);

            Assert.AreEqual(0, ex.Count, "a tiny ripple is not bust/waist/hips");
        }

        [Test]
        public void ClassifyTorso_HipsWaistBust_LabelsByAxisPosition()
        {
            // Torso along +Y: hips bulge low (t≈0.18), waist pinch (t≈0.5), bust bulge
            // high (t≈0.82) — the Hips→Chest convention (hips at t=0, chest at t=1).
            System.Func<float, float> profile = t =>
                0.16f
                + 0.10f * Mathf.Exp(-Mathf.Pow((t - 0.18f) / 0.12f, 2f))
                + 0.12f * Mathf.Exp(-Mathf.Pow((t - 0.82f) / 0.12f, 2f));
            var verts = Tube(profile, 1f, 30, 120);
            var bands = GirthProfile.Compute(Start, End(1f), verts, 24, 36, 8);
            var ex = GirthProfile.FindExtrema(bands, 3, 0.05f);
            var tp = GirthProfile.ClassifyTorso(ex);

            Assert.IsTrue(tp.hasHips, "hips identified");
            Assert.IsTrue(tp.hasWaist, "waist identified");
            Assert.IsTrue(tp.hasBust, "bust identified");
            Assert.Less(tp.hips.axisT, tp.waist.axisT, "hips below waist along the axis");
            Assert.Less(tp.waist.axisT, tp.bust.axisT, "waist below bust along the axis");
            Assert.Less(tp.waist.girthM, tp.hips.girthM, "waist tighter than hips");
            Assert.Less(tp.waist.girthM, tp.bust.girthM, "waist tighter than bust");
        }

        [Test]
        public void ClassifyTorso_SingleMaximum_PlacedByAxisHalf()
        {
            // One bulge high on the axis (t≈0.7) and nothing else: it must be the bust,
            // not the hips, and there is no waist to report.
            var verts = Tube(t => 0.2f + 0.1f * Mathf.Exp(-Mathf.Pow((t - 0.7f) / 0.12f, 2f)), 1f, 24, 120);
            var bands = GirthProfile.Compute(Start, End(1f), verts, 20, 36, 8);
            var ex = GirthProfile.FindExtrema(bands, 3, 0.05f);
            var tp = GirthProfile.ClassifyTorso(ex);

            Assert.IsTrue(tp.hasBust, "the upper-axis bulge is the bust");
            Assert.IsFalse(tp.hasHips, "a lone upper bulge is not the hips");
        }

        [Test]
        public void ClassifyTorso_NoExtrema_ReportsNothing()
        {
            var tp = GirthProfile.ClassifyTorso(new List<GirthProfile.Extremum>());
            Assert.IsFalse(tp.hasBust);
            Assert.IsFalse(tp.hasWaist);
            Assert.IsFalse(tp.hasHips);
        }
    }
}
