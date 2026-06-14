# NDMF 統合スパイク — パス順序の事前検証

**状態: [手順]**（使い捨ての検証実験。本実装の前提固め）

## 目的

VRCloth-Fitter の製品形態は「ビルド時に走り、**どんな着せ方の後でも**残った貫通を直す NDMF パス」を想定している（[ROADMAP](../ROADMAP.md) フェーズ5、[ECOSYSTEM_VISION](ECOSYSTEM_VISION.md)）。この「後段に挿せば全部に効く」という前提は、NDMF のビルドパイプライン上で**自分のパスを正しい位置に・安定して置けること**に全面的に乗っている。本スパイクは、本実装に着手する前に、その前提を実機ビルドで確認するためのもの。数時間の使い捨て実験で前提を固めてから本パスを建てる ↔ 潰さず本実装してズレ発覚で作り直し、の差を消す。

## なぜビルド時でなければならないか

衣装が最終的にフィットした位置になるのは**ビルド時**である。エディタ上では、Modular Avatar の衣装はまだ Merge Armature コンポーネントを持つ別オブジェクトで、合体していない（Setup Outfit はそのコンポーネントを仕込むエディタ操作で、実際のマージはビルド時パスが行う）。したがって「着せた後の形状」に対して貫通修正を行うには、こちらもビルド時に、マージパスより**後**で走る必要がある。これがパス順序を要点にする理由。

なお、エディタ時に衣装メッシュを生成・変換する種類のフィットは、結果がビルド前にシーンへ焼かれているため自然に上流に来る。検証対象はあくまで**ビルド時パス同士の並び**。

## 確かめる3点

### ① 位置 — どのフェーズ・どの順で走るか
NDMF はビルドをフェーズに分けて実行する（Resolving → Generating → Transforming → Optimizing、※正確な区切りは実物で確認）。骨格マージは Transforming 付近、メッシュ最適化（Avatar Optimizer 等）は Optimizing 付近と見込まれる。VRCloth-Fitter のパスは「**骨格マージの後・メッシュ最適化の前**」に入りたい。NDMF の順序指定（AfterPlugin / BeforePlugin 等）が実際に効き、毎ビルド安定するかを確認する。

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

出力は project ルートの **`vrcloth-ndmf-probe.json`**（3点のスナップショットを1ファイルに集約）に書く。Console コピペでなくこの JSON を直接読んで判定する（[VRClothRunLog](../Assets/VRCloth-Fitter/Editor/VRClothRunLog.cs) と同じ「AI が読める場所に出す」方針）。Console には各点1行の確認ログ（フェーズ・SMR 数・出力パス）のみ。

プローブは VRCloth-Fitter パッケージではなく**テストプロジェクト側**（`Assets/VRClothSpike/`、専用 Editor asmdef + 1ファイル）に置く。NDMF 依存をパッケージへ持ち込まないため。スパイク後は削除する。

## 実行手順

1. **プローブ配置（実施済み）** — `vrcloth-fitter-test/Assets/VRClothSpike/` に確定版プローブ + 専用 Editor asmdef（`nadena.dev.ndmf` 参照）を配置済み。API は導入済み NDMF ソースで確認済み。
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

## 結果と判断（実行後に記入）

| 項目 | 観測 |
|---|---|
| 実行フェーズ・順 | TBD |
| マージ済みか（①） | TBD |
| ジオメトリ整合（②） | TBD |
| トポロジ安定（③） | TBD |

**判断:**
- 安定して正しい位置に置ける → NDMF パス本実装へ（ROADMAP フェーズ5「NDMF パス化」）。
- 置けない／ばらつく → 代替（エディタ時の明示実行、依存順の固定方法、別フェーズ）を検討してから本実装。
