// --------------------------------------------------------------
// Copyright 2025 CyberAgent, Inc.
// --------------------------------------------------------------

using System;
using UnityEditor;
using UnityEngine;

namespace Sirius.DevSupport
{
    /// <summary>
    ///     カメラ Prefab を使ったグラフィックス回帰テストの設定を保持する ScriptableObject。
    ///     Inspector でシーン名とカメラ Prefab をドラッグ& ドロップで設定する。
    /// </summary>
    [CreateAssetMenu(fileName = "CameraTestConfig", menuName = "Sirius/Test/Camera Test Config")]
    public class CameraTestConfig : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            [SerializeField, Tooltip("テストシーン")]
            private SceneAsset _sceneAsset;
            public SceneAsset SceneAsset => _sceneAsset;

            [SerializeField, Tooltip("このシーンでテストするカメラ Prefab")]
            private GameObject _cameraPrefab;
            public GameObject CameraPrefab => _cameraPrefab;

            [SerializeField, Tooltip("レンダリング安定化フレーム数")]
            private int _renderCount;
            public int RenderCount => _renderCount;
        }

        [SerializeField]
        private Entry[] _entries = Array.Empty<Entry>();
        public Entry[] entries => _entries;
    }
}
