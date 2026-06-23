# Changelog

このプロジェクトの主要な変更を記録します。形式は [Keep a Changelog](https://keepachangelog.com/ja/1.1.0/) に準拠し、[セマンティック バージョニング](https://semver.org/lang/ja/) を採用します。

[ROADMAP.md](ROADMAP.md) は**これからの計画**、本ファイルは**出荷済みバージョンの履歴**です。

## [Unreleased]

> 実機 GUI 検証(① シーンビューでのプレビュー目視 ② ローカルビルドでアップロード後もメッシュが修正済みか)が通り次第、`0.2.0` として確定します。

### Added
- **NDMF ビルドパス化** — フィットをビルド時(Modular Avatar の Merge Armature 後)にアバターへ適用するようになり、**VRChat アップロード後も修正が維持される**ように。従来は編集時のシーン焼き込みのみで、アップロード時に修正前へ戻っていた(コンポーネントが `IEditorOnly` でビルド時にストリップされ、ビルドパスが存在しなかったため)。`VRClothDeclipperPass` / `VRClothDeclipperNdmfPlugin`(`Transforming.AfterPlugin("nadena.dev.modular-avatar")`)
- **ライブプレビュー** — ビルドと同一の計算(`VRClothPipeline.SolveToFittedMeshes`)を NDMF preview(`IRenderFilter`)でシーンビューに非破壊表示。プレビューとビルドが同じコアを呼ぶため「編集時 = ビルド時」で、二重適用は原理的に発生しない。`VRClothDeclipperPreview`
- **マルチポーズ合成(実験)** — 代表ポーズの貫通を bind 空間の静的デルタ1枚へ束ねる `MultiPoseComposer` と段階1 glue(`Run Multi-Pose Fit` スパイク)
- **RED 原因の名指し** — プリフライトの RED を数値署名から分類する `RedCause`(縮め/隠しシェイプキー・厚物の内側壁という偽陽性[§8]・リターゲ級の体型差)を診断メッセージへ

### Changed
- エディタの「Run Fitting」(シーンへ焼き込み)を「**Run Preflight**」(診断のみ)へ再定義。フィットはライブプレビューとビルドパスで自動適用されるため、シーンには何も焼かない

## [0.1.0] - 2026-06-18

初回公開。貫通修正コア MVP。

### Added
- **貫通修正パイプライン** — プロキシボディ生成(Humanoid ボーン→カプセル列)・貫通検出・押し出し・ラプラシアン平滑化・非破壊適用(元ベース頂点へ補正デルタを加算しブレンドシェイプ二重適用を回避)
- **プリフライト診断** — 検出統計から緑/黄/赤判定(赤は適用を既定で中止。しきい値の較正は E2E)
- **メッシュSDF コライダ** — 衝突バックエンドを `IBodyCollider` に抽象化し、`MeshSdfCollider`(BVH 分枝限定 + Barnes–Hut 巻き数で加速)と `CapsuleBodyCollider` を実装。素体メッシュはメモリ構築し捨てる(No Cache)
- **カプセル半径の素体推定** — 素体メッシュからカプセル半径をパーセンタイル推定
- **配布** — VPM パッケージ化(`IEditorOnly`)、リリース自動化(GitHub Actions)、VPM listing 配信

[Unreleased]: https://github.com/omeletteak/VRCloth-Declipper/compare/0.1.0...HEAD
[0.1.0]: https://github.com/omeletteak/VRCloth-Declipper/releases/tag/0.1.0
