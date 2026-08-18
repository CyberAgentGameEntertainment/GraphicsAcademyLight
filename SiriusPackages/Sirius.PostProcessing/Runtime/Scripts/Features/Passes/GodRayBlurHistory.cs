using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Sirius.PostProcessing.Runtime.Scripts.Features.Passes
{
    /// <summary>
    /// URPのTaaHistory、RawDepthHistory、RawColorHistory、UniversalCameraHistoryあたりを参考にしてGodRayBlurHistoryを作成
    /// </summary>
    public class GodRayBlurHistory : CameraHistoryItem
    {
        private const string Name = "GodRayTemporalBlurTex";
        private int _id;

        private RenderTextureDescriptor _descriptor;
        private Hash128 _descKey;

        public override void OnCreate(BufferedRTHandleSystem owner, uint typeId)
        {
            base.OnCreate(owner, typeId);
            _id = MakeId(0);
        }

        public override void Reset()
        {
            ReleaseHistoryFrameRT(_id);
            _descriptor.width = 0;
            _descriptor.height = 0;
            _descriptor.graphicsFormat = GraphicsFormat.None;
            _descKey = Hash128.Compute(0);
        }

        /// <summary>
        /// Get the current history texture.
        /// Current history might not be valid yet. It is valid only after executing the producing render pass.
        /// </summary>
        /// <param name="eyeIndex">Eye index, typically XRPass.multipassId.</param>
        /// <returns>The texture.</returns>
        public RTHandle GetCurrentTexture()
        {
            return GetCurrentFrameRT(_id);
        }

        /// <summary>
        /// Get the previous history texture.
        /// Previous history might not be valid yet. It is valid only after executing the producing render pass.
        /// </summary>
        /// <param name="eyeIndex">Eye index, typically XRPass.multipassId.</param>
        /// <returns>The texture.</returns>
        public RTHandle GetPreviousTexture()
        {
            return GetPreviousFrameRT(_id);
        }

        private bool IsAllocated()
        {
            return GetCurrentTexture() != null;
        }

        // True if the desc changed, graphicsFormat etc.
        private bool IsDirty(ref RenderTextureDescriptor desc)
        {
            return _descKey != Hash128.Compute(ref desc);
        }

        private void Alloc(ref RenderTextureDescriptor desc)
        {
            // In generic case, the current texture might not have been written yet. We need double buffering.
            AllocHistoryFrameRT(_id, 2, ref desc, Name);
            _descriptor = desc;
            _descKey = Hash128.Compute(ref desc);
        }

        internal RenderTextureDescriptor GetHistoryDescriptor(ref RenderTextureDescriptor cameraDesc)
        {
            var colorDesc = cameraDesc;
            colorDesc.depthBufferBits = (int)DepthBits.None;
            colorDesc.mipCount = 0;
            colorDesc.msaaSamples = 1;

            return colorDesc;
        }

        // Return true if the RTHandles were reallocated.
        internal bool Update(ref RenderTextureDescriptor desc)
        {
            if (desc is { width: > 0, height: > 0 } && desc.graphicsFormat != GraphicsFormat.None)
            {
                var historyDesc = GetHistoryDescriptor(ref desc);

                if (IsDirty(ref historyDesc))
                    Reset();

                if (!IsAllocated())
                {
                    Alloc(ref historyDesc);
                    return true;
                }
            }

            return false;
        }
    }
}
