using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GPUInfiniteGrass {

    public partial class ParticleSurfaceManagerInspector {

        // Syncs trail keyword/state with particle material
        private static void SyncTrailFeature(ParticleSurfaceManager manager, bool enabled) {
            if (manager == null || manager.ParticleMaterial == null) return;
            Material particleMaterial = manager.ParticleMaterial;
            if (!particleMaterial.HasProperty("_EnableTrail")) return;
            float target = enabled ? 1f : 0f;
            if (!Mathf.Approximately(particleMaterial.GetFloat("_EnableTrail"), target)) {
                particleMaterial.SetFloat("_EnableTrail", target);
                EditorUtility.SetDirty(particleMaterial);
            }
            if (enabled) particleMaterial.EnableKeyword("_TRAIL");
            else particleMaterial.DisableKeyword("_TRAIL");
        }

        // Draw Performance Estimation panel
        private static void DrawPerformancePanel(ParticleSurfaceManager manager) {
            if (manager == null) return;
            int drawAmount = Mathf.Clamp(manager.DrawAmount, 0, ParticleSurfaceManager.MaxDrawAmount);
            long totalParticles = (long)drawAmount * _particlesPerBatch;
            int drawCalls = drawAmount <= 0 ? 0 : (drawAmount + 510) / 511;
            float drawDistance = Mathf.Max(manager.DrawDistance, 0.001f);
            float calibratedParticles = ApplyDensityDrift((float)totalParticles, drawDistance);
            int maxPresetRT = GetMaxPresetResolution(manager.SurfaceRTResolution, manager.EnableTrail ? manager.TrailCRTResolution : TextureResolutionPreset.Custom);

            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical("box");
            GUIStyle foldoutStyle = new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold };
            Rect foldoutRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
            foldoutRect.xMin += 16f;
            bool newFoldout = EditorGUI.Foldout(foldoutRect, _performanceFoldout, new GUIContent(_performanceTitle, _performanceTooltip), true, foldoutStyle);
            if (newFoldout != _performanceFoldout) {
                _performanceFoldout = newFoldout;
                EditorPrefs.SetBool(_prefsKey + "PerformanceFoldout", _performanceFoldout);
            }

            if (_performanceFoldout) {
                if (manager.DrawAmount > ParticleSurfaceManager.MaxDrawAmount) {
                    EditorGUILayout.HelpBox(_drawAmountClampedWarning, MessageType.Warning);
                }
                DrawValueLine(_particlesLabel, totalParticles.ToString("N0"), _particlesTooltip);
                DrawValueLine(_drawCallsLabel, drawCalls.ToString("N0"), _drawCallsTooltip);
                EditorGUILayout.Space(2f);

                PerfGrade quest3Grade = PerfGrade.Excellent;
                PerfGrade quest2Grade = PerfGrade.Excellent;
                for (int i = 0; i < _perfProfiles.Length; i++) {
                    PerfProfile profile = _perfProfiles[i];
                    PerfGrade particleGrade = EvaluateGrade(profile, calibratedParticles);
                    PerfGrade rtFloorGrade = GetResolutionFloorGrade(profile.Name, maxPresetRT);
                    PerfGrade grade = MaxGrade(particleGrade, rtFloorGrade);
                    if (i == 2) quest3Grade = grade;
                    else if (i == 3) quest2Grade = grade;
                    float bar = Mathf.Max(GetNormalizedLoad(profile, calibratedParticles), GetResolutionFloorBar(rtFloorGrade));
                    string label = profile.Name + ": " + GradeLabel(grade) + " (" + profile.FpsHints[(int)grade] + ")";
                    DrawPerfBar(label, bar);
                }

                List<string> tips = BuildOptimizationTips(manager, maxPresetRT, quest3Grade, quest2Grade);
                if (tips.Count > 0) {
                    EditorGUILayout.Space(4f);
                    EditorGUILayout.LabelField(_optimizationTipsTitle, EditorStyles.boldLabel);
                    for (int i = 0; i < tips.Count; i++) {
                        EditorGUILayout.LabelField("- " + tips[i], EditorStyles.wordWrappedLabel);
                    }
                }
            }

            EditorGUILayout.EndVertical();
        }

        // Draw stats text line for Performance Estimator
        private static void DrawValueLine(string label, string value, string tooltip = null) {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent(label, tooltip), GUILayout.Width(140f));
            EditorGUILayout.LabelField(value, EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();
        }

        // Draws progress bar for Performance Estimator
        private static void DrawPerfBar(string label, float normalizedLoad) {
            Rect rect = EditorGUILayout.GetControlRect(false, 18f);
            EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.15f));
            float fill = Mathf.Clamp01(normalizedLoad);
            if (fill > 0f) {
                Rect fillRect = rect;
                fillRect.width = rect.width * fill;
                Color color = Color.Lerp(new Color(0.25f, 0.70f, 0.25f), new Color(0.95f, 0.28f, 0.25f), fill);
                EditorGUI.DrawRect(fillRect, color);
            }
            GUIStyle style = new GUIStyle(EditorStyles.boldLabel);
            style.alignment = TextAnchor.MiddleLeft;
            style.padding = new RectOffset(6, 0, 0, 0);
            EditorGUI.LabelField(rect, label, style);
        }

        // Gets Performance Grade
        private static PerfGrade EvaluateGrade(PerfProfile profile, float loadPlanes) {
            if (loadPlanes <= profile.ExcellentMax) return PerfGrade.Excellent;
            if (loadPlanes <= profile.GoodMax) return PerfGrade.Good;
            if (loadPlanes <= profile.MediumMax) return PerfGrade.Medium;
            if (loadPlanes <= profile.HeavyMax) return PerfGrade.Heavy;
            return PerfGrade.Critical;
        }

        private static float GetNormalizedLoad(PerfProfile profile, float loadPlanes) {
            return Mathf.Clamp01(loadPlanes / Mathf.Max(profile.CriticalMax, 1f));
        }

        private static float GetResolutionFloorBar(PerfGrade floorGrade) {
            if (floorGrade == PerfGrade.Critical) return 1f;
            if (floorGrade == PerfGrade.Heavy) return 0.82f;
            if (floorGrade == PerfGrade.Medium) return 0.64f;
            if (floorGrade == PerfGrade.Good) return 0.46f;
            return 0f;
        }

        // Density drift rough estimation
        private static float ApplyDensityDrift(float particleCount, float drawDistance) {
            float radius = Mathf.Max(drawDistance, 1f);
            float baseArea = Mathf.PI * _perfCalibrationRadius * _perfCalibrationRadius;
            float currentArea = Mathf.PI * radius * radius;
            float densityScale = Mathf.Pow(baseArea / currentArea, _densityDriftPower);
            densityScale = Mathf.Clamp(densityScale, 0.75f, 1.35f); // Small drift only
            return particleCount * densityScale;
        }

        private static string GradeLabel(PerfGrade grade) {
            if (grade == PerfGrade.Excellent) return "Excellent";
            if (grade == PerfGrade.Good) return "Good";
            if (grade == PerfGrade.Medium) return "Medium";
            if (grade == PerfGrade.Heavy) return "Heavy";
            return "Critical";
        }

        private static int GetMaxPresetResolution(TextureResolutionPreset surfacePreset, TextureResolutionPreset trailPreset) {
            int surface = GetPresetResolution(surfacePreset);
            int trail = GetPresetResolution(trailPreset);
            return Mathf.Max(surface, trail);
        }

        private static int GetPresetResolution(TextureResolutionPreset preset) {
            if (preset == TextureResolutionPreset.Custom) return 0; // Ignore Custom in RT limits
            int size = (int)preset;
            if (size < 64 || size > 4096) return 0;
            return size;
        }

        // Render Textures Resolution Performance Grade Estimation
        private static PerfGrade GetResolutionFloorGrade(string platformName, int maxPresetRT) {
            if (maxPresetRT <= 0) return PerfGrade.Excellent;

            if (platformName == "Quest 2") {
                if (maxPresetRT >= 1024) return PerfGrade.Critical;
                if (maxPresetRT >= 512) return PerfGrade.Medium;
                if (maxPresetRT >= 256) return PerfGrade.Good;
                return PerfGrade.Excellent;
            }

            if (platformName == "Quest 3") {
                if (maxPresetRT >= 2048) return PerfGrade.Critical;
                if (maxPresetRT >= 1024) return PerfGrade.Medium;
                if (maxPresetRT >= 512) return PerfGrade.Good;
                return PerfGrade.Excellent;
            }

            if (platformName == "Steam Deck") {
                if (maxPresetRT >= 2048) return PerfGrade.Medium;
                if (maxPresetRT >= 1024) return PerfGrade.Good;
                return PerfGrade.Excellent;
            }

            // Gaming PC
            if (maxPresetRT >= 2048) return PerfGrade.Good;
            if (maxPresetRT >= 1024) return PerfGrade.Excellent;
            return PerfGrade.Excellent;
        }

        private static PerfGrade MaxGrade(PerfGrade a, PerfGrade b) {
            return (PerfGrade)Mathf.Max((int)a, (int)b);
        }

        // Optimization tips
        private static List<string> BuildOptimizationTips(ParticleSurfaceManager manager, int maxPresetRT, PerfGrade quest3Grade, PerfGrade quest2Grade) {
            List<string> tips = new List<string>(4);
            if (quest2Grade >= PerfGrade.Medium || quest3Grade >= PerfGrade.Medium) tips.Add(_tipReduceDrawAmount);
            if (maxPresetRT > 512) tips.Add(_tipLowerRTHeavy);
            else if (maxPresetRT == 512) tips.Add(_tipLowerRTMedium);
            if ((manager.CastShadows || manager.ReceiveShadows) && HasRealtimeShadowLights()) tips.Add(_tipDisableShadows);
            if (manager.AlwaysUpdateSurface) tips.Add(_tipDisableAlwaysUpdate);
            return tips;
        }

        // Checks if scene has realtime lights with shadows
        private static bool HasRealtimeShadowLights() {
            double now = EditorApplication.timeSinceStartup;
            if (now - _shadowLightsCacheTime < 1d) return _hasRealtimeShadowLights;
            _shadowLightsCacheTime = now;
            _hasRealtimeShadowLights = false;
            Light[] lights = FindObjectsOfType<Light>();
            for (int i = 0; i < lights.Length; i++) {
                Light light = lights[i];
                if (light == null || !light.enabled) continue;
                if (light.shadows == LightShadows.None) continue;
                if (light.lightmapBakeType != LightmapBakeType.Realtime && light.lightmapBakeType != LightmapBakeType.Mixed) continue;
                _hasRealtimeShadowLights = true;
                break;
            }
            return _hasRealtimeShadowLights;
        }
    }
}
