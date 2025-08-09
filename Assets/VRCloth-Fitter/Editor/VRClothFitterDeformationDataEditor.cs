using UnityEditor;
using UnityEngine;
using VRClothFitter;

[CustomEditor(typeof(VRClothFitterDeformationData))]
public class VRClothFitterDeformationDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Draw the default inspector fields (the anchorPairs list)
        DrawDefaultInspector();

        EditorGUILayout.Space();

        // Add a button to enter anchor editing mode
        if (GUILayout.Button("Enter Anchor Edit Mode"))
        {
            // Logic for entering edit mode will be added here in the next step.
            Debug.Log("Entering anchor edit mode (not yet implemented).");
        }
    }
}
