using UnityEngine;

namespace VRClothFitter
{
    public static class VRClothPipeline
    {
        public static void Run(VRClothFitter fitter)
        {
            if (fitter == null || fitter.targetAvatar == null || fitter.clothToDeform == null)
            {
                Debug.LogError("VRClothFitter: Target Avatar or Cloth is not set.");
                return;
            }

            string modeStr = fitter.mode.ToString();
            Debug.Log($"[VRClothFitter] Running in {modeStr} mode...");
            Debug.Log($"Target Avatar: {fitter.targetAvatar.name}, Cloth: {fitter.clothToDeform.name}");
            if (fitter.sourceAvatar != null)
            {
                Debug.Log($"Source Avatar: {fitter.sourceAvatar.name}");
            }

            // TODO: 実際の処理にfitter.targetAvatarとfitter.clothToDeformを渡すように修正
            var capsules = VRClothProxyGenerator.Generate(fitter.targetAvatar);
            if (capsules == null)
            {
                Debug.LogError("Failed to generate proxy capsules. Aborting.");
                return;
            }
            
            var penetrations = VRClothPenetrationDetector.Detect();
            Debug.Log($"Detected {penetrations} penetrations.");

            if (fitter.mode == VRClothFitter.QualityMode.Light)
            {
                VRClothLaplacian.Smooth();
            }

            // ダミーの頂点変位データ作成
            VRClothDiffMap diff = new VRClothDiffMap
            {
                metadata = $"Mode={modeStr}, Time={System.DateTime.Now}",
                vertexOffsets = new Vector3[10] // 本来はターゲットメッシュ頂点数
            };
            for (int i = 0; i < diff.vertexOffsets.Length; i++)
                diff.vertexOffsets[i] = Random.insideUnitSphere * 0.001f; // 微小な変位

            // VRClothDiffApplier.SaveDiff(diff); // キャッシュ機能は未実装のため、一時的に無効化

            Debug.Log("[VRClothFitter] Process complete.");
        }

        public static void ClearCache()
        {
            // TODO: この機能も新しいUIに統合するか検討
            // VRClothDiffApplier.ClearCache();
        }
    }
}
