#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Sirius.DevSupport.ShaderPerformanceAnalysis
{
    /// <summary>
    /// 静的解析の対象として「登録」するシェーダー集合。
    /// CI / バッチ実行と Editor ウィンドウの両方がこのレジストリを参照することで、
    /// git 差分の自動抽出（include 経由の波及を取りこぼす）に頼らず、登録済みシェーダーを毎回フル解析できる。
    /// 利用側プロジェクト（SIRIUS ホスト）にアセットとしてコミットして運用する。
    ///
    /// 各シェーダーは <b>1 件 = 1 キーワード集合</b>で登録する（同じシェーダーを複数の構成で登録はしない）。
    /// キーワードは「そのシェーダーをどの構成でコンパイルして計測するか」の指定で、
    /// 回帰判定はキーワードに依存しないシェーダー単位（アセット/SubShader/Pass/種別）で行う。
    /// </summary>
    [CreateAssetMenu(
        fileName = "ShaderPerfTargetSet",
        menuName = "Sirius/DevSupport/Shader Perf Target Set",
        order = 1000)]
    internal sealed class ShaderPerfTargetSet : ScriptableObject
    {
        [SerializeField]
        [Tooltip("解析対象シェーダーと、それぞれを計測するキーワード集合。1 シェーダーにつき 1 行（1 集合）。")]
        private List<ShaderVariantEntry> _entries = new();

        // --- 旧フィールド（移行専用。インスペクタには表示しない） ---
        // かつての「Shaders リスト + キーワード宣言」を _entries に統合した。既存アセットを壊さないよう保持し、
        // カスタムエディタが初回表示時に _entries へ移行してこれらをクリアする。
        [SerializeField]
        [HideInInspector]
        private List<Shader> _shaders = new();

        [SerializeField]
        [HideInInspector]
        private List<ShaderVariantDeclaration> _variantDeclarations = new();

        public IReadOnlyList<ShaderVariantEntry> Entries => _entries;

        /// <summary>移行が必要な旧データ（_shaders / _variantDeclarations）が残っているか。</summary>
        public bool HasLegacyData =>
            (_shaders != null && _shaders.Count > 0) || (_variantDeclarations != null && _variantDeclarations.Count > 0);

        /// <summary>登録シェーダー数（UI 表示用）。</summary>
        public int ShaderCount => ResolveEntries().Count(entry => entry.Shader != null);

        /// <summary>キーワードを指定したシェーダー数（UI 表示用）。</summary>
        public int KeywordOverrideShaderCount =>
            ResolveEntries().Count(entry => entry.Shader != null && entry.Keywords.Count > 0);

        /// <summary>
        /// 登録シェーダーのアセットパスを返す（null / 空は除外、重複除去）。
        /// </summary>
        public List<string> ToAssetPaths()
        {
            return ResolveEntries()
                .Select(entry => ResolvePath(entry.Shader))
                .Where(path => path != null)
                .Select(path => path!)
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// 解析対象記述子へ変換する。各シェーダーは 1 件（登録したキーワード集合でコンパイル・計測）。
        /// 回帰判定はキーワードに依存しないシェーダー単位で行う。
        /// </summary>
        public List<ShaderAnalysisTarget> ToTargets()
        {
            var keywordsByPath = new Dictionary<string, KeywordVariant>(StringComparer.Ordinal);
            foreach (var entry in ResolveEntries())
            {
                var path = ResolvePath(entry.Shader);
                if (path == null)
                {
                    continue;
                }

                // 同一シェーダーが複数行にある場合は後勝ち（重複はエディタ側で警告・抑止する）。
                keywordsByPath[path] = new KeywordVariant(entry.Keywords);
            }

            return keywordsByPath
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new ShaderAnalysisTarget(pair.Key, pair.Value))
                .ToList();
        }

        /// <summary>
        /// 旧データ（_shaders / _variantDeclarations）を新形式の entries に変換して返す。登録順を保持する。
        /// カスタムエディタの永続移行・実行時フォールバックの両方で使う。
        /// </summary>
        public List<ShaderVariantEntry> BuildEntriesFromLegacy()
        {
            var byShader = new Dictionary<Shader, ShaderVariantEntry>();
            var order = new List<Shader>();

            foreach (var shader in _shaders)
            {
                if (shader == null || byShader.ContainsKey(shader))
                {
                    continue;
                }

                byShader[shader] = new ShaderVariantEntry(shader, new List<string>());
                order.Add(shader);
            }

            foreach (var declaration in _variantDeclarations)
            {
                if (declaration == null)
                {
                    continue;
                }

                foreach (var shader in declaration.Shaders)
                {
                    if (shader == null)
                    {
                        continue;
                    }

                    if (byShader.TryGetValue(shader, out var entry) == false)
                    {
                        entry = new ShaderVariantEntry(shader, new List<string>());
                        byShader[shader] = entry;
                        order.Add(shader);
                    }

                    entry.SetKeywords(declaration.Keywords); // 後勝ち
                }
            }

            return order.Select(shader => byShader[shader]).ToList();
        }

        /// <summary>有効な entries を返す。未移行（_entries 空）なら旧データから組み立てて返す。</summary>
        private IReadOnlyList<ShaderVariantEntry> ResolveEntries()
        {
            if (_entries.Count > 0)
            {
                return _entries;
            }

            return HasLegacyData ? BuildEntriesFromLegacy() : _entries;
        }

        private static string? ResolvePath(Shader? shader)
        {
            if (shader == null)
            {
                return null;
            }

            var path = AssetDatabase.GetAssetPath(shader);
            return string.IsNullOrWhiteSpace(path) ? null : path;
        }
    }

    /// <summary>
    /// 1 シェーダー + そのシェーダーを計測するキーワード集合。1 シェーダー = 1 エントリ。
    /// </summary>
    [Serializable]
    internal sealed class ShaderVariantEntry
    {
        [SerializeField]
        [Tooltip("解析対象シェーダー。")]
        private Shader? _shader;

        [SerializeField]
        [Tooltip("このシェーダーに適用するキーワード（例: _NORMALMAP）。空ならキーワードなし。")]
        private List<string> _keywords = new();

        public ShaderVariantEntry()
        {
        }

        public ShaderVariantEntry(Shader? shader, List<string> keywords)
        {
            _shader = shader;
            _keywords = keywords ?? new List<string>();
        }

        public Shader? Shader => _shader;

        public IReadOnlyList<string> Keywords => _keywords;

        public void SetKeywords(IEnumerable<string> keywords)
        {
            _keywords = keywords?.ToList() ?? new List<string>();
        }
    }

    /// <summary>
    /// 旧形式の宣言（移行専用）。かつて「複数シェーダー + 1 キーワード集合」を表していた。
    /// </summary>
    [Serializable]
    internal sealed class ShaderVariantDeclaration
    {
        [SerializeField]
        private List<Shader> _shaders = new();

        [SerializeField]
        private List<string> _keywords = new();

        public IReadOnlyList<Shader> Shaders => _shaders;

        public IReadOnlyList<string> Keywords => _keywords;
    }
}
