using UnityEditor;
using UnityEngine;
using VRClothFitter; // 名前空間を追加

namespace VRClothFitter
{
    [CustomEditor(typeof(VRClothFitter))]
    public class VRClothFitterEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            VRClothFitter fitter = (VRClothFitter)target;

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
                fitter.mode = (VRClothFitter.QualityMode)EditorGUILayout.EnumPopup("Quality Mode", fitter.mode);
                EditorGUILayout.HelpBox("Source Avatar is set. High quality fitting is available.", MessageType.Info);
            }
            else
            {
                // 軽量モードのみ
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.EnumPopup("Quality Mode", VRClothFitter.QualityMode.Light);
                EditorGUI.EndDisabledGroup();
                fitter.mode = VRClothFitter.QualityMode.Light; // 内部的に設定
                EditorGUILayout.HelpBox("Source Avatar is not set. Only Light mode is available.", MessageType.Warning);
            }

            fitter.margin = EditorGUILayout.Slider(
                new GUIContent("Margin (m)", "Clearance kept between the body surface and the cloth."),
                fitter.margin, 0f, 0.05f);

            fitter.forceApplyOutOfRange = EditorGUILayout.Toggle(
                new GUIContent("Force Apply (Out of Range)",
                    "Apply even when the preflight diagnostic judges RED (body-shape difference beyond the supported range). Results will look wrong; see docs/DESIGN.md §9."),
                fitter.forceApplyOutOfRange);

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
                    VRClothDebugVisualizer.SetCapsules(capsules);
                }
            }
            GUI.enabled = true;
            if (GUILayout.Button("Clear", GUILayout.Width(60)))
            {
                VRClothDebugVisualizer.Clear();
            }
            EditorGUILayout.EndHorizontal();

            if (VRClothDebugVisualizer.CapsuleCount > 0 || VRClothDebugVisualizer.HitCount > 0)
            {
                EditorGUILayout.LabelField(
                    $"Capsules: {VRClothDebugVisualizer.CapsuleCount}, Penetrating vertices: {VRClothDebugVisualizer.HitCount}",
                    EditorStyles.miniLabel);
            }
        }
    }
}
