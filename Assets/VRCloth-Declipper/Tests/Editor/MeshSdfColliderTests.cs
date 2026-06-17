using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace VRClothDeclipper.Tests
{
    public class MeshSdfColliderTests
    {
        const float Eps = 1e-4f;

        // Axis-aligned cube centered at the origin, half extent 0.5, with a
        // consistently outward-wound surface (12 triangles). Where the nearest
        // feature is a face, its signed distance matches an analytic box.
        static readonly Vector3[] CubeVerts =
        {
            new Vector3(-0.5f, -0.5f, -0.5f), // 0
            new Vector3( 0.5f, -0.5f, -0.5f), // 1
            new Vector3( 0.5f,  0.5f, -0.5f), // 2
            new Vector3(-0.5f,  0.5f, -0.5f), // 3
            new Vector3(-0.5f, -0.5f,  0.5f), // 4
            new Vector3( 0.5f, -0.5f,  0.5f), // 5
            new Vector3( 0.5f,  0.5f,  0.5f), // 6
            new Vector3(-0.5f,  0.5f,  0.5f), // 7
        };

        static readonly int[] CubeTris =
        {
            1, 2, 6, 1, 6, 5, // +X
            0, 4, 7, 0, 7, 3, // -X
            3, 7, 6, 3, 6, 2, // +Y
            0, 1, 5, 0, 5, 4, // -Y
            4, 5, 6, 4, 6, 7, // +Z
            0, 3, 2, 0, 2, 1, // -Z
        };

        static MeshSdfCollider Cube()
        {
            return new MeshSdfCollider((Vector3[])CubeVerts.Clone(), (int[])CubeTris.Clone());
        }

        [Test]
        public void SignedDistance_CenterPoint_IsNegativeHalfExtent()
        {
            Assert.AreEqual(-0.5f, Cube().SignedDistance(Vector3.zero), Eps);
        }

        [Test]
        public void SignedDistance_InsidePointNearFace_IsNegativeGapToFace()
        {
            // Nearest feature is the +X face at x = 0.5.
            Assert.AreEqual(-0.2f, Cube().SignedDistance(new Vector3(0.3f, 0f, 0f)), Eps);
        }

        [Test]
        public void SignedDistance_OutsidePoint_IsPositiveDistanceToFace()
        {
            Assert.AreEqual(0.5f, Cube().SignedDistance(new Vector3(1f, 0f, 0f)), Eps);
        }

        [Test]
        public void SignedDistance_SurfacePoint_IsZero()
        {
            Assert.AreEqual(0f, Cube().SignedDistance(new Vector3(0.5f, 0.1f, 0.1f)), Eps);
        }

        [Test]
        public void Gradient_PointsOutwardAndIsUnitLength()
        {
            var cube = Cube();

            Vector3 inside = cube.Gradient(new Vector3(0.3f, 0f, 0f));
            Assert.AreEqual(1f, inside.magnitude, Eps);
            AssertVector(Vector3.right, inside); // outward through the +X face

            Vector3 outside = cube.Gradient(new Vector3(1f, 0f, 0f));
            Assert.AreEqual(1f, outside.magnitude, Eps);
            AssertVector(Vector3.right, outside);
        }

        [Test]
        public void Gradient_MatchesNumericalDerivativeAwayFromEdges()
        {
            var cube = Cube();
            var samples = new[]
            {
                new Vector3(0.3f, 0.05f, 0.05f),  // inside, near +X face
                new Vector3(0.05f, 0.32f, 0.05f), // inside, near +Y face
                new Vector3(1.0f, 0.1f, 0.1f),    // outside +X
                new Vector3(0.1f, 0.1f, -0.9f),   // outside -Z
            };

            const float h = 1e-3f;
            foreach (var p in samples)
            {
                Vector3 analytic = cube.Gradient(p);
                var numeric = new Vector3(
                    (cube.SignedDistance(p + new Vector3(h, 0f, 0f)) - cube.SignedDistance(p - new Vector3(h, 0f, 0f))) / (2f * h),
                    (cube.SignedDistance(p + new Vector3(0f, h, 0f)) - cube.SignedDistance(p - new Vector3(0f, h, 0f))) / (2f * h),
                    (cube.SignedDistance(p + new Vector3(0f, 0f, h)) - cube.SignedDistance(p - new Vector3(0f, 0f, h))) / (2f * h));

                Assert.AreEqual(1f, analytic.magnitude, Eps, $"unit length at {p}");
                Assert.AreEqual(numeric.x, analytic.x, 2e-3f, $"x at {p}");
                Assert.AreEqual(numeric.y, analytic.y, 2e-3f, $"y at {p}");
                Assert.AreEqual(numeric.z, analytic.z, 2e-3f, $"z at {p}");
            }
        }

        [Test]
        public void PushOut_PenetratingPoint_LandsAtMargin()
        {
            var cube = Cube();
            const float margin = 0.02f;

            Vector3 pushed = cube.PushOut(new Vector3(0.3f, 0f, 0f), margin);

            AssertVector(new Vector3(0.52f, 0f, 0f), pushed);
            Assert.AreEqual(margin, cube.SignedDistance(pushed), Eps);
        }

        [Test]
        public void Contains_RespectsSurfaceAndMargin()
        {
            var cube = Cube();

            Assert.IsTrue(cube.Contains(Vector3.zero));
            Assert.IsFalse(cube.Contains(new Vector3(1f, 0f, 0f)));
            // 0.05 outside the +X face: inside only once inflated by the margin.
            Assert.IsFalse(cube.Contains(new Vector3(0.55f, 0f, 0f)));
            Assert.IsTrue(cube.Contains(new Vector3(0.55f, 0f, 0f), 0.06f));
        }

        [Test]
        public void WindingSign_SurvivesAHoleInTheMesh()
        {
            // Drop the last triangle: the surface is no longer watertight, but
            // the generalized winding number still classifies a deep-inside
            // point as inside (the missing triangle removes only a small
            // fraction of the solid angle). This is the non-watertight
            // robustness docs/DESIGN.md §9 calls for.
            var holed = new MeshSdfCollider(
                (Vector3[])CubeVerts.Clone(),
                Truncated(CubeTris, 3));

            Assert.Less(holed.SignedDistance(Vector3.zero), 0f, "deep inside must stay inside despite the hole");
            Assert.Greater(holed.SignedDistance(new Vector3(2f, 2f, 2f)), 0f, "far outside must stay outside");
        }

        [Test]
        public void EmptyMesh_IsInvalidAndNeverPenetrates()
        {
            var empty = new MeshSdfCollider(new Vector3[0], new int[0]);

            Assert.IsFalse(empty.IsValid);
            Assert.AreEqual(float.MaxValue, empty.SignedDistance(Vector3.zero));
            Assert.IsFalse(empty.Contains(Vector3.zero, 1f));
        }

        [Test]
        public void ClosestPointOnTriangle_HitsFaceEdgeAndVertexRegions()
        {
            Vector3 a = new Vector3(0f, 0f, 0f);
            Vector3 b = new Vector3(1f, 0f, 0f);
            Vector3 c = new Vector3(0f, 1f, 0f);

            // Above the interior projects onto the face.
            AssertVector(new Vector3(0.25f, 0.25f, 0f),
                MeshSdfCollider.ClosestPointOnTriangle(new Vector3(0.25f, 0.25f, 1f), a, b, c));
            // Beyond vertex a clamps to a.
            AssertVector(a, MeshSdfCollider.ClosestPointOnTriangle(new Vector3(-1f, -1f, 0f), a, b, c));
            // Beside edge ab clamps onto it.
            AssertVector(new Vector3(0.5f, 0f, 0f),
                MeshSdfCollider.ClosestPointOnTriangle(new Vector3(0.5f, -1f, 0f), a, b, c));
        }

        [Test]
        public void Accelerated_MatchesBruteForceReference_OnADenseMesh()
        {
            // The BVH closest point is exact and the Barnes–Hut winding stays
            // correct near the surface, so the accelerated signed distance must
            // agree with the unaccelerated reference everywhere.
            BuildSphere(out Vector3[] verts, out int[] tris, 0.2f, 18, 24);
            var collider = new MeshSdfCollider(verts, tris);

            var rng = new System.Random(12345);
            int checks = 0;
            for (int i = 0; i < 400; i++)
            {
                var p = new Vector3(
                    (float)(rng.NextDouble() * 0.8 - 0.4),
                    (float)(rng.NextDouble() * 0.8 - 0.4),
                    (float)(rng.NextDouble() * 0.8 - 0.4));
                float accel = collider.SignedDistance(p);
                float brute = MeshSdfCollider.SignedDistanceBruteForce(verts, tris, p);
                Assert.AreEqual(brute, accel, 1e-4f, $"signed distance mismatch at {p}");
                checks++;
            }
            Assert.Greater(checks, 0);
        }

        [Test]
        public void Solver_ColliderOverload_RemovesPenetration()
        {
            var cube = Cube();
            // A cloth triangle whose first vertex is buried in the cube.
            var positions = new[]
            {
                new Vector3(0.3f, 0f, 0f),    // inside
                new Vector3(0.8f, 0.2f, 0f),  // outside
                new Vector3(0.8f, -0.2f, 0f), // outside
            };
            var triangles = new[] { 0, 1, 2 };

            var result = PenetrationSolver.Solve(positions, triangles, cube, 0.01f);

            Assert.Greater(result.initialHitCount, 0, "the buried vertex should be detected");
            Assert.AreEqual(0, result.finalHitCount, "no vertex should remain penetrating");
            Assert.GreaterOrEqual(cube.SignedDistance(positions[0]), 0.01f - 1e-3f, "the vertex ends on the margin surface");
        }

        static void BuildSphere(out Vector3[] verts, out int[] tris, float radius, int rings, int sectors)
        {
            var v = new List<Vector3>();
            for (int i = 0; i <= rings; i++)
            {
                float phi = Mathf.PI * i / rings;
                float sinP = Mathf.Sin(phi), cosP = Mathf.Cos(phi);
                for (int j = 0; j <= sectors; j++)
                {
                    float theta = 2f * Mathf.PI * j / sectors;
                    v.Add(new Vector3(radius * sinP * Mathf.Cos(theta), radius * cosP, radius * sinP * Mathf.Sin(theta)));
                }
            }
            var t = new List<int>();
            int stride = sectors + 1;
            for (int i = 0; i < rings; i++)
            {
                for (int j = 0; j < sectors; j++)
                {
                    int a = i * stride + j, b = a + 1, c = a + stride, d = c + 1;
                    t.Add(a); t.Add(c); t.Add(b);
                    t.Add(b); t.Add(c); t.Add(d);
                }
            }
            verts = v.ToArray();
            tris = t.ToArray();
        }

        static int[] Truncated(int[] tris, int dropTriangles)
        {
            int keep = tris.Length - dropTriangles * 3;
            var result = new int[keep];
            System.Array.Copy(tris, result, keep);
            return result;
        }

        static void AssertVector(Vector3 expected, Vector3 actual)
        {
            Assert.AreEqual(expected.x, actual.x, Eps, "x");
            Assert.AreEqual(expected.y, actual.y, Eps, "y");
            Assert.AreEqual(expected.z, actual.z, Eps, "z");
        }
    }

    public class CapsuleBodyColliderTests
    {
        const float Eps = 1e-5f;

        [Test]
        public void SignedDistance_IsMinimumOverCapsules()
        {
            var capsules = new List<BodyCapsule>
            {
                new BodyCapsule(Vector3.zero, new Vector3(0f, 1f, 0f), 0.25f),
                new BodyCapsule(new Vector3(1f, 0f, 0f), new Vector3(1f, 1f, 0f), 0.25f),
            };
            var collider = new CapsuleBodyCollider(capsules);

            var p = new Vector3(0.9f, 0.5f, 0f); // closest to the second capsule
            Assert.AreEqual(capsules[1].SignedDistance(p), collider.SignedDistance(p), Eps);
            Assert.AreEqual(1, collider.ClosestIndex(p));
        }

        [Test]
        public void GradientAndThickness_ComeFromClosestCapsule()
        {
            var capsules = new List<BodyCapsule>
            {
                new BodyCapsule(Vector3.zero, new Vector3(0f, 1f, 0f), 0.2f),
                new BodyCapsule(new Vector3(1f, 0f, 0f), new Vector3(1f, 1f, 0f), 0.3f),
            };
            var collider = new CapsuleBodyCollider(capsules);

            var p = new Vector3(0.1f, 0.5f, 0f); // closest to the first capsule
            AssertVector(capsules[0].Gradient(p), collider.Gradient(p));
            Assert.AreEqual(0.2f, collider.LocalThickness(p), Eps);
        }

        [Test]
        public void Empty_HasNoClosestIndex()
        {
            var collider = new CapsuleBodyCollider(new List<BodyCapsule>());
            Assert.AreEqual(-1, collider.ClosestIndex(Vector3.zero));
            Assert.AreEqual(float.MaxValue, collider.SignedDistance(Vector3.zero));
        }

        static void AssertVector(Vector3 expected, Vector3 actual)
        {
            Assert.AreEqual(expected.x, actual.x, Eps, "x");
            Assert.AreEqual(expected.y, actual.y, Eps, "y");
            Assert.AreEqual(expected.z, actual.z, Eps, "z");
        }
    }
}
