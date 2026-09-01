using Sirius.PostProcessing.Runtime.Scripts.Volumes;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Sirius.PostProcessing.Runtime.Scripts.Features.Passes
{
    /// <summary>
    /// RadialBlur描画パス
    /// </summary>
    public sealed class RadialBlurRenderPass : ScriptableRenderPass, IAllowExecute
    {
        public static string[] UsingShaderNameList { get; } =
        {
            "Hidden/Sirius/RadialBlurPass"
        };

        public bool AllowExecute { get; set; }

        private static string ShaderName => UsingShaderNameList[0];
        private readonly ProfilingSampler _profilingSampler;
        private Material _postProcessMaterial;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public RadialBlurRenderPass()
        {
            _profilingSampler = new ProfilingSampler("Sirius.RadialBlur");
        }

        private void UpdateMaterialProperties(RadialBlurVolume radialBlurVolume)
        {
            _postProcessMaterial.SetFloat(ShaderPropertyIDs.RadialBlurGazePositionX, radialBlurVolume.GazePositionX);
            _postProcessMaterial.SetFloat(ShaderPropertyIDs.RadialBlurGazePositionY, radialBlurVolume.GazePositionY);
            _postProcessMaterial.SetFloat(ShaderPropertyIDs.RadialBlurStrength, radialBlurVolume.Strength);
            _postProcessMaterial.SetFloat(ShaderPropertyIDs.RadialBlurWidth, radialBlurVolume.Width);
            _postProcessMaterial.SetFloat(ShaderPropertyIDs.RadialBlurOffset, radialBlurVolume.Offset);
            _postProcessMaterial.SetTexture(ShaderPropertyIDs.RadialBlurMask, radialBlurVolume.Mask ? radialBlurVolume.Mask : Texture2D.whiteTexture);
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
            public static readonly int RadialBlurGazePositionX = Shader.PropertyToID("_RadialBlurGazePositionX");
            public static readonly int RadialBlurGazePositionY = Shader.PropertyToID("_RadialBlurGazePositionY");
            public static readonly int RadialBlurStrength = Shader.PropertyToID("_RadialBlurStrength");
            public static readonly int RadialBlurWidth = Shader.PropertyToID("_RadialBlurWidth");
            public static readonly int RadialBlurOffset = Shader.PropertyToID("_RadialBlurOffset");
            public static readonly int RadialBlurMask = Shader.PropertyToID("_RadialBlurMask");
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

            if (!VolumeManager.instance.IsComponentActiveInMask<RadialBlurVolume>(cameraData.volumeLayerMask)) return;

            var radialBlurVolume = VolumeManager.instance.stack.GetComponent<RadialBlurVolume>();

            if (!radialBlurVolume.IsActive()) return;

            UpdateMaterialProperties(radialBlurVolume);

            var resourceData = frameData.Get<UniversalResourceData>();
            var desc = renderGraph.GetTextureDesc(resourceData.activeColorTexture);
            var destTextureHandle = renderGraph.CreateTexture(desc);
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Sirius.RadialBlur", out var passData))
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
