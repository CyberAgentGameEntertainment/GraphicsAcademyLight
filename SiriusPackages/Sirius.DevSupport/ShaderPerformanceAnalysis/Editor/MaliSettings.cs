#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Sirius.DevSupport.ShaderPerformanceAnalysis
{
    /// <summary>
    /// シェーダーパフォーマンス解析の設定。malioc パスと対象 GPU コアを保持する。
    /// malioc パスはマシン依存なので <see cref="EditorUserSettings"/>（プロジェクトに含めない）に保存し、
    /// GPU コアやタイムアウト等の解析パラメータ、解析対象セット・ベースライン参照は
    /// <see cref="ScriptableSingleton{T}"/>（ProjectSettings 配下）に保存する。ProjectSettings はコミット対象なので
    /// 後者はチーム間で共有される（別 PC でも引き継がれる）。マシン依存の出力先などはここに保存しない。
    /// </summary>
    [FilePath("ProjectSettings/SiriusShaderPerfSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class MaliSettings : ScriptableSingleton<MaliSettings>
    {
        private const string MaliocPathUserSettingsKey = "Sirius.DevSupport.ShaderPerf.MaliocPath";

        /// <summary>Mali Offline Compiler のダウンロードページ。</summary>
        public const string MaliocDownloadUrl = "https://developer.arm.com/Tools%20and%20Software/Mali%20Offline%20Compiler";

        /// <summary>解析対象とする GPU コア名（例: "Mali-G715"）。</summary>
        [SerializeField]
        private string _gpuCore = string.Empty;

        /// <summary>malioc 1 回の実行タイムアウト（ミリ秒）。</summary>
        [SerializeField]
        private int _processTimeoutMs = 60000;

        /// <summary>回帰判定で無視する相対デルタ（例: 0.02 = 2% 以内の悪化はノイズとして許容）。</summary>
        [SerializeField]
        private float _regressionToleranceRatio = 0.02f;

        /// <summary>解析対象として登録したシェーダー集合。アセット参照なので GUID で永続化され別 PC でも解決できる。</summary>
        [SerializeField]
        private ShaderPerfTargetSet? _targetSet;

        /// <summary>
        /// ベースライン JSON のパス。マシン非依存にするためプロジェクトルートからの相対パス（区切りは '/'）で保存する。
        /// 相対化でき
        /// ないパス（別ドライブ等）はそのまま絶対パスを保存する（その場合チーム共有はできない）。
        /// </summary>
        [SerializeField]
        private string _baselineRelativePath = string.Empty;

        public string GpuCore
        {
            get => _gpuCore;
            set
            {
                if (string.Equals(_gpuCore, value, StringComparison.Ordinal))
                {
                    return;
                }

                _gpuCore = value ?? string.Empty;
                Save(true);
            }
        }

        public int ProcessTimeoutMs
        {
            get => _processTimeoutMs;
            set
            {
                var clamped = Mathf.Max(1000, value);
                if (_processTimeoutMs == clamped)
                {
                    return;
                }

                _processTimeoutMs = clamped;
                Save(true);
            }
        }

        public float RegressionToleranceRatio
        {
            get => _regressionToleranceRatio;
            set
            {
                var clamped = Mathf.Max(0f, value);
                if (Mathf.Approximately(_regressionToleranceRatio, clamped))
                {
                    return;
                }

                _regressionToleranceRatio = clamped;
                Save(true);
            }
        }

        /// <summary>
        /// 解析対象として登録したシェーダー集合。チーム共有のため ProjectSettings に保存する。
        /// </summary>
        public ShaderPerfTargetSet? TargetSet
        {
            get => _targetSet;
            set
            {
                if (_targetSet == value)
                {
                    return;
                }

                _targetSet = value;
                Save(true);
            }
        }

        /// <summary>
        /// ベースライン JSON の絶対パス。取得時は保存済みの相対パスを現在のプロジェクトルート基準で絶対化し、
        /// 設定時はプロジェクトルート相対へ正規化して保存する（チーム間でマシン非依存に共有するため）。
        /// </summary>
        public string BaselinePath
        {
            get => string.IsNullOrEmpty(_baselineRelativePath)
                ? string.Empty
                : ToAbsolutePath(_baselineRelativePath);
            set
            {
                var normalized = string.IsNullOrWhiteSpace(value)
                    ? string.Empty
                    : ToProjectRelativePath(value);
                if (string.Equals(_baselineRelativePath, normalized, StringComparison.Ordinal))
                {
                    return;
                }

                _baselineRelativePath = normalized;
                Save(true);
            }
        }

        /// <summary>
        /// malioc 実行パス。マシン依存のため <see cref="EditorUserSettings"/> に保存する。
        /// 未設定なら PATH / 既知のインストール先から自動発見を試みる。
        /// </summary>
        public string MaliocPath
        {
            get
            {
                var stored = EditorUserSettings.GetConfigValue(MaliocPathUserSettingsKey);
                return string.IsNullOrWhiteSpace(stored) ? string.Empty : stored;
            }
            set => EditorUserSettings.SetConfigValue(MaliocPathUserSettingsKey, value ?? string.Empty);
        }

        /// <summary>
        /// 有効な malioc パスを解決する。設定値が無効なら自動発見にフォールバックする。
        /// </summary>
        public bool TryResolveMaliocPath(out string maliocPath, out string errorMessage)
        {
            maliocPath = MaliocPath;
            errorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(maliocPath) == false && IsLikelyValidPath(maliocPath))
            {
                return true;
            }

            if (TryDiscoverMaliocPath(out var discovered))
            {
                maliocPath = discovered;
                MaliocPath = discovered;
                return true;
            }

            maliocPath = string.Empty;
            errorMessage =
                "malioc 実行ファイルが見つかりません。Arm Performance Studio をインストールしてください（malioc が同梱されます）。" +
                "標準のインストール先に入れれば自動検出します。検出できない場合は PATH を通すか、設定で malioc のフルパスを指定してください。" +
                $"\nダウンロード: {MaliocDownloadUrl}";
            return false;
        }

        private static bool IsLikelyValidPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            // 絶対パスは存在チェック。コマンド名のみ（PATH 解決前提）は実行時に判定する。
            return Path.IsPathRooted(path) == false || File.Exists(path);
        }

        private static bool TryDiscoverMaliocPath(out string maliocPath)
        {
            maliocPath = string.Empty;
            var executableNames = new[] { "malioc", "malioc.exe" };

            var candidates = new List<string>();

            // 1. PATH（明示的に通している場合を最優先）。
            var pathValue = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (var directory in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                foreach (var name in executableNames)
                {
                    candidates.Add(Path.Combine(directory, name));
                }
            }

            // 2. Arm Performance Studio / Arm Mobile Studio の標準インストール先（新しいバージョン優先）。
            //    インストールしただけで PATH 設定なしに検出できるようにする。
            candidates.AddRange(EnumerateArmStudioMaliocPaths());

            // 3. ユーザー / Unix 系の慣例的なインストール先。
            var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrWhiteSpace(homeDirectory) == false)
            {
                candidates.Add(Path.Combine(homeDirectory, ".local", "bin", "malioc"));
            }

            candidates.Add("/opt/homebrew/bin/malioc");
            candidates.Add("/usr/local/bin/malioc");
            candidates.Add("/usr/bin/malioc");

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    maliocPath = candidate;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Arm Performance Studio / Arm Mobile Studio はバージョン別ディレクトリにインストールされる
        /// （例: <c>C:\Program Files\Arm\Arm Performance Studio 2026.1\mali_offline_compiler\malioc.exe</c>）。
        /// 複数バージョンが併存しうるので、バージョン名（末尾の "YYYY.N"）で降順に並べ、新しいものを優先する。
        /// </summary>
        private static IEnumerable<string> EnumerateArmStudioMaliocPaths()
        {
            var installRoots = new List<string>();

            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (string.IsNullOrWhiteSpace(programFiles) == false)
            {
                installRoots.Add(Path.Combine(programFiles, "Arm"));
            }

            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            if (string.IsNullOrWhiteSpace(programFilesX86) == false)
            {
                installRoots.Add(Path.Combine(programFilesX86, "Arm"));
            }

            // macOS。
            installRoots.Add("/Applications");
            var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrWhiteSpace(homeDirectory) == false)
            {
                installRoots.Add(Path.Combine(homeDirectory, "Applications"));
                installRoots.Add(Path.Combine(homeDirectory, "arm"));
            }

            // Linux の慣例的なインストール先。
            installRoots.Add("/opt/arm");

            var studioDirectories = new List<string>();
            foreach (var root in installRoots)
            {
                if (Directory.Exists(root) == false)
                {
                    continue;
                }

                foreach (var pattern in new[] { "Arm Performance Studio*", "Arm Mobile Studio*", "Arm_Performance_Studio*", "Arm_Mobile_Studio*" })
                {
                    try
                    {
                        studioDirectories.AddRange(Directory.GetDirectories(root, pattern));
                    }
                    catch (Exception)
                    {
                        // アクセス権限不足などは探索の一候補に過ぎないため無視する。
                    }
                }
            }

            return studioDirectories
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(ParseStudioVersion)
                .SelectMany(studio =>
                {
                    var maliocDirectory = Path.Combine(studio, "mali_offline_compiler");
                    return new[]
                    {
                        Path.Combine(maliocDirectory, "malioc.exe"),
                        Path.Combine(maliocDirectory, "malioc"),
                    };
                });
        }

        /// <summary>インストールディレクトリ名末尾の "YYYY.N" を (年, マイナー) として取り出す。解析不能なら (0, 0)。</summary>
        private static (int Year, int Minor) ParseStudioVersion(string studioDirectory)
        {
            var token = Path.GetFileName(studioDirectory).Split(' ').LastOrDefault() ?? string.Empty;
            var parts = token.Split('.');
            int.TryParse(parts.ElementAtOrDefault(0), out var year);
            int.TryParse(parts.ElementAtOrDefault(1), out var minor);
            return (year, minor);
        }

        private static string ProjectRoot =>
            Directory.GetParent(Application.dataPath)?.FullName ?? Directory.GetCurrentDirectory();

        /// <summary>絶対パスをプロジェクトルート相対（区切り '/'）へ。相対化できない場合は絶対パスのまま返す。</summary>
        private static string ToProjectRelativePath(string absolutePath)
        {
            try
            {
                var full = Path.GetFullPath(absolutePath);
                var relative = Path.GetRelativePath(ProjectRoot, full);

                // 別ドライブ等で相対化できない場合（結果が絶対パスのまま）は絶対パスを保存する。
                // 共有はできないが、誤った相対パスでの参照破損を避ける。
                return Path.IsPathRooted(relative) ? full : relative.Replace('\\', '/');
            }
            catch (ArgumentException)
            {
                return absolutePath;
            }
        }

        /// <summary>保存済みパスを絶対パスへ。絶対パスならそのまま、相対パスはプロジェクトルート基準で解決する。</summary>
        private static string ToAbsolutePath(string storedPath)
        {
            if (Path.IsPathRooted(storedPath))
            {
                return storedPath;
            }

            try
            {
                return Path.GetFullPath(Path.Combine(ProjectRoot, storedPath));
            }
            catch (ArgumentException)
            {
                return storedPath;
            }
        }
    }
}
