// --------------------------------------------------------------
// Copyright 2026 CyberAgent, Inc.
// --------------------------------------------------------------

using System.Collections;
using System.IO;
using System.Linq;
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
    ///     <see cref="CameraTestConfig" /> から TestCaseData を自動生成する Provider。
    ///     NUnit の <c>[TestCaseSource]</c> と組み合わせて使用する。
    /// </summary>
    public class CameraTestProvider : IEnumerable
    {
        public IEnumerator GetEnumerator()
        {
            var guids = AssetDatabase.FindAssets("t:CameraTestConfig");
            if (guids.Length == 0)
            {
                yield return new TestCaseData("", "", "", 0)
                    .SetName("Warning_NoCameraTestConfigFound")
                    .Ignore("CameraTestConfig アセットが見つかりません。\n" +
                            "Assets > Create > Sirius > Test > Camera Test Config で作成してください。");
                yield break;
            }

            var hasEntry = false;

            // CameraTestConfig を収集
            var allEntries = guids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<CameraTestConfig>)
                .Where(config => config != null)
                .SelectMany(config => config.entries)
                .Where(entry => entry.SceneAsset != null && entry.CameraPrefab != null);

            foreach (var entry in allEntries)
            {
                var sceneName = Path.ChangeExtension(AssetDatabase.GetAssetPath(entry.SceneAsset), null);
                var prefabPath = AssetDatabase.GetAssetPath(entry.CameraPrefab);
                if (string.IsNullOrEmpty(prefabPath))
                    continue;

                var prefab = entry.CameraPrefab;

#if HAS_CINEMACHINE_V3
                var cinemachineCameras = prefab.GetComponentsInChildren<CinemachineCamera>(true);
                var cameraTransforms = cinemachineCameras.Length > 0
                    ? cinemachineCameras.Select(c => c.transform)
                    : prefab.GetComponentsInChildren<Camera>(true)
                        .Select(c => c.transform);
#elif HAS_CINEMACHINE_V2
                var cinemachineCameras = prefab.GetComponentsInChildren<CinemachineVirtualCamera>(true);
                var cameraTransforms = cinemachineCameras.Length > 0
                    ? cinemachineCameras.Select(c => c.transform)
                    : prefab.GetComponentsInChildren<Camera>(true)
                        .Select(c => c.transform);
#else
                var cameraTransforms = prefab.GetComponentsInChildren<Camera>(true)
                    .Select(c => c.transform);
#endif

                foreach (var camTransform in cameraTransforms)
                {
                    // Prefab内でのカメラの階層
                    var cameraPath = AnimationUtility.CalculateTransformPath(camTransform, prefab.transform);
                    var displayName = string.IsNullOrEmpty(cameraPath) ? prefab.name : cameraPath;

                    hasEntry = true;
                    yield return new TestCaseData(sceneName, prefabPath, cameraPath, entry.RenderCount)
                        .SetName($"Test(\"{entry.SceneAsset.name}\",\"{displayName}\")");
                }
            }

            if (!hasEntry)
            {
                yield return new TestCaseData("", "", "", 0)
                    .SetName("Warning_NoCameraTestEntries")
                    .Ignore("CameraTestConfig にエントリが設定されていません。\n" +
                            "Inspector でシーンとカメラ Prefab を追加してください。");
            }
        }
    }
}
