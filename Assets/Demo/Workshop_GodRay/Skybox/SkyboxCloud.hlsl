#ifndef __SKYBOX_CLOUD__
#define __SKYBOX_CLOUD__

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "Easing.hlsl"

struct InputCloudData
{
    half3 inputColor;
    float mask;

    float2 uv;
    float3 normalWS;
    float3 tangentWS;
    float3 bitangentWS;

    float3 lightDirWS;
    float3 viewDirWS;

    float2 moveOffset;
};

float _CloudUvScale;
float4 _CloudNoiseUV1;
float4 _CloudNoiseUV2;
float4 _CloudNoiseUV3;
float4 _CloudNoiseUV4;

float _CloudSoftness;
float _CloudRate;
float _CloudRimNarrow;
float _CloudRimForce;

float _Cloud1UvScale;
float4 _Cloud1NoiseUV1;
float4 _Cloud1NoiseUV2;
float4 _Cloud1NoiseUV3;
float4 _Cloud1NoiseUV4;

float _Cloud1Softness;
float _Cloud1Rate;
float _Cloud1RimNarrow;
float _Cloud1RimForce;

TEXTURE2D(_CloudNoiseMap);
SAMPLER(sampler_CloudNoiseMap);

// float _Cloud1Layer1;
// half4 _Cloud1Color;
// half4 _Cloud1TokaColor;
// float _Cloud1ThicknessCoefficient;

float2 GetUV(float2 uv, float4 uvSetting)
{
    return uv * uvSetting.xy + uvSetting.zw;
}

float AlmaRemap(float value, float minOld, float maxOld, float minNew, float maxNew)
{
    maxOld = max(maxOld, minOld + Min_float());
    return minNew + (value - minOld) * (maxNew - minNew) / (maxOld - minOld);
}

half4 CalcClouds(InputCloudData i, float4 uv1, float4 uv2, float4 uv3, float4 uv4, float4 cloudParam)
{
    float2 uv_1 = GetUV(i.uv, uv1);
    float2 uv_2 = GetUV(i.uv, uv2);
    float2 uv_3 = GetUV(i.uv, uv3);
    float2 uv_4 = GetUV(i.uv, uv4);
    uv_1 += i.moveOffset;
    uv_2 += i.moveOffset;
    uv_3 += i.moveOffset;
    uv_4 += i.moveOffset;

    half4 col_1 = SAMPLE_TEXTURE2D(_CloudNoiseMap, sampler_CloudNoiseMap, uv_1);
    half4 col_2 = SAMPLE_TEXTURE2D(_CloudNoiseMap, sampler_CloudNoiseMap, uv_2);
    half4 col_3 = SAMPLE_TEXTURE2D(_CloudNoiseMap, sampler_CloudNoiseMap, uv_3);
    half4 col_4 = SAMPLE_TEXTURE2D(_CloudNoiseMap, sampler_CloudNoiseMap, uv_4);
    float noise = 0;
    noise += col_1.a * 0.5;
    noise += col_2.a * 0.25;
    noise += col_3.a * 0.125;
    noise += col_4.a * 0.0625;

    float cloudNoisePower = clamp(noise, -1, 1) * 0.5 + 0.5;

    // 雲の色を算出
    half3 cloudColor = cloudNoisePower;

    float softness = cloudParam.x;
    float rate = cloudParam.y;
    float rimNarrow = cloudParam.z;
    float rimForce = cloudParam.w;

    // 雲の濃さを算出
    float soft2 = softness * softness; // 感度の調節
    float cloudSoftUnder = 1 - rate - soft2 * 1;
    float cloudSoftTop = cloudSoftUnder + soft2 * 2;
    float cloudPower = saturate(AlmaRemap(noise, cloudSoftUnder, cloudSoftTop, 0, 1));
    cloudPower = cubicInOut(saturate(cloudPower));
    float cloudAreaRate = saturate(AlmaRemap(noise, cloudSoftUnder, 1, 0, 1));

    // 雲の法線を算出
    float3 cloudNormalTS = float3(0.0, 0.0, 1.0);
    cloudNormalTS = BlendNormal(cloudNormalTS, (col_1.rgb * 2.0 - 1.0) * 0.5);
    cloudNormalTS = BlendNormal(cloudNormalTS, (col_2.rgb * 2.0 - 1.0) * 0.25);
    cloudNormalTS = BlendNormal(cloudNormalTS, (col_3.rgb * 2.0 - 1.0) * 0.125);
    cloudNormalTS = BlendNormal(cloudNormalTS, (col_4.rgb * 2.0 - 1.0) * 0.0625);
    float3 cloudNormalWS = cloudNormalTS.x * i.tangentWS + cloudNormalTS.y * i.bitangentWS + cloudNormalTS.z * i.
        normalWS;
    cloudNormalWS.y = cloudAreaRate;

    float cloudNdl = dot(cloudNormalWS, i.lightDirWS);
    float cloudNdlForUv = cloudNdl * 0.5 + 0.5;
    float vdlForUv = dot(i.viewDirWS, i.lightDirWS) * 0.5 + 0.5;

    // 境界が光る度合いを用意
    float rimPowerR = cloudAreaRate * rimNarrow;
    rimPowerR = quadOut(saturate(rimPowerR));
    float rimPower = (1 - rimPowerR) * rimForce;
    rimPower = saturate(rimPower);
    // 境界の光の色を設定
    float2 rimUv = float2(vdlForUv, cloudNdlForUv);
    float3 rimColor = 1; //tex2D(_rimMap, rimUv);
    cloudColor = rimColor.rgb * rimPower + cloudColor;

    return half4(cloudColor, cloudPower);
}

half4 CalcClouds(InputCloudData i)
{
    float4 cloudParam = float4(_CloudSoftness, _CloudRate, _CloudRimNarrow, _CloudRimForce);
    return CalcClouds(i, _CloudNoiseUV1, _CloudNoiseUV2, _CloudNoiseUV3, _CloudNoiseUV4, cloudParam);
}

half4 CalcClouds1(InputCloudData i)
{
    float4 cloudParam = float4(_Cloud1Softness, _Cloud1Rate, _Cloud1RimNarrow, _Cloud1RimForce);
    return CalcClouds(i, _Cloud1NoiseUV1, _Cloud1NoiseUV2, _Cloud1NoiseUV3, _Cloud1NoiseUV4, cloudParam);
    /*
        float2 uv_1 = GetUV(i.uv, _Cloud1NoiseUV1);
        float2 uv_2 = GetUV(i.uv, _Cloud1NoiseUV2);
        float2 uv_3 = GetUV(i.uv, _Cloud1NoiseUV3);
        float2 uv_4 = GetUV(i.uv, _Cloud1NoiseUV4);
        uv_1 += i.moveOffset;
        uv_2 += i.moveOffset;
        uv_3 += i.moveOffset;
        uv_4 += i.moveOffset;
    
        half4 col_1 = SAMPLE_TEXTURE2D(_CloudNoiseMap, sampler_CloudNoiseMap, uv_1);
        half4 col_2 = SAMPLE_TEXTURE2D(_CloudNoiseMap, sampler_CloudNoiseMap, uv_2);
        half4 col_3 = SAMPLE_TEXTURE2D(_CloudNoiseMap, sampler_CloudNoiseMap, uv_3);
        half4 col_4 = SAMPLE_TEXTURE2D(_CloudNoiseMap, sampler_CloudNoiseMap, uv_4);
        float noise = 0;
        noise += col_1.a * 0.5;
        noise += col_2.a * 0.25;
        noise += col_3.a * 0.125;
        noise += col_4.a * 0.0625;
    
        float cloudNoisePower = saturate(noise * 0.5 + 0.5);
    
        // 雲の色を算出
        half3 cloudColor = cloudNoisePower;
    
        // 雲の濃さを算出
        float soft2 = _Cloud1Softness * _Cloud1Softness; // 感度の調節
        float cloudSoftUnder = 1 - _Cloud1Rate - soft2 * 1;
        float cloudSoftTop = cloudSoftUnder + soft2 * 2;
        float cloudPower = saturate(AlmaRemap(noise, cloudSoftUnder, cloudSoftTop, 0, 1));
        // cloudPower = cubicInOut(saturate(cloudPower));
        float cloudAreaRate = saturate(AlmaRemap(noise, cloudSoftUnder, 1, 0, 1));
    
        // float cloudLayer2 = saturate(cloudPower * 20 - _Cloud1Layer1 * 20);
        // float cloudLayer1 = 1 - cloudLayer2;
        // float cloudAreaRate2 = saturate(cloudPower * 8 - 1);
    
        // float cloudThickness = saturate(cloudAreaRate + cloudAreaRate2);
        
        // 雲の法線を算出
        float3 cloudNormalTS = float3(0.0, 0.0, 1.0);
        cloudNormalTS += (col_1.rgb * 2.0 - 1.0) * 0.5;
        cloudNormalTS += (col_2.rgb * 2.0 - 1.0) * 0.25;
        cloudNormalTS += (col_3.rgb * 2.0 - 1.0) * 0.125;
        cloudNormalTS += (col_4.rgb * 2.0 - 1.0) * 0.0625;
        cloudNormalTS.z = cloudAreaRate;
        cloudNormalTS = normalize(cloudNormalTS);
        float3 cloudNormalWS = cloudNormalTS.x * i.tangentWS + cloudNormalTS.y * i.bitangentWS + cloudNormalTS.z * i.normalWS;
    
        // lightDirとnormalは反対が一番強度が大きい
        float cloudNdl = (dot(cloudNormalWS, i.lightDirWS));
        
        float cloudVdl = dot(i.viewDirWS, i.lightDirWS);
        float cloudNdv = dot(cloudNormalWS, i.viewDirWS);
        
        // 境界が光る度合いを用意
        float rimPower = 1 - saturate(cloudAreaRate * 15);
        
        // float rimPowerR = cloudAreaRate * _Cloud1RimNarrow;
        // rimPowerR = quadOut(saturate(rimPowerR));
        // float rimPower = (1 - rimPowerR) * _Cloud1RimForce;
        // rimPower = saturate(rimPower);
        // 境界の光の色を設定
        // float2 rimUv = float2(vdlForUv, cloudNdlForUv);
        float3 rimColor = 1; // tex2D(_rimMap, rimUv);
        cloudColor = rimColor.rgb * rimPower + cloudColor;
    
        cloudColor = cloudNdl;//lerp(_Cloud1LayerHighColor.rgb, _Cloud1LayerLowColor.rgb, (1 - cloudNdl) * saturate(rimPower * 10));
        // cloudColor = lerp(half3(1, 0, 0), cloudColor, saturate(cloudAreaRate * 3));
        // cloudColor = cloudLayer1 * _Cloud1LayerLowColor + cloudLayer2 * _Cloud1LayerHighColor;
    
        // return half4(cloudAreaRate.xxx + half3(1,1,1) * cloudAreaRate2, saturate(cloudPower * 5));
    
        float inputColorLuminance = Luminance(i.inputColor);
        float t = 1 - cloudPower;// cloudNdl * 0.5 + 0.25 + inputColorLuminance * 0.5 - _Cloud1ThicknessCoefficient * cloudThickness;// saturate(inputColorLuminance * 0.5 - cloudThickness) * (i.viewDirWS.y * 2);
        t = saturate(t * saturate(-cloudNdl * 0.5 + 0.5));
        cloudColor = lerp(_Cloud1Color, i.inputColor, t);
        // return lerp(_Cloud1Color, i.inputColor, saturate(inputColorLuminance * 10 - cloudThickness) * ( i.mask));
        return half4(cloudColor, 1);
        */
}

#endif
