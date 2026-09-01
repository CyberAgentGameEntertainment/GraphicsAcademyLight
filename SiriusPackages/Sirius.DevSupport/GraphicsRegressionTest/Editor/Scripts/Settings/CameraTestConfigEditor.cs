// --------------------------------------------------------------
// Copyright 2025 CyberAgent, Inc.
// --------------------------------------------------------------

using UnityEditor;
using UnityEngine;

namespace Sirius.DevSupport
{
    [CustomEditor(typeof(CameraTestConfig))]
    internal class CameraTestConfigEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            EditorGUILayout.Space();

            if (GUILayout.Button("Refresh Test Runner", GUILayout.Width(150)))
            {
                // TestCaseSourceを再評価するため、スクリプトを再ロードします
                EditorUtility.RequestScriptReload();
            }
        }
    }
}
