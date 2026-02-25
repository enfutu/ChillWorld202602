using System;
using UnityEditor;
using UnityEngine;

namespace GPUInfiniteGrass {

    [InitializeOnLoad]
    public static class SceneViewFPSLimiter {

        // Base EditorPrefs keys
        private const string _keyInteractionMode = "InteractionMode";
        private const string _keyIdleTimeMs = "ApplicationIdleTime";

        // Monitor Refresh Rate mode
        private const int _interactionModeMonitorRefresh = 2;

        // Check for monitor change interval
        private const double _pollIntervalSec = 1.0;

        // Refresh rate limits
        private const int _idleMsMin = 1;
        private const int _idleMsMax = 33;

        // Project-scoped first-run flag
        private static readonly string _firstRunFlagKey = "SceneViewFPSLimiter__" + Hash128.Compute(Application.dataPath);
        private const string _firstRunWarning = "[GPUInfiniteGrass] Scene view FPS was limited to your monitor refresh rate to save your background apps performance. Grass instancing slows down background apps insanely! \nYou can undo this changes manually: Preferences -> General -> Interaction Mode -> Default";

        private static double _nextPollTime;
        private static int _lastHz;
        private static int _lastIdleMs;

        static SceneViewFPSLimiter() {
            EditorApplication.delayCall += InitializeOnce;
            EditorApplication.update += Update;
        }

        private static void InitializeOnce() {
            if (!EditorPrefs.GetBool(_firstRunFlagKey, false)) {
                bool changed = false;
                int currentMode = SafeGetInt(_keyInteractionMode, defaultValue: -1);
                if (currentMode != _interactionModeMonitorRefresh) {
                    EditorPrefs.SetInt(_keyInteractionMode, _interactionModeMonitorRefresh);
                    changed = true;
                }
                if (TryUpdateIdleTime(true)) changed = true;
                if (changed) Debug.LogWarning(_firstRunWarning);
                EditorPrefs.SetBool(_firstRunFlagKey, true);
            } else {
                TryUpdateIdleTime(true);
            }
        }

        private static void Update() {
            if (Application.isPlaying) return;
            double t = EditorApplication.timeSinceStartup;
            if (t < _nextPollTime) return;
            _nextPollTime = t + _pollIntervalSec;
            TryUpdateIdleTime(force: false);
        }

        private static bool TryUpdateIdleTime(bool force) {
            int mode = SafeGetInt(_keyInteractionMode, defaultValue: -1);
            if (mode != _interactionModeMonitorRefresh) return false;
            int hz = GetRefreshRateBestEffort();
            if (hz <= 0) return false;
            int idleMs = ComputeIdleMsFromHz(hz);
            if (!force && hz == _lastHz && idleMs == _lastIdleMs) return false;
            _lastHz = hz;
            _lastIdleMs = idleMs;
            int currentIdle = SafeGetInt(_keyIdleTimeMs, defaultValue: -1);
            if (currentIdle == idleMs) return false;
            EditorPrefs.SetInt(_keyIdleTimeMs, idleMs);
            return true;
        }

        private static int ComputeIdleMsFromHz(int hz) {
            double ms = 1000.0 / Mathf.Max(1, hz);
            return Mathf.Clamp((int)Math.Round(ms), _idleMsMin, _idleMsMax);
        }

        private static int GetRefreshRateBestEffort() {
#if UNITY_2021_2_OR_NEWER
            // Preferred: refreshRateRatio is more correct for non-integer refresh.
            var rr = Screen.currentResolution.refreshRateRatio;
            double v = rr.value;
            return (int)Math.Round(v);
#else
        int r = Screen.currentResolution.refreshRate;
        return r > 1 ? r : 0;
#endif
        }

        private static int SafeGetInt(string key, int defaultValue) {
            return EditorPrefs.HasKey(key) ? EditorPrefs.GetInt(key) : defaultValue;
        }

    }
}