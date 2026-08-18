Shader "Hidden/Sirius/RadialBlurPass"
{
    Properties
    {
        _RadialBlurGazePositionX("Gaze Position X", Float) = 0.5
        _RadialBlurGazePositionY("Gaze Position Y", Float) = 0.5
        _RadialBlurStrength("Strength", Float) = 1.0
        _RadialBlurWidth("Width", Float) = 1.0
        _RadialBlurOffset("Offset", Float) = 0.0
        _RadialBlurMask("Mask", 2D) = "white" {}
    }
    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always
        LOD 100
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }
        Pass
        {
            Name "RadialBlur"
            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            // ✅ モバイル最適化済み: uniform は half に変換
            uniform half _RadialBlurGazePositionX;
            uniform half _RadialBlurGazePositionY;
            uniform half _RadialBlurStrength;
            uniform half _RadialBlurWidth;
            uniform half _RadialBlurOffset;

            TEXTURE2D(_RadialBlurMask);

            // sqrt(0.5)=0.7071
            #define RADIAL_BLUR_MAX_DISTANCE 0.7071h
            #define RADIAL_BLUR_SAMPLING_COUNT 6
            // ✅ float 版の 2e-2f / 1e-2f をそのままサフィックスだけ h に変換
            #define RADIAL_BLUR_SAMPLING_OFFSET(i) ((i - 3) * 2e-2h + 1e-2h)

            half4 frag (const Varyings IN) : SV_Target
            {
                // ✅ カラー変数は half4 に変換
                const half4 src_color = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, IN.texcoord);
                const half mask = SAMPLE_TEXTURE2D(_RadialBlurMask, sampler_LinearClamp, IN.texcoord).r;

                // ✅ UV 空間での方向・距離計算は half2/half に変換（0〜1 の範囲なので安全）
                const half2 gaze_position = half2(_RadialBlurGazePositionX, _RadialBlurGazePositionY);
                const half2 direction = gaze_position - half2(IN.texcoord);
                const half distance = length(direction);
                const half normalized_distance = distance * rcp(RADIAL_BLUR_MAX_DISTANCE) * mask;
                const half2 distanced_direction = direction * rcp(max(distance, HALF_EPS));

                // ✅ カラーアキュムレータは half4
                half4 ZERO_INITIALIZE(half4, blur_color);
                UNITY_UNROLL
                for (int n = 0; n < RADIAL_BLUR_SAMPLING_COUNT; n++)
                {
                    const half displacement = RADIAL_BLUR_SAMPLING_OFFSET(n) + _RadialBlurOffset;
                    // ✅ サンプリング UV は float2 のまま（IN.texcoord は float2）
                    blur_color += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp,
                        float2(IN.texcoord) + float2(distanced_direction) * float(displacement * normalized_distance * _RadialBlurWidth));
                }
                blur_color *= rcp(RADIAL_BLUR_SAMPLING_COUNT);

                // ✅ ブレンド計算も half。
                // ✅ pow(x, 0.5)=sqrt フォールオフを維持（線形近似は見た目を変えるので不可）。
                // ✅ saturate(t) は blur_strength > 1.0 のとき必須（外挿による過飽和・色反転を防ぐ）。
                const half blur_strength = max(HALF_EPS, _RadialBlurStrength);
                const half t = SafePositivePow_half(normalized_distance, 0.5h) * blur_strength;
                return lerp(src_color, blur_color, saturate(t));
            }

            ENDHLSL
        }
    }
}
