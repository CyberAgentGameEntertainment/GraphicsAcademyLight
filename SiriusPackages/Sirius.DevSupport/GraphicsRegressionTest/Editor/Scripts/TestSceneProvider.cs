// --------------------------------------------------------------
// Copyright 2025 CyberAgent, Inc.
// --------------------------------------------------------------

using System.Collections;
using NUnit.Framework;

namespace Sirius.DevSupport
{
    /// <summary>
    /// ProjectSettingsで設定されたディレクトリから.unityファイルを自動収集してテストケースを生成
    /// </summary>
    public class TestSceneProvider : IEnumerable
    {
        public IEnumerator GetEnumerator()
        {
            var settings = GraphicsRegressionTestSettings.instance;
            settings.Refresh();

            if (settings.TestSceneParameters.Length == 0)
            {
                yield return new TestCaseData(null, 0)
                    .SetName("Warning_NoScenesFound")
                    .Ignore("No test scenes found. Please check:\n" +
                            "1. ProjectSettings > Graphics Regression Test > Scene Test Directory\n" +
                            "2. Click 'Refresh Scene List' button to update the scene list");
                yield break;
            }

            foreach (var scene in settings.TestSceneParameters)
            {
                yield return new TestCaseData(scene.SceneName, scene.RenderCount)
                    .SetName($"Test(\"{scene.SceneName}\",{scene.RenderCount})");
            }
        }
    }
}
