using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace GPUInfiniteGrass {
    
    [CustomEditor(typeof(ParticleSurfaceManager))]
    public partial class ParticleSurfaceManagerInspector : Editor {
        
        // Editors
        private Editor _grassMaterialEditor;
        private Editor _surfaceMaskMaterialEditor;
        private Editor _trailMaterialEditor;
        private ReorderableList _surfaceList;
        
        // Tips/Titles
        private const string _performanceTitle = "Approximated Performance";
        private const string _optimizationTipsTitle = "Optimization Tips";
        private const string _particlesLabel = "Particles";
        private const string _drawCallsLabel = "Draw Calls";
        private const string _drawAmountClampedWarning = "Draw Amount is clamped to 10,000.";
        private const string _tipReduceDrawAmount = "Reduce Draw Amount to lower rendered polygon count.";
        private const string _tipLowerRTHeavy = "Lower Surface RT / Trail CRT resolution for mobile builds! For Quest, values above 512x512 are heavy.";
        private const string _tipLowerRTMedium = "512x512 RT / Trail CRT is medium cost for Quest and mobile. Use 256x256 or lower for better performance.";
        private const string _tipDisableShadows = "Scene has realtime shadow lights and grass shadows are enabled. You can disable Cast/Receive Shadows on grass for better FPS.";
        private const string _tipDisableAlwaysUpdate = "If you are not sure you need Always Update Surface, disable it.";
        private const string _performanceTooltip = "Very rough estimate only. Use it for approximate expectations, but always test performance manually in your real scene because final FPS depends on many additional factors.";
        private const string _particlesTooltip = "Total rendered particles. Each particle is 2 triangles, so geometry triangle count is double this value.";
        private const string _drawCallsTooltip = "GPU draw calls for particle rendering, up to 511 instances per draw call.";
        private static readonly GUIContent _surfacesTitleContent = new GUIContent("Surfaces", "Sortable list of surfaces used to generate paint/render meshes.");
        private static readonly GUIContent _surfaceColumnContent = new GUIContent("Surface", "Transform that has MeshFilter or Terrain used as source for generated Surface Mesh.");
        private static readonly GUIContent _surfaceMeshColumnContent = new GUIContent("Surface Mesh", "Generated paint/render mesh aligned with Surface. Used directly for Surface Mask rendering and paint operations.");
        private static readonly GUIContent _terrainResolutionPopupLabelContent = new GUIContent("Terrain Resolution", "Global relative terrain mesh resolution used for Terrain -> Surface Mesh generation.");
        private const string _regenerateMessageNoMesh = "Regenerate Surface Mesh from current source?";
        private const string _regenerateMessageCopyByIndex = "Vertex paint will be copied to the regenerated mesh. Paint is preserved if vertex order/topology is unchanged.";
        private const string _regenerateMessagePotentialLoss = "Paint may be lost because vertex count/topology can differ between old and regenerated mesh.";
        private const string _regenerateMessageTerrainSameResolution = "Terrain resolution is unchanged. Regenerate Surface Mesh? Existing paint will be preserved.";
        private const string _regenerateMessageTerrainInterpolated = "Terrain resolution changed. Vertex paint will be interpolated to the new mesh. Some quality loss is possible.";
        private const string _regenerateMessageTerrainCannotInterpolate = "Terrain resolution changed, but previous mesh layout is incompatible. Vertex paint may be lost.";
        private const string _regenerateMessageReadonlySuffix = " Current mesh is not editable, so a new file will be created.";
        private const string _regenerateMessageCopyByIndexReadonly = _regenerateMessageCopyByIndex + _regenerateMessageReadonlySuffix;
        private const string _regenerateMessageTerrainSameResolutionReadonly = _regenerateMessageTerrainSameResolution + _regenerateMessageReadonlySuffix;
        private const string _terrainPolyCountLabelPrefix = "Result Poly Count";
        
        // Paths
        private const string _meshCopyFolder = "Assets/RED_SIM/GPU Infinite Grass/Generated";
        private const string _prefsKey = "GPUInfiniteGrass.SurfacePainter.";
        private const string _terrainResolutionRatioPrefsKey = _prefsKey + "TerrainResolutionRatioIndex";
        
        // Calibrated values
        private const int _particlesPerBatch = 16383;
        private const float _perfCalibrationRadius = 150f;
        private const float _densityDriftPower = 0.2f;
        private static readonly PerfProfile[] _perfProfiles = {
            new PerfProfile("Desktop PC", 1000000f, 1750000f, 3500000f, 7000000f, 12000000f, new[] { "144+ FPS", "90-144+ FPS", "60-90 FPS", "30-60 FPS", "10-40 FPS" }),
            new PerfProfile("Steam Deck", 500000f, 750000f, 1250000f, 2500000f, 5000000f, new[]    { "60+ FPS",  "55-60 FPS",   "50-60 FPS", "30-40 FPS", "20-25 FPS" }),
            new PerfProfile("Quest 3", 300000f, 500000f, 850000f, 1700000f, 2000000f, new[]        { "70+ FPS",  "60-70 FPS",   "50-60 FPS", "30-40 FPS", "20-25 FPS" }),
            new PerfProfile("Quest 2", 150000f, 250000f, 425000f, 850000f, 1000000f, new[]         { "70+ FPS",  "60-70 FPS",   "50-60 FPS", "30-40 FPS", "20-25 FPS" })
        };
        
        // Fields
        private static readonly string[] _hiddenByDefault = { "m_Script", "SurfaceMeshes" };
        private static readonly string[] _trailOnlyFields = { "TrailCRT", "TrailCRTResolution", "TrailMaterial", "TrailDecay", "PlayerTrailRadius", "TrailTargets" };
        private const string _regenerateMeshTooltip = "Regenerate Surface Mesh from current Surface source.";
        private const float _surfaceListColumnSpacing = 8f;
        private const float _surfaceListHeaderLeftInset = 4f;
        private const float _surfaceListHeaderRightInset = 2f;
        private const float _surfaceListActionButtonWidth = 20f;
        private const string _regenerateConfirmButtonLabel = "Regenerate";
        private const string _cancelButtonLabel = "Cancel";
        // Unity popup parses ASCII '/' as submenu path, so use fraction slash.
        private static readonly string[] _terrainResolutionRatioLabels = { "1⁄1", "1⁄2", "1⁄4", "1⁄8" };
        private static readonly int[] _terrainResolutionDividers = { 1, 2, 4, 8 };
        private static int _terrainResolutionRatioIndex = 1;
        
        // Performance estimation
        private static bool _performanceFoldout = true;
        private static double _shadowLightsCacheTime = -1d;
        private static bool _hasRealtimeShadowLights;
        
        private enum PerfGrade {
            Excellent = 0,
            Good = 1,
            Medium = 2,
            Heavy = 3,
            Critical = 4
        }
        
        private struct PerfProfile {
            public readonly string Name;
            public readonly float ExcellentMax;
            public readonly float GoodMax;
            public readonly float MediumMax;
            public readonly float HeavyMax;
            public readonly float CriticalMax;
            public readonly string[] FpsHints;
            public PerfProfile(string name, float excellentMax, float goodMax, float mediumMax, float heavyMax, float criticalMax, string[] fpsHints) {
                Name = name; ExcellentMax = excellentMax; GoodMax = goodMax; MediumMax = mediumMax; HeavyMax = heavyMax; CriticalMax = criticalMax; FpsHints = fpsHints;
            }
        }

        // Constructor
        static ParticleSurfaceManagerInspector() {
            RegisterEditorCallbacks();
        }

        // Ensures static terrain sync callbacks are registered right after script domain reload.
        [InitializeOnLoadMethod]
        private static void InitializeOnEditorLoad() {
            RegisterEditorCallbacks();
            MarkManagersCacheDirty();
        }
        
        // On Project Changed
        private static void OnProjectChanged() {
            MarkManagersCacheDirty();
            ClearGeometryCaches();
            ParticleSurfaceManager[] managers = FindObjectsOfType<ParticleSurfaceManager>();
            for (int i = 0; i < managers.Length; i++) {
                ParticleSurfaceManager manager = managers[i];
                if (manager == null) continue;
                SyncSurfaceMeshesArray(manager);
            }
        }
        
        // On Inspector GUI
        public override void OnInspectorGUI() {
            float previousLabelWidth = EditorGUIUtility.labelWidth;
            float previousFieldWidth = EditorGUIUtility.fieldWidth;
            int previousIndentLevel = EditorGUI.indentLevel;

            try {
                EditorGUIUtility.labelWidth = 0f;
                EditorGUIUtility.fieldWidth = 0f;
                EditorGUI.indentLevel = 0;

                DrawPaintUI();
                GUILayout.Space(-10f);
                var manager = (ParticleSurfaceManager)target;
                serializedObject.Update();
                bool trailEnabled;
                bool trailToggleChanged = DrawManagerProperties(serializedObject, out trailEnabled);
                serializedObject.ApplyModifiedProperties();
                SyncSurfaceMeshesArray(manager);
                if (trailToggleChanged) SyncTrailFeature(manager, trailEnabled);
                DrawPerformancePanel(manager);
                EditorGUILayout.Space();
                DrawMaterialInspector(manager.ParticleMaterial, ref _grassMaterialEditor);
                DrawMaterialInspector(manager.MaskMaterial, ref _surfaceMaskMaterialEditor);
                if (manager.EnableTrail) DrawMaterialInspector(manager.TrailMaterial, ref _trailMaterialEditor);
                else if (_trailMaterialEditor != null) {
                    DestroyImmediate(_trailMaterialEditor);
                    _trailMaterialEditor = null;
                }
            } finally {
                EditorGUIUtility.labelWidth = previousLabelWidth;
                EditorGUIUtility.fieldWidth = previousFieldWidth;
                EditorGUI.indentLevel = previousIndentLevel;
            }
        }
        
        // On Enable
        private void OnEnable() {
            RegisterEditorCallbacks();
            LoadPrefs();
            if (_paintEnabled) ForceEnableGizmos();
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        // On Disable
        private void OnDisable() {
            if (_paintEnabled) {
                if (_isPainting) EndPaint();
                _paintEnabled = false;
                SceneView.RepaintAll();
            }
            _lastPaintSurfaceIndex = -1;
            SavePrefs();
            SaveIfDirty();
            Undo.undoRedoPerformed -= OnUndoRedo;
            DestroyCachedEditors();
            _surfaceList = null;
            ClearGeometryCaches();
        }
        
        // Draw Material Inspector
        private static void DrawMaterialInspector(Material mat, ref Editor cachedEditor) {
            if (mat == null) return;
            EditorGUI.indentLevel++;
            try {
                CreateCachedEditor(mat, typeof(MaterialEditor), ref cachedEditor);
                var matEditor = cachedEditor as MaterialEditor;
                if (matEditor != null) {
                    matEditor.DrawHeader();
                    matEditor.OnInspectorGUI();
                }
            } finally {
                EditorGUI.indentLevel--;
            }
        }

        // Draw Material Properties for Material Inspector
        private bool DrawManagerProperties(SerializedObject so, out bool trailEnabled) {
            var enableTrail = so.FindProperty("EnableTrail");
            bool prevTrailEnabled = enableTrail == null || enableTrail.boolValue;
            trailEnabled = prevTrailEnabled;

            var hiddenFields = new HashSet<string>(_hiddenByDefault);
            if (!trailEnabled) for (int i = 0; i < _trailOnlyFields.Length; i++) hiddenFields.Add(_trailOnlyFields[i]);

            var iterator = so.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren)) {
                enterChildren = false;
                if (hiddenFields.Contains(iterator.name)) continue;

                if (iterator.name == "Surfaces") {
                    SerializedProperty surfaces = so.FindProperty("Surfaces");
                    SerializedProperty surfaceMeshes = so.FindProperty("SurfaceMeshes");
                    DrawSurfaceList(surfaces, surfaceMeshes);
                    continue;
                }

                if (iterator.name == "DrawAmount") {
                    EditorGUILayout.PropertyField(iterator, true);
                    if (!iterator.hasMultipleDifferentValues) iterator.intValue = Mathf.Clamp(iterator.intValue, 0, ParticleSurfaceManager.MaxDrawAmount);
                    continue;
                }

                EditorGUILayout.PropertyField(iterator, true);

                if (iterator.name != "EnableTrail") continue;
                trailEnabled = iterator.boolValue;
                if (trailEnabled) for (int i = 0; i < _trailOnlyFields.Length; i++) hiddenFields.Remove(_trailOnlyFields[i]);
                else for (int i = 0; i < _trailOnlyFields.Length; i++) hiddenFields.Add(_trailOnlyFields[i]);
            }

            return enableTrail != null && trailEnabled != prevTrailEnabled;
        }

        private void DestroyCachedEditors() {
            if (_grassMaterialEditor != null) DestroyImmediate(_grassMaterialEditor);
            if (_surfaceMaskMaterialEditor != null) DestroyImmediate(_surfaceMaskMaterialEditor);
            if (_trailMaterialEditor != null) DestroyImmediate(_trailMaterialEditor);
        }
        
    }
    
}
