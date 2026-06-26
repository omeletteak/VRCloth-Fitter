# 採寸仕様 (MEASUREMENT_SPEC)

このドキュメントは**構想資料**です。[ECOSYSTEM_VISION.md](ECOSYSTEM_VISION.md) §5(採寸表)・§6(Fit Report)で「分離予定」とした採寸層を、実装に近づいた分だけ具体化します。実装を拘束するのは [DESIGN.md](DESIGN.md) と [ROADMAP.md](../ROADMAP.md) のみ。関連: [FAMILY_MODEL.md](FAMILY_MODEL.md)(採寸空間の幾何・§7 最近傍・§8 差分診断)、[DIAGNOSTIC_HONESTY.md](DIAGNOSTIC_HONESTY.md)(計測の誤差と正直さ)、[INFORMATION_ARCHITECTURE.md](INFORMATION_ARCHITECTURE.md)(何を運ぶか)。最終更新: 2026-06-24。

## 0. 一行で

**素体・衣装を「同じ物差し(カプセル軸ごとの周径スカラー)」で測り、生は jsonl(No Cache)、束ねは派生 SQLite、固定はメッシュハッシュ＋計測条件のスナップショット。** 形状そのものはどこにも動かさない。

## 1. 位置づけ

ECOSYSTEM_VISION の三層「予測・保証・吸収」のうち**予測層**を具体化したもの。採寸表が「どのサイズ/素体が合うか」を購入前・着用前に予測し、診断(プリフライト)が保証し、修正(貫通ソルバ)が残差を吸収する。採寸は [FAMILY_MODEL.md](FAMILY_MODEL.md) §7「最も近い代表アバターはどれか」を機械的に答える材料でもある。

権利・プライバシー安全性: 採寸が出すのは**周径スカラー十数個＋一方向ハッシュ**だけで、そこから素体形状は復元できない(現実のアバター商品ページが身長を載せるのと同列の粒度、[INFORMATION_ARCHITECTURE.md](INFORMATION_ARCHITECTURE.md))。No Cache 原則と両立する。

## 2. 標準計測点

計測点は**プロキシカプセル**(`VRClothProxyGenerator` が Humanoid ボーンから生成する骨格セグメント列: Hips→Spine, Spine→Chest, …, UpperLeg→LowerLeg, … 最大19・実体は素体のボーン構成で15前後)で定義する。各計測点の値は:

- **半径 `radius_m`** — そのセグメント軸の周りの素体表面の代表距離(`VRClothBodyRadiusEstimator` が最近接軸割り当て＋パーセンタイルで推定)。**周径 ≈ 2π·radius**(円断面近似)。
- 補助: セグメント長 `length_m`、寄与頂点数 `sampleCount`、推定できたか `estimated`(できなければフォールバック半径)。

**[留保]** 現在の値は円断面近似の半径。真の周径(軸に垂直なスライスの凸包周長、ECOSYSTEM_VISION §5)は楕円断面の胴などで近似誤差を持つ。スライス周長への置き換えは精度向上の余地(本書 §8)。

### 計測点の標準化(目標仕様, 2026-06-26)

現状の計測点は**ボーンセグメント**(Hips→Spine 等)で幾何的に定義され、解剖学的計測点(バスト・ウエスト・ヒップ)と 1 対 1 ではない。標準化の目標は、現実の人体計測に合わせて計測点を定義し直すこと(ユーザー設計):

- **バスト・ヒップ = 軸方向の周径プロファイルが極大になる点** — 胸・尻の出っ張りを、ボーン区間の代表値でなく**断面輪郭が外へ膨らんで戻る点(接線の傾きが反転する極値)**で取る。胴帯(Hips〜Chest)を軸方向にスライスし周径(§8 のスライス凸包周長)をプロファイル化して極大を検出。
- **ウエスト = 同帯の周径極小**(ECOSYSTEM_VISION §5「ウエスト=最小周径」と一致)。
- **他(首・上腕・前腕・太もも・脹脛 等) = 人体計測の標準点** — ボーン位置・比率ベース(例: 太もも = UpperLeg 付け根近傍、上腕 = UpperArm 中点)。出っ張りでないので極値でなく定位置サンプルで十分。

**実装**: §8 のスライス周径プロファイルを計算し、**胴帯のみ極大(バスト/ヒップ)・極小(ウエスト)を検出、四肢は定位置サンプル**。現状のセグメント percentile を置換/補完する `VRClothBodyRadiusEstimator` 拡張。**計測条件**: バスト/ヒップの極大はシェイプキーで動くので shapekey 0 を固定(§6)。**留保**: アバターはデフォルメされる(極値が人体と違う位置・複数ピークになりうる)ので、プロファイルを平滑化してロバストに極値検出する(単純な argmax は誤検出)。出力は計測点名(bust/waist/hips/thigh/…)を持つ採寸表 `vrcloth-sizing-table/1`(機械可読・エージェント照合用、§5 の生 jsonl とは別の整形ビュー)。

## 3. アバター採寸(body) [一部 landed]

**手法**: Humanoid からカプセル生成 → **全ボディパーツを合算**して半径推定(分割素体=YM Body 等で体が複数メッシュに割れていても、髪だけを掴む偽 GREEN を避ける。[DIAGNOSTIC_HONESTY.md](DIAGNOSTIC_HONESTY.md) §1、`BodyModelConfidence`)。頭身(scale 不変の体型ファミリー記述子、FAMILY_MODEL §2)も併記。読むのはメモリ内の頂点、出すのはスカラーのみ。

**ツール(landed)**:
- インスペクタ **Dump Body Measurement (採寸)** ボタン(`VRClothMeasurementDump`) — 対象アバター1行を `vrcloth-body-measurements.jsonl` に追記。
- ヘッドレス **`VRClothMeasureCli`**(`-executeMethod VRClothDeclipper.VRClothMeasureCli.Run`) — (a) `-vrclothScene/-vrclothSceneDir` でシーン内の全 fitter、(b) `-vrclothAvatar`(カンマ可)/`-vrclothAvatarDir` で prefab を一時シーンに instantiate して採寸→破棄。GUI 不要。`-vrclothOut`(既定上書き/`-vrclothAppend` 追記)。

**JSONL 1行スキーマ(schema `vrcloth-body-measurement/1`)**:
```
{ schema, timestamp, avatar, headCount_neckRef, headCount_headRef, height_m,
  bodyCoverage, capsuleCount,
  capsules:[ { label, radius_m, length_m, sampleCount, estimated }, … ] }
```

**計測条件の固定**(再現性のため、ECOSYSTEM_VISION §6/§10): margin 5mm・**デフォルトポーズ**・**シェイプキー 0**。条件を変えて測れば別スナップショット(本書 §6)。

## 4. 衣装採寸(仕上がり寸法) [構想]

衣装には骨格=計測の基準が無い…が、**衣装はアバター骨にスキンされたメッシュ**である。ゆえに:

> **同じカプセルプロキシを生成し、半径推定を「素体メッシュ」でなく「衣装メッシュ」に向ければ、衣装の内周(仕上がり寸法)が body と同一の物差しで取れる。**

`VRClothBodyRadiusEstimator` の双子(対象レンダラを差し替えるだけ)。これが ECOSYSTEM_VISION §5「衣装側=仕上がり寸法(衣装内周)」の実体。**要件**: 衣装が骨格付き(着用状態、または骨を持つ skinned garment)であること。

**ツール(landed 2026-06-24)**: インスペクタ「Dump Garment Measurement (衣装採寸)」＋ヘッドレス `VRClothMeasureCli -vrclothGarment <衣装> -vrclothOnAvatar <素体>`(衣装と素体を同位置 instantiate し、素体の Humanoid から生成したカプセルで衣装メッシュを測る)。

**[要注意・位置合わせの保留]** 衣装は**独自 Armature**(設計素体の骨格)を持つが Humanoid Animator は無いので、カプセルは別の素体 prefab から取る。**衣装 Armature と素体骨格の bind pose が一致して初めて正しく測れる**。co-locate だけでは MOD 素体等で**ずれる**: 実測で Manuka_1(mini stack 4.Manuka)を MOD Manuka 上で測ると、**マッチング行列(§7)で設計素体 Manuka が最良フィットにならず(18.7mm 貫通・5位)** = ずれの兆候。**本来は MA Setup Outfit/Merge で衣装ボーンを素体へ統合してから測る**(または衣装の設計素体そのもの上で測る)。MA 着付けのヘッドレス化([[e2e-motivation-and-cadence]] の ProcessAvatar)は未実装=本項の保留。**サニティ基準=設計素体が最良フィット**になること。
**[留保]** 着衣プレファブ(素体+衣装が一体)を素体として測ると衣装が body に混入する(逆も然り)ので、measure 対象レンダラの選別が要る。

## 5. 採寸コーパスと SQLite 分析層

OMs 等の本番プロジェクトの全アバター・全衣装を測れば、ECOSYSTEM_VISION の**採寸コーパス**になり、[FAMILY_MODEL.md](FAMILY_MODEL.md) §7 のファミリークラスタリングと §9 の体型同質性仮説を実データで検証できる。

**パイプライン(確定方針)**:
```
Unity CLI ──→ jsonl(生・No Cache・依存ゼロ・人が読める)
                 │  python sqlite3 (stdlib) で取り込み
                 ▼
              SQLite(派生・再生成可・gitignore・ローカルの分析 DB)
```
- **SQLite 書き込みを Unity ツールに入れない**(`Mono.Data.Sqlite` 依存を抱えない)。生 jsonl が真実、DB はいつでも作り直せる分析キャッシュ。
- SQLite を選ぶ理由は**データ形状**: 採寸結果は構造化・多数行・成長・関係的で、最近傍/クラスタ/(avatar×garment)行列を **SQL の宣言的クエリ・JOIN** で引ける。散文(メモリ)はファイル+git、表形式で問い合わせるものは SQLite ― 道具をデータ形状に合わせる。

**エンジン選定(2026-06-24 整理)**: 規模は小(素体/衣装が数百〜千、行列でも数千〜万＝メモリに乗る)・分析的・単一ユーザー/ローカル・**生 jsonl から再生成可能な派生層**。重要前提として**「適切な出力」の数学(距離・クラスタリング)はどのエンジンでもアプリ側(numpy/scikit-learn)に載る**ので、エンジンの仕事は「コーパスを保持し絞り込み/JOIN した断面を分析コードに渡す」だけ＝選定の重みは小さい。
- **SQLite=妥当な既定**(実装済、`tools/measurements_to_sqlite.py`)。
- **DuckDB=唯一本気で比べる価値のある代替**: 埋め込み・ゼロ運用は同じだが**列指向・分析特化**で jsonl/Parquet を直読みでき pandas/Arrow 統合が強い。分析(クラスタ・行列・集計)が深まるほど効く。乗り換えても**生データも Unity ツールも触らない**(jsonl から作り直すだけ)＝ロックイン無し。
- **時期尚早(避ける)**: 専用ベクトル DB(Chroma/Qdrant/Milvus/FAISS=百万件・高次元向け、15 次元・数千件にはオーバーキル)、NoSQL/KV/グラフ DB(形が合わない、有向グラフ §7 も行列から導ける)。最小規律で「アクセスパターンに足る最簡」を選ぶ。
- **較正データ**(しきい値版・計測条件・f パラメータ)は [CORRECTABILITY_FIELD.md](CORRECTABILITY_FIELD.md) の非定常=溜めず較正ゆえ「現行＋版履歴」の小構成。専用エンジン不要、同 DB の versions テーブルか JSON で足る。

**同一性シソーラス(`data/identity.json`, v1 landed 2026-06-24)**: 採寸はプレファブ名(`MANUKA_lilToon_base_decimated` 等)で出るので、**canonical 名 ↔ 別名(alias)＋関係(creator / base素体 / family / mini-stack)** の人手キュレーション JSON で正規化する。名前と関係のみ＝公開メタで**形状を含まない(No Cache)**。`tools/identity.py`(stdlib のみ)が解決し、クラスタ等のツールが canonical 名で表示・dedup する。**経験的レイヤ(cluster=形状, matrix=フィット)が乗る同一性レイヤ**。`creator` を埋めると **「同作者≒同ファミリー」仮説(ECOSYSTEM_VISION §9)をファミリーごとの creator まとめで実データ確認**できる(シソーラス=既知の同一性 と cluster=経験的形状 は相補で、互いを検証できる)。SKOS/RDF 等の形式オントロジーは規模的に過剰=alias マップ＋数フィールドの軽量版で、育ったら broader/narrower を足す。

**スキーマ素描**:
```
avatars(id, name, head_count, height_m, body_coverage, mesh_hash, conditions_json, measured_at)
capsule_measurements(avatar_id, label, radius_m, length_m, sample_count, estimated)
garments(id, name, mesh_hash, conditions_json, measured_at)         -- §4 実装後
garment_measurements(garment_id, label, radius_m, …)                 -- §4 実装後
fits(avatar_id, garment_id, verdict, penetrating, clearance_p95, …)  -- §7 マッチング行列
```

## 6. 版管理: メッシュハッシュ＋条件スナップショット

ECOSYSTEM_VISION §6 の identity。各採寸行に**何を測ったか**を固定する。

**ハッシュの役割を分ける(混ぜない)**:
- **(A) 来歴・版固定・陳腐化検知 = 堅い** — 資産が変わればハッシュが変わる→再採寸が要ると分かる。両資産を持つ者は再採寸して数値を検証できる(偽装は自滅)。
- **(B) ユーザー間で「同じアバターだ」をハッシュ一致で判定 = 脆い** — import 設定・MOD・浮動小数差で、同じアバターでもハッシュは一致しない。**ユーザー間の体型比較は採寸値(周径スカラー)でやる**(改変版もバニラに近い数値=同ファミリー)。
- 原則: **ハッシュ=「全く同じ資産か」、採寸値=「似た体か」。**

**ハッシュの作り方(正規化が肝)**: 頂点量子化(例 0.1mm)・bind/T-pose・シェイプキー 0・安定した頂点順・スケール正規化。対象=ベース頂点+トポロジ+ボーン名(+任意で blendshape フレーム差分)。**SHA-256 一方向**ゆえ形状を漏らさない(指紋であって形状ではない)。

**スナップショット = ハッシュ単体でなく、再現性キー**:
```
{ meshHash, shapekeyValues, pose, scaleNorm, toolVersion, thresholdVersion, margin }
```
**同ハッシュ＋同条件 → 同じ採寸値が再現する。** シェイプキーを動かして測れば別スナップショット(その値も記録)。

**v1 実装(2026-06-24)**: `MeshFingerprint`(Core, テスト付) ＝ 量子化頂点(0.1mm)＋トポロジの SHA-256。ボディは全パーツのハッシュを**順序非依存で結合**(`Combine`)。採寸ダンプが **bind-pose の `sharedMesh`** から `meshHash` を計算(scene の pose/scale に不変)＋`conditions.radiusPercentile` を記録(schema `/2`)。

**保留(判断を迷う点はメモして先送り、本書で明示)**:
- **条件の完全化**: pose・**シェイプキー状態**・scale・tool/threshold バージョンは未記録。現状「shapekey 0 で測る」前提だが**強制していない** → 非0で測ると採寸値は変わるのにハッシュ(base mesh)は同じになる不整合。要・条件にシェイプキー状態を載せる(or 計測時に 0 を強制)。
- **blendshape フレームをハッシュに含めるか**(資産同一性 vs 採寸再現性のどちらを優先するか)。
- **scale 正規化**: 現状 `sharedMesh` で scene scale には不変だが import scale には敏感(§6(B) の脆さの一部)。
- **クロスプラットフォーム determinism**(`BinaryWriter` のバイト順)。

これらは [ROADMAP.md](../ROADMAP.md) と開発メモに保留として残し、必要が出た時点で決める。

## 7. マッチング行列(模擬器)

body 採寸表(§3)× 衣装採寸表(§4)が揃えば、(avatar × garment) のマッチング行列を作れる。各セルは「採寸照合による予測(どのサイズが合うか)」と、実着用での「プリフライト診断による保証(緑/黄/赤＋貫通＋クリアランス、FAMILY_MODEL §8 差分診断)」。これが ECOSYSTEM_VISION の**ショップ↔ユーザーのシミュレーション**を本番プロジェクト内で実体化したもの。クリアランス(LOOSE 側)の両側化は [ROADMAP.md](../ROADMAP.md) フェーズ5「クリアランス統計」。

**v1 実装(2026-06-24, `tools/matching_matrix.py`)**: body/garment 採寸 jsonl から各 (素体×衣装) のクリアランスを計算する**予測層の本体（採寸照合 = 着用せず予測）**。
- **モデル**: 衣装 G(設計素体 A 上で測定、内周 gi・長 gl)を素体 B(半径 bi・長 bl)に着せると内周を B の骨長へ等方スケール: `scaled_inner = gi·(bl/gl)`、`clearance = scaled_inner − bi`(負 = 貫通予測)。⇔ scale 不変の比率比較 `gi/gl < bi/bl`。衣装がまたぐカプセルのみ。集計(maxPen / penCaps / minClear)→ **GREEN / YELLOW / RED / LOOSE**。
- **形状距離(対称)と違い方向性を捉える**(FAMILY_MODEL §6 有向グラフ): 合成デモ「Manuka 用トップス」は**小さい unkt には緩く fit(GREEN, +17mm)、大きい Shinano には貫通(YELLOW 16mm)/KUMALY(RED 40mm)**。「A→B 可 ≠ B→A 可」を再現。
- **前提と限界**: MA がスケール調整しない運用では内周は authored のままで本モデルは近似(§9 境界の問題)。**これは予測であって判定でない**(FAMILY_MODEL §9「ラベルは予測、診断が判定」)=最終は保証層(プリフライト)で。
- **実データ投入(2026-06-24)**: 衣装 CLI mode で実 Manuka_1(mini stack 4.Manuka, 15/15 coverage)を採寸し行列に投入 = **行列が実データで動くことを確認**。ただし**サニティ未通過**: 設計素体 Manuka が最良フィットにならず(YELLOW 18.7mm・5位)= §4 の位置合わせ保留。**行列の計算機構は正しいが、衣装採寸の位置合わせ(MA Merge)を解くまで予測値は信頼しない**(最遠の Airi/Kikyo=高頭身, Milk-Re=低身長 は妥当に外れているので、粗い傾向は出ている)。
- **残(follow-up)**: 衣装採寸の**位置合わせ**(§4 保留 = MA Setup Outfit のヘッドレス化, [[e2e-motivation-and-cadence]] ProcessAvatar)、SQLite の `fits` テーブル、保証層(実プリフライト)との突き合わせ、matching への identity 統合。

## 8. 正直な留保

- **頭身は入口の予測であって門番ではない**(FAMILY_MODEL §9)。Neck/Head 基準でぶれ、topY が髪を含む等の誤差(ROADMAP「頭身測定の精度向上」)。最終判定は形状ベクトル(radii)/差分診断。
- **半径=円断面近似**。真の周径(スライス周長)への置き換えは §2 の精度余地。
- **着衣プレファブの混入**(§4)。素体採寸は素体メッシュ、衣装採寸は衣装メッシュに対象を絞る。
- **スケール**は VRChat で任意(FAMILY_MODEL §2)。ファミリー判定は scale 不変量(頭身・正規化 radii)で、絶対 radii は条件として記録。

## 9. 実装状況と順番

- **landed**: アバター body 採寸(分割素体合算・偽 GREEN ガード・dump ボタン・ヘッドレス CLI(a)(b))。実証: unkt × 所持素体4体を OMs でヘッドレス採寸し最近傍=Shinano を算出。
- **次**: ① アバター全素体 BASE を採寸 → SQLite 取込 → クラスタ分析 → ② 衣装内周 measure(§4)→ ③ マッチング行列(§7)。各採寸行に `meshHash`＋条件(§6)を刻む。
- FIT_REPORT_FORMAT(プリフライト判定込みの完全な Fit Report スキーマ)は別途分離予定。本書は採寸(予測層)の範囲。
