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
| HeatDistortionRenderPass | `allowHeatDistortionPostProcess` | HeatDistortionVolume | `_CameraDepthTexture` が必要 |

シェーダーは `Runtime/Shaders/`（`DirectionalBlur.shader` / `RadialBlur.shader` / `RotationBlur.shader` / `HeatDistortion.shader`）。
`Editor/Scripts/ShaderIncluder.cs` がリフレクションで各パスの `UsingShaderNameList` を収集し、ビルドの AlwaysIncludedShaders へ登録する。

## HeatDistortion（陽炎）固有の注意点

- **深度依存**: カメラ距離を `StartDistance`〜`FadeDistance` で減衰カーブに変換して歪み強度を決めるため、URP Renderer の `m_RequireDepthTexture` が有効である必要がある。深度は `Sirius.Core` の `ScreenSpaceUtil.hlsl`（`GetCameraDistance` / `GetCameraVector`）経由で参照しており、RenderGraph 側での `builder.UseTexture` 宣言は不要（`_CameraDepthTexture` は URP のグローバルテクスチャ）。
- **ノイズテクスチャは Texture2DArray**: `_HeatDistortionNoiseTex` は `TEXTURE2D_ARRAY` で宣言している。プリセットの `3DCells64Sheet`（64 スライス）を想定し、時間をスライス番号に対応させて隣接 2 スライスを lerp する。**Texture3D や Texture2D を割り当てると次元不一致になる**ので、インポート設定の Texture Shape を `2D Array` にすること。
- **null 許容**: `NoiseTexture` 未設定時も `RenderPass` 側で `SetTexture` を毎回呼び、null をそのままバインドする（マテリアルをパスで使い回すため、分岐でスキップすると以前のテクスチャが残り Volume の現在値とズレる）。null を渡した場合は次元の一致するシェーダー側デフォルトへフォールバックする。`Texture2D.whiteTexture` のような次元の異なるフォールバックを明示的に渡すと次元不一致になるため、他パスの `Mask` パターン（`mask ? mask : whiteTexture`）は流用できない。
- **既定値 0 でゼロコスト**: `IsActive()` は `Blend > 0 && Intensity > 0 && FadeDistance > 0`。`_intensity` の既定値は他 Volume の `_strength` と同じく **0.0f** にしてある。これにより Volume の Weight を 0 にした場合（＝既定値へ補間される場合）も `IsActive()` が false になり、パスがスキップされる。
