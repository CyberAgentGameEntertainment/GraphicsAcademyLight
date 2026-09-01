// --------------------------------------------------------------
// Copyright 2026 CyberAgent, Inc.
// --------------------------------------------------------------

#if HAS_TIMELINE
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using static Sirius.DevSupport.TimelineFrameComparer;

namespace Sirius.DevSupport
{
    /// <summary>
    ///     Timeline 駆動下のレンダリング結果を全フレーム比較する Assert API。
    ///     既存の <see cref="FlipAssert" />の Timeline版
    /// </summary>
    public static class TimelineFlipAssert
    {
        /// <summary>
        ///     全フレームをループして FLIP 比較する
        /// </summary>
        public static async Awaitable AreEqualCamerasToRefTexAsync(TimelinePrefabTestContext context, List<Camera> cameras, ImageComparisonSettings settings, bool? isReplaceAutoApprove = null)
        {
            if (settings.AverageCorrectnessThreshold <= 0 && settings.MaxCorrectnessThreshold <= 0)
            {
                Assert.Fail("Mean 閾値または Max 閾値のいずれかを設定してください。");
            }

            var autoApprove = isReplaceAutoApprove ?? GraphicsRegressionTestSettings.instance.AutoApproveMissingReferences;

            var results = await CompareAllFramesAsync(cameras, settings, context, autoApprove);

            // 集計
            var failed = results
                .Where(r => r.Status == FrameStatus.Fail)
                .ToArray();
            var missing = results
                .Where(r => r.Status == FrameStatus.MissingReference)
                .ToArray();

            // 自動採用された新規リファレンス画像を、Assertより前にまとめて反映する
            if (missing.Any() && autoApprove)
                AssetDatabase.Refresh();

            if (failed.Any())
            {
                var first = failed[0];
                var message =
                    $"FLIP 比較が {failed.Length} / {context.FrameCount} フレームで閾値超過しました。\n" +
                    $"最初の失敗フレーム: f={first.Frame} Mean={first.Mean:F6} Max={first.Max:F6}\n" +
                    $"閾値: Mean={settings.AverageCorrectnessThreshold} Max={settings.MaxCorrectnessThreshold}\n" +
                    $"全失敗フレーム: {string.Join(", ", failed.Select(r => r.Frame))}";
                if (missing.Any())
                {
                    message += $"\n加えて {missing.Length} フレームのリファレンス画像が欠落しています" +
                               (autoApprove ? "（撮影画像を自動採用済み）。" : "。");
                }
                Assert.Fail(message);
            }

            if (missing.Any())
            {
                var first = missing[0];
                Assert.Inconclusive(
                    $"{missing.Length} / {context.FrameCount} フレームのリファレンス画像が見つかりませんでした。\n" +
                    (autoApprove
                        ? $"撮影画像を自動採用しました。最初の採用パス: {first.ApprovedPath}"
                        : $"最初の欠落フレーム: f={first.Frame} 期待されるパス: {first.ExpectedRelativePath}"));
            }
        }
    }
}
#endif
