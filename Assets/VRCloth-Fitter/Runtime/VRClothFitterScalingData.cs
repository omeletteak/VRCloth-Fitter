using UnityEngine;
using System.Collections.Generic;

namespace VRClothFitter
{
    [System.Serializable]
    public class BoneScaleInfo
    {
        public string boneName;
        public Vector3 scale;
    }

    [AddComponentMenu("VRCloth Fitter/Scaling Data")]
    public class VRClothFitterScalingData : MonoBehaviour
    {
        public List<BoneScaleInfo> boneScales = new List<BoneScaleInfo>();
    }
}
