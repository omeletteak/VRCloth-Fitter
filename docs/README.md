# ドキュメントハブ

VRCloth-Fitter の設計・構想・手順ドキュメントの索引です。

**状態の凡例**
- **[決定]** — 実装を拘束する設計判断。変更には相応の理由が要る
- **[構想]** — 検討資料。採用が決まった部分は DESIGN.md に昇格する
- **[手順]** — 作業ガイド

## 1分で全体像

VRCloth-Fitter は「**着せた後に残る貫通の自動修正**」に特化したオープンソース(MIT)の Unity エディタ拡張です。体型変換(リターゲティング)は先行ツールの領分であり、本プロジェクトはその後段の空白を埋めます。アバター素体形状を復元しうるデータは保存も出力もしません(**No Cache**)。

戦略の背骨は三層です: **採寸表が予測し、診断が保証し、修正が残差を吸収する。** 形状データは動かさず、動かすのは復元できない判断材料(計測値・統計・判定)だけ — この情報設計が、権利・プライバシー安全性とエコシステム標準への道を同時に開きます。

## ドキュメント一覧

| ドキュメント | 状態 | 概要 |
|---|---|---|
| [DESIGN.md](DESIGN.md) | 決定 | 本体。目的(オープン実装の存在)、解く問題、競合分析、変換プロファイルのプライバシー所見、No Cache 原則、パイプライン構成、配布形態、凍結した構想、**サポート範囲の定量定義(§9: 緑/黄/赤)** |
| [ECOSYSTEM_VISION.md](ECOSYSTEM_VISION.md) | 構想 | プリフライト診断を個人ツールからエコシステムの互換性標準へ育てる構想。三層構造、買い手の3チャネル、ショップのサイズタイリング、採寸表、Fit Report、NDMF パス化、普及の階段 |
| [INFORMATION_ARCHITECTURE.md](INFORMATION_ARCHITECTURE.md) | 構想 | 何を運び、何を運ばないかの原則。変換型と判定型の情報要求量の違い、現実のアパレルの先例、サポート範囲(§9)と情報境界の同一性、スケール則 |
| [DEFORMATION_METHODS.md](DEFORMATION_METHODS.md) | 構想 | ソルバの発展候補比較(変位場の調和補間、XPBD、ARAP、RBF 空間場)と適用範囲の考え方 |
| [FAMILY_MODEL.md](FAMILY_MODEL.md) | 構想 | アバター・ファミリーと採寸空間の幾何。頭身=硬い軸/周径=柔らかい軸、ファミリー=空間の領域、差分診断=所属テスト、市場が作るクラスタ |
| [DIAGNOSTIC_HONESTY.md](DIAGNOSTIC_HONESTY.md) | 構想 | 保証層の false confidence リスク。包絡は場・計器誤差→信頼区間・しきい値とアバターの漂流→再実行可能性・静的と動的。Fit Report へ昇格すべき項目 |
| [E2E_TEST_GUIDE.md](E2E_TEST_GUIDE.md) | 手順 | 実機での目視テスト手順。ヌルテスト→クロス着せ替え→Undo 確認、Preflight ログの読み方、トラブルシュート |
| [NDMF_INTEGRATION_SPIKE.md](NDMF_INTEGRATION_SPIKE.md) | 手順 | NDMF パス化の前提検証スパイク。ビルド時に自パスを「骨格マージ後・メッシュ最適化前」へ安定して挿せるかを使い捨てプローブで確認する計画・手順・判定基準 |
| [VPM_PACKAGING.md](VPM_PACKAGING.md) | 手順 | フェーズ2 VPM 化の下調べ。現状監査、`IEditorOnly` 化の SDK 参照方針、Tests 同梱方針、vpm-listing 配布の仕組み、`package.json` 不足フィールド、未決事項と着手順 |

リポジトリ直下: [ROADMAP.md](../ROADMAP.md)(フェーズ別の実行計画)/ [CLAUDE.md](../CLAUDE.md)(AI エージェント向けの開発環境情報。自動テストの実行方法もここ)

## 読み順の推奨

- **はじめて**: 上の「1分で全体像」→ DESIGN.md §1〜§2・§9 → ROADMAP
- **設計に興味**: DESIGN.md 通読 → DEFORMATION_METHODS.md → FAMILY_MODEL.md → DIAGNOSTIC_HONESTY.md
- **構想に興味**: INFORMATION_ARCHITECTURE.md → ECOSYSTEM_VISION.md → FAMILY_MODEL.md
- **テストする**: E2E_TEST_GUIDE.md

## 予定(未作成)

実装が近づいた時点で ECOSYSTEM_VISION.md から分離する予定の仕様書:

- **MEASUREMENT_SPEC** — 標準計測点の正式定義(計測点の位置、スライス方法、丸め)
- **FIT_REPORT_FORMAT** — 機械可読フォーマット(JSON スキーマ、計測条件、バージョン刻印)の仕様
