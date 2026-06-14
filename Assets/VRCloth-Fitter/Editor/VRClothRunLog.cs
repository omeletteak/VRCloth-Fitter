using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace VRClothFitter
{
    /// <summary>
    /// Writes a structured, machine-readable summary of one fitting Run to a
    /// known file (project-root <c>vrcloth-fitter-log.json</c>), so results can
    /// be inspected — by a human or an AI assistant — without scraping the
    /// Unity Console.
    ///
    /// Records aggregates only: per-capsule and per-renderer statistics, never
    /// raw vertex positions. Nothing here can reconstruct the body shape, so it
    /// stays within the No Cache principle (docs/DESIGN.md §5). The log is a
    /// transient diagnostic artifact, not a cache: it is overwritten each Run.
    /// </summary>
    public static class VRClothRunLog
    {
        public const string FilePrefix = "vrcloth-fitter-log";

        /// <summary>
        /// Project-root path (sibling of Assets/) of the latest run log for the
        /// given avatar. The avatar name is folded into the file name so logs
        /// from several avatars coexist instead of overwriting each other;
        /// re-running the same avatar overwrites only its own file.
        /// </summary>
        public static string FilePathFor(string avatarName)
        {
            string safe = Sanitize(avatarName);
            string file = string.IsNullOrEmpty(safe) ? FilePrefix + ".json" : $"{FilePrefix}__{safe}.json";
            return Path.Combine(Directory.GetParent(Application.dataPath).FullName, file);
        }

        static string Sanitize(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return "";
            }
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return name;
        }

        public struct SolveSummary
        {
            public int passes;
            public int remainingPenetrating;
            public int appliedRenderers;
            public int skippedRenderers;
        }

        public static void Write(
            VRClothFitter fitter,
            IReadOnlyList<ClothSnapshot> cloth,
            IReadOnlyList<BodyCapsule> capsules,
            IReadOnlyList<PenetrationHit> hits,
            IReadOnlyList<PreflightReport> reports,
            SolveSummary solve)
        {
            try
            {
                string json = JsonUtility.ToJson(BuildDto(fitter, cloth, capsules, hits, reports, solve), true);
                string path = FilePathFor((fitter != null && fitter.targetAvatar != null) ? fitter.targetAvatar.name : "");
                File.WriteAllText(path, json);
                Debug.Log($"[VRClothFitter] Run log written: {path}");
            }
            catch (Exception e)
            {
                // Logging must never break a fit.
                Debug.LogWarning($"[VRClothFitter] Could not write run log: {e.Message}");
            }
        }

        static RunLogDto BuildDto(
            VRClothFitter fitter,
            IReadOnlyList<ClothSnapshot> cloth,
            IReadOnlyList<BodyCapsule> capsules,
            IReadOnlyList<PenetrationHit> hits,
            IReadOnlyList<PreflightReport> reports,
            SolveSummary solve)
        {
            float margin = fitter != null ? fitter.margin : 0f;

            int capsuleCount = capsules != null ? capsules.Count : 0;
            var capHits = new int[capsuleCount];
            var capMaxDepth = new float[capsuleCount];
            if (hits != null)
            {
                foreach (var h in hits)
                {
                    if (h.capsuleIndex < 0 || h.capsuleIndex >= capsuleCount) continue;
                    capHits[h.capsuleIndex]++;
                    float surfaceDepth = h.depth - margin; // below the actual body surface, margin excluded
                    if (surfaceDepth > capMaxDepth[h.capsuleIndex]) capMaxDepth[h.capsuleIndex] = surfaceDepth;
                }
            }

            var capDtos = new CapsuleDto[capsuleCount];
            for (int i = 0; i < capsuleCount; i++)
            {
                var c = capsules[i];
                capDtos[i] = new CapsuleDto
                {
                    index = i,
                    label = c.label ?? "",
                    radius_m = c.radius,
                    length_m = Vector3.Distance(c.start, c.end),
                    hits = capHits[i],
                    maxDepthBelowSurface_m = capMaxDepth[i],
                };
            }

            int rendererCount = cloth != null ? cloth.Count : 0;
            var rendDtos = new RendererDto[rendererCount];
            for (int i = 0; i < rendererCount; i++)
            {
                PreflightReport r = (reports != null && i < reports.Count) ? reports[i] : default;
                string name = (cloth[i] != null && cloth[i].renderer != null) ? cloth[i].renderer.name : $"renderer{i}";
                rendDtos[i] = new RendererDto
                {
                    name = name,
                    verdict = r.verdict.ToString(),
                    vertexCount = r.vertexCount,
                    penetratingCount = r.penetratingCount,
                    penetratingRatio = r.penetratingRatio,
                    maxDepthBelowSurface_m = r.maxDepth,
                    p95DepthBelowSurface_m = r.p95Depth,
                    maxDepthOverRadius = r.maxDepthOverRadius,
                    largestPatchRatio = r.largestPatchRatio,
                    marginZoneHits = r.hitCount,
                };
            }

            return new RunLogDto
            {
                timestamp = DateTime.Now.ToString("o"),
                avatar = (fitter != null && fitter.targetAvatar != null) ? fitter.targetAvatar.name : "",
                sourceAvatar = (fitter != null && fitter.sourceAvatar != null) ? fitter.sourceAvatar.name : "",
                clothRoot = (fitter != null && fitter.clothRoot != null) ? fitter.clothRoot.name
                    : (fitter != null && fitter.clothToDeform != null ? fitter.clothToDeform.name : ""),
                margin_m = margin,
                totalCapsules = capsuleCount,
                totalHits = hits != null ? hits.Count : 0,
                capsules = capDtos,
                renderers = rendDtos,
                solve = new SolveDto
                {
                    passes = solve.passes,
                    remainingPenetrating = solve.remainingPenetrating,
                    appliedRenderers = solve.appliedRenderers,
                    skippedRenderers = solve.skippedRenderers,
                },
            };
        }

        [Serializable]
        class RunLogDto
        {
            public string schema = "vrcloth-fitter-run/1";
            public string timestamp;
            public string avatar;
            public string sourceAvatar;
            public string clothRoot;
            public float margin_m;
            public int totalCapsules;
            public int totalHits;
            public CapsuleDto[] capsules;
            public RendererDto[] renderers;
            public SolveDto solve;
        }

        [Serializable]
        class CapsuleDto
        {
            public int index;
            public string label;
            public float radius_m;
            public float length_m;
            public int hits;
            public float maxDepthBelowSurface_m;
        }

        [Serializable]
        class RendererDto
        {
            public string name;
            public string verdict;
            public int vertexCount;
            public int penetratingCount;
            public float penetratingRatio;
            public float maxDepthBelowSurface_m;
            public float p95DepthBelowSurface_m;
            public float maxDepthOverRadius;
            public float largestPatchRatio;
            public int marginZoneHits;
        }

        [Serializable]
        class SolveDto
        {
            public int passes;
            public int remainingPenetrating;
            public int appliedRenderers;
            public int skippedRenderers;
        }
    }
}
