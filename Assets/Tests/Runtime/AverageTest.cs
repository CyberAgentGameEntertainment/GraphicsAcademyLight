// --------------------------------------------------------------
// Copyright 2026 CyberAgent, Inc.
// --------------------------------------------------------------

using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Sirius.DevSupport;
using TestHelper.Attributes;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// 拡張メソッドを含む名前空間を追加

namespace Tests.Runtime
{
    /// <summary>
    ///     アベレージテストを行うクラス
    ///     Jzazbz色空間でのピクセルの差分の平均値を使ってテストを行います。
    /// </summary>
    public class AverageTest
    {
        private const string ValidateMobileTargetKey = "Sirius_AverageTest_ValidateMobileTarget";

        private static bool ValidateMobileTargetEnabled
        {
            get => SessionState.GetBool(ValidateMobileTargetKey, true);
            set => SessionState.SetBool(ValidateMobileTargetKey, value);
        }

        [MenuItem("Tools/Sirius/Dev Support/Validate Mobile Target")]
        private static void ToggleValidateMobileTarget()
        {
            ValidateMobileTargetEnabled = !ValidateMobileTargetEnabled;
        }

        [MenuItem("Tools/Sirius/Dev Support/Validate Mobile Target", true)]
        private static bool ToggleValidateMobileTargetValidate()
        {
            Menu.SetChecked("Tools/Sirius/Dev Support/Validate Mobile Target", ValidateMobileTargetEnabled);
            return true;
        }

        private const string StrictThresholdKey = "Sirius_AverageTest_EnableStrictThreshold";

        private static bool EnableStrictThreshold
        {
            get => SessionState.GetBool(StrictThresholdKey, false);
            set => SessionState.SetBool(StrictThresholdKey, value);
        }

        [MenuItem("Tools/Sirius/Dev Support/[Debug] Strict Threshold")]
        private static void ToggleStrictThreshold()
        {
            EnableStrictThreshold = !EnableStrictThreshold;
        }

        [MenuItem("Tools/Sirius/Dev Support/[Debug] Strict Threshold", true)]
        private static bool ToggleStrictThresholdValidate()
        {
            Menu.SetChecked("Tools/Sirius/Dev Support/[Debug] Strict Threshold", EnableStrictThreshold);
            return true;
        }

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            var graphicsApi = SystemInfo.graphicsDeviceType;
            if (graphicsApi != GraphicsDeviceType.Direct3D12 && graphicsApi != GraphicsDeviceType.Metal)
            {
                var message = $"Average Test: グラフィックスAPIが DirectX12 または Metal ではありません（現在: {graphicsApi}）\n" +
                              "DirectX12 または Metal に切り替えてからテストを実行してください。";
                Debug.LogError($"[SIRIUS] {message}");
                Assert.Ignore(message);
            }

            if (!ValidateMobileTargetEnabled) return;

            BuildTarget[] expectedTargets;
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                expectedTargets = new[] { BuildTarget.Android, BuildTarget.WebGL };
            }
            else if (Application.platform == RuntimePlatform.OSXEditor)
            {
                expectedTargets = new[] { BuildTarget.iOS, BuildTarget.WebGL };
            }
            else
            {
                return;
            }

            var actual = EditorUserBuildSettings.activeBuildTarget;
            if (System.Array.IndexOf(expectedTargets, actual) < 0)
            {
                var expectedText = string.Join(" / ", expectedTargets);
                var message = $"Average Test: ビルドターゲットが {expectedText} ではありません（現在: {actual}）\n" +
                              "別ターゲットでテストする場合は Tools/Sirius/Dev Support/Validate Mobile Target を無効にしてください。";

                // Consoleに目立つエラーログを出力
                Debug.LogError($"[SIRIUS] {message}");

                // テストをスキップ扱いにする
                Assert.Ignore(message);
            }
        }

        [TestCase("Workshop_DirectionalBlur/Workshop_DirectionalBlur", 0)]
        [TestCase("Workshop_RotationBlur/Workshop_RotationBlur", 0)]
        [TestCase("Workshop_RadialBlur/Workshop_RadialBlur", 0)]
        // ワーク④（陽炎）・ワーク⑤（光芒）はゼロからの新規実装ワークのため、ビジュアル回帰テストの対象外
        [GameViewResolution(GameViewResolution.FullHD)]
        [TimeScale(0.0f)]
        public async Task Test(string scenePath, int renderCount = 0)
        {
            var sceneFullPath = $"{GraphicsRegressionTestSettings.instance.SceneTestDirectory}/{scenePath}";
            await TestUtility.LoadSceneAndStabilizeRenderingAsync(sceneFullPath, renderCount);

            var settings = new ImageComparisonSettings
            {
                TargetWidth = Screen.width,
                TargetHeight = Screen.height,
                // 最も期待通りの結果になった数値を設定
                AverageCorrectnessThreshold = EnableStrictThreshold ? 0.01f : 0.025f,
            };

            //settings.ActiveImageTests |= ImageComparisonSettings.ImageTests.IncorrectPixelsCount;
            //settings.ActivePixelTests = ImageComparisonSettings.PixelTests.DeltaE;

            var cameras = new List<Camera>();

            cameras.Add(Camera.main);
            // カメラスタックを調査
            var cameraData = Camera.main.GetUniversalAdditionalCameraData();
            // アンチがテンポーラルになっているとテストの安定性が下がるためFXAAに変更する。
            cameraData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            foreach (var stackedCamera in cameraData.cameraStack) cameras.Add(stackedCamera);

            // Flipを使った画像比較
            FlipAssert.AreEqualCamerasToRefTex(cameras, settings);
        }
    }
}
