using UnityEngine;

namespace VRClothFitter
{
    [System.Serializable]
    public class VRClothDiffMap
    {
        public string metadata;
        public Vector3[] vertexOffsets; // 頂点の変位量（ダミー）
    }
}
