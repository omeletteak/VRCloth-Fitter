using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace VRClothDeclipper.Tests
{
    /// <summary>
    /// Pins the non-destructive contract of the apply stage through the real
    /// capture → apply path: the renderer is swapped to a named copy, the
    /// source mesh asset keeps its vertices, and one Undo restores the
    /// original mesh reference.
    /// </summary>
    public class VRClothMeshApplierTests
    {
        GameObject root;
        Mesh sourceMesh;
        Mesh fittedMesh;

        [TearDown]
        public void TearDown()
        {
            if (root != null)
            {
                Object.DestroyImmediate(root);
            }
            if (sourceMesh != null)
            {
                Object.DestroyImmediate(sourceMesh);
            }
            if (fittedMesh != null)
            {
                Object.DestroyImmediate(fittedMesh);
            }
        }

        [Test]
        public void Apply_SwapsToCopy_KeepsSourceMesh_AndUndoRestores()
        {
            root = new GameObject("root");
            var bone = new GameObject("bone").transform;
            bone.SetParent(root.transform);

            var clothGo = new GameObject("cloth");
            clothGo.transform.SetParent(root.transform);
            var renderer = clothGo.AddComponent<SkinnedMeshRenderer>();

            sourceMesh = new Mesh
            {
                vertices = new[]
                {
                    new Vector3(0f, 0f, 0f),
                    new Vector3(1f, 0f, 0f),
                    new Vector3(0f, 1f, 0f),
                    new Vector3(1f, 1f, 0f),
                },
                triangles = new[] { 0, 1, 2, 2, 1, 3 },
            };
            var weights = new BoneWeight[4];
            for (int v = 0; v < weights.Length; v++)
            {
                weights[v] = new BoneWeight { boneIndex0 = 0, weight0 = 1f };
            }
            sourceMesh.boneWeights = weights;
            sourceMesh.bindposes = new[] { bone.worldToLocalMatrix * clothGo.transform.localToWorldMatrix };
            renderer.bones = new[] { bone };
            renderer.rootBone = bone;
            renderer.sharedMesh = sourceMesh;
            Vector3[] sourceVerticesBefore = sourceMesh.vertices;

            var snapshot = VRClothMeshCapture.Capture(root)[0];
            // Stand-in for the solver: nudge every vertex so the write-back
            // has a visible effect.
            for (int v = 0; v < snapshot.worldVertices.Length; v++)
            {
                snapshot.worldVertices[v] += new Vector3(0f, 0.25f, 0f);
            }

            Undo.IncrementCurrentGroup();
            VRClothMeshApplier.Apply(snapshot);
            fittedMesh = renderer.sharedMesh;

            Assert.AreNotSame(sourceMesh, fittedMesh, "renderer should point at a copy");
            StringAssert.Contains("VRClothFitted", fittedMesh.name);
            Assert.AreEqual(0.25f, fittedMesh.vertices[0].y, 1e-4f, "copy should carry the deformation");

            Vector3[] sourceVerticesAfter = sourceMesh.vertices;
            for (int v = 0; v < sourceVerticesBefore.Length; v++)
            {
                Assert.AreEqual(sourceVerticesBefore[v], sourceVerticesAfter[v], $"source vertex {v} must stay untouched");
            }

            Undo.PerformUndo();
            Assert.AreSame(sourceMesh, renderer.sharedMesh, "Undo should restore the original mesh reference");
        }
    }
}
