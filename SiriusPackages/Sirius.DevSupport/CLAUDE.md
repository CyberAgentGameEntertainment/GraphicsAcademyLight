# Sirius.DevSupport

## パッケージ独立性

- **MUST** このパッケージは他のSiriusパッケージ（Sirius.Core等）に依存しない設計。asmdefの参照に他のSiriusパッケージを追加してはならない

## 構造（モジュール構成）

各ツールは機能フォルダ直下に `Runtime/` / `Editor/` + 各自の独立した asmdef を持つモジュール（命名: `Sirius.DevSupport.<Tool>` / `.<Tool>.Editor`）。**asmdef のパス・参照構成は `README.md`「アセンブリ構成」を参照**。コードを追加・参照する際は **どの asmdef に属するか** と **その asmdef が誰を参照しているか** を必ず意識すること。

- 新規コードは該当ツールの asmdef に追加。新ツールは既存と同形式でフォルダ + asmdef を新設する
- 各ツールは互いに参照を持たず独立している。**MUST** 参照を追加する際は依存方向を一方向に保ち、循環参照を作らない（Unity がコンパイルエラーになる）
- ルート直下の `Runtime/` `Editor/`（旧 `Sirius.DevSupport.Runtime` / `.Editor`）は削除済み。**MUST** ルートに集約 asmdef を復活させない（Unity の参照は非推移的で機能しない）
- `package.json` の依存は URP ではなく `com.unity.render-pipelines.core`（+ InputSystem / Newtonsoft.Json）

## ShaderPerformanceAnalysis

- 複数シェーダーの malioc 静的解析 + ベースライン比較による回帰検出ツール（Editor 専用）
- JSON パースに `com.unity.nuget.newtonsoft-json` を使用（Unity レジストリパッケージ。他 Sirius パッケージ非依存の原則には抵触しない）
- **MUST** SPIR-V 一時ファイルは `Path.GetTempPath()`（`Assets/` 外）に書き出す。`Assets/` 配下に出すと `.meta` 汚染を招く（g-csharp-005）

## EasyGPUProfiler

- URP 依存の GPU プロファイラ（`SiriusDevSupportFeature` が `ProfilingSampler` を収集）
- asmdef は `versionDefines` で `HAS_URP` を定義し `defineConstraints: ["HAS_URP"]` で隔離する。URP 無しプロジェクトではアセンブリごとコンパイル対象から除外される（コード内 `#if` 保護は不要）
- `Runtime/URPExtensions/Core/RenderGraphUtilsExtension.cs` は `URPCoreReference.asmref` で `Unity.RenderPipelines.Core.Runtime` に注入される（EasyGPUProfiler の asmdef には属さない）

## GraphicsRegressionTest

- NVIDIA FLIP による画像比較ベースのグラフィックス回帰テスト（Editor 専用 / `bin/` に flip バイナリ同梱）
- 任意依存は asmdef の `versionDefines` で検出しコード内 `#if` で保護する（`HAS_URP` / `HAS_TIMELINE` / `HAS_CINEMACHINE_V2` / `HAS_CINEMACHINE_V3`）。EasyGPUProfiler と違い `defineConstraints` では隔離せず、依存が無くてもツール本体は動作する
