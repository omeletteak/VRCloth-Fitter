# VPM パッケージ化 グラウンドワーク

フェーズ2「VPM パッケージ化」と「vpm-listing での配布」の下調べ資料です。現状の準備度を監査し、必要な作業・設計判断・順序を整理します。配布形態の背景は [DESIGN.md](DESIGN.md) §7、開発計画は [ROADMAP.md](../ROADMAP.md) フェーズ2を参照。最終更新: 2026-06-15(下調べ段階。実装着手時に各項目を本書へ昇格)。

## 0. 一行まとめ

骨格(`package.json`・asmdef 4分割)は既にあり、配布の山場は **(A) Runtime の `IEditorOnly` 化(SDK 参照の入れ方)** と **(B) vpm-listing 用の配布リポジトリ+リリース自動化** の2点。どちらも設計判断を要するので、着手前に第6節の「未決事項」を確定する。

## 1. 現状監査(2026-06-15)

| 項目 | 状態 |
|---|---|
| `Assets/VRCloth-Declipper/package.json` | 有り。`name: dev.omelette_ak.vrcloth-declipper` / `version: 1.0.0` / `unity: 2022.3` / `license: MIT` / `vpmDependencies: { nadena.dev.ndmf: ">=1.0.0", nadena.dev.modular-avatar: ">=1.0.0" }` |
| asmdef | 4分割済み: `VRClothDeclipper.Core`(エンジンのみ)/ `Runtime`(参照なし)/ `Editor`(Core+Runtime、Editor プラットフォーム)/ `Tests.Editor`(`UNITY_INCLUDE_TESTS` 制約・`autoReferenced:false`) |
| 開発環境の依存(`Packages/vpm-manifest.json`) | `com.vrchat.avatars 3.8.2` / `nadena.dev.modular-avatar 1.13.x` / `nadena.dev.ndmf 1.8.3` / `jp.lilxyzw.liltoon`(検証用) |
| コードの SDK コンパイル依存 | **現状なし**。Runtime は素の `MonoBehaviour`。Core はエンジンのみ |

読み取れること:

- VPM パッケージとしての最小骨格は揃っている。`package.json` があり asmdef も分かれているので、フォルダ単位の配布物としては成立する
- ただし「アバターに付ける Runtime コンポーネントがビルド時に剥がれる(`IEditorOnly`)」が未実装。これは VRChat 向けツールの作法であり、フェーズ2の明示項目
- `vpmDependencies` のバージョン下限 `>=1.0.0` は緩すぎる。実際の検証は NDMF 1.8 / MA 1.13 で行っているので、安全な下限へ引き上げるか要検討(第5節)

## 2. IEditorOnly 化 — 山場 (A) ✅ 実装済み(2026-06-15)

> **実装済み**: ユーザー承認(SDK 参照を許可)のもと、下記「実装案(堅牢版)」を採用。`Runtime` asmdef が `VRC.SDKBase`(SDK の asmdef 名)を参照し、`versionDefines` で `com.vrchat.avatars` 存在時に `VRCLOTH_VRCSDK3_AVATARS` を定義。`VRClothDeclipper` は同記号がある時のみ `VRC.SDKBase.IEditorOnly` を実装(SDK 不在環境は `#if` で素の `MonoBehaviour`)。SDK のある本プロジェクトで EditMode 89 件緑=コンパイル健全性を確認。**未決1は解決**。以下は背景記録。


### なぜ要るか

`VRClothDeclipper` はアバター(衣装)に付ける `MonoBehaviour`。`VRC.SDKBase.IEditorOnly` を実装しておくと、VRChat のアバタービルド時に自動で取り除かれ、アップロードされる実機アバターに設定オブジェクトが残らない。NDMF/MA エコシステムのコンポーネントは一様にこれを実装している。

### 制約: リポジトリは SDK を意図的に除外している

本リポジトリは VRChat SDK を再配布物として含まない([DESIGN.md](DESIGN.md) §7、クローン後に VPM で解決)。一方 `IEditorOnly` は `VRCSDKBase` アセンブリの型なので、Runtime asmdef が SDK を参照する必要がある。ここが設計判断点:

- **事実**: 本ツールの `vpmDependencies`(MA/NDMF)は推移的に `com.vrchat.avatars`(=SDK)を要求する。つまり**実利用環境では SDK は常に存在する**。参照すること自体は再配布ではない(利用者環境の VPM 解決物を参照するだけ)
- したがって Runtime asmdef が `VRCSDKBase` を参照するのは、エコシステム標準(MA 等も同様)に沿う妥当な選択

### 実装案(堅牢版・推奨)

SDK 不在環境(SDK を入れずにクローンした CI 等)でもコンパイルが壊れないよう、`versionDefines` で記号を切り替える二段構え:

- `Runtime/VRClothDeclipper.Runtime.asmdef`:
  ```jsonc
  "references": ["VRC.SDKBase"],
  "versionDefines": [
    { "name": "com.vrchat.avatars", "expression": "", "define": "VRCLOTH_VRCSDK3_AVATARS" }
  ]
  ```
- `Runtime/VRClothDeclipper.cs`:
  ```csharp
  #if VRCLOTH_VRCSDK3_AVATARS
      public class VRClothDeclipper : MonoBehaviour, VRC.SDKBase.IEditorOnly
  #else
      public class VRClothDeclipper : MonoBehaviour
  #endif
  ```

注意点(実装で確認済み):

- 参照名は **`VRC.SDKBase`**(SDK ランタイムの asmdef `name`。当初 `VRCSDKBase` と書いていたが、現行 SDK は asmdef `VRC.SDKBase`)。MA は `precompiledReferences` で `VRCSDKBase.dll` 等を挙げる別方式だが、本プロジェクトの SDK は asmdef ソースなので asmdef 名での参照が素直
- asmdef の `references` に挙げた `VRC.SDKBase` は、その**アセンブリが不在だと解決エラー**になる(asmdef に「任意参照」は無い)。本ツールは VRChat 専用で、`vpmDependencies`(MA/NDMF)が推移的に SDK を要求するため**実利用では常に存在**する。`#if` ガードは CI/ヘッドレス向けの保険(型使用は隠れるが参照行自体は残る点は許容)
- versionDefines は `com.vrchat.avatars`(SDK 本体)に紐付け。記号 `VRCLOTH_VRCSDK3_AVATARS` がある時のみ `IEditorOnly` を実装
- EditMode 89 件緑でコンパイル健全性を確認済み

**未決**: 「SDK 参照を Runtime に持ち込む」ことの可否。No SDK redistribution はあくまで**再配布**の話で参照とは独立だが、リポジトリの方針として明文化するか確認したい(第6節)。

## 3. Tests の同梱方針

`Tests.Editor` は `UNITY_INCLUDE_TESTS` 制約+`autoReferenced:false` なので、**消費者プロジェクトでは既定でコンパイルされず実害はない**。ただし VPM 配布物にテスト一式を含めるのは一般的でない。選択肢:

- **(a) そのまま同梱**(現状): 害はないが配布物が重い。`MeshSdfPerformanceTests` 等の重いベンチも含む
- **(b) `Tests~` フォルダへ移す**: 末尾 `~` は Unity が import しない慣習フォルダ。配布物から実質除外しつつソースは残せる
- **(c) リリース zip 生成時に除外**: 配布自動化(第4節)で `Tests/` を含めない

推奨は **(c)**(ソース構成は触らず、配布物だけ絞る)。CI と開発ではテストを回し、利用者には配らない。

## 4. vpm-listing 配布 — 山場 (B)

ALCOM / VCC はどちらも標準 VPM リポジトリ(listing)設定を共有するので、同一の listing で両対応できる([DESIGN.md](DESIGN.md) §7)。必要な構成:

1. **リリース成果物**: タグ push で `Assets/VRCloth-Declipper/` を zip 化し、GitHub Release に添付。`package.json` の `url` をその zip の URL に差し替えたものを listing に載せる
2. **listing リポジトリ(別リポジトリ推奨)**: パッケージ群の索引 JSON(`vpm.json` 形式: `name`/`id`/`url`/`packages{ <pkg> { versions{ <ver>: <package.json + url> } } }`)を GitHub Pages 等で公開
3. **自動化**: タグ→zip→Release→listing JSON 更新を GitHub Actions で。既存の公開アクション(anatawa12 の vpm 系、または VRChat 公式の package-list-action)が使える
4. **利用者導線**: listing URL を ALCOM/VCC に「Add Repository」で追加→本パッケージを Install

groundwork としては「listing は別リポジトリ・リリース自動化で回す」方針を確定すれば十分。実装はフェーズ2着手時。

## 5. package.json の不足フィールド

VPM/listing でユーザー体験を整えるための追加候補(値はリリース自動化が差し込むものを除く):

- `documentationUrl`: リポジトリ URL(`https://github.com/omeletteak/VRCloth-Declipper`)
- `changelogUrl`: CHANGELOG を置くなら
- `url`: 配布 zip の URL(**リリース自動化が差し込む**。手書きしない)
- `keywords`: 検索性(例: `vrchat`, `modular-avatar`, `ndmf`, `clothing`, `penetration`)
- ~~`vpmDependencies` の下限見直し~~ **→ 設定済み(2026-06-15)**: `nadena.dev.ndmf >=1.4.0` / `nadena.dev.modular-avatar >=1.8.0`。根拠: コードは MA/NDMF の**API にコンパイル依存していない**(asmdef 参照なし)ため、下限は「想定ワークフロー(Merge Armature の後段)が安定して存在するバージョン」を表す暫定値。検証環境(NDMF 1.8.3 / MA 1.13.2)より十分下で、かつ現行ユーザーがほぼ超えている安定線に置き、古い環境を不要に弾かないようにした。NDMF パス化(フェーズ5)で実 API 依存が生じた時点で再較正する

`displayName`/`description` は現状英語1文。公開時の方針(英語/日本語、トーン)は [INFORMATION_ARCHITECTURE.md](INFORMATION_ARCHITECTURE.md) §6 に従う。

## 6. 未決事項(着手前に確定したい)

1. ~~**SDK 参照の方針**~~ **→ 解決済み(2026-06-15)**: ユーザー承認のもと Runtime asmdef に `VRC.SDKBase` を参照させ `IEditorOnly` を実装(第2節)。「参照≠再配布」で No SDK redistribution と整合
2. ~~**`vpmDependencies` の下限**~~ **→ 設定済み(2026-06-15)**: NDMF `>=1.4.0` / MA `>=1.8.0`(暫定。第5節に根拠)。NDMF パス化で実 API 依存が生じたら再較正
3. **Tests の配布除外方式**: 第3節 (a)/(b)/(c) のどれか(推奨 c)
4. **listing リポジトリの置き場**: 本リポジトリ内 Pages か、別 `vpm-listing` リポジトリか(別推奨)
5. **バージョニング運用**: `package.json` の `version` とリリースタグの同期、SemVer 運用

## 7. 着手順(確定後)

1. ~~`IEditorOnly` 化 + Runtime asmdef に SDK 参照/versionDefines → EditMode でコンパイル確認~~ **✅ 完了(2026-06-15)**
2. `package.json` に `documentationUrl`/`keywords` 追加、`vpmDependencies` 下限調整
3. リリース自動化(タグ→zip(Tests 除外)→Release→`url` 差し込み)
4. listing リポジトリ作成 + Pages 公開、ALCOM/VCC で追加→Install を実機確認
5. README に導入手順(ALCOM/VCC からの追加)を追記

---

*本書は下調べ段階の記録。フェーズ2着手時に各節を実装結果へ更新する。*
