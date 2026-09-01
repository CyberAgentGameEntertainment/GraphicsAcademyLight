#nullable enable
using System;
using System.Collections.Generic;

namespace Sirius.DevSupport.ShaderPerformanceAnalysis
{
    /// <summary>
    /// レポート / UI 表示用に、英略語のシェーダータイプ・パイプライン・メトリクスキーを
    /// 日本語ラベルへ変換する。内部データ（ベースライン照合キー等）は英語のまま保持し、
    /// ここでは表示時の変換のみを担う。
    /// </summary>
    internal static class ShaderPerfLabels
    {
        /// <summary>
        /// シェーダーバリアントの表示ラベル。空集合は「（キーワードなし）」。
        /// 表示文言の単一管理のため <see cref="KeywordVariant.DisplayLabel"/> へ委譲する。
        /// </summary>
        public static string Variant(IReadOnlyList<string> keywords)
        {
            return new KeywordVariant(keywords).DisplayLabel;
        }

        /// <summary>シェーダーステージ（Vertex / Fragment）の日本語表記。</summary>
        public static string ShaderType(string typeName)
        {
            return typeName switch
            {
                "Vertex" => "頂点シェーダー",
                "Fragment" => "フラグメントシェーダー",
                _ => typeName,
            };
        }

        /// <summary>
        /// malioc のパイプライン略号を日本語へ変換する。
        /// 新形式（arith_*）と旧形式（A/LS/V/T）の両方に対応。
        /// </summary>
        public static string Pipeline(string name)
        {
            return name switch
            {
                "arith_total" => "演算(合計)",
                "arith_fma" => "演算(FMA 積和)",
                "arith_cvt" => "演算(変換)",
                "arith_sfu" => "演算(特殊関数)",
                "load_store" => "ロードストア",
                "texture" => "テクスチャ",
                "varying" => "varying(補間)",
                "A" => "演算",
                "LS" => "ロードストア",
                "V" => "varying(補間)",
                "T" => "テクスチャ",
                _ => name,
            };
        }

        /// <summary>
        /// メトリクスキー（<see cref="ShaderPerfMetrics"/> の比較キー）を日本語ラベルへ変換する。
        /// </summary>
        public static string Metric(string metricKey)
        {
            if (string.Equals(metricKey, ShaderPerfMetrics.WorkRegistersKey, StringComparison.Ordinal))
            {
                return "ワークレジスタ使用量";
            }

            if (string.Equals(metricKey, ShaderPerfMetrics.UniformRegistersKey, StringComparison.Ordinal))
            {
                return "ユニフォームレジスタ使用量";
            }

            if (string.Equals(metricKey, ShaderPerfMetrics.ThreadOccupancyKey, StringComparison.Ordinal))
            {
                return "スレッド占有率";
            }

            if (string.Equals(metricKey, ShaderPerfMetrics.Fp16ArithPercentageKey, StringComparison.Ordinal))
            {
                return "FP16 演算利用率";
            }

            if (metricKey.StartsWith(ShaderPerfMetrics.CyclesKeyPrefix, StringComparison.Ordinal))
            {
                var pipeline = metricKey.Substring(ShaderPerfMetrics.CyclesKeyPrefix.Length);
                return $"{Pipeline(pipeline)} サイクル数";
            }

            return metricKey;
        }
    }
}
