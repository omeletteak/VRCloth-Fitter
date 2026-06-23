using nadena.dev.ndmf;
using nadena.dev.ndmf.fluent;

[assembly: ExportsPlugin(typeof(VRClothDeclipper.VRClothDeclipperNdmfPlugin))]

namespace VRClothDeclipper
{
    /// <summary>
    /// Registers VRCloth-Declipper with NDMF so the penetration fix runs as part
    /// of the avatar build — surviving VRChat upload — instead of only as an
    /// edit-time scene edit. The pass is sequenced in the Transforming phase
    /// <em>after</em> Modular Avatar's Merge Armature, because this project's core
    /// operation is "declip after Merge Armature": only then is the cloth skinned
    /// to the final merged armature and the target body in its final form. Running
    /// before AAO (Optimizing phase) means the optimizer consumes the fitted mesh.
    ///
    /// The same fit is shown live in the editor via PreviewingWith — both the pass
    /// and the preview call <see cref="VRClothPipeline.SolveToFittedMeshes"/>, so
    /// the previewed mesh is exactly the uploaded mesh.
    /// </summary>
    public class VRClothDeclipperNdmfPlugin : Plugin<VRClothDeclipperNdmfPlugin>
    {
        public override string QualifiedName => "dev.omelette_ak.vrcloth-declipper";
        public override string DisplayName => "VRCloth-Declipper";

        protected override void Configure()
        {
            InPhase(BuildPhase.Transforming)
                .AfterPlugin("nadena.dev.modular-avatar")
                .Run(VRClothDeclipperPass.Instance)
                .PreviewingWith(new VRClothDeclipperPreview());
        }
    }
}
