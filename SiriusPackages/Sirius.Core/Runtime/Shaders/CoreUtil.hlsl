/*!
 * @file CoreUtil.hlsl
 * @brief SIRIUSのシェーダー全般のユーティリティ.
 */
#ifndef _SIRIUS_CORE_UTIL_HLSL_
#define _SIRIUS_CORE_UTIL_HLSL_

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "DeclareDepthTexture.hlsl"

float4x4 _CtPrevFrameVpMatrix; // 1フレーム前のビュープロジェクション行列

// グレースケール
#define GRAY_SCALE_BT601(rgbColor) (0.299 * (rgbColor).r + 0.587 * (rgbColor).g + 0.114 * (rgbColor).b)

/**
 * \brief DrawProcedural用の頂点属性
 */
struct AttributesProcedural
{
    uint vertexID : SV_VertexID;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

/**
 * \brief DrawProcedural用の頂点シェーダーからの出力構造体
 */
struct VaryingsProcedural
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_OUTPUT_STEREO
};


#define powFast(a, b) ((a) / ((1.0 - (b)) * (a) + (b)))
float pow2(float a)
{
    return a * a;
}
float2 pow2(float2 a)
{
    return a * a;
}
float3 pow2(float3 a)
{
    return a * a;
}
float4 pow2(float4 a)
{
    return a * a;
}
float pow3(float a)
{
    return a * a * a;
}
float2 pow3(float2 a)
{
    return a * a * a;
}
float3 pow3(float3 a)
{
    return a * a * a;
}
float4 pow3(float4 a)
{
    return a * a * a;
}
float pow4(float a)
{
    a = a * a;
    return a * a;
}
float2 pow4(float2 a)
{
    a = a * a;
    return a * a;
}
float3 pow4(float3 a)
{
    a = a * a;
    return a * a;
}
float4 pow4(float4 a)
{
    a = a * a;
    return a * a;
}
float pow8(float a)
{
    a = a * a;
    a = a * a;
    return a * a;
}
float2 pow8(float2 a)
{
    a = a * a;
    a = a * a;
    return a * a;
}
float3 pow8(float3 a)
{
    a = a * a;
    a = a * a;
    return a * a;
}
float4 pow8(float4 a)
{
    a = a * a;
    a = a * a;
    return a * a;
}
float PositiveClampedPow(float Base, float Exponent)
{
    return (Base <= 2.980233e-8) ? 0.0 : pow(Base, Exponent);
}
float2 PositiveClampedPow(float2 Base, float2 Exponent)
{
    return float2(PositiveClampedPow(Base.x, Exponent.x), PositiveClampedPow(Base.y, Exponent.y));
}
float3 PositiveClampedPow(float3 Base, float3 Exponent)
{
    return float3(PositiveClampedPow(Base.xy, Exponent.xy), PositiveClampedPow(Base.z, Exponent.z));
}
float4 PositiveClampedPow(float4 Base, float4 Exponent)
{
    return float4(PositiveClampedPow(Base.xy, Exponent.xy), PositiveClampedPow(Base.zw, Exponent.zw));
}

float acosFast(float inX)
{
    float x = abs(inX);
    float res = HALF_PI - 0.156583 * x;
    res *= sqrt(1.0 - x);
    return inX >= 0 ? res : PI - res;
}
float asinFast(float x)
{
    return HALF_PI - acosFast(x);
}
float atan2Fast(float y, float x)
{
    float ax = abs(x);
    float ay = abs(y);
    float t0 = max(ax, ay);
    float t1 = min(ax, ay);
    float t3 = t1 / t0;
    float t4 = t3 * t3;
    t0 = 0.0872929;
    t0 = t0 * t4 - 0.301895;
    t0 = t0 * t4 + 1.0;
    t3 = t0 * t3;
    t3 = ay > ax ? HALF_PI - t3 : t3;
    t3 = x < 0 ? PI - t3 : t3;
    t3 = y < 0 ? -t3 : t3;
    return t3;
}

float2 Panner(float2 Coordinate, float2 Speed, float Time, bool FractionalPart = false)
{
    float2 v = Coordinate + Speed * Time;
    v = FractionalPart ? frac(v) : v;
    return v;
}

float3 RotateAboutAxis(float4 NormalizedRotationAxisAndAngle, float3 PositionOnAxis, float3 Position)
{
    float3 ClosestPointOnAxis = PositionOnAxis + NormalizedRotationAxisAndAngle.xyz * dot(NormalizedRotationAxisAndAngle.xyz, Position - PositionOnAxis);
    float3 UAxis = Position - ClosestPointOnAxis;
    float3 VAxis = cross(NormalizedRotationAxisAndAngle.xyz, UAxis);
    float CosAngle;
    float SinAngle;
    sincos(NormalizedRotationAxisAndAngle.w, SinAngle, CosAngle);
    float3 R = UAxis * CosAngle + VAxis * SinAngle;
    float3 RotatedPosition = ClosestPointOnAxis + R;
    return RotatedPosition - Position;
}

bool IsOrthoProjection(float4x4 ViewToClip)
{
    return ViewToClip._44 >= 1.0;
}

// _ScaledScreenParams doesn't exist in URP10 (only exist in URP13 or higher),
// so for old URP versions, we use _CameraDepthTexture_TexelSize as a fallback
// _CameraDepthTexture_TexelSize is not the best fallback solution, but works for NiloToonURP for now
float2 GetScaledScreenWidthHeight()
{
    #if SHADER_LIBRARY_VERSION_MAJOR >= 13
    return _ScaledScreenParams.xy;
    #else
    return _CameraDepthTexture_TexelSize.zw;
    #endif
}

float2 GetScaledScreenTexelSize()
{
    #if SHADER_LIBRARY_VERSION_MAJOR >= 13
    return _ScaledScreenParams.zw-1;
    #else
    return _CameraDepthTexture_TexelSize.xy;
    #endif
}

/**
 * \brief DrawProcedural用の頂点属性から頂点座標とテクスチャ座標を取得する
 */
void GetProceduralQuad(in uint vertexID, out float4 positionCS, out float2 uv)
{
    positionCS = GetQuadVertexPosition(vertexID);
    positionCS.xy = positionCS.xy * float2(2.0f, -2.0f) + float2(-1.0f, 1.0f);
    uv = GetQuadTexCoord(vertexID);
}

/**
 * \brief DrawProcedural用の頂点シェーダー
 */
VaryingsProcedural VertFullscreenMeshProcedural(AttributesProcedural input)
{
    VaryingsProcedural output;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    GetProceduralQuad(input.vertexID, output.positionCS, output.uv);

    return output;
}

float Convert_SV_PositionZ_ToLinearViewSpaceDepthPerspectiveCamera(float rawDepthValueFromDepthTextureSample)
{
    // if perspective camera, URP's LinearEyeDepth will handle everything for you
    // https://docs.unity3d.com/Manual/SL-PlatformDifferences.html
    // remember we can't use LinearEyeDepth() for orthographic camera!
    return LinearEyeDepth(rawDepthValueFromDepthTextureSample, _ZBufferParams);
}

float Convert_SV_PositionZ_ToLinearViewSpaceDepthOrthographicCamera(float rawDepthValueFromDepthTextureSample)
{
    // if orthographic camera, _CameraDepthTexture store scene depth linearly within 0~1 range, no matter which platform, even OpenGL
    // if platform use reverse depth, make sure to 1-depth also
    // https://docs.unity3d.com/Manual/SL-PlatformDifferences.html
    #if defined(UNITY_REVERSED_Z)
    // + UNITY_NEAR_CLIP_VALUE check here to support some android emulator also
    // TODO: check if this check is still useful or not
    rawDepthValueFromDepthTextureSample = UNITY_NEAR_CLIP_VALUE == 1
                                              ? 1 - rawDepthValueFromDepthTextureSample
                                              : rawDepthValueFromDepthTextureSample;
    #endif

    // simply lerp(near,far, [0,1]depth) to get view space depth
    return lerp(_ProjectionParams.y, _ProjectionParams.z, rawDepthValueFromDepthTextureSample);
}

// expected input:
// - SV_POSITION.z in fragment shader
// - tex2D(_CameraDepthTexture), which is SV_POSITION.z of ShadowCaster pass
// *this function runs slower but support both orthographic and perspective camera projection
float Convert_SV_PositionZ_ToLinearViewSpaceDepth(float SV_POSITIONz)
{
    // use a ? b : c (conditional move / movc) instead of if() here because if() itself may introdue more cost then a few extra math
    return unity_OrthoParams.w
               ? Convert_SV_PositionZ_ToLinearViewSpaceDepthOrthographicCamera(SV_POSITIONz)
               : Convert_SV_PositionZ_ToLinearViewSpaceDepthPerspectiveCamera(SV_POSITIONz);
}


float2 CalculateUVCoordFromClipSpace(float4 coordInClipSpace)
{
    float2 uv = coordInClipSpace.xy / coordInClipSpace.w;
    uv *= float2(0.5f, 0.5f * _ProjectionParams.x);
    uv += 0.5f;
    return uv;
}

// just like smoothstep(), but linear, not clamped
half InvLerp(half from, half to, half value)
{
    return (value - from) / (to - from);
}

half4 InvLerp(half from, half to, half4 value)
{
    return (value - from) / (to - from);
}

// just like smoothstep(), but linear
half InvLerpClamp(half from, half to, half value)
{
    return saturate(InvLerp(from, to, value));
}

half4 InvLerpClamp(half from, half to, half4 value)
{
    return saturate(InvLerp(from, to, value));
}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// high level helper functions
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

float LoadDepthTextureLinearDepthVS(int2 loadTexPos)
{
    // clamp loadTexPos to prevent loading outside of _CameraDepthTexture's valid area
    loadTexPos.x = max(loadTexPos.x, 0);
    loadTexPos.y = max(loadTexPos.y, 0);
    loadTexPos = min(loadTexPos, GetScaledScreenWidthHeight() - 1);

    float depthTextureRawSampleValue = LoadSceneDepth(loadTexPos);
    // using URP provided LoadSceneDepth(pos), this will make rendering correct in VR also
    float depthTextureLinearDepthVS = Convert_SV_PositionZ_ToLinearViewSpaceDepth(depthTextureRawSampleValue);
    return depthTextureLinearDepthVS;
}

#endif
