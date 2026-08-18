---
name: ct-pkg-sirius-core
description: "Sirius.Coreパッケージの設計詳細。Use when: (1) Core機能の修正・拡張, (2) ワークショップのシェーダーが参照する共通hlslの理解, (3) 他パッケージが依存する共通基盤の変更"
---

# Sirius.Core 設計詳細

ワークショップ用に最小化された共通基盤。ポストエフェクトのシェーダーが参照する hlsl と、
それを動かすのに必要な最小限の C# のみを持つ。

## パッケージ間の依存関係

```
Sirius.Core  ← Sirius.PostProcessing (asmdef参照)
             ← Sirius.DevSupport は依存しない（独立）
```

asmdef が参照するのは `Unity.RenderPipelines.Core.Runtime` のみ。
hlsl 側は URP の Universal / Core 両方のシェーダーライブラリを include する。

## 構成物

### Runtime/Shaders

| ファイル | 役割 | 主な利用先 |
|---|---|---|
| `Common.hlsl` | `SIRIUS_PREFIX*` マクロ、`SIRIUS_USE_CLUSTERED_LIGHT_LOOP` 判定 | GodRay の Skybox シェーダー、ワーク④⑤ |
| `CoreUtil.hlsl` | `pow2`〜`pow8` / `powFast` / `acosFast` / `Panner` / `RotateAboutAxis` などのユーティリティ | ワーク⑤（GodRayRadialBlur） |
| `DeclareDepthTexture.hlsl` | `SampleSceneDepth` / `LoadSceneDepth` の Unity バージョン差異吸収 | `CoreUtil.hlsl`、ワーク⑤（Binarization） |
| `ScreenSpaceUtil.hlsl` | 深度からのワールド/ビュー座標復元（`GetWorldPosition` / `GetCameraDistance`）、Framebuffer Fetch 抽象化 | ワーク④（HeatDistortion） |

`ScreenSpaceUtil.hlsl` はワーク④の**足場**として提供している。参加者は距離フェード実装時にこれを include する
（`Sirius.PostProcessing/Runtime/Shaders/HeatDistortion.shader` にコメントでヒントを置いてある）。

### Runtime/Scripts

| ファイル | 役割 |
|---|---|
| `CTRenderGrapProfilingScope.cs` | `AddUnsafePass` を使う RenderGraph 用プロファイリングスコープ。URP の `RenderGraphProfilingScope` が `AddRenderPass` 前提で使えないため自前実装。ワーク⑤の `GodRayRenderPass` が利用 |
| `GlobalSettings.cs` | `DevelopmentMode` のみ。`CTRenderGraphProfilingScope` が参照する |
| `SiriusIgnorer.cs` | カメラ単位でポストエフェクトの実行を除外するコンポーネント。`SiriusPostProcessingFeature` が参照 |

## 注意

- `GlobalSettings.DevelopmentMode` を設定する RendererFeature は無い。Editor / DEVELOPMENT_BUILD では既定 `true`、それ以外では常に `false` になる
- ワークショップ縮小版のため、製品版 Sirius.Core にある GBuffer / Fog / Quality / WindZone 連携・Editor 用 MaterialGUI 基盤は含まれない
