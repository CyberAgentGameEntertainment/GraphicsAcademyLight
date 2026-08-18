// --------------------------------------------------------------
// Copyright 2026 CyberAgent, Inc.
// --------------------------------------------------------------

#if HAS_TIMELINE
using UnityEditor;
using UnityEngine;

namespace Sirius.DevSupport
{
    [CustomEditor(typeof(TimelineTestConfig))]
    internal class TimelineTestConfigEditor : Editor
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
#endif
