using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;
using System.IO;
using nadena.dev.modular_avatar.core;

namespace VRClothFitter
{
    /// <summary>
    /// Custom editor for the VRClothFitter component.
    /// This script provides an intuitive, Inspector-based UI for all fitting operations,
    /// acting as an intelligent setup utility for Modular Avatar components.
    /// </summary>
    [CustomEditor(typeof(VRClothFitter))]
    public class VRClothFitterEditor : Editor
    {
        private VRClothFitter fitter;
        private GameObject avatarObject;
        private GameObject clothObject;

        private Vector2 scrollPositionBlendshapes;
        private Vector2 scrollPositionMaterials;

        // Blendshape mapping variables
        private List<string> avatarBlendshapeNames = new List<string>();
        private string[] avatarBlendshapeNamesArray;
        private List<string> clothBlendshapeNames = new List<string>();
        private int[] mappedBlendshapeIndices;

        // Material utility variables
        private Shader targetShader;
        private List<Material> clothMaterials = new List<Material>();
        
        private GUIStyle boldLabelStyle;
        private bool hasRefreshed = false;

        private void OnEnable()
        {
            fitter = (VRClothFitter)target;
            clothObject = fitter.gameObject;
            boldLabelStyle = new GUIStyle(EditorStyles.label) { fontStyle = FontStyle.Bold };
            
            UpdateAllData();
            hasRefreshed = true;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("avatarObject"));
            if (EditorGUI.EndChangeCheck() || (fitter.avatarObject != null && !hasRefreshed))
            {
                serializedObject.ApplyModifiedProperties();
                UpdateAllData();
                hasRefreshed = true;
            }

            if (fitter.avatarObject == null)
            {
                EditorGUILayout.HelpBox("Please assign an Avatar GameObject.", MessageType.Info);
                serializedObject.ApplyModifiedProperties();
                return;
            }

            EditorGUILayout.Space();

            // --- Proportional Scaling Section ---
            DrawProportionalScalingUI();

            EditorGUILayout.Space();

            // --- Blendshape Sync UI ---
            DrawBlendshapeSyncUI();

            EditorGUILayout.Space();
            
            // --- Material & Shader Utilities ---
            DrawMaterialUtilitiesUI();
            
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawProportionalScalingUI()
        {
            GUILayout.Label("Proportional Scaling", boldLabelStyle);
            EditorGUILayout.HelpBox("This will calculate the proportional differences between your avatar and the cloth's original armature, apply it to the cloth's bones, and set up MA Merge Armature for you.", MessageType.Info);
            if (GUILayout.Button("Calculate & Apply Proportional Scale"))
            {
                CalculateAndApplyProportionalScale();
            }
        }

        private void DrawBlendshapeSyncUI()
        {
            GUILayout.Label(VRClothFitterLocalization.Tr("Blendshape Sync"), boldLabelStyle);
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
                    GUILayout.Label(VRClothFitterLocalization.Tr("No blendshapes found."));
                }
            }
            EditorGUILayout.EndScrollView();
            if (GUILayout.Button(VRClothFitterLocalization.Tr("Apply Blendshape Sync"))) ApplyBlendshapeSync();
        }

        private void DrawMaterialUtilitiesUI()
        {
            GUILayout.Label(VRClothFitterLocalization.Tr("Material & Shader Utilities"), boldLabelStyle);
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
        }
        
        private void UpdateAllData()
        {
            if (fitter == null || fitter.avatarObject == null)
            {
                avatarBlendshapeNames.Clear();
                clothBlendshapeNames.Clear();
                clothMaterials.Clear();
                mappedBlendshapeIndices = new int[0];
                hasRefreshed = false;
                return;
            }

            avatarObject = fitter.avatarObject;
            var avatarRenderer = avatarObject?.GetComponentInChildren<SkinnedMeshRenderer>();
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
                if (renderer != null) clothMaterials = renderer.sharedMaterials.ToList();
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

            syncComponent.Bindings.Clear();

            for (int i = 0; i < clothBlendshapeNames.Count; i++)
            {
                int selectedIndex = mappedBlendshapeIndices[i];
                if (selectedIndex > 0)
                {
                    string clothBsName = clothBlendshapeNames[i];
                    string avatarBsName = avatarBlendshapeNamesArray[selectedIndex];
                    
                    var binding = new BlendshapeBinding();
                    var avatarRef = new AvatarObjectReference();
                    var objField = typeof(AvatarObjectReference).GetField("Object", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    if (objField != null) objField.SetValue(avatarRef, avatarRenderer);
                    
                    binding.ReferenceMesh = avatarRef;
                    binding.Blendshape = avatarBsName;
                    binding.LocalBlendshape = clothBsName;
                    
                    syncComponent.Bindings.Add(binding);
                }
            }
            
            EditorUtility.DisplayDialog("Success", $"Applied {syncComponent.Bindings.Count} blendshape sync rules.", "OK");
        }

        private bool GetRenderers(out SkinnedMeshRenderer avatarRenderer, out SkinnedMeshRenderer clothRenderer)
        {
            avatarRenderer = null;
            clothRenderer = null;

            if (avatarObject == null || clothObject == null) return false;

            avatarRenderer = avatarObject.GetComponentInChildren<SkinnedMeshRenderer>();
            clothRenderer = clothObject.GetComponentInChildren<SkinnedMeshRenderer>();

            return avatarRenderer != null && clothRenderer != null;
        }
    
        private void CalculateAndApplyProportionalScale()
        {
            if (!GetRenderers(out var avatarRenderer, out var clothRenderer))
            {
                EditorUtility.DisplayDialog("Error", "Avatar and Cloth must be set and contain SkinnedMeshRenderers.", "OK");
                return;
            }

            var clothBones = clothRenderer.bones;
            Undo.RecordObjects(clothBones, "Apply Proportional Scale to Bones");

            var avatarBonesDict = avatarRenderer.bones.ToDictionary(b => b.name, b => b);
            var clothBonesDict = clothBones.ToDictionary(b => b.name, b => b);

            var avatarBoneRadii = CalculateAllBoneRadii(avatarRenderer);
            var clothBoneRadii = CalculateAllBoneRadii(clothRenderer);

            int appliedCount = 0;
            foreach (var clothBone in clothBones)
            {
                if (!avatarBonesDict.TryGetValue(clothBone.name, out var avatarBone)) continue;

                float scaleY = 1.0f;
                if (clothBone.childCount > 0 && avatarBone.childCount > 0)
                {
                    float clothBoneLength = Vector3.Distance(clothBone.position, clothBone.GetChild(0).position);
                    float avatarBoneLength = Vector3.Distance(avatarBone.position, avatarBone.GetChild(0).position);
                    if (clothBoneLength > 0.0001f) scaleY = avatarBoneLength / clothBoneLength;
                }

                float scaleXZ = 1.0f;
                if (avatarBoneRadii.TryGetValue(avatarBone.name, out float avatarRadius) && clothBoneRadii.TryGetValue(clothBone.name, out float clothRadius))
                {
                    if (clothRadius > 0.0001f) scaleXZ = avatarRadius / clothRadius;
                }

                clothBone.localScale = new Vector3(scaleXZ, scaleY, scaleXZ);
                appliedCount++;
            }

            // Setup MA Merge Armature
            var mergeArmature = clothObject.GetComponent<MA Merge Armature>();
            if (mergeArmature == null)
            {
                mergeArmature = Undo.AddComponent<MA Merge Armature>(clothObject);
            }
            Undo.RecordObject(mergeArmature, "Configure MA Merge Armature");
            mergeArmature.matchBoneScale = true;

            EditorUtility.DisplayDialog("Success", $"Applied proportional scale to {appliedCount} bones and configured MA Merge Armature.", "OK");
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

                if (oldMat.HasProperty("_MainTex")) newMat.SetTexture("_MainTex", oldMat.GetTexture("_MainTex"));
                if (oldMat.HasProperty("_BumpMap")) newMat.SetTexture("_BumpMap", oldMat.GetTexture("_BumpMap"));
                if (oldMat.HasProperty("_NormalMap")) newMat.SetTexture("_NormalMap", oldMat.GetTexture("_NormalMap"));
                
                string path = AssetDatabase.GetAssetPath(oldMat);
                string dir = string.IsNullOrEmpty(path) ? "Assets" : Path.GetDirectoryName(path);
                string newPath = AssetDatabase.GenerateUniqueAssetPath($"{dir}/{newMat.name}.mat");
                AssetDatabase.CreateAsset(newMat, newPath);

                newMaterials[i] = newMat;
            }

            renderer.sharedMaterials = newMaterials;
            UpdateMaterialData();
            
            EditorUtility.DisplayDialog("Success", $"Converted {newMaterials.Length} materials.", "OK");
        }
    }
}
