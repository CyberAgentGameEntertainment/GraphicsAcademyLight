// --------------------------------------------------------------
// Copyright 2025 CyberAgent, Inc.
// --------------------------------------------------------------

namespace Sirius.DevSupport
{
    /// <summary>
    ///     画像比較の設定クラス
    /// </summary>
    public class ImageComparisonSettings
    {
        public int TargetWidth { get; set; }
        public int TargetHeight { get; set; }
        public int TargetMSAASamples { get; set; } = 1;
        public bool UseHDR { get; set; } = false;
        public float AverageCorrectnessThreshold { get; set; }
        public float MaxCorrectnessThreshold { get; set; }
    }
}
