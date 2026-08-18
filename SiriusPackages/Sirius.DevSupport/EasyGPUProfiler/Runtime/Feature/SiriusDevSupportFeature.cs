// --------------------------------------------------------------
// Copyright 2025 CyberAgent, Inc.
// --------------------------------------------------------------

using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Sirius.DevSupport
{
    public class SiriusDevSupportFeature : ScriptableRendererFeature
    {
        [SerializeField] private bool allowEasyGPUProfiler = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private ProfilingSamplerCollectorPass _profilingSamplerCollectorPass;
#endif
        public override void Create()
        {
        }
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (allowEasyGPUProfiler)
            {
                _profilingSamplerCollectorPass ??= new ProfilingSamplerCollectorPass();
                renderer.EnqueuePass(_profilingSamplerCollectorPass);
            }
            else
            {
                _profilingSamplerCollectorPass = null;
            }
#endif
        }
    }
}
