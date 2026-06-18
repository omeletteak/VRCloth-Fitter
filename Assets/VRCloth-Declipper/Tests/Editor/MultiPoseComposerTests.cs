using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace VRClothDeclipper.Tests
{
    /// <summary>
    /// Pins the multi-pose composition model: a single bind-pose-local delta is
    /// accumulated by sweeping the poses so every pose ends (near)
    /// penetration-free. Triangles are empty to isolate the composition from the
    /// smoothing stage — push-out only.
    /// </summary>
    public class MultiPoseComposerTests
    {
        const float Margin = 0.005f;
        static readonly int[] NoTriangles = new int[0];

        static CapsuleBodyCollider CapsuleY(float radius)
        {
            return new CapsuleBodyCollider(new List<BodyCapsule>
            {
                new BodyCapsule(Vector3.zero, new Vector3(0f, 1f, 0f), radius),
            });
        }

        static Matrix4x4[] IdentitySkins(int n)
        {
            var m = new Matrix4x4[n];
            for (int i = 0; i < n; i++)
            {
                m[i] = Matrix4x4.identity;
            }
            return m;
        }

        [Test]
        public void SinglePose_PushesPenetratingVertexClear()
        {
            var orig = new[] { new Vector3(0.24f, 0.5f, 0f), new Vector3(2f, 0.5f, 0f) };
            var pose = new PoseCapture
            {
                originalWorld = orig,
                skinMatrices = IdentitySkins(2),
                collider = CapsuleY(0.25f),
            };
            var delta = new Vector3[2];

            var result = MultiPoseComposer.Compose(delta, NoTriangles, new[] { pose }, Margin);

            Assert.AreEqual(0, result.remainingPenetrating);
            Assert.Greater(delta[0].x, 0.01f, "the penetrating vertex is pushed outward (+X)");
            Vector3 world0 = orig[0] + delta[0];
            Assert.GreaterOrEqual(pose.collider.SignedDistance(world0), Margin - 1e-3f);
            Assert.AreEqual(Vector3.zero, delta[1], "the far vertex is untouched");
        }

        [Test]
        public void TwoPoses_ComposedDeltaSatisfiesBoth_LargerRequirementDominates()
        {
            // Same cloth vertex, two body poses: pose B's body is closer (larger
            // radius), so it needs the bigger push. One static delta must clear both.
            var orig = new[] { new Vector3(0.24f, 0.5f, 0f), new Vector3(2f, 0.5f, 0f) };
            var poseA = new PoseCapture { originalWorld = orig, skinMatrices = IdentitySkins(2), collider = CapsuleY(0.25f) };
            var poseB = new PoseCapture { originalWorld = orig, skinMatrices = IdentitySkins(2), collider = CapsuleY(0.27f) };
            var delta = new Vector3[2];

            var result = MultiPoseComposer.Compose(delta, NoTriangles, new[] { poseA, poseB }, Margin);

            Assert.AreEqual(0, result.remainingPenetrating, "both poses end clear");
            Vector3 world0 = orig[0] + delta[0];
            Assert.GreaterOrEqual(poseA.collider.SignedDistance(world0), Margin - 1e-3f);
            Assert.GreaterOrEqual(poseB.collider.SignedDistance(world0), Margin - 1e-3f);
            Assert.Greater(delta[0].x, 0.03f, "delta is sized to the closer body (pose B)");
        }

        [Test]
        public void NonIdentitySkin_DeltaIsTransferredThroughInverse()
        {
            // A pose whose skinning rotates bind space 90° about Z. The composed
            // bind delta must still clear the pose once re-skinned to world.
            var skin = Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, 90f));
            var orig = new[] { new Vector3(0.24f, 0.5f, 0f), new Vector3(2f, 0.5f, 0f) };
            var pose = new PoseCapture
            {
                originalWorld = orig,
                skinMatrices = new[] { skin, skin },
                collider = CapsuleY(0.25f),
            };
            var delta = new Vector3[2];

            var result = MultiPoseComposer.Compose(delta, NoTriangles, new[] { pose }, Margin);

            Assert.AreEqual(0, result.remainingPenetrating);
            // World push is +X; expressed in bind space (rotated -90°) it points -Y.
            Assert.Less(delta[0].y, -0.01f, "the world push is rotated into bind space");
            Assert.Less(Mathf.Abs(delta[0].x), 1e-3f);
            Vector3 world0 = orig[0] + skin.MultiplyVector(delta[0]);
            Assert.GreaterOrEqual(pose.collider.SignedDistance(world0), Margin - 1e-3f);
        }

        [Test]
        public void NoPenetration_LeavesDeltaZeroAndConvergesImmediately()
        {
            var orig = new[] { new Vector3(2f, 0.5f, 0f), new Vector3(2.1f, 0.5f, 0f) };
            var pose = new PoseCapture { originalWorld = orig, skinMatrices = IdentitySkins(2), collider = CapsuleY(0.25f) };
            var delta = new Vector3[2];

            var result = MultiPoseComposer.Compose(delta, NoTriangles, new[] { pose }, Margin);

            Assert.AreEqual(0, result.remainingPenetrating);
            Assert.AreEqual(1, result.rounds, "a clear sweep breaks the loop");
            Assert.AreEqual(Vector3.zero, delta[0]);
            Assert.AreEqual(Vector3.zero, delta[1]);
        }

        [Test]
        public void NullOrEmptyInputs_AreNoOps()
        {
            Assert.AreEqual(0, MultiPoseComposer.Compose(null, NoTriangles, new List<PoseCapture>(), Margin).rounds);
            var delta = new Vector3[1];
            Assert.AreEqual(0, MultiPoseComposer.Compose(delta, NoTriangles, null, Margin).rounds);
            Assert.AreEqual(0, MultiPoseComposer.Compose(delta, NoTriangles, new List<PoseCapture>(), Margin).rounds);
        }
    }
}
