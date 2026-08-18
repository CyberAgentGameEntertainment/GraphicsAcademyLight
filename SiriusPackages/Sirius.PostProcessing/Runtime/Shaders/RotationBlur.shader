Shader "Hidden/Sirius/RotationBlurPass"
{
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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            half4 frag(const Varyings IN) : SV_Target
            {
                // ワークショップ演習 Part 2: RotationBlur
                // /workshop-ai-dlc rotation-blurを実装 を実行してここを実装してください
                return half4(1.0h, 0.0h, 0.0h, 1.0h);
            }

            ENDHLSL
        }
    }
}
