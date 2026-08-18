// --------------------------------------------------------------
// Copyright 2026 CyberAgent, Inc.
// --------------------------------------------------------------

#if HAS_TIMELINE
using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Sirius.DevSupport
{
    /// <summary>
    ///     PlayableDirector を含む Prefab を使った Timeline 回帰テストのコンテキスト。
    /// </summary>
    public class TimelinePrefabTestContext
    {
        public GameObject PrefabInstance { get; private set; }
        public int FrameCount { get; private set; }

        private PlayableDirector _playableDirector;
        private string _cachedScene;
        private double _frameRate;

        /// <summary>
        ///     キャッシュされたシーンでなければ、ロードする
        /// </summary>
        /// <param name="scenePath">ロードするシーンのパス（拡張子なし）</param>
        /// <param name="renderCount">描画を安定させるための回数</param>
        public async Awaitable LoadSceneIfNeededAsync(string scenePath, int renderCount)
        {
            if (_cachedScene == scenePath)
            {
                return;
            }

            await TestUtility.LoadSceneAndStabilizeRenderingAsync(scenePath, renderCount);
            _cachedScene = scenePath;
        }

        /// <summary>
        ///     Prefab を Instantiate し、PlayableDirector を Manual モードに切り替える。
        /// </summary>
        public void SetupPrefab(string prefabPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assume.That(prefab, Is.Not.Null, $"Prefab が見つかりません: {prefabPath}");
            PrefabInstance = UnityEngine.Object.Instantiate(prefab);

            _playableDirector = PrefabInstance.GetComponentInChildren<PlayableDirector>(true);
            Assume.That(_playableDirector, Is.Not.Null, $"Prefab に PlayableDirector が見つかりません: {prefabPath}");

            var timelineAsset = _playableDirector.playableAsset as TimelineAsset;
            Assume.That(timelineAsset, Is.Not.Null, $"PlayableDirector に TimelineAsset がバインドされていません: {prefabPath}");

            _playableDirector.timeUpdateMode = DirectorUpdateMode.Manual;
            _playableDirector.time = 0;
            _playableDirector.Evaluate();

            var fps = timelineAsset.editorSettings.frameRate;
            var duration = _playableDirector.duration;
            FrameCount = (int)Math.Floor(duration * fps) + 1;
            _frameRate = fps;
        }

        /// <summary>
        ///     Director を frameIndex 番目のフレームへ進めて Evaluate する
        /// </summary>
        public void EvaluateFrame(int frameIndex)
        {
            _playableDirector.time = frameIndex / _frameRate;
            _playableDirector.Evaluate();
        }

        public void Cleanup()
        {
            if (PrefabInstance != null)
            {
                UnityEngine.Object.Destroy(PrefabInstance);
                PrefabInstance = null;
                _playableDirector = null;
            }

            _cachedScene = null;
        }
    }
}
#endif
