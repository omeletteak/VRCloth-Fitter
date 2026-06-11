using NUnit.Framework;
using UnityEngine;

namespace VRClothFitter.Tests
{
    public class BodyCapsuleTests
    {
        const float Eps = 1e-5f;

        // Capsule along the Y axis from origin to (0,1,0), radius 0.25.
        static BodyCapsule MakeCapsule()
        {
            return new BodyCapsule(Vector3.zero, new Vector3(0f, 1f, 0f), 0.25f);
        }

        [Test]
        public void SignedDistance_InsidePoint_IsNegative()
        {
            var capsule = MakeCapsule();
            float sd = capsule.SignedDistance(new Vector3(0.1f, 0.5f, 0f));
            Assert.AreEqual(-0.15f, sd, Eps);
        }

        [Test]
        public void SignedDistance_OutsidePoint_IsPositive()
        {
            var capsule = MakeCapsule();
            float sd = capsule.SignedDistance(new Vector3(0.5f, 0.5f, 0f));
            Assert.AreEqual(0.25f, sd, Eps);
        }

        [Test]
        public void SignedDistance_SurfacePoint_IsZero()
        {
            var capsule = MakeCapsule();
            float sd = capsule.SignedDistance(new Vector3(0.25f, 0.3f, 0f));
            Assert.AreEqual(0f, sd, Eps);
        }

        [Test]
        public void SignedDistance_BeyondEndCap_UsesSphericalDistance()
        {
            var capsule = MakeCapsule();

            // Straight above the end cap.
            Assert.AreEqual(0.25f, capsule.SignedDistance(new Vector3(0f, 1.5f, 0f)), Eps);

            // Diagonal from the end cap: distance to (0,1,0) is 0.5.
            Assert.AreEqual(0.25f, capsule.SignedDistance(new Vector3(0.3f, 1.4f, 0f)), Eps);
        }

        [Test]
        public void ClosestPointOnAxis_ClampsToSegment()
        {
            var capsule = MakeCapsule();

            AssertVector(new Vector3(0f, 1f, 0f), capsule.ClosestPointOnAxis(new Vector3(0.2f, 2f, 0f)));
            AssertVector(Vector3.zero, capsule.ClosestPointOnAxis(new Vector3(0.2f, -3f, 0f)));
            AssertVector(new Vector3(0f, 0.7f, 0f), capsule.ClosestPointOnAxis(new Vector3(0.4f, 0.7f, 0f)));
        }

        [Test]
        public void DegenerateCapsule_ActsAsSphere()
        {
            var center = new Vector3(1f, 1f, 1f);
            var sphere = new BodyCapsule(center, center, 0.5f);

            AssertVector(center, sphere.ClosestPointOnAxis(new Vector3(1f, 1f, 2f)));
            Assert.AreEqual(0.5f, sphere.SignedDistance(new Vector3(1f, 1f, 2f)), Eps);
            Assert.AreEqual(-0.5f, sphere.SignedDistance(center), Eps);
        }

        [Test]
        public void Contains_RespectsMargin()
        {
            var capsule = MakeCapsule();
            var nearSurface = new Vector3(0.35f, 0.5f, 0f); // signed distance +0.10

            Assert.IsFalse(capsule.Contains(nearSurface));
            Assert.IsTrue(capsule.Contains(nearSurface, 0.15f));
            Assert.IsTrue(capsule.Contains(new Vector3(0f, 0.5f, 0.1f)));
            Assert.IsFalse(capsule.Contains(new Vector3(0.5f, 0.5f, 0f)));
        }

        [Test]
        public void PushOut_PenetratingPoint_LandsAtMargin()
        {
            var capsule = MakeCapsule();
            const float margin = 0.02f;

            Vector3 pushed = capsule.PushOut(new Vector3(0.05f, 0.5f, 0f), margin);

            AssertVector(new Vector3(0.27f, 0.5f, 0f), pushed);
            Assert.AreEqual(margin, capsule.SignedDistance(pushed), Eps);
        }

        [Test]
        public void PushOut_OnAxisPoint_StillEscapes()
        {
            var capsule = MakeCapsule();
            const float margin = 0.02f;

            Vector3 pushed = capsule.PushOut(new Vector3(0f, 0.5f, 0f), margin);

            // Must land exactly margin above the surface, staying on the same
            // cross-section plane (pushed perpendicular to the axis).
            Assert.AreEqual(margin, capsule.SignedDistance(pushed), Eps);
            Assert.AreEqual(0.5f, pushed.y, Eps);
        }

        [Test]
        public void PushOut_DegenerateOnCenter_StillEscapes()
        {
            var center = new Vector3(1f, 1f, 1f);
            var sphere = new BodyCapsule(center, center, 0.5f);

            Vector3 pushed = sphere.PushOut(center, 0.01f);

            Assert.AreEqual(0.01f, sphere.SignedDistance(pushed), Eps);
        }

        static void AssertVector(Vector3 expected, Vector3 actual)
        {
            Assert.AreEqual(expected.x, actual.x, Eps, "x");
            Assert.AreEqual(expected.y, actual.y, Eps, "y");
            Assert.AreEqual(expected.z, actual.z, Eps, "z");
        }
    }
}
