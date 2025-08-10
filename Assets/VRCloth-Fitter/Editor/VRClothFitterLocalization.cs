using System.Collections.Generic;
using UnityEditor;

namespace VRClothFitter
{
    public static class VRClothFitterLocalization
    {
        private static readonly Dictionary<string, string> ja = new Dictionary<string, string>
        {
            // Common
            { "Avatar", "アバター" },
            { "Cloth", "衣装" },
            { "None", "[なし]" },

            // VRClothFitterWindow
            { "Bone Mapping", "ボーンマッピング" },
            { "Cloth Bone", "衣装のボーン" },
            { "Avatar Bone", "アバターのボーン" },
            { "Please set Avatar and Cloth objects.", "アバターと衣装のオブジェクトを設定してください。" },
            { "Blendshape Sync", "ブレンドシェイプ同期" },
            { "Cloth Blendshape", "衣装のブレンドシェイプ" },
            { "Avatar Blendshape", "アバターのブレンドシェイプ" },
            { "No blendshapes found or objects not set.", "ブレンドシェイプが見つからないか、オブジェクトが設定されていません。" },
            { "Fit Bones", "ボーンを合わせる" },
            { "Calculate & Save Scale", "スケールを計算して保存" },
            { "Apply Blendshape Sync", "ブレンドシェイプ同期を適用" },
            { "Toggle Preview", "プレビュー表示" },
            { "Stop Preview", "プレビュー停止" },
            { "Material & Shader Utilities", "マテリアル＆シェーダーユーティリティ" },
            { "Target Shader", "対象シェーダー" },
            { "No materials found on cloth object.", "衣装にマテリアルが見つかりません。" },
            { "Convert Materials", "マテリアルを変換" },

            // Scaling Data Editor
            { "Export Preset", "プリセットをエクスポート" },
            { "Import Preset", "プリセットをインポート" },
            
            // Deformation Data Editor
            { "Anchor Points", "アンカーポイント" },
            { "Add New Anchor Pair", "アンカーペアを新規追加" },
            { "Cancel Placing Anchor", "アンカー配置をキャンセル" },
            { "Place New Anchor", "新規アンカーを配置" },
            { "1. Click on the AVATAR mesh to place the first anchor point.", "1. アバターのメッシュをクリックして、1つ目のアンカーを配置します。" },
            { "2. Click on the CLOTH mesh to place the second anchor point.", "2. 衣装のメッシュをクリックして、2つ目のアンカーを配置します。" },
            { "Press ESC to cancel.", "Escキーでキャンセル" },
        };

        private static bool isJapanese;

        static VRClothFitterLocalization()
        {
            isJapanese = Application.systemLanguage == SystemLanguage.Japanese;
        }

        public static string Tr(string key)
        {
            if (isJapanese && ja.TryGetValue(key, out string value))
            {
                return value;
            }
            return key; // Return the key itself as the default (English) value
        }
    }
}
