#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Sirius.DevSupport.ShaderPerformanceAnalysis
{
    /// <summary>
    /// メトリクスの悪化方向。回帰（regression）判定に用いる。
    /// </summary>
    internal enum MetricDirection
    {
        /// <summary>値が大きいほど悪い（サイクル数・レジスタ使用量など）。</summary>
        HigherIsWorse,

        /// <summary>値が小さいほど悪い（スレッド占有率など）。</summary>
        LowerIsWorse,
    }

    /// <summary>
    /// 1 シェーダーパス・1 シェーダータイプ分の Mali Offline Compiler メトリクス。
    /// malioc の performance schema から抽出した構造化データで、ベースライン JSON にもそのまま直列化する。
    /// </summary>
    internal sealed class ShaderPerfMetrics
    {
        public const string WorkRegistersKey = "work_registers";
        public const string UniformRegistersKey = "uniform_registers";
        public const string ThreadOccupancyKey = "thread_occupancy";
        public const string Fp16ArithPercentageKey = "fp16_arith_percentage";

        /// <summary>パイプライン別サイクル数のメトリクスキー接頭辞（例: "cycles.A"）。</summary>
        public const string CyclesKeyPrefix = "cycles.";

        /// <summary>ワークレジスタ使用量（work_registers_used）。</summary>
        public double? WorkRegisters { get; set; }

        /// <summary>ユニフォームレジスタ使用量（uniform_registers_used）。</summary>
        public double? UniformRegisters { get; set; }

        /// <summary>スレッド占有率（thread_occupancy、%）。大きいほど良い。</summary>
        public double? ThreadOccupancy { get; set; }

        /// <summary>FP16 演算利用率（fp16_arith_percentage、%）。大きいほど GPU スループットが高い。</summary>
        public double? Fp16ArithPercentage { get; set; }

        /// <summary>
        /// パイプライン別の total_cycles。キーは malioc のパイプライン略号（A=演算 / LS=ロードストア / V=バリイング / T=テクスチャ等）。
        /// </summary>
        public Dictionary<string, double> TotalCyclesByPipeline { get; set; } = new(StringComparer.Ordinal);

        /// <summary>ボトルネックとなっているパイプライン略号の一覧（bound_pipelines）。</summary>
        public List<string> BoundPipelines { get; set; } = new();

        /// <summary>
        /// 回帰比較用に、メトリクスをキー → (値, 悪化方向) のフラットな列として列挙する。
        /// パイプライン別サイクルは <see cref="CyclesKeyPrefix"/> + パイプライン略号 をキーにする。
        /// </summary>
        public IEnumerable<KeyValuePair<string, MetricValue>> EnumerateComparableMetrics()
        {
            if (WorkRegisters.HasValue)
            {
                yield return new KeyValuePair<string, MetricValue>(
                    WorkRegistersKey, new MetricValue(WorkRegisters.Value, MetricDirection.HigherIsWorse));
            }

            if (UniformRegisters.HasValue)
            {
                yield return new KeyValuePair<string, MetricValue>(
                    UniformRegistersKey, new MetricValue(UniformRegisters.Value, MetricDirection.HigherIsWorse));
            }

            if (ThreadOccupancy.HasValue)
            {
                yield return new KeyValuePair<string, MetricValue>(
                    ThreadOccupancyKey, new MetricValue(ThreadOccupancy.Value, MetricDirection.LowerIsWorse));
            }

            if (Fp16ArithPercentage.HasValue)
            {
                yield return new KeyValuePair<string, MetricValue>(
                    Fp16ArithPercentageKey, new MetricValue(Fp16ArithPercentage.Value, MetricDirection.LowerIsWorse));
            }

            foreach (var pipeline in TotalCyclesByPipeline.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                yield return new KeyValuePair<string, MetricValue>(
                    CyclesKeyPrefix + pipeline.Key, new MetricValue(pipeline.Value, MetricDirection.HigherIsWorse));
            }
        }

        /// <summary>
        /// メトリクスを 1 件も保持していないか（解析自体は成功したが値が空）を判定する。
        /// </summary>
        public bool IsEmpty()
        {
            return WorkRegisters.HasValue == false
                   && UniformRegisters.HasValue == false
                   && ThreadOccupancy.HasValue == false
                   && TotalCyclesByPipeline.Count == 0;
        }
    }

    /// <summary>
    /// 回帰比較で扱う 1 メトリクスの値と悪化方向の組。
    /// </summary>
    internal readonly struct MetricValue
    {
        public MetricValue(double value, MetricDirection direction)
        {
            Value = value;
            Direction = direction;
        }

        public double Value { get; }
        public MetricDirection Direction { get; }
    }
}
