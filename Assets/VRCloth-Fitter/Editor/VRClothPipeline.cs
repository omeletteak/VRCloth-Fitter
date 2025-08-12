using UnityEngine;

public static class VRClothPipeline
{
    public static void Run(string mode)
    {
        Debug.Log($"[VRClothFitter] Running in {mode} mode...");

        VRClothProxyGenerator.Generate();
        var penetrations = VRClothPenetrationDetector.Detect();
        Debug.Log($"Detected {penetrations} penetrations.");

        if (mode == "Lightweight")
        {
            VRClothLaplacian.Smooth();
        }

        // ダミーの頂点変位データ作成
        VRClothDiffMap diff = new VRClothDiffMap
        {
            metadata = $"Mode={mode}, Time={System.DateTime.Now}",
            vertexOffsets = new Vector3[10] // 本来はターゲットメッシュ頂点数
        };
        for (int i = 0; i < diff.vertexOffsets.Length; i++)
            diff.vertexOffsets[i] = Random.insideUnitSphere * 0.001f; // 微小な変位

        // VRClothDiffApplier.SaveDiff(diff); // キャッシュ機能は未実装のため、一時的に無効化

        Debug.Log("[VRClothFitter] Process complete.");
    }

    public static void ClearCache()
    {
        VRClothDiffApplier.ClearCache();
    }
}
