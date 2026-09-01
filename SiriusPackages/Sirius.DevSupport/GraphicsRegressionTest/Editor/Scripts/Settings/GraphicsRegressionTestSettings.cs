using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Sirius.DevSupport
{
    /// <summary>
    ///     Graphics Regression Testフレームワークの設定を管理するクラス
    /// </summary>
    [FilePath("ProjectSettings/GraphicsRegressionTestSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public class GraphicsRegressionTestSettings : ScriptableSingleton<GraphicsRegressionTestSettings>
    {
        private const int DefaultRenderCount = 0;

        [SerializeField]
        private string actualImagesPath = "Assets/ActualImages";
        public string ActualImagesPath => actualImagesPath;

        [SerializeField]
        private string successfulImagesPath = "Assets/Tests/SuccessfulImages";
        public string SuccessfulImagesPath => successfulImagesPath;

        [SerializeField]
        private string sceneTestDirectory = "Assets/Demo/Scenes";
        public string SceneTestDirectory => sceneTestDirectory;

        [SerializeField]
        private TestSceneParameter[] testSceneParameters = Array.Empty<TestSceneParameter>();
        public TestSceneParameter[] TestSceneParameters => testSceneParameters;

        [SerializeField]
        private bool autoApproveMissingReferences = true;
        public bool AutoApproveMissingReferences => autoApproveMissingReferences;

        public event Action OnRefresh;

        public void Refresh()
        {
            RefreshSceneList();
            OnRefresh?.Invoke();
        }

        /// <summary>
        ///     SceneTestDirectoryから全シーンを収集し、sceneRenderCountSettingsを更新します
        /// </summary>
        private void RefreshSceneList()
        {
            // 既存の設定をGUIDベースのDictionaryに変換
            var existingSettings = testSceneParameters
                .ToDictionary(s => s.Guid, s => s.RenderCount);

            var directory = SceneTestDirectory;

            if (!string.IsNullOrEmpty(directory))
            {
                var guids = AssetDatabase.FindAssets("t:Scene", new[] { directory });

                // ディレクトリパスを正規化（末尾のスラッシュを削除）
                var normalizedDirectory = directory.TrimEnd('/', '\\');

                var sortedScenes = guids
                    .Select(guid =>
                    {
                        var path = AssetDatabase.GUIDToAssetPath(guid);

                        // 指定ディレクトリからの相対パスを計算
                        var relativePath = path.Substring(normalizedDirectory.Length + 1);
                        var displayName = relativePath.Replace("\\", "/"); // Windows対応
                        displayName = Path.ChangeExtension(displayName, null); // .unity削除

                        var renderCount = existingSettings.GetValueOrDefault(guid, DefaultRenderCount);

                        return (guid, displayName, renderCount);
                    })
                    .OrderBy(s => s.displayName)
                    .ToArray();

                // 配列を直接更新
                testSceneParameters = sortedScenes
                    .Select(s => new TestSceneParameter(s.guid, s.displayName, s.renderCount))
                    .ToArray();
            }
            else
            {
                // ディレクトリが空の場合は配列をクリア
                testSceneParameters = Array.Empty<TestSceneParameter>();
            }

            Save();
        }

        public void Save()
        {
            Save(true);
        }
    }
}
