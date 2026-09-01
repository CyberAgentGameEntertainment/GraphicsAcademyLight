// --------------------------------------------------------------
// Copyright 2025 CyberAgent, Inc.
// --------------------------------------------------------------

namespace Sirius.Core.Runtime.Scripts
{
    public static class GlobalSettings
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static bool DevelopmentMode { get; set; } = true;
#else
        public static bool DevelopmentMode { get => false; set {} }
#endif
    }
}
