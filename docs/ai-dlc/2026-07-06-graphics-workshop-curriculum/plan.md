# 学生向け AI 駆動グラフィックスプログラミング演習プログラム 実装計画

## 既存パターン言語化

Sirius.PostProcessing は **「Volume + RenderPass + Shader 三層構造原則」**。VolumeComponent でパラメータ管理、`ScriptableRenderPass + IAllowExecute` で描画実行、HLSL シェーダーで GPU 処理。シェーダーは `half` 精度・`UNITY_UNROLL`（定数ループ時）を標準とし、`SiriusPostProcessingFeature` への `allowXxx SerializeField + [AllowFlag]` ペアで有効化される。

RadialBlur は `#define RADIAL_BLUR_SAMPLING_COUNT 6`（定数）で `UNITY_UNROLL` を適用。DirectionalBlur は `int _DirectionalBlurSamplingCount`（変数）のため `UNITY_UNROLL` なし・動的ループ。

## 採用設計

**Plan A: 問題入りコードを SiriusPackages に一時書き込み + docs/workshop に正解配置**

演習素材の生成・配置フロー:

1. **問題入りスキル（`.claude/skills/workshop-rotation-blur/`, `.claude/skills/workshop-directional-blur/`）**:
   スキル実行時に `assets/` フォルダの問題入りファイルを `SiriusPackages/Sirius.PostProcessing/Runtime/` に Write する。全受講者が同一コードを受け取ることを保証（AI が都度生成するのではなく固定コードをコピー）。SiriusPostProcessingFeature の AllowFlag 登録もスキルが自動実行し、演習終了後の rollback 手順を提示する。

2. **RotationBlur 問題入りコード（バグ 2 種）**:
   - バグ①: 接線方向ベクトルにアスペクト比補正なし（`half2(1, 1)` を使用 → 横長画面で楕円形ブラー）
   - バグ②: UV・方向ベクトル計算に `float` 精度を使用（`half` でなく）→ モバイル GPU 静的解析で悪化

3. **DirectionalBlur 問題入り改修コード（バグ 2 種）**:
   - バグ①: サンプリング数を `3 → 8` に増やし `UNITY_UNROLL` を削除（動的ループ化）→ モバイル GPU 負荷増大
   - バグ②: 合成式を `rcp(sample_count)` から `1.0h / 8.0h` 定数除算に変更（Inverse Sampling 有無で除数がズレる）→ ビジュアルデグレ

4. **正解コード（`docs/workshop/answers/`）**: 既存パターン（half 精度・UNITY_UNROLL）に沿った実装。`Sirius.PostProcessing` への本番マージは行わない。

## 棄却した代替案

### Plan B: プロジェクト内 Workshop 専用 asmdef 方式

棄却理由: `Assets/Workshop/` に新規 asmdef と MonoBehaviour を作成する必要があり、既存の `Sirius.PostProcessing` の Volume/Feature パターンから乖離する。学習コスト（asmdef 配線・独自エントリーポイント）が演習の本題（シェーダーコードレビュー・テスト）と無関係に増加する。

### Plan C: 問題入りコードを静的参照のみ（Unity 非実行）

棄却理由: 「横長画面でブラーが楕円形になることを目視確認」という Completion Criteria を満たせない。静的解析のみでは視覚的バグ（アスペクト比補正欠如）の学習体験が不完全。

## Units of Work

### UoW#1 [座学テキスト作成] 独立

- 対象: `docs/workshop/01_intro_ai_dlc.md`
- 内容: AI-DLC の概念説明（従来との主従逆転）、AI が間違える理由、レビューが不可欠な理由をコード例付きで記述
- 依存: なし
- 担当: AI
- コミット先: SIRIUS

### UoW#2 [RotationBlur 正解コード作成] 独立

- 対象: `docs/workshop/answers/RotationBlurPass.shader`, `docs/workshop/answers/RotationBlurRenderPass.cs`, `docs/workshop/answers/RotationBlurVolume.cs`
- 内容: RadialBlur を参考に、接線方向ブラー + アスペクト比補正 (`half2(_ScreenParams.y * rcp(_ScreenParams.x), 1.0)`) + half 精度 + UNITY_UNROLL の正解実装
- 依存: なし（UoW#1 と並列可能）
- 担当: AI
- コミット先: SIRIUS

### UoW#3 [Part 2 スキル＆問題入りコード作成] UoW#2 待ち

- 対象:
  - `.claude/skills/workshop-rotation-blur/SKILL.md`
  - `.claude/skills/workshop-rotation-blur/assets/RotationBlurPass.shader`（バグ①②入り）
  - `.claude/skills/workshop-rotation-blur/assets/RotationBlurRenderPass.cs`
  - `.claude/skills/workshop-rotation-blur/assets/RotationBlurVolume.cs`
- 内容: 正解コードをベースにバグを意図的に混入した問題入り版を作成。スキルは assets/ から SiriusPackages に Write し、SiriusPostProcessingFeature へ AllowFlag 登録し、rollback 手順を提示する
- 依存: UoW#2
- 担当: AI
- コミット先: SIRIUS

### UoW#4 [Part 2 問題入りコード検証] UoW#3 待ち

- 対象: 検証作業（ファイル変更なし）
- 内容:
  - ① Mali Offline Compiler で問題入り vs 正解のレジスタ数 / 算術サイクル数を比較し、測定可能な差異を確認
  - ② SiriusPackages に問題入りコードを一時配置し、Unity 横長画面で楕円形ブラーを目視確認（スクリーンショット記録）
  - 検証後に SiriusPackages と SiriusPostProcessingFeature.cs を復元
- 依存: UoW#3
- 担当: AI（Mali 解析）+ 人間（Unity 目視）
- コミット先: なし（検証のみ）

### UoW#5 [DirectionalBlur 問題入り改修＆スキル作成] 独立

- 対象:
  - `.claude/skills/workshop-directional-blur/SKILL.md`
  - `.claude/skills/workshop-directional-blur/assets/DirectionalBlur.shader`（動的ループ + 定数除算バグ入り）
  - `docs/workshop/answers/DirectionalBlur.shader`（既存コードのコピー = 正解）
- 内容: 既存 DirectionalBlur.shader をベースに「高品質化」を装ったバグ入り改修版を作成。スキルは assets/ から SiriusPackages に Write する
- 依存: なし（UoW#1 と並列可能）
- 担当: AI
- コミット先: SIRIUS

### UoW#6 [Part 3 問題入りコード検証] UoW#5 待ち

- 対象: 検証作業
- 内容:
  - ① Mali Offline Compiler で動的ループ版 vs 既存のサイクル数を比較し、劣化を確認
  - ② 問題入りシェーダーを SiriusPackages に一時配置し、ビジュアルリグレッションテストで既存スナップショットとの差分を検出
  - 検証後に復元
- 依存: UoW#5
- 担当: AI
- コミット先: なし（検証のみ）

### UoW#7 [品質ゲート: 正解コードで全テスト Pass] UoW#2, UoW#5 待ち

- 対象: テスト作業（SiriusPackages への一時配置 → TestRunner 全件実行 → 復元）
- 内容: 正解コードを SiriusPackages に一時配置した状態で EditMode / PlayMode 全テストを実行し、全件 pass を確認
- 依存: UoW#2, UoW#5
- 担当: AI
- コミット先: なし（テストのみ）

### UoW#8 [README_DEVELOPERS.md 更新] UoW#3, UoW#5 待ち

- 対象: `README_DEVELOPERS.md`「Claude Code スキル」セクション
- 内容: `workshop-rotation-blur` / `workshop-directional-blur` スキルのセットアップ手順と使い方を追記（CLAUDE.md 規約対応）
- 依存: UoW#3, UoW#5
- 担当: AI
- コミット先: SIRIUS

### UoW#9 [PR 作成] 全 UoW 待ち

- 対象: SIRIUS PR × 1
- 依存: UoW#1〜8
- 担当: AI + 人間（承認）
- コミット先: SIRIUS

## 並列可能ペア

- UoW#1 ‖ UoW#2 ‖ UoW#5（すべて独立）
- UoW#3 は UoW#2 完了後に UoW#5 と並列実行可能
- UoW#6 ‖ UoW#4（UoW#3 / UoW#5 がそれぞれ完了していれば並列可）

## 触ってはいけないファイル

- `*.meta`（Unity Editor 管理、AI が生成してはならない）
- サブモジュールポインタ（SiriusPackages HEAD の SIRIUS へのコミット）
- `Packages/manifest.json`（tarball swap 後の状態）
- `SiriusPostProcessingFeature.cs` の恒久的変更（UoW#4 の一時変更後は必ず復元、コミットしない）

## PR 構成

- SIRIUS PR × 1
  - `docs/workshop/` 以下の演習素材（座学テキスト・正解コード）
  - `.claude/skills/workshop-rotation-blur/` スキル定義と assets
  - `.claude/skills/workshop-directional-blur/` スキル定義と assets
  - `README_DEVELOPERS.md` スキル追記
