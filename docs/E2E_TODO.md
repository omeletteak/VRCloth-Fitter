# 直近の E2E タスク（これ1本で次が分かる）

> **この1本が「次に手を動かす GUI E2E」の唯一の入口。** 各タスクは〈何を / 期待する信号 / 手順リンク〉で自己完結。**手順の詳細**は [E2E_TEST_GUIDE.md](E2E_TEST_GUIDE.md)、**背景**は [ROADMAP.md](../ROADMAP.md) と各 docs。
> やったら **結果(スクショ＋Preflight ログの数値)を控えてエージェントへ共有**。済みは末尾「最近完了」へ移す。E2E はユーザーが GUI で実施（エージェントは実行不可）。最終更新: 2026-07-01。
>
> 凡例: 🔴 最優先 / 🟡 次 / ⚪ 余裕があれば

## 次にやること（優先順）

### 🔴 1. unkt × mini stack の下半身を切り分ける
- **何を**: 上半身は修正済（偽 GREEN 解消）。残り2点を目視判定。
  - **靴**: Shoes が RED（ThickGarmentInnerWall 38.5%）。**足を包む偽陽性か／実際に足が突き抜けているか**を見る。前者なら RED-skip が正解、後者は footwear の射程外。
  - **stocking**: YELLOW で **LOOSE**。コンポーネントの `Use Projected Solver` を ON にして Manual Bake し、緩みが取れるか coarse と比較。
- **手順**: [E2E_TEST_GUIDE.md](E2E_TEST_GUIDE.md) §4（Bake）・§4-D（ソルバ比較）・§8（合否は可視外側面で）。**背景**: ROADMAP「偽陽性の整理（厚物内側壁）」/ [DETECTION_SEMANTICS.md](DETECTION_SEMANTICS.md)。

### 🔴 2. 推奨 variant 起点で unkt をフィット
- **何を**: 採寸コーパスの最近傍 **`4.Manuka`（0.213）≒ `1.Shinano`（0.214）** から開始 → declip → 目視。**`Airi` は別ファミリー（最遠 0.459）なので避ける**。
- **期待**: 同ファミリー起点ゆえ貫通が浅く、きれいに収まる。
- **手順**: §2 セットアップ → §4 Manual Bake。**背景**: 採寸コーパス（[MEASUREMENT_SPEC.md](MEASUREMENT_SPEC.md) §5、`tools/cluster_measurements.py`）。

### 🟡 3. 同ファミリー force-apply 仮説の検証
- **何を**: **同ファミリー対 vs 跨ぎ対**で、Force Apply が conform（軽いリターゲ）か歪むかを見て、境界が「修正 vs 変換」でなく「**半径 / 接線**」かを確認。最も映えるのは YELLOW 帯。
- **手順**: [E2E_TEST_GUIDE.md](E2E_TEST_GUIDE.md) **§6.1**。**背景**: [ECOSYSTEM_VISION.md](ECOSYSTEM_VISION.md) §9 検証したい仮説。

### 🟡 4. フェーズ1 完了の n>1 目視クロステスト
- **何を**: 自動追従なし衣装 × 別体型（メッシュ焼き込み差）のクリーンなクロステストを**もう数例**。貫通解消を Before/After スクショで。
- **狙い**: プロジェクトの「動く」(earned 1.0) を n=1 から **n>1** へ（クリーンな例を増やすほど信頼が積む）。
- **手順**: §1.2 素材選び（自動追従の罠回避）→ §4-C。**背景**: [ROADMAP.md](../ROADMAP.md) フェーズ1「E2E 検証」。

### ⚪ 5. 採寸コーパスの残り素体
- **何を**: BASE 未特定の 6 素体（Hanna / INABA / KOSAME / Moe / MORPHO / YOLL）の素体 BASE プレファブを特定し採寸（無ければ記録）。コーパスを完成させてファミリー地図を埋める。
- **手順**: ヘッドレス `VRClothMeasureCli`（[MEASUREMENT_SPEC.md](MEASUREMENT_SPEC.md) §3）→ `tools/measurements_to_sqlite.py` → `tools/cluster_measurements.py`。

### ⚪ 6. 偽 GREEN ガードの負例確認
- **何を**: わざと `Body Mesh` 欄に髪メッシュを指定 → **低カバレッジ警告（⚠ Body coverage low）が出るか**。出れば偽 GREEN 自己検知が機能。
- **手順**: §3.1 ヘッドレス or インスペクタ `Run Preflight (Diagnostics)`。**背景**: `BodyModelConfidence`（[DIAGNOSTIC_HONESTY.md](DIAGNOSTIC_HONESTY.md) §1）。

### ⚪ 7. 標準計測点（バスト/ウエスト/ヒップ）の較正
- **何を**: `Dump Body Measurement (採寸)` を実素体で実行し、jsonl の `girth`（`bust_girth_m` / `waist_girth_m` / `hips_girth_m` ＋ `*_axisT`）を**人体感覚と突き合わせる**。配線は landed・しきい値は**未較正の暫定既定**（`VRClothGirthMeasure`: band 数 24 / prominence 0.03m / 軸上端 15% 延長）。
- **期待する信号**: ① `girth.measured=true` で **3 点とも検出**（`extremaCount≥3`）。② 軸位置が **hips < waist < bust**（`*_axisT` が下→上）。③ **waist が bust/hips より細い**。④ 周径の絶対値が見た目と整合（極端な過大/過小がない）。**ズレの兆候**: バストが肩/腕を拾って過大（軸上端の延長過多）、極小が腰でなく股で出る（軸下端=Hips ボーン位置のズレ）、デフォルメ体で複数ピーク→単一誤検出。
- **較正のつまみ**: ズレ方に応じて `VRClothGirthMeasure` の `TopExtendFraction`（バスト過大なら下げる）/ `MinProminenceM`（ノイズ拾いなら上げる）/ `BandCount`。**shapekey 0 固定**で測る（バストが胸 SK で動く、[MEASUREMENT_SPEC.md](MEASUREMENT_SPEC.md) §6）。
- **手順**: §2 セットアップ → インスペクタ `Dump Body Measurement (採寸)` → プロジェクト直下 `vrcloth-body-measurements.jsonl` の最終行 `girth` を確認（複数素体なら `tools/sizing_table_export.py` で `points` 整形ビュー）。**背景**: [MEASUREMENT_SPEC.md](MEASUREMENT_SPEC.md) §2「計測点の標準化」/ §8（周径の取り方）。**残**: 四肢（上腕/前腕/太もも/脹脛）の定位置サンプルは未実装。

## 最近完了（参考・直近の流れが分かるよう数件だけ）

- **2026-06-24** unkt × mini stack: 分割素体の全パーツ合算で**偽 GREEN 解消・上半身の貫通修正**を目視確認（A 修正 `d140952`。衝突体 17 メッシュ・採寸 15/15。earned 1.0 の観測1点目）。
- **2026-06-24** 採寸コーパス 16 素体をヘッドレス測定 → SQLite → クラスタ。unkt 最近傍 = Manuka ≒ Shinano、Airi は別ファミリー。

---

*このファイルは E2E の「次にやること」の単一の真実。タスクの背景・手順は各リンク先が持ち、ここは「何を・なぜ今・どこを見る」だけを最新に保つ。*
