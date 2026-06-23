using nadena.dev.ndmf;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VRClothDeclipper
{
    /// <summary>
    /// The NDMF build pass that makes the fit survive upload. Sequenced (in
    /// <see cref="VRClothDeclipperNdmfPlugin"/>) to run in the Transforming phase
    /// after Modular Avatar's Merge Armature, so the cloth is skinned to the final
    /// merged armature and the body proxy is built from the real target body —
    /// this project's core operation is "declip after Merge Armature".
    ///
    /// It applies the same fit as the live preview (both call
    /// <see cref="VRClothPipeline.SolveToFittedMeshes"/>) onto the build clone, so
    /// what the user previews in the editor is exactly what ships. The fitted
    /// meshes are created in memory; NDMF serializes them into the uploaded
    /// avatar's asset container — nothing is written into the project
    /// (No Cache, docs/DESIGN.md §5).
    /// </summary>
    internal class VRClothDeclipperPass : Pass<VRClothDeclipperPass>
    {
        protected override void Execute(BuildContext context)
        {
            GameObject avatarRoot = context.AvatarRootObject;
            foreach (var fitter in avatarRoot.GetComponentsInChildren<VRClothDeclipper>(true))
            {
                try
                {
                    foreach (var (renderer, fitted) in VRClothPipeline.SolveToFittedMeshes(fitter, avatarRoot, verbose: true))
                    {
                        if (renderer != null && fitted != null)
                        {
                            renderer.sharedMesh = fitted;
                        }
                    }
                }
                catch (System.Exception e)
                {
                    // A fit failure must not break the avatar build: ship the
                    // (unfixed) cloth and surface the reason instead of aborting.
                    Debug.LogWarning($"[VRClothDeclipper] Build-time fit failed on '{fitter.name}': {e.Message}");
                }

                // The setup component carries fit settings only; strip it from the
                // output. It is IEditorOnly (the SDK would remove it), but remove
                // it explicitly so it never lingers in the built hierarchy.
                Object.DestroyImmediate(fitter);
            }
        }
    }
}
