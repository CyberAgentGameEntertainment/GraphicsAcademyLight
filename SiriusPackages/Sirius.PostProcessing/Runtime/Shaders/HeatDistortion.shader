Shader "Hidden/Sirius/HeatDistortion"
{
    Properties
    {
        _HeatDistortionIntensity("Intensity", Float) = 1.0
        _HeatDistortionBlend("Blend", Float) = 1.0
        _HeatDistortionStartDistance("Start Distance", Float) = 20.0
        _HeatDistortionFadeDistance("Fade Distance", Float) = 150.0
        _HeatDistortionSpeed("Speed", Float) = 0.1369
        _HeatDistortionChromaticSeparation("Chromatic Separation", Float) = 0.5
        _HeatDistortionNoiseScale("Noise Scale", Float) = 1.7
        _HeatDistortionNoiseTex("Noise Texture (Texture2DArray)", 2DArray) = "" {}
    }
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
            #include "Packages/jp.co.cyberagent.sirius.core/Runtime/Shaders/ScreenSpaceUtil.hlsl"

            uniform half _HeatDistortionIntensity;
            uniform half _HeatDistortionBlend;
            uniform half _HeatDistortionStartDistance;
            uniform half _HeatDistortionFadeDistance;
            uniform half _HeatDistortionSpeed;
            uniform half _HeatDistortionChromaticSeparation;
            uniform half _HeatDistortionNoiseScale;

            TEXTURE2D_ARRAY(_HeatDistortionNoiseTex);
            SAMPLER(sampler_HeatDistortionNoiseTex);

            #define HEAT_DISTORTION_NOISE_SLICE_COUNT 64.0h
            #define HEAT_DISTORTION_EDGE_TOLERANCE_RATIO 0.1h // エッジガードの許容深度差(カメラ距離に対する比率)
            #define HEAT_DISTORTION_UV_OFFSET_SCALE 0.01h
            #define HEAT_DISTORTION_CHROMA_SLICE_OFFSET 0.05h // 色収差のチャンネル間スライスずらし量(1.0 = 64スライス分)

            half SampleNoise(const half2 uv, const half sliceT)
            {
                const half sliceF = sliceT * HEAT_DISTORTION_NOISE_SLICE_COUNT;
                const half sliceLo = floor(sliceF);
                const half sliceFrac = sliceF - sliceLo;
                const half sliceHi = fmod(sliceLo + 1.0h, HEAT_DISTORTION_NOISE_SLICE_COUNT);
                const half noiseLo = SAMPLE_TEXTURE2D_ARRAY_LOD(_HeatDistortionNoiseTex, sampler_HeatDistortionNoiseTex, uv, sliceLo, 0).r;
                const half noiseHi = SAMPLE_TEXTURE2D_ARRAY_LOD(_HeatDistortionNoiseTex, sampler_HeatDistortionNoiseTex, uv, sliceHi, 0).r;
                return lerp(noiseLo, noiseHi, sliceFrac);
            }

            half4 Frag(const Varyings input) : SV_Target
            {
                const half2 uv = input.texcoord;
                const half depth = SampleSceneDepth(uv);
                const half cameraDistance = (half)GetCameraDistance(uv, depth);

                // 距離減衰: StartDistance未満は0、FadeDistance以上は1
                const half distanceMask = saturate((cameraDistance - _HeatDistortionStartDistance) *
                    rcp(max(HALF_EPS, _HeatDistortionFadeDistance - _HeatDistortionStartDistance)));

                if (distanceMask <= 0.0h)
                {
                    return SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv);
                }

                // スクリーン座標をノイズUV、時間をスライス(Z軸)に対応させて3Dノイズ(Texture2DArray)をサンプルする。
                // ワールド座標xz基準だと遠方ほど1ピクセルあたりの座標変化が大きくなりノイズが高密度に潰れるため、
                // 距離減衰で歪みが最強になる遠方でかえって揺らぎが視認しにくくなる
                const half2 noiseAspect = half2(_ScreenParams.x * rcp(_ScreenParams.y), 1.0h);
                const half2 noiseUV = uv * noiseAspect * _HeatDistortionNoiseScale;
                const half sliceT = frac(_Time.y * _HeatDistortionSpeed);

                const half strength = _HeatDistortionIntensity * distanceMask * HEAT_DISTORTION_UV_OFFSET_SCALE;
                // アスペクト比補正(非正方形画面でも歪みが円形に見えるようにする)
                const half2 aspectCorrection = half2(_ScreenParams.y * rcp(_ScreenParams.x), 1.0h);
                // 色収差: R/G/Bそれぞれに独立したノイズをサンプルし、チャンネルごとに別のオフセットを作る。
                // 従来は単一ノイズから作った1本のオフセットを(1+c)/1/(1-c)倍していたため、
                // 3チャンネルのオフセットが常に同一直線上に並び、分離量の比が固定されていた
                // ノイズのスライス(時間軸)をチャンネルごとにずらすことで、それぞれが異なる屈折量を持つ。
                // ChromaticSeparation=0 のとき3チャンネルが同一スライスに収束し、色ずれが消える
                const half chromaSlice = _HeatDistortionChromaticSeparation * HEAT_DISTORTION_CHROMA_SLICE_OFFSET;
                const half noiseR = SampleNoise(noiseUV, frac(sliceT + chromaSlice * 1.123456h)) * 2.0h - 1.0h;
                const half noiseG = SampleNoise(noiseUV, sliceT) * 2.0h - 1.0h;
                const half noiseB = SampleNoise(noiseUV, frac(sliceT - chromaSlice * 1.23456h  + 1.0h)) * 2.0h - 1.0h;

                const half2 offsetR = noiseR * strength * aspectCorrection;
                const half2 offsetG = noiseG * strength * aspectCorrection;
                const half2 offsetB = noiseB * strength * aspectCorrection;

                const half colorR = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + offsetR).r;
                const half colorG = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + offsetG).g;
                const half colorB = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + offsetB).b;
                const half4 color = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv);

                // 合成係数: 距離フェードを「オフセット量」だけでなく「合成側」でも効かせる。
                // Blendは Volume から与える手動の合成率で、0なら元画像をそのまま返す
                const half t = saturate(distanceMask * _HeatDistortionBlend);

                return lerp(color, half4(colorR, colorG, colorB, color.a), t);
            }

            ENDHLSL
        }
    }
}
