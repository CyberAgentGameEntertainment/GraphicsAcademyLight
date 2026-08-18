using UnityEngine;
using UnityEngine.Rendering;

namespace Sirius.PostProcessing.Runtime.Scripts.Volumes
{
    [VolumeComponentMenu("Sirius/RotationBlur")]
    public class RotationBlurVolume : VolumeComponent, IPostProcessComponent
    {
        [SerializeField, Tooltip("回転ブラーの中心点 X（UV座標 0〜1）")]
        private ClampedFloatParameter _centerX = new(0.5f, 0.0f, 1.0f);
        [SerializeField, Tooltip("回転ブラーの中心点 Y（UV座標 0〜1）")]
        private ClampedFloatParameter _centerY = new(0.5f, 0.0f, 1.0f);
        [SerializeField, Tooltip("ブラーの強度（距離ベースのブレンド係数）")]
        private ClampedFloatParameter _strength = new(0.0f, 0.0f, 10.0f);
        [SerializeField, Tooltip("ブラー幅（接線方向のサンプリング距離スケール）")]
        private ClampedFloatParameter _width = new(0.8f, 0.0f, 10.0f);
        [SerializeField, Tooltip("マスク（Rチャネルのみ使用）")]
        private TextureParameter _mask = new(null);

        public float CenterX { get => _centerX.value; set => _centerX.value = value; }
        public float CenterY { get => _centerY.value; set => _centerY.value = value; }
        public float Strength { get => _strength.value; set => _strength.value = value; }
        public float Width { get => _width.value; set => _width.value = value; }
        public Texture Mask { get => _mask.value; set => _mask.value = value; }

        public bool IsActive() => Strength > 0.0f && Width > 0.0f;
        public bool IsTileCompatible() => false;
    }
}
