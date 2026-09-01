// --------------------------------------------------------------
// Copyright 2025 CyberAgent, Inc.
// --------------------------------------------------------------

using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
#if HAS_URP
using UnityEngine.Rendering.Universal;
#endif

namespace Sirius.DevSupport
{
    /// <summary>
    ///     ImageAssertの拡張メソッド
    /// </summary>
    public static class FlipAssert
    {
        /// <summary>
        ///     Flip画像比較を行うメソッド
        /// </summary>
        public static void AreEqualSceneCameraToRefTex()
        {
            var settings = new ImageComparisonSettings
            {
                TargetWidth = Screen.width,
                TargetHeight = Screen.height,
                // 最も期待通りの結果になった数値を設定
                AverageCorrectnessThreshold = 0.025f,
            };

            var cameras = new List<Camera> { Camera.main };

#if HAS_URP
            // カメラスタックを調査
            var cameraData = Camera.main.GetUniversalAdditionalCameraData();
            // アンチがテンポーラルになっているとテストの安定性が下がるためFXAAに変更する。
            cameraData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            cameras.AddRange(cameraData.cameraStack);
#endif

            AreEqualCamerasToRefTex(cameras, settings);
        }

        /// <summary>
        ///     Flip画像比較を行うメソッド
        /// </summary>
        /// <param name="camera">カメラ</param>
        /// <param name="settings">比較設定</param>
        /// <param name="isReplaceAutoApprove">リファレンス画像がないときに追加するか</param>
        public static void AreEqualCameraToRefTex(Camera camera, ImageComparisonSettings settings, bool? isReplaceAutoApprove = null)
        {
            AreEqualCamerasToRefTex(new List<Camera> { camera }, settings, isReplaceAutoApprove);
        }

        /// <summary>
        ///     Flip画像比較を行うメソッド
        /// </summary>
        /// <param name="cameras">複数カメラ</param>
        /// <param name="settings">比較設定</param>
        /// <param name="isReplaceAutoApprove">リファレンス画像がないときに追加するか</param>
        public static void AreEqualCamerasToRefTex(List<Camera> cameras, ImageComparisonSettings settings, bool? isReplaceAutoApprove = null)
        {
            // テストイメージをキャプチャして一時ファイルとして保存する
            var actualImage = TestUtility.CaptureActualImage(cameras, settings);
            var bytes = actualImage.EncodeToPNG();
            CompareActualPngWithReference(bytes, settings, isReplaceAutoApprove);
        }

        /// <summary>
        ///     2つのTextureをFlip画像比較
        /// </summary>
        /// <param name="expected">期待される画像</param>
        /// <param name="actual">実際の画像</param>
        /// <param name="settings">比較設定</param>
        public static void AreEqualTextures(Texture2D expected, Texture2D actual, ImageComparisonSettings settings)
        {
            const string expectedPath = "Assets/expected_temp.png";
            const string actualPath = "Assets/actual_temp.png";
            var heatmapFilePath = $"flip.{Path.GetFileNameWithoutExtension(expectedPath)}.{Path.GetFileNameWithoutExtension(actualPath)}.67ppd.ldr.png";

            var expectedBytes = expected.EncodeToPNG();
            var actualBytes = actual.EncodeToPNG();

            try
            {
                File.WriteAllBytes(expectedPath, expectedBytes);
                File.WriteAllBytes(actualPath, actualBytes);

                var result = NvidiaFlip.Execute(expectedPath, actualPath);
                AssertFlipResult(result, settings.AverageCorrectnessThreshold, settings.MaxCorrectnessThreshold);
            }
            finally
            {
                SaveFlipComparisonArtifacts(actualBytes, expectedBytes, heatmapFilePath);
                if (File.Exists(expectedPath)) File.Delete(expectedPath);
                if (File.Exists(actualPath)) File.Delete(actualPath);
                if (File.Exists(heatmapFilePath)) File.Delete(heatmapFilePath);
                AssetDatabase.Refresh();
            }
        }

        /// <summary>
        ///     テクスチャとリファレンス画像でFlip比較を行う
        /// </summary>
        /// <param name="actualImage">比較するテクスチャ</param>
        /// <param name="settings">比較設定</param>
        /// <param name="isReplaceAutoApprove">リファレンス画像がないときに追加するか</param>
        public static void AreEqualTexToRefTex(Texture2D actualImage, ImageComparisonSettings settings, bool? isReplaceAutoApprove = null)
        {
            var bytes = actualImage.EncodeToPNG();
            CompareActualPngWithReference(bytes, settings, isReplaceAutoApprove);
        }

        /// <summary>
        ///     PNG バイト列とリファレンス画像で Flip 比較を行う共通処理
        /// </summary>
        private static void CompareActualPngWithReference(byte[] actualPng, ImageComparisonSettings settings, bool? isReplaceAutoApprove = null)
        {
            var expectedPath = TestUtility.GetExpectedImageRelativePath();

            // リファレンス画像の存在チェック
            // ReSharper disable once PossibleNullReferenceException
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var expectedFullPath = Path.Combine(projectRoot, expectedPath);

            if (!File.Exists(expectedFullPath))
            {
                HandleMissingReferenceImage(actualPng, expectedPath, isReplaceAutoApprove);
                return;
            }

            const string actualPath = "Assets/actual.png";
            File.WriteAllBytes(actualPath, actualPng);
            var heatmapFilePath = $"flip.{Path.GetFileNameWithoutExtension(expectedPath)}.actual.67ppd.ldr.png";
            try
            {
                var result = NvidiaFlip.Execute(expectedPath, actualPath);
                AssertFlipResult(result, settings.AverageCorrectnessThreshold, settings.MaxCorrectnessThreshold);
            }
            finally
            {
                SaveFlipComparisonArtifacts(actualPng, expectedPath, heatmapFilePath);
                // 一時ファイルを削除
                if (File.Exists(actualPath))
                    File.Delete(actualPath);
                // ヒートマップを削除
                if (File.Exists(heatmapFilePath))
                    File.Delete(heatmapFilePath);
                AssetDatabase.Refresh();
            }
        }

        /// <summary>
        ///     Flip 比較の実画像・期待画像・差分を ActualImages に保存する（成功・失敗どちらでも出力）。
        /// </summary>
        private static void SaveFlipComparisonArtifacts(byte[] actualPng, string expectedRelativePath, string heatmapFilePath)
        {
            // テスト結果にかかわらず画像を保存
            var pathName = GetOrCreateImageDirectory(false);
            var imageName = GetTestImageName();

            // テスト画像を保存
            File.WriteAllBytes(Path.Combine(pathName, $"{imageName}.png"), actualPng);

            // リファレンス画像を保存
            var expectedImage = TestUtility.LoadImage(expectedRelativePath);
            File.WriteAllBytes(Path.Combine(pathName, $"{imageName}.expected.png"), expectedImage.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(expectedImage);

            // ヒートマップ（差分画像）を保存
            var heatmapFullPath = Path.GetFullPath(heatmapFilePath);
            if (File.Exists(heatmapFullPath))
            {
                var diffImage = TestUtility.LoadImage(heatmapFullPath);
                File.WriteAllBytes(Path.Combine(pathName, $"{imageName}.diff.png"), diffImage.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(diffImage);
            }
        }

        /// <summary>
        ///     Flip 比較の実画像・期待画像・差分を ActualImages に保存する（メモリ上の期待画像版）。
        /// </summary>
        private static void SaveFlipComparisonArtifacts(byte[] actualPng, byte[] expectedPng, string heatmapFilePath)
        {
            // テスト結果にかかわらず画像を保存
            var pathName = GetOrCreateImageDirectory(false);
            var imageName = GetTestImageName();

            // テスト画像を保存
            File.WriteAllBytes(Path.Combine(pathName, $"{imageName}.png"), actualPng);
            // リファレンス画像を保存
            File.WriteAllBytes(Path.Combine(pathName, $"{imageName}.expected.png"), expectedPng);

            // ヒートマップ（差分画像）を保存
            var heatmapFullPath = Path.GetFullPath(heatmapFilePath);
            if (File.Exists(heatmapFullPath))
            {
                var diffImage = TestUtility.LoadImage(heatmapFullPath);
                File.WriteAllBytes(Path.Combine(pathName, $"{imageName}.diff.png"), diffImage.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(diffImage);
            }
        }

        /// <summary>
        ///     FLIP 結果のバリデーション・ログ出力・アサーションを行う共通処理
        /// </summary>
        private static void AssertFlipResult(NvidiaFlip.Result result, float meanThreshold, float maxThreshold)
        {
            if (meanThreshold <= 0 && maxThreshold <= 0)
            {
                Assert.Fail("Mean 閾値または Max 閾値のいずれかを設定してください。");
            }

            var parts = new List<string>();
            if (meanThreshold > 0)
                parts.Add($"Mean={result.Mean:F6} (threshold={meanThreshold})");
            if (maxThreshold > 0)
                parts.Add($"Max={result.Max:F6} (threshold={maxThreshold})");
            Debug.Log($"[FLIP] {string.Join(", ", parts)}");

            if (meanThreshold > 0)
            {
                Assert.Less(result.Mean, meanThreshold,
                    $"FLIP Mean {result.Mean:F6} exceeded threshold {meanThreshold}");
            }
            if (maxThreshold > 0)
            {
                Assert.Less(result.Max, maxThreshold,
                    $"FLIP Max {result.Max:F6} exceeded threshold {maxThreshold}");
            }
        }

        /// <summary>
        ///     テスト画像の名前を取得
        /// </summary>
        private static string GetTestImageName()
        {
            var testName = TestContext.CurrentContext.Test.MethodName != null
                ? TestContext.CurrentContext.Test.Name
                : "NoName";

            return TestUtility.StripParametricTestCharacters(testName);
        }

        /// <summary>
        ///     プラットフォーム別の画像保存先ディレクトリを取得（自動作成）
        /// </summary>
        /// <param name="isReferenceImage">true: SuccessfulImages, false: ActualImages</param>
        internal static string GetOrCreateImageDirectory(bool isReferenceImage)
        {
            var currentTestResultsFolderPath = TestUtility.GetCurrentTestResultsFolderPath();
            var basePath = isReferenceImage
                ? GraphicsRegressionTestSettings.instance.SuccessfulImagesPath
                : GraphicsRegressionTestSettings.instance.ActualImagesPath;

            var directory = Path.Combine(basePath, currentTestResultsFolderPath);
            Directory.CreateDirectory(directory);
            return directory;
        }

        /// <summary>
        ///     リファレンス画像が存在しない場合の処理
        /// </summary>
        private static void HandleMissingReferenceImage(byte[] imageBytes, string expectedPath, bool? isReplaceAutoApprove = null)
        {
            var autoApprove = isReplaceAutoApprove ?? GraphicsRegressionTestSettings.instance.AutoApproveMissingReferences;

            if (autoApprove)
            {
                // SuccessfulImagesにコピー
                var destinationDir = GetOrCreateImageDirectory(true);
                var sanitizedImageName = GetTestImageName();
                var destFilePath = Path.Combine(destinationDir, $"{sanitizedImageName}.png");

                File.WriteAllBytes(destFilePath, imageBytes);
                UnityEditor.AssetDatabase.Refresh();

                Assert.Inconclusive(
                    $"[画像不足] リファレンス画像が見つかりませんでした。\n" +
                    $"撮影した画像をリファレンスとして保存しました。\n" +
                    $"パス: {destFilePath}");
            }
            else
            {
                Assert.Inconclusive(
                    $"[画像不足] リファレンス画像が見つかりませんでした。\n" +
                    $"期待されるパス: {expectedPath}\n\n" +
                    $"初回実行の場合は、以下の手順でリファレンス画像を作成してください：\n" +
                    $"1. テストを実行（このエラーが出る）\n" +
                    $"2. ActualImages/ の画像を確認\n" +
                    $"3. Unity メニューから「Tools > Test > Copy AverageTest Result」を実行\n\n" +
                    $"または ProjectSettings で「Auto Approve Missing References」を有効にしてください。");
            }
        }
    }
}
