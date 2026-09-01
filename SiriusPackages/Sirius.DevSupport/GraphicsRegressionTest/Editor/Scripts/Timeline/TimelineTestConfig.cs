// --------------------------------------------------------------
// Copyright 2026 CyberAgent, Inc.
// --------------------------------------------------------------

#if HAS_TIMELINE
using System;
using UnityEditor;
using UnityEngine;

namespace Sirius.DevSupport
{
    /// <summary>
    ///     Timeline 駆動下のレンダリング結果を毎フレーム比較するグラフィックス回帰テストの設定を保持する ScriptableObject。
    ///     Inspector でシーンと PlayableDirector を含む Prefab をドラッグ& ドロップで設定する。
    /// </summary>
    [CreateAssetMenu(fileName = "TimelineTestConfig", menuName = "Sirius/Test/Timeline Test Config")]
    public class TimelineTestConfig : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            [SerializeField, Tooltip("テストシーン")]
            private SceneAsset _sceneAsset;
            public SceneAsset SceneAsset => _sceneAsset;

            [SerializeField, Tooltip("このシーンでテストするTimeline Prefab")]
            private GameObject _timelinePrefab;
            public GameObject TimelinePrefab => _timelinePrefab;

            [SerializeField, Tooltip("レンダリング安定化フレーム数")]
            private int _renderCount;
            public int RenderCount => _renderCount;
        }

        [SerializeField]
        private Entry[] _entries = Array.Empty<Entry>();
        public Entry[] entries => _entries;
    }
}
#endif
