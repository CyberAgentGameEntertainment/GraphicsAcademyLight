using UnityEngine;

namespace Sirius.Core.Runtime.Scripts
{
    // 付けてるカメラに指定のSirius機能を実行されないようにするコンポーネント
    [RequireComponent(typeof(Camera))]
    public class SiriusIgnorer : MonoBehaviour
    {
        [Header("チェックされてる機能はそのカメラに実行されないようになる")]
        [SerializeField] private bool ignorePostProcessingRendererFeature;

        public bool IgnorePostProcessingRendererFeature
        {
            get => enabled && ignorePostProcessingRendererFeature;
            set => ignorePostProcessingRendererFeature = value;
        }
    }
}
