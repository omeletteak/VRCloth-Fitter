using nadena.dev.ndmf;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

[assembly: ExportsPlugin(typeof(VRClothFitter.DeformationPassPlugin))]

namespace VRClothFitter
{
    public class DeformationPassPlugin : Plugin<DeformationPassPlugin>
    {
        public override string QualifiedName => "dev.omelette.vrcloth-fitter.deformation-pass";
        public override string DisplayName => "VRCloth Fitter Deformation Pass";

        protected override void Configure()
        {
            InPhase(BuildPhase.Transforming)
                .BeforePlugin("nadena.dev.modular-avatar")
                .Run("Deform cloth mesh", ctx =>
                {
                    var deformationDataComponents = ctx.AvatarRootObject.GetComponentsInChildren<VRClothFitterDeformationData>(true);

                    foreach (var data in deformationDataComponents)
                    {
                        var renderer = data.GetComponent<SkinnedMeshRenderer>();
                        if (renderer == null || renderer.sharedMesh == null || data.anchorPairs.Count == 0)
                        {
                            continue;
                        }

                        // Create a new, unique mesh to deform (non-destructive)
                        var originalMesh = renderer.sharedMesh;
                        var newMesh = Object.Instantiate(originalMesh);
                        newMesh.name = $"{originalMesh.name} (Deformed)";
                        
                        var vertices = newMesh.vertices;
                        var newVertices = new Vector3[vertices.Length];
                        
                        // Calculate displacement vectors for each anchor
                        var displacements = data.anchorPairs.Select(p => p.avatarAnchor - p.clothAnchor).ToList();

                        // Deform vertices
                        for (int i = 0; i < vertices.Length; i++)
                        {
                            var totalWeight = 0f;
                            var totalDisplacement = Vector3.zero;

                            for (int j = 0; j < data.anchorPairs.Count; j++)
                            {
                                float dist = Vector3.Distance(vertices[i], data.anchorPairs[j].clothAnchor);
                                float weight = 1.0f / (dist * dist + 0.0001f); // Inverse square distance with a small epsilon
                                
                                totalDisplacement += displacements[j] * weight;
                                totalWeight += weight;
                            }

                            if (totalWeight > 0)
                            {
                                newVertices[i] = vertices[i] + totalDisplacement / totalWeight;
                            }
                            else
                            {
                                newVertices[i] = vertices[i];
                            }
                        }

                        newMesh.vertices = newVertices;
                        newMesh.RecalculateNormals();
                        newMesh.RecalculateBounds();
                        
                        // Save the new mesh as an asset
                        AssetDatabase.AddObjectToAsset(newMesh, ctx.AssetContainer);
                        
                        // Apply the new mesh
                        renderer.sharedMesh = newMesh;
                        
                        // Clean up the component
                        Object.DestroyImmediate(data);
                    }
                });
        }
    }
}
