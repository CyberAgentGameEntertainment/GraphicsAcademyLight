#nullable enable
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Sirius.DevSupport.ShaderPerformanceAnalysis
{
    /// <summary>
    /// Unity Editor 上で <see cref="Shader"/> の各パスを Vulkan / SPIR-V にコンパイルする。
    /// malioc は SPIR-V バイナリを入力に取るため、解析の前段としてここで SPIR-V を生成する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// plan.md は <c>ShaderData.Pass.CompileVariant(..., forExternalTool)</c> を internal と想定し
    /// <c>dynamic</c> 経由の実行時バインドを提案していたが、参考実装（vision-client）は内部 API ブリッジを持たない
    /// asmdef から型付きの直接呼び出しで動作している。本実装も型付き直接呼び出しを採用し、コンパイルで検証する。
    /// </para>
    /// </remarks>
    internal static class SpirvVariantCompiler
    {
        /// <summary>
        /// シェーダーが持つ (SubShader, Pass) を列挙する。
        /// </summary>
        public static IReadOnlyList<ShaderPassDescriptor> EnumeratePasses(Shader shader)
        {
            var result = new List<ShaderPassDescriptor>();
            if (shader == null)
            {
                return result;
            }

            var shaderData = ShaderUtil.GetShaderData(shader);
            if (shaderData == null)
            {
                return result;
            }

            // 作者が記述した（シリアライズされた）SubShader のみを列挙する。
            // 実行時に SRP / インポータが注入する SubShader（UsePass 参照のフォールバック等）は自前のスニペットを持たず、
            // CompileVariant や SourceCode 取得がネイティブ経路で "Snippet not found" /
            // "Trying to access shader snippet ..."（C# の logHandler では抑止不可）を出力するだけで解析もできない。
            // シリアライズ SubShader には注入分が含まれないため、これらに触れずに済む。
            // 作者が書いた DepthOnly / DepthNormals 等の実パス（例: URP/Lit）は通常どおり解析対象に含まれる。
            for (var subShaderIndex = 0; subShaderIndex < shaderData.SerializedSubshaderCount; subShaderIndex++)
            {
                var subShader = shaderData.GetSerializedSubshader(subShaderIndex);
                if (subShader == null)
                {
                    continue;
                }

                for (var passIndex = 0; passIndex < subShader.PassCount; passIndex++)
                {
                    var pass = subShader.GetPass(passIndex);
                    if (pass == null)
                    {
                        continue;
                    }

                    var passName = string.IsNullOrEmpty(pass.Name) ? $"Pass {passIndex}" : pass.Name;
                    result.Add(new ShaderPassDescriptor(subShaderIndex, passIndex, passName));
                }
            }

            return result;
        }

        /// <summary>
        /// 指定パス・シェーダータイプのシェーダーバリアントを SPIR-V にコンパイルする。
        /// </summary>
        /// <returns>
        /// SPIR-V を取得できれば true。指定ステージがパスに存在しない / コンパイル失敗の場合は false を返し、
        /// <paramref name="errorMessage"/> に原因を格納する。<paramref name="stageMissing"/> はステージ不在（エラーではない）を示す。
        /// </returns>
        public static bool TryCompile(
            Shader shader,
            ShaderPassDescriptor descriptor,
            ShaderType shaderType,
            string[] keywords,
            out byte[] spirv,
            out string errorMessage,
            out bool stageMissing)
        {
            spirv = Array.Empty<byte>();
            errorMessage = string.Empty;
            stageMissing = false;

            try
            {
                var shaderData = ShaderUtil.GetShaderData(shader);
                if (shaderData == null)
                {
                    errorMessage = "ShaderUtil.GetShaderData が null を返しました。";
                    return false;
                }

                var subShader = shaderData.GetSubshader(descriptor.SubShaderIndex);
                var pass = subShader?.GetPass(descriptor.PassIndex);
                if (pass == null)
                {
                    errorMessage = $"SubShader {descriptor.SubShaderIndex} / Pass {descriptor.PassIndex} を取得できませんでした。";
                    return false;
                }

                var compileInfo = TryCompileWithStrategies(pass, shaderType, keywords ?? Array.Empty<string>());
                if (compileInfo.ShaderData != null && compileInfo.ShaderData.Length > 0)
                {
                    spirv = compileInfo.ShaderData;
                    return true;
                }

                // データが空 = そのステージがパスに存在しない可能性が高い（例: fragment を持たない shadow caster pass）。
                // 明確なコンパイルエラーメッセージがある場合のみエラーとして扱う。
                var messageText = FormatMessages(compileInfo.Messages);
                if (string.IsNullOrWhiteSpace(messageText))
                {
                    stageMissing = true;
                    errorMessage = $"{shaderType} ステージの SPIR-V が生成されませんでした（ステージ不在の可能性）。";
                    return false;
                }

                errorMessage = $"SPIR-V コンパイルに失敗しました:\n{messageText}";
                return false;
            }
            catch (Exception exception)
            {
                errorMessage = $"SPIR-V コンパイル中に例外が発生しました: {exception}";
                return false;
            }
        }

        /// <summary>
        /// 複数のコンパイル戦略を順に試し、SPIR-V データが得られた時点で返す。
        /// "top-level params outside of cbuffers" 等のプラットフォームキーワード由来エラーを回避するため、
        /// 参考実装と同じく段階的にオプションを変えて試行する。
        /// </summary>
        private static ShaderData.VariantCompileInfo TryCompileWithStrategies(
            ShaderData.Pass pass,
            ShaderType shaderType,
            string[] keywords)
        {
            // 1. forExternalTool: true（標準）
            var compileInfo = pass.CompileVariant(
                shaderType, keywords, ShaderCompilerPlatform.Vulkan, BuildTarget.Android, forExternalTool: true);
            if (HasData(compileInfo))
            {
                return compileInfo;
            }

            // 2. forExternalTool: false
            compileInfo = pass.CompileVariant(
                shaderType, keywords, ShaderCompilerPlatform.Vulkan, BuildTarget.Android, forExternalTool: false);
            if (HasData(compileInfo))
            {
                return compileInfo;
            }

            // 3. 空の BuiltinShaderDefine[] でプラットフォームキーワードを除外
            compileInfo = pass.CompileVariant(
                shaderType, keywords, ShaderCompilerPlatform.Vulkan, BuildTarget.Android,
                Array.Empty<BuiltinShaderDefine>(), forExternalTool: true);
            if (HasData(compileInfo))
            {
                return compileInfo;
            }

            // 4. GraphicsTier.Tier1 を明示
            compileInfo = pass.CompileVariant(
                shaderType, keywords, ShaderCompilerPlatform.Vulkan, BuildTarget.Android,
                GraphicsTier.Tier1, forExternalTool: true);
            return compileInfo;
        }

        private static bool HasData(ShaderData.VariantCompileInfo compileInfo)
        {
            return compileInfo.Success || (compileInfo.ShaderData != null && compileInfo.ShaderData.Length > 0);
        }

        private static string FormatMessages(ShaderMessage[]? messages)
        {
            if (messages == null || messages.Length == 0)
            {
                return string.Empty;
            }

            var lines = new List<string>();
            foreach (var message in messages)
            {
                if (message.severity == ShaderCompilerMessageSeverity.Error)
                {
                    lines.Add($"{message.message} {message.messageDetails}".Trim());
                }
            }

            return string.Join("\n", lines);
        }
    }

    /// <summary>
    /// シェーダー内の 1 パスを指す記述子。
    /// </summary>
    internal readonly struct ShaderPassDescriptor
    {
        public ShaderPassDescriptor(int subShaderIndex, int passIndex, string passName)
        {
            SubShaderIndex = subShaderIndex;
            PassIndex = passIndex;
            PassName = passName;
        }

        public int SubShaderIndex { get; }
        public int PassIndex { get; }
        public string PassName { get; }
    }
}
