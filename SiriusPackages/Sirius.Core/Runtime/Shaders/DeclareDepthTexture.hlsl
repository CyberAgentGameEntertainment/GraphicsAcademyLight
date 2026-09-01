#ifndef SIRIUS_DECLARE_DEPTH_TEXTURE_INCLUDED
#define SIRIUS_DECLARE_DEPTH_TEXTURE_INCLUDED

// Siriusは_CameraDepthTexture_TexelSizeを宣言してて、2022.3.21からURPのDeclareDepthTexture.hlslにもそれが宣言されるようになって、重複宣言エラーとなった。
// しかし2022.3.21のマイナーバージョンがUNITY_VERSION判別できないらしく、この形で対応する
// - 2022以前はこのファイルで宣言したDepthTextureを使う
// - 2023以降はURPのDeclareDepthTexture.hlslを使う
#if UNITY_VERSION >= 202330
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
#else
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GlobalSamplers.hlsl"

TEXTURE2D_X_FLOAT(_CameraDepthTexture);
float4 _CameraDepthTexture_TexelSize;

float SampleSceneDepth(float2 uv)
{
    return SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_PointClamp, UnityStereoTransformScreenSpaceTex(uv)).r;
}

float LoadSceneDepth(uint2 uv)
{
    return LOAD_TEXTURE2D_X(_CameraDepthTexture, uv).r;
}
#endif

#endif
