// Assets/VRCloth-Declipper/Runtime/VRClothDeclipper.cs

using UnityEngine;

namespace VRClothDeclipper
{
    // Implements VRC.SDKBase.IEditorOnly when the VRChat SDK is present, so the
    // VRChat build pipeline strips this setup component from the uploaded
    // avatar (it carries fit settings only — nothing should ship). The SDK is
    // always present for real use: the package's vpmDependencies (Modular
    // Avatar / NDMF) require com.vrchat.avatars. The versionDefines guard keeps
    // the Runtime assembly compiling in SDK-less environments (e.g. headless
    // CI). Referencing the SDK is not redistributing it (docs/VPM_PACKAGING.md §2).
#if VRCLOTH_VRCSDK3_AVATARS
    public class VRClothDeclipper : MonoBehaviour, VRC.SDKBase.IEditorOnly
#else
    public class VRClothDeclipper : MonoBehaviour
#endif
    {
        [Header("Targets")]
        public GameObject targetAvatar;
        public GameObject sourceAvatar; // Optional: The avatar this cloth was originally made for.
        public GameObject clothRoot;

        [HideInInspector]
        public SkinnedMeshRenderer clothToDeform;

        public enum QualityMode { Light, Medium, High }
        [Header("Settings")]
        public QualityMode mode = QualityMode.Light;

        [Tooltip("Clearance kept between the body surface and the cloth, in meters. Vertices closer than this count as penetrating.")]
        [Range(0f, 0.05f)]
        public float margin = 0.005f;

        [Tooltip("Apply the fit even when the preflight diagnostic judges the body-shape difference out of the supported range (RED). Results will look wrong — this tool corrects penetration, it does not retarget garments. See docs/DESIGN.md §9.")]
        public bool forceApplyOutOfRange = false;

        [Tooltip("Use a mesh signed-distance collider built from the avatar's body mesh instead of bone capsules. Capsules can't represent the elliptical cross-section of the torso or the feet, so their null test reports false penetration; the mesh SDF fixes that (docs/DESIGN.md §6). Built in memory and discarded — No Cache holds. Off by default until E2E calibrates the thresholds.")]
        public bool useMeshSdfCollider = false;

        [Tooltip("Use the prototype normal/tangent-split solver (SolveProjected): it re-projects penetrating vertices onto the margin surface after every smoothing step, so the displacement field's normal height can't sink back in and no λ/pass tuning is needed (docs/DEFORMATION_METHODS.md §3.1). Off = the current coarse-pass solver. Prototype — compare the two in E2E.")]
        public bool useProjectedSolver = false;

        [Header("Body Radius Estimation")]
        [Tooltip("Measure each proxy capsule's radius from the avatar's body mesh instead of fixed defaults. Off = legacy fixed radii.")]
        public bool estimateRadiiFromBody = true;

        [Tooltip("Body mesh to measure radii from. Auto-detected (largest active skinned mesh on the Hips bone, excluding the cloth) when left empty.")]
        public SkinnedMeshRenderer bodyMesh;

        [Tooltip("Per capsule, the radius is this percentile of the body-surface distances assigned to it. Higher = looser (envelops more of the body), lower = tighter.")]
        [Range(0.5f, 1f)]
        public float radiusPercentile = 0.75f;

        private void Reset()
        {
            AutoDetectComponents();
        }

        private void OnValidate()
        {
            AutoDetectComponents();
        }

        public void AutoDetectComponents()
        {
            // 1. 衣装ルートの取得 (このGameObject自身)
            if (clothRoot == null) clothRoot = this.gameObject;

            // 2. 変形対象メッシュの取得 (clothRootまたはその子から)
            if (clothToDeform == null && clothRoot != null)
            {
                clothToDeform = clothRoot.GetComponentInChildren<SkinnedMeshRenderer>();
            }
            
            // 3. 対象アバター(変換先)の取得 (親を遡って探す)
            if (targetAvatar == null && clothRoot != null && clothRoot.transform.parent != null)
            {
                Transform parent = clothRoot.transform.parent;
                while (parent != null)
                {
                    Animator animator = parent.GetComponent<Animator>();
                    if (animator != null && animator.isHuman)
                    {
                        targetAvatar = parent.gameObject;
                        break; 
                    }
                    parent = parent.parent;
                }
            }
        }
    }
}