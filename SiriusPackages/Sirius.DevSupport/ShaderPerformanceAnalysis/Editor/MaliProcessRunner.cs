#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Process = System.Diagnostics.Process;
using ProcessStartInfo = System.Diagnostics.ProcessStartInfo;

namespace Sirius.DevSupport.ShaderPerformanceAnalysis
{
    /// <summary>
    /// Mali Offline Compiler (malioc) の外部プロセス実行を担うヘルパー。
    /// timeout / cancel / PATH 拡張に対応し、<c>--list</c> / <c>--version</c> の結果はキャッシュする。
    /// vision-client の MaliOCProcessHelper のロジックを参考にした独立実装（直接移植ではない）。
    /// </summary>
    internal static class MaliProcessRunner
    {
        private static readonly Dictionary<string, string> MaliocVersionCache = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, List<string>> MaliocGpuCoreCache = new(StringComparer.Ordinal);

        /// <summary>
        /// 外部プロセスを実行し、標準出力 / 標準エラー / 終了コードを収集する。
        /// </summary>
        public static ProcessResult RunProcess(
            string executablePath,
            string arguments,
            int timeoutMs,
            string? workingDirectory = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var startInfo = new ProcessStartInfo
                {
                    FileName = executablePath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                    CreateNoWindow = true,
                };
                if (string.IsNullOrWhiteSpace(workingDirectory) == false)
                {
                    startInfo.WorkingDirectory = workingDirectory;
                }

                startInfo.EnvironmentVariables["PATH"] = BuildAugmentedPath(executablePath);
                startInfo.EnvironmentVariables["LANG"] = "en_US.UTF-8";
                startInfo.EnvironmentVariables["LC_ALL"] = "en_US.UTF-8";

                using var process = new Process { StartInfo = startInfo };
                var stdoutBuilder = new StringBuilder();
                var stderrBuilder = new StringBuilder();
                var stdoutCompleted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var stderrCompleted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                process.OutputDataReceived += (_, args) =>
                {
                    if (args.Data == null)
                    {
                        stdoutCompleted.TrySetResult(true);
                        return;
                    }

                    AppendOutputLine(stdoutBuilder, args.Data);
                };
                process.ErrorDataReceived += (_, args) =>
                {
                    if (args.Data == null)
                    {
                        stderrCompleted.TrySetResult(true);
                        return;
                    }

                    AppendOutputLine(stderrBuilder, args.Data);
                };

                using var cancellationRegistration = cancellationToken.Register(() => TryKillProcess(process));
                if (process.Start() == false)
                {
                    return ProcessResult.Fail("プロセスの開始に失敗しました。");
                }

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                var deadlineUtc = DateTime.UtcNow.AddMilliseconds(timeoutMs);

                while (process.WaitForExit(100) == false)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        TryKillProcess(process);
                        return ProcessResult.Cancelled();
                    }

                    if (DateTime.UtcNow >= deadlineUtc)
                    {
                        TryKillProcess(process);
                        return ProcessResult.Timeout();
                    }
                }

                // 残りの非同期出力イベントが流れ切るのを短時間待つ。
                process.WaitForExit();
                Task.WaitAll(new[] { stdoutCompleted.Task, stderrCompleted.Task }, 1000);

                if (cancellationToken.IsCancellationRequested)
                {
                    return ProcessResult.Cancelled();
                }

                return ProcessResult.Success(process.ExitCode, stdoutBuilder.ToString(), stderrBuilder.ToString());
            }
            catch (OperationCanceledException)
            {
                return ProcessResult.Cancelled();
            }
            catch (Exception exception)
            {
                return ProcessResult.Fail(exception.ToString());
            }
        }

        /// <summary>
        /// malioc に渡す解析引数を組み立てる（Vulkan / SPIR-V / JSON 出力固定）。
        /// </summary>
        public static string BuildMaliocArguments(string gpuCore, string shaderTypeArgument, string spirvPath)
        {
            return $"--vulkan --spirv {shaderTypeArgument} --core {QuoteArgument(gpuCore)} --format json {QuoteArgument(spirvPath)}";
        }

        public static string QuoteArgument(string value)
        {
            return $"\"{value.Replace("\"", "\\\"")}\"";
        }

        /// <summary>
        /// <c>malioc --list</c> から OpenGL ES 対応 GPU コア名を抽出する。結果はパス単位でキャッシュ。
        /// </summary>
        public static bool TryGetGpuCores(string maliocPath, int timeoutMs, out List<string> cores, out string errorMessage)
        {
            if (MaliocGpuCoreCache.TryGetValue(maliocPath, out var cachedCores))
            {
                cores = new List<string>(cachedCores);
                errorMessage = string.Empty;
                return true;
            }

            cores = new List<string>();
            errorMessage = string.Empty;

            var processResult = RunProcess(maliocPath, "--list", timeoutMs);
            if (processResult.IsTimeout)
            {
                errorMessage = "malioc --list がタイムアウトしました。";
                return false;
            }

            if (processResult.ExitCode != 0)
            {
                errorMessage = BuildProcessErrorMessage("malioc --list 実行に失敗しました。", processResult);
                return false;
            }

            foreach (var line in processResult.StdOut.Split('\n'))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed))
                {
                    continue;
                }

                var splitIndex = trimmed.IndexOf(" (", StringComparison.Ordinal);
                if (splitIndex <= 0 || trimmed.Contains("OpenGL ES", StringComparison.OrdinalIgnoreCase) == false)
                {
                    continue;
                }

                var coreName = trimmed[..splitIndex].Trim();
                if (string.IsNullOrWhiteSpace(coreName) == false)
                {
                    cores.Add(coreName);
                }
            }

            cores = cores.Distinct().OrderBy(value => value).ToList();
            if (cores.Count == 0)
            {
                errorMessage = "malioc --list の出力から GPU コアを抽出できませんでした。";
                return false;
            }

            MaliocGpuCoreCache[maliocPath] = new List<string>(cores);
            return true;
        }

        /// <summary>
        /// <c>malioc --version</c> の出力（正規化済み）を取得する。結果はパス単位でキャッシュ。
        /// ベースライン比較のキー（malioc バージョン）に用いる。
        /// </summary>
        public static bool TryGetMaliocVersion(string maliocPath, int timeoutMs, out string versionText, out string errorMessage)
        {
            if (MaliocVersionCache.TryGetValue(maliocPath, out versionText!))
            {
                errorMessage = string.Empty;
                return true;
            }

            var processResult = RunProcess(maliocPath, "--version", timeoutMs);
            if (processResult.IsTimeout)
            {
                versionText = string.Empty;
                errorMessage = "malioc --version がタイムアウトしました。";
                return false;
            }

            if (processResult.ExitCode != 0)
            {
                versionText = string.Empty;
                errorMessage = BuildProcessErrorMessage("malioc --version 実行に失敗しました。", processResult);
                return false;
            }

            versionText = NormalizeMultilineText(processResult.StdOut);
            if (string.IsNullOrWhiteSpace(versionText))
            {
                versionText = NormalizeMultilineText(processResult.StdErr);
            }

            if (string.IsNullOrWhiteSpace(versionText))
            {
                errorMessage = "malioc --version の出力が空です。";
                return false;
            }

            MaliocVersionCache[maliocPath] = versionText;
            errorMessage = string.Empty;
            return true;
        }

        /// <summary>
        /// プロセス失敗時の原因が分かるエラーメッセージを組み立てる。
        /// </summary>
        public static string BuildProcessErrorMessage(string title, ProcessResult result)
        {
            var parts = new List<string> { title };
            if (string.IsNullOrWhiteSpace(result.ErrorMessage) == false)
            {
                parts.Add(result.ErrorMessage);
            }

            if (string.IsNullOrWhiteSpace(result.StdErr) == false)
            {
                parts.Add($"stderr:\n{result.StdErr}");
            }

            if (string.IsNullOrWhiteSpace(result.StdOut) == false)
            {
                parts.Add($"stdout:\n{result.StdOut}");
            }

            parts.Add($"終了コード: {result.ExitCode}");
            return string.Join("\n\n", parts);
        }

        /// <summary>
        /// テスト用にキャッシュをクリアする。
        /// </summary>
        public static void ClearCaches()
        {
            MaliocVersionCache.Clear();
            MaliocGpuCoreCache.Clear();
        }

        private static void TryKillProcess(Process process)
        {
            try
            {
                if (process.HasExited == false)
                {
                    process.Kill();
                }
            }
            catch
            {
                // 既に終了している場合などは無視する。
            }
        }

        private static string NormalizeMultilineText(string text)
        {
            return string.Join(
                "\n",
                text.Split('\n')
                    .Select(line => line.TrimEnd('\r'))
                    .Where(line => string.IsNullOrWhiteSpace(line) == false));
        }

        private static string BuildAugmentedPath(string executablePath)
        {
            var pathEntries = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var existingPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (var entry in existingPath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                AddPathEntry(pathEntries, seen, entry);
            }

            var executableDirectory = Path.GetDirectoryName(executablePath);
            AddPathEntry(pathEntries, seen, executableDirectory);

            var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrWhiteSpace(homeDirectory) == false)
            {
                AddPathEntry(pathEntries, seen, Path.Combine(homeDirectory, ".local", "bin"));
            }

            // Unix 系の慣例的なインストール先（Windows では Directory.Exists で弾かれるため無害）。
            AddPathEntry(pathEntries, seen, "/usr/local/bin");
            AddPathEntry(pathEntries, seen, "/opt/homebrew/bin");
            AddPathEntry(pathEntries, seen, "/usr/bin");
            AddPathEntry(pathEntries, seen, "/bin");

            return string.Join(Path.PathSeparator, pathEntries);
        }

        private static void AddPathEntry(List<string> entries, HashSet<string> seen, string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || Directory.Exists(path) == false || seen.Add(path!) == false)
            {
                return;
            }

            entries.Add(path!);
        }

        private static void AppendOutputLine(StringBuilder builder, string line)
        {
            if (builder.Length > 0)
            {
                builder.Append('\n');
            }

            builder.Append(line);
        }

        /// <summary>
        /// 外部プロセスの実行結果。成功 / 失敗 / タイムアウト / キャンセルを区別する。
        /// </summary>
        public readonly struct ProcessResult
        {
            private ProcessResult(int exitCode, string stdOut, string stdErr, bool isTimeout, bool isCancelled, string errorMessage)
            {
                ExitCode = exitCode;
                StdOut = stdOut;
                StdErr = stdErr;
                IsTimeout = isTimeout;
                IsCancelled = isCancelled;
                ErrorMessage = errorMessage;
            }

            public int ExitCode { get; }
            public string StdOut { get; }
            public string StdErr { get; }
            public bool IsTimeout { get; }
            public bool IsCancelled { get; }
            public string ErrorMessage { get; }

            public static ProcessResult Success(int exitCode, string stdOut, string stdErr)
                => new(exitCode, stdOut, stdErr, false, false, string.Empty);

            public static ProcessResult Timeout()
                => new(-1, string.Empty, string.Empty, true, false, string.Empty);

            public static ProcessResult Cancelled()
                => new(-1, string.Empty, string.Empty, false, true, string.Empty);

            public static ProcessResult Fail(string errorMessage)
                => new(-1, string.Empty, string.Empty, false, false, errorMessage);
        }
    }
}
