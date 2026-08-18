Shader "Hidden/Sirius/DirectionalBlurPass"
{
    Properties
    {
        [Enum(Off, 0, On, 1)] _DirectionalBlurUseFixedAspectSampling("Fixed Aspect Sampling", Int) = 0
        _DirectionalBlurNormalizedDirection("Direction", Vector) = (0, 0, 0, 0)
        _DirectionalBlurStrength("Strength", Float) = 1.0
        _DirectionalBlurWidth("Width", Float) = 1.0
        _DirectionalBlurSamplingCount("Sampling Count", Int) = 3
        _DirectionalBlurMask("Mask", 2D) = "white" {}
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
            Name "DirectionalBlur"
            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment frag

            // #pragma enable_d3d11_debug_symbols

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            uniform int _DirectionalBlurUseFixedAspectSampling;
            uniform half2 _DirectionalBlurNormalizedDirection;
            uniform half _DirectionalBlurStrength;
            uniform half _DirectionalBlurWidth;
            uniform int _DirectionalBlurSamplingCount;

            TEXTURE2D(_DirectionalBlurMask);

            // サンプルを均等分割する最大オフセット（サンプル数が増えても範囲は変わらず密度が増す）
            #define DIRECTIONAL_BLUR_SAMPLING_MAX_OFFSET 0.07h

            half4 frag (const Varyings IN) : SV_Target
            {
                // Texture Sampling
                const half4 src_color = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, IN.texcoord);
                const half mask = SAMPLE_TEXTURE2D(_DirectionalBlurMask, sampler_LinearClamp, IN.texcoord).r;

                // アスペクト比によって参照するテクセルを固定する場合の計算
                const half2 screen_aspect = half2(_ScreenParams.y / _ScreenParams.x, 1);
                const half2 fixed_aspect = _DirectionalBlurUseFixedAspectSampling ? screen_aspect : half2(1, 1);

                // Blur
                half4 blur_color = src_color;
                const int hq_count = 6;
                for (int n = 0; n < hq_count; n++)
                {
                    const half displacement = (half(n) + 1.0h) / half(hq_count) * DIRECTIONAL_BLUR_SAMPLING_MAX_OFFSET;
                    blur_color += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, IN.texcoord + displacement * _DirectionalBlurNormalizedDirection * _DirectionalBlurWidth * mask * fixed_aspect);
                }

                blur_color *= rcp(1 + hq_count);

                // Composite
                const half blur_strength = max(HALF_EPS, _DirectionalBlurStrength);
                return lerp(src_color, blur_color, saturate(blur_strength));
            }

            ENDHLSL
        }
    }
}