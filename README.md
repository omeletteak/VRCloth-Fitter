# VRCloth-Fitter

[![MIT License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Unity](https://img.shields.io/badge/Unity-2022.3-blue.svg)](#動作環境)
[![Status](https://img.shields.io/badge/Status-WIP-orange.svg)](#開発状況)

**着せた後の衣装貫通を自動修正する、オープンソースのUnityエディタツールです。**

*An open-source Unity editor tool that auto-fixes body-through-clothing clipping left after dressing a VRChat avatar (e.g., via Modular Avatar's Merge Armature). Documentation is currently Japanese-only; an English README is planned.*

Modular Avatar の Merge Armature(Setup Outfit)で衣装を着せても、体型の差で素体が衣装を突き抜ける「貫通」は残ります。VRCloth-Fitter はこの貫通を、貫通検出 → 押し出し → ラプラシアン平滑化のパイプラインで自動修正します。

## このツールがやること・やらないこと

**やること:**

- Merge Armature 適用後に残るメッシュ貫通の自動修正
- どの方法で着せたかを問わない後処理(対応衣装の手動着せ替えでも、変換ツールで変換した衣装でも)
- 非破壊ワークフロー(元のメッシュアセットは変更しない)

**やらないこと:**

- 非対応衣装の体型変換(リターゲティング)そのもの — それは[もちふぃった～](https://booth.pm/ja/items/7657840)や[Alterith](https://booth.pm/ja/items/7131644)のような変換ツールの領分です。VRCloth-Fitter はそれらの**後段**を補完します(変換ツールはいずれも貫通修正を扱いません)
- アバター形状データの保存・出力(下記の設計原則)

## 設計原則: No Cache

**アバターの素体形状を復元しうるデータを、ディスクに保存せず、ツールの外に出しません。**

体型の差分データは、精度が高いほど元アバターの素体形状を復元できてしまい、アバターの利用規約や作者の権利と衝突します。VRCloth-Fitter は貫通修正をその場で計算してその場で適用し、形状由来の中間データを残さない設計です。オープンソースなのは、この約束をコードで検証可能にするためでもあります。

詳しい背景と先行ツールとの関係は [docs/DESIGN.md](docs/DESIGN.md) を参照してください。

## 開発状況

**現在は開発初期(MVP実装中)で、配布はまだ行っていません。**

| パイプライン | 状態 |
|---|---|
| プロキシボディ生成(ボーンカプセル) | ✅ 実装済み |
| カプセル距離計算(SDF) | ✅ 実装済み(テスト付き) |
| シーンビュー可視化(カプセル+侵入頂点ヒートマップ) | ✅ 実装済み |
| 貫通検出 | 🚧 実装中 |
| 押し出し+ラプラシアン平滑化 | 🚧 実装中 |
| 非破壊適用 | 📋 計画中 |

進行中の計画は [ROADMAP.md](ROADMAP.md) を参照してください。

## 動作環境

- Unity **2022.3.22f1**(VRChat 推奨バージョン)
- [ALCOM](https://vrc-get.anatawa12.com/ja/alcom/) または VRChat Creator Companion(VCC)で管理された VPM プロジェクト
- [NDMF](https://github.com/bdunderscore/ndmf) / [Modular Avatar](https://modular-avatar.nadena.dev/)

## 試してみる(開発者向け)

配布パッケージはまだありません。現状はリポジトリをクローンして使います。

```powershell
git clone https://github.com/omeletteak/VRCloth-Fitter.git
cd VRCloth-Fitter
vrc-get resolve   # または ALCOM / VCC で開いて依存解決(VRChat SDK 等は同梱していません)
```

別の VPM プロジェクトで試す場合は、パッケージフォルダだけをジャンクションでリンクします(リポジトリ全体をコピーしないでください — SDK が二重になります):

```powershell
New-Item -ItemType Junction `
  -Path "<テストプロジェクト>\Assets\VRCloth-Fitter" `
  -Target "<クローン先>\Assets\VRCloth-Fitter"
```

使い方: 衣装の GameObject に `VRClothFitter` コンポーネントを付けると、衣装メッシュと親のアバターを自動検出します。インスペクタの **Preview Body Proxy** でプロキシカプセルをシーンビューに表示できます。

## コントリビュート

バグ報告・提案・プルリクエストを歓迎します。[CONTRIBUTING.md](CONTRIBUTING.md) を参照してください。

## ライセンス

[MIT License](LICENSE)
