Shader "GodRay/Binarization"
{
    SubShader
    {
        Pass
        {
            Name "Binarization"

            ZTest Off
            ZWrite Off
            Cull Off

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            half4 frag(const Varyings IN) : SV_Target
            {
                // ワークショップ ワーク⑤: 光芒（GodRay）— 1段目「明るいところだけを抜き出す（二値化）」
                // /ct-ai-dlc 光芒（GodRay）を実装 を実行してここを実装してください
                return half4(1.0h, 0.0h, 0.0h, 1.0h);
            }

            ENDHLSL
        }
    }
}
