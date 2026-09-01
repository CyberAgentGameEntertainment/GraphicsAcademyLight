using UnityEngine;
using UnityEngine.Rendering;

namespace Sirius.PostProcessing.Runtime.Scripts.Volumes
{
    [VolumeComponentMenu("Sirius/DirectionalBlur")]
    public class DirectionalBlurVolume : VolumeComponent, IPostProcessComponent
    {
        [SerializeField, Tooltip("ブラーテクセル固定サンプリングを使用するかどうか")]
        private BoolParameter _useFixedAspectSampling = new(false);
        [SerializeField, Tooltip("ブラー方向")]
        private ClampedFloatParameter _angle = new(0.0f, 0.0f, 360.0f);
        [SerializeField, Tooltip("ブラーの強度")]
        private ClampedFloatParameter _strength = new(0.0f, 0.0f, 1.0f);
        [SerializeField, Tooltip("ブラー距離")]
        private ClampedFloatParameter _width = new(0.8f, 0.0f, 10.0f);
        [SerializeField, Tooltip("マスク(Rチャネルのみ使用)")]
        private TextureParameter _mask = new(null);

        public bool UseFixedAspectSampling
        {
            get => _useFixedAspectSampling.value;
            set => _useFixedAspectSampling.value = value;
        }

        public float Angle
        {
            get => _angle.value;
            set => _angle.value = value;
        }

        public float Strength
        {
            get => _strength.value;
            set => _strength.value = value;
        }

        public float Width
        {
            get => _width.value;
            set => _width.value = value;
        }

        public Texture Mask
        {
            get => _mask.value;
            set => _mask.value = value;
        }

        public bool IsActive()
        {
            return Strength > 0.0f && Width > 0.0f;
        }

        public bool IsTileCompatible() => false;
    }
}
