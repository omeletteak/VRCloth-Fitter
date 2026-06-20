using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace VRClothDeclipper.Tests
{
    /// <summary>
    /// Multi-pose extension of <see cref="SkinnedRoundTripTests"/>: capture
    /// several posed states of a real SkinnedMeshRenderer, compose one
    /// bind-pose-local delta with <see cref="MultiPoseComposer"/>, apply it with
    /// <see cref="VRClothMeshApplier.ApplyBindLocalDelta"/>, then re-bake every
    /// pose and assert none penetrates. This pins the part of the Stage-1 glue
    /// (docs/MULTIPOSE_GLUE_SPIKE.md) that is verifiable in code — the capture →
    /// compose → apply → re-bake chain — on a real renderer carrying rotation and
    /// scale. Driving humanoid muscles and judging the silhouette are the human
    /// gate the test cannot cover.
    /// </summary>
    public class MultiPoseRoundTripTests
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

        Mesh Track(Mesh mesh)
        {
            meshes.Add(mesh);
            return mesh;
        }

        [Test]
        public void ComposeAndApply_OnSkinnedMesh_ClearsEveryPose()
        {
            // --- Scene: a bone and a renderer, both rotated and scaled, so the
            // skin matrices used for capture and write-back are non-trivial.
            root = new GameObject("root");
            var bone = new GameObject("bone").transform;
            bone.SetParent(root.transform);
            bone.localPosition = new Vector3(0.1f, 0f, 0f);
            bone.localScale = Vector3.one * 1.5f;

            var clothGo = new GameObject("cloth");
            clothGo.transform.SetParent(root.transform);
            clothGo.transform.localPosition = new Vector3(0f, 0.2f, 0f);
            clothGo.transform.localRotation = Quaternion.Euler(0f, 0f, 10f);
            clothGo.transform.localScale = Vector3.one * 2f;
            var renderer = clothGo.AddComponent<SkinnedMeshRenderer>();

            Mesh mesh = Track(MakeSheetMesh(15, 0.05f));
            var weights = new BoneWeight[mesh.vertexCount];
            for (int v = 0; v < weights.Length; v++)
            {
                weights[v] = new BoneWeight { boneIndex0 = 0, weight0 = 1f };
            }
            mesh.boneWeights = weights;

            // Bindpose is taken at the bind orientation; set the bone there first.
            var bindRotation = Quaternion.Euler(0f, 25f, 0f);
            bone.localRotation = bindRotation;
            mesh.bindposes = new[] { bone.worldToLocalMatrix * clothGo.transform.localToWorldMatrix };
            renderer.bones = new[] { bone };
            renderer.rootBone = bone;
            renderer.sharedMesh = mesh;
            Vector3 originalFirstVertex = mesh.vertices[0];

            // --- Three poses = three bone rotations. Each gets a capsule through
            // its own posed sheet center, so each pose genuinely penetrates and a
            // single static delta must clear all three (the over-constrained case
            // MultiPoseComposer solves by sweeping).
            var rotations = new[]
            {
                bindRotation,
                Quaternion.Euler(0f, 25f, 18f),
                Quaternion.Euler(-12f, 25f, 0f),
            };
            var poses = new List<PoseCapture>();
            var poseCapsules = new List<List<BodyCapsule>>();
            foreach (var rot in rotations)
            {
                bone.localRotation = rot;
                Vector3[] world = BakeToWorld(renderer);
                Vector3 center = Centroid(world);
                var capsules = new List<BodyCapsule>
                {
                    new BodyCapsule(center + new Vector3(0f, -1f, 0f), center + new Vector3(0f, 1f, 0f), 0.2f),
                };
                Assert.Greater(PenetrationDetection.Scan(world, capsules, Margin).Count, 0,
                    "each pose should start with penetration");
                poseCapsules.Add(capsules);
                poses.Add(MakeCapture(renderer, world, new CapsuleBodyCollider(capsules)));
            }

            // --- Compose one bind-local delta and apply it non-destructively.
            var delta = new Vector3[mesh.vertexCount];
            var result = MultiPoseComposer.Compose(delta, mesh.triangles, poses, Margin);
            Assert.AreEqual(0, result.remainingPenetrating, "composer should clear every pose");

            VRClothMeshApplier.ApplyBindLocalDelta(renderer, delta);
            Track(renderer.sharedMesh); // the swap created a fresh mesh copy

            // --- Re-bake every pose on the fitted mesh: none may penetrate (each
            // pose against its own stored capsule, which does not move with the fit).
            for (int p = 0; p < rotations.Length; p++)
            {
                bone.localRotation = rotations[p];
                Vector3[] reWorld = BakeToWorld(renderer);
                Assert.AreEqual(0, PenetrationDetection.Scan(reWorld, poseCapsules[p], Margin - 2e-4f).Count,
                    $"fitted mesh should not penetrate at pose {p}");
            }

            // --- Non-destructive: the original mesh asset is untouched.
            Assert.AreEqual(originalFirstVertex, mesh.vertices[0]);
        }

        static PoseCapture MakeCapture(SkinnedMeshRenderer renderer, Vector3[] world, IBodyCollider collider)
        {
            var mesh = renderer.sharedMesh;
            var weights = mesh.boneWeights;
            var bindPoses = mesh.bindposes;
            var bones = renderer.bones;
            var boneToWorld = new Matrix4x4[bones.Length];
            for (int b = 0; b < bones.Length; b++)
            {
                boneToWorld[b] = bones[b] != null ? bones[b].localToWorldMatrix : Matrix4x4.identity;
            }
            var skin = new Matrix4x4[world.Length];
            for (int v = 0; v < world.Length; v++)
            {
                skin[v] = SkinningMath.BlendedSkinMatrix(weights[v], boneToWorld, bindPoses);
            }
            return new PoseCapture { originalWorld = world, skinMatrices = skin, collider = collider };
        }

        Vector3[] BakeToWorld(SkinnedMeshRenderer renderer)
        {
            Mesh baked = Track(new Mesh());
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

        static Vector3 Centroid(Vector3[] points)
        {
            Vector3 sum = Vector3.zero;
            foreach (var p in points)
            {
                sum += p;
            }
            return points.Length > 0 ? sum / points.Length : Vector3.zero;
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
