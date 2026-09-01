#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace Sirius.DevSupport.ShaderPerformanceAnalysis
{
    /// <summary>
    /// 回帰比較の基準となるベースライン。
    /// ヘッダ（GPU コア / malioc バージョン / Unity バージョン）が一致する条件下でのみ比較が有効になる。
    /// JSON にコミットしてリポジトリで管理する想定。
    /// </summary>
    internal sealed class ShaderPerfBaseline
    {
        /// <summary>ベースライン JSON のスキーマバージョン（将来の互換性のため）。</summary>
        public int SchemaVersion { get; set; } = 1;

        public string GpuCore { get; set; } = string.Empty;
        public string MaliocVersion { get; set; } = string.Empty;
        public string UnityVersion { get; set; } = string.Empty;

        public List<ShaderPerfBaselineEntry> Entries { get; set; } = new();

        /// <summary>
        /// 解析結果（成功パスのみ）からベースラインを構築する。
        /// </summary>
        public static ShaderPerfBaseline FromResult(ShaderAnalysisResult result)
        {
            var baseline = new ShaderPerfBaseline
            {
                GpuCore = result.GpuCore,
                MaliocVersion = result.MaliocVersion,
                UnityVersion = result.UnityVersion,
            };

            foreach (var pass in result.Passes.Where(pass => pass.IsSuccess))
            {
                baseline.Entries.Add(new ShaderPerfBaselineEntry
                {
                    ShaderAssetPath = pass.ShaderAssetPath,
                    SubShaderIndex = pass.SubShaderIndex,
                    PassIndex = pass.PassIndex,
                    ShaderTypeName = pass.ShaderTypeName,
                    Metrics = pass.Metrics!,
                });
            }

            baseline.Entries.Sort((a, b) => string.CompareOrdinal(a.EntryKey, b.EntryKey));
            return baseline;
        }

        /// <summary>
        /// エントリキーで検索する。見つからなければ null（= 初回計測のシェーダー）。
        /// </summary>
        public ShaderPerfBaselineEntry? FindEntry(string entryKey)
        {
            return Entries.FirstOrDefault(entry => string.Equals(entry.EntryKey, entryKey, StringComparison.Ordinal));
        }

        /// <summary>
        /// ヘッダ（GPU コア / malioc バージョン / Unity バージョン）が解析結果と一致するか。
        /// 一致しないベースラインで回帰判定すると誤検出になるため、比較側でガードに使う。
        /// </summary>
        public bool MatchesEnvironment(ShaderAnalysisResult result)
        {
            return string.Equals(GpuCore, result.GpuCore, StringComparison.Ordinal)
                   && string.Equals(MaliocVersion, result.MaliocVersion, StringComparison.Ordinal)
                   && string.Equals(UnityVersion, result.UnityVersion, StringComparison.Ordinal);
        }

        /// <summary>
        /// 環境ヘッダのうち、解析結果と一致しない項目を人可読で列挙する（不一致が無ければ空文字）。
        /// </summary>
        public string DescribeEnvironmentMismatch(ShaderAnalysisResult result)
        {
            var diffs = new List<string>();
            if (string.Equals(GpuCore, result.GpuCore, StringComparison.Ordinal) == false)
            {
                diffs.Add($"GPU コア (baseline: {GpuCore} / 現在: {result.GpuCore})");
            }

            if (string.Equals(MaliocVersion, result.MaliocVersion, StringComparison.Ordinal) == false)
            {
                diffs.Add($"malioc バージョン (baseline: {FirstLine(MaliocVersion)} / 現在: {FirstLine(result.MaliocVersion)})");
            }

            if (string.Equals(UnityVersion, result.UnityVersion, StringComparison.Ordinal) == false)
            {
                diffs.Add($"Unity バージョン (baseline: {UnityVersion} / 現在: {result.UnityVersion})");
            }

            return string.Join(", ", diffs);
        }

        private static string FirstLine(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var index = text.IndexOf('\n');
            return index < 0 ? text : text.Substring(0, index);
        }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }

        public static ShaderPerfBaseline FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException("ベースライン JSON が空です。", nameof(json));
            }

            return JsonConvert.DeserializeObject<ShaderPerfBaseline>(json)
                   ?? throw new JsonException("ベースライン JSON のデシリアライズに失敗しました。");
        }

        /// <summary>
        /// ファイルから読み込む。ファイルが無ければ null を返す（= ベースライン未作成）。
        /// </summary>
        public static ShaderPerfBaseline? LoadFromFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || File.Exists(path) == false)
            {
                return null;
            }

            return FromJson(File.ReadAllText(path));
        }

        /// <summary>
        /// ファイルへ保存する。親ディレクトリが無ければ作成する。
        /// </summary>
        public void SaveToFile(string path)
        {
            var directory = Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(directory) == false)
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, ToJson());
        }
    }

    /// <summary>
    /// ベースライン 1 エントリ = 1 シェーダーパス・1 シェーダータイプのメトリクス。
    /// </summary>
    internal sealed class ShaderPerfBaselineEntry
    {
        public string ShaderAssetPath { get; set; } = string.Empty;
        public int SubShaderIndex { get; set; }
        public int PassIndex { get; set; }
        public string ShaderTypeName { get; set; } = string.Empty;
        public ShaderPerfMetrics Metrics { get; set; } = new();

        /// <summary>
        /// シェーダーパス・タイプを一意に識別するキー（シェーダー単位）。
        /// キーワードは識別キーに含めない（回帰判定はバリアントに依存しないシェーダー単位で行う）。
        /// </summary>
        [JsonIgnore]
        public string EntryKey => BuildKey(ShaderAssetPath, SubShaderIndex, PassIndex, ShaderTypeName);

        public static string BuildKey(string shaderAssetPath, int subShaderIndex, int passIndex, string shaderTypeName)
        {
            return $"{shaderAssetPath}|{subShaderIndex}|{passIndex}|{shaderTypeName}";
        }
    }
}
