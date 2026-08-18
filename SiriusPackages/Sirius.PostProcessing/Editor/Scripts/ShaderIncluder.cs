using System.Collections.Generic;
using System.Linq;
using Sirius.PostProcessing.Runtime.Scripts.Features;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Sirius.PostProcessing.Editor
{
    // ビルド時にSiriusPostProcessingFeatureで使用されるShaderをAlwaysIncludedShaderに追加する
    public class ShaderIncluder : IPreprocessBuildWithReport
    {
        private static IEnumerable<string> SiriusShaderNames { get; } = SiriusPostProcessingFeature.GetAllShaderNameList();

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            Debug.Log($"--- Starting SiriusPostProcessing Shader Including ---");
            var settings = LoadSettings();
            // 設定ファイルがないならデフォルトの設定を使用
            var checkRange = settings?.IncludeRange ?? ShaderIncluderSettings.IncludeRangeEnum.AllQuality;
            IncludeShader(checkRange);
            Debug.Log($"--- Finished SiriusPostProcessing Shader Including ---");
        }

        private static ShaderIncluderSettings LoadSettings()
        {
            // "t:ShaderIncluderSettings" で型名検索、Assets配下すべて対象
            const string typeName = nameof(ShaderIncluderSettings);
            var guid = AssetDatabase.FindAssets($"t:{typeName}", new[] { "Assets" }).FirstOrDefault();
            if (guid == null)
            {
                Debug.Log("ShaderIncluderSettingsアセットが見つからないため、デフォルト設定が使用される");
                return null;
            }

            var path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<ShaderIncluderSettings>(path);
            if (asset == null)
            {
                Debug.Log("ShaderIncluderSettingsアセットが見つからないため、デフォルト設定が使用される");
                return null;
            }

            Debug.Log($"ShaderIncluderSettings found: {path}");
            return asset;
        }

        private static HashSet<Shader> GetOtherShaders(ref SerializedProperty alwaysIncludedShadersProperty)
        {
            var otherShaders = new HashSet<Shader>();
            for (int i = 0; i < alwaysIncludedShadersProperty.arraySize; i++)
            {
                var element = alwaysIncludedShadersProperty.GetArrayElementAtIndex(i);
                var shader = element.objectReferenceValue as Shader;
                if (!shader || SiriusShaderNames.Contains(shader.name))
                {
                    continue;
                }
                otherShaders.Add(shader);
            }
            return otherShaders;
        }

        // 格納したShaderリストをAlwaysIncludedShadersにシリアライズする
        private static void SerializeAlwaysIncludedShaderList(
            ref SerializedProperty alwaysIncludedShadersProperty,
            in HashSet<Shader> otherAlwaysIncludedShaders,
            in HashSet<Shader> siriusAlwaysIncludedShaders)
        {
            var totalSize = otherAlwaysIncludedShaders.Count + siriusAlwaysIncludedShaders.Count;
            alwaysIncludedShadersProperty.arraySize = totalSize;
            var i = 0;
            foreach (var shader in otherAlwaysIncludedShaders)
            {
                var element = alwaysIncludedShadersProperty.GetArrayElementAtIndex(i);
                element.objectReferenceValue = shader;
                i++;
            }
            foreach (var shader in siriusAlwaysIncludedShaders)
            {
                var element = alwaysIncludedShadersProperty.GetArrayElementAtIndex(i);
                element.objectReferenceValue = shader;
                i++;
            }
        }

        private static void IncludeShader(ShaderIncluderSettings.IncludeRangeEnum includeRange)
        {
            HashSet<SiriusPostProcessingFeature> siriusFeatures;
            switch (includeRange)
            {
                case ShaderIncluderSettings.IncludeRangeEnum.AllQuality:
                {
                    siriusFeatures = FindSiriusPostProcessingFeaturesFromAllQualities();
                    Debug.Log("Include shaders from all quality settings.");
                    break;
                }
                case ShaderIncluderSettings.IncludeRangeEnum.OnlyCurrentQuality:
                {
                    siriusFeatures = FindSiriusPostProcessingFeaturesFromCurrentQuality();
                    Debug.Log("Include shaders from current quality settings.");
                    break;
                }
                case ShaderIncluderSettings.IncludeRangeEnum.OnlyBuildSceneCameras:
                {
                    siriusFeatures = FindSiriusPostProcessingFeaturesFromSceneCameras();
                    Debug.Log("Include shaders from build scene cameras.");
                    break;
                }
                default:
                {
                    // デフォルトは何もしない
                    return;
                }
            }

            var graphicsSettings = new SerializedObject(GraphicsSettings.GetGraphicsSettings());
            var alwaysIncludedShadersProperty = graphicsSettings.FindProperty("m_AlwaysIncludedShaders");

            // Sirius以外のShader
            var otherAlwaysIncludedShaders = GetOtherShaders(ref alwaysIncludedShadersProperty);
            // SiriusのShader
            var siriusAlwaysIncludedShaders = siriusFeatures.SelectMany(
                f => f.GetUsingShaderNameList()).Select(Shader.Find).ToHashSet();

            foreach (var shader in siriusAlwaysIncludedShaders)
            {
                Debug.Log($"> Including: {shader.name}");
            }

            SerializeAlwaysIncludedShaderList(ref alwaysIncludedShadersProperty, in otherAlwaysIncludedShaders, in siriusAlwaysIncludedShaders);

            graphicsSettings.ApplyModifiedProperties();
        }

        private static HashSet<SiriusPostProcessingFeature> FindSiriusPostProcessingFeaturesFromCurrentQuality()
        {
            var uniqueSiriusFeatures = new HashSet<SiriusPostProcessingFeature>();
            foreach (var rendererData in UniversalRenderPipeline.asset.rendererDataList)
            {
                uniqueSiriusFeatures.UnionWith(rendererData.rendererFeatures.OfType<SiriusPostProcessingFeature>());
            }
            return uniqueSiriusFeatures;
        }

        private static HashSet<SiriusPostProcessingFeature> FindSiriusPostProcessingFeaturesFromAllQualities()
        {
            var uniqueSiriusFeatures = new HashSet<SiriusPostProcessingFeature>();
            var buildTarget = EditorUserBuildSettings.activeBuildTarget;
            var buildTargetGroup = BuildPipeline.GetBuildTargetGroup(buildTarget);
            var buildTargetGroupName = buildTargetGroup.ToString();
            var renderPipelineAssets = new List<RenderPipelineAsset>();
            QualitySettings.GetAllRenderPipelineAssetsForPlatform(buildTargetGroupName, ref renderPipelineAssets);
            var urpAssets = renderPipelineAssets.Cast<UniversalRenderPipelineAsset>().ToArray();
            foreach (var urpAsset in urpAssets)
            {
                foreach (var rendererData in urpAsset.rendererDataList)
                {
                    uniqueSiriusFeatures.UnionWith(rendererData.rendererFeatures.OfType<SiriusPostProcessingFeature>());
                }
            }
            return uniqueSiriusFeatures;
        }

        /// <summary>
        /// ビルド対象の全シーンに含まれる SiriusPostProcessingFeature (重複無し) を探して返す
        /// </summary>
        private static HashSet<SiriusPostProcessingFeature> FindSiriusPostProcessingFeaturesFromSceneCameras()
        {
            var uniqueSiriusFeatures= new HashSet<SiriusPostProcessingFeature>();

            // ビルド対象シーン取得
            var scenePaths = EditorBuildSettings.scenes
                                                .Where(s => s.enabled)
                                                .Select(s => s.path)
                                                .ToArray();

            // 現在の状態をキャッシュ
            var oldSetup = EditorSceneManager.GetSceneManagerSetup();

            foreach (var scenePath in scenePaths)
            {
                var openedScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                Debug.Log($"Opened scene: {openedScene.path}");

                // カメラ全取得（有効/無効問わず）
                var cameras = Resources.FindObjectsOfTypeAll<Camera>()
                                       .Where(cam => cam.gameObject.scene == openedScene)
                                       .ToArray();

                foreach (var cam in cameras)
                {
                    var cameraData = cam.GetUniversalAdditionalCameraData();
                    if (cameraData.scriptableRenderer is UniversalRenderer renderer)
                    {
                        var prop = renderer.GetType().GetProperty("rendererFeatures",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (prop == null || prop.GetValue(renderer) is not IEnumerable<ScriptableRendererFeature> features)
                            continue;

                        uniqueSiriusFeatures.UnionWith(features.OfType<SiriusPostProcessingFeature>());
                    }
                }
            }

            // 最後に状態を戻す
            EditorSceneManager.RestoreSceneManagerSetup(oldSetup);

            return uniqueSiriusFeatures;
        }

        [MenuItem("Tools/Sirius/PostProcessing/ShaderIncluder/IncludeShaderFromAllQualitySettings", priority = 0)]
        public static void IncludeShaderFromAllQualitySettings()
        {
            IncludeShader(ShaderIncluderSettings.IncludeRangeEnum.AllQuality);
        }

        [MenuItem("Tools/Sirius/PostProcessing/ShaderIncluder/IncludeShaderFromCurrenQualitySettings", priority = 1)]
        public static void IncludeShaderFromCurrentQualitySettings()
        {
            IncludeShader(ShaderIncluderSettings.IncludeRangeEnum.OnlyCurrentQuality);
        }

        [MenuItem("Tools/Sirius/PostProcessing/ShaderIncluder/IncludeShaderFromSceneCameras", priority = 2)]
        public static void IncludeShaderFromSceneCameras()
        {
            IncludeShader(ShaderIncluderSettings.IncludeRangeEnum.OnlyBuildSceneCameras);
        }

        [MenuItem("Tools/Sirius/PostProcessing/ShaderIncluder/ClearShader", priority = 3)]
        public static void ClearSiriusShaders()
        {
            var graphicsSettings = new SerializedObject(GraphicsSettings.GetGraphicsSettings());
            var alwaysIncludedShadersProperty = graphicsSettings.FindProperty("m_AlwaysIncludedShaders");

            // Sirius以外のShader
            var otherAlwaysIncludedShaders = GetOtherShaders(ref alwaysIncludedShadersProperty);

            // SiriusのShader
            var siriusAlwaysIncludedShaders = new HashSet<Shader>();

            SerializeAlwaysIncludedShaderList(ref alwaysIncludedShadersProperty, in otherAlwaysIncludedShaders, in siriusAlwaysIncludedShaders);

            graphicsSettings.ApplyModifiedProperties();
        }
    }
}
