using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using UnityEngine;

namespace VRClothDeclipper.Tests
{
    /// <summary>
    /// A throwaway benchmark (not part of the normal suite — marked Explicit)
    /// that times the mesh-SDF scan on a realistically sized body, tracking the
    /// performance the E2E gate cares about (docs/E2E_TEST_GUIDE.md,
    /// docs/DESIGN.md §6). The brute-force baseline measured ~16.4 s/scan on
    /// this mesh; the BVH + Barnes–Hut winding brought it to ~60 ms. Run on
    /// demand with: -testFilter "VRClothDeclipper.Tests.MeshSdfPerformanceTests".
    /// Results are logged with the BENCH marker for reading back from the log.
    /// </summary>
    [Explicit]
    public class MeshSdfPerformanceTests
    {
        const float A = 0.16f, B = 0.32f, C = 0.11f;

        [Test]
        public void Benchmark_Scan_OnBodySizedMesh()
        {
            // ~50k-triangle body — the scale of a real avatar torso/body mesh.
            BuildEllipsoid(out Vector3[] bodyVerts, out int[] bodyTris, 140, 180);
            // ~2k cloth vertices — a typical single garment renderer.
            BuildEllipsoid(out Vector3[] clothVerts, out _, 40, 48);
            int tris = bodyTris.Length / 3;

            var sdf = new MeshSdfCollider(bodyVerts, bodyTris);

            // Warm up the JIT and the memo's first path.
            sdf.SignedDistance(Vector3.zero);

            var sw = Stopwatch.StartNew();
            var hits = PenetrationDetection.Scan(clothVerts, sdf, 0.005f);
            sw.Stop();

            double perScanMs = sw.Elapsed.TotalMilliseconds;
            // A solve runs ~1 initial + up to 3 cycles of (scan) plus the final
            // verification scan: order ~5 scans. Push-out reuses the memo, so
            // scans dominate.
            double estSolveMs = perScanMs * 5.0;

            UnityEngine.Debug.Log(
                $"BENCH body={bodyVerts.Length}v/{tris}t cloth={clothVerts.Length}v | "
                + $"scan={perScanMs:F0} ms ({perScanMs / clothVerts.Length:F2} ms/vert) | "
                + $"est. solve≈{estSolveMs:F0} ms | hits={hits.Count}");

            Assert.Pass($"scan {perScanMs:F0} ms over {clothVerts.Length}×{tris}");
        }

        static void BuildEllipsoid(out Vector3[] verts, out int[] tris, int rings, int sectors)
        {
            var v = new List<Vector3>();
            for (int i = 0; i <= rings; i++)
            {
                float phi = Mathf.PI * i / rings;
                float sinP = Mathf.Sin(phi), cosP = Mathf.Cos(phi);
                for (int j = 0; j <= sectors; j++)
                {
                    float theta = 2f * Mathf.PI * j / sectors;
                    v.Add(new Vector3(A * sinP * Mathf.Cos(theta), B * cosP, C * sinP * Mathf.Sin(theta)));
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
    }
}
