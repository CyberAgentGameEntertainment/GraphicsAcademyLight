// --------------------------------------------------------------
// Copyright 2025 CyberAgent, Inc.
// --------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEngine.Rendering.RenderGraphModule.NativeRenderPassCompiler;

namespace UnityEngine.Rendering.RenderGraphModule.Util
{

    // Render Passのデータ
    public class RenderPassData
    {
        // このRenderPassがコアテク独自のRenderGraphのプロファイリングスコープの開始か？
        public bool BeginCTRenderScope{ get;set; }
        // このRenderPassがコアテク独自のRenderGraphのプロファイリングスコープの終了か？
        public bool EndCTRenderScope{ get;set; }
        // マージ開始
        public bool MergeStart { get; set; }
        // マージ終了
        public bool MergeEnd { get; set; }

        // このRenderPassで使用されているサンプラーのリスト
        public ProfilingSampler ProfilingSampler { get; set; }

    }
    /// <summary>
    /// URPのRenderGraphUtilsの拡張クラス
    /// </summary>
    public static partial class RenderGraphUtils
    {
        public static void GetRenderPassData(List<RenderPassData> profilingSamplers, RenderGraph renderGraph)
        {
            if (renderGraph == null) return ;
            var renderPasses = renderGraph.m_RenderPasses;
            var nativeCompilerField = typeof(RenderGraph).GetField("nativeCompiler", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (nativeCompilerField == null) return;
            var nativeCompiler = nativeCompilerField.GetValue(renderGraph) as NativePassCompiler;
            if (nativeCompiler == null) return;
            var passData = nativeCompiler.contextData.passData;

            RenderPassData renderPassData = null;
            for (var passIndex = 0; passIndex < passData.Length; passIndex++)
            {
                var pass = renderPasses[passIndex];
                if (passData[passIndex].culled) continue;
                renderPassData = new()
                {
                    ProfilingSampler = pass.customSampler
                };
                if(pass.name == "Begin CTRenderGraph Profiling Scope" ){
                    // コアテク独自のRenderGraphのプロファイリングスコープの開始
                    renderPassData.BeginCTRenderScope = true;
                    profilingSamplers.Add(renderPassData);
                }else if(pass.name == "End CTRenderGraph Profiling Scope"){
                    // コアテク独自のRenderGraphのプロファイリングスコープの終了
                    renderPassData.EndCTRenderScope = true;
                    profilingSamplers.Add(renderPassData);
                }else if (passData[passIndex].mergeState == PassMergeState.Begin)
                {
                    // マージ開始を示すパスデータを作成する
                    var mergeStartPassData = new RenderPassData();
                    mergeStartPassData.MergeStart = true;
                    profilingSamplers.Add(mergeStartPassData);
                    profilingSamplers.Add(renderPassData);
                }else if (passData[passIndex].mergeState == PassMergeState.End)
                {
                    profilingSamplers.Add(renderPassData);
                    // マージ終了を示すパスデータを作成する
                    var mergeEndPassData = new RenderPassData();
                    mergeEndPassData.MergeEnd = true;
                    profilingSamplers.Add(mergeEndPassData);
                }
                else if(renderPassData.ProfilingSampler != null)
                {
                    // スタンドアローンパス
                    profilingSamplers.Add(renderPassData);
                }
            }

        }
    }
}
