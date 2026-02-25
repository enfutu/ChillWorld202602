using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace GPUInfiniteGrass {
    [InitializeOnLoad]
    public static class OptimizeMeshDataDisabler {

        private const string _settingPropertyName = "stripUnusedMeshComponents";
        private static readonly string _firstRunFlagKey = "OptimizeMeshDataDisabler__" + Hash128.Compute(Application.dataPath);
        private const string _firstRunWarning = "[GPUInfiniteGrass] Player Setting 'Optimize Mesh Data' was disabled automatically for compatibility. Unity removes required mesh Vertex Color attributes sometimes if this option is enabled. You can enable it back manually: Project Settings -> Player -> Optimize Mesh Data";

        static OptimizeMeshDataDisabler() {
            EditorApplication.delayCall += InitializeOnce;
        }

        private static void InitializeOnce() {
            if (EditorPrefs.GetBool(_firstRunFlagKey, false)) return;
            bool changed = TryDisableOptimizeMeshData();
            if (changed) Debug.LogWarning(_firstRunWarning);
            EditorPrefs.SetBool(_firstRunFlagKey, true);
        }

        private static bool TryDisableOptimizeMeshData() {
            var flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            PropertyInfo property = typeof(PlayerSettings).GetProperty(_settingPropertyName, flags);
            if (property == null || property.PropertyType != typeof(bool) || !property.CanRead || !property.CanWrite) return false;

            bool isEnabled = (bool)property.GetValue(null, null);
            if (!isEnabled) return false;

            property.SetValue(null, false, null);
            AssetDatabase.SaveAssets();
            return true;
        }

    }
}