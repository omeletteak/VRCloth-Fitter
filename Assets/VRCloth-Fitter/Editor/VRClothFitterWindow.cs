using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using VRClothFitter; // Namespaceを追加

public class VRClothFitterWindow : EditorWindow
{
    private GameObject avatarObject;
    private GameObject clothObject;

    private Vector2 scrollPosition;

    private List<string> avatarBoneNames = new List<string>();
    private string[] avatarBoneNamesArray;
    private List<string> clothBoneNames = new List<string>();
    private int[] mappedBoneIndices;

    private const string NO_BONE_SELECTED = "[None]";

    [MenuItem("Tools/VRCloth Fitter")]
    public static void ShowWindow()
    {
        GetWindow<VRClothFitterWindow>("VRCloth Fitter");
    }

    private void OnGUI()
    {
        GUILayout.Label("VRCloth Fitter", EditorStyles.boldLabel);
        
        EditorGUILayout.Space();

        // アバターと衣装の設定
        EditorGUI.BeginChangeCheck();
        avatarObject = (GameObject)EditorGUILayout.ObjectField("Avatar", avatarObject, typeof(GameObject), true);
        clothObject = (GameObject)EditorGUILayout.ObjectField("Cloth", clothObject, typeof(GameObject), true);
        if (EditorGUI.EndChangeCheck())
        {
            UpdateBoneData();
        }

        EditorGUILayout.Space();

        // ボーンマッピングエリア
        GUILayout.Label("Bone Mapping", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Cloth Bone", EditorStyles.boldLabel);
        GUILayout.Label("->", GUILayout.Width(20));
        GUILayout.Label("Avatar Bone", EditorStyles.boldLabel);
        EditorGUILayout.EndHorizontal();

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, EditorStyles.helpBox);
        {
            if (clothBoneNames.Count > 0 && avatarBoneNames.Count > 0)
            {
                for (int i = 0; i < clothBoneNames.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    {
                        EditorGUILayout.LabelField(clothBoneNames[i]);
                        GUILayout.Label("->", GUILayout.Width(20));
                        mappedBoneIndices[i] = EditorGUILayout.Popup(mappedBoneIndices[i], avatarBoneNamesArray);
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }
            else
            {
                GUILayout.Label("Please set Avatar and Cloth objects.");
            }
        }
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();

        // 実行ボタンエリア
        EditorGUILayout.BeginHorizontal();
        {
            if (GUILayout.Button("Fit Bones"))
            {
                FitBones();
            }
            if (GUILayout.Button("Calculate & Save Scale"))
            {
                CalculateAndSaveScale();
            }
        }
        EditorGUILayout.EndHorizontal();
    }

    private void UpdateBoneData()
    {
        // アバターのボーンリストを取得
        avatarBoneNames.Clear();
        if (avatarObject != null)
        {
            var renderer = avatarObject.GetComponentInChildren<SkinnedMeshRenderer>();
            if (renderer != null)
            {
                avatarBoneNames = renderer.bones.Select(b => b.name).ToList();
            }
        }
        avatarBoneNames.Insert(0, NO_BONE_SELECTED);
        avatarBoneNamesArray = avatarBoneNames.ToArray();

        // 衣装のボーンリストを取得
        clothBoneNames.Clear();
        if (clothObject != null)
        {
            var renderer = clothObject.GetComponentInChildren<SkinnedMeshRenderer>();
            if (renderer != null)
            {
                clothBoneNames = renderer.bones.Select(b => b.name).ToList();
            }
        }

        // マッピングを初期化・自動設定
        mappedBoneIndices = new int[clothBoneNames.Count];
        for (int i = 0; i < clothBoneNames.Count; i++)
        {
            int foundIndex = avatarBoneNames.FindIndex(bName => bName == clothBoneNames[i]);
            mappedBoneIndices[i] = (foundIndex != -1) ? foundIndex : 0;
        }
        
        Repaint();
    }

    private bool GetRenderers(out SkinnedMeshRenderer avatarRenderer, out SkinnedMeshRenderer clothRenderer)
    {
        avatarRenderer = null;
        clothRenderer = null;

        if (avatarObject == null || clothObject == null)
        {
            EditorUtility.DisplayDialog("Error", "AvatarとClothの両方を設定してください。", "OK");
            return false;
        }

        avatarRenderer = avatarObject.GetComponentInChildren<SkinnedMeshRenderer>();
        if (avatarRenderer == null)
        {
            EditorUtility.DisplayDialog("Error", "AvatarにSkinnedMeshRendererが見つかりません。", "OK");
            return false;
        }

        clothRenderer = clothObject.GetComponentInChildren<SkinnedMeshRenderer>();
        if (clothRenderer == null)
        {
            EditorUtility.DisplayDialog("Error", "ClothにSkinnedMeshRendererが見つかりません。", "OK");
            return false;
        }

        return true;
    }

    private void FitBones()
    {
        if (!GetRenderers(out var avatarRenderer, out var clothRenderer)) return;

        Undo.RecordObject(clothRenderer, "Fit Cloth Bones");

        var avatarBones = avatarRenderer.bones.ToDictionary(b => b.name, b => b);
        var newClothBones = new Transform[clothRenderer.bones.Length];
        bool allBonesMapped = true;

        for (int i = 0; i < clothRenderer.bones.Length; i++)
        {
            int selectedIndex = mappedBoneIndices[i];
            if (selectedIndex > 0)
            {
                string selectedBoneName = avatarBoneNamesArray[selectedIndex];
                if (avatarBones.TryGetValue(selectedBoneName, out Transform avatarBone))
                {
                    newClothBones[i] = avatarBone;
                }
            }
            else
            {
                newClothBones[i] = clothRenderer.bones[i];
                allBonesMapped = false;
            }
        }

        clothRenderer.bones = newClothBones;

        var avatarRootBone = avatarRenderer.rootBone;
        if (avatarBones.ContainsKey(clothRenderer.rootBone.name))
        {
            avatarRootBone = avatarBones[clothRenderer.rootBone.name];
        }
        clothRenderer.rootBone = avatarRootBone;

        if (allBonesMapped)
        {
            EditorUtility.DisplayDialog("Success", "衣装のボーンをアバターに合わせました。", "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("Warning", "いくつかのボーンが未設定です。詳細はConsoleを確認してください。", "OK");
        }
    }

    private void CalculateAndSaveScale()
    {
        if (!GetRenderers(out var avatarRenderer, out var clothRenderer)) return;

        var scalingData = clothObject.GetComponent<VRClothFitterScalingData>();
        if (scalingData == null)
        {
            scalingData = Undo.AddComponent<VRClothFitterScalingData>(clothObject);
        }
        Undo.RecordObject(scalingData, "Calculate and Save Bone Scale");

        scalingData.boneScales.Clear();

        var avatarBones = avatarRenderer.bones.ToDictionary(b => b.name, b => b);
        var clothBonesOriginal = clothRenderer.bones.ToDictionary(b => b.name, b => b);

        for (int i = 0; i < clothBoneNames.Count; i++)
        {
            string clothBoneName = clothBoneNames[i];
            int selectedIndex = mappedBoneIndices[i];

            if (selectedIndex == 0 || !clothBonesOriginal.TryGetValue(clothBoneName, out var clothBone) || clothBone.childCount == 0) continue;

            string selectedBoneName = avatarBoneNamesArray[selectedIndex];
            if (!avatarBones.TryGetValue(selectedBoneName, out Transform avatarBone) || avatarBone.childCount == 0)
            {
                continue;
            }

            float clothBoneLength = Vector3.Distance(clothBone.position, clothBone.GetChild(0).position);
            float avatarBoneLength = Vector3.Distance(avatarBone.position, avatarBone.GetChild(0).position);

            if (clothBoneLength > 0.0001f && avatarBoneLength > 0.0001f)
            {
                float scaleRatio = avatarBoneLength / clothBoneLength;
                
                var info = new BoneScaleInfo
                {
                    boneName = clothBoneName,
                    scale = Vector3.one * scaleRatio
                };
                scalingData.boneScales.Add(info);
            }
        }
        
        EditorUtility.DisplayDialog("Success", $"{scalingData.boneScales.Count}個のボーンスケール情報を計算し、{clothObject.name}のVRClothFitterScalingDataコンポーネントに保存しました。", "OK");
    }
}
