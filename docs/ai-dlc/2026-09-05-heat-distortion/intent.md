# HeatDistortion（陽炎エフェクト）

## Description

奥行き方向の温度差によって背景が揺らめいて見える、大気ゆらぎ（陽炎）表現のポストプロセスエフェクトを SIRIUS のポストプロセス機構に追加する。シーンの深度情報を手がかりに揺らぎの発生領域・強度を制御し、3D ノイズテクスチャで揺らぎのパターンを生成、背景のサンプリング座標をオフセットすることで屈折しているように見せる。

## Context

- **配置先パッケージ（候補）**: `Sirius.PostProcessing`。既存の `RadialBlur` / `DirectionalBlur` / `RotationBlur` と同じ `VolumeComponent` + `ScriptableRenderPass` + `AllowFlag` 構成に倣う想定（最終決定は plan.md）。
- **既存類似機構との関係**: 既存 3 種のポストエフェクトと同様、`SiriusPostProcessingFeature` に新規パスを追加する形で統合する。既存パスとの共存（同時 ON）を想定する。
- **利用素材の指定**: 揺らぎパターン生成用の 3D ノイズテクスチャとして、既存プリセット `3DCells64Sheet`（`SiriusAssets/Sirius.Core.Assets/Assets/3DCells64Sheet.png`）を利用する。新規ノイズテクスチャは作成しない。
- **利用想定**: レベルデザイナー / エフェクトデザイナーが Volume Profile 上でパラメータ（強度・スケール等）を調整し、シーンに配置して使う。
- **動的軸**: 静的。Inspector / Volume Profile で事前設定する運用のみを想定し、ランタイムでの ON/OFF・強度切替は不要（既存の `RotationBlurVolume` 等と同じ運用）。
- **パフォーマンス制約**: 既存のポストエフェクト群と同程度のオーダー（フルスクリーンパス 1 回分程度）に収まること。OFF 時（強度 0 相当）は本機能由来の追加コストが実質発生しないこと。

## Completion Criteria

### ✅ 成功ケース

- Volume Profile 上でパラメータ（強度・ノイズスケール等）を調整すると、深度に応じた背景の揺らぎ度合いが変化して見える
- 既存の `RadialBlur` / `DirectionalBlur` / `RotationBlur` と同時に ON にしても、双方の効果が破綻せず共存する
- Volume の Weight/強度を 0 にすると、視覚的に効果が発生しない（既存の `IsActive()` パターンに準ずる無効化判定を持つ）

### ❌ 失敗ケース

- 強度 0（無効状態）でも本機能由来の追加パス実行・追加コストが発生しない
- 深度の不連続点（オブジェクトのエッジ）で背景ピクセルのにじみ出し等、破綻した見た目のアーティファクトが出ない
- ノイズテクスチャが未設定（null）の状態でも例外・クラッシュを起こさない

### 🔒 品質ゲート

- コンパイル時エラー 0 / 新規警告 0
- 既存および新規の自動テスト（EditMode / PlayMode）が pass
- ビジュアルリグレッションテストで既存シーンに意図しない差分がないこと
- 人手レビュー pass

具体の検証手段（コマンド / スキル / 計測ツール）は Phase 2 (Inception) で plan.md に記述する。
