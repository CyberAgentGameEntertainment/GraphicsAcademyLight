Shader "GodRay/GodRayRadialBlur"
{
    SubShader
    {
        Pass
        {
            Name "Radial Blur"

            Blend Off
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
                // ワークショップ ワーク⑤: 光芒（GodRay）— 2段目「光源へ向かって放射状にブラー」以降
                // /ct-ai-dlc 光芒（GodRay）を実装 を実行してここを実装してください
                //
                // 放射状ブラー → 合成をどうパス分割するかは自分で設計します（Pass を足してください）。
                // 光源のスクリーン投影は GodRayHelper、テンポラルの履歴バッファは
                // GodRayBlurHistory に用意してあります。
                return half4(1.0h, 0.0h, 0.0h, 1.0h);
            }

            ENDHLSL
        }
    }
}
