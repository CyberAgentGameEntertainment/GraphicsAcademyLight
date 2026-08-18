# RotationBlur 実装プラン（第一稿）

## シェーダー: `RotationBlur.shader`

**シェーダー名:** `Hidden/Sirius/RotationBlurPass`

**プロパティ:**

```
_RotationBlurCenterX  : Float, default 0.5
_RotationBlurCenterY  : Float, default 0.5
_RotationBlurStrength : Float, default 1.0
_RotationBlurWidth    : Float, default 1.0
_RotationBlurMask     : 2D, "white"
```

**uniform 宣言:** すべて `float` 精度で宣言する（モバイル向け最適化は本実装では省略）

**defines:**

```hlsl
#define ROTATION_BLUR_SAMPLING_COUNT 6
#define ROTATION_BLUR_SAMPLING_OFFSET(i) ((i - 3) * 0.02 + 0.01)
```

**frag 処理（順番通りに実装）:**

1. `src_color`（half4）と `mask`（half）をサンプリング
2. 中心と方向ベクトルを計算:
   ```hlsl
   float2 center = float2(_RotationBlurCenterX, _RotationBlurCenterY);
   float2 d = float2(IN.texcoord) - center;
   float dist = length(d);
   ```
3. 接線方向（UV 空間での垂直ベクトル）を計算:
   ```hlsl
   float2 tangent = float2(-d.y, d.x);
   float2 tangent_dir = tangent / max(dist, 1e-5);
   ```
4. `ZERO_INITIALIZE(half4, blur_color)` で初期化し、`UNITY_UNROLL` で 6 回サンプリング:
   ```hlsl
   UNITY_UNROLL
   for (int n = 0; n < ROTATION_BLUR_SAMPLING_COUNT; n++)
   {
       float displacement = ROTATION_BLUR_SAMPLING_OFFSET(n);
       blur_color += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp,
           IN.texcoord + half2(tangent_dir) * displacement * dist * _RotationBlurWidth * mask);
   }
   blur_color *= 1.0 / ROTATION_BLUR_SAMPLING_COUNT;
   ```
5. 合成（距離ベースでブレンド）:
   ```hlsl
   float blur_strength = max(1e-5, _RotationBlurStrength);
   float t = SafePositivePow_half(dist * 1.414, rcp(blur_strength));
   return lerp(src_color, blur_color, saturate(half(t)));
   ```

## Volume: `RotationBlurVolume.cs`

`RadialBlurVolume.cs` をベースに以下のプロパティを定義:

| プロパティ | 型 | デフォルト |
|---|---|---|
| CenterX | ClampedFloatParameter | 0.5f (0〜1) |
| CenterY | ClampedFloatParameter | 0.5f (0〜1) |
| Strength | ClampedFloatParameter | 0.0f (0〜10) |
| Width | ClampedFloatParameter | 0.8f (0〜10) |
| Mask | TextureParameter | null |

`IsActive()`: `Strength > 0.0f && Width > 0.0f`

## RenderPass: `RotationBlurRenderPass.cs`

`RadialBlurRenderPass.cs` をベースに RotationBlur 向けに名称変更。
Shader 名: `Hidden/Sirius/RotationBlurPass`

## SiriusPostProcessingFeature への組み込み

RadialBlur の AllowFlag パターンを参照:
- SerializeField: `[SerializeField] private bool allowRotationBlurPostProcess;`
- AllowFlag: `[AllowFlag("allowRotationBlurPostProcess")] private RotationBlurRenderPass _rotationBlurRenderPass;`
- `Create()` / `AddRenderPasses()` / `Dispose()` に RadialBlur と同様の処理を追加
