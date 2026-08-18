#ifndef _SIRIUS_CORE_SCREEN_SPACE_UTIL_HLSL_
#define _SIRIUS_CORE_SCREEN_SPACE_UTIL_HLSL_

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

#define FRAMEBUFFER_FETCH_ENABLED defined(SHADER_API_VULKAN) || defined(SHADER_API_METAL)

#if FRAMEBUFFER_FETCH_ENABLED
FRAMEBUFFER_INPUT_X_HALF(0);
#define FETCH_FRAMEBUFFER(tex, index, pos) LOAD_FRAMEBUFFER_X_INPUT(index, pos)
#else
#define FETCH_FRAMEBUFFER(tex, index, pos) LOAD_TEXTURE2D_X_LOD(tex, pos, 0)
#endif

half4 FetchFramebuffer(TEXTURE2D(tex), int2 pos)
{
    return FETCH_FRAMEBUFFER(tex, 0, pos);
}

float4 LoadSceneColor(TEXTURE2D(tex), int2 pos)
{
    return LOAD_TEXTURE2D_X_LOD(tex, pos, 0);
}

float4 SampleSceneColor(TEXTURE2D_PARAM(tex, samp), float2 uv)
{
    return SAMPLE_TEXTURE2D_X_LOD(tex, samp, uv, 0);
}

float3 TransformViewToScreenDir(float3 dirVS)
{
    float3 v = dirVS;
    if (abs(v.z) < 0.001)
    {
        v.xy *= 1000;
        v.z = 0;
    }
    else
    {
        v.z *= -1;
        v.xy = UNITY_MATRIX_P._m00_m11 * v.xy / v.z;
        v.z = sign(v.z);
    }
#if UNITY_UV_STARTS_AT_TOP
    v.y *= -1;
#endif
    return 0.5 + 0.5 * v;
}

float3 TransformWorldToScreenDir(float3 dirWS)
{
    float3 v = TransformWorldToViewDir(dirWS);
    return TransformViewToScreenDir(v);
}

float3 GetCameraVector(float2 texcoord)
{
    float4 v = float4(texcoord * 2 - 1, 1, 1);
#if UNITY_UV_STARTS_AT_TOP
    v.y *= -1;
#endif
    v = mul(UNITY_MATRIX_I_VP, v);
    return normalize(_WorldSpaceCameraPos - v.xyz / v.w);
}

float3 GetWorldPosition(float2 texcoord, float depth)
{
    return ComputeWorldSpacePosition(texcoord, depth, UNITY_MATRIX_I_VP);
}

float3 GetWorldPosition(float2 texcoord)
{
    float depth = SampleSceneDepth(texcoord);
    return GetWorldPosition(texcoord, depth);
}

float3 GetRelativeWorldPosition(float2 texcoord, float depth)
{
    return GetWorldPosition(texcoord, depth) - _WorldSpaceCameraPos;
}

float3 GetRelativeWorldPosition(float2 texcoord)
{
    return GetWorldPosition(texcoord) - _WorldSpaceCameraPos;
}

float3 GetViewPosition(float2 texcoord, float depth)
{
    return ComputeViewSpacePosition(texcoord, depth, UNITY_MATRIX_I_P);
}

float3 GetViewPosition(float2 texcoord)
{
    float depth = SampleSceneDepth(texcoord);
    return GetViewPosition(texcoord, depth);
}

float GetCameraDistance(float2 texcoord, float depth)
{
    float3 v = GetViewPosition(texcoord, depth);
    return length(v);
}

float GetCameraDistance(float2 texcoord)
{
    float3 v = GetViewPosition(texcoord);
    return length(v);
}

#undef FRAMEBUFFER_FETCH_ENABLED
#undef FETCH_FRAMEBUFFER

#endif // _SIRIUS_CORE_SCREEN_SPACE_UTIL_HLSL_
