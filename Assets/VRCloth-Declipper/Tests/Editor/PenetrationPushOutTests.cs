using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace VRClothDeclipper.Tests
{
    public class PenetrationPushOutTests
    {
        const float Eps = 1e-5f;
        const float Margin = 0.005f;

        // Capsule along the Y axis from origin to (0,1,0), radius 0.25.
        static List<BodyCapsule> SingleCapsule()
        {
            return new List<BodyCapsule> { new BodyCapsule(Vector3.zero, new Vector3(0f, 1f, 0f), 0.25f) };
        }

        static Vector3[] Compose(Vector3[] originals, Vector3[] displacements)
        {
            var composed = new Vector3[originals.Length];
            for (int v = 0; v < originals.Length; v++)
            {
                composed[v] = originals[v] + displacements[v];
            }
            return composed;
        }

        [Test]
        public void Apply_WritesDisplacementOntoMarginSurface()
        {
            var capsules = SingleCapsule();
            var originals = new[] { new Vector3(0.1f, 0.5f, 0f) };
            var displacements = new Vector3[1];
            var hits = PenetrationDetection.Scan(originals, capsules, Margin);

            PenetrationPushOut.Apply(originals, displacements, hits, capsules, Margin);

            // Pushed radially (+X) to radius + margin, keeping Y; the
            // original is never moved, only the field is written.
            AssertVector(new Vector3(0.155f, 0f, 0f), displacements[0]);
            AssertVector(new Vector3(0.1f, 0.5f, 0f), originals[0]);
            Assert.AreEqual(Margin, capsules[0].SignedDistance(originals[0] + displacements[0]), Eps);
        }

        [Test]
        public void Apply_LeavesNonHitEntriesUntouched()
        {
            var capsules = SingleCapsule();
            var originals = new[]
            {
                new Vector3(1f, 0.5f, 0f),   // outside, no hit
                new Vector3(0.1f, 0.5f, 0f), // inside, hit
            };
            var displacements = new Vector3[2];
            var hits = PenetrationDetection.Scan(originals, capsules, Margin);

            PenetrationPushOut.Apply(originals, displacements, hits, capsules, Margin);

            AssertVector(Vector3.zero, displacements[0]);
            Assert.AreNotEqual(Vector3.zero, displacements[1]);
        }

        [Test]
        public void Apply_ThenRescanComposed_FindsNoPenetration()
        {
            var capsules = SingleCapsule();
            var originals = new[]
            {
                new Vector3(0f, 0.5f, 0f),      // on the axis
                new Vector3(0.1f, 0.5f, 0f),    // inside, sideways
                new Vector3(0.05f, 1.2f, 0.1f), // inside the end cap
                new Vector3(0.2f, -0.1f, -0.1f),
                new Vector3(0.3f, 0.5f, 0f),    // outside: sd = 0.05 > margin, no hit
            };
            var displacements = new Vector3[originals.Length];
            var hits = PenetrationDetection.Scan(originals, capsules, Margin);
            Assert.Greater(hits.Count, 0);

            PenetrationPushOut.Apply(originals, displacements, hits, capsules, Margin);

            // Allow a tiny float tolerance below the exact margin surface.
            var remaining = PenetrationDetection.Scan(Compose(originals, displacements), capsules, Margin - 1e-4f);
            Assert.AreEqual(0, remaining.Count);
        }

        [Test]
        public void Apply_PushesFromDisplacedPositionNotHitPosition()
        {
            var capsules = SingleCapsule();
            var originals = new[] { new Vector3(0.1f, 0.5f, 0f) };
            var displacements = new Vector3[1];
            var hits = PenetrationDetection.Scan(originals, capsules, Margin);

            // Simulate smoothing having moved the field before the re-push:
            // the displaced position sits at (0, 0.5, 0.1).
            displacements[0] = new Vector3(-0.1f, 0f, 0.1f);

            PenetrationPushOut.Apply(originals, displacements, hits, capsules, Margin);

            // Pushed along +Z (the displaced position's gradient), not +X.
            AssertVector(new Vector3(0f, 0.5f, 0.255f), Compose(originals, displacements)[0]);
        }

        [Test]
        public void Apply_NoHits_ChangesNothing()
        {
            var capsules = SingleCapsule();
            var originals = new[] { new Vector3(1f, 0.5f, 0f) };
            var displacements = new Vector3[1];

            PenetrationPushOut.Apply(originals, displacements, new List<PenetrationHit>(), capsules, Margin);

            AssertVector(Vector3.zero, displacements[0]);
        }

        static void AssertVector(Vector3 expected, Vector3 actual)
        {
            Assert.AreEqual(expected.x, actual.x, Eps, $"x of {actual}");
            Assert.AreEqual(expected.y, actual.y, Eps, $"y of {actual}");
            Assert.AreEqual(expected.z, actual.z, Eps, $"z of {actual}");
        }
    }
}
