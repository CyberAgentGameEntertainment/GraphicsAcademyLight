using System;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine;

namespace Sirius.DevSupport
{
    public class ProfilingSamplerCollectorPass : ScriptableRenderPass
    {
        private class PassData
        {
            public RenderGraph RenderGraph;
            public List<RenderPassData> RenderPassData;
        }
        private readonly Dictionary<Camera, List<RenderPassData>> _renderPassData = new();
        public ProfilingSamplerCollectorPass()
        {
            renderPassEvent = RenderPassEvent.AfterRendering;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (CTEasyGPUProfiler.Instance == null) return;
            using var builder = renderGraph.AddRasterRenderPass<PassData>(
                nameof(ProfilingSamplerCollectorPass),
                out var passData);
            var cameraData = frameData.Get<UniversalCameraData>();
            if (_renderPassData.ContainsKey(cameraData.camera))
            {
                _renderPassData[cameraData.camera].Clear();
            }
            else
            {
                _renderPassData[cameraData.camera] = new(64);
            }

            builder.AllowPassCulling(false);
            passData.RenderGraph = renderGraph;
            passData.RenderPassData = _renderPassData[cameraData.camera];

            builder.SetRenderFunc(static(PassData data, RasterGraphContext context) =>
            {
                // Collect profiling data here
                RenderGraphUtils.GetRenderPassData(data.RenderPassData, data.RenderGraph);
            });

            CTEasyGPUProfiler.Instance.AddRenderPassData(cameraData.camera, _renderPassData[cameraData.camera]);
        }
        public void Cleanup()
        {
            _renderPassData.Clear();
        }
    }
}
