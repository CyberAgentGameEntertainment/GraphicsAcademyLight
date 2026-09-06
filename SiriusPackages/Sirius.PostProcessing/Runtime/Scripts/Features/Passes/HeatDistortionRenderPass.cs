using Sirius.PostProcessing.Runtime.Scripts.Volumes;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Sirius.PostProcessing.Runtime.Scripts.Features.Passes
{
    /// <summary>
    /// HeatDistortion描画パス
    /// </summary>
    public sealed class HeatDistortionRenderPass : ScriptableRenderPass, IAllowExecute
    {
        public static string[] UsingShaderNameList { get; } =
        {
            "Hidden/Sirius/HeatDistortion"
        };

        public bool AllowExecute { get; set; }

        private static string ShaderName => UsingShaderNameList[0];
        private readonly ProfilingSampler _profilingSampler;
        private Material _postProcessMaterial;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public HeatDistortionRenderPass()
        {
            _profilingSampler = new ProfilingSampler("Sirius.HeatDistortion");
        }

        private void UpdateMaterialProperties(HeatDistortionVolume heatDistortionVolume)
        {
            _postProcessMaterial.SetFloat(ShaderPropertyIDs.HeatDistortionIntensity, heatDistortionVolume.Intensity);
            _postProcessMaterial.SetFloat(ShaderPropertyIDs.HeatDistortionBlend, heatDistortionVolume.Blend);
            _postProcessMaterial.SetFloat(ShaderPropertyIDs.HeatDistortionStartDistance, heatDistortionVolume.StartDistance);
            _postProcessMaterial.SetFloat(ShaderPropertyIDs.HeatDistortionFadeDistance, heatDistortionVolume.FadeDistance);
            _postProcessMaterial.SetFloat(ShaderPropertyIDs.HeatDistortionSpeed, heatDistortionVolume.Speed);
            _postProcessMaterial.SetFloat(ShaderPropertyIDs.HeatDistortionChromaticSeparation, heatDistortionVolume.ChromaticSeparation);
            _postProcessMaterial.SetFloat(ShaderPropertyIDs.HeatDistortionZenithMask, heatDistortionVolume.ZenithMask ? 1.0f : 0.0f);
            _postProcessMaterial.SetFloat(ShaderPropertyIDs.HeatDistortionHorizonMask, heatDistortionVolume.HorizonMask ? 1.0f : 0.0f);
            _postProcessMaterial.SetFloat(ShaderPropertyIDs.HeatDistortionHorizonExponent, heatDistortionVolume.HorizonExponent);
            _postProcessMaterial.SetFloat(ShaderPropertyIDs.HeatDistortionNoiseScale, heatDistortionVolume.NoiseScale);
            // NoiseTextureはnullableなTexture2DArrayのため、未設定時はSetTextureを呼ばずシェーダー側のデフォルト値に委ねる
            // (Texture2D.whiteTextureのような次元の異なるフォールバックを渡すと次元不一致になるため)
            if (heatDistortionVolume.NoiseTexture != null)
            {
                _postProcessMaterial.SetTexture(ShaderPropertyIDs.HeatDistortionNoiseTex, heatDistortionVolume.NoiseTexture);
            }
        }

        public void Cleanup()
        {
            CoreUtils.Destroy(_postProcessMaterial);
            _postProcessMaterial = null;
        }

        /// <summary>
        /// PropertyID
        /// </summary>
        private static class ShaderPropertyIDs
        {
            public static readonly int HeatDistortionIntensity = Shader.PropertyToID("_HeatDistortionIntensity");
            public static readonly int HeatDistortionBlend = Shader.PropertyToID("_HeatDistortionBlend");
            public static readonly int HeatDistortionStartDistance = Shader.PropertyToID("_HeatDistortionStartDistance");
            public static readonly int HeatDistortionFadeDistance = Shader.PropertyToID("_HeatDistortionFadeDistance");
            public static readonly int HeatDistortionSpeed = Shader.PropertyToID("_HeatDistortionSpeed");
            public static readonly int HeatDistortionChromaticSeparation = Shader.PropertyToID("_HeatDistortionChromaticSeparation");
            public static readonly int HeatDistortionZenithMask = Shader.PropertyToID("_HeatDistortionZenithMask");
            public static readonly int HeatDistortionHorizonMask = Shader.PropertyToID("_HeatDistortionHorizonMask");
            public static readonly int HeatDistortionHorizonExponent = Shader.PropertyToID("_HeatDistortionHorizonExponent");
            public static readonly int HeatDistortionNoiseScale = Shader.PropertyToID("_HeatDistortionNoiseScale");
            public static readonly int HeatDistortionNoiseTex = Shader.PropertyToID("_HeatDistortionNoiseTex");
        }

        private class PassData
        {
            public Material Mat;
            public TextureHandle Source;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var cameraData = frameData.Get<UniversalCameraData>();
            if (cameraData.camera.cameraType == CameraType.Preview)
            {
                return;
            }

            if (cameraData.postProcessEnabled == false)
            {
                return;
            }
            if (_postProcessMaterial == null)
            {
                _postProcessMaterial = CoreUtils.CreateEngineMaterial(ShaderName);
            }

            if (!VolumeManager.instance.IsComponentActiveInMask<HeatDistortionVolume>(cameraData.volumeLayerMask)) return;

            var heatDistortionVolume = VolumeManager.instance.stack.GetComponent<HeatDistortionVolume>();

            if (!heatDistortionVolume.IsActive()) return;

            UpdateMaterialProperties(heatDistortionVolume);

            var resourceData = frameData.Get<UniversalResourceData>();
            var desc = renderGraph.GetTextureDesc(resourceData.activeColorTexture);
            var destTextureHandle = renderGraph.CreateTexture(desc);
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Sirius.HeatDistortion", out var passData))
            {
                builder.SetRenderAttachment(destTextureHandle, 0);
                builder.UseTexture(resourceData.activeColorTexture);
                passData.Source = resourceData.activeColorTexture;
                passData.Mat = _postProcessMaterial;
                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    Blitter.BlitTexture(context.cmd, data.Source, Vector2.one, data.Mat, 0);
                });
            }
            resourceData.cameraColor = destTextureHandle;
        }
    }
}
