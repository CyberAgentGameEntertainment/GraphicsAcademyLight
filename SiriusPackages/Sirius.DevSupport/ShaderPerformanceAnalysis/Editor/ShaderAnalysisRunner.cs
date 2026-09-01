#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

namespace Sirius.DevSupport.ShaderPerformanceAnalysis
{
    /// <summary>
    /// 1 シェーダーパス・1 シェーダータイプの解析結果。
    /// </summary>
    internal sealed class ShaderPassAnalysis
    {
        public string ShaderAssetPath { get; set; } = string.Empty;
        public string ShaderName { get; set; } = string.Empty;
        public int SubShaderIndex { get; set; }
        public int PassIndex { get; set; }
        public string PassName { get; set; } = string.Empty;
        public string ShaderTypeName { get; set; } = string.Empty;

        /// <summary>
        /// この解析が対象とするシェーダーバリアント（正規化済み）。空 = キーワードなし。
        /// 識別キーには含まれない（レポート表示用途のみ）。
        /// </summary>
        public List<string> Keywords { get; set; } = new();

        /// <summary>解析成功時のメトリクス。失敗 / スキップ時は null。</summary>
        public ShaderPerfMetrics? Metrics { get; set; }

        /// <summary>対象単位のエラー。空なら成功。</summary>
        public string Error { get; set; } = string.Empty;

        /// <summary>ステージ不在によるスキップ（エラーではない）。</summary>
        public bool Skipped { get; set; }

        public bool IsSuccess => Metrics != null && string.IsNullOrEmpty(Error) && Skipped == false;
    }

    /// <summary>
    /// 一括解析の結果全体。回帰比較・レポート・ベースライン保存の入力になる。
    /// </summary>
    internal sealed class ShaderAnalysisResult
    {
        public string GpuCore { get; set; } = string.Empty;
        public string MaliocVersion { get; set; } = string.Empty;
        public string UnityVersion { get; set; } = string.Empty;
        public List<ShaderPassAnalysis> Passes { get; set; } = new();

        /// <summary>malioc 未設定 / GPU コア未設定など、解析全体を成立させない環境不備。</summary>
        public List<string> EnvironmentErrors { get; set; } = new();

        public bool HasEnvironmentError => EnvironmentErrors.Count > 0;
    }

    /// <summary>
    /// 複数シェーダー × 複数パス × シェーダータイプを一括で Mali Offline Compiler 解析する中核ランナー。
    /// 対象単位でエラーを隔離し、1 件の失敗で全体を止めない。対象 0 件は空結果として正常終了する。
    /// </summary>
    internal static class ShaderAnalysisRunner
    {
        private static readonly (ShaderType ShaderType, string Argument, string DisplayName)[] AnalysisStages =
        {
            (ShaderType.Vertex, "--vertex", "Vertex"),
            (ShaderType.Fragment, "--fragment", "Fragment"),
        };

        /// <summary>
        /// 解析を実行する。<paramref name="onProgress"/> は (0..1 進捗, メッセージ) を受け取る。
        /// </summary>
        public static ShaderAnalysisResult Analyze(
            IReadOnlyList<ShaderAnalysisTarget> targets,
            MaliSettings settings,
            CancellationToken cancellationToken = default,
            Action<float, string>? onProgress = null)
        {
            var result = new ShaderAnalysisResult
            {
                GpuCore = settings.GpuCore,
                UnityVersion = Application.unityVersion,
            };

            // --- 環境不備の事前チェック（黙って成功扱いにしない）---
            if (settings.TryResolveMaliocPath(out var maliocPath, out var pathError) == false)
            {
                result.EnvironmentErrors.Add(pathError);
            }

            if (string.IsNullOrWhiteSpace(settings.GpuCore))
            {
                result.EnvironmentErrors.Add("対象 GPU コアが設定されていません。設定で GPU コア（例: Mali-G715）を指定してください。");
            }

            if (result.HasEnvironmentError)
            {
                return result;
            }

            if (MaliProcessRunner.TryGetMaliocVersion(maliocPath, settings.ProcessTimeoutMs, out var version, out var versionError))
            {
                result.MaliocVersion = version;
            }
            else
            {
                result.EnvironmentErrors.Add(versionError);
                return result;
            }

            // --- 対象 0 件は空結果で正常終了 ---
            if (targets == null || targets.Count == 0)
            {
                return result;
            }

            for (var targetIndex = 0; targetIndex < targets.Count; targetIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var target = targets[targetIndex];
                onProgress?.Invoke(
                    (float)targetIndex / targets.Count,
                    $"解析中 ({targetIndex + 1}/{targets.Count}): {target.ShaderAssetPath}");

                AnalyzeTarget(target, settings, maliocPath, cancellationToken, result.Passes);
            }

            onProgress?.Invoke(1f, "解析完了");
            return result;
        }

        private static void AnalyzeTarget(
            ShaderAnalysisTarget target,
            MaliSettings settings,
            string maliocPath,
            CancellationToken cancellationToken,
            List<ShaderPassAnalysis> sink)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(target.ShaderAssetPath);
            if (shader == null)
            {
                sink.Add(new ShaderPassAnalysis
                {
                    ShaderAssetPath = target.ShaderAssetPath,
                    Error = $"シェーダーをロードできませんでした: {target.ShaderAssetPath}",
                });
                return;
            }

            IReadOnlyList<ShaderPassDescriptor> passes;
            try
            {
                passes = SpirvVariantCompiler.EnumeratePasses(shader);
            }
            catch (Exception exception)
            {
                sink.Add(new ShaderPassAnalysis
                {
                    ShaderAssetPath = target.ShaderAssetPath,
                    ShaderName = shader.name,
                    Error = $"パス列挙に失敗しました: {exception.Message}",
                });
                return;
            }

            // 1 シェーダー = 1 キーワード集合。パス × ステージで解析する（キーワードは target.Keywords を使う）。
            foreach (var descriptor in passes)
            {
                foreach (var stage in AnalysisStages)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var analysis = AnalyzePassStage(shader, target, descriptor, stage, settings, maliocPath, cancellationToken);
                    if (analysis != null)
                    {
                        sink.Add(analysis);
                    }
                }
            }
        }

        /// <summary>
        /// 1 パス・1 ステージを解析する。ステージ不在（fragment を持たない pass 等）は null を返してスキップする。
        /// </summary>
        private static ShaderPassAnalysis? AnalyzePassStage(
            Shader shader,
            ShaderAnalysisTarget target,
            ShaderPassDescriptor descriptor,
            (ShaderType ShaderType, string Argument, string DisplayName) stage,
            MaliSettings settings,
            string maliocPath,
            CancellationToken cancellationToken)
        {
            var analysis = new ShaderPassAnalysis
            {
                ShaderAssetPath = target.ShaderAssetPath,
                ShaderName = shader.name,
                SubShaderIndex = descriptor.SubShaderIndex,
                PassIndex = descriptor.PassIndex,
                PassName = descriptor.PassName,
                ShaderTypeName = stage.DisplayName,
                Keywords = new List<string>(target.Keywords.Keywords),
            };

            if (SpirvVariantCompiler.TryCompile(
                    shader, descriptor, stage.ShaderType, target.Keywords.ToArray(),
                    out var spirv, out var compileError, out var stageMissing) == false)
            {
                if (stageMissing)
                {
                    // ステージが存在しないだけなので、結果に残さずスキップする。
                    return null;
                }

                analysis.Error = compileError;
                return analysis;
            }

            var spirvPath = Path.Combine(
                Path.GetTempPath(),
                $"sirius_malioc_{Guid.NewGuid():N}.{(stage.ShaderType == ShaderType.Vertex ? "vert" : "frag")}.spv");

            try
            {
                File.WriteAllBytes(spirvPath, spirv);

                var arguments = MaliProcessRunner.BuildMaliocArguments(settings.GpuCore, stage.Argument, spirvPath);
                var processResult = MaliProcessRunner.RunProcess(maliocPath, arguments, settings.ProcessTimeoutMs, cancellationToken: cancellationToken);

                if (processResult.IsCancelled)
                {
                    throw new OperationCanceledException(cancellationToken);
                }

                if (processResult.IsTimeout)
                {
                    analysis.Error = "malioc 実行がタイムアウトしました。";
                    return analysis;
                }

                if (processResult.ExitCode != 0)
                {
                    analysis.Error = MaliProcessRunner.BuildProcessErrorMessage("malioc 実行に失敗しました。", processResult);
                    return analysis;
                }

                if (MaliResultParser.TryParse(processResult.StdOut, out var metrics, out var parseError))
                {
                    analysis.Metrics = metrics;
                }
                else
                {
                    analysis.Error = parseError;
                }

                return analysis;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                analysis.Error = $"解析中に例外が発生しました: {exception.Message}";
                return analysis;
            }
            finally
            {
                DeleteTempFileIfExists(spirvPath);
            }
        }

        private static void DeleteTempFileIfExists(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path) == false && File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // 一時ファイル削除の失敗は解析結果に影響しないため無視する。
            }
        }
    }
}
