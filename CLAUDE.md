# Project Instructions for AI Agents

This file provides instructions and context for AI coding agents working on this project.

<!-- BEGIN BEADS INTEGRATION v:1 profile:minimal hash:7510c1e2 -->
## Beads Issue Tracker

This project uses **bd (beads)** for issue tracking. Run `bd prime` to see full workflow context and commands.

### Quick Reference

```bash
bd ready              # Find available work
bd show <id>          # View issue details
bd update <id> --claim  # Claim work
bd close <id>         # Complete work
```

### Rules

- Use `bd` for ALL task tracking — do NOT use TodoWrite, TaskCreate, or markdown TODO lists
- Run `bd prime` for detailed command reference and session close protocol
- Use `bd remember` for persistent knowledge — do NOT use MEMORY.md files

**Architecture in one line:** issues live in a local Dolt DB; sync uses `refs/dolt/data` on your git remote; `.beads/issues.jsonl` is a passive export. See https://github.com/gastownhall/beads/blob/main/docs/SYNC_CONCEPTS.md for details and anti-patterns.

## Session Completion

**When ending a work session**, you MUST complete ALL steps below. Work is NOT complete until `git push` succeeds.

**MANDATORY WORKFLOW:**

1. **File issues for remaining work** - Create issues for anything that needs follow-up
2. **Run quality gates** (if code changed) - Tests, linters, builds
3. **Update issue status** - Close finished work, update in-progress items
4. **PUSH TO REMOTE** - This is MANDATORY:
   ```bash
   git pull --rebase
   git push
   git status  # MUST show "up to date with origin"
   ```
5. **Clean up** - Clear stashes, prune remote branches
6. **Verify** - All changes committed AND pushed
7. **Hand off** - Provide context for next session

**CRITICAL RULES:**
- Work is NOT complete until `git push` succeeds
- NEVER stop before pushing - that leaves work stranded locally
- NEVER say "ready to push when you are" - YOU must push
- If push fails, resolve and retry until it succeeds
<!-- END BEADS INTEGRATION -->


## Build & Test

Unity 2022.3.22f1 の EditMode テストをバッチモードで実行する(Unity がこのプロジェクトを開いていると失敗する。先に閉じること):

```powershell
& "C:\Program Files\Unity\Hub\Editor\2022.3.22f1\Editor\Unity.exe" -batchmode `
  -projectPath "C:\Users\omelette_ak\github-repos\VRCloth-Fitter" `
  -runTests -testPlatform EditMode `
  -testResults "$env:TEMP\vrcloth-test-results.xml" -logFile "$env:TEMP\vrcloth-test-log.txt"
```

- 終了コード: 0=全件成功 / 2=テスト失敗あり / それ以外=コンパイルエラー等(ログを確認)
- 失敗の詳細は結果 XML の `//test-case[@result='Failed']`、コンパイルエラーはログの `error CS` を見る
- `-testFilter "VRClothFitter.Tests.クラス名"` で絞り込み実行できる

## Architecture Overview

VRChat アバター衣装の貫通自動修正を行う Unity エディタ拡張。4アセンブリ構成(すべて `Assets/VRCloth-Fitter/` 配下):

- **Core**(`VRClothFitter.Core`)— エディタ非依存の幾何計算。カプセル距離・貫通検出・押し出し・Laplacian 平滑化・スキニング数学(`SkinningMath`)
- **Runtime**(`VRClothFitter.Runtime`)— シーンに置く `VRClothFitter` コンポーネント(設定の入れ物)のみ
- **Editor**(`VRClothFitter.Editor`)— パイプライン本体。`VRClothPipeline.Run()` が キャプチャ(`BakeMesh`)→ Humanoid ボーンからカプセル生成 → `PenetrationSolver`(押し出し+平滑化の反復)→ 逆スキニングでメッシュ複製へ書き戻し(`VRClothMeshApplier`)。シーンビュー可視化とインスペクタ GUI もこの層
- **Tests.Editor** — EditMode テスト(Core 直叩き+実 SkinnedMeshRenderer でのラウンドトリップ)

座標規約: キャプチャは `BakeMesh(useScale: false)` + `TRS(position, rotation, scale=1)` でワールド化し、書き戻しはブレンドスキン行列の逆行列で戻す(`SkinnedRoundTripTests` がこの整合を Unity 実スキニングと突き合わせて検証)。

## Conventions & Patterns

- **No Cache 原則** — アバター素体形状を復元しうる中間データを保存・出力しない。`ClothSnapshot` はメモリ内のみ、フィット結果のメッシュ複製もアセット化しない(シーン内完結)
- `Assets/` 配下の `.meta` ファイルは必ずコミットに含める
- コミットメッセージは `feat(fitting): 日本語要約 (bd-issue-id)` 形式
- タスク管理は bd(beads)。ただし `bd create` は現在使用禁止(DB分裂の経緯あり。2026-06-12 に再試行して再現確認: create/show は成功表示でも stats・JSONLエクスポート・dep add から不可視)— 新規タスクは ROADMAP.md か既存 issue の notes へ
