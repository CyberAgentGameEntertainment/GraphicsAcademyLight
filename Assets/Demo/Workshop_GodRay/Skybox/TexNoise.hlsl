#ifndef __TEX_NOISE__
#define __TEX_NOISE__

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

TEXTURE2D(_PerlinNoiseMap);
SAMPLER(sampler_PerlinNoiseMap);

// static const float TexNoiseRandomize = 127.1;
//
// static const float2x2 TexNoiseRandomize2x2 = float2x2(
//     float2(127.1, 311.7),
//     float2(269.5, 183.3)
// );

static const float3x3 TexNoiseRandomize3x3 = float3x3(
    float3(127.1, 311.7, 167.3),
    float3(269.5, 183.3, 32.1),
    float3(123.4, 321.9, 74.5)
);

// float TexNoise1D(float p)
// {
//     p *= TexNoiseRandomize;
//     float4 rand = SAMPLE_TEXTURE2D(_PerlinNoiseMap, sampler_PerlinNoiseMap, float2(p, 0.0));
//     return frac(rand.x * rand.a * 43758.5453123);
// }
//
// float2 TexNoise2D(float2 p)
// {
//     p = mul(TexNoiseRandomize2x2, p);
//     float4 rand = SAMPLE_TEXTURE2D(_PerlinNoiseMap, sampler_PerlinNoiseMap, p);
//     return frac(rand.xy * rand.a * 43758.5453123);
// }

float3 TexNoise3D(float3 p)
{
    p = mul(TexNoiseRandomize3x3, p);
    float4 rand = SAMPLE_TEXTURE2D(_PerlinNoiseMap, sampler_PerlinNoiseMap, p.xy + p.z);
    return  frac((rand.xyz * 2 - 1) * rand.a * 43758.5453123);
}

float3 SinNoise3D(float3 p)
{
    p = mul(TexNoiseRandomize3x3, p);
    return frac(sin(p) * 43758.5453123);
}

#endif