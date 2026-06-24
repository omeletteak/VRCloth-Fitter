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
    /// Headless body-measurement (採寸表) batch — the predict layer of
    /// docs/ECOSYSTEM_VISION.md §5 produced without the GUI. Reuses the same
    /// machinery the headless preflight CLI proves works in batchmode (scene
    /// open, Humanoid proxy, BakeMesh), minus cloth and solve. Writes one JSONL
    /// row per avatar (scalars only — No Cache) via
    /// <see cref="VRClothMeasurementDump.Measure"/>.
    ///
    /// Two modes (project NOT open in the GUI):
    /// <code>
    /// Unity.exe -projectPath &lt;proj&gt; -batchmode -nographics
    ///   -executeMethod VRClothDeclipper.VRClothMeasureCli.Run
    ///   -vrclothOut "C:/path/out.jsonl"              (default: project-root 採寸表)
    ///
    ///   (a) measure every VRClothDeclipper in scenes you set up:
    ///       -vrclothScene "Assets/.../S.unity"   or   -vrclothSceneDir "Assets/.../Folder"
    ///
    ///   (b) measure avatar prefabs directly, no scene setup:
    ///       -vrclothAvatar "A.prefab,B.prefab,..."     (comma-separated; scattered avatars in one run)
    ///       -vrclothAvatarDir "Assets/.../Avatars"     (every *.prefab directly under it)
    ///
    ///   -vrclothAppend                                 (append rows instead of overwriting)
    /// </code>
    /// (b) wins when both are given. The output file is OVERWRITTEN by default (a
    /// batch run = a fresh table) unless -vrclothAppend; the inspector button
    /// always appends. Exit code is 0 on
    /// completion (read the JSONL); non-zero only on a fatal error. Nothing is
    /// saved — instances live in a throwaway scene and are destroyed (No Cache).
    /// </summary>
    public static class VRClothMeasureCli
    {
        public static void Run()
        {
            int exitCode = 0;
            try
            {
                var lines = new List<string>();
                string avatarDir = GetArg("-vrclothAvatarDir");
                string avatarPrefab = GetArg("-vrclothAvatar");
                string mode;
                if (!string.IsNullOrEmpty(avatarDir) || !string.IsNullOrEmpty(avatarPrefab))
                {
                    mode = "prefabs";
                    MeasurePrefabs(avatarDir, avatarPrefab, lines);
                }
                else
                {
                    mode = "scenes";
                    MeasureScenes(lines);
                }

                string outPath = GetArg("-vrclothOut");
                if (string.IsNullOrEmpty(outPath))
                {
                    outPath = VRClothMeasurementDump.FilePath();
                }
                string payload = lines.Count > 0 ? string.Join("\n", lines) + "\n" : "";
                bool append = HasFlag("-vrclothAppend");
                if (append)
                {
                    File.AppendAllText(outPath, payload);
                }
                else
                {
                    File.WriteAllText(outPath, payload);
                }
                Debug.Log($"[VRClothMeasureCli] mode={mode}, measured {lines.Count} avatar(s), {(append ? "appended" : "overwrote")} -> '{outPath}'.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[VRClothMeasureCli] Failed: {e}");
                exitCode = 3;
            }
            EditorApplication.Exit(exitCode);
        }

        // --- mode (a): measure fitters already placed in scenes -------------

        static void MeasureScenes(List<string> lines)
        {
            foreach (string path in ResolveScenePaths())
            {
                if (!string.IsNullOrEmpty(path))
                {
                    EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                }
                string sceneName = SceneManager.GetActiveScene().path;
                var fitters = UnityEngine.Object.FindObjectsByType<VRClothDeclipper>(FindObjectsSortMode.None);
                if (fitters == null || fitters.Length == 0)
                {
                    Debug.LogWarning($"[VRClothMeasureCli] {sceneName}: no VRClothDeclipper component to measure.");
                    continue;
                }
                foreach (var fitter in fitters)
                {
                    string json = VRClothMeasurementDump.Measure(fitter);
                    if (json != null)
                    {
                        lines.Add(json);
                    }
                }
            }
        }

        // --- mode (b): instantiate avatar prefabs and measure ---------------

        static void MeasurePrefabs(string avatarDir, string avatarPrefab, List<string> lines)
        {
            var paths = new List<string>();
            if (!string.IsNullOrEmpty(avatarPrefab))
            {
                // Comma-separated so scattered avatars (different shop folders) can
                // be measured in one run without collecting them into a folder.
                foreach (string one in avatarPrefab.Split(','))
                {
                    string trimmed = one.Trim();
                    if (trimmed.Length > 0)
                    {
                        paths.Add(trimmed);
                    }
                }
            }
            if (!string.IsNullOrEmpty(avatarDir))
            {
                string abs = ToAbsolute(avatarDir);
                if (Directory.Exists(abs))
                {
                    foreach (string f in Directory.GetFiles(abs, "*.prefab", SearchOption.TopDirectoryOnly))
                    {
                        paths.Add(ToProjectRelative(f));
                    }
                    paths.Sort(StringComparer.Ordinal);
                }
                else
                {
                    Debug.LogWarning($"[VRClothMeasureCli] -vrclothAvatarDir not found: {abs}");
                }
            }

            // A throwaway scene so instances never touch the user's scenes and
            // nothing is saved (No Cache).
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            foreach (string p in paths)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (prefab == null)
                {
                    Debug.LogWarning($"[VRClothMeasureCli] not a prefab asset: {p}");
                    continue;
                }

                GameObject instance = null;
                GameObject holder = null;
                try
                {
                    instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    if (instance == null)
                    {
                        Debug.LogWarning($"[VRClothMeasureCli] could not instantiate: {p}");
                        continue;
                    }
                    // The Humanoid Animator may be nested (the prefab root is often a
                    // container with the avatar — and any outfit — as children), so
                    // search children and measure from the avatar root we find.
                    Animator animator = instance.GetComponentInChildren<Animator>(true);
                    if (animator == null || !animator.isHuman)
                    {
                        Debug.LogWarning($"[VRClothMeasureCli] skip (no Humanoid avatar): {p}");
                        continue;
                    }
                    GameObject avatarRoot = animator.gameObject;

                    // The fitter lives on a standalone empty object — NOT under the
                    // avatar — so clothRoot excludes nothing and the whole body is
                    // measured (a bare-body 採寸, docs/ECOSYSTEM_VISION.md §5).
                    holder = new GameObject("__vrcloth_measure__");
                    VRClothDeclipper fitter = holder.AddComponent<VRClothDeclipper>();
                    fitter.targetAvatar = avatarRoot;
                    fitter.clothRoot = holder;
                    fitter.clothToDeform = null;

                    string json = VRClothMeasurementDump.Measure(fitter);
                    if (json != null)
                    {
                        lines.Add(json);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[VRClothMeasureCli] {p}: {e.Message}");
                }
                finally
                {
                    if (holder != null) UnityEngine.Object.DestroyImmediate(holder);
                    if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
                }
            }
        }

        // --- shared CLI plumbing (kept self-contained) ----------------------

        static List<string> ResolveScenePaths()
        {
            var list = new List<string>();
            string sceneDir = GetArg("-vrclothSceneDir");
            string scenePath = GetArg("-vrclothScene");
            if (!string.IsNullOrEmpty(sceneDir))
            {
                string abs = ToAbsolute(sceneDir);
                if (Directory.Exists(abs))
                {
                    foreach (string f in Directory.GetFiles(abs, "*.unity", SearchOption.TopDirectoryOnly))
                    {
                        list.Add(ToProjectRelative(f));
                    }
                    list.Sort(StringComparer.Ordinal);
                }
                else
                {
                    Debug.LogWarning($"[VRClothMeasureCli] -vrclothSceneDir not found: {abs}");
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
    }
}
