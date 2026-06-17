using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace VRClothDeclipper.Tests
{
    /// <summary>
    /// The synthetic equivalent of the E2E null test (docs/E2E_TEST_GUIDE.md
    /// 手順0/0b), run in memory so it executes without a GUI or avatar assets.
    ///
    /// A torso-like ellipsoid (wide in X, deep — thinner — in Z, elongated in
    /// Y) stands in for the body; "cloth" is its own surface offset outward by
    /// a clearance larger than the detection margin, i.e. a perfectly fitting
    /// garment that should register no penetration. This pins the design claim
    /// behind docs/DESIGN.md §6/§9: a circular-section capsule cannot match the
    /// ellipse, so it reports false penetration on the thin axis and the caps,
    /// while the mesh-SDF collider built from the same body stays clean.
    /// </summary>
    public class BodyShapeNullTestTests
    {
        const float A = 0.15f;  // X half-axis (wide)
        const float B = 0.30f;  // Y half-axis (tall)
        const float C = 0.10f;  // Z half-axis (thin/deep)
        const float Clearance = 0.008f; // garment sits 8 mm off the body
        const float Margin = 0.005f;    // 5 mm detection margin (< clearance)

        [Test]
        public void NullTest_CapsuleReportsFalsePenetration_MeshSdfStaysClean()
        {
            BuildEllipsoid(out Vector3[] bodyVerts, out int[] bodyTris, out Vector3[] bodyNormals, 16, 24);
            Vector3[] cloth = OffsetAlongNormals(bodyVerts, bodyNormals, Clearance);

            // Capsule the radius estimator might land on for this body: between
            // the thin (0.10) and wide (0.15) half-axes. Whatever single radius
            // is chosen, a circular section cannot fit an ellipse.
            var capsule = new BodyCapsule(new Vector3(0f, -B, 0f), new Vector3(0f, B, 0f), 0.5f * (A + C));
            var capsuleHits = PenetrationDetection.Scan(cloth, new[] { capsule }, Margin);

            var sdf = new MeshSdfCollider(bodyVerts, bodyTris);
            var sdfHits = PenetrationDetection.Scan(cloth, sdf, Margin);

            float capsuleRatio = (float)capsuleHits.Count / cloth.Length;
            float sdfRatio = (float)sdfHits.Count / cloth.Length;

            // The capsule fabricates a substantial false-penetration patch...
            Assert.Greater(capsuleRatio, 0.15f,
                $"circular capsule should mis-flag the ellipse's thin axis/caps as penetrating (got {capsuleRatio:P1})");
            // ...while the mesh SDF, matching the true surface, stays ~clean.
            Assert.Less(sdfRatio, 0.02f,
                $"mesh SDF should report a clean null test (got {sdfRatio:P1})");
        }

        [Test]
        public void MeshSdf_SignedDistanceTracksTheTrueEllipsoidSurface()
        {
            BuildEllipsoid(out Vector3[] verts, out int[] tris, out _, 24, 32);
            var sdf = new MeshSdfCollider(verts, tris);

            // Deep inside is clearly negative; far outside clearly positive.
            Assert.Less(sdf.SignedDistance(Vector3.zero), -0.08f);
            Assert.Greater(sdf.SignedDistance(new Vector3(0.5f, 0f, 0f)), 0.3f);

            // A point 2 cm outside the thin (Z) pole reads ~+2 cm (facet
            // approximation keeps it within a couple mm of the true distance).
            float sd = sdf.SignedDistance(new Vector3(0f, 0f, C + 0.02f));
            Assert.AreEqual(0.02f, sd, 0.003f);
        }

        [Test]
        public void CrossFit_LocalBodyBulge_IsSolvedByMeshSdf()
        {
            // The synthetic equivalent of 手順1 (cross-fitting): a garment that
            // fits the base body, worn over a body with a localized bulge (a
            // bigger chest), so it penetrates only over that patch — the
            // "半径方向・局所・まだら" difference docs/DESIGN.md §9 supports.
            BuildEllipsoid(out Vector3[] bodyVerts, out int[] bodyTris, out Vector3[] normals, 20, 28);
            Vector3[] cloth = OffsetAlongNormals(bodyVerts, normals, Clearance);
            int[] clothTris = (int[])bodyTris.Clone();

            // Bulge the front-mid of the body past the garment by ~1 cm so the
            // cloth ends up ~1 cm inside there (yellow-class depth, not red).
            for (int i = 0; i < bodyVerts.Length; i++)
            {
                Vector3 d = normals[i];
                if (d.z > 0.5f && Mathf.Abs(d.y) < 0.35f)
                {
                    bodyVerts[i] += normals[i] * 0.018f;
                }
            }
            var body = new MeshSdfCollider(bodyVerts, bodyTris);

            var hitsBefore = PenetrationDetection.Scan(cloth, body, Margin);
            Assert.Greater(hitsBefore.Count, 0, "the bulge should push the garment into the body");

            // Preflight: a local bulge of this size is in scope (not retargeting).
            var report = PreflightDiagnostic.Evaluate(cloth, clothTris, hitsBefore, body, Margin);
            Assert.AreNotEqual(PreflightVerdict.Red, report.verdict,
                $"a ~1 cm local bulge should stay in scope, got {report.verdict} (maxDepth {report.maxDepth * 1000f:F1} mm, ratio {report.penetratingRatio:P1})");

            var result = PenetrationSolver.Solve(cloth, clothTris, body, Margin);
            Assert.Greater(result.initialHitCount, 0);
            Assert.AreEqual(0, result.finalHitCount, "the solver should clear every penetration");

            // Every cloth vertex now rests on or above the margin surface.
            int stillInside = 0;
            for (int i = 0; i < cloth.Length; i++)
            {
                if (body.SignedDistance(cloth[i]) < Margin - 1e-3f)
                {
                    stillInside++;
                }
            }
            Assert.AreEqual(0, stillInside, "no cloth vertex should remain inside the body after the solve");
        }

        // --- helpers -------------------------------------------------------

        /// <summary>
        /// A UV-sphere tessellation scaled to an ellipsoid (a, b, c). Returns
        /// vertices, a consistently wound triangle list, and outward unit
        /// normals (gradient of the implicit ellipsoid).
        /// </summary>
        static void BuildEllipsoid(out Vector3[] verts, out int[] tris, out Vector3[] normals, int rings, int sectors)
        {
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            for (int i = 0; i <= rings; i++)
            {
                float phi = Mathf.PI * i / rings;        // 0..π (pole to pole)
                float sinP = Mathf.Sin(phi), cosP = Mathf.Cos(phi);
                for (int j = 0; j <= sectors; j++)
                {
                    float theta = 2f * Mathf.PI * j / sectors;
                    float dx = sinP * Mathf.Cos(theta);
                    float dy = cosP;
                    float dz = sinP * Mathf.Sin(theta);
                    v.Add(new Vector3(A * dx, B * dy, C * dz));
                    n.Add(new Vector3(dx / A, dy / B, dz / C).normalized);
                }
            }

            var t = new List<int>();
            int stride = sectors + 1;
            for (int i = 0; i < rings; i++)
            {
                for (int j = 0; j < sectors; j++)
                {
                    int a = i * stride + j;
                    int b = a + 1;
                    int c = a + stride;
                    int d = c + 1;
                    t.Add(a); t.Add(c); t.Add(b);
                    t.Add(b); t.Add(c); t.Add(d);
                }
            }

            verts = v.ToArray();
            normals = n.ToArray();
            tris = t.ToArray();
        }

        static Vector3[] OffsetAlongNormals(Vector3[] verts, Vector3[] normals, float distance)
        {
            var result = new Vector3[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                result[i] = verts[i] + normals[i] * distance;
            }
            return result;
        }
    }
}
