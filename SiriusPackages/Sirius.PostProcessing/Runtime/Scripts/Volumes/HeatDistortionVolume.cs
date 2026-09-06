using UnityEngine;
using UnityEngine.Rendering;

namespace Sirius.PostProcessing.Runtime.Scripts.Volumes
{
    [VolumeComponentMenu("Sirius/HeatDistortion")]
    public class HeatDistortionVolume : VolumeComponent, IPostProcessComponent
    {
        [SerializeField, Tooltip("歪みの強度")]
        private ClampedFloatParameter _intensity = new(0.0f, 0.0f, 10.0f);
        [SerializeField, Tooltip("歪み結果を元画像へ合成する割合(lerpのt)。0で元画像そのまま")]
        private ClampedFloatParameter _blend = new(1.0f, 0.0f, 1.0f);
        [SerializeField, Tooltip("効果が発生し始めるカメラからの距離")]
        private ClampedFloatParameter _startDistance = new(20.0f, 0.0f, 1000.0f);
        [SerializeField, Tooltip("効果が最大強度に達するカメラからの距離")]
        private ClampedFloatParameter _fadeDistance = new(150.0f, 0.0f, 1000.0f);
        [SerializeField, Tooltip("揺らぎパターンが流れる速さ")]
        private ClampedFloatParameter _speed = new(0.1369f, 0.0f, 5.0f);
        [SerializeField, Tooltip("色収差の強さ")]
        private ClampedFloatParameter _chromaticSeparation = new(0.5f, 0.0f, 1.0f);
        [SerializeField, Tooltip("揺らぎノイズの座標スケール")]
        private ClampedFloatParameter _noiseScale = new(1.7f, 0.0f, 10.0f);
        [SerializeField, Tooltip("揺らぎパターン生成用の3Dノイズテクスチャ(Texture2DArray)")]
        private TextureParameter _noiseTexture = new(null);

        public float Intensity
        {
            get => _intensity.value;
            set => _intensity.value = value;
        }

        public float Blend
        {
            get => _blend.value;
            set => _blend.value = value;
        }

        public float StartDistance
        {
            get => _startDistance.value;
            set => _startDistance.value = value;
        }

        public float FadeDistance
        {
            get => _fadeDistance.value;
            set => _fadeDistance.value = value;
        }

        public float Speed
        {
            get => _speed.value;
            set => _speed.value = value;
        }

        public float ChromaticSeparation
        {
            get => _chromaticSeparation.value;
            set => _chromaticSeparation.value = value;
        }

        public float NoiseScale
        {
            get => _noiseScale.value;
            set => _noiseScale.value = value;
        }

        public Texture NoiseTexture
        {
            get => _noiseTexture.value;
            set => _noiseTexture.value = value;
        }

        public bool IsActive()
        {
            return Blend > 0.0f && Intensity > 0.0f && FadeDistance > 0.0f;
        }

        public bool IsTileCompatible() => false;
    }
}
