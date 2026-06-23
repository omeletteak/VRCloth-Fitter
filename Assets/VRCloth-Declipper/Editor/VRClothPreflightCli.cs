using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VRClothDeclipper
{
    /// <summary>
    /// Headless preflight (and optional fit) in batchmode, without opening the
    /// editor GUI, so the numeric verdict (GREEN/YELLOW/RED + depths/ratios) — and,
    /// with -vrclothApply, the residual after the fit — can be produced by
    /// automation. Visual acceptance stays a human gate (docs/E2E_TEST_GUIDE.md
    /// §3.1). With -vrclothApply the fit is applied to in-memory mesh copies and
    /// re-measured; the scene is never saved, so No Cache holds.
    ///
    /// Invoke (project NOT open in the GUI; tool installed via VPM or a junction):
    /// <code>
    /// Unity.exe -projectPath &lt;proj&gt; -batchmode -nographics
    ///   -executeMethod VRClothDeclipper.VRClothPreflightCli.Run
    ///   -vrclothScene "Assets/.../Scene.unity"   (one scene; else the open scene)
    ///   -vrclothSceneDir "Assets/.../Folder"      (or sweep every *.unity in a folder)
    ///   -vrclothApply                              (also fit and report after-residual)
    ///   -vrclothOut "C:/path/out.json"
    /// </code>
    /// Exit code is 0 when the run completed (read the JSON) and non-zero only on
    /// a fatal error — a RED verdict still exits 0. Note: in batchmode the process
    /// exit code is not reliably propagated, so treat the JSON as authoritative.
    /// </summary>
    public static class VRClothPreflightCli
    {
        public static void Run()
        {
            int exitCode = 0;
            try
            {
                string scenePath = GetArg("-vrclothScene");
                string sceneDir = GetArg("-vrclothSceneDir");
                string outPath = GetArg("-vrclothOut");
                bool apply = HasFlag("-vrclothApply");

                var report = new CliReport
                {
                    generatedAtUtc = DateTime.UtcNow.ToString("o"),
                    mode = apply ? "apply" : "preflight",
                    scenes = new List<SceneResult>(),
                };
                foreach (var path in ResolveScenePaths(scenePath, sceneDir))
                {
                    report.scenes.Add(ProcessScene(path, apply));
                }
                report.worstVerdict = WorstVerdict(report.scenes);

                if (string.IsNullOrEmpty(outPath))
                {
                    outPath = Path.Combine(Path.GetTempPath(), "vrcloth-preflight.json");
                }
                File.WriteAllText(outPath, JsonUtility.ToJson(report, true));
                Debug.Log($"[VRClothPreflightCli] mode={report.mode}, scenes={report.scenes.Count}, "
                    + $"worstVerdict={report.worstVerdict}. Wrote '{outPath}'.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[VRClothPreflightCli] Failed: {e}");
                exitCode = 3;
            }
            EditorApplication.Exit(exitCode);
        }

        // One explicit scene, or every *.unity directly under a folder (combo
        // sweep), or — if neither is given — the scene already open.
        static List<string> ResolveScenePaths(string scenePath, string sceneDir)
        {
            var list = new List<string>();
            if (!string.IsNullOrEmpty(sceneDir))
            {
                string abs = ToAbsolute(sceneDir);
                if (Directory.Exists(abs))
                {
                    foreach (var f in Directory.GetFiles(abs, "*.unity", SearchOption.TopDirectoryOnly))
                    {
                        list.Add(ToProjectRelative(f));
                    }
                    list.Sort(StringComparer.Ordinal);
                }
                else
                {
                    Debug.LogWarning($"[VRClothPreflightCli] -vrclothSceneDir not found: {abs}");
                }
            }
            else if (!string.IsNullOrEmpty(scenePath))
            {
                list.Add(scenePath);
            }
            else
            {
                list.Add(null); // use the already-open scene
            }
            return list;
        }

        static SceneResult ProcessScene(string scenePath, bool apply)
        {
            var sr = new SceneResult { scene = scenePath ?? "(open scene)", entries = new List<CliEntry>() };
            try
            {
                if (!string.IsNullOrEmpty(scenePath))
                {
                    EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                }
                sr.scene = SceneManager.GetActiveScene().path;

                var fitters = UnityEngine.Object.FindObjectsByType<VRClothDeclipper>(FindObjectsSortMode.None);
                if (fitters == null || fitters.Length == 0)
                {
                    sr.note = "No active VRClothDeclipper component in this scene.";
                    Debug.LogWarning($"[VRClothPreflightCli] {sr.scene}: {sr.note}");
                    return sr;
                }

                foreach (var fitter in fitters)
                {
                    var before = VRClothPipeline.CaptureAndPreflight(fitter);
                    if (before == null)
                    {
                        sr.entries.Add(new CliEntry { fitter = fitter.name, renderer = "(aborted)", verdictBefore = "ERROR" });
                        continue;
                    }
                    VRClothPipeline.PreflightResult after = null;
                    if (apply)
                    {
                        // Solve + apply (swaps each solved renderer to a fitted
                        // mesh copy). RunLog is opt-in and off, so nothing is
                        // written to disk; the scene is never saved.
                        VRClothPipeline.Run(fitter);
                        after = VRClothPipeline.CaptureAndPreflight(fitter);
                    }
                    AddEntries(sr, fitter, before, after);
                }
            }
            catch (Exception e)
            {
                sr.note = $"error: {e.Message}";
                Debug.LogError($"[VRClothPreflightCli] {sr.scene}: {e}");
            }
            return sr;
        }

        static void AddEntries(SceneResult sr, VRClothDeclipper fitter,
            VRClothPipeline.PreflightResult before, VRClothPipeline.PreflightResult after)
        {
            for (int i = 0; i < before.cloth.Count; i++)
            {
                var rb = before.reports[i];
                var entry = new CliEntry
                {
                    fitter = fitter.name,
                    renderer = before.cloth[i].renderer != null ? before.cloth[i].renderer.name : "(null)",
                    backend = before.backend,
                    bodyCoverage = before.bodyCoverage,
                    bodyModelLowConfidence = before.bodyModelLowConfidence,
                    vertexCount = rb.vertexCount,
                    verdictBefore = rb.verdict.ToString().ToUpperInvariant(),
                    redCauseBefore = rb.redCause.ToString(),
                    penetratingBefore = rb.penetratingCount,
                    maxDepthMmBefore = rb.maxDepth * 1000f,
                    targetedBySolver = rb.verdict != PreflightVerdict.Red || fitter.forceApplyOutOfRange,
                };
                if (after != null && i < after.reports.Length)
                {
                    var ra = after.reports[i];
                    entry.verdictAfter = ra.verdict.ToString().ToUpperInvariant();
                    entry.penetratingAfter = ra.penetratingCount;
                    entry.maxDepthMmAfter = ra.maxDepth * 1000f;
                }
                sr.entries.Add(entry);
            }
        }

        // Worst over every entry's before-verdict (GREEN<YELLOW<RED<ERROR): a
        // single line saying whether anything in the sweep needs a look.
        static string WorstVerdict(List<SceneResult> scenes)
        {
            int worst = 0;
            foreach (var s in scenes)
            {
                foreach (var e in s.entries)
                {
                    worst = Mathf.Max(worst, Rank(e.verdictBefore));
                }
            }
            return Name(worst);
        }

        static int Rank(string v) => v == "ERROR" ? 3 : v == "RED" ? 2 : v == "YELLOW" ? 1 : 0;
        static string Name(int r) => r == 3 ? "ERROR" : r == 2 ? "RED" : r == 1 ? "YELLOW" : "GREEN";

        static string ToAbsolute(string dir)
        {
            if (Path.IsPathRooted(dir))
            {
                return dir;
            }
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, dir);
        }

        static string ToProjectRelative(string absPath)
        {
            string p = absPath.Replace('\\', '/');
            string data = Application.dataPath.Replace('\\', '/'); // <proj>/Assets
            return p.StartsWith(data) ? "Assets" + p.Substring(data.Length) : p;
        }

        static string GetArg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name)
                {
                    return args[i + 1];
                }
            }
            return null;
        }

        static bool HasFlag(string name)
        {
            foreach (string a in Environment.GetCommandLineArgs())
            {
                if (a == name)
                {
                    return true;
                }
            }
            return false;
        }

        [Serializable]
        class CliReport
        {
            public string generatedAtUtc;
            public string mode;          // "preflight" | "apply"
            public string worstVerdict;
            public List<SceneResult> scenes;
        }

        [Serializable]
        class SceneResult
        {
            public string scene;
            public string note;
            public List<CliEntry> entries;
        }

        [Serializable]
        class CliEntry
        {
            public string fitter;
            public string renderer;
            public string backend;
            public float bodyCoverage;            // fraction of capsules measured from the body
            public bool bodyModelLowConfidence;   // true = GREEN here may be a false green (§1)
            public int vertexCount;
            public bool targetedBySolver; // false = RED, skipped by the solver
            public string verdictBefore;
            public string redCauseBefore;
            public int penetratingBefore;
            public float maxDepthMmBefore;
            public string verdictAfter;   // apply mode only
            public int penetratingAfter;
            public float maxDepthMmAfter;
        }
    }
}
