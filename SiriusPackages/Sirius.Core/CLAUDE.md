# Sirius.Core

ワークショップ用に最小化された共通基盤。詳細は `.claude/skills/ct-pkg-sirius-core/SKILL.md` を参照。

## 共通hlsl

- **MUST** hlslにはインクルードガード（`#ifndef _SIRIUS_CORE_*_HLSL_`）を付けること
- **MUST** `ScreenSpaceUtil.hlsl` はワーク④の足場として参加者が include する。関数シグネチャを変更する場合は `docs/workshop/answers/HeatDistortion/` の答えと `HeatDistortion.shader` のヒントコメントも合わせて更新すること

## ScriptableRendererFeature

- **MUST** `AddRenderPasses` では `SiriusIgnorer` コンポーネントによるカメラ除外チェックを行うこと。`Sirius.PostProcessing` の `SiriusPostProcessingFeature` を参考にすること
