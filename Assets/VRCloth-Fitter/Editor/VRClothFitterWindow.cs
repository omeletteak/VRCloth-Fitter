using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class VRClothFitterWindow : EditorWindow
{
    private GameObject avatarObject;
    private GameObject clothObject;

    private Vector2 scrollPositionAvatar;
    private Vector2 scrollPositionCloth;

    private List<string> avatarBoneNames = new List<string>();
    private List<string> clothBoneNames = new List<string>();

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
            UpdateBoneLists();
        }

        EditorGUILayout.Space();

        // ボーンリストの表示エリア
        EditorGUILayout.BeginHorizontal();
        {
            // Avatar Bone List
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Avatar Bones", EditorStyles.boldLabel);
            scrollPositionAvatar = EditorGUILayout.BeginScrollView(scrollPositionAvatar);
            foreach (var boneName in avatarBoneNames)
            {
                GUILayout.Label(boneName);
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            // Cloth Bone List
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Cloth Bones", EditorStyles.boldLabel);
            scrollPositionCloth = EditorGUILayout.BeginScrollView(scrollPositionCloth);
            foreach (var boneName in clothBoneNames)
            {
                GUILayout.Label(boneName);
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndHorizontal();


        EditorGUILayout.Space();

        // 実行ボタン
        if (GUILayout.Button("Fit Cloth"))
        {
            FitCloth();
        }
    }

    private void UpdateBoneLists()
    {
        avatarBoneNames.Clear();
        clothBoneNames.Clear();

        if (avatarObject != null)
        {
            var renderer = avatarObject.GetComponentInChildren<SkinnedMeshRenderer>();
            if (renderer != null)
            {
                avatarBoneNames = renderer.bones.Select(b => b.name).ToList();
            }
        }

        if (clothObject != null)
        {
            var renderer = clothObject.GetComponentInChildren<SkinnedMeshRenderer>();
            if (renderer != null)
            {
                clothBoneNames = renderer.bones.Select(b => b.name).ToList();
            }
        }
        
        Repaint();
    }

    private void FitCloth()
    {
        if (avatarObject == null || clothObject == null)
        {
            EditorUtility.DisplayDialog("Error", "AvatarとClothの両方を設定してください。", "OK");
            return;
        }

        var avatarRenderer = avatarObject.GetComponentInChildren<SkinnedMeshRenderer>();
        var clothRenderer = clothObject.GetComponentInChildren<SkinnedMeshRenderer>();

        if (avatarRenderer == null)
        {
            EditorUtility.DisplayDialog("Error", "AvatarにSkinnedMeshRendererが見つかりません。", "OK");
            return;
        }

        if (clothRenderer == null)
        {
            EditorUtility.DisplayDialog("Error", "ClothにSkinnedMeshRendererが見つかりません。", "OK");
            return;
        }

        // アバターのボーンを名前をキーにした辞書に変換
        var avatarBones = avatarRenderer.bones.ToDictionary(b => b.name, b => b);
        
        // 衣装の新しいボーン配列を作成
        var newClothBones = new Transform[clothRenderer.bones.Length];
        bool allBonesFound = true;

        for (int i = 0; i < clothRenderer.bones.Length; i++)
        {
            string boneName = clothRenderer.bones[i].name;
            if (avatarBones.TryGetValue(boneName, out Transform avatarBone))
            {
                newClothBones[i] = avatarBone;
            }
            else
            {
                Debug.LogWarning($"AvatarにClothのボーン '{boneName}' が見つかりませんでした。");
                newClothBones[i] = clothRenderer.bones[i]; // 見つからない場合は元のボーンを維持
                allBonesFound = false;
            }
        }

        // 衣装のSkinnedMeshRendererに新しいボーン配列を設定
        clothRenderer.bones = newClothBones;

        // ルートボーンもアバターのものに合わせる
        var avatarRootBone = avatarRenderer.rootBone;
        if (avatarBones.ContainsKey(clothRenderer.rootBone.name))
        {
            avatarRootBone = avatarBones[clothRenderer.rootBone.name];
        }
        clothRenderer.rootBone = avatarRootBone;

        if (allBonesFound)
        {
            EditorUtility.DisplayDialog("Success", "衣装のボーンをアバターに合わせました。", "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("Warning", "いくつかのボーンが見つからなかったため、処理は不完全かもしれません。詳細はConsoleを確認してください。", "OK");
        }
    }
}
