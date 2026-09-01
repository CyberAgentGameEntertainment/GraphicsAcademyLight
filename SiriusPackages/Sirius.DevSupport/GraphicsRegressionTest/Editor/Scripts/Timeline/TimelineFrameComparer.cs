// --------------------------------------------------------------
// Copyright 2026 CyberAgent, Inc.
// --------------------------------------------------------------

#if HAS_TIMELINE
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Sirius.DevSupport
{
    /// <summary>
    ///     Timeline 駆動のテストで 1 フレーム単位に画像比較するためのヘルパー。
    ///     画像名にフレーム連番を付け、Assert を投げず結果を返す（fail-fast や集計は呼び出し側で行う）。
    /// </summary>
    internal static class TimelineFrameComparer
    {
        // flipの並列実行数
        private const int FlipParallelism = 4;

        private static readonly string TempDir = Path.Combine(
            // ReSharper disable once PossibleNullReferenceException
            Directory.GetParent(Application.dataPath).FullName,
            "Library", "Sirius.DevSupport.GraphicsRegressionTest");

        public enum FrameStatus
        {
            Pass,
            Fail,
            MissingReference,
        }

        public class Result
        {
            public int Frame;
            public FrameStatus Status;
            public float Mean;
            public float Max;
            public string ExpectedRelativePath;
            public string ApprovedPath;
            internal byte[] ActualPngBytes;
            internal string HeatmapFilePath;
            internal string ImageName;
        }

        /// <summary>
        ///     現在のレンダリング結果を frameIndex 番目のリファレンス画像と比較する。
        /// </summary>
        /// <param name="cameras">撮影対象カメラ（カメラスタック含む）</param>
        /// <param name="settings">比較設定</param>
        /// <param name="context">テスト用のTimelineコンテキスト</param>
        /// <param name="isAutoApprove">リファレンス画像がないときに追加するか</param>
        internal static async Awaitable<List<Result>> CompareAllFramesAsync(List<Camera> cameras, ImageComparisonSettings settings, TimelinePrefabTestContext context, bool isAutoApprove)
        {
            // 実際のシーンの画像を入れる一時ディレクトリの作成
            Directory.CreateDirectory(TempDir);

            var dirName = FlipAssert.GetOrCreateImageDirectory(true);
            // ReSharper disable once PossibleNullReferenceException
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;

            using var semaphore = new SemaphoreSlim(FlipParallelism);
            var pendingTasks = new List<Task<Result>>(context.FrameCount);
            var missingReferenceResults = new List<Result>();

            // 各フレームのTimelineを評価
            for (var frameIndex = 0; frameIndex < context.FrameCount; frameIndex++)
            {
                context.EvaluateFrame(frameIndex);

                var actualImage = TestUtility.CaptureActualImage(cameras, settings);
                var bytes = actualImage.EncodeToPNG();
                Object.DestroyImmediate(actualImage);

                var imageName = GetTestImageName(frameIndex);
                var expectedRelativePath = Path.Combine(dirName, $"{imageName}.png");
                var expectedFullPath = Path.Combine(projectRoot, expectedRelativePath);

                // リファレンス画像が無い場合
                if (!File.Exists(expectedFullPath))
                {
                    var result = new Result
                    {
                        Frame = frameIndex,
                        Status = FrameStatus.MissingReference,
                        ExpectedRelativePath = expectedRelativePath,
                    };

                    if (isAutoApprove)
                    {
                        var savedPath = Path.Combine(dirName, $"{imageName}.png");
                        await File.WriteAllBytesAsync(savedPath, bytes);
                        result.ApprovedPath = savedPath;
                    }
                    missingReferenceResults.Add(result);
                    continue;
                }

                // 並列実行のため actual.png はフレーム別パスにする
                var actualPath = Path.Combine(TempDir, $"actual_{frameIndex:D4}.png");
                await File.WriteAllBytesAsync(actualPath, bytes);

                await semaphore.WaitAsync().ConfigureAwait(true);
                var capturedFrameIndex = frameIndex;
                var heatmapFilePath = Path.GetFullPath($"flip.{Path.GetFileNameWithoutExtension(expectedRelativePath)}.{Path.GetFileNameWithoutExtension(actualPath)}.67ppd.ldr.png");

                pendingTasks.Add(Task.Run(async () =>
                {
                    var keepHeatmap = false;
                    try
                    {
                        var flipResult = await NvidiaFlip.ExecuteAsync(expectedFullPath, actualPath);
                        var exceeds = (settings.AverageCorrectnessThreshold > 0 && flipResult.Mean >= settings.AverageCorrectnessThreshold)
                                      || (settings.MaxCorrectnessThreshold > 0 && flipResult.Max >= settings.MaxCorrectnessThreshold);

                        keepHeatmap = exceeds;
                        return new Result
                        {
                            Frame = capturedFrameIndex,
                            Status = exceeds ? FrameStatus.Fail : FrameStatus.Pass,
                            Mean = flipResult.Mean,
                            Max = flipResult.Max,
                            ExpectedRelativePath = expectedRelativePath,
                            ActualPngBytes = exceeds ? bytes : null,
                            HeatmapFilePath = exceeds ? heatmapFilePath : null,
                            ImageName = exceeds ? imageName : null,
                        };
                    }
                    catch (Exception)
                    {
                        // flipの実行に失敗した際には、失敗扱いとし他フレームの集計を継続する
                        return new Result
                        {
                            Frame = capturedFrameIndex,
                            Status = FrameStatus.Fail,
                            ExpectedRelativePath = expectedRelativePath,
                            ActualPngBytes = bytes,
                            ImageName = imageName,
                        };
                    }
                    finally
                    {
                        if (File.Exists(actualPath))
                            File.Delete(actualPath);
                        if (!keepHeatmap && File.Exists(heatmapFilePath))
                            File.Delete(heatmapFilePath);
                        semaphore.Release();
                    }
                }));
            }

            var taskResults = await Task.WhenAll(pendingTasks).ConfigureAwait(true);

            // Texture2D 操作はメインスレッド限定のため、Fail フレームのアーティファクト保存はここでまとめて行う
            SaveFailedFrameArtifacts(taskResults);

            var results = new List<Result>(missingReferenceResults.Count + taskResults.Length);
            results.AddRange(missingReferenceResults);
            results.AddRange(taskResults);
            results.Sort((a, b) => a.Frame.CompareTo(b.Frame));
            return results;
        }

        /// <summary>
        ///     失敗フレームの実画像・期待画像・差分画像を ActualImages に保存する。
        /// </summary>
        private static void SaveFailedFrameArtifacts(IReadOnlyList<Result> results)
        {
            var pathName = FlipAssert.GetOrCreateImageDirectory(false);

            foreach (var result in results)
            {
                if (result.Status != FrameStatus.Fail) continue;

                File.WriteAllBytes(Path.Combine(pathName, $"{result.ImageName}.png"), result.ActualPngBytes);
                var expectedImage = TestUtility.LoadImage(result.ExpectedRelativePath);
                File.WriteAllBytes(Path.Combine(pathName, $"{result.ImageName}.expected.png"), expectedImage.EncodeToPNG());
                Object.DestroyImmediate(expectedImage);

                if (!string.IsNullOrEmpty(result.HeatmapFilePath) && File.Exists(result.HeatmapFilePath))
                {
                    var diffImage = TestUtility.LoadImage(result.HeatmapFilePath);
                    File.WriteAllBytes(Path.Combine(pathName, $"{result.ImageName}.diff.png"), diffImage.EncodeToPNG());
                    Object.DestroyImmediate(diffImage);
                    // ActualImages に diff.png として保存した後はプロジェクトルートに残しておく必要がないため削除
                    File.Delete(result.HeatmapFilePath);
                }
            }
        }

        /// <summary>
        ///     テスト画像の名前を取得
        /// </summary>
        /// <param name="frameIndex">対象の画像のフレーム数</param>
        /// <returns>テスト画像の名前</returns>
        private static string GetTestImageName(int frameIndex)
        {
            var testName = TestContext.CurrentContext.Test.MethodName != null
                ? TestContext.CurrentContext.Test.Name
                : "NoName";
            var sanitized = TestUtility.StripParametricTestCharacters(testName);
            return $"{sanitized}_f{frameIndex:D4}";
        }
    }
}
#endif
