# RotationBlur 新規実装 Intent

## Description

既存の `RadialBlur` を参照しながら、「画面の中心を軸に回転するブラーエフェクト (RotationBlur)」を新規実装する。
UV 座標上の各ピクセルから中心点への方向に対して垂直（接線方向）にサンプリングし、円弧状のブラーを実現する。

## 実装要件

- 中心点 `(CenterX, CenterY)` を基準に、各ピクセルが接線方向にブラーがかかる
- `Sirius.PostProcessing` の Volume / RenderPass / Shader 三層構造原則に従う
- `SiriusPostProcessingFeature` の `allowXxx SerializeField + [AllowFlag]` ペアで有効化
- HLSL シェーダーで GPU 処理、サンプリング 6 回

## 参照コード

- `SiriusPackages/Sirius.PostProcessing/Runtime/Shaders/RadialBlur.shader`（コード構造が類似）
- `SiriusPackages/Sirius.PostProcessing/Runtime/Scripts/Features/Passes/RadialBlurRenderPass.cs`
- `SiriusPackages/Sirius.PostProcessing/Runtime/Scripts/Volumes/RadialBlurVolume.cs`
- `SiriusPackages/Sirius.PostProcessing/Runtime/Scripts/Features/SiriusPostProcessingFeature.cs`
