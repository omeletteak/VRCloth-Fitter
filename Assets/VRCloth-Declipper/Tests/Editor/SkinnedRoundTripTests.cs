using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace VRClothDeclipper.Tests
{
    /// <summary>
    /// Synthetic end-to-end check of the math chain on a real
    /// SkinnedMeshRenderer: bake → detect → solve → inverse-skin write-back →
    /// re-bake → no penetration left. The renderer and its bone carry
    /// rotation and scale on purpose, validating that the capture convention
    /// (BakeMesh useScale:false + rotation/translation-only matrix) and the
    /// skinning matrices used for write-back describe the same space.
    /// </summary>
    public class SkinnedRoundTripTests
    {
        const float Margin = 0.005f;

        GameObject root;
        readonly List<Mesh> meshes = new List<Mesh>();

        [TearDown]
        public void TearDown()
        {
            if (root != null)
            {
                Object.DestroyImmediate(root);
            }
            foreach (var mesh in meshes)
            {
                if (mesh != null)
                {
                    Object.DestroyImmediate(mesh);
                }
            }
            meshes.Clear();
        }

        Mesh TrackMesh(Mesh mesh)
        {
            meshes.Add(mesh);
            return mesh;
        }

        [Test]
        public void SolveAndWriteBack_OnSkinnedMesh_RemovesPenetration()
        {
            // --- Scene setup: a bone and a renderer, both rotated and scaled.
            root = new GameObject("root");
            var bone = new GameObject("bone").transform;
            bone.SetParent(root.transform);
            bone.localPosition = new Vector3(0.1f, 0f, 0f);
            bone.localRotation = Quaternion.Euler(0f, 25f, 0f);
            bone.localScale = Vector3.one * 1.5f;

            var clothGo = new GameObject("cloth");
            clothGo.transform.SetParent(root.transform);
            clothGo.transform.localPosition = new Vector3(0f, 0.2f, 0f);
            clothGo.transform.localRotation = Quaternion.Euler(0f, 0f, 10f);
            clothGo.transform.localScale = Vector3.one * 2f;
            var renderer = clothGo.AddComponent<SkinnedMeshRenderer>();

            Mesh mesh = TrackMesh(MakeSheetMesh(15, 0.05f));
            var weights = new BoneWeight[mesh.vertexCount];
            for (int v = 0; v < weights.Length; v++)
            {
                weights[v] = new BoneWeight { boneIndex0 = 0, weight0 = 1f };
            }
            mesh.boneWeights = weights;
            mesh.bindposes = new[] { bone.worldToLocalMatrix * clothGo.transform.localToWorldMatrix };
            renderer.bones = new[] { bone };
            renderer.rootBone = bone;
            renderer.sharedMesh = mesh;
            Vector3 originalFirstVertex = mesh.vertices[0];

            // --- Capture (mirrors VRClothMeshCapture's convention).
            Vector3[] world = BakeToWorld(renderer);

            // --- A capsule crossing the sheet's center.
            Vector3 center = Vector3.zero;
            foreach (var p in world)
            {
                center += p;
            }
            center /= world.Length;
            var capsules = new List<BodyCapsule>
            {
                new BodyCapsule(center + new Vector3(0f, -1f, 0f), center + new Vector3(0f, 1f, 0f), 0.2f),
            };
            Assert.Greater(PenetrationDetection.Scan(world, capsules, Margin).Count, 0,
                "test setup should start with penetration");

            // --- Solve and write back through the inverse skinning path.
            var result = PenetrationSolver.Solve(world, mesh.triangles, capsules, Margin);
            Assert.AreEqual(0, result.finalHitCount);

            Mesh copy = TrackMesh(Object.Instantiate(mesh));
            copy.vertices = SkinningMath.WorldToMeshLocal(
                world, mesh.boneWeights, new[] { bone.localToWorldMatrix }, mesh.bindposes,
                renderer.transform.worldToLocalMatrix);
            copy.RecalculateBounds();
            renderer.sharedMesh = copy;

            // --- Re-bake: the rendered result must be penetration-free.
            Vector3[] reWorld = BakeToWorld(renderer);
            var remaining = PenetrationDetection.Scan(reWorld, capsules, Margin - 2e-4f);
            Assert.AreEqual(0, remaining.Count, "re-baked mesh should not penetrate");

            // --- Non-destructive: the original mesh asset is untouched.
            Assert.AreEqual(originalFirstVertex, mesh.vertices[0]);
        }

        Vector3[] BakeToWorld(SkinnedMeshRenderer renderer)
        {
            Mesh baked = TrackMesh(new Mesh());
            renderer.BakeMesh(baked, false);
            var vertices = baked.vertices;
            Matrix4x4 toWorld = Matrix4x4.TRS(
                renderer.transform.position, renderer.transform.rotation, Vector3.one);
            for (int v = 0; v < vertices.Length; v++)
            {
                vertices[v] = toWorld.MultiplyPoint3x4(vertices[v]);
            }
            return vertices;
        }

        static Mesh MakeSheetMesh(int n, float spacing)
        {
            var positions = new Vector3[n * n];
            float half = (n - 1) * spacing * 0.5f;
            for (int row = 0; row < n; row++)
            {
                for (int col = 0; col < n; col++)
                {
                    positions[row * n + col] = new Vector3(col * spacing - half, 0f, row * spacing - half);
                }
            }

            var tris = new List<int>();
            for (int row = 0; row < n - 1; row++)
            {
                for (int col = 0; col < n - 1; col++)
                {
                    int v = row * n + col;
                    tris.AddRange(new[] { v, v + 1, v + n });
                    tris.AddRange(new[] { v + n, v + 1, v + n + 1 });
                }
            }

            var mesh = new Mesh { vertices = positions, triangles = tris.ToArray() };
            mesh.RecalculateNormals();
            return mesh;
        }
    }
}
