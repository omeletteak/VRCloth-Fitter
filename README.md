# VRCloth-Declipper

[![MIT License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Unity](https://img.shields.io/badge/Unity-2022.3-blue.svg)](#動作環境)
[![Status](https://img.shields.io/badge/Status-WIP-orange.svg)](#開発状況)

*English: [README(en).md](README(en).md)*

**着せた後の衣装貫通を自動修正する、オープンソースのUnityエディタツールです。**

*An open-source Unity editor tool that auto-fixes body-through-clothing clipping left after dressing a VRChat avatar (e.g., via Modular Avatar's Merge Armature). See [README(en).md](README(en).md); detailed docs are Japanese-only.*

Modular Avatar の Merge Armature(Setup Outfit)で衣装を着せても、体型の差で素体が衣装を突き抜ける「貫通」は残ります。VRCloth-Declipper はこの貫通を、貫通検出 → 押し出し → ラプラシアン平滑化のパイプラインで自動修正します。

## このツールがやること・やらないこと

**やること:**

- Merge Armature 適用後に残るメッシュ貫通の自動修正
- どの方法で着せたかを問わない後処理(対応衣装の手動着せ替えでも、変換ツールで変換した衣装でも)
- 非破壊ワークフロー(元のメッシュアセットは変更しない)

**やらないこと:**

- 非対応衣装の体型変換(リターゲティング)そのもの — それは[もちふぃった～](https://booth.pm/ja/items/7657840)や[Alterith](https://booth.pm/ja/items/7131644)のような変換ツールの領分です。VRCloth-Declipper はそれらの**後段**を補完します(変換ツールはいずれも貫通修正を扱いません)
- アバター形状データの保存・出力(下記の設計原則)

## 設計原則: No Cache

**アバターの素体形状を復元しうるデータを、ディスクに保存せず、ツールの外に出しません。**

体型の差分データは、精度が高いほど元アバターの素体形状を復元できてしまい、アバターの利用規約や作者の権利と衝突します。VRCloth-Declipper は貫通修正をその場で計算してその場で適用し、形状由来の中間データを残さない設計です。オープンソースなのは、この約束をコードで検証可能にするためでもあります。

詳しい背景と先行ツールとの関係は [docs/DESIGN.md](docs/DESIGN.md) を参照してください。

## 開発状況

**MVP(貫通修正コア)の実装は完了。残るは実機での目視 E2E 検証と配布(VPM化)です。** 配布パッケージはまだ公開していません。

| パイプライン | 状態 |
|---|---|
| プロキシボディ生成(ボーンカプセル) | ✅ 実装済み |
| カプセル距離計算(SDF) | ✅ 実装済み(テスト付き) |
| シーンビュー可視化(カプセル+侵入頂点ヒートマップ) | ✅ 実装済み |
| 貫通検出 | ✅ 実装済み(テスト付き) |
| 押し出し+ラプラシアン平滑化 | ✅ 実装済み(テスト付き) |
| 非破壊適用(メッシュ複製・Undo 対応) | ✅ 実装済み(テスト付き) |
| プリフライト診断(緑/黄/赤でサポート範囲判定) | ✅ 実装済み(テスト付き) |
| 素体表現: メッシュSDF コライダ(BVH + 高速巻き数で加速) | ✅ 実装済み(テスト付き) |
| 実機での目視 E2E 検証 | 🚧 残(手順: [docs/E2E_TEST_GUIDE.md](docs/E2E_TEST_GUIDE.md)) |

EditMode テスト 89 件が緑。進行中の計画は [ROADMAP.md](ROADMAP.md) を参照してください。

## 動作環境

- Unity **2022.3.22f1**(VRChat 推奨バージョン)
- [ALCOM](https://vrc-get.anatawa12.com/ja/alcom/) または VRChat Creator Companion(VCC)で管理された VPM プロジェクト
- [NDMF](https://github.com/bdunderscore/ndmf) / [Modular Avatar](https://modular-avatar.nadena.dev/)

## 試してみる(開発者向け)

配布パッケージはまだありません。現状はリポジトリをクローンして使います。

```powershell
git clone https://github.com/omeletteak/VRCloth-Declipper.git
cd VRCloth-Declipper
vrc-get resolve   # または ALCOM / VCC で開いて依存解決(VRChat SDK 等は同梱していません)
```

別の VPM プロジェクトで試す場合は、パッケージフォルダだけをジャンクションでリンクします(リポジトリ全体をコピーしないでください — SDK が二重になります):

```powershell
New-Item -ItemType Junction `
  -Path "<テストプロジェクト>\Assets\VRCloth-Declipper" `
  -Target "<クローン先>\Assets\VRCloth-Declipper"
```

使い方: Modular Avatar の Setup Outfit で衣装を着せた後、衣装の GameObject に `VRClothDeclipper` コンポーネントを付けると、衣装メッシュと親のアバターを自動検出します。インスペクタの **Preview Body Proxy** でプロキシをシーンビューに表示し、**Run Fitting** で貫通修正を実行します(元メッシュは非破壊、Undo で復帰)。素体をより正確に表したい場合は **Use Mesh SDF Collider** を有効化すると、ボーンカプセルの代わりに素体メッシュから構築した符号付き距離場で衝突判定します([docs/DESIGN.md](docs/DESIGN.md) §6)。

## コントリビュート

バグ報告・提案・プルリクエストを歓迎します。[CONTRIBUTING.md](CONTRIBUTING.md) を参照してください。

## ライセンス

[MIT License](LICENSE)
