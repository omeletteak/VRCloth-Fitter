///
using UnityEngine;
using nadena.dev.ndmf;
using VRC.SDKBase;
using System.Collections.Generic;

namespace VRClothFitter
{
    [DisallowMultipleComponent]
    public class ScalingHook : MonoBehaviour, IEditorOnly
    {
        // This component is just a marker to trigger the hook.
    }

    public class ScalingPass : Pass<ScalingPass>
    {
        public override string DisplayName => "Apply VRClothFitter Scaling";

        protected override void Execute(BuildContext context)
        {
            foreach (var scalingHook in context.AvatarRootObject.GetComponentsInChildren<ScalingHook>(true))
            {
                var clothObject = scalingHook.gameObject;
                var scalingData = clothObject.GetComponent<VRClothFitterScalingData>();
                if (scalingData == null || scalingData.boneScales.Count == 0) continue;

                var renderer = clothObject.GetComponent<SkinnedMeshRenderer>();
                if (renderer == null) continue;

                var boneMap = new Dictionary<string, Transform>();
                if (renderer.bones != null)
                {
                    foreach (var bone in renderer.bones)
                    {
                        if (bone != null && !boneMap.ContainsKey(bone.name))
                        {
                            boneMap[bone.name] = bone;
                        }
                    }
                }

                foreach (var boneScaleInfo in scalingData.boneScales)
                {
                    if (boneMap.TryGetValue(boneScaleInfo.boneName, out var bone))
                    {
                        bone.localScale = Vector3.Scale(bone.localScale, boneScaleInfo.scale);
                    }
                }
            }
        }
    }
}
///