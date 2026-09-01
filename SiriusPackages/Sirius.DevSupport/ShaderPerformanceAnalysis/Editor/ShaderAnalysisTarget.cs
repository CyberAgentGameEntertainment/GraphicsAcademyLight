#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Sirius.DevSupport.ShaderPerformanceAnalysis
{
    /// <summary>
    /// 解析対象を表す記述子。1 件 = 1 シェーダーアセット + そのシェーダーをコンパイルする際のキーワード集合。
    /// 1 シェーダーにつき 1 集合（キーワードなしが既定）。回帰判定はキーワードに依存せずシェーダー単位で行う。
    /// </summary>
    internal sealed class ShaderAnalysisTarget
    {
        public ShaderAnalysisTarget(string shaderAssetPath, KeywordVariant keywords = default)
        {
            ShaderAssetPath = shaderAssetPath ?? string.Empty;
            Keywords = keywords;
        }

        /// <summary>解析対象のシェーダーアセットパス（プロジェクトルート相対 "Assets/..." 等）。</summary>
        public string ShaderAssetPath { get; }

        /// <summary>このシェーダーをコンパイルするキーワード集合（正規化済み。空＝キーワードなし）。</summary>
        public KeywordVariant Keywords { get; }

        /// <summary>
        /// 重複を除いた対象リストを作る。各対象はキーワードなしで解析される。
        /// 空文字やシェーダー以外の拡張子は呼び出し側で除外しておくこと。
        /// </summary>
        public static List<ShaderAnalysisTarget> FromAssetPaths(IEnumerable<string> assetPaths)
        {
            return assetPaths
                .Where(path => string.IsNullOrWhiteSpace(path) == false)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(path => new ShaderAnalysisTarget(path))
                .ToList();
        }

        /// <summary>
        /// 複数の対象記述子をアセットパスで束ねる（1 シェーダー = 1 件）。
        /// 同一パスが複数あれば、キーワードを持つ指定を優先する（後勝ち）。
        /// 別経路（request の生パス + TargetSet の宣言）から来た対象を統合するのに使う。
        /// </summary>
        public static List<ShaderAnalysisTarget> Merge(IEnumerable<ShaderAnalysisTarget> targets)
        {
            var keywordsByPath = new Dictionary<string, KeywordVariant>(StringComparer.Ordinal);
            foreach (var target in targets)
            {
                if (target == null || string.IsNullOrWhiteSpace(target.ShaderAssetPath))
                {
                    continue;
                }

                // キーワードなしの既定値は、既存のキーワード指定を上書きしない。非空指定は後勝ちで採用する。
                if (keywordsByPath.TryGetValue(target.ShaderAssetPath, out var existing)
                    && existing.IsDefault == false
                    && target.Keywords.IsDefault)
                {
                    continue;
                }

                keywordsByPath[target.ShaderAssetPath] = target.Keywords;
            }

            return keywordsByPath
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new ShaderAnalysisTarget(pair.Key, pair.Value))
                .ToList();
        }
    }
}
