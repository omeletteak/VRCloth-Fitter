using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using VRClothFitter;

[CustomEditor(typeof(VRClothFitterDeformationData))]
public class VRClothFitterDeformationDataEditor : Editor
{
    private ReorderableList anchorList;
    private VRClothFitterDeformationData data;

    private void OnEnable()
    {
        data = (VRClothFitterDeformationData)target;
        
        anchorList = new ReorderableList(serializedObject, 
            serializedObject.FindProperty("anchorPairs"), 
            true, true, true, true);

        anchorList.drawHeaderCallback = (Rect rect) => {
            EditorGUI.LabelField(rect, "Anchor Points");
        };

        anchorList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) => {
            var element = anchorList.serializedProperty.GetArrayElementAtIndex(index);
            rect.y += 2;
            EditorGUI.PropertyField(
                new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight),
                element.FindPropertyRelative("name"), GUIContent.none);
        };
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        
        EditorGUILayout.PropertyField(serializedObject.FindProperty("avatarRoot"));
        
        EditorGUILayout.Space();
        
        anchorList.DoLayoutList();
        
        serializedObject.ApplyModifiedProperties();
    }

    private void OnSceneGUI()
    {
        if (data == null || data.avatarRoot == null) return;

        var clothTransform = data.transform;
        var avatarTransform = data.avatarRoot.transform;

        for (int i = 0; i < data.anchorPairs.Count; i++)
        {
            var pair = data.anchorPairs[i];

            // --- Draw Avatar Anchor ---
            Handles.color = Color.blue;
            Vector3 avatarWorldPos = avatarTransform.TransformPoint(pair.avatarAnchor);
            
            if (anchorList.index == i) // If this anchor is selected in the list
            {
                EditorGUI.BeginChangeCheck();
                avatarWorldPos = Handles.PositionHandle(avatarWorldPos, Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(data, "Move Avatar Anchor");
                    pair.avatarAnchor = avatarTransform.InverseTransformPoint(avatarWorldPos);
                }
            }
            else
            {
                Handles.SphereHandleCap(0, avatarWorldPos, Quaternion.identity, 0.02f, EventType.Repaint);
            }

            // --- Draw Cloth Anchor ---
            Handles.color = Color.green;
            Vector3 clothWorldPos = clothTransform.TransformPoint(pair.clothAnchor);

            if (anchorList.index == i) // If this anchor is selected in the list
            {
                EditorGUI.BeginChangeCheck();
                clothWorldPos = Handles.PositionHandle(clothWorldPos, Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(data, "Move Cloth Anchor");
                    pair.clothAnchor = clothTransform.InverseTransformPoint(clothWorldPos);
                }
            }
            else
            {
                Handles.SphereHandleCap(0, clothWorldPos, Quaternion.identity, 0.02f, EventType.Repaint);
            }
            
            // --- Draw Line between pair ---
            Handles.color = Color.yellow;
            Handles.DrawLine(avatarWorldPos, clothWorldPos);
        }
    }
}
