#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Sirius.DevSupport.ShaderPerformanceAnalysis
{
    /// <summary>
    /// malioc の <c>--format json</c> 出力（performance schema）を <see cref="ShaderPerfMetrics"/> にパースする。
    /// performance schema 以外 / 空 / 構造不正は <see cref="TryParse"/> が false を返し、原因を errorMessage に格納する。
    /// </summary>
    internal static class MaliResultParser
    {
        /// <summary>
        /// malioc JSON 文字列をパースしてメトリクスを取り出す。
        /// </summary>
        public static bool TryParse(string maliocJson, out ShaderPerfMetrics metrics, out string errorMessage)
        {
            metrics = new ShaderPerfMetrics();
            errorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(maliocJson))
            {
                errorMessage = "malioc の出力が空です。";
                return false;
            }

            JObject root;
            try
            {
                root = JObject.Parse(maliocJson);
            }
            catch (JsonException exception)
            {
                errorMessage = $"malioc JSON のパースに失敗しました: {exception.Message}";
                return false;
            }

            if (IsPerformanceSchema(root) == false)
            {
                errorMessage = "malioc の出力が performance schema ではありません（解析対象外の出力）。";
                return false;
            }

            if (TryGetPrimaryVariant(root, out var variant) == false)
            {
                errorMessage = "malioc 出力から shader/variant 情報を取得できませんでした。";
                return false;
            }

            var variantProperties = BuildPropertyMap(variant["properties"] as JArray);
            metrics.WorkRegisters = GetNumberProperty(variantProperties, "work_registers_used");
            metrics.UniformRegisters = GetNumberProperty(variantProperties, "uniform_registers_used");
            metrics.ThreadOccupancy = GetNumberProperty(variantProperties, "thread_occupancy");
            metrics.Fp16ArithPercentage = GetNumberProperty(variantProperties, "fp16_arithmetic");

            var performance = variant["performance"] as JObject;
            var pipelineNames = (performance?["pipelines"] as JArray)?
                .Select(token => token?.Value<string>() ?? string.Empty)
                .Where(name => string.IsNullOrWhiteSpace(name) == false)
                .ToList() ?? new List<string>();

            var totalCycles = performance?["total_cycles"] as JObject;
            metrics.TotalCyclesByPipeline = BuildCycleMap(totalCycles, pipelineNames);
            metrics.BoundPipelines = ExtractBoundPipelines(totalCycles);

            if (metrics.IsEmpty())
            {
                errorMessage = "malioc 出力から有効なメトリクスを抽出できませんでした。";
                return false;
            }

            return true;
        }

        private static bool IsPerformanceSchema(JObject root)
        {
            // malioc は出力先頭付近に { "schema": { "name": "performance", ... } } を含む。
            var schemaName = (root["schema"] as JObject)?["name"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(schemaName) == false)
            {
                return string.Equals(schemaName, "performance", StringComparison.OrdinalIgnoreCase);
            }

            // schema フィールドが無い古い形式でも、shaders[].variants[].performance があれば performance とみなす。
            return TryGetPrimaryVariant(root, out var variant) && variant["performance"] is JObject;
        }

        private static bool TryGetPrimaryVariant(JObject root, out JObject variant)
        {
            variant = null!;
            if (root["shaders"] is not JArray shaders || shaders.Count == 0 || shaders[0] is not JObject shaderObject)
            {
                return false;
            }

            if (shaderObject["variants"] is not JArray variants || variants.Count == 0 || variants[0] is not JObject variantObject)
            {
                return false;
            }

            variant = variantObject;
            return true;
        }

        private static Dictionary<string, JToken?> BuildPropertyMap(JArray? properties)
        {
            var map = new Dictionary<string, JToken?>(StringComparer.Ordinal);
            if (properties == null)
            {
                return map;
            }

            foreach (var token in properties)
            {
                if (token is not JObject item)
                {
                    continue;
                }

                var name = item["name"]?.Value<string>() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(name) == false)
                {
                    map[name] = item["value"];
                }
            }

            return map;
        }

        private static Dictionary<string, double> BuildCycleMap(JObject? cycleData, IReadOnlyList<string> pipelineNames)
        {
            var result = new Dictionary<string, double>(StringComparer.Ordinal);
            var cycleCounts = cycleData?["cycle_count"] as JArray;
            if (cycleCounts == null)
            {
                return result;
            }

            for (var index = 0; index < pipelineNames.Count && index < cycleCounts.Count; index++)
            {
                var pipelineName = pipelineNames[index];
                if (string.IsNullOrWhiteSpace(pipelineName))
                {
                    continue;
                }

                var value = GetNumberValue(cycleCounts[index]);
                if (value.HasValue)
                {
                    result[pipelineName] = value.Value;
                }
            }

            return result;
        }

        private static List<string> ExtractBoundPipelines(JObject? cycleData)
        {
            var boundPipelines = cycleData?["bound_pipelines"] as JArray;
            if (boundPipelines == null)
            {
                return new List<string>();
            }

            return boundPipelines
                .Select(token => token?.Value<string>() ?? string.Empty)
                .Where(name => string.IsNullOrWhiteSpace(name) == false)
                .ToList();
        }

        private static double? GetNumberProperty(Dictionary<string, JToken?> properties, string key)
        {
            return properties.TryGetValue(key, out var token) ? GetNumberValue(token) : null;
        }

        private static double? GetNumberValue(JToken? token)
        {
            if (token == null || token.Type is JTokenType.Null or JTokenType.Undefined)
            {
                return null;
            }

            if (token.Type is JTokenType.Integer or JTokenType.Float)
            {
                return token.Value<double>();
            }

            if (token.Type == JTokenType.String && double.TryParse(token.Value<string>(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }

            return null;
        }
    }
}
