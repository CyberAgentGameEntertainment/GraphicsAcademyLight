---
name: workshop-rotation-blur-setup
description: "Workshop_RotationBlur シーンで RotationBlur を確認できるように、Global Volume Profile への RotationBlurVolume 追加と Renderer Feature の allowRotationBlurPostProcess 有効化を、探索なしの固定レシピで実行する。【注意】/workshop-ai-dlc rotation-blurを実装 の通常フローでは使わない（Phase 4 は Unity 上の手動設定をユーザーに案内する方針）。ユーザーが明示的にこのスキルでの自動配線を依頼した場合のみ使用する。"
---

# RotationBlur シーン設定スキル

**このスキルはユーザーが明示的に「自動で配線して」等と依頼した場合のみ使う。**
`/workshop-ai-dlc rotation-blurを実装` の通常フロー（Phase 4）では、Volume Profile / Renderer Feature の設定は AI が代行せず、Unity Editor 上での手動手順をユーザーに案内する方針になっている。Phase 3 Construction 完了後に自動でこのスキルを連鎖起動しないこと。

`/workshop-ai-dlc rotation-blurを実装` で生成した `RotationBlurVolume` / `RotationBlurRenderPass` / シェーダーは、
**シーンの Volume Profile と Renderer Feature の設定（配線）** が済むまで Unity 上で確認できない。
この配線は毎回同じ手順で完結するため、探索を行わず固定レシピで実行し、トークンを節約する。

## 前提

以下が既に存在すること（`/workshop-ai-dlc rotation-blurを実装` の Phase 3 Construction 完了後）:

- `SiriusPackages/Sirius.PostProcessing/Runtime/Shaders/RotationBlur.shader`
- `SiriusPackages/Sirius.PostProcessing/Runtime/Scripts/Volumes/RotationBlurVolume.cs`
- `SiriusPackages/Sirius.PostProcessing/Runtime/Scripts/Features/Passes/RotationBlurRenderPass.cs`
- `SiriusPackages/Sirius.PostProcessing/Runtime/Scripts/Features/SiriusPostProcessingFeature.cs` に `allowRotationBlurPostProcess` フィールドが追加済み

未実装なら先に `/workshop-ai-dlc rotation-blurを実装` を実行してから、このスキルを呼び出すこと。

## 対象ファイル（固定パス — 探索不要）

| ファイル | 役割 |
|---|---|
| `Assets/Demo/Workshop_RotationBlur/Workshop_RotationBlur/Global Volume Profile.asset` | シーンの Volume Profile。ここに RotationBlurVolume コンポーネントを配線する |
| `Assets/Settings/UniversalRenderPipelineAsset_Renderer.asset` | `SiriusPostProcessingFeature` の `allowRotationBlurPostProcess` フラグを有効化する |
| `SiriusPackages/Sirius.PostProcessing/Runtime/Scripts/Volumes/RotationBlurVolume.cs.meta` | 現在の GUID を取得するために **Read のみ**行う（**編集・生成は禁止** — `.meta` はUnity Editor管理） |

## 実行手順（この順で実行。各 Step は冒頭の判定で完了済みならスキップする）

### Step 1: 現行 GUID を取得

`RotationBlurVolume.cs.meta` を Read し、`guid:` の値を取得する。

- ファイルが存在しない場合、Phase 3 がまだ実行されていない、または Unity が未インポート。素の `uloop compile` を 1 回実行して Unity にインポートさせてから再度 Read する。

### Step 2: Renderer Feature フラグを有効化

`Assets/Settings/UniversalRenderPipelineAsset_Renderer.asset` を Read し、`SiriusPostProcessingFeature` の MonoBehaviour ブロックを確認する。

- 既に `allowRotationBlurPostProcess: 1` があれば **完了済み。この Step はスキップ**
- 無ければ Edit で `allowDirectionalBlurPostProcess: 1` の直後に 1 行追記する:
  ```yaml
  allowDirectionalBlurPostProcess: 1
  allowRotationBlurPostProcess: 1
  ```

### Step 3: Volume Profile に RotationBlurVolume を配線

`Assets/Demo/Workshop_RotationBlur/Workshop_RotationBlur/Global Volume Profile.asset` を Read する。

- 既に `guid: <Step1で取得したguid>` を参照する `RotationBlurVolume` コンポーネントが `components:` リストに含まれていれば **完了済み。この Step はスキップ**
- それ以外（`components: []`、または過去の演習サイクルで残った古い GUID 参照の孤立ブロックがある等）は、以下のテンプレートで **ファイル全体を Write で上書き**する（孤立ブロックはこの上書きで一掃される）:

```yaml
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: d7fd9488000d3734a9e00ee676215985, type: 3}
  m_Name: Global Volume Profile
  m_EditorClassIdentifier: Unity.RenderPipelines.Core.Runtime::UnityEngine.Rendering.VolumeProfile
  components:
  - {fileID: 4630292859716184871}
--- !u!114 &4630292859716184871
MonoBehaviour:
  m_ObjectHideFlags: 3
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: <STEP1_GUID>, type: 3}
  m_Name: RotationBlurVolume
  m_EditorClassIdentifier: Sirius.PostProcessing.Runtime::Sirius.PostProcessing.Runtime.Scripts.Volumes.RotationBlurVolume
  active: 1
  _centerX:
    m_OverrideState: 1
    m_Value: 0.5
  _centerY:
    m_OverrideState: 1
    m_Value: 0.5
  _strength:
    m_OverrideState: 1
    m_Value: 5
  _width:
    m_OverrideState: 1
    m_Value: 5
  _mask:
    m_OverrideState: 0
    m_Value: {fileID: 0}
    dimension: 1
```

- `<STEP1_GUID>` を Step 1 で取得した GUID に置き換える（それ以外は書き換えない）
- `m_Script` の `fileID: 11500000` / `type: 3` は MonoScript 参照の固定値（変更不可）
- `Strength` / `Width` はレビューで効果が視認できるようあえて既定値より大きくした値（`5` / `5`）。`CenterX` / `CenterY` は画面中央（`0.5` / `0.5`）

### Step 4: コンパイル確認

素の `uloop compile` を 1 回実行する（`--force-recompile` / `--wait-for-domain-reload` は付けない。プロジェクト運用ルールに従う）。`Success: true` を確認する。

### Step 5: 結果を報告

Step 2 / Step 3 それぞれ「変更した」か「既に設定済みでスキップした」かを 1 行で報告する。
Workshop_RotationBlur シーンを開けば、Game View に（シェーダー未実装時は赤、実装済みなら）回転ブラーが確認できることを伝える。

## トークン節約のための制約（厳守）

- **探索禁止**: 上記の固定パス以外を Glob/Grep で探索しない。ファイル構造は毎回同一
- **`.meta` ファイルは Read のみ**。生成・編集は絶対に行わない
- **`uloop execute-dynamic-code` は使わない**。YAML の直接編集（Read → Edit/Write）で完結させる
- 検証は Step 4 の素の `compile` 1 回のみ。`get-logs` / `find-game-objects` 等の追加確認は行わない（目視確認は受講者に委ねる）

## 復元

演習を最初からやり直す場合:

```bash
git checkout HEAD -- "Assets/Demo/Workshop_RotationBlur/Workshop_RotationBlur/Global Volume Profile.asset"
git checkout HEAD -- "Assets/Settings/UniversalRenderPipelineAsset_Renderer.asset"
```

`/workshop-ai-dlc` Phase 6 の RotationBlur 復元手順と合わせて実行すること。
