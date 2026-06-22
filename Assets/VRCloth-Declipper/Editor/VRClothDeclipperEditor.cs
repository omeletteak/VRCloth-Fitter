using UnityEditor;
using UnityEngine;
using VRClothDeclipper; // 名前空間を追加

namespace VRClothDeclipper
{
    [CustomEditor(typeof(VRClothDeclipper))]
    public class VRClothDeclipperEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            VRClothDeclipper fitter = (VRClothDeclipper)target;

            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
            EditorGUILayout.LabelField("VRCloth Fitter", headerStyle);
            EditorGUILayout.Space();

            // --- ターゲット設定 ---
            EditorGUILayout.LabelField("Targets", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();

            fitter.targetAvatar = (GameObject)EditorGUILayout.ObjectField("Target Avatar", fitter.targetAvatar, typeof(GameObject), true);
            fitter.sourceAvatar = (GameObject)EditorGUILayout.ObjectField("Source Avatar (Optional)", fitter.sourceAvatar, typeof(GameObject), true);

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.ObjectField("Cloth Root", fitter.clothRoot, typeof(GameObject), true);
            EditorGUI.EndDisabledGroup();
            
            EditorGUILayout.Space();

            // --- ステータスと設定 ---
            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);

            if (fitter.sourceAvatar != null)
            {
                // 高品質モードが利用可能
                fitter.mode = (VRClothDeclipper.QualityMode)EditorGUILayout.EnumPopup("Quality Mode", fitter.mode);
                EditorGUILayout.HelpBox("Source Avatar is set. High quality fitting is available.", MessageType.Info);
            }
            else
            {
                // 軽量モードのみ
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.EnumPopup("Quality Mode", VRClothDeclipper.QualityMode.Light);
                EditorGUI.EndDisabledGroup();
                fitter.mode = VRClothDeclipper.QualityMode.Light; // 内部的に設定
                EditorGUILayout.HelpBox("Source Avatar is not set. Only Light mode is available.", MessageType.Warning);
            }

            fitter.margin = EditorGUILayout.Slider(
                new GUIContent("Margin (m)", "Clearance kept between the body surface and the cloth."),
                fitter.margin, 0f, 0.05f);

            fitter.forceApplyOutOfRange = EditorGUILayout.Toggle(
                new GUIContent("Force Apply (Out of Range)",
                    "Apply even when the preflight diagnostic judges RED (body-shape difference beyond the supported range). Results will look wrong; see docs/DESIGN.md §9."),
                fitter.forceApplyOutOfRange);

            fitter.useMeshSdfCollider = EditorGUILayout.Toggle(
                new GUIContent("Use Mesh SDF Collider",
                    "Collide against a signed-distance field built from the avatar's body mesh instead of bone capsules. Capsules can't represent the torso/feet cross-section; the mesh SDF removes that false-penetration source (docs/DESIGN.md §6). Built in memory, never saved. Off by default until E2E calibrates."),
                fitter.useMeshSdfCollider);

            fitter.useProjectedSolver = EditorGUILayout.Toggle(
                new GUIContent("Use Projected Solver (Prototype)",
                    "Normal/tangent-split solver: re-projects penetrating vertices onto the margin surface after every smoothing step, so the field can't sink back in and no λ/pass tuning is needed (docs/DEFORMATION_METHODS.md §3.1). Off = current coarse-pass solver. Prototype — compare the two in E2E."),
                fitter.useProjectedSolver);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Body Radius Estimation", EditorStyles.boldLabel);

            fitter.estimateRadiiFromBody = EditorGUILayout.Toggle(
                new GUIContent("Estimate Radii From Body",
                    "Measure each proxy capsule's radius from the avatar's body mesh instead of fixed defaults."),
                fitter.estimateRadiiFromBody);

            // The body mesh feeds both radius estimation and the mesh-SDF
            // collider, so allow assigning it when either is on.
            using (new EditorGUI.DisabledScope(!fitter.estimateRadiiFromBody && !fitter.useMeshSdfCollider))
            {
                fitter.bodyMesh = (SkinnedMeshRenderer)EditorGUILayout.ObjectField(
                    new GUIContent("Body Mesh (Optional)",
                        "Auto-detected when empty: largest active skinned mesh on the Hips bone, excluding the cloth. Used for radius estimation and the mesh-SDF collider."),
                    fitter.bodyMesh, typeof(SkinnedMeshRenderer), true);
            }

            using (new EditorGUI.DisabledScope(!fitter.estimateRadiiFromBody))
            {
                fitter.radiusPercentile = EditorGUILayout.Slider(
                    new GUIContent("Radius Percentile",
                        "Per capsule, the radius is this percentile of body-surface distances. Higher = looser, lower = tighter."),
                    fitter.radiusPercentile, 0.5f, 1f);
            }

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(fitter);
            }

            if (fitter.clothToDeform == null)
            {
                EditorGUILayout.HelpBox("No SkinnedMeshRenderer found in the cloth root or its children.", MessageType.Error);
            }

            EditorGUILayout.Space();

            // --- 実行 ---
            GUI.enabled = fitter.targetAvatar != null && fitter.clothToDeform != null;
            if (GUILayout.Button("Run Fitting", GUILayout.Height(30)))
            {
                VRClothPipeline.Run(fitter);
            }
            GUI.enabled = true;

            EditorGUILayout.Space();

            // --- 実験(代表ポーズ対応 段階1スパイク) ---
            EditorGUILayout.LabelField("Experimental", EditorStyles.boldLabel);
            GUI.enabled = fitter.targetAvatar != null && fitter.clothToDeform != null;
            if (GUILayout.Button(new GUIContent("Run Multi-Pose Fit (Spike)",
                "Stage-1 multi-pose glue spike: drives the avatar through representative poses, composes one bind-local delta that clears them all, applies it non-destructively, and logs per-pose residuals. Experimental — calibrate poses by eye. See docs/MULTIPOSE_GLUE_SPIKE.md and docs/E2E_TEST_GUIDE.md §7.1.")))
            {
                VRClothMultiPoseSpike.Run(fitter);
            }
            GUI.enabled = true;

            EditorGUILayout.Space();

            // --- デバッグ表示 ---
            EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);

            VRClothDebugVisualizer.Visible = EditorGUILayout.Toggle(
                "Show Scene Gizmos", VRClothDebugVisualizer.Visible);

            EditorGUILayout.BeginHorizontal();
            GUI.enabled = fitter.targetAvatar != null;
            if (GUILayout.Button("Preview Body Proxy"))
            {
                var capsules = VRClothProxyGenerator.Generate(fitter.targetAvatar);
                if (capsules != null)
                {
                    if (fitter.estimateRadiiFromBody)
                    {
                        capsules = VRClothBodyRadiusEstimator.Apply(fitter, capsules).capsules;
                    }
                    VRClothDebugVisualizer.SetCapsules(capsules);
                }
            }
            GUI.enabled = true;
            if (GUILayout.Button("Clear", GUILayout.Width(60)))
            {
                VRClothDebugVisualizer.Clear();
            }
            EditorGUILayout.EndHorizontal();

            GUI.enabled = fitter.targetAvatar != null;
            if (GUILayout.Button(new GUIContent("Measure Head Count",
                "Logs the target avatar's head-count (頭身) measured from its body mesh — a scale-invariant body-family descriptor (docs/FAMILY_MODEL.md). Reads vertices in memory, logs only scalars (No Cache).")))
            {
                VRClothHeadCountMeasure.Measure(fitter);
            }
            GUI.enabled = true;

            if (VRClothDebugVisualizer.CapsuleCount > 0 || VRClothDebugVisualizer.HitCount > 0)
            {
                EditorGUILayout.LabelField(
                    $"Capsules: {VRClothDebugVisualizer.CapsuleCount}, Penetrating vertices: {VRClothDebugVisualizer.HitCount}",
                    EditorStyles.miniLabel);
            }
        }
    }
}
