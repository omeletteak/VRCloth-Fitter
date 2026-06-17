using NUnit.Framework;
using UnityEngine;

namespace VRClothDeclipper.Tests
{
    public class SkinningMathTests
    {
        const float Eps = 1e-4f;

        static Matrix4x4[] TwoBones()
        {
            return new[]
            {
                Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, 0f, 30f), Vector3.one),
                Matrix4x4.TRS(new Vector3(0.5f, 0f, 0f), Quaternion.Euler(0f, 45f, 0f), Vector3.one * 1.2f),
            };
        }

        static Matrix4x4[] TwoBindPoses()
        {
            return new[]
            {
                Matrix4x4.TRS(new Vector3(0f, -0.5f, 0f), Quaternion.identity, Vector3.one),
                Matrix4x4.TRS(new Vector3(0.1f, 0f, 0f), Quaternion.Euler(10f, 0f, 0f), Vector3.one),
            };
        }

        [Test]
        public void BlendedSkinMatrix_SingleBone_IsBoneTimesBindPose()
        {
            var bones = TwoBones();
            var binds = TwoBindPoses();
            var weight = new BoneWeight { boneIndex0 = 1, weight0 = 1f };

            Matrix4x4 blended = SkinningMath.BlendedSkinMatrix(weight, bones, binds);
            Matrix4x4 expected = bones[1] * binds[1];

            for (int i = 0; i < 16; i++)
            {
                Assert.AreEqual(expected[i], blended[i], Eps, $"component {i}");
            }
        }

        [Test]
        public void WorldToMeshLocal_InvertsForwardSkinning()
        {
            var bones = TwoBones();
            var binds = TwoBindPoses();
            var weights = new[]
            {
                new BoneWeight { boneIndex0 = 0, weight0 = 1f },
                new BoneWeight { boneIndex0 = 0, weight0 = 0.3f, boneIndex1 = 1, weight1 = 0.7f },
                new BoneWeight { boneIndex0 = 1, weight0 = 1f },
            };
            var locals = new[]
            {
                new Vector3(0.1f, 0.2f, 0.3f),
                new Vector3(-0.2f, 0.5f, 0f),
                new Vector3(0f, 0.05f, -0.4f),
            };

            var world = new Vector3[locals.Length];
            for (int v = 0; v < locals.Length; v++)
            {
                world[v] = SkinningMath.BlendedSkinMatrix(weights[v], bones, binds).MultiplyPoint3x4(locals[v]);
            }

            var roundTripped = SkinningMath.WorldToMeshLocal(world, weights, bones, binds, Matrix4x4.identity);

            for (int v = 0; v < locals.Length; v++)
            {
                Assert.AreEqual(locals[v].x, roundTripped[v].x, Eps, $"x of vertex {v}");
                Assert.AreEqual(locals[v].y, roundTripped[v].y, Eps, $"y of vertex {v}");
                Assert.AreEqual(locals[v].z, roundTripped[v].z, Eps, $"z of vertex {v}");
            }
        }

        [Test]
        public void WorldToMeshLocal_WithoutWeights_FallsBackToRendererMatrix()
        {
            var world = new[] { new Vector3(2f, 1f, 0f) };
            Matrix4x4 rendererWorldToLocal = Matrix4x4.TRS(new Vector3(1f, 0f, 0f), Quaternion.identity, Vector3.one).inverse;

            var local = SkinningMath.WorldToMeshLocal(world, null, null, null, rendererWorldToLocal);

            Assert.AreEqual(1f, local[0].x, Eps);
            Assert.AreEqual(1f, local[0].y, Eps);
            Assert.AreEqual(0f, local[0].z, Eps);
        }

        [Test]
        public void WorldToMeshLocal_ZeroTotalWeight_FallsBackToRendererMatrix()
        {
            var world = new[] { new Vector3(2f, 1f, 0f) };
            var weights = new[] { new BoneWeight() }; // all weights zero
            Matrix4x4 rendererWorldToLocal = Matrix4x4.TRS(new Vector3(1f, 0f, 0f), Quaternion.identity, Vector3.one).inverse;

            var local = SkinningMath.WorldToMeshLocal(world, weights, TwoBones(), TwoBindPoses(), rendererWorldToLocal);

            Assert.AreEqual(1f, local[0].x, Eps);
        }

        [Test]
        public void WorldDeltaToMeshLocal_AddsInverseSkinnedCorrectionToBase()
        {
            var bones = TwoBones();
            var binds = TwoBindPoses();
            var weights = new[]
            {
                new BoneWeight { boneIndex0 = 0, weight0 = 1f },
                new BoneWeight { boneIndex0 = 0, weight0 = 0.3f, boneIndex1 = 1, weight1 = 0.7f },
                new BoneWeight { boneIndex0 = 1, weight0 = 1f },
            };
            // The mesh's own base vertices — arbitrary and independent of the
            // captured world positions (where shape keys live).
            var baseVerts = new[]
            {
                new Vector3(0.4f, 0.1f, -0.2f),
                new Vector3(-0.3f, 0.6f, 0.1f),
                new Vector3(0.2f, -0.1f, 0.5f),
            };
            // Pre-solve local positions and a known per-vertex correction in
            // local space; both are forward-skinned to world to build the pair.
            var preLocals = new[]
            {
                new Vector3(0.1f, 0.2f, 0.3f),
                new Vector3(-0.2f, 0.5f, 0f),
                new Vector3(0f, 0.05f, -0.4f),
            };
            var deltaLocal = new[]
            {
                new Vector3(0f, 0.1f, 0f),
                new Vector3(0.05f, 0f, -0.05f),
                new Vector3(-0.1f, 0.2f, 0.1f),
            };

            var originalWorld = new Vector3[preLocals.Length];
            var fittedWorld = new Vector3[preLocals.Length];
            for (int v = 0; v < preLocals.Length; v++)
            {
                Matrix4x4 skin = SkinningMath.BlendedSkinMatrix(weights[v], bones, binds);
                originalWorld[v] = skin.MultiplyPoint3x4(preLocals[v]);
                fittedWorld[v] = skin.MultiplyPoint3x4(preLocals[v] + deltaLocal[v]);
            }

            var result = SkinningMath.WorldDeltaToMeshLocal(
                baseVerts, originalWorld, fittedWorld, weights, bones, binds, Matrix4x4.identity);

            for (int v = 0; v < baseVerts.Length; v++)
            {
                Vector3 expected = baseVerts[v] + deltaLocal[v];
                Assert.AreEqual(expected.x, result[v].x, Eps, $"x of vertex {v}");
                Assert.AreEqual(expected.y, result[v].y, Eps, $"y of vertex {v}");
                Assert.AreEqual(expected.z, result[v].z, Eps, $"z of vertex {v}");
            }
        }

        [Test]
        public void WorldDeltaToMeshLocal_MismatchedWorlds_ReturnsBaseUnchanged()
        {
            var baseVerts = new[] { new Vector3(1f, 2f, 3f), new Vector3(-1f, 0f, 0.5f) };

            var result = SkinningMath.WorldDeltaToMeshLocal(
                baseVerts, null, null, null, null, null, Matrix4x4.identity);

            Assert.AreEqual(baseVerts[0], result[0]);
            Assert.AreEqual(baseVerts[1], result[1]);
        }
    }
}
