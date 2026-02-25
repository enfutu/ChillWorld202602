using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GPUInfiniteGrass {

    public partial class ParticleSurfaceManagerInspector {
        private const double _terrainLiveUpdateInterval = 0.12d;
        private const int _maxPendingTerrainRegionsPerTerrain = 64;
        private const int _terrainUndoResyncFrameCount = 4;

        private static bool _editorCallbacksRegistered;
        private static bool _terrainRegionUpdateQueued;
        private static bool _terrainUndoResyncQueued;
        private static int _terrainUndoResyncFramesRemaining;
        private static bool _cachedManagersDirty = true;
        private static double _nextTerrainRegionUpdateTime;
        private static readonly Dictionary<Terrain, List<RectInt>> _pendingTerrainRegions = new Dictionary<Terrain, List<RectInt>>();
        private static readonly List<ParticleSurfaceManager> _cachedManagers = new List<ParticleSurfaceManager>(8);

        // Marks manager cache dirty when hierarchy or project changes.
        private static void MarkManagersCacheDirty() {
            _cachedManagersDirty = true;
        }

        // Registers all static editor callbacks once.
        private static void RegisterEditorCallbacks() {
            if (_editorCallbacksRegistered) return;
            _editorCallbacksRegistered = true;
            EditorApplication.projectChanged += OnProjectChanged;
            EditorApplication.hierarchyChanged += MarkManagersCacheDirty;
            EditorSceneManager.sceneSaving += OnSceneSaving;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorSceneManager.newSceneCreated += OnNewSceneCreated;
            TerrainCallbacks.heightmapChanged += OnTerrainHeightmapChanged;
            Undo.undoRedoPerformed += OnGlobalUndoRedo;
            AssemblyReloadEvents.beforeAssemblyReload += UnregisterEditorCallbacks;
        }

        // Unregisters static editor callbacks before domain reload.
        private static void UnregisterEditorCallbacks() {
            if (!_editorCallbacksRegistered) return;
            _editorCallbacksRegistered = false;
            EditorApplication.projectChanged -= OnProjectChanged;
            EditorApplication.hierarchyChanged -= MarkManagersCacheDirty;
            EditorSceneManager.sceneSaving -= OnSceneSaving;
            EditorSceneManager.sceneOpened -= OnSceneOpened;
            EditorSceneManager.newSceneCreated -= OnNewSceneCreated;
            TerrainCallbacks.heightmapChanged -= OnTerrainHeightmapChanged;
            Undo.undoRedoPerformed -= OnGlobalUndoRedo;
            AssemblyReloadEvents.beforeAssemblyReload -= UnregisterEditorCallbacks;

            StopTerrainRegionUpdateQueue();
            StopTerrainUndoResyncQueue();
            _pendingTerrainRegions.Clear();
            _cachedManagers.Clear();
            _cachedManagersDirty = true;
        }

        // Handles terrain sculpt changes and accumulates changed heightmap regions.
        private static void OnTerrainHeightmapChanged(Terrain terrain, RectInt region, bool synched) {
            if (Application.isPlaying || terrain == null || terrain.terrainData == null) return;
            if (region.width <= 0 || region.height <= 0) return;

            EnqueueTerrainRegion(terrain, region);
            if (synched) {
                FlushPendingTerrainRegionUpdatesNow();
                return;
            }

            QueueTerrainRegionUpdate();
        }

        // Saves dirty generated mesh assets only when scene is explicitly saved.
        private static void OnSceneSaving(Scene scene, string path) {
            if (Application.isPlaying) return;
            FlushPendingTerrainRegionUpdatesNow();
            SaveDirtyMeshesNow();
        }

        // Keeps manager cache valid when scene is opened.
        private static void OnSceneOpened(Scene scene, OpenSceneMode mode) {
            MarkManagersCacheDirty();
        }

        // Keeps manager cache valid when a new scene is created.
        private static void OnNewSceneCreated(Scene scene, NewSceneSetup setup, NewSceneMode mode) {
            MarkManagersCacheDirty();
        }

        // Global undo handler keeps terrain-derived surface meshes in sync even when inspector is not selected.
        private static void OnGlobalUndoRedo() {
            if (Application.isPlaying) return;

            MarkManagersCacheDirty();
            FlushPendingTerrainRegionUpdatesNow();
            ResyncAllTerrainSurfaceMeshesFromTerrain();
            QueueTerrainUndoRefresh();

            ClearGeometryCaches();
            SceneView.RepaintAll();
        }

        // Adds changed terrain region and merges overlapping/adjacent regions.
        private static void EnqueueTerrainRegion(Terrain terrain, RectInt region) {
            if (region.width <= 0 || region.height <= 0) return;

            List<RectInt> regions;
            if (!_pendingTerrainRegions.TryGetValue(terrain, out regions)) {
                regions = new List<RectInt>(4);
                _pendingTerrainRegions.Add(terrain, regions);
            }

            RectInt merged = region;
            bool mergedAny;
            do {
                mergedAny = false;
                for (int i = regions.Count - 1; i >= 0; i--) {
                    RectInt existing = regions[i];
                    if (!AreRegionsConnected(existing, merged)) continue;
                    merged = UnionRegions(existing, merged);
                    regions.RemoveAt(i);
                    mergedAny = true;
                }
            } while (mergedAny);

            regions.Add(merged);
            if (regions.Count <= _maxPendingTerrainRegionsPerTerrain) return;

            // Fallback guard: if region list grows too much, collapse into one region.
            RectInt collapsed = regions[0];
            for (int i = 1; i < regions.Count; i++) {
                collapsed = UnionRegions(collapsed, regions[i]);
            }
            regions.Clear();
            regions.Add(collapsed);
        }

        // Returns true when regions overlap or touch by one pixel.
        private static bool AreRegionsConnected(RectInt a, RectInt b) {
            return a.xMin <= b.xMax + 1
                && a.xMax + 1 >= b.xMin
                && a.yMin <= b.yMax + 1
                && a.yMax + 1 >= b.yMin;
        }

        // Builds minimal bounding union of two regions.
        private static RectInt UnionRegions(RectInt a, RectInt b) {
            int xMin = Mathf.Min(a.xMin, b.xMin);
            int yMin = Mathf.Min(a.yMin, b.yMin);
            int xMax = Mathf.Max(a.xMax, b.xMax);
            int yMax = Mathf.Max(a.yMax, b.yMax);
            return new RectInt(xMin, yMin, Mathf.Max(0, xMax - xMin), Mathf.Max(0, yMax - yMin));
        }

        // Queues throttled region updates while terrain tool is dragging.
        private static void QueueTerrainRegionUpdate() {
            if (_terrainRegionUpdateQueued) return;
            _terrainRegionUpdateQueued = true;
            _nextTerrainRegionUpdateTime = EditorApplication.timeSinceStartup + _terrainLiveUpdateInterval;
            EditorApplication.update += ProcessTerrainRegionUpdateQueue;
        }

        // Processes queued region updates at throttled interval.
        private static void ProcessTerrainRegionUpdateQueue() {
            if (EditorApplication.timeSinceStartup < _nextTerrainRegionUpdateTime) return;
            StopTerrainRegionUpdateQueue();
            FlushPendingTerrainRegionUpdates();
        }

        // Queues several post-undo resync passes to catch delayed terrain state updates.
        private static void QueueTerrainUndoRefresh() {
            _terrainUndoResyncFramesRemaining = Mathf.Max(_terrainUndoResyncFramesRemaining, _terrainUndoResyncFrameCount);
            if (_terrainUndoResyncQueued) return;
            _terrainUndoResyncQueued = true;
            EditorApplication.update += ProcessTerrainUndoResyncQueue;
        }

        // Runs forced terrain->mesh resync for a few editor frames after undo/redo.
        private static void ProcessTerrainUndoResyncQueue() {
            if (_terrainUndoResyncFramesRemaining <= 0) {
                StopTerrainUndoResyncQueue();
                return;
            }

            MarkManagersCacheDirty();
            FlushPendingTerrainRegionUpdatesNow();
            ResyncAllTerrainSurfaceMeshesFromTerrain();
            _terrainUndoResyncFramesRemaining--;

            if (_terrainUndoResyncFramesRemaining > 0) return;
            StopTerrainUndoResyncQueue();
        }

        // Stops throttled terrain region update queue.
        private static void StopTerrainRegionUpdateQueue() {
            EditorApplication.update -= ProcessTerrainRegionUpdateQueue;
            _terrainRegionUpdateQueued = false;
        }

        // Stops multi-frame undo resync queue.
        private static void StopTerrainUndoResyncQueue() {
            EditorApplication.update -= ProcessTerrainUndoResyncQueue;
            _terrainUndoResyncQueued = false;
            _terrainUndoResyncFramesRemaining = 0;
        }

        // Flushes all pending terrain region updates immediately.
        private static void FlushPendingTerrainRegionUpdatesNow() {
            StopTerrainRegionUpdateQueue();
            FlushPendingTerrainRegionUpdates();
        }

        // Applies all accumulated terrain regions to all affected managers.
        private static bool FlushPendingTerrainRegionUpdates() {
            if (_pendingTerrainRegions.Count == 0) return false;

            List<ParticleSurfaceManager> managers = GetCachedManagers();
            bool anyChanged = false;
            foreach (var pair in _pendingTerrainRegions) {
                Terrain terrain = pair.Key;
                List<RectInt> regions = pair.Value;
                if (terrain == null || terrain.terrainData == null || regions == null || regions.Count == 0) continue;
                if (UpdateTerrainSurfaceMeshes(terrain, regions, managers)) anyChanged = true;
                regions.Clear();
            }

            _pendingTerrainRegions.Clear();
            return anyChanged;
        }

        // Returns cached list of managers and rebuilds cache when needed.
        private static List<ParticleSurfaceManager> GetCachedManagers() {
            if (_cachedManagersDirty) {
                _cachedManagers.Clear();
                ParticleSurfaceManager[] managers = Object.FindObjectsOfType<ParticleSurfaceManager>();
                for (int i = 0; i < managers.Length; i++) {
                    ParticleSurfaceManager manager = managers[i];
                    if (manager == null) continue;
                    _cachedManagers.Add(manager);
                }
                _cachedManagersDirty = false;
            }
            return _cachedManagers;
        }

        // Updates all manager meshes that use the changed terrain.
        private static bool UpdateTerrainSurfaceMeshes(Terrain terrain, List<RectInt> regions, List<ParticleSurfaceManager> managers) {
            if (terrain == null || terrain.terrainData == null || regions == null || regions.Count == 0 || managers == null || managers.Count == 0) return false;
            bool anyChanged = false;
            for (int i = 0; i < managers.Count; i++) {
                ParticleSurfaceManager manager = managers[i];
                if (manager == null || manager.Surfaces == null || manager.Surfaces.Length == 0) continue;
                if (UpdateTerrainSurfaceMeshesForManager(manager, terrain, regions)) anyChanged = true;
            }
            return anyChanged;
        }

        // Updates affected terrain meshes for one manager.
        private static bool UpdateTerrainSurfaceMeshesForManager(ParticleSurfaceManager manager, Terrain terrain, List<RectInt> regions) {
            bool changed = false;
            for (int i = 0; i < manager.Surfaces.Length; i++) {
                Transform surface = manager.Surfaces[i];
                if (surface == null || !surface.gameObject.activeInHierarchy) continue;
                Terrain surfaceTerrain = surface.GetComponent<Terrain>();
                if (surfaceTerrain != terrain) continue;
                Mesh surfaceMesh = GetAssignedSurfaceMesh(manager, i);
                if (!TryUpdateTerrainMeshRegions(surfaceTerrain, surfaceMesh, regions)) continue;
                changed = true;
            }
            return changed;
        }

        // Forces full terrain resync for all managers (used by undo fallback).
        private static bool ResyncAllTerrainSurfaceMeshesFromTerrain() {
            List<ParticleSurfaceManager> managers = GetCachedManagers();
            bool anyChanged = false;
            for (int i = 0; i < managers.Count; i++) {
                ParticleSurfaceManager manager = managers[i];
                if (manager == null || manager.Surfaces == null || manager.Surfaces.Length == 0) continue;
                if (ResyncTerrainSurfaceMeshesForManager(manager)) anyChanged = true;
            }
            return anyChanged;
        }

        // Forces full terrain resync for one manager.
        private static bool ResyncTerrainSurfaceMeshesForManager(ParticleSurfaceManager manager) {
            bool changed = false;
            for (int i = 0; i < manager.Surfaces.Length; i++) {
                Transform surface = manager.Surfaces[i];
                if (surface == null || !surface.gameObject.activeInHierarchy) continue;
                Terrain terrain = surface.GetComponent<Terrain>();
                if (terrain == null || terrain.terrainData == null) continue;
                Mesh surfaceMesh = GetAssignedSurfaceMesh(manager, i);
                if (!TryUpdateTerrainMeshFull(terrain, surfaceMesh)) continue;
                changed = true;
            }
            return changed;
        }

        // Updates full terrain-driven mesh from full heightmap region.
        private static bool TryUpdateTerrainMeshFull(Terrain terrain, Mesh mesh) {
            if (terrain == null || terrain.terrainData == null || mesh == null) return false;
            int meshResolution;
            if (!TryGetSquareGridResolution(mesh.vertexCount, out meshResolution)) return false;

            Vector3[] vertices = GetCachedVertices(mesh);
            if (vertices == null || vertices.Length != mesh.vertexCount) return false;

            int heightmapResolution = terrain.terrainData.heightmapResolution;
            if (heightmapResolution < 2) return false;

            RectInt fullRegion = new RectInt(0, 0, heightmapResolution, heightmapResolution);
            int xMin;
            int xMax;
            int zMin;
            int zMax;
            if (!ApplyTerrainHeightRegionToVertices(terrain.terrainData, meshResolution, vertices, fullRegion, out xMin, out xMax, out zMin, out zMax)) return false;

            mesh.vertices = vertices;
            UpdateTerrainMeshNormals(mesh, vertices, meshResolution, xMin, xMax, zMin, zMax);
            mesh.RecalculateBounds();
            TrackMeshDirty(mesh);
            return true;
        }

        // Updates only changed terrain regions on already assigned terrain mesh.
        private static bool TryUpdateTerrainMeshRegions(Terrain terrain, Mesh mesh, List<RectInt> regions) {
            if (terrain == null || terrain.terrainData == null || mesh == null) return false;
            int meshResolution;
            if (!TryGetSquareGridResolution(mesh.vertexCount, out meshResolution)) return false;
            if (regions == null || regions.Count == 0) return false;

            Vector3[] vertices = GetCachedVertices(mesh);
            if (vertices == null || vertices.Length != mesh.vertexCount) return false;

            TerrainData terrainData = terrain.terrainData;
            bool changed = false;
            int xMin = meshResolution;
            int xMax = -1;
            int zMin = meshResolution;
            int zMax = -1;
            for (int i = 0; i < regions.Count; i++) {
                int regionXMin;
                int regionXMax;
                int regionZMin;
                int regionZMax;
                if (!ApplyTerrainHeightRegionToVertices(terrainData, meshResolution, vertices, regions[i], out regionXMin, out regionXMax, out regionZMin, out regionZMax)) continue;
                changed = true;
                if (regionXMin < xMin) xMin = regionXMin;
                if (regionXMax > xMax) xMax = regionXMax;
                if (regionZMin < zMin) zMin = regionZMin;
                if (regionZMax > zMax) zMax = regionZMax;
            }

            if (!changed) return false;
            mesh.vertices = vertices;
            UpdateTerrainMeshNormals(mesh, vertices, meshResolution, xMin, xMax, zMin, zMax);
            mesh.RecalculateBounds();
            TrackMeshDirty(mesh);
            return true;
        }

        // Applies one terrain region to mesh vertices by sampling minimal required height block.
        private static bool ApplyTerrainHeightRegionToVertices(TerrainData terrainData, int meshResolution, Vector3[] vertices, RectInt region, out int xMin, out int xMax, out int zMin, out int zMax) {
            xMin = 0;
            xMax = -1;
            zMin = 0;
            zMax = -1;
            if (terrainData == null || vertices == null || vertices.Length == 0) return false;
            if (region.width <= 0 || region.height <= 0) return false;

            int heightmapResolution = terrainData.heightmapResolution;
            if (heightmapResolution < 2) return false;
            int hmMax = heightmapResolution - 1;

            int hmXMin = Mathf.Clamp(region.xMin, 0, hmMax);
            int hmXMaxExclusive = Mathf.Clamp(region.xMax, 0, heightmapResolution);
            int hmYMin = Mathf.Clamp(region.yMin, 0, hmMax);
            int hmYMaxExclusive = Mathf.Clamp(region.yMax, 0, heightmapResolution);
            if (hmXMaxExclusive <= hmXMin || hmYMaxExclusive <= hmYMin) return false;

            // Expand by one vertex around region to keep interpolation stable at borders.
            float hmToMesh = (meshResolution - 1f) / hmMax;
            xMin = Mathf.Clamp(Mathf.FloorToInt(hmXMin * hmToMesh) - 1, 0, meshResolution - 1);
            xMax = Mathf.Clamp(Mathf.CeilToInt((hmXMaxExclusive - 1) * hmToMesh) + 1, 0, meshResolution - 1);
            zMin = Mathf.Clamp(Mathf.FloorToInt(hmYMin * hmToMesh) - 1, 0, meshResolution - 1);
            zMax = Mathf.Clamp(Mathf.CeilToInt((hmYMaxExclusive - 1) * hmToMesh) + 1, 0, meshResolution - 1);
            if (xMax < xMin || zMax < zMin) return false;

            float meshToHm = hmMax / Mathf.Max(meshResolution - 1f, 1f);
            int sampleHmXMin = Mathf.Clamp(Mathf.FloorToInt(xMin * meshToHm), 0, hmMax);
            int sampleHmXMax = Mathf.Clamp(Mathf.CeilToInt(xMax * meshToHm) + 1, 0, hmMax);
            int sampleHmYMin = Mathf.Clamp(Mathf.FloorToInt(zMin * meshToHm), 0, hmMax);
            int sampleHmYMax = Mathf.Clamp(Mathf.CeilToInt(zMax * meshToHm) + 1, 0, hmMax);
            int sampleWidth = sampleHmXMax - sampleHmXMin + 1;
            int sampleHeight = sampleHmYMax - sampleHmYMin + 1;
            if (sampleWidth < 1 || sampleHeight < 1) return false;

            float[,] heights = terrainData.GetHeights(sampleHmXMin, sampleHmYMin, sampleWidth, sampleHeight);
            if (heights == null) return false;

            bool changed = false;
            float heightScale = terrainData.size.y;
            for (int z = zMin; z <= zMax; z++) {
                float hmY = z * meshToHm - sampleHmYMin;
                int y0 = Mathf.Clamp(Mathf.FloorToInt(hmY), 0, sampleHeight - 1);
                int y1 = Mathf.Min(y0 + 1, sampleHeight - 1);
                float ty = Mathf.Clamp01(hmY - y0);
                int row = z * meshResolution;
                for (int x = xMin; x <= xMax; x++) {
                    int index = row + x;
                    float hmX = x * meshToHm - sampleHmXMin;
                    int x0 = Mathf.Clamp(Mathf.FloorToInt(hmX), 0, sampleWidth - 1);
                    int x1 = Mathf.Min(x0 + 1, sampleWidth - 1);
                    float tx = Mathf.Clamp01(hmX - x0);

                    // Bilinear interpolation in sampled height block.
                    float h00 = heights[y0, x0];
                    float h10 = heights[y0, x1];
                    float h01 = heights[y1, x0];
                    float h11 = heights[y1, x1];
                    float h0 = Mathf.Lerp(h00, h10, tx);
                    float h1 = Mathf.Lerp(h01, h11, tx);
                    float newY = Mathf.Lerp(h0, h1, ty) * heightScale;

                    if (Mathf.Abs(vertices[index].y - newY) <= 1e-6f) continue;
                    vertices[index].y = newY;
                    changed = true;
                }
            }

            return changed;
        }

        // Updates normals only in changed grid area to keep terrain mesh shading coherent without full-mesh recompute.
        private static void UpdateTerrainMeshNormals(Mesh mesh, Vector3[] vertices, int meshResolution, int xMin, int xMax, int zMin, int zMax) {
            if (mesh == null || vertices == null || meshResolution < 2) return;
            if (xMax < xMin || zMax < zMin) return;

            xMin = Mathf.Max(0, xMin - 1);
            xMax = Mathf.Min(meshResolution - 1, xMax + 1);
            zMin = Mathf.Max(0, zMin - 1);
            zMax = Mathf.Min(meshResolution - 1, zMax + 1);
            if (xMax < xMin || zMax < zMin) return;

            Vector3[] normals = GetCachedNormals(mesh, vertices.Length);
            if (normals == null || normals.Length != vertices.Length) return;

            for (int z = zMin; z <= zMax; z++) {
                int zPrev = z > 0 ? z - 1 : z;
                int zNext = z < meshResolution - 1 ? z + 1 : z;
                int row = z * meshResolution;
                int prevRow = zPrev * meshResolution;
                int nextRow = zNext * meshResolution;
                for (int x = xMin; x <= xMax; x++) {
                    int xPrev = x > 0 ? x - 1 : x;
                    int xNext = x < meshResolution - 1 ? x + 1 : x;

                    Vector3 dx = vertices[row + xNext] - vertices[row + xPrev];
                    Vector3 dz = vertices[nextRow + x] - vertices[prevRow + x];
                    Vector3 normal = Vector3.Cross(dz, dx);
                    normals[row + x] = normal.sqrMagnitude > 1e-12f ? normal.normalized : Vector3.up;
                }
            }

            mesh.normals = normals;
        }

        // Marks mesh as dirty in memory without forcing immediate disk save.
        private static void TrackMeshDirty(Mesh mesh) {
            if (mesh == null) return;
            if (_dirtyMeshes.Add(mesh)) EditorUtility.SetDirty(mesh);
            _touchedMeshes.Add(mesh);
        }

        // Saves dirty generated mesh assets to disk.
        private static void SaveDirtyMeshesNow() {
            if (_dirtyMeshes.Count == 0) return;

            Mesh[] snapshot = new Mesh[_dirtyMeshes.Count];
            _dirtyMeshes.CopyTo(snapshot);
            for (int i = 0; i < snapshot.Length; i++) {
                Mesh mesh = snapshot[i];
                _dirtyMeshes.Remove(mesh);
                if (mesh == null || !AssetDatabase.Contains(mesh)) continue;
                AssetDatabase.SaveAssetIfDirty(mesh);
            }

        }
    }
}
