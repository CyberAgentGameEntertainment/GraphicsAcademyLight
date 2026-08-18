---
name: ct-pkg-sirius-postprocessing
description: "Sirius.PostProcessingパッケージの設計詳細。Use when: (1) ポストエフェクトの修正・拡張, (2) パス設計・Volume/AllowFlagパターンの理解"
---

# Sirius.PostProcessing 設計詳細

ポストエフェクトをVolumeベースで制御するパッケージ（ワークショップ用縮小版）。

## AllowFlag パターンの仕組み

新規パス追加時の規約は CLAUDE.md を参照。ここではパターンの動作を説明する。

`AddRenderPasses` 内で `allow*` フラグをチェックし、true の場合のみ `EnqueuePass` する。Volume を持つエフェクトは、パス内部で Volume の値を参照して効果量を決める（例: DirectionalBlurVolume / RadialBlurVolume）。

## パス一覧と登録条件

| パス | allow フラグ | Volume | 追加条件 |
|------|------------|--------|---------|
| RadialBlurRenderPass | `allowRadialBlurPostProcess` | RadialBlurVolume | - |
| DirectionalBlurRenderPass | `allowDirectionalBlurPostProcess` | DirectionalBlurVolume | - |

シェーダーは `Runtime/Shaders/`（`DirectionalBlur.shader` / `RadialBlur.shader` / `RotationBlur.shader`）。
`Editor/Scripts/ShaderIncluder.cs` がリフレクションで各パスの `UsingShaderNameList` を収集し、ビルドの AlwaysIncludedShaders へ登録する。
