using Sirius.PostProcessing.Runtime.Scripts.Volumes;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Sirius.PostProcessing.Runtime.Scripts.Features.Passes
{
    /// <summary>
    /// RotationBlur描画パス
    /// </summary>
    public sealed class RotationBlurRenderPass : ScriptableRenderPass, IAllowExecute
    {
        public static string[] UsingShaderNameList { get; } =
        {
            "Hidden/Sirius/RotationBlurPass"
        };

        public bool AllowExecute { get; set; }

        private static string ShaderName => UsingShaderNameList[0];
        private readonly ProfilingSampler _profilingSampler;
        private Material _postProcessMaterial;

        public RotationBlurRenderPass()
        {
            _profilingSampler = new ProfilingSampler("Sirius.RotationBlur");
        }

        private void UpdateMaterialProperties(RotationBlurVolume volume)
        {
            _postProcessMaterial.SetFloat(ShaderPropertyIDs.CenterX, volume.CenterX);
            _postProcessMaterial.SetFloat(ShaderPropertyIDs.CenterY, volume.CenterY);
            _postProcessMaterial.SetFloat(ShaderPropertyIDs.Strength, volume.Strength);
            _postProcessMaterial.SetFloat(ShaderPropertyIDs.Width, volume.Width);
            _postProcessMaterial.SetTexture(ShaderPropertyIDs.Mask, volume.Mask ? volume.Mask : Texture2D.whiteTexture);
        }

        public void Cleanup()
        {
            CoreUtils.Destroy(_postProcessMaterial);
            _postProcessMaterial = null;
        }

        private static class ShaderPropertyIDs
        {
            public static readonly int CenterX = Shader.PropertyToID("_RotationBlurCenterX");
            public static readonly int CenterY = Shader.PropertyToID("_RotationBlurCenterY");
            public static readonly int Strength = Shader.PropertyToID("_RotationBlurStrength");
            public static readonly int Width = Shader.PropertyToID("_RotationBlurWidth");
            public static readonly int Mask = Shader.PropertyToID("_RotationBlurMask");
        }

        private class PassData
        {
            public Material Mat;
            public TextureHandle Source;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var cameraData = frameData.Get<UniversalCameraData>();
            if (cameraData.camera.cameraType == CameraType.Preview) return;
            if (!cameraData.postProcessEnabled) return;

            if (_postProcessMaterial == null)
                _postProcessMaterial = CoreUtils.CreateEngineMaterial(ShaderName);

            if (!VolumeManager.instance.IsComponentActiveInMask<RotationBlurVolume>(cameraData.volumeLayerMask)) return;

            var volume = VolumeManager.instance.stack.GetComponent<RotationBlurVolume>();
            if (!volume.IsActive()) return;

            UpdateMaterialProperties(volume);

            var resourceData = frameData.Get<UniversalResourceData>();
            var desc = renderGraph.GetTextureDesc(resourceData.activeColorTexture);
            var destTextureHandle = renderGraph.CreateTexture(desc);
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Sirius.RotationBlur", out var passData))
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
