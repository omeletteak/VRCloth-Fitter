# TODO — 実機 E2E 目視(Mini Stack 軸 / フェーズ1確定)

軸の衣装: **Mini Stack**(SEI10・無料・MA必須) https://booth.pm/ja/items/8414572
狙い: **対応リスト外(非対応)アバターにどこまで貫通修正で対応できるかのマップを作る** = 検証＋採用訴求。
最小経路は ★。詳細手順: docs/E2E_TEST_GUIDE.md

対応12体(=ファミリー内側): Shianao / Airi / Manuka / Chocolat / Chiffon / Plum / Milfy / Eku / Sio / Rurune / Mayo / Kumaly

## 準備(Unity を開く前でOK)
- [ ] Mini Stack 入手(無料・Booth)。スクショ公開するなら VN3 規約を先に確認
- [ ] 対応アバター1体(ヌルテスト用) … 上の対応12体から手持ちを1体
- [ ] 非対応アバターを2〜3体(クロステスト用) … 対応リスト**外**から、体型差が「近い→そこそこ→かなり違う」の順
- [ ] 各非対応で §1.2 自動追従テスト: 素体の胸/尻シェイプキーを動かして**衣装が追従しない(Sync不発)**ことを確認 → 追従するなら別アバターへ

## 段階1: ヌルテスト(対応アバター・マージ前)
- [ ] Setup Outfit → 衣装ルートに VRClothDeclipper → Preview Body Proxy でカプセルが体に沿うか目視
- [ ] 手順0(カプセル): GREEN 期待(胴・足は赤が既知)
- [ ] ★手順0b: **Use Mesh SDF Collider = ON** で再 Run → 胴・足が GREEN に近づくか。カプセル/SDF 両方の数値を控える

## 段階2: 非対応クロステスト(本命・Bake 後・段階的)
各非対応アバターで繰り返す:
- [ ] Setup Outfit → Manual Bake Avatar → 複製の衣装に VRClothDeclipper 付け直し → Mesh SDF ON → Run
- [ ] `M vertices still penetrating` の M=0
- [ ] 肌見せ境界×関節(襟元・胸元 / スカート裾⇔太もも / 肩)に素体が出てないか
- [ ] 胸/尻(Sync不発で追従しない)が衣装に収まるか / はみ出しが直るか
- [ ] 緑/黄/赤のどれに落ちたか + Preflight 数値を記録
- [ ] Before/After スクショ

## 記録 = 非対応対応マップ(採用訴求の素材)
- [ ] 「対応リスト外の ○○(体型タグ)まで 緑/黄 で着れる」を表に: アバター名 + 体型タグ + 判定 + 主要数値
- [ ] スカートは**静止で評価**(PhysBone の動的クリッピングはツール対象外)
- [ ] 「直せてほしいのに赤 / 無理筋なのに緑」は必ず数値を残す(§9 しきい値較正)

## 完了チェック(フェーズ1確定・すべて Bake 後)
- [ ] 対応アバターでヌルテスト GREEN(メッシュSDF)
- [ ] 非対応アバター少なくとも1体で貫通解消 + Before/After スクショ
- [ ] 新破綻(膨らみ・伸び・潰れ)なし / Undo で戻る / メッシュSDF 所要時間メモ
