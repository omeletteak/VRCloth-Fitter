using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace VRClothDeclipper.Tests
{
    public class PenetrationSolverTests
    {
        const float Margin = 0.005f;

        // Capsule along the Y axis from origin to (0,1,0), radius 0.25.
        static List<BodyCapsule> SingleCapsule()
        {
            return new List<BodyCapsule> { new BodyCapsule(Vector3.zero, new Vector3(0f, 1f, 0f), 0.25f) };
        }

        // A flat cloth sheet in the XZ plane at y=0.5, centered on the
        // capsule axis, so its middle vertices penetrate the capsule.
        static void MakeSheet(int n, float spacing, out Vector3[] positions, out int[] triangles)
        {
            positions = new Vector3[n * n];
            float half = (n - 1) * spacing * 0.5f;
            for (int row = 0; row < n; row++)
            {
                for (int col = 0; col < n; col++)
                {
                    positions[row * n + col] = new Vector3(col * spacing - half, 0.5f, row * spacing - half);
                }
            }

            var tris = new List<int>();
            for (int row = 0; row < n - 1; row++)
            {
                for (int col = 0; col < n - 1; col++)
                {
                    int v = row * n + col;
                    tris.AddRange(new[] { v, v + 1, v + n });
                    tris.AddRange(new[] { v + n, v + 1, v + n + 1 });
                }
            }
            triangles = tris.ToArray();
        }

        [Test]
        public void Solve_ResolvesAllPenetration()
        {
            MakeSheet(21, 0.05f, out var positions, out var triangles);
            var capsules = SingleCapsule();

            var result = PenetrationSolver.Solve(positions, triangles, capsules, Margin);

            Assert.Greater(result.initialHitCount, 0);
            Assert.AreEqual(0, result.finalHitCount);
            Assert.AreEqual(0, PenetrationDetection.Scan(positions, capsules, Margin - 1e-4f).Count);
        }

        [Test]
        public void Solve_LeavesFarVerticesUntouched()
        {
            MakeSheet(21, 0.05f, out var positions, out var triangles);
            Vector3 corner = positions[0];

            PenetrationSolver.Solve(positions, triangles, SingleCapsule(), Margin);

            Assert.AreEqual(corner, positions[0]);
        }

        [Test]
        public void Solve_ProducesSmootherResultThanPushOutAlone()
        {
            MakeSheet(21, 0.05f, out var pushed, out var triangles);
            MakeSheet(21, 0.05f, out var solved, out _);
            var capsules = SingleCapsule();

            var hits = PenetrationDetection.Scan(pushed, capsules, Margin);
            var displacements = new Vector3[pushed.Length];
            PenetrationPushOut.Apply(pushed, displacements, hits, capsules, Margin);
            for (int v = 0; v < pushed.Length; v++)
            {
                pushed[v] += displacements[v];
            }
            PenetrationSolver.Solve(solved, triangles, capsules, Margin);

            float pushedMaxEdge = MaxEdgeLength(pushed, triangles);
            float solvedMaxEdge = MaxEdgeLength(solved, triangles);
            Assert.Less(solvedMaxEdge, pushedMaxEdge,
                $"solver should shorten the longest stretched edge (push-out only: {pushedMaxEdge}, solver: {solvedMaxEdge})");
        }

        // A wavy vertical sheet grazing the capsule's side: the wave troughs
        // dip a few centimeters in, while the wave amplitude itself is larger
        // than any penetration depth.
        static void MakeWavySheet(int n, out Vector3[] positions, out int[] triangles)
        {
            positions = new Vector3[n * n];
            for (int row = 0; row < n; row++)
            {
                for (int col = 0; col < n; col++)
                {
                    float y = 0.3f + row * 0.02f;
                    float z = -0.2f + col * 0.02f;
                    float x = 0.29f + 0.06f * Mathf.Sin(row * 0.9f) * Mathf.Sin(col * 1.1f);
                    positions[row * n + col] = new Vector3(x, y, z);
                }
            }
            MakeSheet(n, 0.02f, out _, out triangles); // same grid topology
        }

        [Test]
        public void Solve_OnWavySheet_MovesNoVertexFartherThanTheCorrectionScale()
        {
            MakeWavySheet(21, out var positions, out var triangles);
            var originals = (Vector3[])positions.Clone();
            var capsules = SingleCapsule();

            var initialHits = PenetrationDetection.Scan(positions, capsules, Margin);
            Assert.Greater(initialHits.Count, 0, "test setup should start with penetration");
            float maxDepth = 0f;
            foreach (var hit in initialHits)
            {
                maxDepth = Mathf.Max(maxDepth, hit.depth);
            }

            var result = PenetrationSolver.Solve(positions, triangles, capsules, Margin);

            Assert.AreEqual(0, result.finalHitCount);
            // Smoothing the displacement field (not the positions) keeps every
            // move on the order of the correction itself: the wavy cloth
            // detail must not be flattened toward neighbor averages, so no
            // vertex may travel anywhere near the wave amplitude.
            float maxMove = 0f;
            for (int v = 0; v < positions.Length; v++)
            {
                maxMove = Mathf.Max(maxMove, Vector3.Distance(originals[v], positions[v]));
            }
            Assert.LessOrEqual(maxMove, 2f * maxDepth,
                $"moves should stay on the correction scale (max depth {maxDepth}, max move {maxMove})");
        }

        [Test]
        public void Solve_WithoutPenetration_ReportsZeroAndChangesNothing()
        {
            MakeSheet(5, 0.05f, out var positions, out var triangles);
            for (int i = 0; i < positions.Length; i++)
            {
                positions[i] += new Vector3(0f, 2f, 0f); // far above the capsule
            }
            var before = (Vector3[])positions.Clone();

            var result = PenetrationSolver.Solve(positions, triangles, SingleCapsule(), Margin);

            Assert.AreEqual(0, result.initialHitCount);
            Assert.AreEqual(0, result.passes);
            CollectionAssert.AreEqual(before, positions);
        }

        [Test]
        public void SolveProjected_ResolvesAllPenetration()
        {
            MakeSheet(21, 0.05f, out var positions, out var triangles);
            var capsules = SingleCapsule();

            var result = PenetrationSolver.SolveProjected(positions, triangles, capsules, Margin);

            Assert.Greater(result.initialHitCount, 0);
            Assert.AreEqual(0, result.finalHitCount);
            Assert.AreEqual(0, PenetrationDetection.Scan(positions, capsules, Margin - 1e-4f).Count);
        }

        [Test]
        public void SolveProjected_LeavesFarVerticesUntouched()
        {
            MakeSheet(21, 0.05f, out var positions, out var triangles);
            Vector3 corner = positions[0];

            PenetrationSolver.SolveProjected(positions, triangles, SingleCapsule(), Margin);

            Assert.AreEqual(corner, positions[0]);
        }

        [Test]
        public void SolveProjected_ProducesSmootherResultThanPushOutAlone()
        {
            MakeSheet(21, 0.05f, out var pushed, out var triangles);
            MakeSheet(21, 0.05f, out var solved, out _);
            var capsules = SingleCapsule();

            var hits = PenetrationDetection.Scan(pushed, capsules, Margin);
            var displacements = new Vector3[pushed.Length];
            PenetrationPushOut.Apply(pushed, displacements, hits, capsules, Margin);
            for (int v = 0; v < pushed.Length; v++)
            {
                pushed[v] += displacements[v];
            }
            PenetrationSolver.SolveProjected(solved, triangles, capsules, Margin);

            Assert.Less(MaxEdgeLength(solved, triangles), MaxEdgeLength(pushed, triangles),
                "projected solve should shorten the longest stretched edge vs push-out only");
        }

        // The point of the normal/tangent split: because every iteration ends
        // by re-projecting penetrating vertices to the margin surface, adding
        // smoothing iterations only smooths more — it never lets the field sink
        // back into the body. So penetration stays resolved across a wide range
        // of iteration counts, unlike the coarse solve's λ/pass balancing act.
        [Test]
        public void SolveProjected_StaysResolvedAcrossIterationCounts()
        {
            var capsules = SingleCapsule();
            foreach (int iterations in new[] { 1, 2, 4, 8, 16, 40 })
            {
                MakeSheet(21, 0.05f, out var positions, out var triangles);
                var result = PenetrationSolver.SolveProjected(positions, triangles, capsules, Margin, iterations: iterations);

                Assert.AreEqual(0, result.finalHitCount, $"sank back at iterations={iterations}");
                Assert.AreEqual(0, PenetrationDetection.Scan(positions, capsules, Margin - 1e-4f).Count,
                    $"residual penetration at iterations={iterations}");
            }
        }

        static float MaxEdgeLength(Vector3[] positions, int[] triangles)
        {
            float max = 0f;
            for (int t = 0; t + 2 < triangles.Length; t += 3)
            {
                max = Mathf.Max(max, Vector3.Distance(positions[triangles[t]], positions[triangles[t + 1]]));
                max = Mathf.Max(max, Vector3.Distance(positions[triangles[t + 1]], positions[triangles[t + 2]]));
                max = Mathf.Max(max, Vector3.Distance(positions[triangles[t + 2]], positions[triangles[t]]));
            }
            return max;
        }
    }
}
