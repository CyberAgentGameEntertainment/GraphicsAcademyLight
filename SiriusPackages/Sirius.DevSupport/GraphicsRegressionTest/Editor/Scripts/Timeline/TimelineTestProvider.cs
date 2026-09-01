// --------------------------------------------------------------
// Copyright 2026 CyberAgent, Inc.
// --------------------------------------------------------------

#if HAS_TIMELINE
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.Playables;

namespace Sirius.DevSupport
{
    /// <summary>
    ///     <see cref="TimelineTestConfig" /> から TestCaseData を自動生成する Provider。
    ///     NUnit の <c>[TestCaseSource]</c> と組み合わせて使用する。
    /// </summary>
    public class TimelineTestProvider : IEnumerable
    {
        public IEnumerator GetEnumerator()
        {
            var guids = AssetDatabase.FindAssets("t:TimelineTestConfig");
            if (guids.Length == 0)
            {
                yield return new TestCaseData("", "", 0)
                    .SetName("Warning_NoTimelineTestConfigFound")
                    .Ignore("TimelineTestConfig アセットが見つかりません。\n" +
                            "Assets > Create > Sirius > Test > Timeline Test Config で作成してください。");
                yield break;
            }

            var hasEntry = false;

            var allEntries = guids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<TimelineTestConfig>)
                .Where(config => config != null)
                .SelectMany(config => config.entries)
                .Where(entry => entry.SceneAsset != null && entry.TimelinePrefab != null);

            foreach (var entry in allEntries)
            {
                var sceneName = Path.ChangeExtension(AssetDatabase.GetAssetPath(entry.SceneAsset), null);
                var prefabPath = AssetDatabase.GetAssetPath(entry.TimelinePrefab);
                if (string.IsNullOrEmpty(prefabPath))
                    continue;

                var director = entry.TimelinePrefab.GetComponentInChildren<PlayableDirector>(true);
                if (director == null)
                {
                    yield return new TestCaseData(sceneName, prefabPath, entry.RenderCount)
                        .SetName($"Warning_NoPlayableDirector_{entry.TimelinePrefab.name}")
                        .Ignore($"Prefab に PlayableDirector が見つかりません: {prefabPath}");
                    continue;
                }

                hasEntry = true;
                yield return new TestCaseData(sceneName, prefabPath, entry.RenderCount)
                    .SetName($"Test(\"{entry.SceneAsset.name}\",\"{entry.TimelinePrefab.name}\")");
            }

            if (!hasEntry)
            {
                yield return new TestCaseData("", "", 0)
                    .SetName("Warning_NoTimelineTestEntries")
                    .Ignore("TimelineTestConfig に有効なエントリが設定されていません。\n" +
                            "Inspector でシーンと PlayableDirector を含む Prefab を追加してください。");
            }
        }
    }
}
#endif
