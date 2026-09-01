using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sirius.DevSupport
{
    /// <summary>
    /// Project SettingsにGraphics Regression Testの設定UIを表示するプロバイダー
    /// </summary>
    public class GraphicsRegressionTestSettingsProvider : SettingsProvider
    {
        private const string SettingsPath = "Project/Sirius/Graphics Regression Test";

        private SerializedObject _setting;
        private SerializedProperty _actualImagesPathProp;
        private SerializedProperty _successfulImagesPathProp;
        private SerializedProperty _sceneDirectoryProp;
        private SerializedProperty _sceneRenderCountSettingsProp;
        private SerializedProperty _autoApproveMissingReferencesProp;

        private string _cachedSceneDirectory;

        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            var provider = new GraphicsRegressionTestSettingsProvider(SettingsPath, SettingsScope.Project)
            {
                keywords = new[] { "Graphics", "Regression", "Test", "Image", "Path", "Visual", "FLIP" }
            };

            return provider;
        }

        private GraphicsRegressionTestSettingsProvider(string path, SettingsScope scopes) : base(path, scopes)
        {
        }

        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            GraphicsRegressionTestSettings.instance.OnRefresh += OnSettingsRefreshed;

            Initialize();

            // 初回のシーンリストリフレッシュ
            GraphicsRegressionTestSettings.instance.Refresh();
        }

        public override void OnDeactivate()
        {
            // イベント購読解除
            if (GraphicsRegressionTestSettings.instance != null)
            {
                GraphicsRegressionTestSettings.instance.OnRefresh -= OnSettingsRefreshed;
            }
        }

        private void Initialize()
        {
            var settings = GraphicsRegressionTestSettings.instance;
            _setting = new SerializedObject(settings);

            // FindPropertyを一度だけ実行してキャッシュ
            _actualImagesPathProp = _setting.FindProperty("actualImagesPath");
            _successfulImagesPathProp = _setting.FindProperty("successfulImagesPath");
            _sceneDirectoryProp = _setting.FindProperty("sceneTestDirectory");
            _sceneRenderCountSettingsProp = _setting.FindProperty("testSceneParameters");
            _autoApproveMissingReferencesProp = _setting.FindProperty("autoApproveMissingReferences");
        }

        public override void OnGUI(string searchContext)
        {
            if (_setting == null || _setting.targetObject == null)
            {
                Initialize();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Path Settings", EditorStyles.boldLabel);

            EditorGUILayout.Space();

            using var changeScope = new EditorGUI.ChangeCheckScope();

            // Actual Images Path with directory picker
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(_actualImagesPathProp, new GUIContent(
                    "Actual Images Path",
                    "テスト失敗時に生成される画像差分の出力先パス"));
                if (GUILayout.Button("...", GUILayout.Width(30)))
                {
                    var currentPath = GraphicsRegressionTestSettings.instance.ActualImagesPath;
                    var defaultPath = Directory.Exists(currentPath) ? currentPath : "Assets";
                    var selectedPath = EditorUtility.OpenFolderPanel("Select Actual Images Directory", defaultPath, "");
                    if (!string.IsNullOrEmpty(selectedPath))
                    {
                        _actualImagesPathProp.stringValue = MakeRelativePath(selectedPath);
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                // Successful Images Path with directory picker
                EditorGUILayout.PropertyField(_successfulImagesPathProp, new GUIContent(
                    "Successful Images Path",
                    "期待される画像のパス"));
                if (GUILayout.Button("...", GUILayout.Width(30)))
                {
                    var currentPath = GraphicsRegressionTestSettings.instance.SuccessfulImagesPath;
                    var defaultPath = Directory.Exists(currentPath) ? currentPath : "Assets";
                    var selectedPath = EditorUtility.OpenFolderPanel("Select Successful Images Directory", defaultPath, "");
                    if (!string.IsNullOrEmpty(selectedPath))
                    {
                        _successfulImagesPathProp.stringValue = MakeRelativePath(selectedPath);
                    }
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Test Behavior", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(_autoApproveMissingReferencesProp, new GUIContent(
                "Save Missing References",
                "有効にすると、リファレンス画像がない場合に自動的に撮影した画像を保存します。"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Scene Test Directory", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(_sceneDirectoryProp, new GUIContent(
                    "Scene Test Directory",
                    "テストとなるシーンを格納するディレクトリパス"));

                if (GUILayout.Button("...", GUILayout.Width(30)))
                {
                    var currentPath = GraphicsRegressionTestSettings.instance.SceneTestDirectory;
                    var defaultPath = Directory.Exists(currentPath) ? currentPath : "Assets";
                    var selectedPath = EditorUtility.OpenFolderPanel("Select Scene Test Directory", defaultPath, "");
                    if (!string.IsNullOrEmpty(selectedPath))
                    {
                        _sceneDirectoryProp.stringValue = MakeRelativePath(selectedPath);
                    }
                }
            }

            EditorGUILayout.Space();

            // ディレクトリ変更検出
            var currentDirectory = GraphicsRegressionTestSettings.instance.SceneTestDirectory;
            if (_cachedSceneDirectory != currentDirectory)
            {
                GraphicsRegressionTestSettings.instance.Refresh();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Scene Render Count Settings", EditorStyles.boldLabel);
                if (GUILayout.Button("Refresh Scene List", GUILayout.Width(150)))
                {
                    GraphicsRegressionTestSettings.instance.Refresh();
                }
                if (GUILayout.Button("Refresh Test Runner", GUILayout.Width(150)))
                {
                    // TestCaseSourceを再評価するため、スクリプトを再ロードします
                    EditorUtility.RequestScriptReload();
                }
            }

            // 表形式でシーン名とrenderCountを表示
            if (_sceneRenderCountSettingsProp.arraySize > 0)
            {
                using var _ = new EditorGUILayout.VerticalScope(EditorStyles.helpBox);

                // ヘッダー
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("Scene Name", EditorStyles.boldLabel, GUILayout.Width(300));
                    GUILayout.Label("Render Count", EditorStyles.boldLabel, GUILayout.Width(100));
                }

                // 区切り線
                var rect = EditorGUILayout.GetControlRect(false, 1);
                EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 1));

                // シーンリスト（交互に背景色を変える）
                for (var i = 0; i < _sceneRenderCountSettingsProp.arraySize; i++)
                {
                    var bgColor = i % 2 == 0 ? new Color(0, 0, 0, 0.1f) : Color.clear;

                    using var horizontalScope = new EditorGUILayout.HorizontalScope(GUILayout.Height(EditorGUIUtility.singleLineHeight + 2));

                    if (Event.current.type == EventType.Repaint)
                    {
                        EditorGUI.DrawRect(horizontalScope.rect, bgColor);
                    }

                    var elementProp = _sceneRenderCountSettingsProp.GetArrayElementAtIndex(i);
                    var sceneNameProp = elementProp.FindPropertyRelative("sceneName");
                    var renderCountProp = elementProp.FindPropertyRelative("renderCount");

                    // シーン名は読み取り専用
                    GUILayout.Label(sceneNameProp.stringValue, GUILayout.Width(300));

                    // renderCountのみ編集可能
                    EditorGUILayout.PropertyField(renderCountProp, GUIContent.none, GUILayout.Width(100));
                }
            }
            else
            {
                EditorGUILayout.HelpBox("No scenes found in the configured directories.", MessageType.Info);
            }

            EditorGUILayout.Space();

            if (changeScope.changed)
            {
                _setting.ApplyModifiedProperties();
                GraphicsRegressionTestSettings.instance.Save();
            }
        }

        /// <summary>
        ///     Settings が更新された後に呼ばれ、SerializedObject を更新します
        /// </summary>
        private void OnSettingsRefreshed()
        {
            var settings = GraphicsRegressionTestSettings.instance;

            // 現在のディレクトリ設定をキャッシュ
            _cachedSceneDirectory = settings.SceneTestDirectory;

            // SerializedObject を更新して UI に反映
            _setting.Update();
        }

        private static string MakeRelativePath(string absolutePath)
        {
            var projectPath = Application.dataPath.Replace("/Assets", "");
            if (absolutePath.StartsWith(projectPath))
            {
                return absolutePath.Substring(projectPath.Length + 1).Replace("\\", "/");
            }
            return absolutePath;
        }
    }
}
