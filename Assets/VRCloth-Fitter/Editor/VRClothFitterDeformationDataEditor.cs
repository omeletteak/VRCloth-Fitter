using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using VRClothFitter;

[CustomEditor(typeof(VRClothFitterDeformationData))]
public class VRClothFitterDeformationDataEditor : Editor
{
    private ReorderableList anchorList;
    private VRClothFitterDeformationData data;

    private static bool isPlacingAnchor = false;
    private static bool hasPlacedAvatarAnchor = false;
    private static Vector3 pendingAvatarAnchor;
    private static VRClothFitterDeformationDataEditor activeEditor;
    
    private static MeshCollider avatarCollider;
    private static MeshCollider clothCollider;

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
    
    private void OnDisable()
    {
        FinishPlacing();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        
        EditorGUILayout.PropertyField(serializedObject.FindProperty("avatarRoot"));
        
        EditorGUILayout.Space();
        
        anchorList.DoLayoutList();
        
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();

        string buttonText = isPlacingAnchor ? "Cancel Placing Anchor" : "Add New Anchor Pair";
        GUI.color = isPlacingAnchor ? Color.yellow : Color.white;
        if (GUILayout.Button(buttonText))
        {
            if (isPlacingAnchor)
            {
                FinishPlacing();
            }
            else
            {
                StartPlacing();
            }
        }
        GUI.color = Color.white;
    }

    private void StartPlacing()
    {
        if (data.avatarRoot == null)
        {
            EditorUtility.DisplayDialog("Error", "Avatar Root must be set before placing anchors.", "OK");
            return;
        }
        isPlacingAnchor = true;
        hasPlacedAvatarAnchor = false;
        activeEditor = this;
        
        avatarCollider = AddTempCollider(data.avatarRoot);
        clothCollider = AddTempCollider(data.gameObject);
        
        SceneView.FocusWindowIfItsOpen<SceneView>();
    }

    private void FinishPlacing()
    {
        if (avatarCollider != null) DestroyImmediate(avatarCollider);
        if (clothCollider != null) DestroyImmediate(clothCollider);
        isPlacingAnchor = false;
        activeEditor = null;
        SceneView.RepaintAll();
    }
    
    private MeshCollider AddTempCollider(GameObject obj)
    {
        var renderer = obj.GetComponentInChildren<SkinnedMeshRenderer>();
        if (renderer == null) return null;

        var colliderGo = renderer.gameObject;
        var collider = colliderGo.AddComponent<MeshCollider>();
        collider.sharedMesh = renderer.sharedMesh;
        collider.hideFlags = HideFlags.HideAndDontSave;
        return collider;
    }

    private void OnSceneGUI()
    {
        if (isPlacingAnchor && activeEditor == this)
        {
            HandleAnchorPlacement();
        }
        else
        {
            DrawAndManipulateAnchors();
        }
    }

    private void HandleAnchorPlacement()
    {
        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
        
        Handles.BeginGUI();
        GUILayout.Window(1, new Rect(10, 30, 250, 60), (id) =>
        {
            if (!hasPlacedAvatarAnchor)
            {
                GUILayout.Label("1. Click on the AVATAR mesh to place the first anchor point.");
            }
            else
            {
                GUILayout.Label("2. Click on the CLOTH mesh to place the second anchor point.");
            }
            GUILayout.Label("Press ESC to cancel.");
        }, "Place New Anchor");
        Handles.EndGUI();

        if (hasPlacedAvatarAnchor)
        {
            Handles.color = Color.cyan;
            Handles.SphereHandleCap(0, data.avatarRoot.transform.TransformPoint(pendingAvatarAnchor), Quaternion.identity, 0.02f, EventType.Repaint);
        }

        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
        {
            FinishPlacing();
            Event.current.Use();
            return;
        }

        if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);

            if (!hasPlacedAvatarAnchor)
            {
                if (avatarCollider != null && avatarCollider.Raycast(ray, out RaycastHit hit, 100f))
                {
                    pendingAvatarAnchor = avatarCollider.transform.InverseTransformPoint(hit.point);
                    hasPlacedAvatarAnchor = true;
                    Event.current.Use();
                }
            }
            else
            {
                if (clothCollider != null && clothCollider.Raycast(ray, out RaycastHit hit, 100f))
                {
                    Vector3 clothAnchorPos = clothCollider.transform.InverseTransformPoint(hit.point);
                    
                    Undo.RecordObject(data, "Add Anchor Pair");
                    data.anchorPairs.Add(new DeformationAnchorPair
                    {
                        name = $"Anchor {data.anchorPairs.Count + 1}",
                        avatarAnchor = pendingAvatarAnchor,
                        clothAnchor = clothAnchorPos
                    });
                    
                    FinishPlacing();
                    Event.current.Use();
                }
            }
        }
    }

    private void DrawAndManipulateAnchors()
    {
        if (data == null || data.avatarRoot == null) return;

        var clothTransform = data.transform;
        var avatarTransform = data.avatarRoot.transform;

        for (int i = 0; i < data.anchorPairs.Count; i++)
        {
            var pair = data.anchorPairs[i];

            Handles.color = Color.blue;
            Vector3 avatarWorldPos = avatarTransform.TransformPoint(pair.avatarAnchor);
            
            if (anchorList.index == i)
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

            Handles.color = Color.green;
            Vector3 clothWorldPos = clothTransform.TransformPoint(pair.clothAnchor);

            if (anchorList.index == i)
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
            
            Handles.color = Color.yellow;
            Handles.DrawLine(avatarWorldPos, clothWorldPos);
        }
    }
}
