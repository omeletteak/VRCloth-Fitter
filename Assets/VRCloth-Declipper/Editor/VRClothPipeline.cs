using System.Collections.Generic;
using UnityEngine;

namespace VRClothDeclipper
{
    public static class VRClothPipeline
    {
        /// <summary>
        /// Everything the pipeline computes before solving: the captured cloth,
        /// the body proxy, the chosen collider backend, the detected hits and the
        /// per-renderer preflight reports. Shared by <see cref="Run"/> and the
        /// headless preflight CLI (<see cref="VRClothPreflightCli"/>) so both
        /// judge identically.
        /// </summary>
        public class PreflightResult
        {
            public List<ClothSnapshot> cloth;
            public List<BodyCapsule> capsules;
            public IBodyCollider collider;
            public string backend;
            public List<PenetrationHit> hits;
            public PreflightReport[] reports;

            /// <summary>
            /// Fraction of proxy capsules that found body geometry to measure from
            /// (<see cref="BodyModelConfidence.Coverage"/>). −1 when radius
            /// estimation was off, so no coverage could be judged.
            /// </summary>
            public float bodyCoverage;

            /// <summary>
            /// True when the body model covers too little of the skeleton to trust
            /// a GREEN verdict — the false-green guard (docs/DIAGNOSTIC_HONESTY.md
            /// §1). Set only when radius estimation ran.
            /// </summary>
            public bool bodyModelLowConfidence;
        }

        /// <summary>
        /// Capture → proxy → detect → preflight, with no solve and no write-back.
        /// Returns null (after logging the reason) when inputs are missing or
        /// nothing is capturable. Nothing is serialized — No Cache holds.
        /// </summary>
        public static PreflightResult CaptureAndPreflight(VRClothDeclipper fitter, GameObject bodyRootOverride = null, bool verbose = true)
        {
            // bodyRoot is the avatar to build the body proxy from: the scene
            // avatar in the editor, or the post-Merge-Armature build clone
            // (ctx.AvatarRootObject) when called from the NDMF build pass.
            GameObject bodyRoot = bodyRootOverride != null ? bodyRootOverride
                : (fitter != null ? fitter.targetAvatar : null);
            if (fitter == null || bodyRoot == null || fitter.clothToDeform == null)
            {
                Debug.LogError("VRClothDeclipper: Target Avatar or Cloth is not set.");
                return null;
            }

            if (verbose)
            {
                string modeStr = fitter.mode.ToString();
                Debug.Log($"[VRClothDeclipper] Running in {modeStr} mode...");
                Debug.Log($"Target Avatar: {bodyRoot.name}, Cloth: {fitter.clothToDeform.name}");
                if (fitter.sourceAvatar != null)
                {
                    Debug.Log($"Source Avatar: {fitter.sourceAvatar.name}");
                }
            }

            GameObject clothRoot = fitter.clothRoot != null ? fitter.clothRoot : fitter.clothToDeform.gameObject;
            List<ClothSnapshot> cloth = VRClothMeshCapture.Capture(clothRoot);
            if (cloth.Count == 0)
            {
                Debug.LogError("VRClothDeclipper: No active SkinnedMeshRenderer found under the cloth root. Aborting.");
                return null;
            }

            int totalVertices = 0;
            foreach (var snapshot in cloth)
            {
                totalVertices += snapshot.VertexCount;
            }
            if (verbose) Debug.Log($"[VRClothDeclipper] Captured {cloth.Count} renderer(s), {totalVertices} vertices in world space.");

            var capsules = VRClothProxyGenerator.Generate(bodyRoot);
            if (capsules == null)
            {
                Debug.LogError("Failed to generate proxy capsules. Aborting.");
                return null;
            }
            // −1 = not judged (radius estimation off). When it runs, the
            // per-capsule "estimated" flags double as a body-coverage signal: a
            // split body resolved to one part (e.g. the hair) measures almost
            // nothing, which is the false-green tell (docs/DIAGNOSTIC_HONESTY.md §1).
            float bodyCoverage = -1f;
            bool bodyModelLowConfidence = false;
            if (fitter.estimateRadiiFromBody)
            {
                var outcome = VRClothBodyRadiusEstimator.Apply(fitter, capsules);
                capsules = outcome.capsules;
                bodyCoverage = BodyModelConfidence.Coverage(outcome.estimated);
                bodyModelLowConfidence = BodyModelConfidence.IsLowConfidence(outcome.estimated);
            }
            // Pick the collision backend: the mesh-SDF collider when requested
            // and a body mesh is available, otherwise the bone capsules
            // (docs/DESIGN.md §6). Detection differs (the mesh has no capsule
            // index); preflight and the solver run through the IBodyCollider
            // abstraction either way.
            IBodyCollider collider;
            List<PenetrationHit> hits;
            string backend;
            MeshSdfCollider sdf = fitter.useMeshSdfCollider ? VRClothBodySdfBuilder.Build(fitter) : null;
            if (sdf != null)
            {
                collider = sdf;
                backend = "mesh";
                VRClothDebugVisualizer.SetCapsules(System.Array.Empty<BodyCapsule>());
                hits = VRClothPenetrationDetector.Detect(cloth, collider, fitter.margin);
            }
            else
            {
                if (verbose && fitter.useMeshSdfCollider)
                {
                    Debug.LogWarning("[VRClothDeclipper] Mesh-SDF collider unavailable — falling back to bone capsules for this run.");
                }
                collider = new CapsuleBodyCollider(capsules);
                backend = "capsule";
                VRClothDebugVisualizer.SetCapsules(capsules);
                hits = VRClothPenetrationDetector.Detect(cloth, capsules, fitter.margin);
            }
            VRClothDebugVisualizer.SetHits(hits);
            if (verbose) Debug.Log($"[VRClothDeclipper] Detected {hits.Count} penetrating vertices (margin {fitter.margin:F3} m, {backend} backend).");

            // Preflight: judge per renderer whether the body-shape difference
            // is within the supported envelope (docs/DESIGN.md §9).
            var reports = new PreflightReport[cloth.Count];
            for (int i = 0; i < cloth.Count; i++)
            {
                var snapshot = cloth[i];
                reports[i] = PreflightDiagnostic.Evaluate(
                    snapshot.worldVertices, snapshot.triangles, snapshot.hits, collider, fitter.margin);
                if (verbose) Debug.Log(FormatPreflight(snapshot.renderer.name, reports[i]));
            }

            // False-green guard: when the body model covers too little of the
            // skeleton, GREEN means "could not see the body", not "no penetration"
            // — surface that instead of letting a false green stand silently
            // (docs/DIAGNOSTIC_HONESTY.md §1). Gated by verbose so the per-frame
            // live preview doesn't spam; the inspector's Run Preflight button and
            // the headless CLI are verbose and do show it.
            if (verbose && bodyModelLowConfidence)
            {
                Debug.LogWarning(
                    $"[VRClothDeclipper] ⚠ Body coverage low: only {bodyCoverage:P0} of proxy capsules found body geometry. "
                    + "The body model is likely missing parts — a split body whose parts weren't all detected, or an auto-picked "
                    + "wrong body mesh (e.g. the hair). GREEN / low-penetration results here are NOT trustworthy (possible false green). "
                    + "Assign 'Body Mesh' on the component or verify the body parts are active (docs/DESIGN.md §9).");
            }

            return new PreflightResult
            {
                cloth = cloth,
                capsules = capsules,
                collider = collider,
                backend = backend,
                hits = hits,
                reports = reports,
                bodyCoverage = bodyCoverage,
                bodyModelLowConfidence = bodyModelLowConfidence,
            };
        }

        /// <summary>
        /// Capture → detect → preflight → solve, returning a fitted mesh copy per
        /// cloth renderer <em>without</em> assigning it anywhere (no Undo, no
        /// scene mutation, nothing serialized — No Cache). This is the shared core
        /// behind both the NDMF build pass (<see cref="VRClothDeclipperPass"/>,
        /// with <paramref name="bodyRoot"/> = the post-Merge-Armature build clone)
        /// and the live preview (<see cref="VRClothDeclipperPreview"/>, with the
        /// scene avatar), so the fit shown in the editor is exactly the fit baked
        /// at upload. Mirrors <see cref="Run"/>'s solve loop: RED renderers are
        /// skipped unless <see cref="VRClothDeclipper.forceApplyOutOfRange"/>, and
        /// only renderers that actually penetrate produce a mesh. The caller owns
        /// every returned mesh and must assign or destroy it.
        /// </summary>
        public static List<(SkinnedMeshRenderer renderer, Mesh fitted)> SolveToFittedMeshes(
            VRClothDeclipper fitter, GameObject bodyRoot, bool verbose = false)
        {
            var results = new List<(SkinnedMeshRenderer, Mesh)>();
            var pf = CaptureAndPreflight(fitter, bodyRoot, verbose);
            if (pf == null || pf.hits.Count == 0)
            {
                return results;
            }
            for (int i = 0; i < pf.cloth.Count; i++)
            {
                if (pf.reports[i].verdict == PreflightVerdict.Red && !fitter.forceApplyOutOfRange)
                {
                    continue;
                }
                var snapshot = pf.cloth[i];
                var result = fitter.useProjectedSolver
                    ? PenetrationSolver.SolveProjected(snapshot.worldVertices, snapshot.triangles, pf.collider, fitter.margin)
                    : PenetrationSolver.Solve(snapshot.worldVertices, snapshot.triangles, pf.collider, fitter.margin);
                if (result.initialHitCount > 0)
                {
                    results.Add((snapshot.renderer, VRClothMeshApplier.BuildFittedMesh(snapshot)));
                }
            }
            return results;
        }

        public static void Run(VRClothDeclipper fitter)
        {
            var pf = CaptureAndPreflight(fitter);
            if (pf == null)
            {
                return;
            }
            var cloth = pf.cloth;
            var hits = pf.hits;
            var reports = pf.reports;

            // Preflight RED is apply-specific: warn here, and (unless forced)
            // skip the renderer in the solve loop below (docs/DESIGN.md §9).
            for (int i = 0; i < cloth.Count; i++)
            {
                if (reports[i].verdict == PreflightVerdict.Red)
                {
                    string cause = DescribeRedCause(reports[i].redCause);
                    Debug.LogWarning(fitter.forceApplyOutOfRange
                        ? $"[VRClothDeclipper] {cloth[i].renderer.name}: RED ({cause}), but Force Apply (Out of Range) is enabled — applying anyway. Expect artifacts (docs/DESIGN.md §9)."
                        : $"[VRClothDeclipper] {cloth[i].renderer.name}: RED — {cause} Apply will be skipped (docs/DESIGN.md §9). Enable 'Force Apply (Out of Range)' to override.");
                }
            }

            var solve = new VRClothRunLog.SolveSummary();
            // Pick the solver: the prototype normal/tangent-split SolveProjected
            // when requested, otherwise the current coarse-pass Solve. Both run
            // through the IBodyCollider abstraction and return the same Result
            // (docs/DEFORMATION_METHODS.md §3.1).
            string solverName = fitter.useProjectedSolver ? "projected" : "coarse";
            if (hits.Count > 0)
            {
                for (int i = 0; i < cloth.Count; i++)
                {
                    if (reports[i].verdict == PreflightVerdict.Red && !fitter.forceApplyOutOfRange)
                    {
                        solve.skippedRenderers++;
                        continue;
                    }
                    var snapshot = cloth[i];
                    var result = fitter.useProjectedSolver
                        ? PenetrationSolver.SolveProjected(snapshot.worldVertices, snapshot.triangles, pf.collider, fitter.margin)
                        : PenetrationSolver.Solve(snapshot.worldVertices, snapshot.triangles, pf.collider, fitter.margin);
                    solve.passes = Mathf.Max(solve.passes, result.passes);
                    solve.remainingPenetrating += result.finalHitCount;
                    if (result.initialHitCount > 0)
                    {
                        VRClothMeshApplier.Apply(snapshot);
                        solve.appliedRenderers++;
                    }
                }
                Debug.Log($"[VRClothDeclipper] Push-out + smoothing finished after {solve.passes} pass(es) ({solverName} solver); {solve.remainingPenetrating} vertices still penetrating.");
                Debug.Log($"[VRClothDeclipper] Applied fitted mesh copies to {solve.appliedRenderers} renderer(s)"
                    + (solve.skippedRenderers > 0 ? $", skipped {solve.skippedRenderers} out-of-range renderer(s)" : "")
                    + ". Originals untouched; Undo (Ctrl+Z) restores.");
            }

            // The run log records which backend produced these hits so capsule
            // and mesh-SDF runs can be compared (docs/DESIGN.md §6). Capsule
            // geometry is still logged; per-capsule attribution is skipped for
            // mesh hits, which carry no capsule index.
            VRClothRunLog.Write(fitter, cloth, pf.capsules, hits, reports, solve, pf.backend, pf.bodyCoverage);
            Debug.Log("[VRClothDeclipper] Process complete.");
        }

        static string FormatPreflight(string rendererName, PreflightReport report)
        {
            string verdict = report.verdict.ToString().ToUpperInvariant();
            if (report.redCause == RedCause.CollapsedShapeKey)
            {
                verdict += " (collapsed blendshape?)";
            }
            return $"[VRClothDeclipper] Preflight {rendererName}: {verdict} — "
                + $"penetrating {report.penetratingCount}/{report.vertexCount} verts ({report.penetratingRatio:P1}), "
                + $"max {report.maxDepth * 1000f:F1} mm below surface ({report.maxDepthOverRadius:P0} of capsule radius), "
                + $"p95 {report.p95Depth * 1000f:F1} mm, largest patch {report.largestPatchRatio:P1}, "
                + $"margin-zone hits {report.hitCount}.";
        }

        /// <summary>
        /// User-facing reason for a Red verdict. The collapsed-blendshape case
        /// is named explicitly because it is otherwise hard to discover — the
        /// folded cloth reads as intended design (ROADMAP phase 3).
        /// </summary>
        static string DescribeRedCause(RedCause cause)
        {
            switch (cause)
            {
                case RedCause.CollapsedShapeKey:
                    return "likely a shrink/hide blendshape folding cloth deep into the body, "
                        + "not a body-shape difference — check this mesh's blendshapes (neutralize the shrink/hide shape).";
                case RedCause.ThickGarmentInnerWall:
                    return "high penetration but spread across many small patches at shallow depth — likely "
                        + "a thick/enclosing garment's inner wall or a body-hugging accessory (shoes, chokers, "
                        + "belts) reading as penetration, not a body-shape difference. Verify visually; this is "
                        + "a known false-positive class (docs/DESIGN.md §8), not a retargeting job.";
                case RedCause.RetargetingClassDifference:
                default:
                    return "body-shape difference exceeds the supported range (retargeting-class).";
            }
        }
    }
}
