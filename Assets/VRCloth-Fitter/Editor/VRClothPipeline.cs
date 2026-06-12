using System.Collections.Generic;
using UnityEngine;

namespace VRClothFitter
{
    public static class VRClothPipeline
    {
        public static void Run(VRClothFitter fitter)
        {
            if (fitter == null || fitter.targetAvatar == null || fitter.clothToDeform == null)
            {
                Debug.LogError("VRClothFitter: Target Avatar or Cloth is not set.");
                return;
            }

            string modeStr = fitter.mode.ToString();
            Debug.Log($"[VRClothFitter] Running in {modeStr} mode...");
            Debug.Log($"Target Avatar: {fitter.targetAvatar.name}, Cloth: {fitter.clothToDeform.name}");
            if (fitter.sourceAvatar != null)
            {
                Debug.Log($"Source Avatar: {fitter.sourceAvatar.name}");
            }

            GameObject clothRoot = fitter.clothRoot != null ? fitter.clothRoot : fitter.clothToDeform.gameObject;
            List<ClothSnapshot> cloth = VRClothMeshCapture.Capture(clothRoot);
            if (cloth.Count == 0)
            {
                Debug.LogError("VRClothFitter: No active SkinnedMeshRenderer found under the cloth root. Aborting.");
                return;
            }

            int totalVertices = 0;
            foreach (var snapshot in cloth)
            {
                totalVertices += snapshot.VertexCount;
            }
            Debug.Log($"[VRClothFitter] Captured {cloth.Count} renderer(s), {totalVertices} vertices in world space.");

            var capsules = VRClothProxyGenerator.Generate(fitter.targetAvatar);
            if (capsules == null)
            {
                Debug.LogError("Failed to generate proxy capsules. Aborting.");
                return;
            }
            VRClothDebugVisualizer.SetCapsules(capsules);

            List<PenetrationHit> hits = VRClothPenetrationDetector.Detect(cloth, capsules, fitter.margin);
            VRClothDebugVisualizer.SetHits(hits);
            Debug.Log($"[VRClothFitter] Detected {hits.Count} penetrating vertices (margin {fitter.margin:F3} m).");

            // Preflight: judge per renderer whether the body-shape difference
            // is within the supported envelope (docs/DESIGN.md §9).
            var verdicts = new PreflightVerdict[cloth.Count];
            for (int i = 0; i < cloth.Count; i++)
            {
                var snapshot = cloth[i];
                var report = PreflightDiagnostic.Evaluate(
                    snapshot.worldVertices, snapshot.triangles, snapshot.hits, capsules, fitter.margin);
                verdicts[i] = report.verdict;
                Debug.Log(FormatPreflight(snapshot.renderer.name, report));
                if (report.verdict == PreflightVerdict.Red)
                {
                    Debug.LogWarning(fitter.forceApplyOutOfRange
                        ? $"[VRClothFitter] {snapshot.renderer.name}: RED, but Force Apply (Out of Range) is enabled — applying anyway. Expect artifacts; this is retargeting-class difference (docs/DESIGN.md §9)."
                        : $"[VRClothFitter] {snapshot.renderer.name}: body-shape difference exceeds the supported range — apply will be skipped (docs/DESIGN.md §9). Enable 'Force Apply (Out of Range)' to override.");
                }
            }

            if (hits.Count > 0)
            {
                int passes = 0;
                int remaining = 0;
                int applied = 0;
                int skipped = 0;
                for (int i = 0; i < cloth.Count; i++)
                {
                    if (verdicts[i] == PreflightVerdict.Red && !fitter.forceApplyOutOfRange)
                    {
                        skipped++;
                        continue;
                    }
                    var snapshot = cloth[i];
                    var result = PenetrationSolver.Solve(snapshot.worldVertices, snapshot.triangles, capsules, fitter.margin);
                    passes = Mathf.Max(passes, result.passes);
                    remaining += result.finalHitCount;
                    if (result.initialHitCount > 0)
                    {
                        VRClothMeshApplier.Apply(snapshot);
                        applied++;
                    }
                }
                Debug.Log($"[VRClothFitter] Push-out + smoothing finished after {passes} pass(es); {remaining} vertices still penetrating.");
                Debug.Log($"[VRClothFitter] Applied fitted mesh copies to {applied} renderer(s)"
                    + (skipped > 0 ? $", skipped {skipped} out-of-range renderer(s)" : "")
                    + ". Originals untouched; Undo (Ctrl+Z) restores.");
            }

            Debug.Log("[VRClothFitter] Process complete.");
        }

        static string FormatPreflight(string rendererName, PreflightReport report)
        {
            string verdict = report.verdict.ToString().ToUpperInvariant();
            return $"[VRClothFitter] Preflight {rendererName}: {verdict} — "
                + $"penetrating {report.penetratingCount}/{report.vertexCount} verts ({report.penetratingRatio:P1}), "
                + $"max {report.maxDepth * 1000f:F1} mm below surface ({report.maxDepthOverRadius:P0} of capsule radius), "
                + $"p95 {report.p95Depth * 1000f:F1} mm, largest patch {report.largestPatchRatio:P1}, "
                + $"margin-zone hits {report.hitCount}.";
        }
    }
}
