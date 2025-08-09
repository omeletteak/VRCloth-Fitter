# VRCloth-Fitter

[![MIT License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

VRChatアバターに衣装を簡単にフィットさせるために開発された、オープンソースのUnityエディタツールです。Modular Avatarが利用する**[Non-Destructive Modular Framework (NDMF)](https://github.com/bdunderscore/ndmf)**を活用し、すべて変更をビルド時に安全に適用します。

[English version is available here: README.md](./README.md).

## 主な機能

- **ボーンマッピング**: 衣装のボーンをアバターのボーンに自動で対応付けし、手動での調整も可能です。
- **NDMFベースのスケーリング**: ボーンの長さと太さを計算し、コンポーネントに保存します。スケーリングはアバターのビルド時にNDMF Passによって適用されるため、元のファイルは変更されません。
- **NDMFベースのメッシュ変形**: メッシュ上にアンカーポイントを配置することで、衣装をアバターの体型にフィットさせます。NDMF Passがビルド時に変形後の新しいメッシュを生成するため、完全に非破壊なワークフローが保証されます。
- **ブレンドシェイプ同期ヘルパー**: 対応するブレンドシェイプ名をマッピングし、Modular Avatarの`ModularAvatarBlendshapeSync`コンポーネントを自動でセットアップします。
- **マテリアル変換**: 衣装のマテリアルを指定したシェーダー（例：lilToon）に一括変換し、可能な限りテクスチャの割り当てを維持します。
- **プリセットシステム**: スケール調整とメッシュ変形のデータをJSONファイルとしてエクスポート・インポートし、コミュニティ間で共有できます。

## 導入方法

このパッケージはVRChat Creator Companion (VCC) を通じて配布されます。

1.  VCCを開き、**Settings** > **Community Packages** へ移動します。
2.  **Add**ボタンを押し、以下のURLを貼り付けます。
    ```
    https://raw.githubusercontent.com/omeletteak/vpm-listing/main/index.json
    ```
3.  **Confirm**を押すと、プロジェクトのパッケージ一覧に`VRCloth-Fitter`が表示され、追加できるようになります。

## 使い方

1.  **ボーンのフィット**:
    - Unityメニューの **Tools > VRCloth Fitter** からウィンドウを開きます。
    - AvatarとClothのGameObjectをそれぞれ設定します。
    - 「Bone Mapping」セクションで、自動的に対応付けされたボーンを確認し、間違っている箇所があればドロップダウンで修正します。
    - **Fit Bones**ボタンを押すと、衣装のSkinned Mesh Rendererのボーン参照が更新されます。
2.  **スケールのフィット**:
    - ボーンマッピング完了後、**Calculate & Save Scale**ボタンを押します。これにより、衣装オブジェクトに`VRClothFitterScalingData`コンポーネントが作成されます。
    - **Toggle Preview**ボタンを使うと、シーンビューで変更結果をプレビューできます。
    - スケールはアバターのアップロード時に自動的に適用されます。
3.  **メッシュの変形**:
    - 衣装オブジェクトに`VRClothFitterDeformationData`コンポーネントを追加します。
    - Avatar RootにアバターのGameObjectを設定します。
    - 「Add New Anchor Pair」ボタンを押し、シーンビューでアバター、次いで衣装のメッシュをクリックしてアンカーペアを作成します。
    - シーンビュー上のハンドルを操作して、アンカーの位置を調整します。
    - 変形はアバターのアップロード時に自動的に適用されます。

## 開発について

開発計画や機能の履歴については、[ROADMAP.md](./ROADMAP.md)をご覧ください。

## ライセンス

このプロジェクトはMITライセンスです。詳細は[LICENSE](./LICENSE)ファイルをご覧ください。
