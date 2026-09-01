using UnityEngine;
using UnityEngine.Rendering;

namespace Sirius.PostProcessing.Runtime.Scripts.Volumes
{
    [VolumeComponentMenu("Sirius/RadialBlur")]
    public class RadialBlurVolume : VolumeComponent, IPostProcessComponent
    {
        [SerializeField, Tooltip("ブラーのかかる注視点")]
        private ClampedFloatParameter _gazePositionX = new(0.5f, 0.0f, 1.0f);
        [SerializeField, Tooltip("ブラーのかかる注視点")]
        private ClampedFloatParameter _gazePositionY = new(0.5f, 0.0f, 1.0f);
        [SerializeField, Tooltip("ブラーの強度")]
        private ClampedFloatParameter _strength = new(0.0f, 0.0f, 10.0f);
        [SerializeField, Tooltip("ブラー距離")]
        private ClampedFloatParameter _width = new(0.8f, 0.0f, 10.0f);
        [SerializeField, Tooltip("オフセット")]
        private ClampedFloatParameter _offset = new(0.0f, 0.0f, 1.0f);
        [SerializeField, Tooltip("マスク(Rチャネルのみ使用)")]
        private TextureParameter _mask = new(null);

        public float GazePositionX
        {
            get => _gazePositionX.value;
            set => _gazePositionX.value = value;
        }

        public float GazePositionY
        {
            get => _gazePositionY.value;
            set => _gazePositionY.value = value;
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

        public float Offset
        {
            get => _offset.value;
            set => _offset.value = value;
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
