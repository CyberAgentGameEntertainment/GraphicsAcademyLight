# DirectionalBlur 高品質化 Intent

## Description

既存の `DirectionalBlur.shader` に対して「より高品質なサンプリング」を適用する改修を行う。
デフォルトのサンプリング数が少なく画質が粗い場合があるため、サンプリング数を増やして
より滑らかなブラー結果を得られるようにする。

## 改修要件

- `Sirius.PostProcessing` パッケージ内の既存 `DirectionalBlur.shader` を改修する
- サンプリング数を増やして画質を向上させる（高品質モード）
- サンプリング数は学生が選択する
- 既存の Inverse Sampling ロジックは維持する

## 参照コード

- `SiriusPackages/Sirius.PostProcessing/Runtime/Shaders/DirectionalBlur.shader`（改修対象）
