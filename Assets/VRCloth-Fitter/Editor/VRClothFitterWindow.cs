using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using VRClothFitter;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using VRClothFitter;
using nadena.dev.modular_avatar.core;
using System;

public class VRClothFitterWindow : EditorWindow
{
    private class BoneInfo
    {
        public string name;
        public bool isHumanoid;
    }

    private GameObject avatarObject;
    private GameObject clothObject;

    private Vector2 scrollPositionBones;
    private Vector2 scrollPositionBlendshapes;

    // Bone mapping variables
    private List<BoneInfo> avatarBones = new List<BoneInfo>();
    private GUIContent[] avatarBonePopupContent;
    private List<BoneInfo> clothBones = new List<BoneInfo>();
    private int[] mappedBoneIndices;

    // Blendshape mapping variables
    private List<string> avatarBlendshapeNames = new List<string>();
    private string[] avatarBlendshapeNamesArray;
    private List<string> clothBlendshapeNames = new List<string>();
    private int[] mappedBlendshapeIndices;

    private bool isPreviewing = false;
    private Dictionary<Transform, Vector3> originalBoneScales;

    private const string NO_BONE_SELECTED = "[None]";
    private const string NO_BLENDSHAPE_SELECTED = "[None]";
    
    private GUIStyle boldLabelStyle;

    // Material utility variables
    private Shader targetShader;
    private List<Material> clothMaterials = new List<Material>();
    private Vector2 scrollPositionMaterials;

    [MenuItem("Tools/VRCloth Fitter")]
    public static void ShowWindow()
    {
        GetWindow<VRClothFitterWindow>("VRCloth Fitter");
    }

    private void OnEnable()
    {
        boldLabelStyle = new GUIStyle(EditorStyles.label) { fontStyle = FontStyle.Bold };
    }

    private void OnDisable()
    {
        StopPreview();
    }

    private void OnGUI()
    {
        GUILayout.Label("VRCloth Fitter", EditorStyles.boldLabel);
        
        EditorGUILayout.Space();

        EditorGUI.BeginChangeCheck();
        avatarObject = (GameObject)EditorGUILayout.ObjectField("Avatar", avatarObject, typeof(GameObject), true);
        clothObject = (GameObject)EditorGUILayout.ObjectField("Cloth", clothObject, typeof(GameObject), true);
        if (EditorGUI.EndChangeCheck())
        {
            StopPreview();
            UpdateAllData();
        }

        EditorGUILayout.Space();

        // --- Bone Mapping UI ---
        GUILayout.Label("Bone Mapping", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Cloth Bone", EditorStyles.boldLabel);
        GUILayout.Label("->", GUILayout.Width(20));
        GUILayout.Label("Avatar Bone", EditorStyles.boldLabel);
        EditorGUILayout.EndHorizontal();

        scrollPositionBones = EditorGUILayout.BeginScrollView(scrollPositionBones, EditorStyles.helpBox, GUILayout.Height(150));
        {
            if (clothBones.Count > 0 && avatarBones.Count > 0)
            {
                for (int i = 0; i < clothBones.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(clothBones[i].name, clothBones[i].isHumanoid ? boldLabelStyle : EditorStyles.label);
                    GUILayout.Label("->", GUILayout.Width(20));
                    mappedBoneIndices[i] = EditorGUILayout.Popup(mappedBoneIndices[i], avatarBonePopupContent);
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

        // --- Blendshape Sync UI ---
        GUILayout.Label("Blendshape Sync", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Cloth Blendshape", EditorStyles.boldLabel);
        GUILayout.Label("->", GUILayout.Width(20));
        GUILayout.Label("Avatar Blendshape", EditorStyles.boldLabel);
        EditorGUILayout.EndHorizontal();

        scrollPositionBlendshapes = EditorGUILayout.BeginScrollView(scrollPositionBlendshapes, EditorStyles.helpBox, GUILayout.Height(150));
        {
            if (clothBlendshapeNames.Count > 0 && avatarBlendshapeNames.Count > 0)
            {
                for (int i = 0; i < clothBlendshapeNames.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(clothBlendshapeNames[i]);
                    GUILayout.Label("->", GUILayout.Width(20));
                    mappedBlendshapeIndices[i] = EditorGUILayout.Popup(mappedBlendshapeIndices[i], avatarBlendshapeNamesArray);
                    EditorGUILayout.EndHorizontal();
                }
            }
            else
            {
                GUILayout.Label("No blendshapes found or objects not set.");
            }
        }
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();

        // --- Material & Shader Utilities ---
        GUILayout.Label("Material & Shader Utilities", EditorStyles.boldLabel);
        targetShader = (Shader)EditorGUILayout.ObjectField("Target Shader", targetShader, typeof(Shader), false);

        scrollPositionMaterials = EditorGUILayout.BeginScrollView(scrollPositionMaterials, EditorStyles.helpBox, GUILayout.Height(100));
        {
            if (clothMaterials.Count > 0)
            {
                foreach (var mat in clothMaterials)
                {
                    EditorGUILayout.ObjectField(mat.name, mat, typeof(Material), false);
                }
            }
            else
            {
                GUILayout.Label("No materials found on cloth object.");
            }
        }
        EditorGUILayout.EndScrollView();

        if (GUILayout.Button("Convert Materials")) ConvertMaterials();

        EditorGUILayout.Space();

        // --- Action Buttons ---
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Fit Bones")) FitBones();
        if (GUILayout.Button("Calculate & Save Scale")) CalculateAndSaveScale();
        if (GUILayout.Button("Apply Blendshape Sync")) ApplyBlendshapeSync();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        GUI.color = isPreviewing ? Color.yellow : Color.white;
        if (GUILayout.Button(isPreviewing ? "Stop Preview" : "Toggle Preview")) TogglePreview();
        GUI.color = Color.white;
    }

    private void UpdateAllData()
    {
        var avatarRenderer = avatarObject?.GetComponentInChildren<SkinnedMeshRenderer>();
        var clothRenderer = clothObject?.GetComponentInChildren<SkinnedMeshRenderer>();
        var animator = avatarObject?.GetComponent<Animator>();

        UpdateBoneData(avatarRenderer, clothRenderer, animator);
        UpdateBlendshapeData(avatarRenderer, clothRenderer);
        UpdateMaterialData();
        
        Repaint();
    }

    private void UpdateBoneData(SkinnedMeshRenderer avatarRenderer, SkinnedMeshRenderer clothRenderer, Animator animator)
    {
        avatarBones.Clear();
        clothBones.Clear();

        var humanoidBoneNames = new HashSet<string>();
        if (animator != null && animator.isHuman)
        {
            foreach (HumanBodyBones boneType in Enum.GetValues(typeof(HumanBodyBones)))
            {
                if (boneType == HumanBodyBones.LastBone) continue;
                var boneTransform = animator.GetBoneTransform(boneType);
                if (boneTransform != null)
                {
                    humanoidBoneNames.Add(boneTransform.name);
                }
            }
        }

        if (avatarRenderer != null)
        {
            avatarBones = avatarRenderer.bones.Select(b => new BoneInfo { name = b.name, isHumanoid = humanoidBoneNames.Contains(b.name) }).ToList();
        }
        avatarBones.Insert(0, new BoneInfo { name = NO_BONE_SELECTED, isHumanoid = false });
        avatarBonePopupContent = avatarBones.Select(b => new GUIContent(b.isHumanoid ? $"{b.name} *" : b.name)).ToArray();

        if (clothRenderer != null)
        {
            clothBones = clothRenderer.bones.Select(b => new BoneInfo { name = b.name, isHumanoid = humanoidBoneNames.Contains(b.name) }).ToList();
        }
        
        mappedBoneIndices = new int[clothBones.Count];
        for (int i = 0; i < clothBones.Count; i++)
        {
            int foundIndex = avatarBones.FindIndex(b => b.name == clothBones[i].name);
            mappedBoneIndices[i] = (foundIndex != -1) ? foundIndex : 0;
        }
    }

    private void UpdateBlendshapeData(SkinnedMeshRenderer avatarRenderer, SkinnedMeshRenderer clothRenderer)
    {
        avatarBlendshapeNames.Clear();
        clothBlendshapeNames.Clear();

        if (avatarRenderer?.sharedMesh != null)
        {
            for (int i = 0; i < avatarRenderer.sharedMesh.blendShapeCount; i++)
            {
                avatarBlendshapeNames.Add(avatarRenderer.sharedMesh.GetBlendShapeName(i));
            }
        }
        avatarBlendshapeNames.Insert(0, NO_BLENDSHAPE_SELECTED);
        avatarBlendshapeNamesArray = avatarBlendshapeNames.ToArray();

        if (clothRenderer?.sharedMesh != null)
        {
            for (int i = 0; i < clothRenderer.sharedMesh.blendShapeCount; i++)
            {
                clothBlendshapeNames.Add(clothRenderer.sharedMesh.GetBlendShapeName(i));
            }
        }
        mappedBlendshapeIndices = new int[clothBlendshapeNames.Count];
        for (int i = 0; i < clothBlendshapeNames.Count; i++)
        {
            int foundIndex = avatarBlendshapeNames.FindIndex(bName => bName == clothBlendshapeNames[i]);
            mappedBlendshapeIndices[i] = (foundIndex != -1) ? foundIndex : 0;
        }
    }

    private void UpdateMaterialData()
    {
        clothMaterials.Clear();
        if (clothObject != null)
        {
            var renderer = clothObject.GetComponentInChildren<SkinnedMeshRenderer>();
            if (renderer != null)
            {
                clothMaterials = renderer.sharedMaterials.ToList();
            }
        }
    }
    
    // ... (The rest of the methods remain largely the same, but need to be updated to use the new BoneInfo class)

    private void FitBones()
    {
        if (!GetRenderers(out var avatarRenderer, out var clothRenderer)) return;

        Undo.RecordObject(clothRenderer, "Fit Cloth Bones");

        var avatarBonesDict = avatarRenderer.bones.ToDictionary(b => b.name, b => b);
        var newClothBones = new Transform[clothRenderer.bones.Length];
        bool allBonesMapped = true;

        for (int i = 0; i < clothRenderer.bones.Length; i++)
        {
            int selectedIndex = mappedBoneIndices[i];
            if (selectedIndex > 0)
            {
                string selectedBoneName = avatarBones[selectedIndex].name;
                if (avatarBonesDict.TryGetValue(selectedBoneName, out Transform avatarBone))
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
        if (avatarBonesDict.ContainsKey(clothRenderer.rootBone.name))
        {
            avatarRootBone = avatarBonesDict[clothRenderer.rootBone.name];
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
    
    // ... (CalculateAndSaveScale, TogglePreview, StartPreview, StopPreview, CalculateAllBoneRadii need minor adjustments to use BoneInfo)

    private void ConvertMaterials()
    {
        if (clothObject == null || targetShader == null)
        {
            EditorUtility.DisplayDialog("Error", "Please set the Cloth object and a Target Shader.", "OK");
            return;
        }

        var renderer = clothObject.GetComponentInChildren<SkinnedMeshRenderer>();
        if (renderer == null) return;

        if (!EditorUtility.DisplayDialog("Confirm Material Conversion",
            $"This will create new materials and replace them on '{clothObject.name}'. The original material assets will not be modified.

Continue?",
            "Convert", "Cancel"))
        {
            return;
        }

        Undo.RecordObject(renderer, "Convert Materials");

        var originalMaterials = renderer.sharedMaterials;
        var newMaterials = new Material[originalMaterials.Length];

        for (int i = 0; i < originalMaterials.Length; i++)
        {
            var oldMat = originalMaterials[i];
            var newMat = new Material(targetShader);
            newMat.name = $"{oldMat.name}_{targetShader.name}";

            // Attempt to copy common textures
            if (oldMat.HasProperty("_MainTex")) newMat.SetTexture("_MainTex", oldMat.GetTexture("_MainTex"));
            if (oldMat.HasProperty("_BumpMap")) newMat.SetTexture("_BumpMap", oldMat.GetTexture("_BumpMap")); // For Standard shader normals
            if (oldMat.HasProperty("_NormalMap")) newMat.SetTexture("_NormalMap", oldMat.GetTexture("_NormalMap"));
            
            // You can add more texture properties to copy here, e.g., for lilToon:
            // if (oldMat.HasProperty("_ShadowMap")) newMat.SetTexture("_ShadowMap", oldMat.GetTexture("_ShadowMap"));

            // Create the new material as an asset
            string path = AssetDatabase.GetAssetPath(oldMat);
            string dir = Path.GetDirectoryName(path);
            string newPath = AssetDatabase.GenerateUniqueAssetPath($"{dir}/{newMat.name}.mat");
            AssetDatabase.CreateAsset(newMat, newPath);

            newMaterials[i] = newMat;
        }

        renderer.sharedMaterials = newMaterials;
        UpdateMaterialData();
        
        EditorUtility.DisplayDialog("Success", $"Converted {newMaterials.Length} materials to the '{targetShader.name}' shader.", "OK");
    }

    // ... (The rest of the file remains the same)
}

using System;

public class VRClothFitterWindow : EditorWindow
{
    private class BoneInfo
    {
        public string name;
        public bool isHumanoid;
    }

    private GameObject avatarObject;
    private GameObject clothObject;

    private Vector2 scrollPositionBones;
    private Vector2 scrollPositionBlendshapes;

    // Bone mapping variables
    private List<BoneInfo> avatarBones = new List<BoneInfo>();
    private GUIContent[] avatarBonePopupContent;
    private List<BoneInfo> clothBones = new List<BoneInfo>();
    private int[] mappedBoneIndices;

    // Blendshape mapping variables
    private List<string> avatarBlendshapeNames = new List<string>();
    private string[] avatarBlendshapeNamesArray;
    private List<string> clothBlendshapeNames = new List<string>();
    private int[] mappedBlendshapeIndices;

    private bool isPreviewing = false;
    private Dictionary<Transform, Vector3> originalBoneScales;

    private const string NO_BONE_SELECTED = "[None]";
    private const string NO_BLENDSHAPE_SELECTED = "[None]";
    
    private GUIStyle boldLabelStyle;

    [MenuItem("Tools/VRCloth Fitter")]
    public static void ShowWindow()
    {
        GetWindow<VRClothFitterWindow>("VRCloth Fitter");
    }

    private void OnEnable()
    {
        boldLabelStyle = new GUIStyle(EditorStyles.label) { fontStyle = FontStyle.Bold };
    }

    private void OnDisable()
    {
        StopPreview();
    }

    private void OnGUI()
    {
        GUILayout.Label("VRCloth Fitter", EditorStyles.boldLabel);
        
        EditorGUILayout.Space();

        EditorGUI.BeginChangeCheck();
        avatarObject = (GameObject)EditorGUILayout.ObjectField("Avatar", avatarObject, typeof(GameObject), true);
        clothObject = (GameObject)EditorGUILayout.ObjectField("Cloth", clothObject, typeof(GameObject), true);
        if (EditorGUI.EndChangeCheck())
        {
            StopPreview();
            UpdateAllData();
        }

        EditorGUILayout.Space();

        // --- Bone Mapping UI ---
        GUILayout.Label("Bone Mapping", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Cloth Bone", EditorStyles.boldLabel);
        GUILayout.Label("->", GUILayout.Width(20));
        GUILayout.Label("Avatar Bone", EditorStyles.boldLabel);
        EditorGUILayout.EndHorizontal();

        scrollPositionBones = EditorGUILayout.BeginScrollView(scrollPositionBones, EditorStyles.helpBox, GUILayout.Height(150));
        {
            if (clothBones.Count > 0 && avatarBones.Count > 0)
            {
                for (int i = 0; i < clothBones.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(clothBones[i].name, clothBones[i].isHumanoid ? boldLabelStyle : EditorStyles.label);
                    GUILayout.Label("->", GUILayout.Width(20));
                    mappedBoneIndices[i] = EditorGUILayout.Popup(mappedBoneIndices[i], avatarBonePopupContent);
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

        // --- Blendshape Sync UI ---
        GUILayout.Label("Blendshape Sync", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Cloth Blendshape", EditorStyles.boldLabel);
        GUILayout.Label("->", GUILayout.Width(20));
        GUILayout.Label("Avatar Blendshape", EditorStyles.boldLabel);
        EditorGUILayout.EndHorizontal();

        scrollPositionBlendshapes = EditorGUILayout.BeginScrollView(scrollPositionBlendshapes, EditorStyles.helpBox, GUILayout.Height(150));
        {
            if (clothBlendshapeNames.Count > 0 && avatarBlendshapeNames.Count > 0)
            {
                for (int i = 0; i < clothBlendshapeNames.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(clothBlendshapeNames[i]);
                    GUILayout.Label("->", GUILayout.Width(20));
                    mappedBlendshapeIndices[i] = EditorGUILayout.Popup(mappedBlendshapeIndices[i], avatarBlendshapeNamesArray);
                    EditorGUILayout.EndHorizontal();
                }
            }
            else
            {
                GUILayout.Label("No blendshapes found or objects not set.");
            }
        }
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();

        // --- Action Buttons ---
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Fit Bones")) FitBones();
        if (GUILayout.Button("Calculate & Save Scale")) CalculateAndSaveScale();
        if (GUILayout.Button("Apply Blendshape Sync")) ApplyBlendshapeSync();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        GUI.color = isPreviewing ? Color.yellow : Color.white;
        if (GUILayout.Button(isPreviewing ? "Stop Preview" : "Toggle Preview")) TogglePreview();
        GUI.color = Color.white;
    }

    private void UpdateAllData()
    {
        var avatarRenderer = avatarObject?.GetComponentInChildren<SkinnedMeshRenderer>();
        var clothRenderer = clothObject?.GetComponentInChildren<SkinnedMeshRenderer>();
        var animator = avatarObject?.GetComponent<Animator>();

        UpdateBoneData(avatarRenderer, clothRenderer, animator);
        UpdateBlendshapeData(avatarRenderer, clothRenderer);
        
        Repaint();
    }

    private void UpdateBoneData(SkinnedMeshRenderer avatarRenderer, SkinnedMeshRenderer clothRenderer, Animator animator)
    {
        avatarBones.Clear();
        clothBones.Clear();

        var humanoidBoneNames = new HashSet<string>();
        if (animator != null && animator.isHuman)
        {
            foreach (HumanBodyBones boneType in Enum.GetValues(typeof(HumanBodyBones)))
            {
                if (boneType == HumanBodyBones.LastBone) continue;
                var boneTransform = animator.GetBoneTransform(boneType);
                if (boneTransform != null)
                {
                    humanoidBoneNames.Add(boneTransform.name);
                }
            }
        }

        if (avatarRenderer != null)
        {
            avatarBones = avatarRenderer.bones.Select(b => new BoneInfo { name = b.name, isHumanoid = humanoidBoneNames.Contains(b.name) }).ToList();
        }
        avatarBones.Insert(0, new BoneInfo { name = NO_BONE_SELECTED, isHumanoid = false });
        avatarBonePopupContent = avatarBones.Select(b => new GUIContent(b.isHumanoid ? $"{b.name} *" : b.name)).ToArray();

        if (clothRenderer != null)
        {
            clothBones = clothRenderer.bones.Select(b => new BoneInfo { name = b.name, isHumanoid = humanoidBoneNames.Contains(b.name) }).ToList();
        }
        
        mappedBoneIndices = new int[clothBones.Count];
        for (int i = 0; i < clothBones.Count; i++)
        {
            int foundIndex = avatarBones.FindIndex(b => b.name == clothBones[i].name);
            mappedBoneIndices[i] = (foundIndex != -1) ? foundIndex : 0;
        }
    }

    private void UpdateBlendshapeData(SkinnedMeshRenderer avatarRenderer, SkinnedMeshRenderer clothRenderer)
    {
        avatarBlendshapeNames.Clear();
        clothBlendshapeNames.Clear();

        if (avatarRenderer?.sharedMesh != null)
        {
            for (int i = 0; i < avatarRenderer.sharedMesh.blendShapeCount; i++)
            {
                avatarBlendshapeNames.Add(avatarRenderer.sharedMesh.GetBlendShapeName(i));
            }
        }
        avatarBlendshapeNames.Insert(0, NO_BLENDSHAPE_SELECTED);
        avatarBlendshapeNamesArray = avatarBlendshapeNames.ToArray();

        if (clothRenderer?.sharedMesh != null)
        {
            for (int i = 0; i < clothRenderer.sharedMesh.blendShapeCount; i++)
            {
                clothBlendshapeNames.Add(clothRenderer.sharedMesh.GetBlendShapeName(i));
            }
        }
        mappedBlendshapeIndices = new int[clothBlendshapeNames.Count];
        for (int i = 0; i < clothBlendshapeNames.Count; i++)
        {
            int foundIndex = avatarBlendshapeNames.FindIndex(bName => bName == clothBlendshapeNames[i]);
            mappedBlendshapeIndices[i] = (foundIndex != -1) ? foundIndex : 0;
        }
    }
    
    // ... (The rest of the methods remain largely the same, but need to be updated to use the new BoneInfo class)

    private void FitBones()
    {
        if (!GetRenderers(out var avatarRenderer, out var clothRenderer)) return;

        Undo.RecordObject(clothRenderer, "Fit Cloth Bones");

        var avatarBonesDict = avatarRenderer.bones.ToDictionary(b => b.name, b => b);
        var newClothBones = new Transform[clothRenderer.bones.Length];
        bool allBonesMapped = true;

        for (int i = 0; i < clothRenderer.bones.Length; i++)
        {
            int selectedIndex = mappedBoneIndices[i];
            if (selectedIndex > 0)
            {
                string selectedBoneName = avatarBones[selectedIndex].name;
                if (avatarBonesDict.TryGetValue(selectedBoneName, out Transform avatarBone))
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
        if (avatarBonesDict.ContainsKey(clothRenderer.rootBone.name))
        {
            avatarRootBone = avatarBonesDict[clothRenderer.rootBone.name];
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
    
    // ... (CalculateAndSaveScale, TogglePreview, StartPreview, StopPreview, CalculateAllBoneRadii need minor adjustments to use BoneInfo)
}
