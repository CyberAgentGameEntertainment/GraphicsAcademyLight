#if UNITY_EDITOR

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Sirius.PostProcessing.Runtime.Scripts.Features
{
    // PostProcessingFeatureの使用Shaderを提供する部分の実装
    // Editor上でのみ有効
    public sealed partial class SiriusPostProcessingFeature : IUsingShaderProvider
    {
        // 各PassのAllowExecuteをそれに紐つけられてるbool値に更新する
        private void UpdatePassAllow()
        {
            var fields = GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var field in fields)
            {
                var attr = field.GetCustomAttribute<AllowFlagAttribute>();
                if (attr == null)
                    continue;   // AllowFlagAttributeが付いていないフィールドはスキップ

                if (field.GetValue(this) is not IAllowExecute pass)
                    continue;   // IAllowExecuteを実装していないフィールドはスキップ

                // 属性で指定したboolフィールドを探す
                var boolField = fields.FirstOrDefault(f => f.FieldType == typeof(bool) && f.Name == attr.BoolFieldName);
                if (boolField == null)
                {
                    Debug.LogWarning($"AllowFlag: '{attr.BoolFieldName}' が見つかりませんでした");
                    continue;
                }

                var allowValue = (bool)boolField.GetValue(this);
                pass.AllowExecute = allowValue;
            }
        }

        // 型がScriptableRenderPassのフィールドからStaticプロパティUsingShaderNameListを取ってくる
        private static IEnumerable<string> GetFieldShaderNameList(FieldInfo field)
        {
            var t = field.FieldType;
            if (!typeof(ScriptableRenderPass).IsAssignableFrom(t))
                return null;   // ScriptableRenderPassを継承している型のみ

            var prop = t.GetProperty("UsingShaderNameList",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy);
            if (prop == null || !typeof(IEnumerable<string>).IsAssignableFrom(prop.PropertyType))
                return null;   // UsingShaderNameListプロパティが存在しない、もしくはIEnumerable<string>でない

            return prop.GetValue(null, null) as IEnumerable<string>;
        }

        // フィールドにあるScriptableRenderPassを継承している型のStaticプロパティUsingShaderNameListから使われるShaderNameを収集
        public static IEnumerable<string> GetAllShaderNameList()
        {
            var uniqueShaderNames = new HashSet<string>();

            // SiriusPostProcessingFeatureの全フィールド
            var fields = typeof(SiriusPostProcessingFeature)
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            foreach (var field in fields)
            {
                var shaderNames = GetFieldShaderNameList(field);
                if (shaderNames == null)
                    continue;   // nullならスキップ
                uniqueShaderNames.UnionWith(shaderNames);
            }
            return uniqueShaderNames;
        }

        // 実際有効のPostProcessing用のShader名リストを取得する
        public IEnumerable<string> GetUsingShaderNameList()
        {
            // 各PassのAllowExecuteを更新
            UpdatePassAllow();

            var uniqueShaderNames = new HashSet<string>();

            var fields = GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var field in fields)
            {
                var value = field.GetValue(this);
                if (value is IAllowExecute { AllowExecute: false })
                {
                    continue;   // AllowExecuteがあり && falseならスキップ
                }
                // IAllowExecuteを実装していない場合、デフォルト許可になる

                var shaderNames = GetFieldShaderNameList(field);
                if (shaderNames == null)
                    continue;   // nullならスキップ
                uniqueShaderNames.UnionWith(shaderNames);
            }

            return uniqueShaderNames;
        }
    }
}

#endif
