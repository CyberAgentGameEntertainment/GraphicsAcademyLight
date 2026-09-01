Shader "Hidden/Sirius/HeatDistortion"
{
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }
        LOD 100
        ZWrite Off
        Cull Off
        ZTest Always

        Pass
        {
            Name "HeatDistortionPass"

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            half4 Frag(const Varyings input) : SV_Target
            {
                // ワークショップ ワーク④: 陽炎（HeatDistortion）
                // /ct-ai-dlc 陽炎（HeatDistortion）を実装 を実行してここを実装してください
                //
                // 画面の深度・3Dノイズ・視線方向マスクを使った歪みは自分で設計します。
                // 座標復元のヘルパは Sirius.Core に用意してあります:
                //   #include "Packages/jp.co.cyberagent.sirius.core/Runtime/Shaders/ScreenSpaceUtil.hlsl"
                return half4(1.0h, 0.0h, 0.0h, 1.0h);
            }

            ENDHLSL
        }
    }
}
