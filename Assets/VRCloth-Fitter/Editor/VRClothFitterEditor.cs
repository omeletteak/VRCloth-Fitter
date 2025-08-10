using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;
using System.IO;
using nadena.dev.modular_avatar.core;

namespace VRClothFitter
{
    [CustomEditor(typeof(VRClothFitter))]
    public class VRClothFitterEditor : Editor
    {
        private VRClothFitter fitter;
        private GameObject clothObject;

        private Vector2 scrollPositionBlendshapes;
        private Vector2 scrollPositionMaterials;
        private Vector2 scrollPositionHardMaterials;

        private List<string> avatarBlendshapeNames = new List<string>();
        private string[] avatarBlendshapeNamesArray;
        private List<string> clothBlendshapeNames = new List<string>();
        private int[] mappedBlendshapeIndices;

        private Shader targetShader;
        private List<Material> clothMaterials = new List<Material>();
        
        private List<Material> hardPartCandidateMaterials = new List<Material>();
        private Dictionary<Material, bool> selectedHardMaterials = new Dictionary<Material, bool>();
        
        private GUIStyle boldLabelStyle;
        private bool isPreviewing = false;
        private Dictionary<Transform, Vector3> originalBoneScales;

        private void OnEnable()
        {
            fitter = (VRClothFitter)target;
            clothObject = fitter.gameObject;
            boldLabelStyle = new GUIStyle(EditorStyles.label) { fontStyle = FontStyle.Bold };
            UpdateAllData();
        }

        private void OnDisable()
        {
            StopPreview();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // 自動で親のアバターを検出する
            if (fitter.targetAvatarObject == null)
            {
                var parentAnimator = fitter.GetComponentInParent<Animator>();
                if (parentAnimator != null)
                {
                    fitter.targetAvatarObject = parentAnimator.gameObject;
                    EditorUtility.SetDirty(fitter);
                }
            }

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("targetAvatarObject"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("sourceAvatarObject"));
            
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                UpdateAllData();
            }

            if (fitter.targetAvatarObject == null)
            {
                EditorGUILayout.HelpBox("Please assign a Target Avatar GameObject.", MessageType.Info);
                serializedObject.ApplyModifiedProperties();
                return;
            }

            EditorGUILayout.Space();
            DrawProportionalScalingUI();
            EditorGUILayout.Space();
            DrawGhostAvatarUI();
            EditorGUILayout.Space();
            DrawBlendshapeSyncUI();
            EditorGUILayout.Space();
            DrawMaterialUtilitiesUI();
            
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawProportionalScalingUI()
        {
            GUILayout.Label("Proportional Scaling", boldLabelStyle);
            
            string helpText = fitter.sourceAvatarObject == null 
                ? "Mode: Fallback. Compares Target Avatar to the cloth's bones."
                : "Mode: High-Precision. Compares Source and Target avatars directly.";
            EditorGUILayout.HelpBox(helpText, MessageType.Info);

            if (GUILayout.Button("Calculate & Save Scale Data"))
            {
                CalculateAndSaveScaleData(null);
            }

            GUI.enabled = clothObject.GetComponent<VRClothFitterScalingData>() != null;
            GUI.color = isPreviewing ? Color.yellow : Color.white;
            if (GUILayout.Button(isPreviewing ? "Stop Preview" : "Start Preview"))
            {
                TogglePreview();
            }
            GUI.color = Color.white;
            GUI.enabled = true;
        }
        
        private void DrawGhostAvatarUI()
        {
            GUILayout.Label("Ghost Avatar Estimation", boldLabelStyle);
            EditorGUILayout.HelpBox("Experimental: Estimates the source avatar's body shape from the cloth mesh. Use if you don't have the source avatar.", MessageType.Info);

            if (GUILayout.Button("1. Detect Hard Part Candidates"))
            {
                DetectHardPartCandidates();
            }

            if (hardPartCandidateMaterials.Count > 0)
            {
                EditorGUILayout.LabelField("Select Hard Surface Materials:", EditorStyles.boldLabel);
                scrollPositionHardMaterials = EditorGUILayout.BeginScrollView(scrollPositionHardMaterials, EditorStyles.helpBox, GUILayout.Height(100));
                {
                    var keys = new List<Material>(selectedHardMaterials.Keys);
                    foreach (var mat in keys)
                    {
                        if (mat != null)
                        {
                            selectedHardMaterials[mat] = EditorGUILayout.ToggleLeft(mat.name, selectedHardMaterials[mat]);
                        }
                    }
                }
                EditorGUILayout.EndScrollView();
                
                if (GUILayout.Button("2. Generate Ghost & Use for Scaling"))
                {
                    GenerateGhostAndScale();
                }
            }
        }

        private void DrawBlendshapeSyncUI()
        {
            GUILayout.Label("Blendshape Sync", boldLabelStyle);
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
                    GUILayout.Label("No blendshapes found.");
                }
            }
            EditorGUILayout.EndScrollView();
            if (GUILayout.Button("Apply Blendshape Sync")) ApplyBlendshapeSync();
        }

        private void DrawMaterialUtilitiesUI()
        {
            GUILayout.Label("Material & Shader Utilities", boldLabelStyle);
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
        }
        
        private void UpdateAllData()
        {
            if (fitter == null || fitter.targetAvatarObject == null)
            {
                avatarBlendshapeNames.Clear();
                clothBlendshapeNames.Clear();
                clothMaterials.Clear();
                mappedBlendshapeIndices = new int[0];
                return;
            }

            var avatarRenderer = fitter.targetAvatarObject?.GetComponentInChildren<SkinnedMeshRenderer>();
            var clothRenderer = clothObject?.GetComponentInChildren<SkinnedMeshRenderer>();

            UpdateBlendshapeData(avatarRenderer, clothRenderer);
            UpdateMaterialData();
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
            avatarBlendshapeNames.Insert(0, "None");
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
                if (renderer != null) clothMaterials = renderer.sharedMaterials.ToList();
            }
        }

        private void ApplyBlendshapeSync()
        {
            if (clothObject == null || fitter.targetAvatarObject == null) return;
            var avatarRenderer = fitter.targetAvatarObject.GetComponentInChildren<SkinnedMeshRenderer>();
            if (avatarRenderer == null) return;

            var syncComponent = clothObject.GetComponent<ModularAvatarBlendshapeSync>();
            if (syncComponent == null) syncComponent = Undo.AddComponent<ModularAvatarBlendshapeSync>(clothObject);
            Undo.RecordObject(syncComponent, "Apply Blendshape Sync");

            syncComponent.Bindings.Clear();
            for (int i = 0; i < clothBlendshapeNames.Count; i++)
            {
                int selectedIndex = mappedBlendshapeIndices[i];
                if (selectedIndex > 0)
                {
                    var binding = new BlendshapeBinding();
                    var avatarRef = new AvatarObjectReference();
                    var objField = typeof(AvatarObjectReference).GetField("Object", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    if (objField != null) objField.SetValue(avatarRef, avatarRenderer);
                    
                    binding.ReferenceMesh = avatarRef;
                    binding.Blendshape = avatarBlendshapeNamesArray[selectedIndex];
                    binding.LocalBlendshape = clothBlendshapeNames[i];
                    syncComponent.Bindings.Add(binding);
                }
            }
            EditorUtility.DisplayDialog("Success", $"Applied {syncComponent.Bindings.Count} blendshape sync rules.", "OK");
        }

        private bool GetRenderers(out SkinnedMeshRenderer targetAvatarRenderer, out SkinnedMeshRenderer clothRenderer, out SkinnedMeshRenderer sourceAvatarRenderer)
        {
            targetAvatarRenderer = null;
            clothRenderer = null;
            sourceAvatarRenderer = null;

            if (fitter.targetAvatarObject == null || clothObject == null) return false;

            targetAvatarRenderer = fitter.targetAvatarObject.GetComponentInChildren<SkinnedMeshRenderer>();
            clothRenderer = clothObject.GetComponentInChildren<SkinnedMeshRenderer>();
            if (fitter.sourceAvatarObject != null)
            {
                sourceAvatarRenderer = fitter.sourceAvatarObject.GetComponentInChildren<SkinnedMeshRenderer>();
            }
            return targetAvatarRenderer != null && clothRenderer != null;
        }
    
        private void CalculateAndSaveScaleData(SkinnedMeshRenderer ghostRenderer)
        {
            if (!GetRenderers(out var targetAvatarRenderer, out var clothRenderer, out var sourceAvatarRenderer) && ghostRenderer == null)
            {
                EditorUtility.DisplayDialog("Error", "Renderers not found.", "OK");
                return;
            }

            var sourceComparisonRenderer = ghostRenderer != null ? ghostRenderer : (sourceAvatarRenderer != null ? sourceAvatarRenderer : clothRenderer);

            var targetAvatarBonesDict = targetAvatarRenderer.bones.ToDictionary(b => b.name, b => b);
            var sourceBonesDict = sourceComparisonRenderer.bones.ToDictionary(b => b.name, b => b);
            var targetAvatarBoneRadii = CalculateAllBoneRadii(targetAvatarRenderer);
            var sourceBoneRadii = CalculateAllBoneRadii(sourceComparisonRenderer);

            var scaleInfos = new List<BoneScaleInfo>();
            foreach (var clothBone in clothRenderer.bones)
            {
                if (!sourceBonesDict.TryGetValue(clothBone.name, out var sourceBone)) continue;
                if (!targetAvatarBonesDict.TryGetValue(clothBone.name, out var targetBone)) continue;

                float scaleY = 1.0f;
                if (sourceBone.childCount > 0 && targetBone.childCount > 0)
                {
                    float sourceBoneLength = Vector3.Distance(sourceBone.position, sourceBone.GetChild(0).position);
                    float targetBoneLength = Vector3.Distance(targetBone.position, targetBone.GetChild(0).position);
                    if (sourceBoneLength > 0.0001f) scaleY = targetBoneLength / sourceBoneLength;
                }

                float scaleXZ = 1.0f;
                if (targetAvatarBoneRadii.TryGetValue(targetBone.name, out float targetRadius) && sourceBoneRadii.TryGetValue(sourceBone.name, out float sourceRadius))
                {
                    if (sourceRadius > 0.0001f) scaleXZ = targetRadius / sourceRadius;
                }
                scaleInfos.Add(new BoneScaleInfo { boneName = clothBone.name, scale = new Vector3(scaleXZ, scaleY, scaleXZ) });
            }

            var scalingData = clothObject.GetComponent<VRClothFitterScalingData>();
            if (scalingData == null) scalingData = Undo.AddComponent<VRClothFitterScalingData>(clothObject);
            Undo.RecordObject(scalingData, "Save Scale Data");
            scalingData.boneScales = scaleInfos;

            if (clothObject.GetComponent<ScalingHook>() == null) Undo.AddComponent<ScalingHook>(clothObject);
            if (clothObject.GetComponent<ModularAvatarMergeArmature>() == null) Undo.AddComponent<ModularAvatarMergeArmature>(clothObject);

            EditorUtility.DisplayDialog("Success", $"Calculated and saved {scaleInfos.Count} bone scales.", "OK");
        }

        private void TogglePreview()
        {
            if (isPreviewing) StopPreview();
            else StartPreview();
        }

        private void StartPreview()
        {
            var scalingData = clothObject.GetComponent<VRClothFitterScalingData>();
            var clothRenderer = clothObject.GetComponent<SkinnedMeshRenderer>();
            if (scalingData == null || clothRenderer == null) return;

            originalBoneScales = new Dictionary<Transform, Vector3>();
            foreach (var bone in clothRenderer.bones)
            {
                if (bone != null) originalBoneScales[bone] = bone.localScale;
            }

            foreach (var info in scalingData.boneScales)
            {
                var bone = clothRenderer.bones.FirstOrDefault(b => b.name == info.boneName);
                if (bone != null) bone.localScale = Vector3.Scale(bone.localScale, info.scale);
            }
            isPreviewing = true;
        }

        private void StopPreview()
        {
            if (!isPreviewing || originalBoneScales == null) return;
            foreach (var pair in originalBoneScales)
            {
                if (pair.Key != null) pair.Key.localScale = pair.Value;
            }
            originalBoneScales = null;
            isPreviewing = false;
        }

        private void DetectHardPartCandidates()
        {
            var clothRenderer = clothObject.GetComponentInChildren<SkinnedMeshRenderer>();
            if (clothRenderer == null || clothRenderer.sharedMesh == null) return;

            var mesh = clothRenderer.sharedMesh;
            var boneWeights = mesh.boneWeights;
            var materials = clothRenderer.sharedMaterials;
            var candidateMaterialSet = new HashSet<Material>();

            var vertexToSubmeshMap = new Dictionary<int, int>();
            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                foreach (var vertIndex in mesh.GetTriangles(i)) vertexToSubmeshMap[vertIndex] = i;
            }

            for (int i = 0; i < boneWeights.Length; i++)
            {
                var weight = boneWeights[i];
                if (weight.weight0 > 0.99f && weight.weight1 == 0 && weight.weight2 == 0 && weight.weight3 == 0)
                {
                    if (vertexToSubmeshMap.TryGetValue(i, out int submeshIndex) && submeshIndex < materials.Length)
                    {
                        candidateMaterialSet.Add(materials[submeshIndex]);
                    }
                }
            }

            hardPartCandidateMaterials = candidateMaterialSet.ToList();
            selectedHardMaterials.Clear();
            foreach (var mat in hardPartCandidateMaterials) selectedHardMaterials[mat] = true;
            
            EditorUtility.DisplayDialog("Detection Complete", $"{hardPartCandidateMaterials.Count} candidate materials found.", "OK");
        }

        private void GenerateGhostAndScale()
        {
            var clothRenderer = clothObject.GetComponentInChildren<SkinnedMeshRenderer>();
            if (clothRenderer == null || clothRenderer.sharedMesh == null) return;

            var originalMesh = clothRenderer.sharedMesh;
            var ghostMesh = Instantiate(originalMesh);
            ghostMesh.name = $"{originalMesh.name} (Ghost)";

            var vertices = originalMesh.vertices;
            var normals = originalMesh.normals;
            var materials = clothRenderer.sharedMaterials;
            var newVertices = new Vector3[vertices.Length];

            var hardMaterialIndices = new HashSet<int>();
            for (int i = 0; i < materials.Length; i++)
            {
                if (selectedHardMaterials.ContainsKey(materials[i]) && selectedHardMaterials[materials[i]])
                {
                    hardMaterialIndices.Add(i);
                }
            }

            var vertexToSubmeshMap = new Dictionary<int, int>();
            for (int i = 0; i < originalMesh.subMeshCount; i++)
            {
                foreach (var vertIndex in originalMesh.GetTriangles(i)) vertexToSubmeshMap[vertIndex] = i;
            }

            for (int i = 0; i < vertices.Length; i++)
            {
                if (vertexToSubmeshMap.TryGetValue(i, out int submeshIndex) && hardMaterialIndices.Contains(submeshIndex))
                {
                    newVertices[i] = vertices[i];
                }
                else
                {
                    float skinDistance = 0.01f; 
                    newVertices[i] = vertices[i] - (normals[i] * skinDistance);
                }
            }

            ghostMesh.vertices = newVertices;
            ghostMesh.RecalculateBounds();
            ghostMesh.RecalculateNormals();

            var ghostObject = new GameObject("_GhostForScaling");
            var ghostRenderer = ghostObject.AddComponent<SkinnedMeshRenderer>();
            ghostRenderer.sharedMesh = ghostMesh;
            ghostRenderer.bones = clothRenderer.bones;
            ghostRenderer.rootBone = clothRenderer.rootBone;

            CalculateAndSaveScaleData(ghostRenderer);

            DestroyImmediate(ghostObject);
        }

        private Dictionary<string, float> CalculateAllBoneRadii(SkinnedMeshRenderer renderer)
        {
            var boneRadii = new Dictionary<string, float>();
            if (renderer == null || renderer.sharedMesh == null) return boneRadii;

            var mesh = renderer.sharedMesh;
            var boneWeights = mesh.boneWeights;
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
            if (clothObject == null || targetShader == null) return;
            var renderer = clothObject.GetComponentInChildren<SkinnedMeshRenderer>();
            if (renderer == null) return;

            if (!EditorUtility.DisplayDialog("Confirm Material Conversion",
                "This will create new materials and replace them on '" + clothObject.name + "'. The original material assets will not be modified.\n\nContinue?",
                "Convert", "Cancel")) return;

            Undo.RecordObject(renderer, "Convert Materials");
            var originalMaterials = renderer.sharedMaterials;
            var newMaterials = new Material[originalMaterials.Length];

            for (int i = 0; i < originalMaterials.Length; i++)
            {
                var oldMat = originalMaterials[i];
                if (oldMat == null) continue;
                
                var newMat = new Material(targetShader);
                newMat.name = $"{oldMat.name}_{targetShader.name}";

                if (oldMat.HasProperty("_MainTex")) newMat.SetTexture("_MainTex", oldMat.GetTexture("_MainTex"));
                if (oldMat.HasProperty("_BumpMap")) newMat.SetTexture("_BumpMap", oldMat.GetTexture("_BumpMap"));
                if (oldMat.HasProperty("_NormalMap")) newMat.SetTexture("_NormalMap", oldMat.GetTexture("_NormalMap"));
                
                string path = AssetDatabase.GetAssetPath(oldMat);
                string dir = string.IsNullOrEmpty(path) ? "Assets" : Path.GetDirectoryName(path);
                string newPath = AssetDatabase.GenerateUniqueAssetPath($"{dir}/{{newMat.name}}.mat");
                AssetDatabase.CreateAsset(newMat, newPath);
                newMaterials[i] = newMat;
            }
            renderer.sharedMaterials = newMaterials;
            UpdateMaterialData();
            EditorUtility.DisplayDialog("Success", $"Converted {newMaterials.Length} materials.", "OK");
        }
    }
}
