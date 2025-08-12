using UnityEngine;

namespace VRClothFitter
{
    public static class VRClothPenetrationDetector
    {
        public static int Detect()
        {
            Debug.Log("[VRClothFitter] Detecting mesh penetrations...");
            // 実装：カプセルと衣装メッシュの距離を測定
            return Random.Range(0, 10); // ダミーで件数返す
        }
    }
}
