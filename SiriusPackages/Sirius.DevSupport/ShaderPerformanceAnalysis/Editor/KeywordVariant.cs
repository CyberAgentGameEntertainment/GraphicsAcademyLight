#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Sirius.DevSupport.ShaderPerformanceAnalysis
{
    /// <summary>
    /// 解析対象とするシェーダーキーワード集合（バリアント）を一意に識別する値型。
    /// キーワード配列を正規化（trim → 空除去 → Ordinal 重複除去 → Ordinal ソート）して保持し、
    /// 順序・重複に依存しない一意キー（<see cref="CanonicalKey"/>）と表示ラベルを提供する。
    /// 空集合は「キーワードなし」を表す。
    /// </summary>
    internal readonly struct KeywordVariant : IEquatable<KeywordVariant>
    {
        private readonly string[]? _keywords;

        public KeywordVariant(IEnumerable<string>? keywords)
        {
            _keywords = Normalize(keywords);
        }

        /// <summary>キーワードなし。</summary>
        public static KeywordVariant Default => new(null);

        /// <summary>正規化済みキーワード列（Ordinal ソート・重複なし・空文字なし）。</summary>
        public IReadOnlyList<string> Keywords => _keywords ?? Array.Empty<string>();

        /// <summary>キーワードを 1 つも持たないキーワードなしか。</summary>
        public bool IsDefault => Keywords.Count == 0;

        /// <summary>
        /// 識別キーに使う一意文字列。空集合は空文字列（識別キーに接尾辞を付けないため）。
        /// </summary>
        public string CanonicalKey => string.Join(",", Keywords);

        /// <summary>人可読の表示ラベル。空集合は「（キーワードなし）」。</summary>
        public string DisplayLabel => IsDefault ? "（キーワードなし）" : string.Join(" ", Keywords);

        /// <summary>SPIR-V / malioc コンパイラへ渡すキーワード配列（毎回新規配列を返す）。</summary>
        public string[] ToArray()
        {
            return Keywords.ToArray();
        }

        private static string[] Normalize(IEnumerable<string>? keywords)
        {
            if (keywords == null)
            {
                return Array.Empty<string>();
            }

            return keywords
                .Where(keyword => string.IsNullOrWhiteSpace(keyword) == false)
                .Select(keyword => keyword.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(keyword => keyword, StringComparer.Ordinal)
                .ToArray();
        }

        public bool Equals(KeywordVariant other)
        {
            return string.Equals(CanonicalKey, other.CanonicalKey, StringComparison.Ordinal);
        }

        public override bool Equals(object? obj)
        {
            return obj is KeywordVariant other && Equals(other);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(CanonicalKey);
        }

        public override string ToString()
        {
            return DisplayLabel;
        }
    }
}
