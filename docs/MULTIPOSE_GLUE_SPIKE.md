# 代表ポーズ glue スパイク — Composer 核の実地検証とビルド時ポーズ駆動

**状態: 段階1 landed**(エディタ glue + 実 SkinnedMeshRenderer の数値検証テスト、2026-06-21)/ **段階2 未着手**(ビルド時ポーズ駆動 Bake)

## 目的

代表ポーズ対応の核 [MultiPoseComposer](../Assets/VRCloth-Declipper/Core/MultiPoseComposer.cs)(landed)を実アバターへ繋ぐ glue を建てる前に、2つの未知を使い捨て実験で潰す。文章で詰めずプローブで詰めるのは [NDMF_INTEGRATION_SPIKE.md](NDMF_INTEGRATION_SPIKE.md) と同じ流儀(BakeMesh×HumanPose の実挙動は経験的)。

1. **Composer 核が実データで数値妥当か** — 実衣装メッシュ・実スキン行列・実 collider で掃引合成が収束し、デルタ1枚が全ポーズを clear するか(エディタ、安く取れる)
2. **ビルド時にアバターをポーズ駆動して Bake できるか** — NDMF ビルドの T-pose 固定下で、各代表ポーズへ駆動→Bake→戻すが安全か(ビルド、固有の未知)

背景と設計分岐は [MULTIPOSE_COMPOSITION.md](MULTIPOSE_COMPOSITION.md) §7。

## なぜ2段に割るか

- **パス順は決着済み・ポーズ駆動は別問題**: [NDMF_INTEGRATION_SPIKE.md](NDMF_INTEGRATION_SPIKE.md) が `AfterPlugin(MA)` の安定スロットを確定したが、それは「何も変形しないダンプ」。ビルド中のポーズ駆動 Bake はパス順とは独立した未検証領域。
- **Composer 検証はビルド内外に依存しない**: 線形スキニングゆえ([MULTIPOSE_COMPOSITION.md](MULTIPOSE_COMPOSITION.md) §1)、合成ロジックの数値妥当性はマージ前後・ビルド内外で変わらない。先にエディタで安く確かめ、ビルド固有のリスクだけを段階2へ隔離する。
- 段階1が緑にならなければ段階2は無意味(順序依存)。

## 段階1 — エディタでの Composer 核実地検証

現 Run Fitting(単一ポーズ)を**複数ポーズへ拡張する使い捨て glue**をエディタで書き、Composer がプレースホルダ三角形でなく実メッシュで動くことを確かめる。マージ前形状でも合成ロジックの数値検証は成立する(線形性)。

### 実装(2026-06-21 landed)

- **glue 本体** [VRClothMultiPoseSpike](../Assets/VRCloth-Declipper/Editor/VRClothMultiPoseSpike.cs) — インスペクタの **Experimental ▸ Run Multi-Pose Fit (Spike)** ボタンで起動。`DefaultPoses()`(叩き台 = A ポーズ / 肘 ~75° / 座り / 股関節屈曲)を `HumanPoseHandler` の muscle 値で順に駆動し、各ポーズで全レンダラーを Bake → `PoseCapture`(originalWorld + 頂点別 `BlendedSkinMatrix` + ポーズ姿勢の collider)を構築。`MultiPoseComposer.Compose` で bind-local デルタ1枚に束ね、[VRClothMeshApplier.ApplyBindLocalDelta](../Assets/VRCloth-Declipper/Editor/VRClothMeshApplier.cs)(`source.vertices + delta`。線形性ゆえデルタはメッシュローカル空間で直接加算でき、増分なので二重適用も起きない)で非破壊適用。最後に各ポーズを再駆動・再 Bake して残留貫通数と maxDelta をログ出力。
- **適用 API** `ApplyBindLocalDelta` を Applier に追加(置換でなく base 頂点へ加算、Undo 対応)。
- **数値検証(コードで詰めた分)** [MultiPoseRoundTripTests](../Assets/VRCloth-Declipper/Tests/Editor/MultiPoseRoundTripTests.cs) — 回転+スケール付きの実 `SkinnedMeshRenderer` で、3 ポーズ捕捉 → `Compose` → `ApplyBindLocalDelta` → 各ポーズ再 Bake で**全ポーズ非貫通**を固定(EditMode 114 緑)。「捕捉 skin 行列が再スキンと合うか / Compose が収束するか / 適用が二重適用しないか」の3失敗モードをこのテストが押さえる。
- **残り(人手ゲート)** — 実アバター×実衣装での目視較正。muscle 値は経験的叩き台で、ポーズの見た目と過膨張シルエットは目視で詰める([E2E_TEST_GUIDE.md](E2E_TEST_GUIDE.md) §7.1)。マージ前形状ゆえ衣装が素体ボーンに追従する素材が前提(§7 の落とし穴)。

### 確かめる点
- 各代表ポーズへ `HumanPoseHandler`(muscle 値)で駆動し、各ポーズで Bake した `originalWorld`・頂点別 skin 行列([SkinningMath.BlendedSkinMatrix](../Assets/VRCloth-Declipper/Core/SkinningMath.cs))・body collider が正しく捕捉できるか
- `MultiPoseComposer.Compose` が `remainingPenetrating = 0` へ収束し、過大デルタを出さないか(`maxRounds` の妥当性)
- 適用した1枚デルタを**独立に再 Bake**して各ポーズへ再スキンしたとき、Preflight 残留が単一ポーズ Run と同等まで下がるか(Composer 内部チェックでなく外から検証)

### 手順
1. ペア用意: Blendshape Sync 無しの衣装 × アバター(ポーズ効果と Sync 追従を切り分け。[E2E_TEST_GUIDE.md](E2E_TEST_GUIDE.md) §0.1 の落とし穴)。現状の手動クロステスト素材を流用。
2. 使い捨て glue: 代表ポーズ集合(叩き台 = A ポーズ腕下ろし / 肘 ~75° / 膝 ~90° 座り / 股関節屈曲)へ順に駆動。
3. 各ポーズで Bake → `PoseCapture`(originalWorld + 頂点別 skin 行列 + collider)を構築。
4. `Compose` 呼び出し → `bindLocalDelta` 取得。
5. デルタを衣装メッシュ複製へ適用(既存 [VRClothMeshApplier](../Assets/VRCloth-Declipper/Editor/VRClothMeshApplier.cs) + [SkinningMath.WorldDeltaToMeshLocal](../Assets/VRCloth-Declipper/Core/SkinningMath.cs) 流用、置換でなく加算)。
6. 各ポーズへ再駆動 → 再 Bake → Preflight で残留検証(計算合否)+ 可視面を目視(効果側)。
7. 静止ポーズで standoff/膨らみを目視(過膨張コスト側、[MULTIPOSE_COMPOSITION.md](MULTIPOSE_COMPOSITION.md) §3)。

### 成功・失敗基準
- **成功**: 各代表ポーズで残留が単一 Run と同等、静止の過膨張が許容シルエット内。デルタ1枚で全ポーズ clear。
- **失敗(捕まえたい)**: (a) 捕捉 skin 行列が再スキンと合わない(BlendedSkinMatrix の取り違え) / (b) Compose が収束しない・過大デルタ(`maxRounds` 不足 or ロジック不整合) / (c) 適用が二重適用を起こす(WorldDeltaToMeshLocal の前提崩れ)。

## 段階2 — ビルド時ポーズ駆動 Bake のスパイク

### なぜ要るか
[NDMF_INTEGRATION_SPIKE.md](NDMF_INTEGRATION_SPIKE.md) §② が確定した通り、**ビルド時のアバターは T-pose・ブレンドシェイプ既定値**。代表ポーズ対応は各ポーズへ駆動して Bake が必須なので、ビルド中に一時的にアバターを曲げ、デルタを得て T-pose へ戻す状態操作が要る。これがビルドパイプラインで安全かは [NDMF_INTEGRATION_SPIKE.md](NDMF_INTEGRATION_SPIKE.md)(変形しないダンプ)が踏んでいない。

### 確かめる点
- (a) **駆動**: `Transforming.AfterPlugin(MA)` スロットで、ビルド中の(マージ済み)アバター階層を `HumanPoseHandler` / Transform 回転で曲げられるか。Humanoid muscle 空間がビルド時アバターで引けるか。
- (b) **捕捉**: 曲げた状態で `BakeMesh` し、衣装+素体の頂点・skin 行列を段階1と同じく捕捉できるか。
- (c) **復元**: T-pose へ戻し、デルタ適用済みメッシュが最終ビルド(AAO 後)に乗るか。ポーズ駆動の痕跡が残らないか。
- (d) **協調**: 自パス内で Transform を一時的に動かすことが、同フェーズ他パス・後段 AAO の前提を壊さないか。ビルド後にアバターが T-pose で確定しているか。

### プローブ/手順
[NDMF_INTEGRATION_SPIKE.md](NDMF_INTEGRATION_SPIKE.md) と同じく**テストプロジェクト側**(`Assets/VRClothSpike/`、NDMF 依存をパッケージへ持ち込まない)に使い捨てパスを置く。
1. `Transforming.AfterPlugin("nadena.dev.modular-avatar")` で走る使い捨てパス。
2. パス内で: 1つの代表ポーズへ駆動 → `BakeMesh` で衣装+素体をダンプ(頂点数・bounds・skin 行列サンプル)→ T-pose へ戻す。
3. ダンプを project ルートの `vrcloth-posedrive-probe.json` へ書く(Console コピペでなく JSON 直読、[NDMF_INTEGRATION_SPIKE.md](NDMF_INTEGRATION_SPIKE.md) と同方針)。
4. **Manual bake**(アップロード不要)で実走。JSON を読んで (a)-(d) を判定。
5. ビルド後の最終アバターが T-pose で無傷か(ポーズ駆動が漏れていないか)を確認。スパイク後にパスは削除。

### 成功・失敗基準
- **成功**: ビルド中にポーズ駆動 Bake ができ、戻した後の最終アバターが無傷。捕捉数値が段階1のエディタ捕捉と整合。
- **失敗(捕まえたい)**: (a) ビルド時アバターで HumanPose が引けない(Animator 無効 / muscle 空間不在) / (b) Bake がビルドコンテキストで失敗 or 別状態を返す / (c) ポーズ駆動の痕跡が最終ビルドに残る / (d) 他パスが壊れる・ビルドが落ちる。

## 段階の依存と中止・退避

- 段階1が赤 → Composer か glue 配線のバグ。段階2へ進まない。
- 段階2が赤(ビルド中ポーズ駆動が不可/不安定)→ **設計退避が要る**。本命 glue を「自動 NDMF パス」から「**半手動エディタワークフロー**」へ後退させる選択肢(ユーザーがマージ相当の状態をエディタで用意 → エディタで代表ポーズ較正 → デルタをシーン内メッシュ複製へ非破壊適用)。ただしエディタはマージ前形状なので「マージ後形状をエディタでどう得るか」が次の問いになる。退避はプロダクト形態の判断であり、No Cache(デルタをアセット化しない)は退避先でも保つ。この分岐自体を本ドキュメントが記録する。

---

*Composer 核は landed。実アバターへの glue 着手前に、(1) 数値妥当性をエディタで安く、(2) ビルド時ポーズ駆動という固有リスクを使い捨てプローブで、の2段で前提を固める。パス順は決着済み、残る未知はビルド中のポーズ駆動 Bake。*
