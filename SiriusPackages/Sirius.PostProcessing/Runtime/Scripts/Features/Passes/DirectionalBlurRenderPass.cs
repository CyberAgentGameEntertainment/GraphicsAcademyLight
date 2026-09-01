using Sirius.PostProcessing.Runtime.Scripts.Volumes;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Sirius.PostProcessing.Runtime.Scripts.Features.Passes
{
    /// <summary>
    /// DirectionalBlur描画パス
    /// </summary>
    public sealed class DirectionalBlurRenderPass : ScriptableRenderPass, IAllowExecute
    {
        public static string[] UsingShaderNameList { get; } =
        {
            "Hidden/Sirius/DirectionalBlurPass"
        };

        public bool AllowExecute { get; set; }

        private static string ShaderName => UsingShaderNameList[0];

        private readonly ProfilingSampler _profilingSampler;
        private Material _postProcessMaterial;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public DirectionalBlurRenderPass()
        {
            _profilingSampler = new ProfilingSampler("Sirius.DirectionalBlur");
        }

        private void UpdateMaterialProperties(DirectionalBlurVolume directionalBlurVolume)
        {
            var angleDirection = new Vector2(Mathf.Cos(directionalBlurVolume.Angle * Mathf.Deg2Rad), Mathf.Sin(directionalBlurVolume.Angle * Mathf.Deg2Rad));

            _postProcessMaterial.SetFloat(ShaderPropertyIDs.DirectionalBlurUseFixedAspectSampling, directionalBlurVolume.UseFixedAspectSampling ? 1.0f : 0.0f);
            _postProcessMaterial.SetVector(ShaderPropertyIDs.DirectionalBlurNormalizedDirection, angleDirection);
            _postProcessMaterial.SetFloat(ShaderPropertyIDs.DirectionalBlurStrength, directionalBlurVolume.Strength);
            _postProcessMaterial.SetFloat(ShaderPropertyIDs.DirectionalBlurWidth, directionalBlurVolume.Width);
            _postProcessMaterial.SetTexture(ShaderPropertyIDs.DirectionalBlurMask, directionalBlurVolume.Mask ? directionalBlurVolume.Mask : Texture2D.whiteTexture);
        }
        /// <summary>
        /// PropertyID
        /// </summary>
        private static class ShaderPropertyIDs
        {
            public static readonly int DirectionalBlurUseFixedAspectSampling = Shader.PropertyToID("_DirectionalBlurUseFixedAspectSampling");
            public static readonly int DirectionalBlurNormalizedDirection = Shader.PropertyToID("_DirectionalBlurNormalizedDirection");
            public static readonly int DirectionalBlurStrength = Shader.PropertyToID("_DirectionalBlurStrength");
            public static readonly int DirectionalBlurWidth = Shader.PropertyToID("_DirectionalBlurWidth");
            public static readonly int DirectionalBlurMask = Shader.PropertyToID("_DirectionalBlurMask");
        }

        public void Cleanup()
        {
            CoreUtils.Destroy(_postProcessMaterial);
            _postProcessMaterial = null;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var cameraData = frameData.Get<UniversalCameraData>();
            var resourceData = frameData.Get<UniversalResourceData>();
            if (cameraData.camera.cameraType == CameraType.Preview) return;
            if (cameraData.postProcessEnabled == false) return;
            if (_postProcessMaterial == null) _postProcessMaterial = CoreUtils.CreateEngineMaterial(ShaderName);
            // DirectionalBlur
            if (!VolumeManager.instance.IsComponentActiveInMask<DirectionalBlurVolume>(cameraData.volumeLayerMask))
                return;
            var directionalBlurVolume = VolumeManager.instance.stack.GetComponent<DirectionalBlurVolume>();
            if (!directionalBlurVolume.IsActive()) return;

            UpdateMaterialProperties(directionalBlurVolume);
            var desc = renderGraph.GetTextureDesc(resourceData.activeColorTexture);
            var destTextureHandle = renderGraph.CreateTexture(desc);
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Sirius.DirectionalBlur", out var passData))
            {
                builder.SetRenderAttachment(destTextureHandle, 0);
                builder.UseTexture(resourceData.activeColorTexture);
                passData.Source = resourceData.activeColorTexture;
                passData.Destination = destTextureHandle;
                passData.Mat = _postProcessMaterial;
                builder.SetRenderFunc((PassData data, RasterGraphContext context) => { Blitter.BlitTexture(context.cmd, data.Source, Vector2.one, data.Mat, 0); });
            }
            resourceData.cameraColor = destTextureHandle;
        }
        private class PassData
        {
            public TextureHandle Destination;
            public Material Mat;
            public TextureHandle Source;
        }
    }
}
