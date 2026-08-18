#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace Sirius.DevSupport.ShaderPerformanceAnalysis
{
    /// <summary>
    /// CI / バッチ実行用の headless 静的入口。<c>-batchmode -executeMethod</c> / uloop / MenuItem から起動できる。
    /// request JSON（対象リスト or git base ref, GPU コア, 出力先）を受け取り、レポートを出力して
    /// 回帰有無を result JSON と終了コードで返す。
    /// </summary>
    public static class ShaderPerfBatchEntry
    {
        private const string RequestPathArgument = "-shaderPerfRequest";
        private const string ResultPathArgument = "-shaderPerfResult";

        private const int ExitCodeClean = 0;
        private const int ExitCodeError = 1;
        private const int ExitCodeRegression = 2;

        /// <summary>
        /// <c>-executeMethod</c> から呼ばれるエントリ。コマンドライン引数で request/result JSON のパスを受け取る。
        /// batchmode では結果に応じた終了コードで <see cref="EditorApplication.Exit"/> する。
        /// </summary>
        public static void RunFromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            var requestPath = GetArgumentValue(args, RequestPathArgument);
            var resultPath = GetArgumentValue(args, ResultPathArgument);

            int exitCode;
            ShaderPerfBatchResult result;
            try
            {
                if (string.IsNullOrWhiteSpace(requestPath) || File.Exists(requestPath) == false)
                {
                    result = ShaderPerfBatchResult.Failure($"request JSON が見つかりません: {requestPath} （{RequestPathArgument} で指定してください）");
                    exitCode = ExitCodeError;
                }
                else
                {
                    var request = JsonConvert.DeserializeObject<ShaderPerfBatchRequest>(File.ReadAllText(requestPath))
                                  ?? new ShaderPerfBatchRequest();
                    exitCode = Run(request, out result);
                }
            }
            catch (Exception exception)
            {
                result = ShaderPerfBatchResult.Failure($"バッチ実行中に例外が発生しました: {exception}");
                exitCode = ExitCodeError;
            }

            if (string.IsNullOrWhiteSpace(resultPath) == false)
            {
                TryWriteResult(resultPath, result);
            }

            Debug.Log($"[ShaderPerf] バッチ完了: exitCode={exitCode}, {result.Message}");

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(exitCode);
            }
        }

        /// <summary>
        /// プログラムから直接呼び出すエントリ。終了コードを返す。
        /// </summary>
        public static int Run(ShaderPerfBatchRequest request, out ShaderPerfBatchResult result)
        {
            var settings = MaliSettings.instance;
            if (string.IsNullOrWhiteSpace(request.GpuCore) == false)
            {
                settings.GpuCore = request.GpuCore;
            }

            if (request.RegressionToleranceRatio.HasValue)
            {
                settings.RegressionToleranceRatio = request.RegressionToleranceRatio.Value;
            }

            if (TryResolveTargets(request, out var targets, out var targetError) == false)
            {
                result = ShaderPerfBatchResult.Failure(targetError);
                return ExitCodeError;
            }

            var analysis = ShaderAnalysisRunner.Analyze(targets, settings, CancellationToken.None);
            if (analysis.HasEnvironmentError)
            {
                result = new ShaderPerfBatchResult
                {
                    Success = false,
                    EnvironmentErrors = analysis.EnvironmentErrors,
                    Message = "環境不備のため解析できませんでした: " + string.Join(" / ", analysis.EnvironmentErrors),
                };
                return ExitCodeError;
            }

            ShaderPerfBaseline? baseline = null;
            try
            {
                baseline = ShaderPerfBaseline.LoadFromFile(request.BaselinePath);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[ShaderPerf] ベースライン読み込みに失敗しました（初回扱いで継続）: {exception.Message}");
            }

            var report = RegressionComparer.Compare(analysis, baseline, settings.RegressionToleranceRatio);

            if (request.RetryOnRegression && report.HasAnyRegression)
            {
                report = ReAnalyzeRegressions(analysis, report, targets, settings, baseline);
            }

            ShaderPerfReporter.WriteReports(analysis, report, request.MarkdownReportPath, request.JsonReportPath);

            if (request.UpdateBaseline && string.IsNullOrWhiteSpace(request.BaselinePath) == false)
            {
                ShaderPerfBaseline.FromResult(analysis).SaveToFile(request.BaselinePath);
            }

            var analyzedCount = analysis.Passes.Count(pass => pass.IsSuccess);
            var failedCount = analysis.Passes.Count(pass => pass.IsSuccess == false);

            result = new ShaderPerfBatchResult
            {
                Success = true,
                HasRegression = report.HasAnyRegression,
                AnalyzedPassCount = analyzedCount,
                FailedPassCount = failedCount,
                MarkdownReportPath = request.MarkdownReportPath ?? string.Empty,
                JsonReportPath = request.JsonReportPath ?? string.Empty,
                Message = report.HasAnyRegression
                    ? $"パフォーマンスの悪化を {report.RegressedComparisons.Count()} 件検出しました（解析 {analyzedCount} / 失敗 {failedCount}）。"
                    : $"パフォーマンスの悪化は検出されませんでした（解析 {analyzedCount} / 失敗 {failedCount}）。",
            };

            return report.HasAnyRegression ? ExitCodeRegression : ExitCodeClean;
        }

        /// <summary>
        /// 回帰が検出された <paramref name="currentReport"/> のシェーダーのみを再解析し、
        /// <paramref name="analysis"/> のパスを差し替えた上で新しい <see cref="RegressionReport"/> を返す。
        /// 再解析で環境エラーが発生した場合は元の <paramref name="currentReport"/> をそのまま返す。
        /// </summary>
        private static RegressionReport ReAnalyzeRegressions(
            ShaderAnalysisResult analysis,
            RegressionReport currentReport,
            List<ShaderAnalysisTarget> allTargets,
            MaliSettings settings,
            ShaderPerfBaseline? baseline)
        {
            var regressedPathSet = new HashSet<string>(
                currentReport.RegressedComparisons.Select(c => c.Current.ShaderAssetPath),
                StringComparer.Ordinal);

            var regressedTargets = allTargets
                .Where(t => regressedPathSet.Contains(t.ShaderAssetPath))
                .ToList();

            if (regressedTargets.Count == 0)
            {
                return currentReport;
            }

            Debug.Log($"[ShaderPerf] 回帰検出: {regressedTargets.Count} 件のシェーダーを再解析します。");
            var reResult = ShaderAnalysisRunner.Analyze(regressedTargets, settings, CancellationToken.None);

            if (reResult.HasEnvironmentError)
            {
                Debug.LogWarning("[ShaderPerf] 再解析で環境エラーが発生しました。初回結果を使用します。");
                return currentReport;
            }

            analysis.Passes.RemoveAll(pass => regressedPathSet.Contains(pass.ShaderAssetPath));
            analysis.Passes.AddRange(reResult.Passes);

            var newReport = RegressionComparer.Compare(analysis, baseline, settings.RegressionToleranceRatio);
            Debug.Log($"[ShaderPerf] 再解析完了。残存回帰: {newReport.RegressedComparisons.Count()} 件。");
            return newReport;
        }

        /// <summary>
        /// 解析対象を解決する。登録済みシェーダー（<see cref="ShaderPerfTargetSet"/> アセット）を主経路とし、
        /// 明示パス（<see cref="ShaderPerfBatchRequest.ShaderAssetPaths"/>）も補助的に受け付ける。
        /// CI でも git 差分の自動抽出は行わない（include 経由の波及を取りこぼすため）。
        /// </summary>
        private static bool TryResolveTargets(
            ShaderPerfBatchRequest request,
            out List<ShaderAnalysisTarget> targets,
            out string errorMessage)
        {
            errorMessage = string.Empty;
            var collected = new List<ShaderAnalysisTarget>();

            // request の生パスはキーワードなしとして解析する。
            if (request.ShaderAssetPaths != null)
            {
                collected.AddRange(ShaderAnalysisTarget.FromAssetPaths(request.ShaderAssetPaths));
            }

            // TargetSet からは登録シェーダー（キーワードなし）＋宣言されたシェーダーバリアントを取り込む。
            if (string.IsNullOrWhiteSpace(request.TargetSetAssetPath) == false)
            {
                var targetSet = AssetDatabase.LoadAssetAtPath<ShaderPerfTargetSet>(request.TargetSetAssetPath);
                if (targetSet == null)
                {
                    targets = new List<ShaderAnalysisTarget>();
                    errorMessage = $"ShaderPerfTargetSet アセットをロードできませんでした: {request.TargetSetAssetPath}";
                    return false;
                }

                collected.AddRange(targetSet.ToTargets());
            }

            // アセットパスで束ねて和集合化（同一パスのシェーダーバリアントをマージ）。
            targets = ShaderAnalysisTarget.Merge(collected);
            return true;
        }

        private static void TryWriteResult(string resultPath, ShaderPerfBatchResult result)
        {
            try
            {
                var directory = Path.GetDirectoryName(resultPath);
                if (string.IsNullOrWhiteSpace(directory) == false)
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(resultPath, JsonConvert.SerializeObject(result, Formatting.Indented));
            }
            catch (Exception exception)
            {
                Debug.LogError($"[ShaderPerf] result JSON の書き込みに失敗しました: {exception.Message}");
            }
        }

        private static string GetArgumentValue(string[] args, string argumentName)
        {
            for (var index = 0; index < args.Length - 1; index++)
            {
                if (string.Equals(args[index], argumentName, StringComparison.Ordinal))
                {
                    return args[index + 1];
                }
            }

            return string.Empty;
        }
    }

    /// <summary>
    /// バッチ解析のリクエスト。request JSON にそのまま対応する。
    /// </summary>
    public sealed class ShaderPerfBatchRequest
    {
        /// <summary>
        /// 登録済みシェーダーを保持する <see cref="ShaderPerfTargetSet"/> アセットのパス（CI の主経路）。
        /// </summary>
        public string? TargetSetAssetPath { get; set; }

        /// <summary>明示指定するシェーダーアセットパス（任意・補助）。</summary>
        public List<string>? ShaderAssetPaths { get; set; }

        /// <summary>対象 GPU コア。指定時は設定を上書きする。</summary>
        public string? GpuCore { get; set; }

        /// <summary>比較に使うベースライン JSON のパス。</summary>
        public string? BaselinePath { get; set; }

        /// <summary>Markdown レポート出力先。</summary>
        public string? MarkdownReportPath { get; set; }

        /// <summary>JSON レポート出力先。</summary>
        public string? JsonReportPath { get; set; }

        /// <summary>true なら今回の結果でベースライン（<see cref="BaselinePath"/>）を更新する。</summary>
        public bool UpdateBaseline { get; set; }

        /// <summary>回帰判定の許容相対デルタ（任意。指定時は設定を上書き）。</summary>
        public float? RegressionToleranceRatio { get; set; }

        /// <summary>true なら回帰検出後、悪化シェーダーのみを自動で再解析して結果を確定させる。</summary>
        public bool RetryOnRegression { get; set; }
    }

    /// <summary>
    /// バッチ解析の結果。result JSON にそのまま対応する。
    /// </summary>
    public sealed class ShaderPerfBatchResult
    {
        public bool Success { get; set; }
        public bool HasRegression { get; set; }
        public int AnalyzedPassCount { get; set; }
        public int FailedPassCount { get; set; }
        public List<string> EnvironmentErrors { get; set; } = new();
        public string MarkdownReportPath { get; set; } = string.Empty;
        public string JsonReportPath { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;

        public static ShaderPerfBatchResult Failure(string message)
        {
            return new ShaderPerfBatchResult { Success = false, Message = message };
        }
    }
}
