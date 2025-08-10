using UnityEditor;
using UnityEngine;

namespace VRClothFitter
{
    [CustomEditor(typeof(VRClothFitterCageDeformer))]
    public class VRClothFitterCageDeformerEditor : Editor
    {
        private VRClothFitterCageDeformer deformer;
        private Mesh workingMesh;
        private Mesh originalMesh;

        private void OnEnable()
        {
            deformer = (VRClothFitterCageDeformer)target;
        }
        
        private void OnDisable()
        {
            // Ensure we clean up if the inspector is closed or deselected
            StopEditing();
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            if (deformer.targetRenderer == null)
            {
                EditorGUILayout.HelpBox("Please assign a Target Renderer.", MessageType.Warning);
                return;
            }

            if (deformer.isEditing)
            {
                if (GUILayout.Button("Stop Editing"))
                {
                    StopEditing();
                }
            }
            else
            {
                if (GUILayout.Button("Generate / Reset Cage"))
                {
                    GenerateCage();
                }
                if (deformer.cageControlPoints != null && deformer.cageControlPoints.Length > 0)
                {
                    if (GUILayout.Button("Start Editing"))
                    {
                        StartEditing();
                    }
                }
            }
        }

        private void StartEditing()
        {
            deformer.isEditing = true;
            
            // Clone the mesh to work on it non-destructively
            originalMesh = deformer.targetRenderer.sharedMesh;
            workingMesh = Instantiate(originalMesh);
            workingMesh.name = $"{originalMesh.name} (Deformed)";
            deformer.targetRenderer.sharedMesh = workingMesh;
        }

        private void StopEditing()
        {
            if (!deformer.isEditing) return;
            
            deformer.isEditing = false;
            
            // Restore the original mesh and clean up the clone
            deformer.targetRenderer.sharedMesh = originalMesh;
            if (workingMesh != null)
            {
                DestroyImmediate(workingMesh);
            }
            
            originalMesh = null;
            workingMesh = null;
        }

        private void GenerateCage()
        {
            if (deformer.targetRenderer == null) return;

            var bounds = deformer.targetRenderer.sharedMesh.bounds;
            var divisions = deformer.cageDivisions;
            divisions.x = Mathf.Max(2, divisions.x);
            divisions.y = Mathf.Max(2, divisions.y);
            divisions.z = Mathf.Max(2, divisions.z);

            int pointCount = divisions.x * divisions.y * divisions.z;
            deformer.cageControlPoints = new Vector3[pointCount];

            for (int z = 0; z < divisions.z; z++)
            {
                for (int y = 0; y < divisions.y; y++)
                {
                    for (int x = 0; x < divisions.x; x++)
                    {
                        int index = z * (divisions.x * divisions.y) + y * divisions.x + x;
                        float u = x / (float)(divisions.x - 1);
                        float v = y / (float)(divisions.y - 1);
                        float w = z / (float)(divisions.z - 1);
                        
                        deformer.cageControlPoints[index] = new Vector3(
                            Mathf.Lerp(bounds.min.x, bounds.max.x, u),
                            Mathf.Lerp(bounds.min.y, bounds.max.y, v),
                            Mathf.Lerp(bounds.min.z, bounds.max.z, w)
                        );
                    }
                }
            }
            
            Undo.RecordObject(deformer, "Generate Cage");
            EditorUtility.SetDirty(deformer);
        }

        private void OnSceneGUI()
        {
            if (deformer.cageControlPoints == null || deformer.cageControlPoints.Length == 0)
            {
                return;
            }

            var transform = deformer.transform;
            
            // Draw handles for each control point
            for (int i = 0; i < deformer.cageControlPoints.Length; i++)
            {
                var worldPos = transform.TransformPoint(deformer.cageControlPoints[i]);
                
                using (var check = new EditorGUI.ChangeCheckScope())
                {
                    Handles.color = deformer.isEditing ? Color.yellow : new Color(1f, 1f, 0.5f, 0.5f);
                    var newWorldPos = Handles.PositionHandle(worldPos, Quaternion.identity);
                    
                    if (check.changed)
                    {
                        if (!deformer.isEditing)
                        {
                            Debug.LogWarning("Cannot move cage points. Click 'Start Editing' first.");
                        }
                        else
                        {
                            Undo.RecordObject(deformer, "Move Cage Control Point");
                            deformer.cageControlPoints[i] = transform.InverseTransformPoint(newWorldPos);
                            EditorUtility.SetDirty(deformer);
                            // DeformMesh(); // This will be implemented next
                        }
                    }
                }
            }
            
            // Draw lines connecting the control points
            var divisions = deformer.cageDivisions;
            Handles.color = deformer.isEditing ? new Color(1f, 1f, 0f, 0.5f) : new Color(1f, 1f, 0.5f, 0.2f);
            for (int z = 0; z < divisions.z; z++)
            {
                for (int y = 0; y < divisions.y; y++)
                {
                    for (int x = 0; x < divisions.x; x++)
                    {
                        int p0_idx = z * (divisions.x * divisions.y) + y * divisions.x + x;
                        var p0 = transform.TransformPoint(deformer.cageControlPoints[p0_idx]);

                        else
                        {
                            Undo.RecordObject(deformer, "Move Cage Control Point");
                            deformer.cageControlPoints[i] = transform.InverseTransformPoint(newWorldPos);
                            EditorUtility.SetDirty(deformer);
                            DeformMesh();
                        }
                    }
                }
            }
            
            // Draw lines connecting the control points
            var divisions = deformer.cageDivisions;
            Handles.color = deformer.isEditing ? new Color(1f, 1f, 0f, 0.5f) : new Color(1f, 1f, 0.5f, 0.2f);
            for (int z = 0; z < divisions.z; z++)
            {
                for (int y = 0; y < divisions.y; y++)
                {
                    for (int x = 0; x < divisions.x; x++)
                    {
                        int p0_idx = z * (divisions.x * divisions.y) + y * divisions.x + x;
                        var p0 = transform.TransformPoint(deformer.cageControlPoints[p0_idx]);

                        if (x < divisions.x - 1)
                        {
                            int p1_idx = z * (divisions.x * divisions.y) + y * divisions.x + (x + 1);
                            var p1 = transform.TransformPoint(deformer.cageControlPoints[p1_idx]);
                            Handles.DrawLine(p0, p1);
                        }
                        if (y < divisions.y - 1)
                        {
                            int p1_idx = z * (divisions.x * divisions.y) + (y + 1) * divisions.x + x;
                            var p1 = transform.TransformPoint(deformer.cageControlPoints[p1_idx]);
                            Handles.DrawLine(p0, p1);
                        }
                        if (z < divisions.z - 1)
                        {
                            int p1_idx = (z + 1) * (divisions.x * divisions.y) + y * divisions.x + x;
                            var p1 = transform.TransformPoint(deformer.cageControlPoints[p1_idx]);
                            Handles.DrawLine(p0, p1);
                        }
                    }
                }
            }
        }

        private void DeformMesh()
        {
            if (workingMesh == null) return;

            var originalVertices = originalMesh.vertices;
            var newVertices = new Vector3[originalVertices.Length];
            var bounds = originalMesh.bounds;

            for (int i = 0; i < originalVertices.Length; i++)
            {
                var p = originalVertices[i];
                
                // Normalize vertex position into local 0-1 coordinates (UVWs)
                float u = Mathf.InverseLerp(bounds.min.x, bounds.max.x, p.x);
                float v = Mathf.InverseLerp(bounds.min.y, bounds.max.y, p.y);
                float w = Mathf.InverseLerp(bounds.min.z, bounds.max.z, p.z);

                newVertices[i] = Interpolate(u, v, w);
            }

            workingMesh.vertices = newVertices;
            workingMesh.RecalculateNormals();
            workingMesh.RecalculateBounds();
        }

        private Vector3 Interpolate(float u, float v, float w)
        {
            var divs = deformer.cageDivisions;
            
            // Find the 8 surrounding control points
            float tx = u * (divs.x - 1);
            float ty = v * (divs.y - 1);
            float tz = w * (divs.z - 1);

            int ix = (int)tx;
            int iy = (int)ty;
            int iz = (int)tz;

            float fx = tx - ix;
            float fy = ty - iy;
            float fz = tz - iz;

            Vector3 c000 = GetControlPoint(ix, iy, iz);
            Vector3 c100 = GetControlPoint(ix + 1, iy, iz);
            Vector3 c010 = GetControlPoint(ix, iy + 1, iz);
            Vector3 c110 = GetControlPoint(ix + 1, iy + 1, iz);
            Vector3 c001 = GetControlPoint(ix, iy, iz + 1);
            Vector3 c101 = GetControlPoint(ix + 1, iy, iz + 1);
            Vector3 c011 = GetControlPoint(ix, iy + 1, iz + 1);
            Vector3 c111 = GetControlPoint(ix + 1, iy + 1, iz + 1);

            // Trilinear interpolation
            var x0 = Vector3.Lerp(c000, c100, fx);
            var x1 = Vector3.Lerp(c010, c110, fx);
            var x2 = Vector3.Lerp(c001, c101, fx);
            var x3 = Vector3.Lerp(c011, c111, fx);
            var y0 = Vector3.Lerp(x0, x1, fy);
            var y1 = Vector3.Lerp(x2, x3, fy);
            return Vector3.Lerp(y0, y1, fz);
        }

        private Vector3 GetControlPoint(int x, int y, int z)
        {
            var divs = deformer.cageDivisions;
            if (x < 0 || x >= divs.x || y < 0 || y >= divs.y || z < 0 || z >= divs.z)
            {
                return Vector3.zero; // Should not happen with clamped UVWs
            }
            int index = z * (divs.x * divs.y) + y * divs.x + x;
            return deformer.cageControlPoints[index];
        }
    }
}
