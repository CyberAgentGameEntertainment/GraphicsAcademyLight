Shader "Alma/Skybox/Gradient"
{
    Properties
    {
        [HDR]
        _SkyTopColor ("空色 (上)", Color) = (0.2, 0.5, 1.0, 0.0)
        [HDR]
        _SkyBottomColor ("空色 (下)", Color) = (0.1, 0.4, 0.5, 0.0)
        _SkyLerpCurvature ("空色 補間曲線", Range(-1.0, 3.0)) = 1.0

        [Space()]

        [HDR]
        _GroundColor ("地面色", Color) = (0.0, 0.0, 0.0, 0.0)
        _GroundSkyColorFadeOffset ("地面への空色フェードのオフセット", Range(0.0, 1.0)) = 0
        _GroundSkyColorFadeScale ("地面への空色フェードのスケール", Float) = 0
        _GroundLerpCurvature ("地面色 補間曲線", Range(0.0, 3.0)) = 1.0

        [Space()]
        [Space()]
        [Space()]

        _SunDir ("太陽 向き", Vector) = (0.0, 1.0, 0.0, 0.0)
        [HDR]
        _SunColor ("太陽 色", Color) = (0.0, 0.0, 0.0, 0.0)
        _SunSize ("太陽 サイズ", Range(0.0, 1.0)) = 0.5
        _SunExposure ("太陽 強度", Range(0.0, 10.0)) = 1

        [HDR]
        _SunFlareColor ("太陽 フレアの色", Color) = (0.0, 0.0, 0.0, 0.0)
        _SunFlareSize ("太陽 フレアのサイズ", Range(0.0, 1.0)) = 0.5
        _SunFlareFadeOffset ("太陽 フレアフェードのオフセット", Range(0.0, 1.0)) = 0
        _SunFlareFadeScale ("太陽 フレアフェードのスケール", Range(0.0, 1.0)) = 0
        _SunFlareLerpCurvature ("太陽 フレアの補間曲線", Range(0.0, 2.0)) = 1.0
        _SunFlareExposure ("太陽 フレアの強度", Range(0.0, 1.0)) = 0.5

        [Space()]
        [Space()]
        [Space()]

        _MoonDir ("月 向き", Vector) = (0.0, 1.0, 0.0, 0.0)
        [HDR]
        _MoonColor ("月 色", Color) = (0.0, 0.0, 0.0, 0.0)
        _MoonSize ("月 サイズ", Range(0.0, 1.0)) = 0.5
        _MoonExposure ("月 強度", Range(0.0, 10.0)) = 1

        [HDR]
        _MoonFlareColor ("月 フレアの色", Color) = (0.0, 0.0, 0.0, 0.0)
        _MoonFlareSize ("月 フレアのサイズ", Range(0.0, 1.0)) = 0.5
        _MoonFlareFadeOffset ("月 フレアフェードのオフセット", Range(0.0, 1.0)) = 0
        _MoonFlareFadeScale ("月 フレアフェードのスケール", Range(0.0, 1.0)) = 0
        _MoonFlareLerpCurvature ("月 フレアの補間曲線", Range(0.0, 2.0)) = 1.0
        _MoonFlareExposure ("月 フレアの強度", Range(0.0, 1.0)) = 0.5

        [Space()]
        [Space()]
        [Space()]
        
        _PerlinNoiseMap("星の分布マップ", 2D) = "white" {}
        _StarDistance ("星 距離", Range(0.0, 100.0)) = 50
        [HDR]
        _StarColor ("星 色", Color) = (0.3, 0.4, 1.0, 0.0)
        _StarColorRange ("星 色のランダム度合", Range(0.0, 1.0)) = 0.5
        _StarExposure ("星 輝度", Range(0.0, 1.0)) = 0.5
        
        [Space()]
        [Space()]
        [Space()]
        
        _CloudNoiseMap("雲の分布マップ（雲１，２共通）", 2D) = "white" {}
        _CloudLayerHeight("雲１の高さ", Range(1.5, 10.0)) = 2.0
        _CloudUvScale("雲１の分布マップのUVスケール", Range(0.001, 100)) = 5.0
        _CloudFadeOffset ("雲１のフェードのオフセット", Range(0.0, 1.0)) = 0.0
        _CloudFadeScale ("雲１のフェードのスケール", Range(0.1, 10.0)) = 1.0
        _CloudSpeedCoefficient("雲１の速度", Float) = 1.0
        _CloudMoveForward("雲１の移動方向", Vector) = (1,0,0,0)
        
        [HideInInspector] _CloudNoiseUV1 ("雲 ノイズ UV 1", Vector) = (1,1,0,0)
        [HideInInspector] _CloudNoiseUV2 ("雲 ノイズ UV 2", Vector) = (2,2,0,0)
        [HideInInspector] _CloudNoiseUV3 ("雲 ノイズ UV 3", Vector) = (4,4,0,0)
        [HideInInspector] _CloudNoiseUV4 ("雲 ノイズ UV 4", Vector) = (8,8,0,0)
        
        _CloudSoftness("雲１の境界の柔らかさ", Range(0.0, 1.0)) = 0.5
        _CloudRate("雲１の割合", Range(0.0, 1.0)) = 0
        
        _CloudRimNarrow ("雲１の輪郭のきつさ", Float) = 0.5
        _CloudRimForce ("雲１の輪郭の大きさ", Float) = 0.5
        
        [Space()]
        [Space()]
        [Space()]
        
        _Cloud1LayerHeight("雲２の高さ", Range(1.5, 10.0)) = 2.0
        _Cloud1UvScale("雲２の分布マップのUVスケール", Range(0.001, 100)) = 5.0
        _Cloud1FadeOffset ("雲 フェードのオフセット", Range(0.0, 1.0)) = 0.0
        _Cloud1FadeScale ("雲 フェードのスケール", Range(0.1, 10.0)) = 1.0
        _Cloud1SpeedCoefficient("雲２の速度", Float) = 1.0
        _Cloud1MoveForward("雲２の移動方向", Vector) = (1,0,0,0)
        
        [HideInInspector] _Cloud1NoiseUV1 ("雲 ノイズ UV 1", Vector) = (1,1,0,0)
        [HideInInspector] _Cloud1NoiseUV2 ("雲 ノイズ UV 2", Vector) = (2,2,0,0)
        [HideInInspector] _Cloud1NoiseUV3 ("雲 ノイズ UV 3", Vector) = (4,4,0,0)
        [HideInInspector] _Cloud1NoiseUV4 ("雲 ノイズ UV 4", Vector) = (8,8,0,0)
        
        _Cloud1Softness("雲２の境界の柔らかさ", Range(0.0, 1.0)) = 0.5
        _Cloud1Rate("雲２の割合", Range(0.0, 1.0)) = 0
        
        _Cloud1RimNarrow ("雲２の輪郭のきつさ", Float) = 0.5
        _Cloud1RimForce ("雲２の輪郭の大きさ", Float) = 0.5
        
//        _Cloud1Layer1 ("雲1 レイヤー1 閾値", Range(0.0, 1.0)) = 0.5
//        
//        [HDR]
//        _Cloud1Color ("雲1 色", Color) = (0.0, 0.0, 0.0, 0.0)
//        [HDR]
//        _Cloud1TokaColor ("雲1 透過色", Color) = (0.0, 0.0, 0.0, 0.0)
//        _Cloud1ThicknessCoefficient ("雲1 暑さ係数", Float) = 1
//        
//        _Cloud1Scale ("", Float) = 1
//        _Cloud1Offset ("", Float) = 0
    }
    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
            "Queue"="Geometry"
            "Preview Type"="Quad"
        }

        Pass
        {
            ZTest LEqual
            ZWrite Off
            Blend One Zero
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Easing.hlsl"
            #include "TexNoise.hlsl"
            #include "SkyboxCloud.hlsl"
            
            struct Attribute
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 vertex : SV_POSITION;
                float2 screenUV : TEXCOORD0;
                float3 positionWS :TEXCOORD1;
            };
            
            static const float MIE_G = -0.999f;
            static const float MIE_G2 = MIE_G * MIE_G;

            half4 _SkyTopColor;
            half4 _SkyBottomColor;
            float _SkyLerpCurvature;

            half4 _GroundColor;
            float _GroundSkyColorFadeOffset;
            float _GroundSkyColorFadeScale;
            float _GroundLerpCurvature;

            float4 _SunDir;
            half4 _SunColor;
            float _SunSize;
            float _SunExposure;
            half4 _SunFlareColor;
            float _SunFlareSize;
            float _SunFlareFadeOffset;
            float _SunFlareFadeScale;
            float _SunFlareLerpCurvature;
            float _SunFlareExposure;

            float4 _MoonDir;
            half4 _MoonColor;
            float _MoonSize;
            float _MoonExposure;
            half4 _MoonFlareColor;
            float _MoonFlareSize;
            float _MoonFlareFadeOffset;
            float _MoonFlareFadeScale;
            float _MoonFlareLerpCurvature;
            float _MoonFlareExposure;
            
            float _StarDistance;
            half4 _StarColor;
            float _StarColorRange;
            float _StarExposure;

            float _CloudFadeOffset;
            float _CloudFadeScale;
            float _CloudLayerHeight;
            float _CloudSpeedCoefficient;
            float4 _CloudMoveForward;

            float _Cloud1FadeOffset;
            float _Cloud1FadeScale;
            float _Cloud1LayerHeight;
            float _Cloud1SpeedCoefficient;
            float4 _Cloud1MoveForward;
            
            float CalcCurvedLerp(float t, float curvature)
            {
                return t * (t + curvature - t * curvature);
            }

            float CalcMie(float c)
            {
                return 1.5 * ((1.0 - MIE_G2) / (2.0 + MIE_G2)) * (1.0 + c * c) / (1.0 + MIE_G2 - 2.0 * MIE_G * c);
            }

            half3 CalcStar(float3 viewDirWS, float starProjDistance, float mask)
            {
                float3 positionWS = viewDirWS * starProjDistance;
                float3 ist = floor(positionWS);
                float3 fst = frac(positionWS);

                // 簡易的なセルラーノイズを取得する
                float3 starPosition = TexNoise3D(ist);
                float starDistance = distance(starPosition, fst);

                half luminance = Luminance(_StarColor.rgb);
                half3 color = lerp(_StarColor.rgb, starPosition * luminance, _StarColorRange);
                float intensity = pow(abs(1 - starDistance), 32);
                float randomIntensity = starPosition.x * 0.5 + 0.5;
                return color * intensity * randomIntensity;
            }

            Varyings vert(Attribute i)
            {
                Varyings o = (Varyings)0;
                o.vertex = TransformObjectToHClip(i.vertex);
                o.positionWS = mul(UNITY_MATRIX_I_VP, float4(o.vertex)).xyz;
                return o;
            }

            half4 CalcCloudColor(float3 viewDirWS, float3 lightDirWS)
            {
                float3 cloudCorrectViewWS = viewDirWS;
                cloudCorrectViewWS.y = abs(cloudCorrectViewWS.y) * _CloudLayerHeight + 0.02;
                cloudCorrectViewWS = normalize(cloudCorrectViewWS);
                float3 cloudCorrectPositionWS = cloudCorrectViewWS;
                float2 cloudUV = (cloudCorrectPositionWS.xz * _CloudUvScale) % 1.0;

                // 雲ノイズのサンプル
                // float2 moveForward = float2(dot(_CloudMoveForward.xy, float2(1.0, 0.0)), dot(_CloudMoveForward.xy, float2(0.0, 1.0)));
                InputCloudData input_cloud_data;
                input_cloud_data.uv = cloudUV;
                input_cloud_data.moveOffset = (_Time.y * _CloudSpeedCoefficient) % 1.0;
                input_cloud_data.viewDirWS = viewDirWS;
                input_cloud_data.lightDirWS = lightDirWS;
                input_cloud_data.normalWS = float3(0, 0, 1);
                input_cloud_data.tangentWS = float3(1, 0, 0);
                input_cloud_data.bitangentWS = float3(0, 1, 0);
                return  CalcClouds(input_cloud_data) * saturate(viewDirWS.y * _CloudFadeScale - _CloudFadeOffset);
            }

            half4 CalcCloud1Color(float3 viewDirWS, float3 lightDirWS)
            {
                float3 cloudCorrectViewWS = viewDirWS;
                cloudCorrectViewWS.y = abs(cloudCorrectViewWS.y) * _Cloud1LayerHeight + 0.02;
                cloudCorrectViewWS = normalize(cloudCorrectViewWS);
                float3 cloudCorrectPositionWS = cloudCorrectViewWS;
                float2 cloudUV = frac(cloudCorrectPositionWS.xz * _Cloud1UvScale);

                // 雲ノイズのサンプル
                // float2 moveForward = float2(dot(_Cloud1MoveForward.xy, float2(1.0, 0.0)), dot(_Cloud1MoveForward.xy, float2(0.0, 1.0)));
                InputCloudData input_cloud_data;
                input_cloud_data.inputColor = 1;
                input_cloud_data.mask = 1;
                input_cloud_data.uv = cloudUV;
                input_cloud_data.moveOffset = frac(_Time.y * _Cloud1SpeedCoefficient);
                input_cloud_data.viewDirWS = viewDirWS;
                input_cloud_data.lightDirWS = lightDirWS;
                input_cloud_data.normalWS = float3(0, -1, 0);
                input_cloud_data.tangentWS = float3(-1, 0, 0);
                input_cloud_data.bitangentWS = float3(0, 0, -1);
                return  CalcClouds1(input_cloud_data) * saturate(viewDirWS.y * _Cloud1FadeScale - _Cloud1FadeOffset);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 viewDirWS = normalize(i.positionWS);
                float yAxisCos = viewDirWS.y;

                // 太陽フレア
                float3 sunDir = normalize(_SunDir.xyz);
                float sunDirDv = dot(sunDir, viewDirWS);
                
                float sunFlareT = saturate(sunDirDv - 1.0 + _SunFlareSize);
                sunFlareT = saturate(sunFlareT * _SunFlareFadeScale - _SunFlareFadeOffset);
                sunFlareT = CalcCurvedLerp(sunFlareT, _SunFlareLerpCurvature);
                half3 sunFlareColor = lerp(0.0, _SunFlareColor.rgb, sunFlareT) * _SunFlareExposure;
                
                // 太陽(= ミー散乱)
                float sunMie = CalcMie(-sunDirDv) * _SunSize;
                half3 sunColor = smoothstep(0, 1 - _SunSize, sunMie) * _SunColor.rgb * _SunExposure;
                
                // 月フレア
                float3 moonDir = normalize(_MoonDir.xyz);
                float moonDirDv = dot(moonDir, viewDirWS);
                
                float moonFlareT = saturate(moonDirDv - 1.0 + _MoonFlareSize);
                moonFlareT = saturate(moonFlareT * _MoonFlareFadeScale - _MoonFlareFadeOffset);
                moonFlareT = CalcCurvedLerp(moonFlareT, _MoonFlareLerpCurvature);
                half3 moonFlareColor = lerp(0.0, _MoonFlareColor.rgb, moonFlareT) * _MoonFlareExposure;
                
                // 月(= ミー散乱)
                float moonMie = CalcMie(-moonDirDv) * _MoonSize;
                half3 moonColor = smoothstep(0, 1 - _MoonSize, moonMie) * _MoonColor.rgb * _MoonExposure;

                // 空色ベース
                float skyColorT = CalcCurvedLerp(yAxisCos, _SkyLerpCurvature);
                half3 skyColor = lerp(_SkyBottomColor.rgb, _SkyTopColor.rgb, skyColorT);

                // 地面色ベース
                float groundColorT = saturate(-yAxisCos * _GroundSkyColorFadeScale - _GroundSkyColorFadeOffset);
                groundColorT = CalcCurvedLerp(groundColorT, _GroundLerpCurvature);
                half3 groundColor = lerp(_SkyBottomColor.rgb, _GroundColor.rgb, groundColorT);

                float isSky = saturate(yAxisCos * 100);
                half3 star = CalcStar(viewDirWS, _StarDistance, 1) * _StarExposure * isSky;
                
                half3 finalColor = lerp(groundColor, skyColor, isSky);
                float invGroundColorT4 = (1 - groundColorT);

                half4 cloudColor1 = CalcCloudColor(viewDirWS, sunDir);
                half4 cloudColor2 = CalcCloud1Color(viewDirWS, sunDir);
                half3 cloudColor = cloudColor1.xyz * cloudColor1.a + cloudColor2.xyz * cloudColor2.a;
                
                finalColor += (sunFlareColor + sunColor + moonFlareColor + moonColor + cloudColor) * invGroundColorT4;
                finalColor = max(finalColor, star);
                return half4(finalColor, 1);
            }
            ENDHLSL
        }
    }
}