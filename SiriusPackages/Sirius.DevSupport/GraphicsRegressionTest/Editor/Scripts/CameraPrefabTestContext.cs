// --------------------------------------------------------------
// Copyright 2026 CyberAgent, Inc.
// --------------------------------------------------------------

using NUnit.Framework;
using UnityEditor;
using UnityEngine;
#if HAS_CINEMACHINE_V3
using Unity.Cinemachine;
#elif HAS_CINEMACHINE_V2
using Cinemachine;
#endif

namespace Sirius.DevSupport
{
    /// <summary>
    ///     カメラ Prefab を使ったグラフィックス回帰テストのコンテキスト。
    ///     シーンのキャッシュ付きロードと、カメラ Prefab の配置・復元を管理する。
    /// </summary>
    public class CameraPrefabTestContext
    {
        private string _cachedScene;
        public GameObject PrefabInstance { get; private set; }

        /// <summary>
        /// キャッシュされたシーンでなければ、ロードする
        /// </summary>
        /// <param name="scenePath">ロードするシーンのパス</param>
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
        /// シーンをキャプチャするためのカメラの設定
        /// </summary>
        /// <param name="prefabPath">生成するPrefabのPath</param>
        /// <param name="cameraPath">キャプチャするカメラのPrefabのパス</param>
        /// <param name="renderCount">描画を安定させるための回数</param>
        /// <returns></returns>
        public async Awaitable<Camera> SetupPrefabCameraAsync(string prefabPath, string cameraPath, int renderCount)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assume.That(prefab, Is.Not.Null, $"Camera Prefab が見つかりません: {prefabPath}");
            PrefabInstance = Object.Instantiate(prefab);

            var target = FindTarget(PrefabInstance.transform, cameraPath);

#if HAS_CINEMACHINE_V2 || HAS_CINEMACHINE_V3
            if (!TrySetupCinemachineCamera(target, PrefabInstance))
#endif
            {
                SetupCamera(target);
            }

            await TestUtility.StabilizeRenderingAsync(renderCount);
            return Camera.main;
        }

        public void Cleanup()
        {
            if (PrefabInstance != null)
            {
                Object.Destroy(PrefabInstance);
                PrefabInstance = null;
            }

            _cachedScene = null;
        }

#if HAS_CINEMACHINE_V2 || HAS_CINEMACHINE_V3
        private static bool TrySetupCinemachineCamera(Transform target, GameObject cameraPrefab)
        {
#if HAS_CINEMACHINE_V3
            var targetVcam = target.GetComponent<CinemachineCamera>();
#else
            var targetVcam = target.GetComponent<CinemachineVirtualCamera>();
#endif
            if (targetVcam == null)
                return false;

            var mainCamera = Camera.main;
            // mainCameraがBrainではなかったらBrain扱いにする
            var brain = mainCamera.GetComponent<CinemachineBrain>()
                        ?? mainCamera.gameObject.AddComponent<CinemachineBrain>();
#if HAS_CINEMACHINE_V3
            brain.DefaultBlend = new CinemachineBlendDefinition(
                CinemachineBlendDefinition.Styles.Cut, 0f);
#else
            brain.m_DefaultBlend = new CinemachineBlendDefinition(
                CinemachineBlendDefinition.Style.Cut, 0f);
#endif

#if HAS_CINEMACHINE_V3
            var cinemachineCameras = cameraPrefab.GetComponentsInChildren<CinemachineCamera>(true);
#else
            var cinemachineCameras = cameraPrefab.GetComponentsInChildren<CinemachineVirtualCamera>(true);
#endif
            foreach (var cinemachineCamera in cinemachineCameras)
                cinemachineCamera.enabled = false;

            targetVcam.enabled = true;
            return true;
        }
#endif

        private void SetupCamera(Transform target)
        {
            var targetCamera = target.GetComponent<Camera>();
            Assume.That(targetCamera, Is.Not.Null, "SetupCamera: target に Camera コンポーネントが必要です");

            // Prefab 内の全カメラを無効化・タグ除去
            foreach (var cam in PrefabInstance.GetComponentsInChildren<Camera>(true))
            {
                cam.gameObject.tag = "Untagged";
                cam.enabled = false;
            }

            // ターゲットカメラを MainCamera として有効化
            targetCamera.gameObject.tag = "MainCamera";
            targetCamera.enabled = true;
        }

        private static Transform FindTarget(Transform root, string cameraPath)
        {
            var target = string.IsNullOrEmpty(cameraPath) ? root : root.Find(cameraPath);
            Assume.That(target, Is.Not.Null, $"カメラが見つかりません: {cameraPath}");
            return target;
        }
    }
}
