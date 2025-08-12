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
        }
    }
}
