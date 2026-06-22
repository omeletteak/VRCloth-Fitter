using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace VRClothDeclipper
{
    /// <summary>
    /// One-click test-scene prep for the E2E cross-test. The manual prep has three
    /// deterministic, easy-to-botch steps; this does them correctly in one go:
    ///
    ///   1. <b>Manual Bake with AAO's automatic skinned-mesh merge OFF.</b> Left on,
    ///      AAO (Trace and Optimize) folds the garment into the body mesh
    ///      (<c>$$AAO_AUTO_MERGE_SKINNED_MESH_n</c>) so only oddly-tagged pieces stay
    ///      separately declippable — declip must run before the mesh merge, which is
    ///      exactly where the future NDMF pass (ROADMAP phase 5) would sit.
    ///   2. <b>Re-attach VRClothDeclipper to the baked garment root</b> (the component
    ///      is IEditorOnly and is stripped by the bake) with clothRoot pointing at the
    ///      whole outfit, so every piece is captured (docs/DESIGN.md §6).
    ///   3. <b>Turn the mesh-SDF collider on.</b>
    ///
    /// The FIT (position/rotation/scale by eye) is never touched: scripted fitting was
    /// shown to not reproduce the hand fit (VRClothComboBaker, removed). Nothing here
    /// ships into an uploaded avatar — it only builds a baked scene for the headless
    /// preflight sweep (docs/E2E_TEST_GUIDE.md §3.1). NDMF and AAO are invoked by
    /// reflection / SerializedObject so the source repo (no AAO, older NDMF) compiles
    /// and the same code runs against the test project's newer packages.
    /// </summary>
    public static class VRClothBakeTestScene
    {
        const string MenuPath = "Tools/VRClothDeclipper/Bake Test Scene (AAO merge off)";

        const string AaoTraceAndOptimize = "Anatawa12.AvatarOptimizer.TraceAndOptimize";
        const string AaoMergeField = "mergeSkinnedMesh";
        const string NdmfProcessor = "nadena.dev.ndmf.AvatarProcessor";

        // Enabled only when the selection is a garment root under a Humanoid avatar.
        [MenuItem(MenuPath, true)]
        static bool Validate()
        {
            GameObject g = Selection.activeGameObject;
            if (g == null)
            {
                return false;
            }
            GameObject avatar = FindHumanoidAncestor(g.transform);
            return avatar != null && avatar != g;
        }

        [MenuItem(MenuPath)]
        public static void BakeSelected()
        {
            GameObject garment = Selection.activeGameObject;
            if (garment == null)
            {
                Debug.LogWarning("[VRClothDeclipper] Bake Test Scene: select the garment root (the object holding all cloth pieces) in the Hierarchy first.");
                return;
            }
            Bake(garment);
        }

        /// <summary>
        /// Bakes the avatar that owns <paramref name="garmentRoot"/> with AAO mesh
        /// merge disabled, then re-attaches a mesh-SDF VRClothDeclipper to the baked
        /// garment root. The source avatar is left deactivated; the scene is marked
        /// dirty but not saved (review, then save into Assets/TEST_vrcloth_declipper/).
        /// </summary>
        public static void Bake(GameObject garmentRoot)
        {
            GameObject avatar = FindHumanoidAncestor(garmentRoot.transform);
            if (avatar == null)
            {
                Debug.LogError($"[VRClothDeclipper] Bake Test Scene: '{garmentRoot.name}' has no Humanoid avatar ancestor. Select the garment root that sits under the avatar.");
                return;
            }
            if (avatar == garmentRoot)
            {
                Debug.LogError("[VRClothDeclipper] Bake Test Scene: select the garment root, not the avatar root (clothRoot must exclude the body).");
                return;
            }

            // Record the garment as a name-path under the avatar so we can find its
            // counterpart in the baked clone (the bake preserves object names).
            List<string> garmentPath = TransformPath(avatar.transform, garmentRoot.transform);
            if (garmentPath == null || garmentPath.Count == 0)
            {
                Debug.LogError($"[VRClothDeclipper] Bake Test Scene: '{garmentRoot.name}' is not under avatar '{avatar.name}'.");
                return;
            }

            // Disable AAO auto-merge on the source so the clone bakes with the garment
            // pieces separate; always restore afterwards (even if the bake throws).
            List<AaoMergeRestore> restore = SetAaoMergeSkinnedMesh(avatar, false);
            GameObject baked;
            try
            {
                baked = ManualBake(avatar);
            }
            finally
            {
                foreach (AaoMergeRestore r in restore)
                {
                    r.Restore();
                }
            }
            if (baked == null)
            {
                return; // ManualBake already logged the reason.
            }

            // Mirror "Manual Bake Avatar": keep the baked clone, deactivate the source
            // (also hides its pre-bake VRClothDeclipper, if any, from the headless sweep
            // which ignores inactive objects).
            avatar.SetActive(false);

            Transform bakedGarment = ResolvePath(baked.transform, garmentPath);
            if (bakedGarment == null)
            {
                Debug.LogError($"[VRClothDeclipper] Bake Test Scene: the baked clone has no object at '{string.Join("/", garmentPath)}' — the bake may have renamed/removed it. Attach VRClothDeclipper by hand on the selected clone.");
                Selection.activeGameObject = baked;
                EditorSceneManager.MarkSceneDirty(baked.scene);
                return;
            }

            VRClothDeclipper fitter = bakedGarment.GetComponent<VRClothDeclipper>()
                ?? bakedGarment.gameObject.AddComponent<VRClothDeclipper>();
            fitter.clothRoot = bakedGarment.gameObject;
            fitter.targetAvatar = baked;
            fitter.bodyMesh = null;            // auto-detect the body on the baked clone
            fitter.useMeshSdfCollider = true;  // the whole point of step 3
            fitter.AutoDetectComponents();
            EditorUtility.SetDirty(fitter);

            Selection.activeGameObject = fitter.gameObject;
            EditorSceneManager.MarkSceneDirty(baked.scene);

            int clothRenderers = bakedGarment.GetComponentsInChildren<SkinnedMeshRenderer>(false).Length;
            Debug.Log($"[VRClothDeclipper] Bake Test Scene: baked '{avatar.name}' with AAO merge off, re-attached VRClothDeclipper to '{bakedGarment.name}' "
                + $"({clothRenderers} active cloth renderer(s) under clothRoot), mesh-SDF on. Review the fit, then save the scene into Assets/TEST_vrcloth_declipper/ and run the headless sweep.");
        }

        // ---- transform path helpers (pure; unit-tested) ----

        /// <summary>
        /// Child-name chain from <paramref name="root"/> (exclusive) down to
        /// <paramref name="target"/> (inclusive). Null if target is not under root;
        /// empty list if target == root.
        /// </summary>
        public static List<string> TransformPath(Transform root, Transform target)
        {
            var names = new List<string>();
            for (Transform t = target; t != null; t = t.parent)
            {
                if (t == root)
                {
                    names.Reverse();
                    return names;
                }
                names.Add(t.name);
            }
            return null;
        }

        /// <summary>
        /// Walks <paramref name="path"/> (child names) down from <paramref name="root"/>,
        /// returning the deepest matching transform, or null if any step is missing.
        /// </summary>
        public static Transform ResolvePath(Transform root, IReadOnlyList<string> path)
        {
            Transform cur = root;
            foreach (string name in path)
            {
                Transform next = null;
                for (int i = 0; i < cur.childCount; i++)
                {
                    if (cur.GetChild(i).name == name)
                    {
                        next = cur.GetChild(i);
                        break;
                    }
                }
                if (next == null)
                {
                    return null;
                }
                cur = next;
            }
            return cur;
        }

        static GameObject FindHumanoidAncestor(Transform t)
        {
            for (Transform p = t; p != null; p = p.parent)
            {
                Animator a = p.GetComponent<Animator>();
                if (a != null && a.isHuman)
                {
                    return p.gameObject;
                }
            }
            return null;
        }

        // ---- NDMF manual bake via reflection (compiles without NDMF referenced) ----

        static GameObject ManualBake(GameObject avatar)
        {
            Type proc = FindType(NdmfProcessor);
            if (proc == null)
            {
                Debug.LogError($"[VRClothDeclipper] Bake Test Scene: NDMF ({NdmfProcessor}) not found — is Modular Avatar / NDMF installed in this project?");
                return null;
            }
            // ManualProcessAvatar(GameObject[, INDMFPlatformProvider]) clones, bakes the
            // clone, and returns it (NDMF 1.9+). Fall back to the obsolete
            // ProcessAvatarUI(GameObject) on older NDMF. Never call ProcessAvatar(root):
            // that is destructive and would bake the source in place.
            MethodInfo m = proc.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(x => x.Name == "ManualProcessAvatar"
                    && x.GetParameters().Length >= 1
                    && x.GetParameters()[0].ParameterType == typeof(GameObject));
            if (m == null)
            {
                m = proc.GetMethod("ProcessAvatarUI", BindingFlags.Public | BindingFlags.Static,
                    null, new[] { typeof(GameObject) }, null);
            }
            if (m == null)
            {
                Debug.LogError("[VRClothDeclipper] Bake Test Scene: no ManualProcessAvatar/ProcessAvatarUI on NDMF AvatarProcessor — unexpected NDMF version.");
                return null;
            }

            try
            {
                ParameterInfo[] ps = m.GetParameters();
                object[] args = ps.Length == 1 ? new object[] { avatar } : new object[] { avatar, null };
                return m.Invoke(null, args) as GameObject;
            }
            catch (TargetInvocationException e)
            {
                Debug.LogError($"[VRClothDeclipper] Bake Test Scene: NDMF manual bake threw: {e.InnerException?.Message ?? e.Message}");
                Debug.LogException(e.InnerException ?? e);
                return null;
            }
        }

        // ---- AAO Trace-and-Optimize toggling via SerializedObject ----

        readonly struct AaoMergeRestore
        {
            readonly SerializedObject so;
            readonly bool previous;

            public AaoMergeRestore(SerializedObject so, bool previous)
            {
                this.so = so;
                this.previous = previous;
            }

            public void Restore()
            {
                if (so == null || so.targetObject == null)
                {
                    return;
                }
                so.Update();
                SerializedProperty p = so.FindProperty(AaoMergeField);
                if (p != null)
                {
                    p.boolValue = previous;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }

        static List<AaoMergeRestore> SetAaoMergeSkinnedMesh(GameObject avatar, bool value)
        {
            var restores = new List<AaoMergeRestore>();
            foreach (Component c in avatar.GetComponentsInChildren<Component>(true))
            {
                if (c == null || c.GetType().FullName != AaoTraceAndOptimize)
                {
                    continue;
                }
                var so = new SerializedObject(c);
                SerializedProperty p = so.FindProperty(AaoMergeField);
                if (p == null)
                {
                    continue;
                }
                restores.Add(new AaoMergeRestore(so, p.boolValue));
                p.boolValue = value;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            if (restores.Count == 0)
            {
                Debug.Log("[VRClothDeclipper] Bake Test Scene: no AAO Trace and Optimize on the avatar (nothing to disable). If the garment still bakes into one mesh, check for explicit Merge Skinned Mesh components.");
            }
            return restores;
        }

        static Type FindType(string fullName)
        {
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = asm.GetType(fullName);
                if (t != null)
                {
                    return t;
                }
            }
            return null;
        }
    }
}
