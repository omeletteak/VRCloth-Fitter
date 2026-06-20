using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace VRClothDeclipper
{
    /// <summary>
    /// Stage 1 of the multi-pose glue spike (docs/MULTIPOSE_GLUE_SPIKE.md):
    /// field-verify <see cref="MultiPoseComposer"/> on a real avatar in the
    /// editor. It drives the target avatar through a small set of representative
    /// poses with <see cref="HumanPoseHandler"/>, captures each pose's cloth world
    /// positions, per-vertex skin matrices and body collider, composes one
    /// bind-pose-local delta that clears every pose, applies it non-destructively
    /// (<see cref="VRClothMeshApplier.ApplyBindLocalDelta"/>), then re-drives each
    /// pose and logs the residual penetration so the numbers can be judged.
    ///
    /// Experimental — this is the cheap editor half of the spike. The pose set is
    /// a hand-tuned starting point (muscle space is empirical, calibrate by eye);
    /// correctness of the capture → compose → apply → re-bake chain is pinned
    /// separately by MultiPoseRoundTripTests, the part verifiable in code. The
    /// silhouette/over-inflation check is the human gate
    /// (docs/E2E_TEST_GUIDE.md §7.1).
    ///
    /// Pre-merge caveat: in the editor the cloth is not yet merged onto the avatar
    /// armature, so pose driving only moves the cloth if its bones are the
    /// avatar's humanoid bones (or follow them). Driving the build-time merged
    /// avatar is the separate Stage 2 spike (docs/MULTIPOSE_COMPOSITION.md §7).
    /// </summary>
    public static class VRClothMultiPoseSpike
    {
        /// <summary>A representative pose as muscle overrides on the avatar's captured pose.</summary>
        public struct RepresentativePose
        {
            public string name;
            public (string muscle, float value)[] muscles;
        }

        /// <summary>
        /// Starting set (叩き台): rest + elbows bent + sitting + hip flexion. The
        /// names are Unity's <see cref="HumanTrait.MuscleName"/> entries; the
        /// values are normalized to roughly [-1, 1] and are empirical — calibrate
        /// against the avatar by eye. The first pose carries no overrides so the
        /// default standing shape is always one of the constraints
        /// (docs/MULTIPOSE_GLUE_SPIKE.md step 2).
        /// </summary>
        public static RepresentativePose[] DefaultPoses()
        {
            return new[]
            {
                new RepresentativePose { name = "Rest (A-pose)", muscles = new (string, float)[0] },
                new RepresentativePose
                {
                    name = "Elbows bent ~75°",
                    muscles = new (string, float)[]
                    {
                        ("Left Forearm Stretch", -0.6f),
                        ("Right Forearm Stretch", -0.6f),
                    },
                },
                new RepresentativePose
                {
                    name = "Sitting (hips + knees ~90°)",
                    muscles = new (string, float)[]
                    {
                        ("Left Upper Leg Front-Back", 0.7f),
                        ("Right Upper Leg Front-Back", 0.7f),
                        ("Left Lower Leg Stretch", -0.8f),
                        ("Right Lower Leg Stretch", -0.8f),
                    },
                },
                new RepresentativePose
                {
                    name = "Hip flexion (L)",
                    muscles = new (string, float)[] { ("Left Upper Leg Front-Back", 0.8f) },
                },
            };
        }

        public static void Run(VRClothDeclipper fitter)
        {
            if (fitter == null || fitter.targetAvatar == null || fitter.clothToDeform == null)
            {
                Debug.LogError("[VRClothMultiPose] Target Avatar or Cloth is not set.");
                return;
            }
            var animator = fitter.targetAvatar.GetComponent<Animator>();
            if (animator == null || !animator.isHuman || animator.avatar == null)
            {
                Debug.LogError("[VRClothMultiPose] Target Avatar needs a Humanoid Animator to drive representative poses.");
                return;
            }

            GameObject clothRoot = fitter.clothRoot != null ? fitter.clothRoot : fitter.clothToDeform.gameObject;
            var renderers = CollectRenderers(clothRoot);
            if (renderers.Count == 0)
            {
                Debug.LogError("[VRClothMultiPose] No active SkinnedMeshRenderer under the cloth root. Aborting.");
                return;
            }

            var poses = DefaultPoses();
            Debug.Log($"[VRClothMultiPose] Stage-1 spike: {renderers.Count} renderer(s) over {poses.Length} representative pose(s), "
                + $"margin {fitter.margin:F3} m.");

            // One PoseCapture per (renderer, pose).
            var captures = new List<PoseCapture>[renderers.Count];
            for (int i = 0; i < renderers.Count; i++)
            {
                captures[i] = new List<PoseCapture>();
            }

            var handler = new HumanPoseHandler(animator.avatar, animator.transform);
            try
            {
                var basePose = new HumanPose();
                handler.GetHumanPose(ref basePose);
                float[] baseMuscles = (float[])basePose.muscles.Clone();

                // --- Capture: drive each pose, bake every renderer, build colliders.
                try
                {
                    foreach (var rp in poses)
                    {
                        DrivePose(handler, ref basePose, baseMuscles, rp);
                        IBodyCollider collider = BuildCollider(fitter);
                        for (int i = 0; i < renderers.Count; i++)
                        {
                            captures[i].Add(CaptureRenderer(renderers[i], collider));
                        }
                    }
                }
                finally
                {
                    RestorePose(handler, ref basePose, baseMuscles);
                }

                // --- Compose + apply per renderer.
                for (int i = 0; i < renderers.Count; i++)
                {
                    var renderer = renderers[i];
                    int n = renderer.sharedMesh.vertexCount;
                    var delta = new Vector3[n];
                    var result = MultiPoseComposer.Compose(delta, renderer.sharedMesh.triangles, captures[i], fitter.margin);

                    float maxDelta = 0f;
                    for (int v = 0; v < delta.Length; v++)
                    {
                        maxDelta = Mathf.Max(maxDelta, delta[v].magnitude);
                    }
                    Debug.Log($"[VRClothMultiPose] {renderer.name}: composed in {result.rounds} sweep(s); "
                        + $"remainingPenetrating={result.remainingPenetrating} (expect 0), maxDelta={maxDelta * 1000f:F1} mm.");

                    VRClothMeshApplier.ApplyBindLocalDelta(renderer, delta);
                }

                // --- Residual: re-drive each pose, re-bake the fitted mesh, count penetration.
                try
                {
                    foreach (var rp in poses)
                    {
                        DrivePose(handler, ref basePose, baseMuscles, rp);
                        IBodyCollider collider = BuildCollider(fitter);
                        for (int i = 0; i < renderers.Count; i++)
                        {
                            Vector3[] world = BakeToWorld(renderers[i]);
                            int penetrating = CountPenetrating(world, collider, fitter.margin);
                            Debug.Log($"[VRClothMultiPose] residual — pose '{rp.name}', {renderers[i].name}: "
                                + $"{penetrating}/{world.Length} vert(s) below margin.");
                        }
                    }
                }
                finally
                {
                    RestorePose(handler, ref basePose, baseMuscles);
                }
            }
            finally
            {
                handler.Dispose();
            }

            Debug.Log("[VRClothMultiPose] Stage-1 spike complete. Compare residuals to a single-pose Run and "
                + "check the silhouette by eye (docs/E2E_TEST_GUIDE.md §7.1). Undo (Ctrl+Z) restores the meshes.");
        }

        static List<SkinnedMeshRenderer> CollectRenderers(GameObject clothRoot)
        {
            var renderers = new List<SkinnedMeshRenderer>();
            foreach (var r in clothRoot.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (r.sharedMesh != null && r.gameObject.activeInHierarchy && r.enabled)
                {
                    renderers.Add(r);
                }
            }
            return renderers;
        }

        // Reset to the captured muscles, apply this pose's overrides, then drive.
        // Body position/rotation in basePose are left at the captured values, so
        // only the limbs move and the avatar stays put.
        static void DrivePose(HumanPoseHandler handler, ref HumanPose basePose, float[] baseMuscles, RepresentativePose rp)
        {
            System.Array.Copy(baseMuscles, basePose.muscles, baseMuscles.Length);
            foreach (var (muscle, value) in rp.muscles)
            {
                int idx = MuscleIndex(muscle);
                if (idx >= 0 && idx < basePose.muscles.Length)
                {
                    basePose.muscles[idx] = value;
                }
                else
                {
                    Debug.LogWarning($"[VRClothMultiPose] Unknown muscle '{muscle}' — skipped (pose '{rp.name}').");
                }
            }
            handler.SetHumanPose(ref basePose);
        }

        static void RestorePose(HumanPoseHandler handler, ref HumanPose basePose, float[] baseMuscles)
        {
            System.Array.Copy(baseMuscles, basePose.muscles, baseMuscles.Length);
            handler.SetHumanPose(ref basePose);
        }

        static int MuscleIndex(string name)
        {
            string[] names = HumanTrait.MuscleName;
            for (int i = 0; i < names.Length; i++)
            {
                if (names[i] == name)
                {
                    return i;
                }
            }
            return -1;
        }

        // Mirrors VRClothPipeline.Run's backend choice: mesh-SDF when requested
        // and available, otherwise bone capsules (with radius estimation if on).
        // Rebuilt per pose so the collider reflects the driven body.
        static IBodyCollider BuildCollider(VRClothDeclipper fitter)
        {
            if (fitter.useMeshSdfCollider)
            {
                MeshSdfCollider sdf = VRClothBodySdfBuilder.Build(fitter);
                if (sdf != null)
                {
                    return sdf;
                }
                Debug.LogWarning("[VRClothMultiPose] Mesh-SDF collider unavailable — using bone capsules for this pose.");
            }
            var capsules = VRClothProxyGenerator.Generate(fitter.targetAvatar);
            if (fitter.estimateRadiiFromBody && capsules != null)
            {
                capsules = VRClothBodyRadiusEstimator.Apply(fitter, capsules).capsules;
            }
            return new CapsuleBodyCollider(capsules);
        }

        // Captures one renderer at the current (driven) pose: baked world
        // positions plus the per-vertex skin matrix that maps mesh-local space to
        // this pose's world (the same matrix the composer folds the push back
        // through). Both use the conventions VRClothMeshCapture/SkinningMath pin.
        static PoseCapture CaptureRenderer(SkinnedMeshRenderer renderer, IBodyCollider collider)
        {
            var mesh = renderer.sharedMesh;
            Vector3[] world = BakeToWorld(renderer);

            var weights = mesh.boneWeights;
            var bindPoses = mesh.bindposes;
            var bones = renderer.bones;
            var boneToWorld = new Matrix4x4[bones.Length];
            for (int b = 0; b < bones.Length; b++)
            {
                boneToWorld[b] = bones[b] != null ? bones[b].localToWorldMatrix : Matrix4x4.identity;
            }
            var skinMatrices = new Matrix4x4[world.Length];
            for (int v = 0; v < world.Length; v++)
            {
                skinMatrices[v] = SkinningMath.BlendedSkinMatrix(weights[v], boneToWorld, bindPoses);
            }
            return new PoseCapture { originalWorld = world, skinMatrices = skinMatrices, collider = collider };
        }

        static int CountPenetrating(Vector3[] world, IBodyCollider collider, float margin)
        {
            int count = 0;
            for (int v = 0; v < world.Length; v++)
            {
                if (collider.SignedDistance(world[v]) < margin - 1e-4f)
                {
                    count++;
                }
            }
            return count;
        }

        // Same world-space convention as VRClothMeshCapture: BakeMesh(useScale:
        // false) divides out the renderer's scale, then a rigid TRS reaches world.
        static Vector3[] BakeToWorld(SkinnedMeshRenderer renderer)
        {
            var baked = new Mesh();
            try
            {
                renderer.BakeMesh(baked, false);
                Vector3[] vertices = baked.vertices;
                Matrix4x4 toWorld = Matrix4x4.TRS(
                    renderer.transform.position, renderer.transform.rotation, Vector3.one);
                for (int v = 0; v < vertices.Length; v++)
                {
                    vertices[v] = toWorld.MultiplyPoint3x4(vertices[v]);
                }
                return vertices;
            }
            finally
            {
                Object.DestroyImmediate(baked);
            }
        }
    }
}
