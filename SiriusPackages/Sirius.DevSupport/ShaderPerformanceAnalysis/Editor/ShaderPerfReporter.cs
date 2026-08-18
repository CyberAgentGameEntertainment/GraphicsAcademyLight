#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace Sirius.DevSupport.ShaderPerformanceAnalysis
{
    /// <summary>
    /// 解析結果と回帰比較を、人可読 Markdown と機械可読 JSON の両形式で出力する。
    /// </summary>
    internal static class ShaderPerfReporter
    {
        /// <summary>
        /// 人が読むための Markdown レポートを生成する。
        /// </summary>
        public static string BuildMarkdown(ShaderAnalysisResult result, RegressionReport report)
        {
            var builder = new StringBuilder(4096);
            builder.AppendLine("# シェーダーパフォーマンス解析レポート");
            builder.AppendLine();
            builder.AppendLine($"- GPU コア: `{Escape(result.GpuCore)}`");
            builder.AppendLine($"- malioc バージョン: `{Escape(FirstLine(result.MaliocVersion))}`");
            builder.AppendLine($"- Unity バージョン: `{Escape(result.UnityVersion)}`");
            builder.AppendLine();

            if (result.HasEnvironmentError)
            {
                builder.AppendLine("## 環境エラー");
                builder.AppendLine();
                foreach (var error in result.EnvironmentErrors)
                {
                    builder.AppendLine($"- ⚠️ {Escape(FirstLine(error))}");
                }

                builder.AppendLine();
                return builder.ToString();
            }

            var successCount = result.Passes.Count(pass => pass.IsSuccess);
            var failureCount = result.Passes.Count(pass => pass.IsSuccess == false);
            var regressionCount = report.RegressedComparisons.Count();

            builder.AppendLine("## サマリー");
            builder.AppendLine();
            builder.AppendLine($"- 解析成功パス: {successCount}");
            builder.AppendLine($"- 解析失敗パス: {failureCount}");
            builder.AppendLine($"- ベースライン比較: {(report.HasBaseline ? "あり" : "なし（初回・現値のみ記録）")}");
            builder.AppendLine($"- パフォーマンス悪化パス: {regressionCount}");
            builder.AppendLine();

            foreach (var warning in report.Warnings)
            {
                builder.AppendLine($"> ⚠️ {Escape(warning)}");
                builder.AppendLine();
            }

            AppendRegressionTable(builder, report);
            AppendDetailSection(builder, result);
            AppendFailureSection(builder, result);

            return builder.ToString();
        }

        /// <summary>
        /// 後段処理が扱う機械可読 JSON を生成する。
        /// </summary>
        public static string BuildJson(ShaderAnalysisResult result, RegressionReport report)
        {
            var payload = new ReportPayload
            {
                GpuCore = result.GpuCore,
                MaliocVersion = result.MaliocVersion,
                UnityVersion = result.UnityVersion,
                EnvironmentErrors = result.EnvironmentErrors,
                HasBaseline = report.HasBaseline,
                HasAnyRegression = report.HasAnyRegression,
                Warnings = report.Warnings,
                Passes = result.Passes,
                Regressions = report.RegressedComparisons.Select(comparison => new RegressionPayload
                {
                    ShaderAssetPath = comparison.Current.ShaderAssetPath,
                    SubShaderIndex = comparison.Current.SubShaderIndex,
                    PassIndex = comparison.Current.PassIndex,
                    PassName = comparison.Current.PassName,
                    ShaderTypeName = comparison.Current.ShaderTypeName,
                    Keywords = comparison.Current.Keywords,
                    Metrics = comparison.Metrics.Where(metric => metric.IsRegression).ToList(),
                }).ToList(),
            };

            return JsonConvert.SerializeObject(payload, Formatting.Indented);
        }

        /// <summary>
        /// Markdown と JSON をファイルに書き出す。親ディレクトリは自動作成する。
        /// </summary>
        public static void WriteReports(ShaderAnalysisResult result, RegressionReport report, string markdownPath, string jsonPath)
        {
            if (string.IsNullOrWhiteSpace(markdownPath) == false)
            {
                WriteTextFile(markdownPath, BuildMarkdown(result, report));
            }

            if (string.IsNullOrWhiteSpace(jsonPath) == false)
            {
                WriteTextFile(jsonPath, BuildJson(result, report));
            }
        }

        private static void AppendRegressionTable(StringBuilder builder, RegressionReport report)
        {
            var regressed = report.RegressedComparisons.ToList();
            if (regressed.Count == 0)
            {
                builder.AppendLine("## パフォーマンス悪化");
                builder.AppendLine();
                builder.AppendLine(report.HasBaseline ? "パフォーマンスの悪化は検出されませんでした。" : "ベースラインが無いため悪化判定はスキップされました。");
                builder.AppendLine();
                return;
            }

            builder.AppendLine("## パフォーマンス悪化一覧");
            builder.AppendLine();
            builder.AppendLine("| シェーダー | パス | キーワード | シェーダー種別 | 悪化した指標 | ベースライン | 現在 | 悪化率 |");
            builder.AppendLine("|---|---|---|---|---|---:|---:|---:|");
            foreach (var comparison in regressed)
            {
                foreach (var metric in comparison.Metrics.Where(metric => metric.IsRegression))
                {
                    builder.AppendLine(
                        $"| {Escape(comparison.Current.ShaderName)} " +
                        $"| {Escape(comparison.Current.PassName)} " +
                        $"| {Escape(ShaderPerfLabels.Variant(comparison.Current.Keywords))} " +
                        $"| {Escape(ShaderPerfLabels.ShaderType(comparison.Current.ShaderTypeName))} " +
                        $"| {Escape(ShaderPerfLabels.Metric(metric.MetricKey))} " +
                        $"| {FormatNumber(metric.BaselineValue)} " +
                        $"| {FormatNumber(metric.CurrentValue)} " +
                        $"| {FormatRatio(metric.WorseningRatio)} |");
                }
            }

            builder.AppendLine();
        }

        private static void AppendDetailSection(StringBuilder builder, ShaderAnalysisResult result)
        {
            var successes = result.Passes.Where(pass => pass.IsSuccess).ToList();
            if (successes.Count == 0)
            {
                return;
            }

            builder.AppendLine("## 解析結果詳細");
            builder.AppendLine();

            foreach (var group in successes.GroupBy(pass => pass.ShaderAssetPath).OrderBy(group => group.Key, StringComparer.Ordinal))
            {
                builder.AppendLine($"### {Escape(group.Key)}");
                builder.AppendLine();
                builder.AppendLine("| パス | キーワード | シェーダー種別 | ワークレジスタ | ユニフォームレジスタ | スレッド占有率 | FP16演算利用率(%) | サイクル数(パイプライン別) | ボトルネック |");
                builder.AppendLine("|---|---|---|---:|---:|---:|---:|---|---|");
                foreach (var pass in group
                             .OrderBy(pass => pass.PassIndex)
                             .ThenBy(pass => pass.ShaderTypeName, StringComparer.Ordinal)
                             .ThenBy(pass => new KeywordVariant(pass.Keywords).CanonicalKey, StringComparer.Ordinal))
                {
                    var metrics = pass.Metrics!;
                    var cycles = string.Join(
                        " / ",
                        metrics.TotalCyclesByPipeline
                            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                            .Select(pair => $"{ShaderPerfLabels.Pipeline(pair.Key)}:{FormatNumber(pair.Value)}"));
                    var bound = metrics.BoundPipelines.Count > 0
                        ? string.Join(", ", metrics.BoundPipelines.Select(ShaderPerfLabels.Pipeline))
                        : "-";
                    builder.AppendLine(
                        $"| {Escape(pass.PassName)} " +
                        $"| {Escape(ShaderPerfLabels.Variant(pass.Keywords))} " +
                        $"| {Escape(ShaderPerfLabels.ShaderType(pass.ShaderTypeName))} " +
                        $"| {FormatNullable(metrics.WorkRegisters)} " +
                        $"| {FormatNullable(metrics.UniformRegisters)} " +
                        $"| {FormatNullable(metrics.ThreadOccupancy)} " +
                        $"| {FormatNullable(metrics.Fp16ArithPercentage)} " +
                        $"| {Escape(string.IsNullOrEmpty(cycles) ? "-" : cycles)} " +
                        $"| {Escape(bound)} |");
                }

                builder.AppendLine();
            }
        }

        private static void AppendFailureSection(StringBuilder builder, ShaderAnalysisResult result)
        {
            var failures = result.Passes.Where(pass => pass.IsSuccess == false).ToList();
            if (failures.Count == 0)
            {
                return;
            }

            builder.AppendLine("## 解析できなかった対象");
            builder.AppendLine();
            foreach (var failure in failures)
            {
                var variant = new KeywordVariant(failure.Keywords);
                var variantSuffix = variant.IsDefault ? string.Empty : $" / {variant.DisplayLabel}";
                var location = string.IsNullOrEmpty(failure.PassName)
                    ? failure.ShaderAssetPath
                    : $"{failure.ShaderAssetPath} / {failure.PassName} / {failure.ShaderTypeName}{variantSuffix}";
                builder.AppendLine($"- ❌ {Escape(location)}: {Escape(FirstLine(failure.Error))}");
            }

            builder.AppendLine();
        }

        private static string FormatNullable(double? value)
        {
            return value.HasValue ? FormatNumber(value.Value) : "-";
        }

        private static string FormatNumber(double value)
        {
            return value.ToString("0.####", CultureInfo.InvariantCulture);
        }

        private static string FormatRatio(double ratio)
        {
            if (double.IsPositiveInfinity(ratio))
            {
                return "新規(0→)";
            }

            return (ratio * 100d).ToString("+0.##;-0.##;0", CultureInfo.InvariantCulture) + "%";
        }

        private static string FirstLine(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var newlineIndex = text.IndexOf('\n');
            return newlineIndex < 0 ? text : text.Substring(0, newlineIndex);
        }

        private static string Escape(string text)
        {
            // Markdown テーブルセルを壊さないよう、パイプと改行をエスケープ / 置換する。
            return (text ?? string.Empty).Replace("|", "\\|").Replace("\r", string.Empty).Replace("\n", " ");
        }

        private static void WriteTextFile(string path, string content)
        {
            var directory = Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(directory) == false)
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, content);
        }

        private sealed class ReportPayload
        {
            public string GpuCore { get; set; } = string.Empty;
            public string MaliocVersion { get; set; } = string.Empty;
            public string UnityVersion { get; set; } = string.Empty;
            public List<string> EnvironmentErrors { get; set; } = new();
            public bool HasBaseline { get; set; }
            public bool HasAnyRegression { get; set; }
            public List<string> Warnings { get; set; } = new();
            public List<ShaderPassAnalysis> Passes { get; set; } = new();
            public List<RegressionPayload> Regressions { get; set; } = new();
        }

        private sealed class RegressionPayload
        {
            public string ShaderAssetPath { get; set; } = string.Empty;
            public int SubShaderIndex { get; set; }
            public int PassIndex { get; set; }
            public string PassName { get; set; } = string.Empty;
            public string ShaderTypeName { get; set; } = string.Empty;
            public List<string> Keywords { get; set; } = new();
            public List<MetricComparison> Metrics { get; set; } = new();
        }
    }
}
