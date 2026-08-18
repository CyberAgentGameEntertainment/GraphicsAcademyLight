Shader "Hidden/Sirius/RotationBlurPass"
{
    Properties
    {
        _RotationBlurCenterX("Center X", Float) = 0.5
        _RotationBlurCenterY("Center Y", Float) = 0.5
        _RotationBlurStrength("Strength", Float) = 1.0
        _RotationBlurWidth("Width", Float) = 1.0
        _RotationBlurMask("Mask", 2D) = "white" {}
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
            Name "RotationBlur"
            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment frag

            // #pragma enable_d3d11_debug_symbols

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            uniform half _RotationBlurCenterX;
            uniform half _RotationBlurCenterY;
            uniform half _RotationBlurStrength;
            uniform half _RotationBlurWidth;

            TEXTURE2D(_RotationBlurMask);

            #define ROTATION_BLUR_SAMPLING_COUNT 6
            #define ROTATION_BLUR_SAMPLING_OFFSET(i) ((i - 3) * 0.02h + 0.01h)

            half4 frag(const Varyings IN) : SV_Target
            {
                // Texture Sampling
                const half4 src_color = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, IN.texcoord);
                const half mask = SAMPLE_TEXTURE2D(_RotationBlurMask, sampler_LinearClamp, IN.texcoord).r;

                // Direction from center to pixel (UV space)
                const half2 center = half2(_RotationBlurCenterX, _RotationBlurCenterY);
                const half2 d = half2(IN.texcoord) - center;
                const half dist = length(d);

                // Tangent direction with aspect ratio correction.
                // Multiplying by (height/width, 1.0) converts the perpendicular UV vector
                // to account for non-square screens, keeping the blur circular.
                const half2 tangent = half2(-d.y, d.x) * half2(_ScreenParams.y * rcp(_ScreenParams.x), 1.0h);
                const half2 tangent_dir = tangent * rcp(max(dist, HALF_EPS));

                // Blur: sample along tangent, scaled by distance from center
                half4 ZERO_INITIALIZE(half4, blur_color);
                UNITY_UNROLL
                for (int n = 0; n < ROTATION_BLUR_SAMPLING_COUNT; n++)
                {
                    const half displacement = ROTATION_BLUR_SAMPLING_OFFSET(n);
                    blur_color += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp,
                        IN.texcoord + tangent_dir * displacement * dist * _RotationBlurWidth * mask);
                }
                blur_color *= rcp(ROTATION_BLUR_SAMPLING_COUNT);

                // Composite: blend based on distance from center
                const half blur_strength = max(HALF_EPS, _RotationBlurStrength);
                const half t = SafePositivePow_half(dist * 1.414h, rcp(blur_strength));
                return lerp(src_color, blur_color, saturate(t));
            }

            ENDHLSL
        }
    }
}
