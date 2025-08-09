using UnityEditor;
using UnityEngine;
using VRClothFitter;
using System.IO;

[CustomEditor(typeof(VRClothFitterScalingData))]
public class VRClothFitterScalingDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();

        if (GUILayout.Button("Export Preset"))
        {
            ExportPreset();
        }

        if (GUILayout.Button("Import Preset"))
        {
            ImportPreset();
        }
    }

    private void ExportPreset()
    {
        var data = (VRClothFitterScalingData)target;
        var preset = new ScalingPreset
        {
            avatarName = data.gameObject.name, // A simple default
            clothName = data.gameObject.name,  // A simple default
            boneScales = data.boneScales
        };

        string path = EditorUtility.SaveFilePanel("Export Scaling Preset", "", $"{preset.clothName}_on_{preset.avatarName}.json", "json");

        if (string.IsNullOrEmpty(path)) return;

        string json = JsonUtility.ToJson(preset, true);
        File.WriteAllText(path, json);
        EditorUtility.DisplayDialog("Export Successful", $"Preset saved to:\n{path}", "OK");
    }

    private void ImportPreset()
    {
        string path = EditorUtility.OpenFilePanel("Import Scaling Preset", "", "json");

        if (string.IsNullOrEmpty(path)) return;

        string json = File.ReadAllText(path);
        var preset = JsonUtility.FromJson<ScalingPreset>(json);

        if (preset == null || preset.boneScales == null)
        {
            EditorUtility.DisplayDialog("Import Failed", "Invalid preset file.", "OK");
            return;
        }

        if (EditorUtility.DisplayDialog("Confirm Import", 
            $"Import preset for '{preset.clothName}' on '{preset.avatarName}'?\nThis will overwrite current scaling data.", 
            "Import", "Cancel"))
        {
            var data = (VRClothFitterScalingData)target;
            Undo.RecordObject(data, "Import Scaling Preset");
            data.boneScales = preset.boneScales;
        }
    }
}
