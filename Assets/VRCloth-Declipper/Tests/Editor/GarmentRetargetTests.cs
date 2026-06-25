using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace VRClothDeclipper.Tests
{
    /// <summary>
    /// Garment 採寸 re-binds the garment's bones onto the body skeleton (the core
    /// of MA Merge Armature, done just for the measurement) so the garment meshes
    /// skin on the BODY bones and line up with the body capsules — co-locating the
    /// roots alone is not enough. Validates on real SkinnedMeshRenderers that
    /// <see cref="VRClothMeasurementDump.RetargetGarmentToBody"/> (1) re-points
    /// same-named bones to the body, (2) leaves garment-only bones alone, and
    /// (3) actually moves the baked geometry into the body's bone space.
    /// </summary>
    public class GarmentRetargetTests
    {
        readonly List<Object> trash = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in trash)
            {
                if (o != null) Object.DestroyImmediate(o);
            }
            trash.Clear();
        }

        T Track<T>(T o) where T : Object { trash.Add(o); return o; }

        [Test]
        public void RetargetGarmentToBody_RebindsSameNamedBones_AndMovesBakeToBodySpace()
        {
            // --- Body avatar: a "Hips" bone offset to x = +1.
            var body = Track(new GameObject("body"));
            var bodyHips = new GameObject("Hips").transform;
            bodyHips.SetParent(body.transform);
            bodyHips.position = new Vector3(1f, 0f, 0f);

            // --- Garment: its OWN "Hips" bone at the origin, a sheet skinned to it.
            var garment = Track(new GameObject("garment"));
            var garHips = new GameObject("Hips").transform;
            garHips.SetParent(garment.transform);
            garHips.position = Vector3.zero;

            var smrGo = new GameObject("garment_mesh");
            smrGo.transform.SetParent(garment.transform);
            var smr = smrGo.AddComponent<SkinnedMeshRenderer>();
            Mesh mesh = Track(MakeSheet(5, 0.05f));
            var w = new BoneWeight[mesh.vertexCount];
            for (int i = 0; i < w.Length; i++)
            {
                w[i] = new BoneWeight { boneIndex0 = 0, weight0 = 1f };
            }
            mesh.boneWeights = w;
            mesh.bindposes = new[] { garHips.worldToLocalMatrix * smrGo.transform.localToWorldMatrix };
            smr.bones = new[] { garHips };
            smr.rootBone = garHips;
            smr.sharedMesh = mesh;

            var meshes = new List<SkinnedMeshRenderer> { smr };
            Vector3 before = Centroid(VRClothMeshCapture.BakeWorldVertices(smr));

            // --- Re-bind onto the body skeleton.
            int remapped = VRClothMeasurementDump.RetargetGarmentToBody(meshes, body, out int total);

            Assert.AreEqual(1, total, "one garment bone seen");
            Assert.AreEqual(1, remapped, "the same-named Hips should re-bind to the body");
            Assert.AreSame(bodyHips, smr.bones[0], "garment bone now points at the BODY Hips");

            // --- The baked geometry moved into the body's bone space (+1 on x).
            Vector3 after = Centroid(VRClothMeshCapture.BakeWorldVertices(smr));
            Assert.AreEqual(1f, after.x - before.x, 1e-3f, "bake should shift by the body-bone offset");
            Assert.AreEqual(0f, after.y - before.y, 1e-3f);
            Assert.AreEqual(0f, after.z - before.z, 1e-3f);
        }

        [Test]
        public void RetargetGarmentToBody_LeavesGarmentOnlyBones_Untouched()
        {
            var body = Track(new GameObject("body"));
            var bodyHips = new GameObject("Hips").transform;
            bodyHips.SetParent(body.transform);

            var garment = Track(new GameObject("garment"));
            var garHips = new GameObject("Hips").transform;
            garHips.SetParent(garment.transform);
            var deco = new GameObject("Ribbon_01").transform; // no body counterpart
            deco.SetParent(garment.transform);
            deco.position = new Vector3(5f, 0f, 0f); // far from any body bone → not snapped

            var smrGo = new GameObject("m");
            smrGo.transform.SetParent(garment.transform);
            var smr = smrGo.AddComponent<SkinnedMeshRenderer>();
            Mesh mesh = Track(MakeSheet(3, 0.05f));
            var w = new BoneWeight[mesh.vertexCount];
            for (int i = 0; i < w.Length; i++)
            {
                w[i] = new BoneWeight { boneIndex0 = 0, weight0 = 1f };
            }
            mesh.boneWeights = w;
            mesh.bindposes = new[] { Matrix4x4.identity, Matrix4x4.identity };
            smr.bones = new[] { garHips, deco };
            smr.sharedMesh = mesh;

            int remapped = VRClothMeasurementDump.RetargetGarmentToBody(
                new List<SkinnedMeshRenderer> { smr }, body, out int total);

            Assert.AreEqual(2, total, "two garment bones seen");
            Assert.AreEqual(1, remapped, "only Hips has a body counterpart");
            Assert.AreSame(bodyHips, smr.bones[0], "Hips re-bound to body");
            Assert.AreSame(deco, smr.bones[1], "garment-only bone left as-is");
        }

        [Test]
        public void RetargetGarmentToBody_DifferentNaming_SnapsToCoLocatedBodyBone()
        {
            // Body bone with the body's OWN (FBX-style) name at a known position.
            var body = Track(new GameObject("body"));
            var bodyFoot = new GameObject("J_foot_L").transform;
            bodyFoot.SetParent(body.transform);
            bodyFoot.position = new Vector3(0.1f, 0f, 0f);

            // Garment bone with a DIFFERENT name ("Foot_L") but co-located on the body
            // bone — the real-world case (garment uses _L/_R, body uses another scheme).
            var garment = Track(new GameObject("garment"));
            var garFoot = new GameObject("Foot_L").transform;
            garFoot.SetParent(garment.transform);
            garFoot.position = new Vector3(0.1f, 0f, 0f);

            var smrGo = new GameObject("m");
            smrGo.transform.SetParent(garment.transform);
            var smr = smrGo.AddComponent<SkinnedMeshRenderer>();
            Mesh mesh = Track(MakeSheet(3, 0.05f));
            var w = new BoneWeight[mesh.vertexCount];
            for (int i = 0; i < w.Length; i++)
            {
                w[i] = new BoneWeight { boneIndex0 = 0, weight0 = 1f };
            }
            mesh.boneWeights = w;
            mesh.bindposes = new[] { Matrix4x4.identity };
            smr.bones = new[] { garFoot };
            smr.sharedMesh = mesh;

            int remapped = VRClothMeasurementDump.RetargetGarmentToBody(
                new List<SkinnedMeshRenderer> { smr }, body, out int total);

            Assert.AreEqual(1, total);
            Assert.AreEqual(1, remapped, "a differently-named bone snaps to the co-located body bone");
            Assert.AreSame(bodyFoot, smr.bones[0], "re-bound by POSITION, not name");
        }

        static Vector3 Centroid(Vector3[] pts)
        {
            Vector3 s = Vector3.zero;
            foreach (var p in pts) s += p;
            return pts.Length > 0 ? s / pts.Length : s;
        }

        static Mesh MakeSheet(int n, float spacing)
        {
            var pos = new Vector3[n * n];
            float half = (n - 1) * spacing * 0.5f;
            for (int r = 0; r < n; r++)
            {
                for (int c = 0; c < n; c++)
                {
                    pos[r * n + c] = new Vector3(c * spacing - half, 0f, r * spacing - half);
                }
            }
            var tris = new List<int>();
            for (int r = 0; r < n - 1; r++)
            {
                for (int c = 0; c < n - 1; c++)
                {
                    int v = r * n + c;
                    tris.AddRange(new[] { v, v + 1, v + n });
                    tris.AddRange(new[] { v + n, v + 1, v + n + 1 });
                }
            }
            var mesh = new Mesh { vertices = pos, triangles = tris.ToArray() };
            mesh.RecalculateNormals();
            return mesh;
        }
    }
}
