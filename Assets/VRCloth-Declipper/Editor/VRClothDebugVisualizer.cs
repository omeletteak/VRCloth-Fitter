using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace VRClothDeclipper
{
    /// <summary>
    /// Draws the generated body capsules and detected penetration vertices in
    /// the scene view. Data is pushed by the pipeline or the inspector preview
    /// button and lives only for the current editor session — nothing is
    /// written to disk.
    /// </summary>
    public static class VRClothDebugVisualizer
    {
        const string VisibleKey = "VRClothDeclipper.GizmosVisible";

        static readonly List<BodyCapsule> capsules = new List<BodyCapsule>();
        static readonly List<PenetrationHit> hits = new List<PenetrationHit>();
        static float maxDepth;

        static readonly Color capsuleColor = new Color(0f, 0.8f, 1f, 0.9f);
        static readonly Color shallowColor = Color.yellow;
        static readonly Color deepColor = Color.red;

        public static bool Visible
        {
            get => SessionState.GetBool(VisibleKey, true);
            set
            {
                SessionState.SetBool(VisibleKey, value);
                SceneView.RepaintAll();
            }
        }

        public static int CapsuleCount => capsules.Count;
        public static int HitCount => hits.Count;

        [InitializeOnLoadMethod]
        static void Register()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        public static void SetCapsules(IEnumerable<BodyCapsule> newCapsules)
        {
            capsules.Clear();
            if (newCapsules != null)
            {
                capsules.AddRange(newCapsules);
            }
            SceneView.RepaintAll();
        }

        public static void SetHits(IEnumerable<PenetrationHit> newHits)
        {
            hits.Clear();
            maxDepth = 0f;
            if (newHits != null)
            {
                hits.AddRange(newHits);
                foreach (var hit in hits)
                {
                    maxDepth = Mathf.Max(maxDepth, hit.depth);
                }
            }
            SceneView.RepaintAll();
        }

        public static void Clear()
        {
            capsules.Clear();
            hits.Clear();
            maxDepth = 0f;
            SceneView.RepaintAll();
        }

        static void OnSceneGUI(SceneView sceneView)
        {
            if (!Visible || Event.current.type != EventType.Repaint)
            {
                return;
            }

            Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;

            Handles.color = capsuleColor;
            foreach (var capsule in capsules)
            {
                DrawWireCapsule(capsule);
            }

            foreach (var hit in hits)
            {
                float t = maxDepth > 1e-9f ? hit.depth / maxDepth : 1f;
                Handles.color = Color.Lerp(shallowColor, deepColor, t);
                float size = HandleUtility.GetHandleSize(hit.position) * 0.04f;
                Handles.SphereHandleCap(0, hit.position, Quaternion.identity, size, EventType.Repaint);
            }
        }

        static void DrawWireCapsule(BodyCapsule capsule)
        {
            Vector3 axis = capsule.end - capsule.start;
            float length = axis.magnitude;
            float r = capsule.radius;

            if (length < 1e-6f)
            {
                // Degenerate capsule: draw a wire sphere.
                Handles.DrawWireDisc(capsule.start, Vector3.up, r);
                Handles.DrawWireDisc(capsule.start, Vector3.right, r);
                Handles.DrawWireDisc(capsule.start, Vector3.forward, r);
                return;
            }

            Vector3 dir = axis / length;
            Vector3 n1 = Vector3.Cross(dir, Vector3.up);
            if (n1.sqrMagnitude < 1e-6f)
            {
                n1 = Vector3.Cross(dir, Vector3.right);
            }
            n1.Normalize();
            Vector3 n2 = Vector3.Cross(dir, n1);

            // Side lines connecting the two cap rims.
            Handles.DrawLine(capsule.start + n1 * r, capsule.end + n1 * r);
            Handles.DrawLine(capsule.start - n1 * r, capsule.end - n1 * r);
            Handles.DrawLine(capsule.start + n2 * r, capsule.end + n2 * r);
            Handles.DrawLine(capsule.start - n2 * r, capsule.end - n2 * r);

            // Cross-section circles at both cap centers.
            Handles.DrawWireDisc(capsule.start, dir, r);
            Handles.DrawWireDisc(capsule.end, dir, r);

            // Hemisphere arcs. Rotating n1 about n2 by +90 degrees reaches
            // -dir, and rotating n2 about n1 by +90 degrees reaches +dir,
            // which fixes the sweep signs below.
            Handles.DrawWireArc(capsule.start, n2, n1, 180f, r);
            Handles.DrawWireArc(capsule.end, n2, n1, -180f, r);
            Handles.DrawWireArc(capsule.start, n1, n2, -180f, r);
            Handles.DrawWireArc(capsule.end, n1, n2, 180f, r);
        }
    }
}
