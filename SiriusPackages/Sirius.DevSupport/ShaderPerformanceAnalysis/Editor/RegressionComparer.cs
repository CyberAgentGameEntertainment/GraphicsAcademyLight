#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Sirius.DevSupport.ShaderPerformanceAnalysis
{
    /// <summary>
    /// 1 メトリクスの現在値とベースライン値の比較結果。
    /// </summary>
    internal sealed class MetricComparison
    {
        public string MetricKey { get; set; } = string.Empty;
        public double BaselineValue { get; set; }
        public double CurrentValue { get; set; }
        public MetricDirection Direction { get; set; }

        /// <summary>現在値 - ベースライン値。</summary>
        public double Delta => CurrentValue - BaselineValue;

        /// <summary>悪化方向への相対変化率。ベースラインが 0 のときは絶対差で判断する。</summary>
        public double WorseningRatio { get; set; }

        /// <summary>許容デルタを超えて悪化方向に変化したか。</summary>
        public bool IsRegression { get; set; }

        /// <summary>許容デルタを超えて改善方向に変化したか（良化）。</summary>
        public bool IsImprovement { get; set; }
    }

    /// <summary>
    /// 1 シェーダーパス・1 タイプの比較結果。
    /// </summary>
    internal sealed class PassComparison
    {
        public ShaderPassAnalysis Current { get; set; } = new();

        /// <summary>対応するベースラインエントリが存在したか。false = 初回計測（回帰なし扱い）。</summary>
        public bool HasBaseline { get; set; }

        public List<MetricComparison> Metrics { get; set; } = new();

        public bool HasRegression => Metrics.Any(metric => metric.IsRegression);

        public bool HasImprovement => Metrics.Any(metric => metric.IsImprovement);
    }

    /// <summary>
    /// 回帰比較レポート全体。
    /// </summary>
    internal sealed class RegressionReport
    {
        public List<PassComparison> Comparisons { get; set; } = new();

        /// <summary>有効なベースラインが存在したか。false = 初回（現値のみ記録、回帰なし）。</summary>
        public bool HasBaseline { get; set; }

        /// <summary>環境不一致など、比較に関する注意メッセージ。</summary>
        public List<string> Warnings { get; set; } = new();

        public bool HasAnyRegression => Comparisons.Any(comparison => comparison.HasRegression);

        public IEnumerable<PassComparison> RegressedComparisons =>
            Comparisons.Where(comparison => comparison.HasRegression);

        public IEnumerable<PassComparison> ImprovedComparisons =>
            Comparisons.Where(comparison => comparison.HasImprovement);
    }

    /// <summary>
    /// 現在の解析結果をベースラインと比較し、悪化（回帰）・良化（改善）したメトリクスを検出する。
    /// </summary>
    internal static class RegressionComparer
    {
        /// <summary>
        /// 解析結果とベースラインを比較する。
        /// </summary>
        /// <param name="result">今回の解析結果。</param>
        /// <param name="baseline">基準ベースライン。null なら初回扱い（回帰なし）。</param>
        /// <param name="toleranceRatio">ノイズ吸収のための許容相対デルタ（0.02 = 2%）。負値は 0 に丸める。</param>
        public static RegressionReport Compare(
            ShaderAnalysisResult result,
            ShaderPerfBaseline? baseline,
            double toleranceRatio)
        {
            var tolerance = Math.Max(0d, toleranceRatio);
            var report = new RegressionReport();

            // ベースライン無し（初回）→ 回帰なし、現値のみ記録。
            var baselineUsable = baseline != null;
            if (baseline != null && baseline.MatchesEnvironment(result) == false)
            {
                // 環境差（malioc バージョン等）があっても比較は実施する。
                // malioc バージョンは実行環境（開発者マシン / CI ホスト）により異なり得るため、不一致を理由に
                // 比較をスキップすると「環境が変わると無言で回帰検出が止まる（success のまま）」盲点になる。
                // よって比較は続行し、環境差による誤検知の可能性を必ず警告として残す（判断は人へ委ねる）。
                report.Warnings.Add(
                    $"ベースラインの環境が今回の解析環境と一致しません。比較は実施しますが、環境差により誤検知の可能性があります。" +
                    $"不一致: {baseline.DescribeEnvironmentMismatch(result)}。" +
                    $"差が malioc バージョンのみであれば概ね比較可能ですが、必要なら現在の環境でベースラインを更新してください。");
            }

            report.HasBaseline = baselineUsable;

            foreach (var pass in result.Passes.Where(pass => pass.IsSuccess))
            {
                var comparison = new PassComparison { Current = pass };
                var entryKey = ShaderPerfBaselineEntry.BuildKey(
                    pass.ShaderAssetPath, pass.SubShaderIndex, pass.PassIndex, pass.ShaderTypeName);

                var baselineEntry = baselineUsable ? baseline!.FindEntry(entryKey) : null;
                comparison.HasBaseline = baselineEntry != null;

                if (baselineEntry != null)
                {
                    CompareMetrics(pass.Metrics!, baselineEntry.Metrics, tolerance, comparison.Metrics);
                }

                report.Comparisons.Add(comparison);
            }

            return report;
        }

        private static void CompareMetrics(
            ShaderPerfMetrics current,
            ShaderPerfMetrics baseline,
            double tolerance,
            List<MetricComparison> sink)
        {
            var baselineMetrics = baseline.EnumerateComparableMetrics()
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

            foreach (var currentMetric in current.EnumerateComparableMetrics())
            {
                if (baselineMetrics.TryGetValue(currentMetric.Key, out var baselineValue) == false)
                {
                    continue;
                }

                var comparison = BuildMetricComparison(
                    currentMetric.Key, currentMetric.Value, baselineValue.Value, tolerance);
                sink.Add(comparison);
            }
        }

        private static MetricComparison BuildMetricComparison(
            string metricKey,
            MetricValue current,
            double baselineValue,
            double tolerance)
        {
            // 悪化方向の符号付き差分。HigherIsWorse は (current - baseline)、LowerIsWorse は (baseline - current)。
            var worseningDelta = current.Direction == MetricDirection.HigherIsWorse
                ? current.Value - baselineValue
                : baselineValue - current.Value;

            double worseningRatio;
            bool isRegression;
            bool isImprovement;
            if (Math.Abs(baselineValue) > double.Epsilon)
            {
                worseningRatio = worseningDelta / Math.Abs(baselineValue);
                isRegression = worseningRatio > tolerance;
                isImprovement = worseningRatio < -tolerance;
            }
            else
            {
                // ベースラインが 0 の場合は相対比較できないため、悪化方向に少しでも増えたら回帰、
                // 改善方向に少しでも動いたら良化とする。
                worseningRatio = worseningDelta > 0d ? double.PositiveInfinity : 0d;
                isRegression = worseningDelta > double.Epsilon;
                isImprovement = worseningDelta < -double.Epsilon;
            }

            return new MetricComparison
            {
                MetricKey = metricKey,
                BaselineValue = baselineValue,
                CurrentValue = current.Value,
                Direction = current.Direction,
                WorseningRatio = worseningRatio,
                IsRegression = isRegression,
                IsImprovement = isImprovement,
            };
        }
    }
}
