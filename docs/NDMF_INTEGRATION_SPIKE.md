# NDMF 統合スパイク — パス順序の事前検証

**状態: [手順]**（使い捨ての検証実験。本実装の前提固め）

## 目的

VRCloth-Declipper の製品形態は「ビルド時に走り、**どんな着せ方の後でも**残った貫通を直す NDMF パス」を想定している（[ROADMAP](../ROADMAP.md) フェーズ5、[ECOSYSTEM_VISION](ECOSYSTEM_VISION.md)）。この「後段に挿せば全部に効く」という前提は、NDMF のビルドパイプライン上で**自分のパスを正しい位置に・安定して置けること**に全面的に乗っている。本スパイクは、本実装に着手する前に、その前提を実機ビルドで確認するためのもの。数時間の使い捨て実験で前提を固めてから本パスを建てる ↔ 潰さず本実装してズレ発覚で作り直し、の差を消す。

## なぜビルド時でなければならないか

衣装が最終的にフィットした位置になるのは**ビルド時**である。エディタ上では、Modular Avatar の衣装はまだ Merge Armature コンポーネントを持つ別オブジェクトで、合体していない（Setup Outfit はそのコンポーネントを仕込むエディタ操作で、実際のマージはビルド時パスが行う）。したがって「着せた後の形状」に対して貫通修正を行うには、こちらもビルド時に、マージパスより**後**で走る必要がある。これがパス順序を要点にする理由。

なお、エディタ時に衣装メッシュを生成・変換する種類のフィットは、結果がビルド前にシーンへ焼かれているため自然に上流に来る。検証対象はあくまで**ビルド時パス同士の並び**。

## 確かめる3点

### ① 位置 — どのフェーズ・どの順で走るか
NDMF はビルドをフェーズに分けて実行する（Resolving → Generating → Transforming → Optimizing、※正確な区切りは実物で確認）。骨格マージは Transforming 付近、メッシュ最適化（Avatar Optimizer 等）は Optimizing 付近と見込まれる。VRCloth-Declipper のパスは「**骨格マージの後・メッシュ最適化の前**」に入りたい。NDMF の順序指定（AfterPlugin / BeforePlugin 等）が実際に効き、毎ビルド安定するかを確認する。

### ② 状態 — その瞬間のジオメトリは想定通りか
ビルド時のアバターは基本ビルド姿勢（レスト/T）・ブレンドシェイプ既定値。コアは現在ポーズの頂点で検出・半径推定する。パス実行時点で次を確認する。
- (a) 衣装メッシュが素体アーマチュアにマージ済み（ボーン付け替え済み）か
- (b) 頂点が最終フィット位置にあるか
- (c) 半径推定で読む素体メッシュのブレンドシェイプ/ポーズ状態と整合するか

### ③ トポロジ — 後段で壊されないか
メッシュ最適化はメッシュ結合・ブレンドシェイプ焼き込み・未使用頂点削除で頂点インデックスやトポロジを変えうる。こちらの修正が後段でも保たれるか、あるいはこちらが最適化の前で素のメッシュを触るか — 安定した前後関係が要る。

## プローブ（使い捨て・テストプロジェクトに常駐）

導入済み NDMF（`Packages/nadena.dev.ndmf`）のソースを読み、API と並びを確定済み:

- フェーズ順: `FirstChance → PlatformInit → Resolving → Generating → Transforming → Optimizing → PlatformFinish`
- **MA の Merge Armature は `Transforming` フェーズで走る**（`PluginDefinition.cs` で確認）→ 仮説①の「位置」は概ね事前確定
- MA プラグインの `QualifiedName` は `"nadena.dev.modular-avatar"`、順序指定は `Sequence.AfterPlugin(qualifiedName)`

確定した最小プローブは**何も変形せず、3点でアバターのスキンメッシュをダンプ**する（1回の bake で前後比較できる）:

1. `InPhase(BuildPhase.Generating)` — マージ前（baseline）
2. `InPhase(BuildPhase.Transforming).AfterPlugin("nadena.dev.modular-avatar")` — **MA マージ後・最適化前（本命スロット）**
3. `InPhase(BuildPhase.PlatformFinish)` — 最適化後（最終状態）

各点で SMR ごとに「階層パス / 頂点数 / ブレンドシェイプ数 / ボーン数 / rootBone・bone[0] の階層パス / ワールド bounds」を記録する。**ボーンの階層パス**がマージ完了の指標（衣装の bones が素体アーマチュア配下を指す）、**頂点数・SMR 数の変化**が最適化のトポロジ改変の指標。

出力は project ルートの **`vrcloth-ndmf-probe.json`**（3点のスナップショットを1ファイルに集約）に書く。Console コピペでなくこの JSON を直接読んで判定する（[VRClothRunLog](../Assets/VRCloth-Declipper/Editor/VRClothRunLog.cs) と同じ「AI が読める場所に出す」方針）。Console には各点1行の確認ログ（フェーズ・SMR 数・出力パス）のみ。

プローブは VRCloth-Declipper パッケージではなく**テストプロジェクト側**（`Assets/VRClothSpike/`、専用 Editor asmdef + 1ファイル）に置く。NDMF 依存をパッケージへ持ち込まないため。スパイク後は削除する。

## 実行手順

1. **プローブ配置（実施済み）** — `vrcloth-declipper-test/Assets/VRClothSpike/` に確定版プローブ + 専用 Editor asmdef（`nadena.dev.ndmf` 参照）を配置済み。API は導入済み NDMF ソースで確認済み。
2. テストアバター（MA 衣装 + Avatar Optimizer あり）にプローブを入れる。
3. **Manual bake avatar**（アップロード不要・エディタ内でビルドパイプラインを実走）で焼く。
4. プローブが project ルートに `vrcloth-ndmf-probe.json`（3点の SMR スナップショット）を書く。これを読んで①②③を判定する。
5. 順序指定（AfterPlugin / BeforePlugin 等）を変え、NDMF が実際にどう並べ替えるかを観測する。

## 成功・失敗の基準

- **成功**: 自パスを「衣装マージ済み・最終ジオメトリ・トポロジ未破壊」の点に**安定して**置ける。
- **失敗（捕まえたいもの）**:
  - (a) マージ前に走り、衣装がまだ別オブジェクト
  - (b) メッシュ最適化後に走り、頂点インデックス/トポロジが変わっている
  - (c) 順序指定が効かず、ビルドごとに位置がばらつく

## 結果と判断（2026-06-14 実施: ミルティナ + 競泳水着 + AAO）

`Assets/VRClothSpike` 配置 → コンパイル成功 → Manual bake で `vrcloth-ndmf-probe.json` 生成。衣装 `Miltina_競泳水着` を3点で追跡:

| 項目 | 観測 |
|---|---|
| 実行フェーズ・順 | 3点とも期待通り発火。`InPhase(Transforming).AfterPlugin("nadena.dev.modular-avatar")` が安定スロットとして機能 |
| ①マージ済みか | **Yes。** Generating では衣装の rootBone/bone0 = 衣装自前 `…/Armature.1/Hips`、bounds 0.43m（局所）。Transforming after MA では `Armature/Hips`（素体）・bounds 2.0m（全身）= **素体アーマチュアへ統合済み** |
| ②ジオメトリ整合 | **Yes。** 同点で衣装メッシュが `RETARGETED__Miltina_競泳水着`（= MA Merge Armature 内部の `MeshRetargeter` による bind-pose 再バインド後の作業名。衣装は元から対応品で、別途のフィット/リターゲはしていない）。素体（`Milltina_body` 17749v / `Body` 14316v）もブレンドシェイプ込みで無傷 = 半径推定に使える |
| ③トポロジ安定 | **本命スロットでは安定、Optimizing 後は崩壊。** after MA は 16 SMR・原頂点数・ブレンドシェイプ保持。PlatformFinish では AAO が **16→3 SMR**（本体+多数を `$$AAO_AUTO_MERGE_SKINNED_MESH_0` 59617v へ結合、ブレンドシェイプ凍結、メッシュ改名）。後段では手遅れ |

**判断: 仮説は成立。本実装は `InPhase(BuildPhase.Transforming).AfterPlugin("nadena.dev.modular-avatar")`（MAマージ後・AAO最適化前）を基点に進める。** 確定事項と残課題:

- **必ず Optimizing（AAO）より前に走る。** AAO がメッシュ結合・頂点/ブレンドシェイプ/ボーンの作り替えを行うため後段では per-garment メッシュも原トポロジも失われる。Transforming スロットは構造上 Optimizing より前なので自動的に満たす。
- **`.AfterPlugin(MA)` 一つで MA のマージ＋内部 `MeshRetargeter` まで後段に来る**（`RETARGETED__` は MA 自身の merge 内部処理で、別のフィットツールではない）。標準的な MA(+AAO) 構成ではこのスロットで十分。ただし**ビルド時に走る別系統のフィット/メッシュ加工ツール**を併用する場合は、それらの後にも順序指定が要る（今回の構成には無し）。
- 本スロットで衣装を補正すれば AAO はその補正済みメッシュを入力として最適化する（競泳水着は AAO 後も別メッシュ・同頂点数で残存 = 補正は保たれる）。
- **本スパイクの範囲外（続編スパイク要）**: 本検証は「何も変形しないダンプ」で**順序のみ**を確かめた。本命の代表ポーズ対応は**ビルド中にアバターをポーズ駆動して Bake** する必要があり（§② が確定した通りビルド時は T-pose 固定）、その状態操作がビルドコンテキストで安全かは本スパイクが踏んでいない未検証領域。パス順とは独立の論点で、続編の実験計画は [MULTIPOSE_GLUE_SPIKE.md](MULTIPOSE_GLUE_SPIKE.md)。
- 副次観測: 素体メッシュが複数（`Body` / `Milltina_body`）。A改の素体自動検出は kisekae の複数ボディで誤検出しうる → `bodyMesh` 明示指定オプション（実装済み）で対処。
