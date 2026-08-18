// --------------------------------------------------------------
// Copyright 2025 CyberAgent, Inc.
// --------------------------------------------------------------

using System;
using System.Runtime.CompilerServices;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Sirius.Core.Runtime.Scripts
{
    /// <summary>
    ///     Render Graph用のプロファイリングスコープ
    ///     RenderGraphProfilingScopeがAddRenderPassを使っているため使えないので自前で用意。
    ///     実装はAddUnsafeRenderPassを使っている点以外はRenderGraphProfilingScopeと同じ
    /// </summary>
    public struct CTRenderGraphProfilingScope : IDisposable
    {
        private readonly RenderGraph _renderGraph;
        private readonly ProfilingSampler _sampler;
        private bool _disposed;
        private class ProfilingScopePassData
        {
            public ProfilingSampler sampler;
        }
        private void BeginProfilingSampler([CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (_sampler == null)
                return;

            using var builder = _renderGraph.AddUnsafePass<ProfilingScopePassData>("Begin CTRenderGraph Profiling Scope", out var passData,
                                                                                   _sampler, file, line);
            passData.sampler = _sampler;
            builder.AllowPassCulling(false);
            builder.SetRenderFunc((ProfilingScopePassData data, UnsafeGraphContext ctx) =>
            {
                var cmd = CommandBufferHelpers.GetNativeCommandBuffer(ctx.cmd);
                data.sampler.Begin(cmd);
            });
#endif
        }
        private void EndProfilingSampler([CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (_sampler == null)
                return;

            using var builder = _renderGraph.AddUnsafePass<ProfilingScopePassData>("End CTRenderGraph Profiling Scope", out var passData,
                                                                                   _sampler, file, line);
            passData.sampler = _sampler;
            builder.AllowPassCulling(false);
            builder.SetRenderFunc((ProfilingScopePassData data, UnsafeGraphContext ctx) =>
            {
                var cmd = CommandBufferHelpers.GetNativeCommandBuffer(ctx.cmd);
                data.sampler.End(cmd);
            });
#endif
        }

        public CTRenderGraphProfilingScope(RenderGraph renderGraph, ProfilingSampler sampler)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (GlobalSettings.DevelopmentMode)
            {
                _renderGraph = renderGraph;
                _sampler = sampler;
                _disposed = false;
                BeginProfilingSampler();
            }else
            {
                _renderGraph = null;
                _sampler = null;
                _disposed = true;
            }
#else
            _renderGraph = null;
            _sampler = null;
            _disposed = true;
#endif
        }
        /// <summary>
        ///     Dispose pattern implementation
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        // Protected implementation of Dispose pattern.
        private void Dispose(bool disposing)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (_disposed)
                return;

            // As this is a struct, it could have been initialized using an empty constructor so we
            // need to make sure `cmd` isn't null to avoid a crash. Switching to a class would fix
            // this but will generate garbage on every frame (and this struct is used quite a lot).
            if (disposing && GlobalSettings.DevelopmentMode)
            {
                EndProfilingSampler();
            }

            _disposed = true;
#endif
        }
    }
}

