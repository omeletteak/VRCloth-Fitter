using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace VRClothDeclipper.Tests
{
    public class PenetrationDetectionTests
    {
        const float Eps = 1e-5f;

        // Capsule along the Y axis from origin to (0,1,0), radius 0.25.
        static List<BodyCapsule> SingleCapsule()
        {
            return new List<BodyCapsule> { new BodyCapsule(Vector3.zero, new Vector3(0f, 1f, 0f), 0.25f) };
        }

        [Test]
        public void Scan_InsideVertex_IsDetected()
        {
            var positions = new[] { new Vector3(0.1f, 0.5f, 0f) }; // signed distance -0.15
            var hits = PenetrationDetection.Scan(positions, SingleCapsule(), 0.005f);

            Assert.AreEqual(1, hits.Count);
            Assert.AreEqual(0, hits[0].vertexIndex);
            Assert.AreEqual(0, hits[0].capsuleIndex);
            Assert.AreEqual(0.155f, hits[0].depth, Eps); // margin - signed distance
            Assert.AreEqual(positions[0], hits[0].position);
        }

        [Test]
        public void Scan_FarOutsideVertex_IsNotDetected()
        {
            var positions = new[] { new Vector3(1f, 0.5f, 0f) }; // signed distance 0.75
            var hits = PenetrationDetection.Scan(positions, SingleCapsule(), 0.005f);

            Assert.AreEqual(0, hits.Count);
        }

        [Test]
        public void Scan_VertexWithinMarginBand_IsDetected()
        {
            // Signed distance 0.05: outside the surface but inside a 0.1 margin.
            var positions = new[] { new Vector3(0.3f, 0.5f, 0f) };
            var hits = PenetrationDetection.Scan(positions, SingleCapsule(), 0.1f);

            Assert.AreEqual(1, hits.Count);
            Assert.AreEqual(0.05f, hits[0].depth, Eps);
        }

        [Test]
        public void Scan_SurfaceVertexWithZeroMargin_IsNotDetected()
        {
            var positions = new[] { new Vector3(0.25f, 0.5f, 0f) }; // signed distance 0
            var hits = PenetrationDetection.Scan(positions, SingleCapsule(), 0f);

            Assert.AreEqual(0, hits.Count);
        }

        [Test]
        public void Scan_PicksClosestCapsule()
        {
            var capsules = new List<BodyCapsule>
            {
                new BodyCapsule(new Vector3(10f, 0f, 0f), new Vector3(10f, 1f, 0f), 0.25f), // far
                new BodyCapsule(Vector3.zero, new Vector3(0f, 1f, 0f), 0.25f),              // close
            };
            var positions = new[] { new Vector3(0.1f, 0.5f, 0f) };
            var hits = PenetrationDetection.Scan(positions, capsules, 0.005f);

            Assert.AreEqual(1, hits.Count);
            Assert.AreEqual(1, hits[0].capsuleIndex);
        }

        [Test]
        public void Scan_MixedVertices_DetectsOnlyPenetrating()
        {
            var positions = new[]
            {
                new Vector3(0f, 0.5f, 0f),   // deep inside (on the axis)
                new Vector3(0.5f, 0.5f, 0f), // outside
                new Vector3(0.2f, 0.5f, 0f), // inside
            };
            var hits = PenetrationDetection.Scan(positions, SingleCapsule(), 0.005f);

            Assert.AreEqual(2, hits.Count);
            Assert.AreEqual(0, hits[0].vertexIndex);
            Assert.AreEqual(2, hits[1].vertexIndex);
        }

        [Test]
        public void Scan_NoCapsules_ReturnsNoHits()
        {
            var positions = new[] { Vector3.zero };
            var hits = PenetrationDetection.Scan(positions, new List<BodyCapsule>(), 0.005f);

            Assert.AreEqual(0, hits.Count);
        }
    }
}
