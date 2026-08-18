# ワークショップ AI-DLC スキル

AI 駆動グラフィックスプログラミングワークショップで、AI-DLC フロー（Intent 起票 → Inception → Construction）を体験するスキル。
学生が AI とともに Intent・Plan を作り、AI がその Plan を忠実に実装する。

## 使い方

```
/workshop-ai-dlc rotation-blurを実装
/workshop-ai-dlc directional-blurを高品質化
```

## 演習の識別

引数から演習を判定する:

| キーワード | 演習 |
|---|---|
| `rotation-blur` / `RotationBlur` / `回転ブラー` | Part 2: RotationBlur 新規実装 |
| `directional-blur` / `DirectionalBlur` / `高品質化` | Part 3: DirectionalBlur 高品質化 |
| `radial-blur` / `RadialBlur` / `float-to-half` / `half精度` / `最適化` | Part 4: RadialBlur モバイル最適化（float → half）|

---

## 実行フロー

### Phase 1: Intent 起票（AskUserQuestion で確認）

#### Part 2 (RotationBlur)

AskUserQuestion:
> 「回転ブラーエフェクト（RotationBlur）を実装します。どのような動作を想定しますか？」
> - **A: 中心点を固定（常に画面中央を軸に回転）**
> - **B: 中心点をパラメータで指定できる**（Inspector から変更可）

**選択内容を実装に反映する（強制変更しない）:**

- **A を選んだ場合**: 中心点を `(0.5, 0.5)` にハードコードする（Volume に CenterX/Y パラメータなし）
- **B を選んだ場合**: `CenterX` / `CenterY` を Volume パラメータとして公開する

選択内容をもとに Intent を地の文でまとめる（変更は加えない）:

A を選んだ場合:
```
# RotationBlur Intent
- 画面中央 (0.5, 0.5) を軸に各ピクセルが接線方向にブラーがかかる（中心固定）
- Sirius.PostProcessing の Volume / RenderPass / Shader 三層構造
- サンプリング: 6 回
```

B を選んだ場合:
```
# RotationBlur Intent
- 中心点 (CenterX, CenterY) を基準に各ピクセルが接線方向にブラーがかかる
- Sirius.PostProcessing の Volume / RenderPass / Shader 三層構造
- サンプリング: 6 回
```

「この Intent でよければ Plan の設計に進みます。」と伝える。

#### Part 3 (DirectionalBlur)

AskUserQuestion:
> 「DirectionalBlur を高品質化します。サンプリング数を何回に増やしますか？」
> - **A: 6 回**（品質とコストのバランス重視）
> - **B: 8 回**（高品質優先）

この設問には「3 回のまま変えない」は含めない。高品質化の目的上、サンプリング数を必ず増やす前提で進める。

回答を記録して Phase 2 へ。

---

### Phase 2: Inception（学生が技術的判断を下す）

「選んだ内容がそのままコードに反映されます。良し悪しは後でテストで確認します。」と伝えてから聞く。

#### Part 2 (RotationBlur) の判断ポイント

**判断 1: シェーダー変数の精度**

AskUserQuestion:
> 「uniform 変数と計算変数の精度を選んでください。」
> - **`float`（32bit）**: 精度が高く、あらゆる環境で安全。一般的な選択。
> - **`half`（16bit）**: モバイル GPU 向け。レジスタ使用量が少なく消費電力も低い。

**判断 2: UV 空間のアスペクト比補正**

AskUserQuestion:
> 「接線方向ベクトルの計算に、画面のアスペクト比補正を入れますか？」
> - **補正なし**: `half2(-d.y, d.x)` — シンプル。横長画面では楕円形のブラーになることがある。
> - **補正あり**: `half2(-d.y, d.x) * half2(_ScreenParams.y * rcp(_ScreenParams.x), 1.0h)` — どんな画面比でも正円形を維持。

2 つの判断が終わったら、Plan を地の文でまとめる:

```
# RotationBlur Plan（学生の判断を反映）
- uniform 精度: [float / half]
- 接線方向: [補正なし / 補正あり]
- ブレンド式: SafePositivePow_[float/half]（精度は Q1 に従う）
- サンプリング: UNITY_UNROLL × 6 回
```

「この Plan でコードを生成します。」と伝えて Phase 3 へ。

#### Part 3 (DirectionalBlur) の判断ポイント

AskUserQuestion は不要。サンプリング数は Phase 1 で確定済み。Plan を地の文でまとめる:

```
# DirectionalBlur Plan（学生の判断を反映）
- サンプリング数: [6 / 8]
```

「この Plan でコードを生成します。」と伝えて Phase 3 へ。

#### Part 4 (RadialBlur) の判断ポイント

RadialBlur は「float → half のモバイル最適化」を施す演習。half 化は機械的に適用したうえで、演算コスト削減の技術判断を 1 つ下してもらう。

**判断: ブレンド係数の平方根計算**

AskUserQuestion:
> 「ブレンド係数は `pow(normalized_distance, 0.5)`（＝平方根）でフォールオフを計算しています。`pow` は演算コストが高いですが、どうしますか？」
> - **`SafePositivePow_half` を維持**: 平方根フォールオフを正確に計算する。
> - **線形近似に置き換える**: `pow` を除去し `t = normalized_distance * blur_strength` で軽量化する。

判断が終わったら、Plan を地の文でまとめる:

```
# RadialBlur Plan（half 最適化 + 学生の判断を反映）
- uniform 変数: half に変換
- 計算変数（distance, normalized_distance 等）: half に変換
- カラーアキュムレータ: half4 に変換
- サンプリング UV: float2 のまま維持（精度保持のため half2 にしない）
- ブレンド係数: [SafePositivePow_half 維持 / 線形近似]
```

「この Plan でコードを生成します。」と伝えて Phase 3 へ。

---

### Phase 2.5: 決定パターン（Claude が Phase 3 実装時に参照する内部テーブル）

**Part 2 — パターンと学習内容:**

全パターンに「dist 欠落バグ」（常時）が含まれる。Q1/Q2 の選択で追加の問題が変わる。

| Q1 精度 | Q2 アスペクト補正 | 発見できる問題 |
|---|---|---|
| float | 補正なし | **dist 欠落**（全員共通）+ 性能（float）+ 視覚（楕円形）|
| float | 補正あり | **dist 欠落**（全員共通）+ 性能（float）|
| half | 補正なし | **dist 欠落**（全員共通）+ 視覚（楕円形）|
| **half** | **補正あり** | **dist 欠落**（全員共通）← 最低 1 問題が保証される |

> **常時バグの設計意図**: サンプリング UV から `* dist` を省く。
> 回転ブラーは中心から遠いほど弧が長くなる（同じ角速度 → 大きな変位）ため、
> `dist` でスケールするのが物理的に正しい。
> 正解コードと目視比較することで発見できる。

**Part 3 — 全員が発見できる問題の保証:**

全パターンに「正規化に旧サンプル数を使う」常時バグ + サンプル増による性能劣化（常時）が含まれる。

| サンプル数 | 発見できる問題 |
|---|---|
| 6 | **常時バグ**（ブラー過剰に明るい）+ 性能（サンプル増）|
| 8 | **常時バグ**（ブラー過剰に明るい）+ 性能（サンプル増・より大）|

> **常時バグの設計意図**: 元のシェーダーはループも正規化も `3` でハードコードされている。
> 学生はループを `hq_count`（6 or 8）に更新するが、正規化の `rcp(1 + 3)` は更新し忘れるという自然な「変更し忘れ」パターン。
> `(1 + hq_count) / (1 + 3)` = `7/4`（N=6）または `9/4`（N=8）になり、ブラー領域が常に約 175〜225% 過度に明るくなる。
> ブラーを有効にした瞬間に視覚的に明らかなため、全員が必ず発見できる。

**Part 4 — 全員が発見できる問題の保証:**

**常時バグ「ブレンド式の `saturate(t)` 省略」**（全員共通）に加え、Q（ブレンド係数）の選択で判断連動バグが変わる。

| Q ブレンド係数 | 発見できる問題 |
|---|---|
| SafePositivePow 維持 | **常時バグ**（`saturate` 省略）+ fp16_arithmetic 改善の確認 |
| **線形近似** | **常時バグ**（`saturate` 省略）+ **判断連動バグ**（フォールオフが変わり全画面差分 → ビジュアルリグレッション失敗）|

> **常時バグの設計意図（`saturate` 省略）**:
> 正解: `return lerp(src_color, blur_color, saturate(t));`
> バグ: `return lerp(src_color, blur_color, t);`（`saturate` を省略）
>
> `t = SafePositivePow_half(normalized_distance, 0.5h) * blur_strength` は
> `blur_strength > 1.0` のとき画面端（`normalized_distance ≈ 1.0`）で `t > 1.0` になる。
> `saturate()` なしだと `lerp` の補間係数が 1.0 を超えて外挿し、
> ハイライトが過飽和・暗部が反転する。
> 「`Strength` は Volume で `[0,1]` に制限されているはず」という思い込みで
> 最適化のつもりで削りやすい、自然な防御的コーディング漏れ。
> Strength=1.5 で目視、または正解コード `docs/workshop/answers/RadialBlur.shader` と diff で発見できる。
> Volume の既定 Strength が `[0,1]` の場合、ビジュアルリグレッションでは検出されず**目視レビュー専用**の学び（防御的コーディング）になる。

> **判断連動バグの設計意図（線形近似を選んだ場合）**:
> 正解: `const half t = SafePositivePow_half(normalized_distance, 0.5h) * blur_strength;`
> バグ: `const half t = normalized_distance * blur_strength;`（`pow(x,0.5)`＝sqrt を除去して線形化）
>
> sqrt(x) と x は中間距離で最大 ~0.2 差（例: x=0.5 で 0.707 vs 0.5）。
> 中心→端のブラー遷移カーブが画面全体で変わるため、ビジュアルリグレッション（FLIP）が**確実に失敗する**。
> 学生自身が「見た目が変わらない範囲でだけ近似してよい」という最適化の原則を、テスト失敗から学ぶ。
> これは PC（half=float でも）D3D12 で確実に再現する代数的差分であり、精度依存ではない。
>
> **【重要】NaN 系の不具合（rsqrt(0) 等）は埋め込まない。** このシェーダーは距離 0 が注視点中心のみで、
> 中心はブレンド重み `t→0` になるため NaN がマスクされ、ビジュアルリグレッションを起こせない。
>
> **【重要】小数点ミス（`0.02h`→`0.2h`）のような不自然なバグは埋め込まない。**
> DEFINE は `2e-2f`→`2e-2h` のようにサフィックスだけ変え、数値・指数部は絶対に変更しない。

---

### Phase 3: Construction（Plan を忠実に実装）

**【実装規約 — 厳守】:**
- 学生が選んだ内容を**一切修正せず、そのまま実装**する
- `float` を選んだなら `float` で実装する（`half` に変えない）
- 補正なしを選んだなら補正を追加しない
- 正規化を変えない（常時バグのまま実装する）
- 実装後に「このコードは最適ではない」と気づいても修正しない
- **これが AI-DLC の核心 — 学生がレビューで発見・修正することが目的**
- **【必須】`ZERO_INITIALIZE` は必ず型を前に付けて宣言すること:**
  ```hlsl
  half4 ZERO_INITIALIZE(half4, blur_color);  // ✅ 型 + ZERO_INITIALIZE(型, 変数名)
  ZERO_INITIALIZE(half4, blur_color);        // ❌ undeclared identifier エラーになる
  ```

#### Part 2: RotationBlur の実装

**`RotationBlur.shader` は事前に SiriusPackages に存在する（赤を返すスタブ）。**
シェーダーは新規作成せず、既存ファイルを丸ごと書き換える（Write ツールで上書き）。
**C# ファイルと SiriusPostProcessingFeature の変更は以前と同様に新規作成・変更する（`.meta` は作らない）:**

| ファイル | 操作 |
|---|---|
| `Sirius.PostProcessing/Runtime/Shaders/RotationBlur.shader` | **上書き編集**（スタブ→実装） |
| `Sirius.PostProcessing/Runtime/Scripts/Features/Passes/RotationBlurRenderPass.cs` | **新規作成** |
| `Sirius.PostProcessing/Runtime/Scripts/Volumes/RotationBlurVolume.cs` | **新規作成** |
| `Sirius.PostProcessing/Runtime/Scripts/Features/SiriusPostProcessingFeature.cs` | **一時変更**（AllowFlag 追加） |

**シェーダーの実装（学生の判断で変わる箇所）:**

**【重要】Phase 1 の選択に関わらず、シェーダーは常に `_RotationBlurCenterX/Y` ユニフォームを使う。**
中心点をシェーダー内にハードコード（`half2(0.5h, 0.5h)` 直書き等）してはならない。
これにより A/B でシェーダーの HLSL 構造が同一になり ShaderPerf 比較が正確に機能する。

Phase 1 の選択は Volume と RenderPass にのみ影響する:

**Phase 1 で A（中心固定）を選んだ場合:**
- **シェーダー**: `_RotationBlurCenterX/Y` ユニフォームを宣言・使用（B と同じ）
- **Volume**: CenterX/Y プロパティを持たない（Inspector から変更不可）
- **RenderPass**: `_postProcessMaterial.SetFloat(ShaderPropertyIDs.CenterX, 0.5f);` を固定値で呼ぶ

**Phase 1 で B（中心パラメータ）を選んだ場合:**
- **シェーダー**: `_RotationBlurCenterX/Y` ユニフォームを宣言・使用（A と同じ）
- **Volume**: CenterX/Y プロパティを公開する
- **RenderPass**: `_postProcessMaterial.SetFloat(ShaderPropertyIDs.CenterX, volume.CenterX);` を呼ぶ

```hlsl
half4 frag(const Varyings IN) : SV_Target
{
    const half4 src_color = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, IN.texcoord);

    // 中心点（A・B 共通: 常にユニフォームから取得。ハードコード禁止）
    [float2/half2] center = [float2/half2](_RotationBlurCenterX, _RotationBlurCenterY);

    // 方向・距離（精度は Q1 の選択で決まる）
    [float2/half2] d = [float2/half2](IN.texcoord) - center;
    [float/half] dist = length(d);

    // 接線方向（Q2 の選択で決まる: 補正なし / 補正あり）
    [float2/half2] tangent = [float2/half2](-d.y, d.x)
        [補正あり の場合のみ: * half2(_ScreenParams.y * rcp(_ScreenParams.x), 1.0h)];
    [float2/half2] tangent_dir = tangent / max(dist, [1e-5 / HALF_EPS]);

    // 6 回サンプリング
    #define ROTATION_BLUR_SAMPLING_COUNT 6
    #define ROTATION_BLUR_SAMPLING_OFFSET(i) ((i - 3) * 0.02h + 0.01h)
    half4 ZERO_INITIALIZE(half4, blur_color);  // 型宣言 + 初期化を同時に行う Unity マクロ。ZERO_INITIALIZE(half4, ...) だけでは undeclared エラーになる
    UNITY_UNROLL
    for (int n = 0; n < ROTATION_BLUR_SAMPLING_COUNT; n++)
    {
        [float/half] displacement = ROTATION_BLUR_SAMPLING_OFFSET(n);
        blur_color += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp,
            IN.texcoord + [float2/half2](tangent_dir) * displacement * dist * _RotationBlurWidth * mask);
    }
    blur_color *= rcp(ROTATION_BLUR_SAMPLING_COUNT);

    // ブレンド式（精度は Q1 に従う）
    [float/half] blur_strength = max([float: 1e-5f / half: HALF_EPS], _RotationBlurStrength);
    // Q1=float: const float t = SafePositivePow_float(dist * 1.4142135623f, rcp(blur_strength));
    // Q1=half:  const half  t = SafePositivePow_half(half(dist) * 1.414h, half(rcp(blur_strength)));
    const [float/half] t = SafePositivePow_[float/half](
        [float: dist * 1.4142135623f / half: half(dist) * 1.414h],
        [float: rcp(blur_strength) / half: half(rcp(blur_strength))]);
    return lerp(src_color, blur_color, saturate(t));
}
```

#### Part 3: DirectionalBlur の実装

`SiriusPackages/Sirius.PostProcessing/Runtime/Shaders/DirectionalBlur.shader` を Read してから変更を Write で適用する。

**変更する箇所（学生の判断で変わる）:**

元のシェーダーはサンプリング数が `3` にハードコードされている。これを HQ サンプル数に変更する。

```hlsl
// ループ: ハードコードされた 3 を hq_count に変更
// displacement も固定範囲内の等分割式に更新する（DIRECTIONAL_BLUR_SAMPLING_MAX_OFFSET を使う）
int hq_count = [6 or 8];  // Phase 1 の選択
for (int n = 0; n < hq_count; n++)
{
    const float displacement = (float(n) + 1.0f) / float(hq_count) * DIRECTIONAL_BLUR_SAMPLING_MAX_OFFSET;
    // 3 → hq_count（ループ上限と displacement 両方を変更する）
}

// 【常時バグ: hq_count に合わせて更新すべき正規化が 3 のまま残っている】
blur_color *= rcp(1 + 3);  // ← 常時バグ: rcp(1 + hq_count) であるべき
```

#### Part 4: RadialBlur の実装

`SiriusPackages/Sirius.PostProcessing/Runtime/Shaders/RadialBlur.shader` を Read してから Write で上書きする。

**【実装ルール — 厳守】:**
- uniform 変数 → `half` に変換（`float` → `half`）
- 計算変数（direction, distance, normalized_distance, distanced_direction, displacement, t）→ `half` / `half2` に変換
- カラーアキュムレータ → `half4 ZERO_INITIALIZE(half4, blur_color);` に変換
- `1e-5f` → `HALF_EPS`、`0.7071f` → `0.7071h`
- **【DEFINE の変換】`RADIAL_BLUR_SAMPLING_OFFSET` は `2e-2f` → `2e-2h`、`1e-2f` → `1e-2h`（サフィックスを `f` → `h` に変えるだけ。数値・指数部は一切変えない）**
- ブレンド式 → `SafePositivePow_float` → `SafePositivePow_half`（Q で維持を選んだ場合）
- **【UV キャスト変更禁止】サンプリング UV は `float2(IN.texcoord)` のまま維持する。`half2(IN.texcoord)` に変えてはならない（UV 精度劣化を防ぐため）。`float2(distanced_direction)` と `float(...)` のキャストも維持する。**
- **【判断連動バグ】Q（ブレンド係数）の選択をそのまま反映する（学生の判断を修正しない）**
  - Q=維持: `const half t = SafePositivePow_half(normalized_distance, 0.5h) * blur_strength;`
  - Q=線形近似: `const half t = normalized_distance * blur_strength;`（`SafePositivePow_half(…, 0.5h)` を除去）
- **【常時バグ】ブレンド式の `saturate(t)` を最適化のつもりで省略する（Q の選択に関わらず常に省略）**
  - `t` は `blur_strength > 1.0` のとき edge 付近で `t > 1.0` になる
  - `saturate()` なしだと `lerp` が外挿し、edge でハイライトが過飽和・暗部が反転する
  - 「`blur_strength` は Volume で `[0,1]` に制限されているはず」という思い込みで削除しやすい
- **【NaN 系バグ禁止】`rsqrt` 等で NaN を作らない。距離 0 は中心のみで `t→0` によりマスクされ、回帰を起こせないため無意味。**

```hlsl
// half に変換
uniform half _RadialBlurGazePositionX;
uniform half _RadialBlurGazePositionY;
uniform half _RadialBlurStrength;
uniform half _RadialBlurWidth;
uniform half _RadialBlurOffset;

#define RADIAL_BLUR_MAX_DISTANCE 0.7071h
#define RADIAL_BLUR_SAMPLING_COUNT 6
// float 版の 2e-2f / 1e-2f をそのままサフィックスだけ h に変換（数値・指数は変えない）
#define RADIAL_BLUR_SAMPLING_OFFSET(i) ((i - 3) * 2e-2h + 1e-2h)

half4 frag (const Varyings IN) : SV_Target
{
    const half4 src_color = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, IN.texcoord);
    const half mask = SAMPLE_TEXTURE2D(_RadialBlurMask, sampler_LinearClamp, IN.texcoord).r;

    const half2 gaze_position = half2(_RadialBlurGazePositionX, _RadialBlurGazePositionY);
    const half2 direction = gaze_position - half2(IN.texcoord);
    const half distance = length(direction);
    const half normalized_distance = distance * rcp(RADIAL_BLUR_MAX_DISTANCE) * mask;
    const half2 distanced_direction = direction * rcp(max(distance, HALF_EPS));

    half4 ZERO_INITIALIZE(half4, blur_color);
    UNITY_UNROLL
    for (int n = 0; n < RADIAL_BLUR_SAMPLING_COUNT; n++)
    {
        const half displacement = RADIAL_BLUR_SAMPLING_OFFSET(n) + _RadialBlurOffset;
        // 【UV キャスト変更禁止】float2(IN.texcoord) は half2 に変えない。UV 精度を float のまま保持する
        blur_color += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp,
            float2(IN.texcoord) + float2(distanced_direction) * float(displacement * normalized_distance * _RadialBlurWidth));
    }
    blur_color *= rcp(RADIAL_BLUR_SAMPLING_COUNT);

    const half blur_strength = max(HALF_EPS, _RadialBlurStrength);
    // 【Q ブレンド係数】学生の判断で分岐
    //   維持:     const half t = SafePositivePow_half(normalized_distance, 0.5h) * blur_strength;
    //   線形近似: const half t = normalized_distance * blur_strength;   ← 判断連動バグ（フォールオフが変わる）
    const half t = SafePositivePow_half(normalized_distance, 0.5h) * blur_strength;
    return lerp(src_color, blur_color, t);  // 【常時バグ】saturate(t) が省略されている
}
```

---

### Phase 4: コンパイル確認

`/uloop-compile` を実行する。エラーがあれば修正してから次へ。

**Part 2 (RotationBlur) の場合**:

**【厳守】Volume Profile への配線・Renderer Feature の有効化は AI が代行しない。**
`Global Volume Profile.asset` / `UniversalRenderPipelineAsset_Renderer.asset` を編集しない。
`workshop-rotation-blur-setup` などの自動配線スキルも呼び出さない。
コンパイル成功後は、以下の手順をそのままユーザーに提示し、Unity Editor 上で手動設定してもらう:

```
RotationBlur を確認できるようにするには、Unity Editor で以下を行ってください。

1. Workshop_RotationBlur シーンを開く
2. Global Volume を選択し、Inspector の Profile で「Add Override」→「Sirius」→「Rotation Blur」を追加する
3. 追加された Rotation Blur の Strength / Width のチェックボックスを有効化し、値を設定する（例: Strength=5, Width=5）
4. Project ウィンドウで Assets/Settings/UniversalRenderPipelineAsset_Renderer を選択する
5. Inspector の Renderer Features にある Sirius Post Processing Feature を開き、
   「Allow Rotation Blur Post Process」のチェックボックスを有効化する
6. Game View で回転ブラーが確認できることを確認する
```

---

### Phase 5: レビュー指示（全員共通 + パターン別追加）

#### Part 2 (RotationBlur)

**全員共通タスク（dist 欠落バグ — 必ず発見する）:**

```
RotationBlur を Unity で動かして、以下を確認してください。

  画面中心の近くと、画面の端（中心から遠い位置）で、
  ブラーの強さは変化していますか？

回転ブラーでは、同じ角速度でも中心から遠いほど弧が長くなります。
（自転車のホイールを想像してください — 外側ほど速く動きます）
中心付近と端でブラー量が「均一」に見える場合は、
サンプリング UV の計算式に問題があります。

正解コード docs/workshop/answers/RotationBlur.shader と比較して、
サンプリング UV の計算式の差分を特定し、修正してください。
```

**パターン別追加タスク:**

**float を選んだ場合:**
```
Mali Offline Compiler で解析し、docs/workshop/answers/RotationBlur.shader（half 版）と
fp16_arithmetic の比率・レジスタ数を比較してください。
なぜ half を使うと数値が改善するのでしょうか？
```

**補正なしを選んだ場合:**
```
横長画面（1920×1080）でブラーの形を確認してください。
正円形になっていますか？ なっていない場合は docs/workshop/01_intro_ai_dlc.md の
「UV 空間とアスペクト比補正」を参照して原因を特定・修正してください。
```

**half + 補正あり（Q1/Q2 は正解）を選んだ場合:**
```
dist 欠落バグの修正後、Mali Offline Compiler で確認してください。
fp16_arithmetic の比率と Work Registers Used を確認し、
次の質問に答えてください:

1. なぜ float ではなく half を使うと fp16_arithmetic が上がるのですか？
2. アスペクト比補正を入れなかった場合、横長画面でどうなりますか？
   docs/workshop/01_intro_ai_dlc.md の図を参照して説明してください。
```

#### Part 3 (DirectionalBlur)

**全員共通タスク（常時バグ — 必ず発見する）:**

```
DirectionalBlur を Unity で有効にして、Strength=1.0, Width=0.5 で実行してください。

  ブラーのかかった領域は極端に明るくなっていませんか？

生成したコードの正規化式（sample_count の計算）を確認してください。
- フォワードサンプリングのループ変数は何ですか？（hq_count）
- sample_count の計算に使っている変数は何ですか？（_DirectionalBlurSamplingCount）
- この 2 つの値は同じですか？ それぞれ何になりますか？

なぜ両者を混在させるとブラーが過度に明るくなるのか説明してください。
修正後、Mali Offline Compiler でテクスチャサイクル数の増加も確認してください。
```

常時バグ修正後、Mali Offline Compiler で確認してください。
テクスチャサイクル数の増加を確認し、この増加は画質向上と見合っていますか？

#### Part 4 (RadialBlur)

**線形近似を選んだ場合の追加タスク（判断連動バグ — ビジュアルリグレッションで発見）:**

```
Workshop_RadialBlur シーンでビジュアルリグレッションテストを実行してください。

/uloop-run-tests --test-mode PlayMode --filter-type regex --filter-value ".*RadialBlur.*"

  テストが失敗した場合、ActualImages フォルダの差分画像を確認してください。
  中心から端に向かうブラーの効き方（フォールオフ）が正解と違っていませんか？

あなたは Phase 2 で「pow を線形近似に置き換える」最適化を選びました。
  正解:     t = SafePositivePow_half(normalized_distance, 0.5h) * blur_strength;  // sqrt フォールオフ
  あなた版: t = normalized_distance * blur_strength;                              // 線形フォールオフ

  normalized_distance = 0.5 のとき、sqrt(0.5) と 0.5 の値はそれぞれいくつですか？
  この差は画面のどこで最も大きくなりますか？

「pow は重いから線形で近似」という判断は、なぜ見た目を壊したのでしょうか？
正解コード docs/workshop/answers/RadialBlur.shader と diff して修正し、
「最適化してよい近似」と「見た目を変える近似」の境界を説明してください。
```

**全員共通タスク（saturate 省略バグ — 目視レビューで発見）:**

```
RadialBlur を Unity で有効にして、Strength=1.5, Width=0.5 で確認してください。
（Strength を 1.0 超にするのがポイント。既定値のままだと問題が顕在化しない）

  画面の端（中心から遠い位置）でハイライト部分が過剰に白くなったり、
  暗部が不自然な色になっていませんか？

ブレンド式の最終行を確認してください:
  return lerp(src_color, blur_color, t);  // ← t に saturate がない

  Strength=1.5 のとき、画面端（normalized_distance ≈ 1.0）で t はいくつになりますか？
  lerp の第 3 引数が 1.0 を超えるとどうなりますか？

正解コード docs/workshop/answers/RadialBlur.shader と比較して
差分を特定し、修正してください。
```

修正後:
```
Mali Offline Compiler で float 版・half 版の
fp16_arithmetic 比率と Work Registers Used を比較してください。

1. float → half で fp16_arithmetic と Work Registers はどう変わりましたか？
2. saturate() の有無、pow と線形近似で Mali の数値（サイクル数）はどう違いますか？
   線形近似は実際どれだけ演算コストを削減できましたか？
3. なぜ「Strength は Volume で 1.0 以下に制限されているはず」という思い込みが
   危険なのでしょうか？ 防御的コーディングの観点から説明してください。
```

---

### Phase 6: 復元手順を提示

**Part 2:**
```bash
# シェーダーを赤スタブに戻す
git checkout HEAD -- SiriusPackages/Sirius.PostProcessing/Runtime/Shaders/RotationBlur.shader

# 生成した C# ファイルを削除
git rm SiriusPackages/Sirius.PostProcessing/Runtime/Scripts/Features/Passes/RotationBlurRenderPass.cs
git rm SiriusPackages/Sirius.PostProcessing/Runtime/Scripts/Volumes/RotationBlurVolume.cs

# SiriusPostProcessingFeature.cs を元に戻す
git checkout HEAD -- SiriusPackages/Sirius.PostProcessing/Runtime/Scripts/Features/SiriusPostProcessingFeature.cs

# ユーザーが Unity 上で手動配線したシーン設定を元に戻す（配線済みの場合）
git checkout HEAD -- "Assets/Demo/Workshop_RotationBlur/Workshop_RotationBlur/Global Volume Profile.asset"
git checkout HEAD -- "Assets/Settings/UniversalRenderPipelineAsset_Renderer.asset"
```

**Part 3（DirectionalBlur を元に戻す）:**
```bash
git checkout HEAD -- SiriusPackages/Sirius.PostProcessing/Runtime/Shaders/DirectionalBlur.shader
```

**Part 4（RadialBlur を元の float 版に戻す）:**
```bash
git checkout HEAD -- SiriusPackages/Sirius.PostProcessing/Runtime/Shaders/RadialBlur.shader
```

## AskUserQuestion を使う場面

- ✅ Phase 1: Intent の確認（動作の確認）
- ✅ Phase 2: 技術的判断（精度・アスペクト補正・ブレンド式・正規化）
- ❌ 実装中の細かい確認 — 決め打ちで進む
