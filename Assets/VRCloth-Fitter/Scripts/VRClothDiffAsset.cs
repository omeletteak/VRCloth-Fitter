using UnityEngine;

namespace VRClothFitter
{
    [CreateAssetMenu(fileName = "VRClothDiff", menuName = "VRClothFitter/Diff Asset", order = 1)]
    public class VRClothDiffAsset : ScriptableObject
    {
        public VRClothDiffMap diffMap;
    }
}
