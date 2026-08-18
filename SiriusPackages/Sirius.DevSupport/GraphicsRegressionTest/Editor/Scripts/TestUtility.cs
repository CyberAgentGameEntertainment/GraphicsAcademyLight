// --------------------------------------------------------------
// Copyright 2025 CyberAgent, Inc.
// --------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.SceneManagement;
#if HAS_URP
using UnityEngine.Rendering.Universal;
#endif
using Object = UnityEngine.Object;

namespace Sirius.DevSupport
{
    public static class TestUtility
    {
        private static Type _gameViewType;

        public static IEnumerator LoadSceneAndStabilizeRendering(string scenePath, int addRenderCount = 0)
        {
            var asyncOp = EditorSceneManager.LoadSceneAsyncInPlayMode(
                $"{scenePath}.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            // シーンの読み込み待ち
            while (!asyncOp.isDone) yield return null;
            // タイムスケールを0に指定しても、バインドポーズになるときもあれば、
            // 0フレームのアニメーションが再生されてしまうことがあり、テストが不安定だった。
            // そこでシーンに含まれているアニメーターを無効にしてアニメーションが再生されないようにする。
            var animators = Object.FindObjectsByType<Animator>(FindObjectsSortMode.None);
            foreach (var animator in animators) animator.enabled = false;

            // シーンのレンダリングが一回終わるまで待つ
            yield return EndOfFrameOrRenderAll();

            // 一回描画するとシェーダーの非同期コンパイルが走るので、コンパイルが終わるのを待つ
            while (ShaderUtil.anythingCompiling) yield return null;
            // GTAOなどでTemporalフィルタを使っているのでシーンのテストを安定させるために指定回数描画する
            for (var i = 0; i < addRenderCount; i++)
                yield return EndOfFrameOrRenderAll();
        }

        /// <summary>
        ///     シーンを読み込む（Awaitable版）
        /// </summary>
        public static async Awaitable LoadSceneAndStabilizeRenderingAsync(string scenePath, int addRenderCount = 0)
        {
            var asyncOp = EditorSceneManager.LoadSceneAsyncInPlayMode(
                $"{scenePath}.unity",
                new LoadSceneParameters(LoadSceneMode.Single));

            // シーンの読み込み待ち
            await Awaitable.FromAsyncOperation(asyncOp);

            // Animator無効化
            var animators = Object.FindObjectsByType<Animator>(FindObjectsSortMode.None);
            foreach (var animator in animators)
                animator.enabled = false;

            // シーンのレンダリングが一回終わるまで待つ
            await EndOfFrameOrRenderAllAsync();

            // 一回描画するとシェーダーの非同期コンパイルが走るので、コンパイルが終わるのを待つ
            while (ShaderUtil.anythingCompiling)
                await Awaitable.NextFrameAsync();

            // GTAOなどでTemporalフィルタを使っているのでシーンのテストを安定させるために指定回数描画する
            for (var i = 0; i < addRenderCount; i++)
                await EndOfFrameOrRenderAllAsync();
        }

        /// <summary>
        ///     レンダリングを安定化させる（カメラ切り替え後などに使用）
        /// </summary>
        public static IEnumerator StabilizeRendering(int addRenderCount = 0)
        {
            yield return EndOfFrameOrRenderAll();

            while (ShaderUtil.anythingCompiling)
                yield return null;

            for (var i = 0; i < addRenderCount; i++)
                yield return EndOfFrameOrRenderAll();
        }

        /// <summary>
        ///     レンダリングを安定化させる（Awaitable版）
        /// </summary>
        public static async Awaitable StabilizeRenderingAsync(int addRenderCount = 0)
        {
            await EndOfFrameOrRenderAllAsync();

            while (ShaderUtil.anythingCompiling)
                await Awaitable.NextFrameAsync();

            for (var i = 0; i < addRenderCount; i++)
                await EndOfFrameOrRenderAllAsync();
        }

        /// <summary>
        ///     1フレーム待つ
        /// </summary>
        private static object EndOfFrameOrRenderAll()
        {
            // バッチモードでGameViewが開いていないと、フレームを待つことができないため
            if (Application.isBatchMode && !IsGameViewOpen())
            {
                RenderAllCameras();
                return null;
            }
            return new WaitForEndOfFrame();
        }

        /// <summary>
        ///     1フレーム待つ（Awaitable版）
        /// </summary>
        private static async Awaitable EndOfFrameOrRenderAllAsync()
        {
            // バッチモードでGameViewが開いていないと、フレームを待つことができないため
            if (Application.isBatchMode && !IsGameViewOpen())
            {
                RenderAllCameras();
                await Awaitable.NextFrameAsync();
                return;
            }
            await Awaitable.EndOfFrameAsync();
        }

        /// <summary>
        ///     全カメラを明示的にレンダリングする
        /// </summary>
        private static void RenderAllCameras()
        {
            foreach (var camera in Camera.allCameras)
            {
#if HAS_URP
                if (camera.TryGetComponent<UniversalAdditionalCameraData>(out var data) && data.renderType != CameraRenderType.Base)
                {
                    continue;
                }
#endif
                camera.Render();
            }
        }

        /// <summary>
        ///     GameView ウィンドウが開かれているかを判定する。
        /// </summary>
        private static bool IsGameViewOpen()
        {
            _gameViewType ??= typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            return _gameViewType != null && Resources.FindObjectsOfTypeAll(_gameViewType).Length > 0;
        }

        /// <summary>
        ///     指定されたカメラの描画結果をキャプチャーします。
        ///     キャプチャーの処理はTest FrameworkのImageAssert.AreEqualの実装を参考にしています。
        /// </summary>
        public static Texture2D CaptureActualImage(List<Camera> cameras, ImageComparisonSettings settings)
        {
            var width = settings.TargetWidth;
            var height = settings.TargetHeight;
            var samples = settings.TargetMSAASamples;
            const TextureFormat format = TextureFormat.ARGB32;
            Texture2D actualImage = null;
            const int dummyRenderedFrameCount = 1;

            var defaultFormat = settings.UseHDR ? SystemInfo.GetGraphicsFormat(DefaultFormat.HDR) : SystemInfo.GetGraphicsFormat(DefaultFormat.LDR);
            var desc = new RenderTextureDescriptor(width, height, defaultFormat, 24)
            {
                msaaSamples = samples
            };
            var rt = RenderTexture.GetTemporary(desc);
            Graphics.SetRenderTarget(rt);
            GL.Clear(true, true, Color.black);
            Graphics.SetRenderTarget(null);

            for (var i = 0; i < dummyRenderedFrameCount + 1; i++) // x frame delay + the last one is the one really tested ( ie 5 frames delay means 6 frames are rendered )
            {
                foreach (var camera in cameras)
                {
                    if (camera == null)
                        continue;
                    camera.targetTexture = rt;
                    camera.Render();
                    camera.targetTexture = null;
                }

                // only proceed the test on the last rendered frame
                if (dummyRenderedFrameCount == i)
                {
                    actualImage = new Texture2D(width, height, format, false);
                    RenderTexture dummy = null;

                    if (settings.UseHDR)
                    {
                        desc.graphicsFormat = SystemInfo.GetGraphicsFormat(DefaultFormat.LDR);
                        dummy = RenderTexture.GetTemporary(desc);
                        Graphics.Blit(rt, dummy);
                        RenderTexture.active = dummy;
                    }
                    else
                    {
                        RenderTexture.active = rt;
                    }

                    actualImage.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    RenderTexture.active = null;

                    if (dummy != null)
                        RenderTexture.ReleaseTemporary(dummy);

                    actualImage.Apply();
                }
            }

            return actualImage;
        }

        public static string StripParametricTestCharacters(string name)
        {
            {
                const string illegal = "\"";
                var found = name.IndexOf(illegal, StringComparison.Ordinal);
                while (found >= 0)
                {
                    name = name.Remove(found, 1);
                    found = name.IndexOf(illegal, StringComparison.Ordinal);
                }
            }
            {
                const string illegal = ",";
                name = name.Replace(illegal, "-");
            }
            {
                const string illegal = "(";
                name = name.Replace(illegal, "_");
            }
            {
                const string illegal = ")";
                name = name.Replace(illegal, "_");
            }
            {
                const string illegal = "/";
                name = name.Replace(illegal, "_");
            }
            {
                const string illegal = "\\";
                name = name.Replace(illegal, "_");
            }
            return name;
        }

        public static string GetExpectedImageRelativePath()
        {
            var expectedFile = TestContext.CurrentTestExecutionContext.CurrentTest.Name
                .Replace('(', '_')
                .Replace(')', '_')
                .Replace(',', '-')
                .Replace("\"", "")
                .Replace("/", "_");

            var dirName = Path.Combine(GraphicsRegressionTestSettings.instance.SuccessfulImagesPath, GetCurrentTestResultsFolderPath());
            return Path.Combine(
                dirName,
                $"{expectedFile}.png");
        }

        public static string GetCurrentTestResultsFolderPath()
        {
            var colorSpace = QualitySettings.activeColorSpace;
            var runtimePlatform = Application.platform;
            var graphicsApi = SystemInfo.graphicsDeviceType;
            const string xrsdk = "None";

            var path = $"{colorSpace}/{runtimePlatform.ToUniqueString(TestPlatform.GetCurrent().Arch)}/{graphicsApi}/{xrsdk}";

            // WebGL ビルドターゲット時は専用サブフォルダを使う（既存 Android/iOS のリファレンスとは別管理）
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.WebGL)
            {
                path = $"{path}/WebGL";
            }

            return path;
        }

        public static Texture2D LoadImage(string filePath)
        {
            Texture2D expected = null;
            if (File.Exists(filePath))
            {
                var bytes = File.ReadAllBytes(Path.GetFullPath(filePath));
                expected = new Texture2D(Screen.width, Screen.height);
                expected.LoadImage(bytes);
            }
            else
            {
                // ダミーのテクスチャを作成
                expected = new Texture2D(Screen.width, Screen.height);
                for (var x = 0; x < Screen.width; x++)
                for (var y = 0; y < Screen.height; y++)
                    expected.SetPixel(x, y, Color.black);
            }

            return expected;
        }
    }
}
