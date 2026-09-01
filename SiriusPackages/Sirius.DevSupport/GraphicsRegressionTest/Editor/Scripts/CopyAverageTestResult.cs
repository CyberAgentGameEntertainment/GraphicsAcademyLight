using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Sirius.DevSupport
{
    public static class CopyAverageTestResult
    {
        [MenuItem("Tools/Sirius/Dev Support/Copy AverageTest Result")]
        private static void CopyResult()
        {
            var settings = GraphicsRegressionTestSettings.instance;
            string[] platforms = { @"WindowsEditor/Direct3D11", @"WindowsEditor/Direct3D12", "WindowsEditor/Vulkan", @"OSXEditor_AppleSilicon/Metal" };
            foreach (var platform in platforms)
            {
                // コピー元とコピー先のパスを定義
                var sourceRoot = $"{settings.ActualImagesPath}/{QualitySettings.activeColorSpace}/{platform}/None";
                var destinationRoot = $"{settings.SuccessfulImagesPath}/{QualitySettings.activeColorSpace}/{platform}/None";

                if (!Directory.Exists(sourceRoot)) continue;
                Directory.CreateDirectory(destinationRoot);

                // ビルドターゲット別サブフォルダ (WebGL 等) も含めて再帰的にコピー
                var files = Directory.EnumerateFiles(sourceRoot, "*.png", SearchOption.AllDirectories)
                    .Where(file => !file.EndsWith(".diff.png", StringComparison.OrdinalIgnoreCase)
                                   && !file.EndsWith(".expected.png", StringComparison.OrdinalIgnoreCase));

                foreach (var file in files)
                {
                    // ファイル名を取得
                    var fileName = Path.GetFileName(file);
                    if (fileName == "TestOptimizedShader.png")
                        // 最適化シェーダーの成功イメージはコピーしない
                        continue;

                    // コピー元の相対パスを保持してコピー先パスを構築（サブフォルダ構造を維持）
                    var relativePath = Path.GetRelativePath(sourceRoot, file);
                    var destFile = Path.Combine(destinationRoot, relativePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(destFile));

                    // ファイルをコピー
                    File.Copy(file, destFile, true);
                }
            }

            AssetDatabase.Refresh();
        }
    }
}
