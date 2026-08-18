// --------------------------------------------------------------
// Copyright 2025 CyberAgent, Inc.
// --------------------------------------------------------------

using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Sirius.DevSupport
{
    /// <summary>
    ///     NvidiaのFlipを利用する画像比較機能を提供するクラス
    /// </summary>
    public static class NvidiaFlip
    {
        private static string _flipExecutablePath;

        private static string FlipExecutablePath
        {
            get
            {
                _flipExecutablePath ??= FindFlipExecutable();
                return _flipExecutablePath;
            }
        }

        private static string FindFlipExecutable()
        {
#if UNITY_EDITOR_WIN || UNITY_EDITOR_OSX
#if UNITY_EDITOR_WIN
            const string flipGuid = "0914acf933b4bad42b0eb324ee28e331";
#else
            const string flipGuid = "914e96ccae4c6a84db9617d649c5441f";
#endif

            var assetPath = AssetDatabase.GUIDToAssetPath(flipGuid);

            if (string.IsNullOrEmpty(assetPath))
            {
                throw new FileNotFoundException($"flip executable not found (GUID: {flipGuid})");
            }

            // Packages/の場合はPackageInfo経由で実際のファイルシステムパスを取得
            if (assetPath.StartsWith("Packages/"))
            {
                var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(assetPath);
                if (packageInfo != null)
                {
                    // Packages/{packageName}/... から相対パスを抽出
                    var relativePath = assetPath.Substring($"Packages/{packageInfo.name}/".Length);
                    return Path.Combine(packageInfo.resolvedPath, relativePath);
                }
            }

            // Assets/の場合は従来通り
            // ReSharper disable once PossibleNullReferenceException
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, assetPath);
#else
            // flip 実行ファイルは Windows / macOS のみ提供（GUID も両 OS だけに存在）。
            // Linux Editor（CI のシェーダー性能解析コンテナ等）ではコンパイルのみ通し、呼び出し時に明示的に失敗させる。
            throw new PlatformNotSupportedException("flip is only supported on Windows and macOS");
#endif
        }

        /// <summary>
        ///     Flipによる画像比較を実行する
        /// </summary>
        /// <param name="referenceImagePath"></param>
        /// <param name="testImagePath"></param>
        /// <returns>flipによる実行結果</returns>
        public static Result Execute(string referenceImagePath, string testImagePath)
        {
            Result result;

            // ReSharper disable once PossibleNullReferenceException
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            referenceImagePath = Path.Combine(projectRoot, referenceImagePath);
            testImagePath = Path.Combine(projectRoot, testImagePath);
            // コマンドライン引数
            var args = $"-r \"{referenceImagePath}\" -t \"{testImagePath}\"";

            var psi = new ProcessStartInfo
            {
                FileName = FlipExecutablePath,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            try
            {
                using var process = Process.Start(psi);
                // ReSharper disable once PossibleNullReferenceException
                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                result = ParseFlipOutput(output);
                if (result.Mean < 0) throw new ParseException("Parse Error");
                if (!string.IsNullOrEmpty(error)) Debug.LogError("Flip Error: " + error);
            }
            catch (ParseException)
            {
                Debug.LogWarning("flip result parse error: ");
                throw;
            }
            catch (Exception e)
            {
                Debug.LogError("Error running flip command: " + e.Message);
                throw;
            }

            return result;
        }

        /// <summary>
        /// Flipによる画像比較を並列実行する
        /// </summary>
        /// <param name="absoluteReferenceImagePath">リファレンス画像の絶対パス</param>
        /// <param name="absoluteTestImagePath">テスト画像の絶対パス</param>
        public static async Awaitable<Result> ExecuteAsync(string absoluteReferenceImagePath, string absoluteTestImagePath)
        {
            if (_flipExecutablePath == null)
            {
                // PathはAssetDatabase経由で取得するので、メインスレッドに戻す
                await Awaitable.MainThreadAsync();
                _ = FlipExecutablePath;
                await Awaitable.BackgroundThreadAsync();
            }
            var executablePath = _flipExecutablePath;

            var args = $"-r \"{absoluteReferenceImagePath}\" -t \"{absoluteTestImagePath}\"";

            var psi = new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            try
            {
                using var process = new Process();
                process.StartInfo = psi;
                process.Start();
                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();
                // AwaitableでProcessを待てないためTask.Runにする
                await Task.Run(process.WaitForExit).ConfigureAwait(false);
                var output = await outputTask.ConfigureAwait(false);
                var error = await errorTask.ConfigureAwait(false);

                var result = ParseFlipOutput(output);
                if (result.Mean < 0) throw new ParseException("Parse Error");
                if (!string.IsNullOrEmpty(error)) Debug.LogError("Flip Error: " + error);
                return result;
            }
            catch (ParseException)
            {
                Debug.LogWarning("flip result parse error: ");
                throw;
            }
            catch (Exception e)
            {
                Debug.LogError("Error running flip command: " + e.Message);
                throw;
            }
        }

        private static Result ParseFlipOutput(string output)
        {
            var result = new Result
            {
                // 各行を正規表現でマッチ（キー: 値 の形式）
                Mean = ParseKey(output, @"Mean:\s*([-+]?[0-9]*\.?[0-9]+)"),
                WeightedMedian = ParseKey(output, @"Weighted median:\s*([-+]?[0-9]*\.?[0-9]+)"),
                FirstQuartile = ParseKey(output, @"1st weighted quartile:\s*([-+]?[0-9]*\.?[0-9]+)"),
                ThirdQuartile = ParseKey(output, @"3rd weighted quartile:\s*([-+]?[0-9]*\.?[0-9]+)"),
                Min = ParseKey(output, @"Min:\s*([-+]?[0-9]*\.?[0-9]+)"),
                Max = ParseKey(output, @"Max:\s*([-+]?[0-9]*\.?[0-9]+)")
            };

            return result;
        }

        private static float ParseKey(string input, string pattern)
        {
            var match = Regex.Match(input, pattern);
            if (match.Success && float.TryParse(match.Groups[1].Value, out var value))
                return value;
            Debug.LogWarning("Could not parse pattern: " + pattern);
            return -1f;
        }

        private class ParseException : Exception
        {
            public ParseException(string message)
                : base(message)
            {
            }
        }

        /// <summary>
        ///     Flipの実行結果
        /// </summary>
        public class Result
        {
            public float FirstQuartile; // 第1重み付き四分位数。差分マップの値の下位25%がこの値以下であることを示します。（重み付き）
            public float Max; // 差分マップにおける最も大きい差分値。
            public float Mean; // 差分マップの重み付き平均値。
            public float Min; // 差分マップにおける最も小さい差分値。
            public float ThirdQuartile; // 第3重み付き四分位数。 差分マップの値の下位75%がこの値以下であることを示します（重み付き）
            public float WeightedMedian; // // 重み付き中央値。差分マップにおけるピクセルごとの差分値を小さい順に並べたときの中央に位置する値。
        }
    }
}
