using UnityEngine;

namespace Sirius.PostProcessing.Editor
{
    [CreateAssetMenu(fileName = "ShaderIncluderSettings", menuName = "Sirius/ShaderIncluderSettings", order = 1)]
    public class ShaderIncluderSettings : ScriptableObject
    {
        public enum IncludeRangeEnum
        {
            None,                   // 何も含めない
            OnlyBuildSceneCameras,  // ビルドするシーン内のカメラに使われるShaderのみ
            OnlyCurrentQuality,     // 現在のQuality設定で使われるShaderのみ
            AllQuality,             // 全てのQuality設定で使われるShader（デフォルト）
        }

        [SerializeField]
        private IncludeRangeEnum includeRange = IncludeRangeEnum.AllQuality;

        public IncludeRangeEnum IncludeRange
        {
            get => includeRange;
            set => includeRange = value;
        }
    }
}
