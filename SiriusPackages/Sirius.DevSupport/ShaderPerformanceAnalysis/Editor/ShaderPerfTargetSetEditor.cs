#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Sirius.DevSupport.ShaderPerformanceAnalysis
{
    /// <summary>
    /// <see cref="ShaderPerfTargetSet"/> のカスタムインスペクタ。
    /// 「1 行 = 1 シェーダー + そのキーワード集合」のフラットなリストで編集する（1 シェーダー = 1 集合）。
    /// シェーダーをセットするとそのキーワード（<c>shader.keywordSpace</c>）をチェックボックスで列挙する。
    /// </summary>
    [CustomEditor(typeof(ShaderPerfTargetSet))]
    internal sealed class ShaderPerfTargetSetEditor : Editor
    {
        private const int ToggleColumns = 2;

        // コピー＆ペースト用のキーワード集合（エディタセッション内で共有。null = 未コピー）。
        private static List<string>? s_copiedKeywords;

        private SerializedProperty _entriesProp = null!;
        private SerializedProperty _legacyShadersProp = null!;
        private SerializedProperty _legacyDeclarationsProp = null!;

        // エディタインスタンス内で保持する表示状態。
        private readonly Dictionary<int, string[]> _keywordCache = new();
        private readonly Dictionary<int, string> _filters = new();
        private readonly Dictionary<int, bool> _userKeywordsOnly = new();
        private readonly Dictionary<int, bool> _foldouts = new();

        public override void OnInspectorGUI()
        {
            // target が null の状態で serializedObject に触ると例外になる（解析実行に伴う再インポートで
            // エディタが一時的に null ターゲットで再生成されることがある）。ここでガードする。
            if (target == null)
            {
                return;
            }

            serializedObject.Update();

            // プロパティは OnEnable ではなく毎回ここで取得する（OnEnable での serializedObject アクセスは
            // null ターゲット時に SerializedObjectNotCreatableException を投げるため）。
            _entriesProp = serializedObject.FindProperty("_entries");
            _legacyShadersProp = serializedObject.FindProperty("_shaders");
            _legacyDeclarationsProp = serializedObject.FindProperty("_variantDeclarations");

            MigrateLegacyIfNeeded();

            EditorGUILayout.LabelField("解析対象シェーダー", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "1 行 = 1 シェーダー。各シェーダーに適用するキーワードをチェックボックスで指定します（チェックなし＝キーワードなし）。" +
                "悪化判定はキーワードに依存せずシェーダー単位で行うため、ベースライン（記録時の構成）と比べて" +
                "どれだけ重くなったかが分かります。同じシェーダーは 1 行だけにしてください。",
                MessageType.None);

            DrawClipboardBar();
            DrawEntries();

            EditorGUILayout.Space();
            if (GUILayout.Button("シェーダーを追加"))
            {
                AddEntry();
            }

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>旧フィールド（_shaders / _variantDeclarations）が残っていれば _entries へ一度だけ移行する。</summary>
        private void MigrateLegacyIfNeeded()
        {
            if (_entriesProp.arraySize > 0)
            {
                return;
            }

            if (target is not ShaderPerfTargetSet set || set.HasLegacyData == false)
            {
                return;
            }

            var entries = set.BuildEntriesFromLegacy();
            _entriesProp.ClearArray();
            for (var i = 0; i < entries.Count; i++)
            {
                _entriesProp.arraySize = i + 1;
                var element = _entriesProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("_shader").objectReferenceValue = entries[i].Shader;

                var keywordsProp = element.FindPropertyRelative("_keywords");
                keywordsProp.ClearArray();
                var keywords = entries[i].Keywords;
                for (var k = 0; k < keywords.Count; k++)
                {
                    keywordsProp.arraySize = k + 1;
                    keywordsProp.GetArrayElementAtIndex(k).stringValue = keywords[k];
                }
            }

            _legacyShadersProp.ClearArray();
            _legacyDeclarationsProp.ClearArray();
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(set);
            serializedObject.Update();
        }

        /// <summary>コピー中のキーワード集合の表示と「全シェーダーに適用 / クリア」操作。</summary>
        private void DrawClipboardBar()
        {
            if (s_copiedKeywords == null)
            {
                return;
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(
                    $"コピー中: {new KeywordVariant(s_copiedKeywords).DisplayLabel}", EditorStyles.miniLabel);
                if (GUILayout.Button("全シェーダーに適用", GUILayout.Width(130)))
                {
                    for (var i = 0; i < _entriesProp.arraySize; i++)
                    {
                        var keywordsProp = _entriesProp.GetArrayElementAtIndex(i).FindPropertyRelative("_keywords");
                        SetKeywords(keywordsProp, s_copiedKeywords);
                    }
                }

                if (GUILayout.Button("クリア", GUILayout.Width(50)))
                {
                    s_copiedKeywords = null;
                }
            }
        }

        private void DrawEntries()
        {
            var duplicateShaderIds = FindDuplicateShaderIds(out var firstIndexByShader);
            if (duplicateShaderIds.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    "同じシェーダーが複数行に登録されています。1 シェーダー = 1 行にしてください（解析では一番上の行が使われます）。",
                    MessageType.Error);
            }

            for (var i = 0; i < _entriesProp.arraySize; i++)
            {
                var element = _entriesProp.GetArrayElementAtIndex(i);
                var shaderProp = element.FindPropertyRelative("_shader");
                var keywordsProp = element.FindPropertyRelative("_keywords");

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.PropertyField(shaderProp, new GUIContent($"シェーダー {i}"));
                        if (GUILayout.Button(new GUIContent("削除", "この行（シェーダー）を削除する"), GUILayout.Width(60)))
                        {
                            _entriesProp.DeleteArrayElementAtIndex(i);
                            _keywordCache.Clear();
                            _filters.Clear();
                            _userKeywordsOnly.Clear();
                            _foldouts.Clear();
                            return; // 配列が変わったのでこのフレームは打ち切る。
                        }
                    }

                    if (shaderProp.objectReferenceValue is not Shader shader)
                    {
                        EditorGUILayout.HelpBox("シェーダーを設定してください。", MessageType.Info);
                        continue;
                    }

                    var id = shader.GetInstanceID();
                    if (duplicateShaderIds.Contains(id) && firstIndexByShader[id] != i)
                    {
                        EditorGUILayout.HelpBox(
                            $"このシェーダーは上の行（シェーダー {firstIndexByShader[id]}）と重複しています。この行は解析で無視されます。",
                            MessageType.Warning);
                    }

                    DrawKeywordSelector(i, shader, keywordsProp);
                }
            }
        }

        private void DrawKeywordSelector(int entryIndex, Shader shader, SerializedProperty keywordsProp)
        {
            var current = ReadStringList(keywordsProp);
            var label = new KeywordVariant(current).DisplayLabel;

            if (_foldouts.TryGetValue(entryIndex, out var expanded) == false)
            {
                expanded = false; // 既定は折りたたみ（行数を抑える）。
            }

            // フォールドヘッダ右端に「コピー / ペースト」を置く（キーワード集合に対する操作だと明確にする）。
            var headerRect = EditorGUILayout.GetControlRect();
            const float buttonWidth = 56f;
            const float gap = 4f;
            var foldoutRect = new Rect(
                headerRect.x, headerRect.y, headerRect.width - (buttonWidth * 2) - (gap * 2), headerRect.height);
            var copyRect = new Rect(headerRect.xMax - (buttonWidth * 2) - gap, headerRect.y, buttonWidth, headerRect.height);
            var pasteRect = new Rect(headerRect.xMax - buttonWidth, headerRect.y, buttonWidth, headerRect.height);

            // 「キーワード: <集合>」をフォールドラベルに併記する。Foldout はラベルを rect でクリップせず
            // ボタンの下にはみ出すため、ボタン手前の幅に収まるよう省略（…）してから描画する（全文はツールチップ）。
            var fullLabel = $"キーワード: {label}";
            var clippedLabel = TruncateToWidth(fullLabel, EditorStyles.foldout, foldoutRect.width);
            expanded = EditorGUI.Foldout(
                foldoutRect, expanded, new GUIContent(clippedLabel, fullLabel), true, EditorStyles.foldout);
            if (GUI.Button(copyRect, new GUIContent("コピー", "このキーワード集合をコピーする")))
            {
                s_copiedKeywords = ReadStringList(keywordsProp);
            }

            using (new EditorGUI.DisabledScope(s_copiedKeywords == null))
            {
                if (GUI.Button(pasteRect, new GUIContent("ペースト", "コピーしたキーワード集合をこの行に貼り付ける")))
                {
                    SetKeywords(keywordsProp, s_copiedKeywords!);
                }
            }

            _foldouts[entryIndex] = expanded;
            if (expanded == false)
            {
                return;
            }

            var allKeywords = GetKeywordNames(shader);
            if (allKeywords.Length == 0)
            {
                EditorGUILayout.HelpBox("このシェーダーにはキーワードがありません（キーワードなしで計測されます）。", MessageType.Info);
                return;
            }

            _filters.TryGetValue(entryIndex, out var filter);
            filter ??= string.Empty;
            if (_userKeywordsOnly.TryGetValue(entryIndex, out var userOnly) == false)
            {
                userOnly = true; // 既定はユーザー定義（_ 始まり）キーワードのみ表示。
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                filter = EditorGUILayout.TextField("キーワード絞り込み", filter);
                userOnly = GUILayout.Toggle(userOnly, new GUIContent("_ 始まりのみ", "ビルトイン/グローバルを隠し、ユーザー定義キーワードのみ表示する"), GUILayout.Width(110));
                if (GUILayout.Button("再取得", GUILayout.Width(60)))
                {
                    _keywordCache.Remove(shader.GetInstanceID());
                }
            }

            _filters[entryIndex] = filter;
            _userKeywordsOnly[entryIndex] = userOnly;

            var displayList = FilterKeywords(allKeywords, filter, userOnly);
            // 現在 ON だが絞り込みで隠れているキーワードも取りこぼさないよう末尾に足す。
            foreach (var keyword in current)
            {
                if (displayList.Contains(keyword) == false)
                {
                    displayList.Add(keyword);
                }
            }

            if (displayList.Count == 0)
            {
                EditorGUILayout.LabelField("（表示できるキーワードがありません。絞り込みを変えてください）", EditorStyles.miniLabel);
                return;
            }

            DrawKeywordToggles(displayList, current, keywordsProp);
        }

        private HashSet<int> FindDuplicateShaderIds(out Dictionary<int, int> firstIndexByShader)
        {
            firstIndexByShader = new Dictionary<int, int>();
            var duplicates = new HashSet<int>();
            for (var i = 0; i < _entriesProp.arraySize; i++)
            {
                var shader = _entriesProp.GetArrayElementAtIndex(i).FindPropertyRelative("_shader").objectReferenceValue;
                if (shader == null)
                {
                    continue;
                }

                var id = shader.GetInstanceID();
                if (firstIndexByShader.ContainsKey(id))
                {
                    duplicates.Add(id);
                }
                else
                {
                    firstIndexByShader[id] = i;
                }
            }

            return duplicates;
        }

        private static void DrawKeywordToggles(List<string> displayList, List<string> current, SerializedProperty keywordsProp)
        {
            for (var k = 0; k < displayList.Count; k++)
            {
                if (k % ToggleColumns == 0)
                {
                    EditorGUILayout.BeginHorizontal();
                }

                var keyword = displayList[k];
                var on = current.Contains(keyword);
                var newOn = EditorGUILayout.ToggleLeft(keyword, on);
                if (newOn != on)
                {
                    if (newOn)
                    {
                        AddKeyword(keywordsProp, keyword);
                    }
                    else
                    {
                        RemoveKeyword(keywordsProp, keyword);
                    }
                }

                if (k % ToggleColumns == ToggleColumns - 1 || k == displayList.Count - 1)
                {
                    EditorGUILayout.EndHorizontal();
                }
            }
        }

        /// <summary>style で描画したとき maxWidth に収まるよう、必要なら末尾を省略（…）した文字列を返す。</summary>
        private static string TruncateToWidth(string text, GUIStyle style, float maxWidth)
        {
            if (maxWidth <= 0f)
            {
                return text;
            }

            var content = new GUIContent(text);
            if (style.CalcSize(content).x <= maxWidth)
            {
                return text;
            }

            const string ellipsis = "…";
            var low = 0;
            var high = text.Length;
            while (low < high)
            {
                var mid = (low + high + 1) / 2;
                content.text = text.Substring(0, mid) + ellipsis;
                if (style.CalcSize(content).x <= maxWidth)
                {
                    low = mid;
                }
                else
                {
                    high = mid - 1;
                }
            }

            return text.Substring(0, low) + ellipsis;
        }

        private static List<string> FilterKeywords(string[] allKeywords, string filter, bool userOnly)
        {
            return allKeywords
                .Where(keyword => userOnly == false || keyword.StartsWith("_", StringComparison.Ordinal))
                .Where(keyword => string.IsNullOrEmpty(filter)
                                  || keyword.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();
        }

        private string[] GetKeywordNames(Shader shader)
        {
            var id = shader.GetInstanceID();
            if (_keywordCache.TryGetValue(id, out var names) == false)
            {
                var raw = shader.keywordSpace.keywordNames ?? Array.Empty<string>();
                names = raw
                    .Where(name => string.IsNullOrEmpty(name) == false)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .ToArray();
                _keywordCache[id] = names;
            }

            return names;
        }

        private static List<string> ReadStringList(SerializedProperty arrayProp)
        {
            var result = new List<string>(arrayProp.arraySize);
            for (var i = 0; i < arrayProp.arraySize; i++)
            {
                result.Add(arrayProp.GetArrayElementAtIndex(i).stringValue);
            }

            return result;
        }

        private static void AddKeyword(SerializedProperty arrayProp, string keyword)
        {
            var index = arrayProp.arraySize;
            arrayProp.arraySize = index + 1;
            arrayProp.GetArrayElementAtIndex(index).stringValue = keyword;
        }

        private static void RemoveKeyword(SerializedProperty arrayProp, string keyword)
        {
            for (var i = 0; i < arrayProp.arraySize; i++)
            {
                if (string.Equals(arrayProp.GetArrayElementAtIndex(i).stringValue, keyword, StringComparison.Ordinal))
                {
                    arrayProp.DeleteArrayElementAtIndex(i);
                    return;
                }
            }
        }

        /// <summary>キーワード配列プロパティを指定の集合で置き換える（コピー＆ペースト用）。</summary>
        private static void SetKeywords(SerializedProperty arrayProp, List<string> keywords)
        {
            arrayProp.ClearArray();
            for (var i = 0; i < keywords.Count; i++)
            {
                arrayProp.arraySize = i + 1;
                arrayProp.GetArrayElementAtIndex(i).stringValue = keywords[i];
            }
        }

        private void AddEntry()
        {
            var index = _entriesProp.arraySize;
            _entriesProp.arraySize = index + 1;
            // arraySize++ は直前要素を複製するため、新規エントリは空にする。
            var element = _entriesProp.GetArrayElementAtIndex(index);
            element.FindPropertyRelative("_shader").objectReferenceValue = null;
            element.FindPropertyRelative("_keywords").ClearArray();
        }
    }
}
