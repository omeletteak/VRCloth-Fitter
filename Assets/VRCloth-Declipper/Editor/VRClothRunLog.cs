using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace VRClothDeclipper
{
    /// <summary>
    /// Optional developer diagnostic log for one fitting Run. OFF by default:
    /// the shipped tool persists nothing (No Cache, docs/DESIGN.md §5) — the
    /// Console, the inspector verdicts and the scene-view heatmap are the
    /// always-on feedback. A developer opts in via
    /// <c>Tools ▸ VRCloth-Declipper ▸ Write Run Log</c> to capture runs while
    /// calibrating the preflight thresholds or comparing collider backends.
    ///
    /// When enabled, appends one line per run (JSONL) to a project-root
    /// <c>vrcloth-declipper-runs.jsonl</c> so improvement can be tracked over time
    /// without overwriting history. That file is a local, gitignored,
    /// non-distributed dev artifact and records aggregates only — per-capsule
    /// and per-renderer statistics, never raw vertex positions, so nothing in
    /// it can reconstruct the body shape.
    /// </summary>
    public static class VRClothRunLog
    {
        public const string FileName = "vrcloth-declipper-runs.jsonl";

        const string MenuPath = "Tools/VRCloth-Declipper/Write Run Log (dev)";
        const string EnabledKey = "VRClothDeclipper.WriteRunLog";

        /// <summary>
        /// Whether runs are written to disk. False by default — opt-in, stored
        /// per developer in EditorPrefs (not serialized into the avatar or
        /// scene, so a distributed prefab never carries "logging on").
        /// </summary>
        public static bool Enabled
        {
            get => EditorPrefs.GetBool(EnabledKey, false);
            set => EditorPrefs.SetBool(EnabledKey, value);
        }

        [MenuItem(MenuPath)]
        static void ToggleEnabled() => Enabled = !Enabled;

        [MenuItem(MenuPath, true)]
        static bool ToggleEnabledValidate()
        {
            Menu.SetChecked(MenuPath, Enabled);
            return true;
        }

        /// <summary>Project-root path (sibling of Assets/) of the dev run log.</summary>
        public static string FilePath()
        {
            return Path.Combine(Directory.GetParent(Application.dataPath).FullName, FileName);
        }

        /// <summary>
        /// Strips a leading "Assets/" so an asset path reads from its shop
        /// folder down (e.g. "Assets/Chocolate rice/.../Ash_Blue_1.prefab" ->
        /// "Chocolate rice/.../Ash_Blue_1.prefab"). Pure and unit-tested.
        /// Backslashes are normalized; paths outside Assets (e.g. "Packages/…")
        /// are returned unchanged.
        /// </summary>
        public static string RelativeFromAssets(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return "";
            }
            string p = assetPath.Replace('\\', '/');
            const string prefix = "Assets/";
            return p.StartsWith(prefix) ? p.Substring(prefix.Length) : p;
        }

        public struct SolveSummary
        {
            public int passes;
            public int remainingPenetrating;
            public int appliedRenderers;
            public int skippedRenderers;
        }

        public static void Write(
            VRClothDeclipper fitter,
            IReadOnlyList<ClothSnapshot> cloth,
            IReadOnlyList<BodyCapsule> capsules,
            IReadOnlyList<PenetrationHit> hits,
            IReadOnlyList<PreflightReport> reports,
            SolveSummary solve,
            string colliderBackend)
        {
            if (!Enabled)
            {
                return; // opt-in only; by default the tool persists nothing (No Cache)
            }
            try
            {
                string json = JsonUtility.ToJson(
                    BuildDto(fitter, cloth, capsules, hits, reports, solve, colliderBackend), false);
                string path = FilePath();
                File.AppendAllText(path, json + "\n");
                Debug.Log($"[VRClothDeclipper] Run log appended: {path}");
            }
            catch (Exception e)
            {
                // Logging must never break a fit.
                Debug.LogWarning($"[VRClothDeclipper] Could not write run log: {e.Message}");
            }
        }

        /// <summary>
        /// Source prefab path of the cloth instance in the scene, relative from
        /// its shop folder (see <see cref="RelativeFromAssets"/>). Empty when the
        /// cloth has been unpacked from its prefab; the caller keeps the name.
        /// </summary>
        static string ResolveClothPath(VRClothDeclipper fitter)
        {
            GameObject go = (fitter != null && fitter.clothRoot != null) ? fitter.clothRoot
                : (fitter != null && fitter.clothToDeform != null ? fitter.clothToDeform.gameObject : null);
            if (go == null)
            {
                return "";
            }
            string assetPath = "";
            UnityEngine.Object src = PrefabUtility.GetCorrespondingObjectFromSource(go);
            if (src != null)
            {
                assetPath = AssetDatabase.GetAssetPath(src);
            }
            if (string.IsNullOrEmpty(assetPath))
            {
                assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
            }
            return RelativeFromAssets(assetPath);
        }

        static RunLogDto BuildDto(
            VRClothDeclipper fitter,
            IReadOnlyList<ClothSnapshot> cloth,
            IReadOnlyList<BodyCapsule> capsules,
            IReadOnlyList<PenetrationHit> hits,
            IReadOnlyList<PreflightReport> reports,
            SolveSummary solve,
            string colliderBackend)
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
                colliderBackend = colliderBackend ?? "",
                avatar = (fitter != null && fitter.targetAvatar != null) ? fitter.targetAvatar.name : "",
                sourceAvatar = (fitter != null && fitter.sourceAvatar != null) ? fitter.sourceAvatar.name : "",
                clothRoot = (fitter != null && fitter.clothRoot != null) ? fitter.clothRoot.name
                    : (fitter != null && fitter.clothToDeform != null ? fitter.clothToDeform.name : ""),
                clothPath = ResolveClothPath(fitter),
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
            public string schema = "vrcloth-declipper-run/2";
            public string timestamp;
            public string colliderBackend;
            public string avatar;
            public string sourceAvatar;
            public string clothRoot;
            public string clothPath;
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
