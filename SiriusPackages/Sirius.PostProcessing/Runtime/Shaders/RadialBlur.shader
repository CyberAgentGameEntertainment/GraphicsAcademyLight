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

            // #pragma enable_d3d11_debug_symbols

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            // モバイル最適化前: すべての変数が float（ワークショップ演習の出発点）
            uniform float _RadialBlurGazePositionX;
            uniform float _RadialBlurGazePositionY;
            uniform float _RadialBlurStrength;
            uniform float _RadialBlurWidth;
            uniform float _RadialBlurOffset;

            TEXTURE2D(_RadialBlurMask);

            // sqrt(0.5)=0.7071: 画面中央から画面端（斜め距離）までの距離を最大距離とする
            #define RADIAL_BLUR_MAX_DISTANCE 0.7071
            #define RADIAL_BLUR_SAMPLING_COUNT 6
            #define RADIAL_BLUR_SAMPLING_OFFSET(i) ((i - 3) * 0.02 + 0.01)

            float4 frag (const Varyings IN) : SV_Target
            {
                // Texture Sampling
                const float4 src_color = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, IN.texcoord);
                const float mask = SAMPLE_TEXTURE2D(_RadialBlurMask, sampler_LinearClamp, IN.texcoord).r;

                // Vector: 注視点からピクセルへの方向（注視点中心のブラー）
                const float2 gaze_position = float2(_RadialBlurGazePositionX, _RadialBlurGazePositionY);
                const float2 direction = gaze_position - float2(IN.texcoord);
                const float distance = length(direction);
                const float normalized_distance = distance * rcp(RADIAL_BLUR_MAX_DISTANCE) * mask;
                const float2 distanced_direction = direction * rcp(max(distance, 1e-5));

                // Blur
                float4 ZERO_INITIALIZE(float4, blur_color);
                UNITY_UNROLL
                for (int n = 0; n < RADIAL_BLUR_SAMPLING_COUNT; n++)
                {
                    const float displacement = RADIAL_BLUR_SAMPLING_OFFSET(n) + _RadialBlurOffset;
                    blur_color += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp,
                        float2(IN.texcoord) + distanced_direction * displacement * normalized_distance * _RadialBlurWidth);
                }
                blur_color *= rcp(RADIAL_BLUR_SAMPLING_COUNT);

                // Composite
                const float blur_strength = max(1e-5, _RadialBlurStrength);
                const float t = SafePositivePow_float(normalized_distance, 0.5h) * blur_strength;   
                return lerp(src_color, blur_color, saturate(t));
            }

            ENDHLSL
        }
    }
}
