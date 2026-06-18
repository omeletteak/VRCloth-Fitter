# 多ポーズ補正の合成 — 設計と最小実装

このドキュメントは**検討資料 兼 実装記録**です。フェーズ3「代表ポーズ対応」(本命)の核心 ― **複数の代表ポーズで得た補正を、1枚の静的メッシュ編集に束ねる「合成」** ― の設計と、その純 Core 実装を記録します。背景と位置づけは [DEFORMATION_METHODS.md](DEFORMATION_METHODS.md) §6・[JOINT_MARGIN.md](JOINT_MARGIN.md) §7、書き戻し(デルタ)機構は [DESIGN.md](DESIGN.md) §6 と [SkinningMath](../Assets/VRCloth-Declipper/Core/SkinningMath.cs)。最終更新: 2026-06-18。

## 0. 一行まとめ

関節貫通は「曲げたときだけ」起きるが、書き戻せるメッシュ編集は**静止ポーズの bind 空間に1枚**しか無い。だから N ポーズの補正を合成する必要がある。これは**過剰拘束**(1頂点3自由度で N 個の姿勢拘束を満たす)なので、独立に解いて平均するのではなく、**共有の蓄積デルタを携えてポーズを掃く**(Gauss-Seidel)。押し出しは常に外向き・単調なので、蓄積は全ポーズの最悪押し出しの和で上に有界。数掃きで各ポーズが解ける。Core 実装は `MultiPoseComposer`。

## 1. なぜ合成が要るのか(問題の核心)

- 関節貫通は **素体と衣装のウェイト塗りの差 × 関節回転角**で生じ、**曲げたポーズでしか現れない**([DEFORMATION_METHODS.md](DEFORMATION_METHODS.md) §6)。
- 一方、本ツールが書き戻せるのは **bind-pose ローカルのメッシュ頂点に乗る静的デルタ1枚**([SkinningMath.WorldDeltaToMeshLocal](../Assets/VRCloth-Declipper/Core/SkinningMath.cs))。実行時にどのポーズへスキニングされても、このデルタは骨に乗って一緒に動く。
- つまり「曲げたときだけ出る貫通」を「全ポーズで動く静的デルタ1枚」で吸収する必要がある。**これが合成問題**。

### 線形スキニングの効く性質(合成の土台)

線形ブレンドスキニング(LBS)は線形なので、bind-local デルタ `d` は各ポーズ Pi の世界へ厳密に再スキンされる:

```
worldPos_Pi[v] = skin_Pi[v] · (base[v] + blend_Pi[v] + d[v])
              = originalWorld_Pi[v] + skin_Pi[v] · d[v]
```

`originalWorld_Pi`(ポーズ Pi で Bake した補正前の世界座標)はブレンドシェイプ変位 `blend_Pi` を内包し、デルタ部分だけが `skin_Pi · d` として線形に乗る。**ブレンドシェイプは originalWorld 側に入り、デルタとは分離される**([SkinningMath](../Assets/VRCloth-Declipper/Core/SkinningMath.cs) が単一ポーズで二重適用を消すのと同じ原理)。この線形性が、bind 空間で1枚のデルタを扱える根拠。

## 2. 過剰拘束と、なぜ「平均」ではダメか

1頂点のデルタ `d[v]`(3自由度)が、N ポーズの「素体の外に出ている」拘束を**同時に**満たさねばならない。素朴な手は2つあるがどちらも不可:

- **各ポーズを独立に解いて平均/最大** — ポーズごとに最適な押し出し方向は skinning 後で異なるため、ベクトルの平均は方向を殺し、最大(大きさ)選択は片方のポーズで過押し/不足になる。互いに張り合う。
- **全拘束を一括の線形最適化** — 拘束は不等式(外側にいる)で非線形、かつ衝突は非凸。エディタツールに重すぎる([DEFORMATION_METHODS.md](DEFORMATION_METHODS.md) §4 の IPC/ARAP 隔離と同じ理由)。

## 3. 採用する合成モデル ― 蓄積デルタによるポーズ掃引

**独立に解かず、共有の蓄積デルタ `d` を携えてポーズを順に掃く**(各ポーズで現状の `d` を前提に追加押し出しだけを足す)。1掃き=全ポーズ1巡:

```
d ← 0
repeat (最大 maxRounds 回):
  for each pose Pi:
     world ← originalWorld_Pi + skin_Pi · d          # 現状デルタをこのポーズへ再スキン
     fitted ← PenetrationSolver.Solve(world, collider_Pi, margin)   # 既存ソルバを流用
     d[v] += skin_Pi[v]⁻¹ · (fitted[v] − world[v])   # 追加押し出しを bind 空間へ畳んで蓄積
  全ポーズが clear(追加押し出しゼロ)なら break
```

### なぜ収束し、なぜ有界か

- 押し出し([PenetrationPushOut](../Assets/VRCloth-Declipper/Core/PenetrationPushOut.cs))は**常に外向き(SDF 勾配方向)**にしか動かさない。各ポーズの寄与は外向き加算のみなので、蓄積は単調で、**全ポーズの最悪押し出しの和**で上に有界。
- これは**ポーズ拘束に対する Gauss-Seidel / 逐次投影**。あるポーズの押し出しが別ポーズの貫通を再生しうるので1掃きでは足りないが、掃きを重ねると各ポーズが順に満たされていく。全ポーズが clear になった掃きで `Solve` が即 return(hit ゼロ)し、追加ゼロ→収束。
- 既存の単一ポーズソルバ(検出→押し出し→平滑化の反復)を**そのまま各ポーズに使う**。新規なのは bind 空間で1枚のデルタを共有する外側の掃引だけ。

### コスト(過膨張)とその意味

合成デルタは「どれかのポーズが必要とした押し出しの和」なので、**静止ポーズでは、ある曲げポーズのために離した分だけ体から浮く**ことがある。これは「1枚の静的編集で全ポーズを満たす」ことの本質的コストで、回避不能。ただし [JOINT_MARGIN.md](JOINT_MARGIN.md) の可変マージンの「当て推量の standoff」と違い、**実際にあるポーズが必要とした分だけ**なので過不足が原理的に正当化される。ポーズ依存に動く補正が要るなら別機構(ポーズ別ブレンドシェイプ)になるが、それは本項の範囲外。

### No Cache との両立

ポーズ捕捉(originalWorld・skin 行列・collider)はすべてセッション内メモリで構築し捨てる。合成デルタもメッシュ複製に乗せるだけでアセット化しない([DESIGN.md](DESIGN.md) §5)。

## 4. 純 Core と エディタ glue の分離

合成は **Unity の再 Bake を介さず純粋な数学**で書ける(線形性, §1)。各ポーズを1回だけ捕捉すれば、掃引中は再 Bake 不要:

| 層 | 役割 | 状態 |
|---|---|---|
| **Core: `MultiPoseComposer`** | `PoseCapture`(originalWorld + 頂点別 skin 行列 + collider)の列から bind-local デルタを掃引合成。**純粋・ユニットテスト可** | **landed(2026-06-18)** |
| **Editor glue(未)** | アバターを各代表ポーズへ駆動(`HumanPoseHandler` か Transform 直接回転)→ Bake で originalWorld と skin 行列を捕捉 → そのポーズの body collider 構築 → Composer 呼び出し → デルタを1枚適用 | 未着手(経験的スパイク) |

`PoseCapture` の `skinMatrices` は [SkinningMath.BlendedSkinMatrix](../Assets/VRCloth-Declipper/Core/SkinningMath.cs) で各ポーズの `boneToWorld × bindPose` から作れる。glue は **BakeMesh × HumanPose × 逆スキニングの実挙動**が絡むため、文章でなく使い捨てプローブで詰めるのが筋([NDMF_INTEGRATION_SPIKE.md](NDMF_INTEGRATION_SPIKE.md) と同じ流儀)。

## 5. 最小実装の検証(landed)

[MultiPoseComposer](../Assets/VRCloth-Declipper/Core/MultiPoseComposer.cs) + [テスト](../Assets/VRCloth-Declipper/Tests/Editor/MultiPoseComposerTests.cs)。三角形を空にして平滑化を排除し、合成ロジックを純粋に検証:

- **単一ポーズ** — 貫通頂点が外へ、遠方頂点は不変
- **2ポーズ・近い体が支配** — 1枚のデルタが両ポーズを clear、サイズは厳しい方(近い体)に決まる
- **非単位スキン** — 90°回転スキンで、world 押し出しが bind 空間へ正しく逆変換され、再スキンで clear
- **無貫通** — デルタ0・1掃きで収束
- **null/空** — no-op

## 6. 残る判断(エディタ glue 着手前)

1. **ポーズ駆動**: `HumanPoseHandler`(muscle 値)か Transform 直接回転か ― BakeMesh との相性は経験的、スパイクで確認
2. **代表ポーズ集合**: どの曲げを標準にするか ― 較正(目視待ち)
3. **掃き回数 `maxRounds`**: 既定4。ポーズ数と難度で要調整(暫定)
4. **レンダラー間整合**: 複数 SkinnedMeshRenderer の重ね着([DEFORMATION_METHODS.md](DEFORMATION_METHODS.md) §3.4)はまず統合 SDF で。合成とは直交

Core(掃引合成)が確定したので、残るは glue とポーズ較正のみ。glue は経験的スパイクで進める。

---

*関節貫通は動的・編集は静的。その不整合を「過剰拘束を蓄積デルタの掃引で満たす」合成で橋渡しする。純 Core は landed・テスト済、残るは BakeMesh×HumanPose のエディタ glue(スパイク)とポーズ較正(目視)。*
