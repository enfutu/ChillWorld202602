using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GPUInfiniteGrass {

    public partial class ParticleSurfaceManagerInspector {
        private const int _vertexGizmoDotLimit = 128;
        private static readonly int[] _vertexGizmoSelectionBuffer = new int[_vertexGizmoDotLimit];

        // Paint tool
        private enum ToolMode { Brush, Fill, Eraser }
        private static bool _paintEnabled;
        private static ToolMode _tool = ToolMode.Brush;
        private static float _brushRadius = 1f;
        private static float _brushFeather = 0.5f;
        private static float _brushBrightness = 1f;
        private static float _fillBrightness = 1f;
        private static float _eraserRadius = 1f;
        private static float _eraserFeather = 0.5f;
        private static int _paintChannel;
        private static int _lastPaintSurfaceIndex = -1;

        // Undo
        private static bool _isPainting;
        private static int _undoGroup;
        private static readonly HashSet<Mesh> _undoMeshes = new HashSet<Mesh>();
        private static readonly HashSet<Mesh> _dirtyMeshes = new HashSet<Mesh>();
        private static readonly HashSet<Mesh> _touchedMeshes = new HashSet<Mesh>();

        // On Undo/Redo
        private void OnUndoRedo() {
            ClearGeometryCaches();
            foreach (var mesh in _touchedMeshes) {
                if (mesh == null) continue;
                Color[] colors = mesh.colors;
                mesh.colors = colors;
                EditorUtility.SetDirty(mesh);
            }
            SceneView.RepaintAll();
            Repaint();
        }
        
        // Drawing Paint UI
        private void DrawPaintUI() {
            EditorGUILayout.BeginVertical("box");
            EditorGUI.BeginChangeCheck();
            bool newPaintEnabled = GUILayout.Toggle(_paintEnabled, "Paint", "Button");
            if (newPaintEnabled != _paintEnabled) {
                _paintEnabled = newPaintEnabled;
                if (_paintEnabled) ForceEnableGizmos();
                if (!_paintEnabled) {
                    if (_isPainting) EndPaint();
                    SaveIfDirty();
                }
                SceneView.RepaintAll();
            }
            if (_paintEnabled) {
                int toolIndex = _tool == ToolMode.Brush ? 0 : _tool == ToolMode.Eraser ? 1 : 2;
                toolIndex = GUILayout.Toolbar(toolIndex, new[] { "Brush", "Eraser", "Fill" });
                _tool = toolIndex == 0 ? ToolMode.Brush : toolIndex == 1 ? ToolMode.Eraser : ToolMode.Fill;
                EditorGUILayout.LabelField("Grass Type");
                _paintChannel = GUILayout.Toolbar(_paintChannel, new[] { "R", "G", "B" });
                if (_tool == ToolMode.Brush) {
                    _brushRadius = Mathf.Max(0.01f, EditorGUILayout.FloatField("Radius", _brushRadius));
                    _brushFeather = Mathf.Clamp01(EditorGUILayout.Slider("Feathering", _brushFeather, 0f, 1f));
                    _brushBrightness = Mathf.Clamp01(EditorGUILayout.Slider("Particle Size", _brushBrightness, 0f, 1f));
                } else if (_tool == ToolMode.Fill) {
                    _fillBrightness = Mathf.Clamp01(EditorGUILayout.Slider("Particle Size", _fillBrightness, 0f, 1f));
                } else if (_tool == ToolMode.Eraser) {
                    _eraserRadius = Mathf.Max(0.01f, EditorGUILayout.FloatField("Radius", _eraserRadius));
                    _eraserFeather = Mathf.Clamp01(EditorGUILayout.Slider("Feathering", _eraserFeather, 0f, 1f));
                }
            }
            if (EditorGUI.EndChangeCheck()) SavePrefs();
            EditorGUILayout.EndVertical();
        }

        // Force enable gizmos
        private static void ForceEnableGizmos() {
            for (int i = 0; i < SceneView.sceneViews.Count; i++) {
                SceneView sceneView = SceneView.sceneViews[i] as SceneView;
                if (sceneView == null) continue;
                sceneView.drawGizmos = true;
            }
        }


        // On Scene GUI
        private void OnSceneGUI() {
            Event e = Event.current;
            if (e == null) return;
            
            if (e.type == EventType.MouseUp && _isPainting) EndPaint();
            if (!_paintEnabled) return;
            
            ParticleSurfaceManager manager = (ParticleSurfaceManager)target;
            if (manager == null || manager.Surfaces == null || manager.Surfaces.Length == 0) return;
            
            if (e.alt || e.control || e.command) return;
            if (e.type == EventType.Layout) {
                HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
                return;
            }

            bool needsHit = e.type == EventType.Repaint || e.type == EventType.MouseDown || e.type == EventType.MouseDrag;
            if (!needsHit) return;
            
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            HitInfo hit = new HitInfo();
            hit.SurfaceIndex = -1;
            bool hitFound = false;
            if (_isPainting && _lastPaintSurfaceIndex >= 0) {
                hitFound = TryPickSurfaceByIndex(manager, ray, _lastPaintSurfaceIndex, out hit);
            }
            if (!hitFound) hitFound = TryPickSurface(manager, ray, out hit);
            if (!hitFound) return;
            _lastPaintSurfaceIndex = hit.SurfaceIndex;
            if (e.type == EventType.Repaint) DrawPaintGizmos(manager, hit);
            
            if (e.button != 0) return;
            
            if (_tool == ToolMode.Fill) {
                if (e.type == EventType.MouseDown) {
                    StartPaint();
                    ApplyFill(manager, hit);
                    EndPaint();
                    e.Use();
                }
                return;
            }
            
            if (e.type == EventType.MouseDown) {
                StartPaint();
                ApplyRadialPaint(manager, hit, _tool == ToolMode.Eraser);
                e.Use();
            } else if (e.type == EventType.MouseDrag && _isPainting) {
                ApplyRadialPaint(manager, hit, _tool == ToolMode.Eraser);
                e.Use();
            }
        }
        
        // Start Paint
        private void StartPaint() {
            if (_isPainting) return;
            _isPainting = true;
            _undoMeshes.Clear();
            Undo.IncrementCurrentGroup();
            _undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Paint Grass");
        }
        
        // End Paint
        private void EndPaint() {
            if (!_isPainting) return;
            _isPainting = false;
            Undo.CollapseUndoOperations(_undoGroup);
            Undo.IncrementCurrentGroup();
            _lastPaintSurfaceIndex = -1;
        }
        
        // Draw with radial brush/eraser every frame
        private void ApplyRadialPaint(ParticleSurfaceManager manager, HitInfo hit, bool eraserMode) {
            Mesh mesh = EnsureSurfaceMeshCopy(manager, hit.SurfaceIndex);
            if (mesh == null) return;
            RegisterUndo(mesh);
            if (hit.Surface == null) return;

            Color[] colors = GetColors(mesh);
            Vector3[] vertices = GetCachedVertices(mesh);
            Matrix4x4 localToWorld = hit.Surface.localToWorldMatrix;
            float radius = eraserMode ? _eraserRadius : _brushRadius;
            float feather = eraserMode ? _eraserFeather : _brushFeather;
            float brushTarget = _brushBrightness;
            float innerRadius = radius * (1f - feather);
            float featherSize = Mathf.Max(1e-6f, radius - innerRadius);
            float sqrRadius = radius * radius;

            GridMeshInfo gridInfo;
            int xMin;
            int xMax;
            int zMin;
            int zMax;
            if (TryGetGridVertexRange(mesh, hit.Surface, hit.Point, radius, out gridInfo, out xMin, out xMax, out zMin, out zMax)) {
                for (int z = zMin; z <= zMax; z++) {
                    int rowOffset = z * gridInfo.Resolution;
                    for (int x = xMin; x <= xMax; x++) {
                        int index = rowOffset + x;
                        Vector3 worldPos = localToWorld.MultiplyPoint3x4(vertices[index]);
                        float sqrDist = (worldPos - hit.Point).sqrMagnitude;
                        if (sqrDist > sqrRadius) continue;
                        float strength = GetRadialStrength(sqrDist, innerRadius, featherSize);
                        ApplyRadialChannel(ref colors[index], _paintChannel, strength, eraserMode, brushTarget);
                    }
                }
            } else {
                for (int i = 0; i < vertices.Length; i++) {
                    Vector3 worldPos = localToWorld.MultiplyPoint3x4(vertices[i]);
                    float sqrDist = (worldPos - hit.Point).sqrMagnitude;
                    if (sqrDist > sqrRadius) continue;
                    float strength = GetRadialStrength(sqrDist, innerRadius, featherSize);
                    ApplyRadialChannel(ref colors[i], _paintChannel, strength, eraserMode, brushTarget);
                }
            }

            mesh.colors = colors;
            MarkDirty(mesh, hit.Surface);
        }

        private static float GetRadialStrength(float sqrDist, float innerRadius, float featherSize) {
            float dist = Mathf.Sqrt(sqrDist);
            if (dist <= innerRadius) return 1f;
            return 1f - (dist - innerRadius) / featherSize;
        }

        private static void ApplyRadialChannel(ref Color c, int channel, float strength, bool eraserMode, float brushTarget) {
            if (eraserMode) {
                ApplyErase(ref c, channel, strength);
                return;
            }

            ApplyBrightness(ref c, channel, brushTarget, strength);
        }
        
        // Fill color
        private void ApplyFill(ParticleSurfaceManager manager, HitInfo hit) {
            Mesh mesh = EnsureSurfaceMeshCopy(manager, hit.SurfaceIndex);
            if (mesh == null) return;
            RegisterUndo(mesh);
            if (hit.Surface == null) return;
            
            Vector3[] vertices = GetCachedVertices(mesh);
            int startIndex = GetClosestVertexIndex(mesh, vertices, hit.Surface, hit.Point);
            if (startIndex < 0) return;
            
            Color[] colors = GetColors(mesh);
            List<int>[] neighbors = GetCachedNeighbors(mesh, vertices.Length);
            if (neighbors == null || neighbors.Length != vertices.Length) return;
            Queue<int> queue = new Queue<int>();
            bool[] visited = new bool[vertices.Length];
            
            visited[startIndex] = true;
            queue.Enqueue(startIndex);
            
            while (queue.Count > 0) {
                int v = queue.Dequeue();
                Color c = colors[v];
                SetChannel(ref c, _paintChannel, _fillBrightness);
                colors[v] = c;
                var list = neighbors[v];
                for (int i = 0; i < list.Count; i++) {
                    int n = list[i];
                    if (visited[n]) continue;
                    visited[n] = true;
                    queue.Enqueue(n);
                }
            }
            
            mesh.colors = colors;
            MarkDirty(mesh, hit.Surface);
        }
        
        private void RegisterUndo(Mesh mesh) {
            if (_undoMeshes.Contains(mesh)) return;
            Undo.RegisterCompleteObjectUndo(mesh, "Paint Grass");
            _undoMeshes.Add(mesh);
            _touchedMeshes.Add(mesh);
        }
        
        private void MarkDirty(Mesh mesh, Transform surface) {
            if (mesh == null) return;
            EditorUtility.SetDirty(mesh);
            _dirtyMeshes.Add(mesh);
            _touchedMeshes.Add(mesh);
            if (surface != null) EditorSceneManager.MarkSceneDirty(surface.gameObject.scene);
        }
        
        // Flushes pending terrain updates before inspector/session transitions.
        private void SaveIfDirty() {
            FlushPendingTerrainRegionUpdatesNow();
        }
        
        private void DrawPaintGizmos(ParticleSurfaceManager manager, HitInfo hit) {
            if (Event.current.type != EventType.Repaint) return;
            if (_tool == ToolMode.Fill) {
                DrawFillGizmos(hit);
                return;
            }
            DrawBrushGizmos(manager, hit);
        }

        private void DrawFillGizmos(HitInfo hit) {
            Handles.color = new Color(1f, 1f, 1f, 0.9f);
            float radius = HandleUtility.GetHandleSize(hit.Point) * 0.08f;
            Handles.SphereHandleCap(0, hit.Point, Quaternion.identity, radius, EventType.Repaint);
        }

        private void DrawBrushGizmos(ParticleSurfaceManager manager, HitInfo hit) {
            if (Event.current.type != EventType.Repaint) return;
            float radius = _tool == ToolMode.Eraser ? _eraserRadius : _brushRadius;
            float feather = _tool == ToolMode.Eraser ? _eraserFeather : _brushFeather;
            Handles.color = new Color(1f, 1f, 1f, 0.9f);
            Handles.DrawWireDisc(hit.Point, hit.Normal, radius);
            float innerRadius = radius * (1f - feather);
            if (innerRadius > 1e-6f) {
                Handles.color = new Color(1f, 1f, 1f, 0.5f);
                Handles.DrawWireDisc(hit.Point, hit.Normal, innerRadius);
            }
            DrawVertexGizmos(manager, hit.SurfaceIndex, hit.Surface, hit.Point, radius);
        }
        
        private void DrawVertexGizmos(ParticleSurfaceManager manager, int surfaceIndex, Transform surface, Vector3 hitPoint, float radius) {
            Mesh mesh = GetSurfaceMeshForPaint(manager, surfaceIndex);
            if (surface == null || mesh == null) return;
            Vector3[] vertices = GetCachedVertices(mesh);
            Matrix4x4 localToWorld = surface.localToWorldMatrix;
            float maxDist = radius * 2f;
            float sqrMax = maxDist * maxDist;
            float sqrRadius = radius * radius;

            GridMeshInfo gridInfo;
            int xMin;
            int xMax;
            int zMin;
            int zMax;
            int drawn = 0;
            if (TryGetGridVertexRange(mesh, surface, hitPoint, maxDist, out gridInfo, out xMin, out xMax, out zMin, out zMax)) {
                for (int z = zMin; z <= zMax; z++) {
                    int rowOffset = z * gridInfo.Resolution;
                    for (int x = xMin; x <= xMax; x++) {
                        int index = rowOffset + x;
                        DrawVertexDot(vertices[index], localToWorld, hitPoint, radius, sqrRadius, sqrMax, ref drawn);
                        if (drawn >= _vertexGizmoDotLimit) return;
                    }
                }
                return;
            }

            int candidateCount = 0;
            int selectedCount = 0;
            uint rng = (uint)mesh.GetInstanceID() * 747796405u + 2891336453u;
            for (int i = 0; i < vertices.Length; i++) {
                Vector3 worldPos = localToWorld.MultiplyPoint3x4(vertices[i]);
                float sqrDist = (worldPos - hitPoint).sqrMagnitude;
                if (sqrDist > sqrMax) continue;

                if (selectedCount < _vertexGizmoDotLimit) {
                    _vertexGizmoSelectionBuffer[selectedCount] = i;
                    selectedCount++;
                } else {
                    rng = rng * 1664525u + 1013904223u;
                    int replaceIndex = (int)(rng % (uint)(candidateCount + 1));
                    if (replaceIndex < _vertexGizmoDotLimit) _vertexGizmoSelectionBuffer[replaceIndex] = i;
                }

                candidateCount++;
            }

            for (int i = 0; i < selectedCount; i++) {
                int index = _vertexGizmoSelectionBuffer[i];
                DrawVertexDot(vertices[index], localToWorld, hitPoint, radius, sqrRadius, sqrMax, ref drawn);
            }
        }

        private static void DrawVertexDot(Vector3 localVertex, Matrix4x4 localToWorld, Vector3 hitPoint, float radius, float sqrRadius, float sqrMax, ref int drawn) {
            Vector3 worldPos = localToWorld.MultiplyPoint3x4(localVertex);
            float sqrDist = (worldPos - hitPoint).sqrMagnitude;
            if (sqrDist > sqrMax) return;
            float alpha;
            if (sqrDist <= sqrRadius) alpha = 1f;
            else {
                float dist = Mathf.Sqrt(sqrDist);
                alpha = 1f - (dist - radius) / radius;
            }
            if (alpha <= 0f) return;
            Handles.color = new Color(1f, 1f, 1f, alpha);
            float size = HandleUtility.GetHandleSize(worldPos) * 0.02f;
            Handles.DotHandleCap(0, worldPos, Quaternion.identity, size, EventType.Repaint);
            drawn++;
        }
        
        private static void ApplyBrightness(ref Color c, int channel, float target, float strength) {
            float current = GetChannel(c, channel);
            if (target > current) {
                float maxAllowed = Mathf.Lerp(0f, target, strength);
                if (maxAllowed > current) current = maxAllowed;
            } else if (target < current) {
                float minAllowed = Mathf.Lerp(1f, target, strength);
                if (minAllowed < current) current = minAllowed;
            }
            SetChannel(ref c, channel, current);
        }

        private static void ApplyErase(ref Color c, int channel, float strength) {
            float current = GetChannel(c, channel);
            SetChannel(ref c, channel, Mathf.Lerp(current, 0f, Mathf.Clamp01(strength)));
        }
        
        private static float GetChannel(Color c, int channel) {
            if (channel == 1) return c.g;
            if (channel == 2) return c.b;
            return c.r;
        }
        
        private static void SetChannel(ref Color c, int channel, float value) {
            if (channel == 1) c.g = value;
            else if (channel == 2) c.b = value;
            else c.r = value;
        }
        
        private static Color[] GetColors(Mesh mesh) {
            Color[] colors = mesh.colors;
            int count = mesh.vertexCount;
            if (colors == null || colors.Length != count) {
                colors = new Color[count];
                for (int i = 0; i < colors.Length; i++) colors[i] = new Color(1f, 1f, 1f, 1f);
            }
            return colors;
        }

    }

}
