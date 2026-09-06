# HeatDistortion（陽炎）実装計画

<!-- Phase 2 (Inception) で生成するファイル。
     intent.md を入力に、UoW 分解と採用設計を記述する。
     Phase 3 (Construction) で UoW ごとに該当セクションを読む。
-->

## Step 1 調査サマリ（既存パターンの言語化）

- **RenderPass は Volume 駆動の自己完結原則**: `RadialBlurRenderPass` / `DirectionalBlurRenderPass` / `RotationBlurRenderPass` はいずれも `VolumeManager.instance.stack.GetComponent<T>()` で Volume の値だけを読み、シーン側の状態を参照しない。`IsActive()` が false なら `RecordRenderGraph` が早期 return し、パス自体が実質何もしない（ゼロコスト無効化）。
- **Volume 公開パターン**: `ClampedFloatParameter` / `BoolParameter` / `TextureParameter` を `[SerializeField]` で持ち、同名の `get/set` プロパティを公開。`Mask` 系テクスチャは常に nullable（`TextureParameter(null)` がデフォルト）で、パス側が `mask ? mask : Texture2D.whiteTexture` のようにフォールバックする。
- **パスの実装は単一 Blit パス**: `renderGraph.CreateTexture` → `AddRasterRenderPass` → `Blitter.BlitTexture(cmd, source, Vector2.one, mat, 0)` → `resourceData.cameraColor = dest` という定型。マルチパス／追加 RT は使っていない。
- **深度アクセスは Sirius.Core の共通 hlsl 経由**: `ScreenSpaceUtil.hlsl` の `GetWorldPosition` / `GetCameraDistance` / `DeclareDepthTexture.hlsl` の `SampleSceneDepth` は `_CameraDepthTexture`（URP グローバルテクスチャ）を直接参照する設計で、RenderGraph 側の `builder.UseTexture` 宣言は不要（既存 3 パスも同様にグローバルテクスチャは明示宣言していない）。`Assets/Settings/UniversalRenderPipelineAsset_Renderer.asset` は `m_RequireDepthTexture: 1` 済みで、深度テクスチャは既に有効化されている（追加設定不要）。
- **ノイズテクスチャの実体**: intent 記載の `3DCells64Sheet.png` は `flipbookColumns: 64` で、64 枚のスライスを持つシート状のノイズ。`TEXTURE2D_ARRAY` / `SAMPLE_TEXTURE2D_ARRAY_LOD` でスライス指定サンプリングする（3D ボリュームノイズの疑似トライリニアは スライス N / N+1 を手動 lerp する）。
  - ⚠️ **Construction時の修正（Phase 2 の調査誤り）**: 当初 plan には「`.meta` 上 `textureShape: 8`（Texture2DArray）としてインポート済み」と記載していたが、**これは誤り**。`TextureImporterShape` の実値は `Texture2D=1 / TextureCube=2 / Texture2DArray=4 / Texture3D=8` であり、`8` は **Texture3D** を指す。つまり当該アセットは Texture3D としてインポートされていた。本実装の `TEXTURE2D_ARRAY` を成立させるため、Construction 中に **Inspector で Texture2DArray（`textureShape: 4`）へ変更**しており、この `.meta` 変更は本機能に必須の差分として PR に含める。
  - なお元が Texture3D だったため、`TEXTURE3D` + ハードウェアのトライリニア補間で実装する選択肢も本来あった（スライスの手動 lerp が不要になる）。本 Construction では既に Texture2DArray 前提で実装が進んでいたためそのまま採用したが、再設計するならこちらの方が素直。
- **multi_compile は不使用**: 3 パスとも shader keyword 分岐なし。固定 1 パーミュテーションが既存原則。
- **参考データの発見（重要・注意点あり）**: `Assets/Demo/Workshop_HeatDistortion/Global Volume Profile.asset` 内に、`components: []`（未適用）だが孤立した `HeatDistortionVolume` のシリアライズ済みブロックが残存している。フィールド名とデフォルト値は以下の通りで、採用設計のパラメータ命名の参考にした:
  - `_Intensity` = 1, `_StartDistance` = 20, `_FadeDistance` = 150, `_Speed` = 0.1369, `_ChromaticSeparation` = 0.5, `_ZenithMask`(bool, override off) = 0, `_HorizonMask`(bool, override off) = 1, `_HorizonExponent`(override off) = 2.5, `_NoiseScale`(override off) = 1.7
  - ⚠️ **注意**: この孤立データの `m_EditorClassIdentifier` は `Sirius.PostProcessing.HeatDistortionVolume`（名前空間 `Sirius.PostProcessing` 直下）であり、本リポジトリの現行規約（`Sirius.PostProcessing.Runtime.Scripts.Volumes` 名前空間、`RotationBlurVolume` 等参照）と異なる。製品版 Sirius など別コードベース由来の残骸の可能性が高いため、**名前空間はこの孤立データに合わせず、現行 `Volumes` 名前空間に従う**。フィールド名/デフォルト値のみ参考として採用。
  - このデータには **ノイズテクスチャ用フィールドが存在しない**（テクスチャ関連の項目が一切ない）。一方 intent.md の失敗ケースには「ノイズテクスチャ未設定（null）でもクラッシュしない」という要件が明記されており、null になり得る＝ユーザーが差し替え可能な公開フィールドの存在を前提にした文言。孤立データの分解元が不明な簡略実装である可能性を踏まえ、**本設計では既存 3 エフェクトの `Mask` パターンに倣い `NoiseTexture` を nullable `TextureParameter` として公開する**（既存原則との整合を優先）。

## 採用設計

**Plan A: 既存 3 エフェクトと同一の Volume 駆動 + RenderGraph 単一 Blit パス構成**

- `HeatDistortionVolume`（`VolumeComponentMenu("Sirius/HeatDistortion")`）: `Intensity` / `Blend`（Construction時に追加した合成率）/ `StartDistance` / `FadeDistance` / `Speed` / `ChromaticSeparation` / `ZenithMask`(bool) / `HorizonMask`(bool) / `HorizonExponent` / `NoiseScale` / `NoiseTexture`(nullable `TextureParameter`)
- `HeatDistortionRenderPass`: 既存 3 パスと同型（`ScriptableRenderPass` + `IAllowExecute`、`UsingShaderNameList`、`UpdateMaterialProperties`、`RecordRenderGraph` で Blit 1 回）。Volume から読んだ値を `SetFloat`/`SetVector`/`SetTexture` でマテリアルに渡す。
- `HeatDistortion.shader`（既存スタブの `Frag` を実装）:
  1. `SampleSceneDepth` → `GetViewPosition`/`GetCameraDistance` でカメラ距離を取得し、`StartDistance`〜`FadeDistance` で強度を減衰させる距離カーブを作る
  2. スクリーン座標 UV（アスペクト比補正込み）を Texture2DArray の xy、`_Time.y * Speed` をスライス番号（0〜63）の連続値として使い、隣接 2 スライスを lerp してノイズを取得（`NoiseScale` で座標スケール）
     - Construction時の修正: 当初案はワールド座標 xz を xy に使う設計だったが、実装後の目視確認で**遠方ほど 1 ピクセルあたりのワールド座標変化が大きくなりノイズが高密度に潰れる**ことが判明。距離減衰（`StartDistance`〜`FadeDistance`）で歪みが最も強くなる遠方領域と、ノイズが最も視認しにくくなる領域が一致してしまうため、スクリーン座標基準に変更した。副作用としてノイズパターンがカメラ移動に追従せず画面に貼り付く挙動になるが、陽炎表現としては一般的な手法であり許容する。`NoiseScale` の意味は「ワールド単位あたりのタイル数」から「画面高さあたりのタイル数」に変わる（既定値 1.7・範囲 0〜10 はそのまま流用可能なため Volume 側は変更なし）
  3. カメラ前方ベクトルと世界上方向の角度から Horizon/Zenith マスクを計算し、`HorizonMask`/`ZenithMask`/`HorizonExponent` で重み付け
  4. ノイズ値を UV オフセットに変換（`RotationBlur.shader` と同様にアスペクト比補正を必ず入れる。ワークショップ資料 `01_intro_ai_dlc.md` に明記された「AI が省略しがちなバグ」の再発を避けるため、Construction 時に意図的に最初から入れる）
  5. **エッジにじみ防止**: オフセット先 UV でも深度をサンプルし、オフセット元との深度差が一定閾値を超える場合はオフセットを 0 に戻す（intent の失敗ケース「深度の不連続点でにじみ出ない」を満たすための実装。閾値は固定の内部定数とし、Volume パラメータとしては公開しない）
     - **Construction時の修正（最終的に不採用）**: 当初の「絶対深度差 2.0 ワールド単位を超えたらオフセットを 0 にする」二値ガードは、シーンの被写体距離が 180〜230 あるため**ほぼ全画面で発動**し、エフェクトが視認できなくなった（A/B スクリーンショットで確認）。原因は (a) 閾値が絶対距離で遠景の地面のような平坦面でも即座に振り切れる (b) `abs` で「奥方向へのずれ」まで弾いている (c) 二値ステップで境界がちらつく、の 3 点。「方向性あり・カメラ距離に対する相対閾値・`smoothstep` でソフト」な形へ作り替えたうえで、適用先もオフセット量から**合成係数側**へ移した。最終的にはユーザー判断でガード自体を削除し、`t` は `distanceMask * Blend` の 2 項に落ち着いている
  6. `ChromaticSeparation` で RGB チャンネルごとにオフセット量をずらし、色収差混じりの陽炎らしい見た目にする
     - **Construction時の修正**: 当初は単一ノイズから作った 1 本のオフセットを `(1+c) / 1 / (1-c)` 倍する方式だったが、3 チャンネルのオフセットが常に同一直線上に並び分離量の比が固定されるため、**R/G/B それぞれに独立したノイズをサンプルする**方式へ変更した。ノイズのスライス（時間軸）をチャンネルごとにずらす（`HEAT_DISTORTION_CHROMA_SLICE_OFFSET`）ことで、`ChromaticSeparation = 0` のとき 3 チャンネルが同一スライスに収束して色ずれが消える。コストは `SampleNoise` が 1 回 → 3 回（テクスチャフェッチ 2 → 6 タップ）
  7. **合成**: 歪み結果を元画像へ `lerp(color, distorted, t)` で合成する。`t = saturate(distanceMask * Blend)`。`Blend` は Volume に公開した合成率（0 で元画像そのまま）で、`IsActive()` にも含めているため **Blend = 0 のときはパス自体がスキップされる**（ゼロコスト無効化）。`angleMask` は `strength`（オフセット量）側に留める（`HorizonExponent` が最大 10 のため二重掛けすると過剰に潰れる）
- `IsActive()`: `Blend > 0 && Intensity > 0 && FadeDistance > 0`（`NoiseTexture` が null でも例外にならないことは RenderPass 側で `SetTexture` を呼ばないことにより担保する）
  - **Construction時の修正**: 当初は `Intensity > 0 && FadeDistance > 0` のみだったが、Volume に `Blend` を追加した際に条件へ加えた。これにより Blend = 0 で確実にパスがスキップされる。
  - **Phase 3 品質チェック時に解消**: `_intensity` の既定値が 1.0f のままだと、**Volume の weight を 0 にしても Intensity は既定値 1.0 に補間されるだけで 0 にならず**、intent の成功ケース「Volume の Weight/強度を 0 にすると視覚的に効果が発生しない」を weight 経由で満たせなかった。既存 `RotationBlurVolume._strength` / `RadialBlurVolume._strength` はいずれも既定値 0.0f でこの問題がない（一方シェーダー側 Property の既定値は 1.0 のまま、というのが既存 3 エフェクト共通の規約）。この house convention に合わせ `SiriusPackages/.../HeatDistortionVolume.cs` の `_intensity` 既定値を **0.0f に変更**した（シェーダーの Properties ブロックは 1.0 のまま据え置き）。デモの `Global Volume Profile.asset` は `_intensity` を `m_OverrideState: 1` で明示 override（値 2）しているため、この既定値変更によるデモの見た目への影響はない

## 棄却した代替案

### Plan B: MonoBehaviour + MaterialPropertyBlock 駆動
棄却理由: intent.md の動的軸が「静的」と明記されており、Volume の Weight/Inspector 運用で十分。かつ `Sirius.PostProcessing` の既存 3 エフェクトは全て Volume 駆動であり、本機能だけ MonoBehaviour 経路にする構造的必然性がない（既存原則との異質性が唯一の理由になってしまい、客観的な採用理由が作れない）。

### Plan C: 2 パス構成（オフセット計算 RT を先に生成 → 合成パスで適用）
棄却理由: 既存 3 エフェクトは全てテクセルごとに 1 回の `_BlitTexture` サンプルで完結する単一 Blit パス。本機能もテクセルごとに「深度 1 回サンプル → ノイズ 1〜2 回サンプル → オフセット後に 1 回サンプル」で完結でき、追加 RT を経由する理由がない。RenderGraph のリソース数・UoW 数が増えるだけで既存原則から逸脱する。

## Units of Work

### UoW#1 [Volume] 依存なし
- 対象: `SiriusPackages/Sirius.PostProcessing/Runtime/Scripts/Volumes/HeatDistortionVolume.cs`（新規）
- 追加/変更ファイル: 上記 1 ファイル
- 依存: なし
- 担当: AI
- コミット先: SiriusPackages

### UoW#2 [RenderPass] 依存: UoW#1
- 対象: `SiriusPackages/Sirius.PostProcessing/Runtime/Scripts/Features/Passes/HeatDistortionRenderPass.cs`（新規）
- 追加/変更ファイル: 上記 1 ファイル
- 依存: UoW#1（`HeatDistortionVolume` の型・プロパティを参照するため）
- 担当: AI
- コミット先: SiriusPackages

### UoW#3 [Shader] 依存: UoW#1（プロパティ命名を合わせるため）
- 対象: `SiriusPackages/Sirius.PostProcessing/Runtime/Shaders/HeatDistortion.shader`（既存スタブを編集）
- 追加/変更ファイル: 上記 1 ファイル。**Shader 名 `Hidden/Sirius/HeatDistortion` は既存スタブのまま維持**（他 3 シェーダーは `...Pass` サフィックス付きだが、本ファイルは既存スタブ命名を尊重しリネームしない）
- 依存: UoW#1
- 担当: AI
- コミット先: SiriusPackages

### UoW#4 [Feature配線] 依存: UoW#2
- 対象: `SiriusPackages/Sirius.PostProcessing/Runtime/Scripts/Features/SiriusPostProcessingFeature.cs`
- 追加/変更ファイル: `allowHeatDistortionPostProcess` フィールド＋`[AllowFlag]` ペア追加、`Create()`/`AddRenderPasses()`/`Dispose()` へ配線
- 依存: UoW#2
- 担当: AI
- コミット先: SiriusPackages

### UoW#5 [答えドキュメント] — ❌ **撤回（Phase 3 品質チェック時にユーザー指摘で判明した Phase 2 の誤り）**

当初「`docs/workshop/answers/` へ `HeatDistortionVolume.cs` / `HeatDistortionRenderPass.cs` / `HeatDistortion.shader` の答え一式を追加する」としていたが、これは **`answers/` の役割を取り違えた誤り**だったため撤回し、実装済みだった 3 ファイルとそのコミットを削除した。

**誤りの根拠:**
- `docs/workshop/answers/` は **ワーク①〜③の「答え合わせ」用**ディレクトリ。README.md の「各ワークの『答え』は `docs/workshop/answers/` にあります」という注記は、ワーク①〜③のフロー説明（「② AI が『正しそうに見える』コードを作る（ここに不具合が仕込まれています）→ ③ 受講者が見つける → ④ 答え合わせ」）の直後に置かれている。README_DEVELOPERS.md も `RotationBlur.shader` / `DirectionalBlur.shader` を「正解コード」として参照している
- ワーク④について README は「こちらには**仕込みの不具合はありません**。AI が書いたコードが正しいかどうかを、あなた自身が確かめてください」と明記しており、**正解コードが存在しない**。受講者ごとに実装が異なるのが前提
- したがって自分の実装を「答え」として `answers/` に置くのは筋が違う。fork 元への PR に含めると、受講者が講師側のコンテンツを上書きする形になる
- 加えてパッケージ実体との二重管理になる（実際、`_intensity` 既定値の修正を 2 箇所に入れる必要が生じた）

**Phase 2 での誤推論:** 「RotationBlur（ワーク③）の答えが Volume / RenderPass / Shader の 3 ファイルあるから、同じくゼロから実装するワーク④も 3 ファイル構成にする」と考えたが、RotationBlur に答えがあるのは**ワーク③が仕込みバグを探す演習で正解が一意に定まるから**であって、ファイル数の問題ではなかった。

### UoW#6 [デモ配線] 依存: UoW#1〜4
- 対象: `Assets/Settings/UniversalRenderPipelineAsset_Renderer.asset`（`allowHeatDistortionPostProcess: 1` 追加）、`Assets/Demo/Workshop_HeatDistortion/Global Volume Profile.asset`（`HeatDistortionVolume` を `components` に追加し、`NoiseTexture` に `3DCells64Sheet` を割り当て）
- 追加/変更ファイル: 上記 2 ファイル
- 依存: UoW#1〜4（Volume/Pass/配線が存在しないと Inspector 上で選択できない）
- 担当: **人間（Unity Editor 上での操作）** — 本スキルの既定方針（Phase 4 相当の Unity 手動設定はユーザーに案内する）に従う。自動配線が必要な場合は `workshop-rotation-blur-setup` に相当する専用スキルを別途作成できるが、本 Plan のスコープ外（ユーザーが明示的に依頼した場合のみ）
- コミット先: ルート直下（`Assets/`）

## 並列可能ペア

- ~~UoW#5（答えドキュメント）‖ UoW#4（Feature配線）~~ — UoW#5 は撤回したため該当なし
- UoW#1〜4 は基本的に直列（Volume→Pass→Shader命名確定→Feature配線）。本 Construction では並列実行はしない方針だが、依存関係の理解のため明示

## 触ってはいけないファイル

- `*.meta` 全般（既存 GUID 維持、新規は Unity Editor 任せ）
- `allowRadialBlurPostProcess` / `allowDirectionalBlurPostProcess` / `allowRotationBlurPostProcess`（既存 3 エフェクトの allow フラグ値）
- `docs/workshop/answers/` 配下すべて — ワーク①〜③の「答え合わせ」用ディレクトリであり、**ワーク④（陽炎）は仕込みバグのない新規実装ワークなので正解コードが存在せず、本機能のファイルは一切追加しない**（当初 UoW#5 として追加していたが撤回。詳細は UoW#5 の項を参照）
- `Assets/Tests/Runtime/AverageTest.cs` への `Workshop_HeatDistortion` テストケース追加 — **明示的に対象外**。同ファイル 113 行目のコメント「ワーク④（陽炎）・ワーク⑤（光芒）はゼロからの新規実装ワークのため、ビジュアル回帰テストの対象外」により、本機能は AverageTest の新規 TestCase を追加しない。品質ゲートは「コンパイル 0 エラー」「既存 3 ケース（RadialBlur/DirectionalBlur/RotationBlur）に回帰がないこと」「人手での Unity Editor 上の目視確認」に読み替える
- サブモジュールポインタ（該当があれば）
- `Assets/Demo/Workshop_HeatDistortion/*.mat`（`Mat_Near`/`Mat_Mid`/`Mat_Far`/`Mat_Ground` — 深度テスト用に既に配置済みのため変更不要）

## PR 構成

単一リポジトリ（サブモジュールなし）のため PR は 1 本。

**ブランチ方針（Phase 3 で確定）**: 現行ブランチ `feat/rotation-blur` には未マージの RotationBlur コミット（ワーク③）が乗っており、`SiriusPostProcessingFeature.cs` と `Assets/Settings/UniversalRenderPipelineAsset_Renderer.asset` の 2 ファイルを本機能と共有している。ユーザー判断により **RotationBlur は放置（マージしない）** となったため、`origin/main` からブランチを作成し、この 2 ファイルは「`origin/main` の内容 + HeatDistortion の追加分のみ」に再構成する（RotationBlur の配線は含めない）。

ブランチ名は **`feat/heatDistortion`**（README ワーク④ step-1 の指定プロンプトの文言に合わせる。当初 plan には `feat/heat-distortion` と書いていたが、README の指定を優先した）。PR の提出先は fork 元 `CyberAgentGameEntertainment/GraphicsAcademyLight` の `main`。

コミットは性質ごとに分割（Phase 3 実施結果）:

1. パッケージ実装コミット（UoW#1〜4）— `SiriusPackages/Sirius.PostProcessing/`（Volume / RenderPass / Shader / Feature 配線 + 新規 `.meta`）
2. パッケージドキュメントコミット — `SiriusPackages/Sirius.PostProcessing/.claude/skills/ct-pkg-sirius-postprocessing/SKILL.md`（Phase 3 Step 7-2。パス一覧への追加と陽炎固有の注意点）
3. デモ配線・アセットコミット（UoW#6）— `Assets/Settings/UniversalRenderPipelineAsset_Renderer.asset`, `Assets/Demo/Workshop_HeatDistortion/Global Volume Profile.asset`, `SiriusAssets/Sirius.Core.Assets/Assets/3DCells64Sheet.png.meta`（Texture3D → Texture2DArray 変更。本機能に必須）
4. AI-DLC ドキュメントコミット — `docs/ai-dlc/2026-09-05-heat-distortion/`（intent.md / plan.md）

※ 当初 2 番目に置いていた「答えドキュメントコミット（UoW#5）」は撤回した（UoW#5 の項を参照）。

**PR に含めない作業ツリー上の変更**（`git add` はファイル名を明示して除外する）:
`.claude/settings.json`, `ProjectSettings/GraphicsRegressionTestSettings.asset`, `ProjectSettings/ShaderGraphSettings.asset`, `ProjectSettings/TimelineSettings.asset`, `ProjectSettings/Packages/com.unity.testtools.codecoverage/Settings.json`, `.vsconfig`(未追跡), `GraphicsAcademyLight.slnx`(未追跡)
