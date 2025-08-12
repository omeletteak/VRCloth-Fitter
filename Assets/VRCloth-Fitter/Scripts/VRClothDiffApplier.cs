using UnityEngine;
using UnityEditor;
using System.IO;

namespace VRClothFitter
{
    public static class VRClothDiffApplier
    {
        private static string CacheFolder = "Assets/VRClothFitter/Cache";

        public static void SaveDiff(VRClothDiffMap diff)
        {
            if (!Directory.Exists(CacheFolder))
                Directory.CreateDirectory(CacheFolder);

            var asset = ScriptableObject.CreateInstance<VRClothDiffAsset>();
            asset.diffMap = diff;

            string assetPath = Path.Combine(CacheFolder, "Diff_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".asset");
            AssetDatabase.CreateAsset(asset, assetPath);
            AssetDatabase.SaveAssets();

            Debug.Log($"[VRClothFitter] Diff map saved: {assetPath}");
        }

        public static void ClearCache()
        {
            if (Directory.Exists(CacheFolder))
            {
                Directory.Delete(CacheFolder, true);
                File.Delete(CacheFolder + ".meta");
                AssetDatabase.Refresh();
                Debug.Log("[VRClothFitter] Cache cleared.");
            }
            else
            {
                Debug.Log("[VRClothFitter] No cache to clear.");
            }
        }

        public static void ApplyDiff(VRClothDiffAsset diffAsset, Mesh targetMesh)
        {
            if (diffAsset == null || diffAsset.diffMap.vertexOffsets == null)
            {
                Debug.LogWarning("[VRClothFitter] No diff data to apply.");
                return;
            }

            Vector3[] vertices = targetMesh.vertices;
            for (int i = 0; i < vertices.Length && i < diffAsset.diffMap.vertexOffsets.Length; i++)
            {
                vertices[i] += diffAsset.diffMap.vertexOffsets[i];
            }
            targetMesh.vertices = vertices;
            targetMesh.RecalculateNormals();
            Debug.Log("[VRClothFitter] Diff applied to mesh.");
        }
    }
}
