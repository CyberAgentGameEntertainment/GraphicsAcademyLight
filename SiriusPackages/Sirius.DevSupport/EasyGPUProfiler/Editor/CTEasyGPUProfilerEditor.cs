// --------------------------------------------------------------
// Copyright 2025 CyberAgent, Inc.
// --------------------------------------------------------------

using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Sirius.DevSupport.Editor
{
    public static class CTEasyGPUProfilerEditor
    {
        private static readonly string RecordDefine = "UNITY_USE_RECORDER";
        [MenuItem("Tools/Sirius/Dev Support/Easy GPU Profiler/Add Define")]
        private static void OnEnableEasyGPUProfiler()
        {
            // 必要なプラットフォームを有効にしてください
            AddDefine(NamedBuildTarget.Standalone);
            AddDefine(NamedBuildTarget.Android);
            AddDefine(NamedBuildTarget.iOS);
#if UNITY_SWITCH
            AddDefine(NamedBuildTarget.NintendoSwitch);
#endif
#if UNITY_GAMECORE_XBOXONE || UNITY_XBOXONE
            AddDefine(NamedBuildTarget.XboxOne);
#endif
#if UNITY_GAMECORE_XBOXSERIES
            AddDefine(NamedBuildTarget.XboxSeries);
#endif
#if UNITY_PS4
            AddDefine(NamedBuildTarget.PS4);
#endif
#if UNITY_PS5
            AddDefine(NamedBuildTarget.PS5);
#endif
        }
        private static void AddDefine(NamedBuildTarget namedBuildTarget)
        {
            PlayerSettings.GetScriptingDefineSymbols(namedBuildTarget, out var defines);

            foreach (var def in defines)
                if (def == RecordDefine) {
                    Debug.Log(namedBuildTarget + "には、既にDefine " + RecordDefine + " があります");
                    return;
                }

            var newDefines = new string[defines.Length + 1];
            defines.CopyTo(newDefines, 0);
            newDefines[defines.Length] = RecordDefine;
            PlayerSettings.SetScriptingDefineSymbols(namedBuildTarget, newDefines);
        }
    }
}
