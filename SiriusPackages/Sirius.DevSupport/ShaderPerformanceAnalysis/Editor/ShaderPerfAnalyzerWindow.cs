#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace Sirius.DevSupport.ShaderPerformanceAnalysis
{
    /// <summary>
    /// シェーダーパフォーマンス解析・回帰検出の Editor バッチ UI。
    /// 対象選択（git 変更 / 手動）、解析実行、回帰・良化サマリー表示、ベースライン更新を提供する。
    /// </summary>
    internal sealed class ShaderPerfAnalyzerWindow : EditorWindow
    {
        private ShaderPerfTargetSet? _targetSet;
        private string _shaderNameFilter = string.Empty;
        private string _baselinePath = string.Empty;
        private string _reportDirectory = string.Empty;

        private string[] _gpuCoreChoices = Array.Empty<string>();
        private Vector2 _scroll;
        private ShaderAnalysisResult? _lastResult;
        private RegressionReport? _lastReport;
        private string _lastMarkdownPath = string.Empty;
        private string _lastJsonPath = string.Empty;
        private string _statusMessage = string.Empty;
        private readonly Dictionary<string, bool> _detailFoldouts = new();

        [MenuItem("Tools/Sirius/Dev Support/Shader Performance Analyzer")]
        private static void Open()
        {
            var window = GetWindow<ShaderPerfAnalyzerWindow>();
            window.titleContent = new GUIContent("Shader Perf");
            window.minSize = new Vector2(520f, 480f);
            window.Show();
        }

        private void OnEnable()
        {
            LoadState();
        }

        private void OnDisable()
        {
            // ウィンドウを閉じたとき / ドメインリロード前に保存する。EditorPrefs はディスク永続なので
            // 再オープン・Unity 再起動を越えて入力状態が残る。
            SaveState();
        }

        private void OnGUI()
        {
            var settings = MaliSettings.instance;
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawSettingsSection(settings);
            EditorGUILayout.Space();
            DrawTargetSection();
            EditorGUILayout.Space();
            DrawBaselineSection();
            EditorGUILayout.Space();
            DrawReportSection();
            EditorGUILayout.Space();
            DrawRunSection(settings);
            EditorGUILayout.Space();
            DrawResultSection();

            EditorGUILayout.EndScrollView();
        }

        private void DrawSettingsSection(MaliSettings settings)
        {
            EditorGUILayout.LabelField("設定", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("malioc パス");
                var path = EditorGUILayout.TextField(settings.MaliocPath);
                if (path != settings.MaliocPath)
                {
                    settings.MaliocPath = path;
                }

                var detectButton = new GUIContent(
                    "PATH から検出",
                    "環境変数 PATH と既知のインストール先（~/.local/bin, /usr/local/bin 等）から malioc を探します。ディスク全体は検索しません。");
                if (GUILayout.Button(detectButton, GUILayout.Width(120f)))
                {
                    if (settings.TryResolveMaliocPath(out var resolved, out var error))
                    {
                        _statusMessage = $"malioc を検出しました: {resolved}";
                    }
                    else
                    {
                        _statusMessage = error;
                    }

                    // 編集中のテキストフィールドはフォーカス中だと外部からの値変更が反映されないため、
                    // フォーカスを外して再描画し、検出したパスを確実に表示させる。
                    GUIUtility.keyboardControl = 0;
                    Repaint();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                // malioc は再配布できない（Arm EULA）。各自が Arm Performance Studio をインストールする導線のみ提供する。
                EditorGUILayout.LabelField(
                    "malioc は Arm Performance Studio に同梱。未導入なら入手してインストールしてください。",
                    EditorStyles.miniLabel);
                if (GUILayout.Button("入手", GUILayout.Width(120f)))
                {
                    Application.OpenURL(MaliSettings.MaliocDownloadUrl);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (_gpuCoreChoices.Length > 0)
                {
                    var currentIndex = Mathf.Max(0, Array.IndexOf(_gpuCoreChoices, settings.GpuCore));
                    var selected = EditorGUILayout.Popup("GPU コア", currentIndex, _gpuCoreChoices);
                    settings.GpuCore = _gpuCoreChoices[Mathf.Clamp(selected, 0, _gpuCoreChoices.Length - 1)];
                }
                else
                {
                    var core = EditorGUILayout.TextField("GPU コア", settings.GpuCore);
                    if (core != settings.GpuCore)
                    {
                        settings.GpuCore = core;
                    }
                }

                if (GUILayout.Button("コア一覧取得", GUILayout.Width(110f)))
                {
                    FetchGpuCores(settings);
                }
            }

            settings.ProcessTimeoutMs = EditorGUILayout.IntField("タイムアウト(ms)", settings.ProcessTimeoutMs);

            // 内部は比率（0.02 = 2%）で保持するが、UI は分かりやすさ優先で百分率（%）で見せる。
            var tolerancePercent = EditorGUILayout.Slider("許容悪化率 (%)", settings.RegressionToleranceRatio * 100f, 0f, 50f);
            settings.RegressionToleranceRatio = tolerancePercent / 100f;
        }

        private void DrawTargetSection()
        {
            EditorGUILayout.LabelField("解析対象", EditorStyles.boldLabel);

            _targetSet = (ShaderPerfTargetSet)EditorGUILayout.ObjectField(
                "登録セット (TargetSet)", _targetSet, typeof(ShaderPerfTargetSet), false);
            EditorGUILayout.HelpBox(
                "CI / バッチと共有する登録済みシェーダー集合。ここに登録したシェーダーが解析対象になる。" +
                "対象シェーダーの追加・削除は登録セットアセットを Inspector で編集する。",
                MessageType.None);

            _shaderNameFilter = EditorGUILayout.TextField(
                new GUIContent(
                    "ファイル名フィルタ",
                    "シェーダーのファイル名に前方一致するものだけを解析対象にする（大文字小文字は無視）。空欄なら登録シェーダー全てが対象。"),
                _shaderNameFilter);

            if (_targetSet != null)
            {
                EditorGUILayout.LabelField($"登録シェーダー数: {_targetSet.ShaderCount}");
                EditorGUILayout.LabelField($"キーワード指定シェーダー数: {_targetSet.KeywordOverrideShaderCount}");

                if (string.IsNullOrWhiteSpace(_shaderNameFilter) == false)
                {
                    EditorGUILayout.LabelField($"フィルタ一致シェーダー数: {BuildTargets().Count}");
                }
            }
        }

        private void DrawBaselineSection()
        {
            EditorGUILayout.LabelField("ベースライン", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                _baselinePath = EditorGUILayout.TextField("baseline JSON", _baselinePath);
                if (GUILayout.Button("選択", GUILayout.Width(60f)))
                {
                    var picked = EditorUtility.OpenFilePanel("ベースライン JSON を選択", Application.dataPath, "json");
                    if (string.IsNullOrWhiteSpace(picked) == false)
                    {
                        _baselinePath = picked;
                    }
                }
            }
        }

        private void DrawReportSection()
        {
            EditorGUILayout.LabelField("レポート出力", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                _reportDirectory = EditorGUILayout.TextField("出力フォルダ", _reportDirectory);
                if (GUILayout.Button("選択", GUILayout.Width(60f)))
                {
                    var picked = EditorUtility.OpenFolderPanel("レポート出力フォルダを選択", GetDefaultReportDirectory(), string.Empty);
                    if (string.IsNullOrWhiteSpace(picked) == false)
                    {
                        _reportDirectory = picked;
                    }
                }
            }

            EditorGUILayout.LabelField($"空欄なら既定: {GetDefaultReportDirectory()}", EditorStyles.miniLabel);
        }

        private static string GetDefaultReportDirectory()
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Directory.GetCurrentDirectory();
            return Path.Combine(projectRoot, "Library", "ShaderPerfReports");
        }

        private void DrawRunSection(MaliSettings settings)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("解析を実行", GUILayout.Height(28f)))
                {
                    RunAnalysis(settings);
                }

                using (new EditorGUI.DisabledScope(_lastResult == null || _lastResult.HasEnvironmentError))
                {
                    if (GUILayout.Button("現在の結果でベースライン更新", GUILayout.Height(28f)))
                    {
                        UpdateBaseline();
                    }
                }
            }

            using (new EditorGUI.DisabledScope(_lastReport == null || _lastReport.HasAnyRegression == false))
            {
                if (GUILayout.Button("悪化したシェーダーのみ再解析", GUILayout.Height(24f)))
                {
                    RunReAnalysis(settings);
                }
            }

            if (string.IsNullOrWhiteSpace(_statusMessage) == false)
            {
                EditorGUILayout.HelpBox(_statusMessage, MessageType.Info);
            }
        }

        private void DrawResultSection()
        {
            if (_lastResult == null)
            {
                return;
            }

            EditorGUILayout.LabelField("結果", EditorStyles.boldLabel);

            if (_lastResult.HasEnvironmentError)
            {
                EditorGUILayout.HelpBox("環境エラー:\n" + string.Join("\n", _lastResult.EnvironmentErrors), MessageType.Error);
                return;
            }

            var successCount = _lastResult.Passes.Count(pass => pass.IsSuccess);
            var failureCount = _lastResult.Passes.Count(pass => pass.IsSuccess == false);
            var regressionCount = _lastReport?.RegressedComparisons.Count() ?? 0;
            var improvementCount = _lastReport?.ImprovedComparisons.Count() ?? 0;

            // サマリーバナー（パフォーマンス悪化・良化の有無を一目で分かるように）
            var summary = $"解析成功 {successCount} / 失敗 {failureCount} / 悪化 {regressionCount} / 良化 {improvementCount}";
            if (regressionCount > 0)
            {
                EditorGUILayout.HelpBox($"⚠ パフォーマンスの悪化を {regressionCount} パスで検出しました。\n{summary}", MessageType.Warning);
            }
            else if (_lastReport != null && _lastReport.HasBaseline)
            {
                EditorGUILayout.HelpBox($"✓ パフォーマンスの悪化は検出されませんでした。\n{summary}", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox($"ベースラインが無いため悪化判定はスキップ（現値のみ記録）。\n{summary}", MessageType.Info);
            }

            // 比較に関する注意（環境不一致など）を画面にも出す。
            if (_lastReport != null)
            {
                foreach (var warning in _lastReport.Warnings)
                {
                    EditorGUILayout.HelpBox(warning, MessageType.Warning);
                }
            }

            if (_lastReport != null)
            {
                foreach (var comparison in _lastReport.RegressedComparisons)
                {
                    foreach (var metric in comparison.Metrics.Where(metric => metric.IsRegression))
                    {
                        EditorGUILayout.HelpBox(
                            $"悪化: {comparison.Current.ShaderName} / パス {comparison.Current.PassName} / {ShaderPerfLabels.ShaderType(comparison.Current.ShaderTypeName)}\n" +
                            $"{ShaderPerfLabels.Metric(metric.MetricKey)}: {metric.BaselineValue:0.##} → {metric.CurrentValue:0.##}",
                            MessageType.Warning);
                    }
                }

                // 良化は HelpBox に色付きスタイルが無いため、緑字の richText ラベルで列挙する。
                var improvementLabel = new GUIStyle(EditorStyles.label) { richText = true };
                foreach (var comparison in _lastReport.ImprovedComparisons)
                {
                    foreach (var metric in comparison.Metrics.Where(metric => metric.IsImprovement))
                    {
                        EditorGUILayout.LabelField(
                            HighlightImproved(
                                $"良化: {comparison.Current.ShaderName} / パス {comparison.Current.PassName} / {ShaderPerfLabels.ShaderType(comparison.Current.ShaderTypeName)} / " +
                                $"{ShaderPerfLabels.Metric(metric.MetricKey)}: {metric.BaselineValue:0.##} → {metric.CurrentValue:0.##}"),
                            improvementLabel);
                    }
                }
            }

            foreach (var failure in _lastResult.Passes.Where(pass => pass.IsSuccess == false))
            {
                EditorGUILayout.HelpBox($"解析失敗: {failure.ShaderAssetPath}\n{FirstLine(failure.Error)}", MessageType.None);
            }

            // 全パスのメトリクスを Editor 上でも確認できるようにする（ベースライン無しの初回でも数値が見える）。
            DrawMetricsDetail();

            // 詳細レポートのパスは結果サマリー・悪化項目の後（末尾）に表示する。
            DrawReportPaths();
        }

        /// <summary>
        /// 解析成功パスのメトリクスを、シェーダー毎の foldout でコンパクトに表示する。
        /// </summary>
        private void DrawMetricsDetail()
        {
            var successes = _lastResult!.Passes.Where(pass => pass.IsSuccess).ToList();
            if (successes.Count == 0)
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("メトリクス詳細", EditorStyles.boldLabel);

            // 悪化・良化箇所の索引を作る（シェーダー単位 / パス単位のメトリクスキー）。
            var regressedShaders = new HashSet<string>(StringComparer.Ordinal);
            var improvedShaders = new HashSet<string>(StringComparer.Ordinal);
            var regressedMetricsByPass = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            var improvedMetricsByPass = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            if (_lastReport != null)
            {
                foreach (var comparison in _lastReport.Comparisons)
                {
                    var passKey = BuildPassKey(comparison.Current);
                    foreach (var metric in comparison.Metrics)
                    {
                        if (metric.IsRegression)
                        {
                            regressedShaders.Add(comparison.Current.ShaderAssetPath);
                            AddMetricKey(regressedMetricsByPass, passKey, metric.MetricKey);
                        }
                        else if (metric.IsImprovement)
                        {
                            improvedShaders.Add(comparison.Current.ShaderAssetPath);
                            AddMetricKey(improvedMetricsByPass, passKey, metric.MetricKey);
                        }
                    }
                }
            }

            var richLabel = new GUIStyle(EditorStyles.miniLabel) { richText = true };
            var richBoldLabel = new GUIStyle(EditorStyles.miniBoldLabel) { richText = true };
            var richFoldout = new GUIStyle(EditorStyles.foldout) { richText = true };

            foreach (var group in successes.GroupBy(pass => pass.ShaderAssetPath).OrderBy(group => group.Key, StringComparer.Ordinal))
            {
                var foldoutLabel = HighlightIf(
                    $"{Path.GetFileName(group.Key)} ({group.Count()} パス)",
                    regressedShaders.Contains(group.Key),
                    improvedShaders.Contains(group.Key));

                _detailFoldouts.TryGetValue(group.Key, out var expanded);
                expanded = EditorGUILayout.Foldout(expanded, foldoutLabel, true, richFoldout);
                _detailFoldouts[group.Key] = expanded;
                if (expanded == false)
                {
                    continue;
                }

                EditorGUI.indentLevel++;
                foreach (var pass in group
                             .OrderBy(pass => pass.PassIndex)
                             .ThenBy(pass => pass.ShaderTypeName, StringComparer.Ordinal)
                             .ThenBy(pass => new KeywordVariant(pass.Keywords).CanonicalKey, StringComparer.Ordinal))
                {
                    var metrics = pass.Metrics!;
                    var passKey = BuildPassKey(pass);
                    regressedMetricsByPass.TryGetValue(passKey, out var passRegressed);
                    passRegressed ??= EmptyKeys;
                    improvedMetricsByPass.TryGetValue(passKey, out var passImproved);
                    passImproved ??= EmptyKeys;

                    // 該当メトリクスが悪化なら赤字、良化なら緑字にする。
                    string HighlightByKey(string text, string metricKey) =>
                        HighlightIf(text, passRegressed.Contains(metricKey), passImproved.Contains(metricKey));

                    var cycles = string.Join(
                        " / ",
                        metrics.TotalCyclesByPipeline
                            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                            .Select(pair => HighlightByKey(
                                $"{ShaderPerfLabels.Pipeline(pair.Key)}:{pair.Value:0.##}",
                                ShaderPerfMetrics.CyclesKeyPrefix + pair.Key)));
                    var bound = metrics.BoundPipelines.Count > 0
                        ? string.Join(", ", metrics.BoundPipelines.Select(ShaderPerfLabels.Pipeline))
                        : "-";

                    var variant = new KeywordVariant(pass.Keywords);
                    var variantSuffix = variant.IsDefault ? string.Empty : $" / {variant.DisplayLabel}";
                    var passLabel = $"{pass.PassName} / {ShaderPerfLabels.ShaderType(pass.ShaderTypeName)}{variantSuffix}";
                    EditorGUILayout.LabelField(
                        HighlightIf(passLabel, passRegressed.Count > 0, passImproved.Count > 0), richBoldLabel);
                    EditorGUI.indentLevel++;
                    var registerLine =
                        $"{HighlightByKey($"ワークレジスタ {FormatMetric(metrics.WorkRegisters)}", ShaderPerfMetrics.WorkRegistersKey)} / " +
                        $"{HighlightByKey($"ユニフォームレジスタ {FormatMetric(metrics.UniformRegisters)}", ShaderPerfMetrics.UniformRegistersKey)} / " +
                        $"{HighlightByKey($"スレッド占有率 {FormatMetric(metrics.ThreadOccupancy)}", ShaderPerfMetrics.ThreadOccupancyKey)} / " +
                        $"{HighlightByKey($"FP16演算利用率 {FormatMetric(metrics.Fp16ArithPercentage)}", ShaderPerfMetrics.Fp16ArithPercentageKey)}";
                    EditorGUILayout.LabelField(registerLine, richLabel);
                    EditorGUILayout.LabelField($"サイクル数: {(string.IsNullOrEmpty(cycles) ? "-" : cycles)}", richLabel);
                    EditorGUILayout.LabelField($"ボトルネック: {bound}", richLabel);
                    EditorGUI.indentLevel--;
                }

                EditorGUI.indentLevel--;
            }
        }

        private static readonly HashSet<string> EmptyKeys = new();

        private static string BuildPassKey(ShaderPassAnalysis pass)
        {
            // 回帰判定と同じシェーダー単位キー（キーワードは含めない）。
            return ShaderPerfBaselineEntry.BuildKey(
                pass.ShaderAssetPath, pass.SubShaderIndex, pass.PassIndex, pass.ShaderTypeName);
        }

        private static void AddMetricKey(Dictionary<string, HashSet<string>> metricsByPass, string passKey, string metricKey)
        {
            if (metricsByPass.TryGetValue(passKey, out var keys) == false)
            {
                keys = new HashSet<string>(StringComparer.Ordinal);
                metricsByPass[passKey] = keys;
            }

            keys.Add(metricKey);
        }

        /// <summary>悪化を示す赤字（richText）にする。</summary>
        private static string Highlight(string text)
        {
            return $"<color=#FF6B6B>{text}</color>";
        }

        /// <summary>良化を示す緑字（richText）にする。</summary>
        private static string HighlightImproved(string text)
        {
            return $"<color=#6BCB77>{text}</color>";
        }

        /// <summary>悪化は赤字、良化は緑字にする。同一表示単位に両方が混在する場合は悪化（赤）を優先する。</summary>
        private static string HighlightIf(string text, bool regressed, bool improved)
        {
            if (regressed)
            {
                return Highlight(text);
            }

            return improved ? HighlightImproved(text) : text;
        }

        private static string FormatMetric(double? value)
        {
            return value.HasValue ? value.Value.ToString("0.##") : "-";
        }

        private void DrawReportPaths()
        {
            if (string.IsNullOrEmpty(_lastMarkdownPath))
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("詳細レポート", EditorStyles.boldLabel);
            var lineHeight = EditorGUIUtility.singleLineHeight;
            EditorGUILayout.SelectableLabel($"Markdown: {_lastMarkdownPath}", EditorStyles.textField, GUILayout.Height(lineHeight));
            EditorGUILayout.SelectableLabel($"JSON: {_lastJsonPath}", EditorStyles.textField, GUILayout.Height(lineHeight));

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("出力フォルダを表示"))
                {
                    EditorUtility.RevealInFinder(_lastMarkdownPath);
                }

                if (GUILayout.Button("レポートを開く"))
                {
                    Application.OpenURL(new Uri(_lastMarkdownPath).AbsoluteUri);
                }
            }
        }

        private void FetchGpuCores(MaliSettings settings)
        {
            if (settings.TryResolveMaliocPath(out var maliocPath, out var pathError) == false)
            {
                _statusMessage = pathError;
                return;
            }

            if (MaliProcessRunner.TryGetGpuCores(maliocPath, settings.ProcessTimeoutMs, out var cores, out var error))
            {
                _gpuCoreChoices = cores.ToArray();
                _statusMessage = $"GPU コアを {cores.Count} 件取得しました。";
            }
            else
            {
                _statusMessage = error;
            }
        }

        private void RunAnalysis(MaliSettings settings)
        {
            var targets = BuildTargets();
            if (targets.Count == 0)
            {
                _statusMessage = string.IsNullOrWhiteSpace(_shaderNameFilter)
                    ? "解析対象がありません。登録セット (TargetSet) にシェーダーを登録してください。"
                    : $"ファイル名フィルタ「{_shaderNameFilter.Trim()}」に一致するシェーダーがありません。";
                return;
            }

            try
            {
                _lastResult = ShaderAnalysisRunner.Analyze(
                    targets,
                    settings,
                    CancellationToken.None,
                    (progress, message) => EditorUtility.DisplayProgressBar("シェーダー解析", message, progress));

                ShaderPerfBaseline? baseline = null;
                try
                {
                    baseline = ShaderPerfBaseline.LoadFromFile(_baselinePath);
                }
                catch (Exception exception)
                {
                    _statusMessage = $"ベースライン読み込み失敗（初回扱い）: {exception.Message}";
                }

                _lastReport = RegressionComparer.Compare(_lastResult, baseline, settings.RegressionToleranceRatio);

                var reportDir = string.IsNullOrWhiteSpace(_reportDirectory) ? GetDefaultReportDirectory() : _reportDirectory;
                _lastMarkdownPath = Path.Combine(reportDir, "ShaderPerfReport.md");
                _lastJsonPath = Path.Combine(reportDir, "ShaderPerfReport.json");
                ShaderPerfReporter.WriteReports(_lastResult, _lastReport, _lastMarkdownPath, _lastJsonPath);

                _statusMessage = $"解析完了: 対象 {targets.Count} 件。レポートを出力しました。";
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private void RunReAnalysis(MaliSettings settings)
        {
            if (_lastResult == null || _lastReport == null || _lastReport.HasAnyRegression == false)
            {
                return;
            }

            var regressedPathSet = new HashSet<string>(
                _lastReport.RegressedComparisons.Select(c => c.Current.ShaderAssetPath),
                StringComparer.Ordinal);

            var allTargets = BuildTargets();
            var regressedTargets = allTargets
                .Where(t => regressedPathSet.Contains(t.ShaderAssetPath))
                .ToList();

            // TargetSet が未設定の場合はキーワードなしで対象を構築する。
            if (regressedTargets.Count == 0)
            {
                regressedTargets = ShaderAnalysisTarget.FromAssetPaths(regressedPathSet);
            }

            try
            {
                var reResult = ShaderAnalysisRunner.Analyze(
                    regressedTargets,
                    settings,
                    CancellationToken.None,
                    (progress, message) => EditorUtility.DisplayProgressBar("悪化シェーダー再解析", message, progress));

                if (reResult.HasEnvironmentError)
                {
                    _statusMessage = "再解析で環境エラーが発生しました。初回結果を使用します: " + string.Join(" / ", reResult.EnvironmentErrors);
                    return;
                }

                _lastResult.Passes.RemoveAll(pass => regressedPathSet.Contains(pass.ShaderAssetPath));
                _lastResult.Passes.AddRange(reResult.Passes);
                ShaderPerfBaseline? baseline = null;
                try
                {
                    baseline = ShaderPerfBaseline.LoadFromFile(_baselinePath);
                }
                catch (Exception exception)
                {
                    _statusMessage = $"ベースライン読み込み失敗: {exception.Message}";
                }

                _lastReport = RegressionComparer.Compare(_lastResult, baseline, settings.RegressionToleranceRatio);

                var reportDir = string.IsNullOrWhiteSpace(_reportDirectory) ? GetDefaultReportDirectory() : _reportDirectory;
                _lastMarkdownPath = Path.Combine(reportDir, "ShaderPerfReport.md");
                _lastJsonPath = Path.Combine(reportDir, "ShaderPerfReport.json");
                ShaderPerfReporter.WriteReports(_lastResult, _lastReport, _lastMarkdownPath, _lastJsonPath);

                var remainingRegressions = _lastReport.RegressedComparisons.Count();
                _statusMessage = remainingRegressions > 0
                    ? $"再解析完了: {regressedTargets.Count} 件を再解析。引き続き {remainingRegressions} 件で悪化を検出しました。"
                    : $"再解析完了: {regressedTargets.Count} 件を再解析。悪化は検出されませんでした。";
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private void UpdateBaseline()
        {
            if (_lastResult == null || _lastResult.HasEnvironmentError)
            {
                return;
            }

            var savePath = _baselinePath;
            if (string.IsNullOrWhiteSpace(savePath))
            {
                savePath = EditorUtility.SaveFilePanel("ベースライン JSON を保存", Application.dataPath, "ShaderPerfBaseline", "json");
                if (string.IsNullOrWhiteSpace(savePath))
                {
                    return;
                }

                _baselinePath = savePath;
            }

            ShaderPerfBaseline.FromResult(_lastResult).SaveToFile(savePath);
            _statusMessage = $"ベースラインを保存しました: {savePath}";
        }

        private List<ShaderAnalysisTarget> BuildTargets()
        {
            if (_targetSet == null)
            {
                return new List<ShaderAnalysisTarget>();
            }

            return FilterByFileName(_targetSet.ToTargets(), _shaderNameFilter);
        }

        /// <summary>
        /// シェーダーのファイル名に対する前方一致（大文字小文字を無視）で解析対象を絞る。
        /// フィルタが空欄なら登録シェーダーを全件そのまま返す。
        /// </summary>
        private static List<ShaderAnalysisTarget> FilterByFileName(List<ShaderAnalysisTarget> targets, string? fileNamePrefix)
        {
            var prefix = fileNamePrefix?.Trim();
            if (string.IsNullOrEmpty(prefix))
            {
                return targets;
            }

            return targets
                .Where(target => Path.GetFileName(target.ShaderAssetPath)
                    .StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .ToList();
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

        // --- 入力状態の永続化 ---
        // 登録セットとベースラインはチーム共有したいので MaliSettings（ProjectSettings 配下＝コミット対象）へ保存する。
        // レポート出力先はマシン依存で構わないので EditorPrefs（マシンローカル）に残す。

        // EditorPrefs はマシン横断で共有されるため、プロジェクトパスをキーに含めて他プロジェクトと混ざらないようにする。
        private static string KeyPrefix => $"Sirius.DevSupport.ShaderPerf.Window.{Application.dataPath}.";

        private void LoadState()
        {
            var settings = MaliSettings.instance;
            _baselinePath = settings.BaselinePath;
            _targetSet = settings.TargetSet;

            _reportDirectory = EditorPrefs.GetString(KeyPrefix + "ReportDirectory", string.Empty);
            _shaderNameFilter = EditorPrefs.GetString(KeyPrefix + "ShaderNameFilter", string.Empty);
        }

        private void SaveState()
        {
            var settings = MaliSettings.instance;
            settings.BaselinePath = _baselinePath ?? string.Empty;
            settings.TargetSet = _targetSet;

            EditorPrefs.SetString(KeyPrefix + "ReportDirectory", _reportDirectory ?? string.Empty);
            EditorPrefs.SetString(KeyPrefix + "ShaderNameFilter", _shaderNameFilter ?? string.Empty);
        }
    }
}
