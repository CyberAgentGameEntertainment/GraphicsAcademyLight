using System;
using UnityEngine;

namespace Sirius.DevSupport
{
    /// <summary>
    ///     テストシーンの設定を保持するクラス
    /// </summary>
    [Serializable]
    public class TestSceneParameter
    {
        [SerializeField]
        private string guid = "";
        public string Guid => guid;

        [SerializeField]
        private string sceneName = "";
        public string SceneName => sceneName;

        [SerializeField]
        private int renderCount;
        public int RenderCount => renderCount;

        public TestSceneParameter()
        {
        }

        public TestSceneParameter(string guid, string sceneName, int renderCount)
        {
            this.guid = guid;
            this.sceneName = sceneName;
            this.renderCount = renderCount;
        }
    }
}
