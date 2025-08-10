using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using VRClothFitter;
using nadena.dev.modular_avatar.core;
using System;
using System.IO;

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
    private Vector2 scrollPositionMaterials;

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

    // Material utility variables
    private Shader targetShader;
    private List<Material> clothMaterials = new List<Material>();

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
        avatarObject = (GameObject)EditorGUILayout.ObjectField(VRClothFitterLocalization.Tr("Avatar"), avatarObject, typeof(GameObject), true);
        clothObject = (GameObject)EditorGUILayout.ObjectField(VRClothFitterLocalization.Tr("Cloth"), clothObject, typeof(GameObject), true);
        if (EditorGUI.EndChangeCheck())
        {
            StopPreview();
            UpdateAllData();
        }

        EditorGUILayout.Space();

        // --- Bone Mapping UI ---
        GUILayout.Label(VRClothFitterLocalization.Tr("Bone Mapping"), EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(VRClothFitterLocalization.Tr("Cloth Bone"), EditorStyles.boldLabel);
        GUILayout.Label("->", GUILayout.Width(20));
        GUILayout.Label(VRClothFitterLocalization.Tr("Avatar Bone"), EditorStyles.boldLabel);
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
                GUILayout.Label(VRClothFitterLocalization.Tr("Please set Avatar and Cloth objects."));
            }
        }
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();

        // --- Blendshape Sync UI ---
        GUILayout.Label(VRClothFitterLocalization.Tr("Blendshape Sync"), EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(VRClothFitterLocalization.Tr("Cloth Blendshape"), EditorStyles.boldLabel);
        GUILayout.Label("->", GUILayout.Width(20));
        GUILayout.Label(VRClothFitterLocalization.Tr("Avatar Blendshape"), EditorStyles.boldLabel);
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
                GUILayout.Label(VRClothFitterLocalization.Tr("No blendshapes found or objects not set."));
            }
        }
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();
        
        // --- Material & Shader Utilities ---
        GUILayout.Label(VRClothFitterLocalization.Tr("Material & Shader Utilities"), EditorStyles.boldLabel);
        targetShader = (Shader)EditorGUILayout.ObjectField(VRClothFitterLocalization.Tr("Target Shader"), targetShader, typeof(Shader), false);

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
                GUILayout.Label(VRClothFitterLocalization.Tr("No materials found on cloth object."));
            }
        }
        EditorGUILayout.EndScrollView();

        if (GUILayout.Button(VRClothFitterLocalization.Tr("Convert Materials"))) ConvertMaterials();

        EditorGUILayout.Space();

        // --- Action Buttons ---
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(VRClothFitterLocalization.Tr("Fit Bones"))) FitBones();
        if (GUILayout.Button(VRClothFitterLocalization.Tr("Calculate & Save Scale"))) CalculateAndSaveScale();
        if (GUILayout.Button(VRClothFitterLocalization.Tr("Apply Blendshape Sync"))) ApplyBlendshapeSync();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        GUI.color = isPreviewing ? Color.yellow : Color.white;
        if (GUILayout.Button(isPreviewing ? VRClothFitterLocalization.Tr("Stop Preview") : VRClothFitterLocalization.Tr("Toggle Preview"))) TogglePreview();
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
        avatarBones.Insert(0, new BoneInfo { name = VRClothFitterLocalization.Tr("None"), isHumanoid = false });
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
        avatarBlendshapeNames.Insert(0, VRClothFitterLocalization.Tr("None"));
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

    private void ApplyBlendshapeSync()
    {
        if (clothObject == null || avatarObject == null) return;

        var avatarRenderer = avatarObject.GetComponentInChildren<SkinnedMeshRenderer>();
        if (avatarRenderer == null) return;

        var syncComponent = clothObject.GetComponent<ModularAvatarBlendshapeSync>();
        if (syncComponent == null)
        {
            syncComponent = Undo.AddComponent<ModularAvatarBlendshapeSync>(clothObject);
        }
        Undo.RecordObject(syncComponent, "Apply Blendshape Sync");

        // This is the correct property according to the source code.
        // It seems the API has changed over time.
        // We set the reference mesh directly on the component.
        syncComponent.ReferenceMesh = new AvatarObjectReference(avatarRenderer);

        // The list of bindings is now a simple string-to-string map.
        syncComponent.Bindings.Clear();

        for (int i = 0; i < clothBlendshapeNames.Count; i++)
        {
            int selectedIndex = mappedBlendshapeIndices[i];
            if (selectedIndex > 0) // 0 is [None]
            {
                string clothBsName = clothBlendshapeNames[i];
                string avatarBsName = avatarBlendshapeNamesArray[selectedIndex];
                
                // The BlendshapeBinding struct is simpler now.
                syncComponent.Bindings.Add(new BlendshapeBinding
                {
                    Blendshape = avatarBsName,
                    LocalBlendshape = clothBsName
                });
            }
        }
        
        EditorUtility.DisplayDialog("Success", $"Applied {syncComponent.Bindings.Count} blendshape sync rules.", "OK");
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
    
    private List<BoneScaleInfo> CalculateScale()
    {
        var scaleInfos = new List<BoneScaleInfo>();
        if (!GetRenderers(out var avatarRenderer, out var clothRenderer)) return scaleInfos;

        var avatarBonesDict = avatarRenderer.bones.ToDictionary(b => b.name, b => b);
        var clothBonesOriginal = clothRenderer.bones.ToDictionary(b => b.name, b => b);

        var avatarBoneRadii = CalculateAllBoneRadii(avatarRenderer);
        var clothBoneRadii = CalculateAllBoneRadii(clothRenderer);

        for (int i = 0; i < clothBones.Count; i++)
        {
            string clothBoneName = clothBones[i].name;
            int selectedIndex = mappedBoneIndices[i];

            if (selectedIndex == 0 || !clothBonesOriginal.TryGetValue(clothBoneName, out var clothBone)) continue;

            string selectedBoneName = avatarBones[selectedIndex].name;
            if (!avatarBonesDict.TryGetValue(selectedBoneName, out Transform avatarBone))
            {
                continue;
            }

            float scaleY = 1.0f;
            if (clothBone.childCount > 0 && avatarBone.childCount > 0)
            {
                float clothBoneLength = Vector3.Distance(clothBone.position, clothBone.GetChild(0).position);
                float avatarBoneLength = Vector3.Distance(avatarBone.position, avatarBone.GetChild(0).position);
                if (clothBoneLength > 0.0001f)
                {
                    scaleY = avatarBoneLength / clothBoneLength;
                }
            }

            float scaleXZ = 1.0f;
            if (avatarBoneRadii.TryGetValue(avatarBone.name, out float avatarRadius) && clothBoneRadii.TryGetValue(clothBone.name, out float clothRadius))
            {
                if (clothRadius > 0.0001f)
                {
                    scaleXZ = avatarRadius / clothRadius;
                }
            }

            scaleInfos.Add(new BoneScaleInfo
            {
                boneName = clothBoneName,
                scale = new Vector3(scaleXZ, scaleY, scaleXZ)
            });
        }
        return scaleInfos;
    }

    private void CalculateAndSaveScale()
    {
        if (clothObject == null) return;
        
        var scalingData = clothObject.GetComponent<VRClothFitterScalingData>();
        if (scalingData == null)
        {
            scalingData = Undo.AddComponent<VRClothFitterScalingData>(clothObject);
        }
        Undo.RecordObject(scalingData, "Calculate and Save Bone Scale");

        scalingData.boneScales = CalculateScale();
        
        EditorUtility.DisplayDialog("Success", $"{scalingData.boneScales.Count}個のボーンスケール情報を計算し、{clothObject.name}のVRClothFitterScalingDataコンポーネントに保存しました。", "OK");
    }

    private void TogglePreview()
    {
        if (isPreviewing)
        {
            StopPreview();
        }
        else
        {
            StartPreview();
        }
    }

    private void StartPreview()
    {
        if (!GetRenderers(out _, out var clothRenderer)) return;

        originalBoneScales = new Dictionary<Transform, Vector3>();
        foreach (var bone in clothRenderer.bones)
        {
            if (bone != null)
            {
                originalBoneScales[bone] = bone.localScale;
            }
        }

        var scaleInfos = CalculateScale();
        var clothBonesDict = clothRenderer.bones.ToDictionary(b => b.name, b => b);

        foreach (var info in scaleInfos)
        {
            if (clothBonesDict.TryGetValue(info.boneName, out var bone))
            {
                bone.localScale = Vector3.Scale(bone.localScale, info.scale);
            }
        }

        isPreviewing = true;
    }

    private void StopPreview()
    {
        if (!isPreviewing || originalBoneScales == null) return;

        foreach (var pair in originalBoneScales)
        {
            if (pair.Key != null)
            {
                pair.Key.localScale = pair.Value;
            }
        }

        originalBoneScales = null;
        isPreviewing = false;
    }

    private Dictionary<string, float> CalculateAllBoneRadii(SkinnedMeshRenderer renderer)
    {
        var boneRadii = new Dictionary<string, float>();
        if (renderer == null || renderer.sharedMesh == null) return boneRadii;

        var mesh = renderer.sharedMesh;
        var boneWeights = mesh.GetAllBoneWeights();
        var vertices = mesh.vertices;
        var bones = renderer.bones;

        var boneVertexLists = new Dictionary<int, List<Vector3>>();
        for (int i = 0; i < bones.Length; i++)
        {
            if(bones[i] != null) boneVertexLists[i] = new List<Vector3>();
        }

        for (int i = 0; i < boneWeights.Length; i++)
        {
            var boneWeight = boneWeights[i];
            if (boneWeight.boneIndex0 >= 0 && boneWeight.weight0 > 0 && boneVertexLists.ContainsKey(boneWeight.boneIndex0)) boneVertexLists[boneWeight.boneIndex0].Add(vertices[i]);
            if (boneWeight.boneIndex1 >= 0 && boneWeight.weight1 > 0 && boneVertexLists.ContainsKey(boneWeight.boneIndex1)) boneVertexLists[boneWeight.boneIndex1].Add(vertices[i]);
            if (boneWeight.boneIndex2 >= 0 && boneWeight.weight2 > 0 && boneVertexLists.ContainsKey(boneWeight.boneIndex2)) boneVertexLists[boneWeight.boneIndex2].Add(vertices[i]);
            if (boneWeight.boneIndex3 >= 0 && boneWeight.weight3 > 0 && boneVertexLists.ContainsKey(boneWeight.boneIndex3)) boneVertexLists[boneWeight.boneIndex3].Add(vertices[i]);
        }

        for (int i = 0; i < bones.Length; i++)
        {
            var bone = bones[i];
            if (bone == null || !boneVertexLists.ContainsKey(i)) continue;

            var vertexList = boneVertexLists[i];
            if (vertexList.Count == 0) continue;

            float totalDistance = 0;
            foreach (var vertex in vertexList)
            {
                var worldVertex = renderer.transform.TransformPoint(vertex);
                totalDistance += Vector3.Distance(worldVertex, bone.position);
            }
            boneRadii[bone.name] = totalDistance / vertexList.Count;
        }

        return boneRadii;
    }
    
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
            $"This will create new materials and replace them on '{clothObject.name}'. The original material assets will not be modified.\n\nContinue?",
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
            if (oldMat == null) continue;
            
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
            string dir = string.IsNullOrEmpty(path) ? "Assets" : Path.GetDirectoryName(path);
            string newPath = AssetDatabase.GenerateUniqueAssetPath($"{dir}/{newMat.name}.mat");
            AssetDatabase.CreateAsset(newMat, newPath);

            newMaterials[i] = newMat;
        }

        renderer.sharedMaterials = newMaterials;
        UpdateMaterialData();
        
        EditorUtility.DisplayDialog("Success", $"Converted {newMaterials.Length} materials to the '{targetShader.name}' shader.", "OK");
    }
}