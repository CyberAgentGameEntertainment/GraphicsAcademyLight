// --------------------------------------------------------------
// Copyright 2025 CyberAgent, Inc.
// --------------------------------------------------------------

using System;
using Sirius.Core.Runtime.Scripts;
using Sirius.PostProcessing.Runtime.Scripts.Features.Passes;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Sirius.PostProcessing.Runtime.Scripts.Features
{
    // allowFlag属性でPassのAllowExecuteと紐つける
    [AttributeUsage(AttributeTargets.Field)]
    public class AllowFlagAttribute : Attribute
    {
        public string BoolFieldName { get; }
        public AllowFlagAttribute(string boolFieldName)
        {
            BoolFieldName = boolFieldName;
        }
    }

    [DisallowMultipleRendererFeature]
    public sealed partial class SiriusPostProcessingFeature : ScriptableRendererFeature
    {
        [SerializeField] private bool allowRadialBlurPostProcess;
        [SerializeField] private bool allowDirectionalBlurPostProcess;

        [AllowFlag("allowDirectionalBlurPostProcess")]
        private DirectionalBlurRenderPass _directionalBlurRenderPass;

        [AllowFlag("allowRadialBlurPostProcess")]
        private RadialBlurRenderPass _radialBlurRenderPass;

        public override void Create()
        {
            _radialBlurRenderPass = new RadialBlurRenderPass();
            _directionalBlurRenderPass = new DirectionalBlurRenderPass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.isPreviewCamera)
                return;

            // camera ignore check
            var ignorerExist = renderingData.cameraData.camera.TryGetComponent<SiriusIgnorer>(out var ignorer);
            if (ignorerExist && ignorer.IgnorePostProcessingRendererFeature)
                return;

            // add pass only if camera's postprocess is enabled
            if (!renderingData.cameraData.postProcessEnabled)
                return;

            // RadialBlur
            if (allowRadialBlurPostProcess)
            {
                _radialBlurRenderPass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
                renderer.EnqueuePass(_radialBlurRenderPass);
            }

            // DirectionalBlur
            if (allowDirectionalBlurPostProcess)
            {
                _directionalBlurRenderPass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
                renderer.EnqueuePass(_directionalBlurRenderPass);
            }
        }

        protected override void Dispose(bool disposing)
        {
            _directionalBlurRenderPass.Cleanup();
            _radialBlurRenderPass.Cleanup();
        }
    }
}
