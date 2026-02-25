using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace GPUInfiniteGrass {

    public partial class ParticleSurfaceManagerInspector {

        // Gets currently assigned paint mesh for surface index
        private static Mesh GetAssignedSurfaceMesh(ParticleSurfaceManager manager, int surfaceIndex) {
            if (manager == null || manager.SurfaceMeshes == null || surfaceIndex < 0 || surfaceIndex >= manager.SurfaceMeshes.Length) return null;
            return manager.SurfaceMeshes[surfaceIndex];
        }

        // Returns paint mesh that should be used by scene painting tools.
        private static Mesh GetSurfaceMeshForPaint(ParticleSurfaceManager manager, int surfaceIndex) {
            return GetAssignedSurfaceMesh(manager, surfaceIndex);
        }

        // Returns currently assigned mesh without creating automatic fallback copies.
        private static Mesh EnsureSurfaceMeshCopy(ParticleSurfaceManager manager, int surfaceIndex) {
            if (manager == null || manager.Surfaces == null || surfaceIndex < 0 || surfaceIndex >= manager.Surfaces.Length) return null;
            return GetAssignedSurfaceMesh(manager, surfaceIndex);
        }

        // Assigns mesh into synced SurfaceMeshes slot and marks owner dirty.
        private static void AssignSurfaceMesh(ParticleSurfaceManager manager, int surfaceIndex, Mesh mesh) {
            if (manager == null || surfaceIndex < 0) return;
            int surfaceCount = manager.Surfaces != null ? manager.Surfaces.Length : 0;
            if (surfaceIndex >= surfaceCount) return;
            Mesh[] currentMeshes = manager.SurfaceMeshes;
            if (currentMeshes == null || currentMeshes.Length != surfaceCount) {
                Mesh[] resized = surfaceCount == 0 ? new Mesh[0] : new Mesh[surfaceCount];
                if (currentMeshes != null) {
                    int copyCount = Mathf.Min(currentMeshes.Length, resized.Length);
                    for (int i = 0; i < copyCount; i++) resized[i] = currentMeshes[i];
                }
                currentMeshes = resized;
            }
            if (currentMeshes[surfaceIndex] == mesh && manager.SurfaceMeshes == currentMeshes) return;
            Undo.RecordObject(manager, "Regenerate Surface Mesh");
            currentMeshes[surfaceIndex] = mesh;
            manager.SurfaceMeshes = currentMeshes;
            EditorUtility.SetDirty(manager);
            if (manager.gameObject != null) EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        }

        // Creates mesh asset copy and tracks it in touched set for undo/refresh path.
        private static Mesh CreateAndTrackSurfaceMeshCopy(Transform surface) {
            Mesh mesh = CreateSurfaceMeshAssetCopy(surface);
            if (mesh != null) _touchedMeshes.Add(mesh);
            return mesh;
        }

        // Checks whether regenerated mesh can safely overwrite old mesh without paint loss.
        private static bool CanRegenerateWithoutPaintLoss(Mesh previousMesh, Mesh regeneratedMesh) {
            if (previousMesh == null || regeneratedMesh == null) return false;
            if (!IsMeshEditable(previousMesh)) return false;
            if (previousMesh.vertexCount != regeneratedMesh.vertexCount) return false;

            // Compare merged topology/index streams to guarantee stable vertex mapping.
            MeshTopology previousTopology;
            MeshTopology regeneratedTopology;
            int[] previousIndices;
            int[] regeneratedIndices;
            if (!TryGetComparableIndexStream(previousMesh, out previousTopology, out previousIndices)) return false;
            if (!TryGetComparableIndexStream(regeneratedMesh, out regeneratedTopology, out regeneratedIndices)) return false;
            if (previousTopology != regeneratedTopology) return false;
            if (previousIndices.Length != regeneratedIndices.Length) return false;
            for (int i = 0; i < previousIndices.Length; i++) {
                if (previousIndices[i] != regeneratedIndices[i]) return false;
            }

            return true;
        }

        // Builds one comparable index stream across all submeshes.
        private static bool TryGetComparableIndexStream(Mesh mesh, out MeshTopology topology, out int[] indices) {
            topology = MeshTopology.Triangles;
            indices = null;
            if (mesh == null || mesh.subMeshCount <= 0) return false;

            topology = mesh.GetTopology(0);
            int subMeshCount = mesh.subMeshCount;
            int totalIndexCount = 0;
            for (int i = 0; i < subMeshCount; i++) {
                if (mesh.GetTopology(i) != topology) return false;
                totalIndexCount += (int)mesh.GetIndexCount(i);
            }

            int[] merged = new int[totalIndexCount];
            int offset = 0;
            for (int i = 0; i < subMeshCount; i++) {
                int[] subMeshIndices = mesh.GetIndices(i);
                if (subMeshIndices == null) return false;
                System.Array.Copy(subMeshIndices, 0, merged, offset, subMeshIndices.Length);
                offset += subMeshIndices.Length;
            }

            indices = merged;
            return true;
        }

        // Returns true when mesh asset can be modified in-place.
        private static bool IsMeshEditable(Mesh mesh) {
            if (mesh == null) return false;
            if (!AssetDatabase.Contains(mesh)) return true;
            string path = AssetDatabase.GetAssetPath(mesh);
            if (string.IsNullOrEmpty(path)) return true;
            AssetImporter importer = AssetImporter.GetAtPath(path);
            return !(importer is ModelImporter);
        }

        // Overwrites existing mesh asset data preserving original asset reference.
        private static void OverwriteMeshAssetInPlace(Mesh destinationMesh, Mesh sourceMesh) {
            if (destinationMesh == null || sourceMesh == null) return;
            Undo.RegisterCompleteObjectUndo(destinationMesh, "Regenerate Surface Mesh");
            destinationMesh.Clear(false);
            destinationMesh.indexFormat = sourceMesh.indexFormat;

            destinationMesh.vertices = sourceMesh.vertices;
            destinationMesh.normals = sourceMesh.normals;
            destinationMesh.tangents = sourceMesh.tangents;
            destinationMesh.colors = sourceMesh.colors;
            destinationMesh.uv = sourceMesh.uv;
            destinationMesh.uv2 = sourceMesh.uv2;
            destinationMesh.uv3 = sourceMesh.uv3;
            destinationMesh.uv4 = sourceMesh.uv4;

            destinationMesh.subMeshCount = sourceMesh.subMeshCount;
            for (int i = 0; i < sourceMesh.subMeshCount; i++) {
                destinationMesh.SetIndices(sourceMesh.GetIndices(i), sourceMesh.GetTopology(i), i, false);
            }
            destinationMesh.bounds = sourceMesh.bounds;

            EditorUtility.SetDirty(destinationMesh);
            AssetDatabase.SaveAssetIfDirty(destinationMesh);
        }

        // Predicts generated vertex count for current surface and terrain ratio setting.
        private static int GetPlannedSurfaceMeshVertexCount(Transform surface, int terrainRatioIndex) {
            if (surface == null) return 0;
            MeshFilter meshFilter = surface.GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null) return meshFilter.sharedMesh.vertexCount;
            Terrain terrain = surface.GetComponent<Terrain>();
            if (terrain == null || terrain.terrainData == null) return 0;
            int terrainResolution = GetConfiguredTerrainResolution(terrain, terrainRatioIndex);
            if (terrainResolution < 2) return 0;
            return terrainResolution * terrainResolution;
        }

        // Transfers paint mask colors to target mesh by copy or terrain interpolation.
        private static void TransferPaintMask(Mesh sourceMesh, Mesh targetMesh, bool allowTerrainInterpolation) {
            if (sourceMesh == null || targetMesh == null) return;
            bool transferred = TryCopyPaintMaskByVertexIndex(sourceMesh, targetMesh);
            if (!transferred && allowTerrainInterpolation) transferred = TryInterpolateTerrainPaintMask(sourceMesh, targetMesh);
            if (!transferred) return;
            if (!AssetDatabase.Contains(targetMesh)) return;
            EditorUtility.SetDirty(targetMesh);
            AssetDatabase.SaveAssetIfDirty(targetMesh);
        }

        // Copies paint colors by direct vertex index mapping.
        private static bool TryCopyPaintMaskByVertexIndex(Mesh sourceMesh, Mesh targetMesh) {
            if (sourceMesh == null || targetMesh == null) return false;
            int targetVertexCount = targetMesh.vertexCount;
            if (targetVertexCount <= 0 || sourceMesh.vertexCount != targetVertexCount) return false;
            Color[] sourceColors = GetColors(sourceMesh);
            if (sourceColors == null || sourceColors.Length != targetVertexCount) return false;
            Color[] targetColors = new Color[targetVertexCount];
            System.Array.Copy(sourceColors, targetColors, targetVertexCount);
            targetMesh.colors = targetColors;
            return true;
        }

        // Interpolates terrain paint colors between different terrain mesh resolutions.
        private static bool TryInterpolateTerrainPaintMask(Mesh sourceMesh, Mesh targetMesh) {
            if (sourceMesh == null || targetMesh == null) return false;
            int sourceResolution;
            int targetResolution;
            if (!TryGetSquareGridResolution(sourceMesh.vertexCount, out sourceResolution)) return false;
            if (!TryGetSquareGridResolution(targetMesh.vertexCount, out targetResolution)) return false;
            if (sourceResolution < 2 || targetResolution < 2) return false;

            Color[] sourceColors = GetColors(sourceMesh);
            if (sourceColors == null || sourceColors.Length != sourceMesh.vertexCount) return false;

            Color[] targetColors = new Color[targetMesh.vertexCount];
            float sourceMax = sourceResolution - 1f;
            float targetInv = 1f / Mathf.Max(targetResolution - 1f, 1f);
            for (int z = 0; z < targetResolution; z++) {
                float v = z * targetInv;
                float sourceZ = v * sourceMax;
                int z0 = Mathf.Clamp(Mathf.FloorToInt(sourceZ), 0, sourceResolution - 1);
                int z1 = Mathf.Min(z0 + 1, sourceResolution - 1);
                float tz = sourceZ - z0;
                int row0 = z0 * sourceResolution;
                int row1 = z1 * sourceResolution;
                for (int x = 0; x < targetResolution; x++) {
                    float u = x * targetInv;
                    float sourceX = u * sourceMax;
                    int x0 = Mathf.Clamp(Mathf.FloorToInt(sourceX), 0, sourceResolution - 1);
                    int x1 = Mathf.Min(x0 + 1, sourceResolution - 1);
                    float tx = sourceX - x0;

                    // Bilinear interpolation in source grid color space.
                    Color c00 = sourceColors[row0 + x0];
                    Color c10 = sourceColors[row0 + x1];
                    Color c01 = sourceColors[row1 + x0];
                    Color c11 = sourceColors[row1 + x1];
                    Color c0 = Color.Lerp(c00, c10, tx);
                    Color c1 = Color.Lerp(c01, c11, tx);
                    targetColors[z * targetResolution + x] = Color.Lerp(c0, c1, tz);
                }
            }

            targetMesh.colors = targetColors;
            return true;
        }

        // Detects square-grid resolution from vertex count.
        private static bool TryGetSquareGridResolution(int vertexCount, out int resolution) {
            resolution = 0;
            if (vertexCount < 4) return false;
            int candidate = Mathf.RoundToInt(Mathf.Sqrt(vertexCount));
            if (candidate < 2 || candidate * candidate != vertexCount) return false;
            resolution = candidate;
            return true;
        }

        // Syncs array length and clears invalid rows
        private static void SyncSurfaceMeshesArray(ParticleSurfaceManager manager) {
            if (manager == null) return;
            Transform[] surfaces = manager.Surfaces;
            int surfaceCount = surfaces != null ? surfaces.Length : 0;
            Mesh[] currentMeshes = manager.SurfaceMeshes;
            bool changed = currentMeshes == null || currentMeshes.Length != surfaceCount;
            if (changed) {
                Mesh[] resized = surfaceCount == 0 ? new Mesh[0] : new Mesh[surfaceCount];
                if (currentMeshes != null) {
                    int copyCount = Mathf.Min(currentMeshes.Length, resized.Length);
                    for (int i = 0; i < copyCount; i++) resized[i] = currentMeshes[i];
                }
                currentMeshes = resized;
            }

            for (int i = 0; i < surfaceCount; i++) {
                Transform surface = surfaces[i];
                if (surface == null) {
                    if (currentMeshes[i] != null) {
                        currentMeshes[i] = null;
                        changed = true;
                    }
                    continue;
                }
            }

            if (!changed) return;
            manager.SurfaceMeshes = currentMeshes;
            EditorUtility.SetDirty(manager);
            if (manager.gameObject != null) EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        }

        // Sanitizes source object names to valid asset file names.
        private static string SanitizeFileName(string value) {
            if (string.IsNullOrEmpty(value)) return "Surface";
            char[] chars = value.ToCharArray();
            char[] invalid = System.IO.Path.GetInvalidFileNameChars();
            for (int i = 0; i < chars.Length; i++) {
                for (int j = 0; j < invalid.Length; j++) {
                    if (chars[i] != invalid[j]) continue;
                    chars[i] = '_';
                    break;
                }
            }
            return new string(chars);
        }

        // Builds unique generated mesh asset path with random suffix.
        private static string GetSurfaceMeshCopyPath(Transform surface) {
            string sourceName = surface != null ? surface.name : "Surface";
            string safeName = SanitizeFileName(sourceName);
            string randomHash = System.Guid.NewGuid().ToString("N").Substring(0, 16);
            return AssetDatabase.GenerateUniqueAssetPath(_meshCopyFolder + "/" + safeName + "_" + randomHash + ".asset");
        }

        // Creates mesh instance and prepares it for paint usage.
        private static Mesh CreatePreparedSurfaceMesh(Transform surface) {
            if (surface == null) return null;
            Mesh copy = CreateSurfaceMeshInstance(surface);
            if (copy == null) return null;
            PreparePaintMeshCopy(copy);
            return copy;
        }

        // Saves prepared mesh as generated asset.
        private static Mesh CreateSurfaceMeshAssetFromPrepared(Transform surface, Mesh preparedMesh) {
            if (surface == null || preparedMesh == null) return null;
            EnsureFolder(_meshCopyFolder);
            string path = GetSurfaceMeshCopyPath(surface);
            preparedMesh.name = System.IO.Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(preparedMesh, path);
            ClearGeometryCaches();
            return preparedMesh;
        }

        // Creates generated mesh asset directly from surface source.
        private static Mesh CreateSurfaceMeshAssetCopy(Transform surface) {
            Mesh preparedMesh = CreatePreparedSurfaceMesh(surface);
            if (preparedMesh == null) return null;
            return CreateSurfaceMeshAssetFromPrepared(surface, preparedMesh);
        }

        // Creates mesh instance from MeshFilter or generated Terrain mesh.
        private static Mesh CreateSurfaceMeshInstance(Transform surface) {
            if (surface == null) return null;
            MeshFilter meshFilter = surface.GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null) return Instantiate(meshFilter.sharedMesh);
            Terrain terrain = surface.GetComponent<Terrain>();
            if (terrain == null || terrain.terrainData == null) return null;
            int terrainResolution = GetConfiguredTerrainResolution(terrain);
            if (terrainResolution < 2) return null;
            return CreateMeshFromTerrain(terrain, terrainResolution);
        }

        // Gets configured terrain resolution using global ratio setting.
        private static int GetConfiguredTerrainResolution(Terrain terrain) {
            return GetConfiguredTerrainResolution(terrain, _terrainResolutionRatioIndex);
        }

        // Converts terrain heightmap resolution to target mesh resolution for selected ratio.
        private static int GetConfiguredTerrainResolution(Terrain terrain, int ratioIndex) {
            if (terrain == null || terrain.terrainData == null) return 0;
            int fullResolution = terrain.terrainData.heightmapResolution;
            int clampedRatio = ClampTerrainResolutionRatio(ratioIndex);
            int divider = Mathf.Max(1, _terrainResolutionDividers[clampedRatio]);
            int sourceQuads = Mathf.Max(1, fullResolution - 1);
            int targetQuads = Mathf.Max(1, Mathf.RoundToInt(sourceQuads / (float)divider));
            return targetQuads + 1;
        }

        // Clamps terrain ratio index into valid range.
        private static int ClampTerrainResolutionRatio(int ratioIndex) {
            return Mathf.Clamp(ratioIndex, 0, _terrainResolutionDividers.Length - 1);
        }

        // Updates global terrain ratio and persists it in editor prefs.
        private static void SetTerrainResolutionRatio(int ratioIndex) {
            _terrainResolutionRatioIndex = ClampTerrainResolutionRatio(ratioIndex);
            EditorPrefs.SetInt(_terrainResolutionRatioPrefsKey, _terrainResolutionRatioIndex);
        }

        // Creates triangulated terrain mesh at target resolution with initialized paint colors.
        private static Mesh CreateMeshFromTerrain(Terrain terrain, int targetResolution) {
            if (terrain == null || terrain.terrainData == null) return null;
            TerrainData terrainData = terrain.terrainData;
            int resolution = Mathf.Clamp(targetResolution, 2, terrainData.heightmapResolution);
            if (resolution < 2) return null;
            int vertexCount = resolution * resolution;
            Vector3[] vertices = new Vector3[vertexCount];
            Color[] colors = new Color[vertexCount];
            Vector2[] uv = new Vector2[vertexCount];
            float invRes = 1f / (resolution - 1);
            Vector3 terrainSize = terrainData.size;

            // Build regular grid in terrain local space.
            for (int z = 0; z < resolution; z++) {
                float v = z * invRes;
                for (int x = 0; x < resolution; x++) {
                    int index = z * resolution + x;
                    float u = x * invRes;
                    float height = terrainData.GetInterpolatedHeight(u, v);
                    vertices[index] = new Vector3(u * terrainSize.x, height, v * terrainSize.z);
                    uv[index] = new Vector2(u, v);
                    colors[index] = new Color(1f, 1f, 1f, 1f);
                }
            }

            int quadCount = (resolution - 1) * (resolution - 1);
            int[] triangles = new int[quadCount * 6];
            int triangleIndex = 0;
            for (int z = 0; z < resolution - 1; z++) {
                int row = z * resolution;
                int nextRow = (z + 1) * resolution;
                for (int x = 0; x < resolution - 1; x++) {
                    int i0 = row + x;
                    int i1 = i0 + 1;
                    int i2 = nextRow + x;
                    int i3 = i2 + 1;
                    triangles[triangleIndex++] = i0;
                    triangles[triangleIndex++] = i2;
                    triangles[triangleIndex++] = i1;
                    triangles[triangleIndex++] = i1;
                    triangles[triangleIndex++] = i2;
                    triangles[triangleIndex++] = i3;
                }
            }

            Mesh mesh = new Mesh();
            mesh.name = terrain.name + "_TerrainSource";
            mesh.indexFormat = vertexCount > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.colors = colors;
            mesh.SetTriangles(triangles, 0, false);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // Runs mesh preparation pipeline needed by paint workflow.
        private static void PreparePaintMeshCopy(Mesh mesh) {
            if (mesh == null) return;
            if (!MergeSubMeshesIntoOne(mesh)) return;
            EditorUtility.SetDirty(mesh);
            if (AssetDatabase.Contains(mesh)) AssetDatabase.SaveAssetIfDirty(mesh);
        }

        // Merges all submeshes into one to simplify paint/index logic.
        private static bool MergeSubMeshesIntoOne(Mesh mesh) {
            if (mesh == null || mesh.subMeshCount <= 1) return false;

            int subMeshCount = mesh.subMeshCount;
            MeshTopology topology = mesh.GetTopology(0);
            bool sameTopology = true;
            int totalIndexCount = 0;

            for (int i = 0; i < subMeshCount; i++) {
                if (mesh.GetTopology(i) != topology) sameTopology = false;
                totalIndexCount += (int)mesh.GetIndexCount(i);
            }

            if (sameTopology) {
                int[] merged = new int[totalIndexCount];
                int offset = 0;
                for (int i = 0; i < subMeshCount; i++) {
                    int[] indices = mesh.GetIndices(i);
                    System.Array.Copy(indices, 0, merged, offset, indices.Length);
                    offset += indices.Length;
                }
                mesh.subMeshCount = 1;
                mesh.SetIndices(merged, topology, 0, false);
                return true;
            }

            // Fallback for mixed topology assets: keep triangle submeshes only.
            List<int> mergedTriangles = new List<int>(totalIndexCount);
            for (int i = 0; i < subMeshCount; i++) {
                if (mesh.GetTopology(i) != MeshTopology.Triangles) continue;
                mergedTriangles.AddRange(mesh.GetIndices(i));
            }

            if (mergedTriangles.Count == 0) return false;

            mesh.subMeshCount = 1;
            mesh.SetIndices(mergedTriangles, MeshTopology.Triangles, 0, false);
            return true;
        }

        // Creates nested folder chain if it does not exist.
        private static void EnsureFolder(string folder) {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++) {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        // Clears cached geometry data used by paint and picking paths.
        private static void ClearGeometryCaches() {
            _meshVerticesCache.Clear();
            _meshNormalsCache.Clear();
            _meshTrianglesCache.Clear();
            _meshNeighborsCache.Clear();
            _gridMeshInfoCache.Clear();
        }

        // Loads editor tool settings from EditorPrefs.
        private static void LoadPrefs() {
            _paintEnabled = EditorPrefs.GetBool(_prefsKey + "PaintEnabled", false);
            _tool = (ToolMode)EditorPrefs.GetInt(_prefsKey + "Tool", (int)ToolMode.Brush);
            _brushRadius = EditorPrefs.GetFloat(_prefsKey + "BrushRadius", 1f);
            _brushFeather = EditorPrefs.GetFloat(_prefsKey + "BrushFeather", 0.5f);
            _brushBrightness = EditorPrefs.GetFloat(_prefsKey + "BrushBrightness", 1f);
            _fillBrightness = EditorPrefs.GetFloat(_prefsKey + "FillBrightness", 1f);
            _eraserRadius = EditorPrefs.GetFloat(_prefsKey + "EraserRadius", 1f);
            _eraserFeather = EditorPrefs.GetFloat(_prefsKey + "EraserFeather", 0.5f);
            _paintChannel = Mathf.Clamp(EditorPrefs.GetInt(_prefsKey + "PaintChannel", 0), 0, 2);
            _performanceFoldout = EditorPrefs.GetBool(_prefsKey + "PerformanceFoldout", true);
            _terrainResolutionRatioIndex = ClampTerrainResolutionRatio(EditorPrefs.GetInt(_terrainResolutionRatioPrefsKey, 1));
        }

        // Saves editor tool settings to EditorPrefs.
        private static void SavePrefs() {
            EditorPrefs.SetBool(_prefsKey + "PaintEnabled", _paintEnabled);
            EditorPrefs.SetInt(_prefsKey + "Tool", (int)_tool);
            EditorPrefs.SetFloat(_prefsKey + "BrushRadius", _brushRadius);
            EditorPrefs.SetFloat(_prefsKey + "BrushFeather", _brushFeather);
            EditorPrefs.SetFloat(_prefsKey + "BrushBrightness", _brushBrightness);
            EditorPrefs.SetFloat(_prefsKey + "FillBrightness", _fillBrightness);
            EditorPrefs.SetFloat(_prefsKey + "EraserRadius", _eraserRadius);
            EditorPrefs.SetFloat(_prefsKey + "EraserFeather", _eraserFeather);
            EditorPrefs.SetInt(_prefsKey + "PaintChannel", _paintChannel);
            EditorPrefs.SetBool(_prefsKey + "PerformanceFoldout", _performanceFoldout);
            EditorPrefs.SetInt(_terrainResolutionRatioPrefsKey, ClampTerrainResolutionRatio(_terrainResolutionRatioIndex));
        }
    }
}
