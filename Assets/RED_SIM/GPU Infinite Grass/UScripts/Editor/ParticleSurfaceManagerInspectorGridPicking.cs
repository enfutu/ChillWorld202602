using System.Collections.Generic;
using UnityEngine;

namespace GPUInfiniteGrass {

    public partial class ParticleSurfaceManagerInspector {
        // Mesh cache
        private static readonly Dictionary<int, Vector3[]> _meshVerticesCache = new Dictionary<int, Vector3[]>();
        private static readonly Dictionary<int, Vector3[]> _meshNormalsCache = new Dictionary<int, Vector3[]>();
        private static readonly Dictionary<int, int[]> _meshTrianglesCache = new Dictionary<int, int[]>();
        private static readonly Dictionary<int, List<int>[]> _meshNeighborsCache = new Dictionary<int, List<int>[]>();
        private static readonly Dictionary<int, GridMeshInfo> _gridMeshInfoCache = new Dictionary<int, GridMeshInfo>();

        private struct GridMeshInfo {
            public bool IsValid;
            public int Resolution;
            public float MinX;
            public float MinZ;
            public float StepX;
            public float StepZ;
            public int GridDiagonal;
        }

        private static Vector3[] GetCachedVertices(Mesh mesh) {
            if (mesh == null) return null;
            int meshId = mesh.GetInstanceID();
            Vector3[] cached;
            if (_meshVerticesCache.TryGetValue(meshId, out cached) && cached != null && cached.Length == mesh.vertexCount) return cached;
            Vector3[] vertices = mesh.vertices;
            _meshVerticesCache[meshId] = vertices;
            return vertices;
        }

        private static Vector3[] GetCachedNormals(Mesh mesh, int vertexCount) {
            if (mesh == null || vertexCount <= 0) return null;
            int meshId = mesh.GetInstanceID();
            Vector3[] cached;
            if (_meshNormalsCache.TryGetValue(meshId, out cached) && cached != null && cached.Length == vertexCount) return cached;
            Vector3[] normals = mesh.normals;
            if (normals == null || normals.Length != vertexCount) {
                normals = new Vector3[vertexCount];
                for (int i = 0; i < vertexCount; i++) normals[i] = Vector3.up;
            }
            _meshNormalsCache[meshId] = normals;
            return normals;
        }

        private static int[] GetCachedTriangles(Mesh mesh) {
            if (mesh == null) return null;
            int meshId = mesh.GetInstanceID();
            int[] cached;
            if (_meshTrianglesCache.TryGetValue(meshId, out cached) && cached != null) return cached;
            int[] triangles = mesh.triangles;
            _meshTrianglesCache[meshId] = triangles;
            return triangles;
        }

        private static List<int>[] GetCachedNeighbors(Mesh mesh, int vertexCount) {
            if (mesh == null || vertexCount <= 0) return null;
            int meshId = mesh.GetInstanceID();
            List<int>[] neighbors;
            if (_meshNeighborsCache.TryGetValue(meshId, out neighbors) && neighbors != null && neighbors.Length == vertexCount) return neighbors;
            int[] triangles = GetCachedTriangles(mesh);
            if (triangles == null || triangles.Length == 0) return null;
            neighbors = BuildNeighbors(vertexCount, triangles);
            _meshNeighborsCache[meshId] = neighbors;
            return neighbors;
        }

        private static bool TryGetGridMeshInfo(Mesh mesh, out GridMeshInfo info) {
            info = new GridMeshInfo();
            if (mesh == null) return false;
            int meshId = mesh.GetInstanceID();
            if (_gridMeshInfoCache.TryGetValue(meshId, out info)) return info.IsValid;

            Vector3[] vertices = GetCachedVertices(mesh);
            int[] triangles = GetCachedTriangles(mesh);
            if (vertices == null || triangles == null || vertices.Length < 4 || triangles.Length < 6) {
                _gridMeshInfoCache[meshId] = info;
                return false;
            }

            int resolution = Mathf.RoundToInt(Mathf.Sqrt(vertices.Length));
            bool validResolution = resolution >= 2 && resolution * resolution == vertices.Length;
            if (!validResolution) {
                _gridMeshInfoCache[meshId] = info;
                return false;
            }

            float minX = vertices[0].x;
            float maxX = vertices[resolution - 1].x;
            float minZ = vertices[0].z;
            float maxZ = vertices[(resolution - 1) * resolution].z;
            float stepX = (maxX - minX) / (resolution - 1);
            float stepZ = (maxZ - minZ) / (resolution - 1);
            if (stepX < 0f) {
                float swap = minX;
                minX = maxX;
                maxX = swap;
                stepX = -stepX;
            }
            if (stepZ < 0f) {
                float swap = minZ;
                minZ = maxZ;
                maxZ = swap;
                stepZ = -stepZ;
            }
            if (Mathf.Abs(stepX) <= 1e-6f || Mathf.Abs(stepZ) <= 1e-6f) {
                _gridMeshInfoCache[meshId] = info;
                return false;
            }

            if (!ValidateGridVertexLayout(vertices, resolution, minX, minZ, stepX, stepZ)) {
                _gridMeshInfoCache[meshId] = info;
                return false;
            }

            int diagonal = DetectGridDiagonalPattern(triangles, resolution);
            info = new GridMeshInfo {
                IsValid = true,
                Resolution = resolution,
                MinX = minX,
                MinZ = minZ,
                StepX = stepX,
                StepZ = stepZ,
                GridDiagonal = diagonal
            };
            _gridMeshInfoCache[meshId] = info;
            return true;
        }

        private static bool ValidateGridVertexLayout(Vector3[] vertices, int resolution, float minX, float minZ, float stepX, float stepZ) {
            if (vertices == null || resolution < 2) return false;
            float toleranceX = Mathf.Max(Mathf.Abs(stepX) * 0.02f, 1e-4f);
            float toleranceZ = Mathf.Max(Mathf.Abs(stepZ) * 0.02f, 1e-4f);
            int[] sample = BuildGridSampleIndices(resolution);

            for (int sx = 0; sx < sample.Length; sx++) {
                int x = sample[sx];
                Vector3 top = vertices[x];
                if (Mathf.Abs(top.z - minZ) > toleranceZ) return false;
                float expectedX = minX + stepX * x;
                if (Mathf.Abs(top.x - expectedX) > toleranceX) return false;
            }

            for (int sz = 0; sz < sample.Length; sz++) {
                int z = sample[sz];
                int leftIndex = z * resolution;
                Vector3 left = vertices[leftIndex];
                if (Mathf.Abs(left.x - minX) > toleranceX) return false;
                float expectedZ = minZ + stepZ * z;
                if (Mathf.Abs(left.z - expectedZ) > toleranceZ) return false;
            }

            for (int sz = 0; sz < sample.Length; sz++) {
                int z = sample[sz];
                float expectedZ = minZ + stepZ * z;
                for (int sx = 0; sx < sample.Length; sx++) {
                    int x = sample[sx];
                    int index = z * resolution + x;
                    Vector3 vertex = vertices[index];
                    float expectedX = minX + stepX * x;
                    if (Mathf.Abs(vertex.x - expectedX) > toleranceX) return false;
                    if (Mathf.Abs(vertex.z - expectedZ) > toleranceZ) return false;
                }
            }

            return true;
        }

        private static int[] BuildGridSampleIndices(int resolution) {
            int last = resolution - 1;
            int q1 = Mathf.Clamp(Mathf.RoundToInt(last * 0.25f), 0, last);
            int q2 = Mathf.Clamp(Mathf.RoundToInt(last * 0.5f), 0, last);
            int q3 = Mathf.Clamp(Mathf.RoundToInt(last * 0.75f), 0, last);
            return new[] { 0, q1, q2, q3, last };
        }

        private static int DetectGridDiagonalPattern(int[] triangles, int resolution) {
            if (triangles == null || resolution < 2) return 0;
            int cellsPerAxis = resolution - 1;
            int expectedTriangleCount = cellsPerAxis * cellsPerAxis * 6;
            if (triangles.Length != expectedTriangleCount) return 0;

            int[] sample = BuildGridSampleIndices(cellsPerAxis);
            int detectedDiagonal = 0;
            for (int sz = 0; sz < sample.Length; sz++) {
                int z = sample[sz];
                for (int sx = 0; sx < sample.Length; sx++) {
                    int x = sample[sx];
                    int cellIndex = z * cellsPerAxis + x;
                    int triangleStart = cellIndex * 6;
                    int i0 = z * resolution + x;
                    int i1 = i0 + 1;
                    int i2 = i0 + resolution;
                    int i3 = i2 + 1;
                    int cellDiagonal;
                    if (!TryGetCellDiagonal(triangles, triangleStart, i0, i1, i2, i3, out cellDiagonal)) return 0;
                    if (detectedDiagonal == 0) detectedDiagonal = cellDiagonal;
                    else if (detectedDiagonal != cellDiagonal) return 0;
                }
            }

            return detectedDiagonal;
        }

        private static bool TryGetCellDiagonal(int[] triangles, int triangleStart, int i0, int i1, int i2, int i3, out int diagonal) {
            diagonal = 0;
            if (triangles == null || triangleStart < 0 || triangleStart + 5 >= triangles.Length) return false;
            int a0 = triangles[triangleStart];
            int a1 = triangles[triangleStart + 1];
            int a2 = triangles[triangleStart + 2];
            int b0 = triangles[triangleStart + 3];
            int b1 = triangles[triangleStart + 4];
            int b2 = triangles[triangleStart + 5];
            if (!IsCellIndex(a0, i0, i1, i2, i3) || !IsCellIndex(a1, i0, i1, i2, i3) || !IsCellIndex(a2, i0, i1, i2, i3)) return false;
            if (!IsCellIndex(b0, i0, i1, i2, i3) || !IsCellIndex(b1, i0, i1, i2, i3) || !IsCellIndex(b2, i0, i1, i2, i3)) return false;

            int[] triA = { a0, a1, a2 };
            int[] triB = { b0, b1, b2 };
            int sharedA = -1;
            int sharedB = -1;
            int sharedCount = 0;
            for (int ia = 0; ia < triA.Length; ia++) {
                int value = triA[ia];
                bool match = false;
                for (int ib = 0; ib < triB.Length; ib++) {
                    if (value != triB[ib]) continue;
                    match = true;
                    break;
                }
                if (!match) continue;
                if (sharedCount == 0) sharedA = value;
                else if (sharedCount == 1) sharedB = value;
                sharedCount++;
            }
            if (sharedCount != 2) return false;

            bool sharedI1I2 = (sharedA == i1 && sharedB == i2) || (sharedA == i2 && sharedB == i1);
            if (sharedI1I2) {
                diagonal = 1;
                return true;
            }

            bool sharedI0I3 = (sharedA == i0 && sharedB == i3) || (sharedA == i3 && sharedB == i0);
            if (sharedI0I3) {
                diagonal = 2;
                return true;
            }

            return false;
        }

        private static bool IsCellIndex(int value, int i0, int i1, int i2, int i3) {
            return value == i0 || value == i1 || value == i2 || value == i3;
        }

        private static bool TryGetGridVertexRange(Mesh mesh, Transform surface, Vector3 hitPoint, float worldRadius, out GridMeshInfo info, out int xMin, out int xMax, out int zMin, out int zMax) {
            info = new GridMeshInfo();
            xMin = 0;
            xMax = -1;
            zMin = 0;
            zMax = -1;
            if (surface == null || mesh == null) return false;
            if (!TryGetGridMeshInfo(mesh, out info) || !info.IsValid) return false;

            Matrix4x4 worldToLocal = surface.worldToLocalMatrix;
            Vector3 hitLocal = worldToLocal.MultiplyPoint3x4(hitPoint);
            Vector3 lossyScale = surface.lossyScale;
            float localRadiusX = worldRadius / Mathf.Max(Mathf.Abs(lossyScale.x), 1e-6f);
            float localRadiusZ = worldRadius / Mathf.Max(Mathf.Abs(lossyScale.z), 1e-6f);
            float minLocalX = hitLocal.x - localRadiusX;
            float maxLocalX = hitLocal.x + localRadiusX;
            float minLocalZ = hitLocal.z - localRadiusZ;
            float maxLocalZ = hitLocal.z + localRadiusZ;
            int resolution = info.Resolution;

            xMin = Mathf.Clamp(Mathf.FloorToInt((minLocalX - info.MinX) / info.StepX), 0, resolution - 1);
            xMax = Mathf.Clamp(Mathf.CeilToInt((maxLocalX - info.MinX) / info.StepX), 0, resolution - 1);
            zMin = Mathf.Clamp(Mathf.FloorToInt((minLocalZ - info.MinZ) / info.StepZ), 0, resolution - 1);
            zMax = Mathf.Clamp(Mathf.CeilToInt((maxLocalZ - info.MinZ) / info.StepZ), 0, resolution - 1);
            return xMax >= xMin && zMax >= zMin;
        }

        private static List<int>[] BuildNeighbors(int vertexCount, int[] triangles) {
            List<int>[] neighbors = new List<int>[vertexCount];
            for (int i = 0; i < vertexCount; i++) neighbors[i] = new List<int>(6);
            for (int i = 0; i < triangles.Length; i += 3) {
                int a = triangles[i];
                int b = triangles[i + 1];
                int c = triangles[i + 2];
                AddNeighbor(neighbors, a, b);
                AddNeighbor(neighbors, a, c);
                AddNeighbor(neighbors, b, a);
                AddNeighbor(neighbors, b, c);
                AddNeighbor(neighbors, c, a);
                AddNeighbor(neighbors, c, b);
            }
            return neighbors;
        }
        
        private static void AddNeighbor(List<int>[] neighbors, int a, int b) {
            if (!neighbors[a].Contains(b)) neighbors[a].Add(b);
        }

        private static int GetClosestVertexIndex(Mesh mesh, Vector3[] vertices, Transform surface, Vector3 hitPoint) {
            if (mesh == null || vertices == null || surface == null || vertices.Length == 0) return -1;
            GridMeshInfo gridInfo;
            if (TryGetGridMeshInfo(mesh, out gridInfo) && gridInfo.IsValid) {
                Matrix4x4 worldToLocal = surface.worldToLocalMatrix;
                Vector3 hitLocal = worldToLocal.MultiplyPoint3x4(hitPoint);
                int x = Mathf.Clamp(Mathf.RoundToInt((hitLocal.x - gridInfo.MinX) / gridInfo.StepX), 0, gridInfo.Resolution - 1);
                int z = Mathf.Clamp(Mathf.RoundToInt((hitLocal.z - gridInfo.MinZ) / gridInfo.StepZ), 0, gridInfo.Resolution - 1);
                return z * gridInfo.Resolution + x;
            }

            Matrix4x4 localToWorld = surface.localToWorldMatrix;
            int best = -1;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < vertices.Length; i++) {
                Vector3 worldPos = localToWorld.MultiplyPoint3x4(vertices[i]);
                float sqr = (worldPos - hitPoint).sqrMagnitude;
                if (sqr < bestSqr) {
                    bestSqr = sqr;
                    best = i;
                }
            }
            return best;
        }
        
        private struct HitInfo {
            public Transform Surface;
            public int SurfaceIndex;
            public Vector3 Point;
            public Vector3 Normal;
            public float Distance;
        }
        
        private static bool TryPickSurface(ParticleSurfaceManager manager, Ray ray, out HitInfo hitInfo) {
            hitInfo = new HitInfo();
            float bestDistance = float.MaxValue;
            bool found = false;
            
            for (int i = 0; i < manager.Surfaces.Length; i++) {
                HitInfo singleHit;
                if (!TryPickSurfaceByIndex(manager, ray, i, out singleHit)) continue;
                if (singleHit.Distance >= bestDistance) continue;
                bestDistance = singleHit.Distance;
                hitInfo = singleHit;
                found = true;
            }
            
            return found;
        }

        private static bool TryPickSurfaceByIndex(ParticleSurfaceManager manager, Ray ray, int surfaceIndex, out HitInfo hitInfo) {
            hitInfo = new HitInfo();
            if (manager == null || manager.Surfaces == null || surfaceIndex < 0 || surfaceIndex >= manager.Surfaces.Length) return false;
            Transform surface = manager.Surfaces[surfaceIndex];
            if (surface == null || !surface.gameObject.activeInHierarchy) return false;
            Mesh surfaceMesh = GetSurfaceMeshForPaint(manager, surfaceIndex);
            if (surfaceMesh == null) return false;
            MeshRaycastHit hit;
            if (!IntersectRayMesh(ray, surfaceMesh, surface.localToWorldMatrix, out hit)) return false;
            hitInfo = new HitInfo {
                Surface = surface,
                SurfaceIndex = surfaceIndex,
                Point = hit.Point,
                Normal = hit.Normal,
                Distance = hit.Distance
            };
            return true;
        }

        private struct MeshRaycastHit {
            public Vector3 Point;
            public Vector3 Normal;
            public float Distance;
        }

        // Raycast Mesh
        private static bool IntersectRayMesh(Ray ray, Mesh mesh, Matrix4x4 localToWorld, out MeshRaycastHit hit) {
            hit = new MeshRaycastHit();
            if (mesh == null) return false;
            Matrix4x4 worldToLocal = localToWorld.inverse;
            Vector3 localRayOrigin = worldToLocal.MultiplyPoint3x4(ray.origin);
            Vector3 localRayDirection = worldToLocal.MultiplyVector(ray.direction);
            if (localRayDirection.sqrMagnitude <= 1e-12f) return false;
            Ray localRay = new Ray(localRayOrigin, localRayDirection.normalized);
            Bounds bounds = mesh.bounds;
            if (!bounds.IntersectRay(localRay)) return false;

            Vector3[] vertices = GetCachedVertices(mesh);
            if (vertices == null || vertices.Length < 3) return false;

            GridMeshInfo gridInfo;
            float hitT;
            Vector3 localNormal;
            bool foundHit = false;
            if (TryGetGridMeshInfo(mesh, out gridInfo) && gridInfo.IsValid && gridInfo.GridDiagonal != 0) {
                foundHit = IntersectRayGrid(localRay, vertices, gridInfo, out hitT, out localNormal);
            } else {
                int[] triangles = GetCachedTriangles(mesh);
                if (triangles == null || triangles.Length < 3) return false;
                foundHit = IntersectRayTriangles(localRay, vertices, triangles, out hitT, out localNormal);
            }

            if (!foundHit) return false;

            Vector3 localPoint = localRay.origin + localRay.direction * hitT;
            Vector3 worldPoint = localToWorld.MultiplyPoint3x4(localPoint);
            Vector3 worldNormal = localToWorld.MultiplyVector(localNormal).normalized;
            hit = new MeshRaycastHit {
                Point = worldPoint,
                Normal = worldNormal,
                Distance = (worldPoint - ray.origin).magnitude
            };
            return true;
        }

        private static bool IntersectRayTriangles(Ray localRay, Vector3[] vertices, int[] triangles, out float bestT, out Vector3 bestNormal) {
            bestT = float.MaxValue;
            bestNormal = Vector3.up;
            bool found = false;
            for (int i = 0; i < triangles.Length; i += 3) {
                int ia = triangles[i];
                int ib = triangles[i + 1];
                int ic = triangles[i + 2];
                if (ia < 0 || ib < 0 || ic < 0 || ia >= vertices.Length || ib >= vertices.Length || ic >= vertices.Length) continue;
                Vector3 a = vertices[ia];
                Vector3 b = vertices[ib];
                Vector3 c = vertices[ic];
                float t;
                if (!RayTriangle(localRay.origin, localRay.direction, a, b, c, out t)) continue;
                if (t <= 0f || t >= bestT) continue;
                bestT = t;
                bestNormal = Vector3.Cross(b - a, c - a).normalized;
                found = true;
            }
            return found;
        }

        private static bool IntersectRayGrid(Ray localRay, Vector3[] vertices, GridMeshInfo gridInfo, out float bestT, out Vector3 bestNormal) {
            bestT = float.MaxValue;
            bestNormal = Vector3.up;
            int resolution = gridInfo.Resolution;
            if (resolution < 2 || gridInfo.GridDiagonal == 0) return false;

            float minX = gridInfo.MinX;
            float maxX = minX + gridInfo.StepX * (resolution - 1);
            float minZ = gridInfo.MinZ;
            float maxZ = minZ + gridInfo.StepZ * (resolution - 1);
            Vector3 origin = localRay.origin;
            Vector3 dir = localRay.direction;
            float tEntry;
            float tExit;
            if (!TryGetRayXZInterval(origin, dir, minX, maxX, minZ, maxZ, out tEntry, out tExit)) return false;

            int maxCell = resolution - 2;
            Vector3 startPoint = origin + dir * tEntry;
            int cellX = Mathf.Clamp(Mathf.FloorToInt((startPoint.x - minX) / gridInfo.StepX), 0, maxCell);
            int cellZ = Mathf.Clamp(Mathf.FloorToInt((startPoint.z - minZ) / gridInfo.StepZ), 0, maxCell);
            int stepX = dir.x > 1e-7f ? 1 : dir.x < -1e-7f ? -1 : 0;
            int stepZ = dir.z > 1e-7f ? 1 : dir.z < -1e-7f ? -1 : 0;
            float nextBoundaryX = stepX > 0
                ? minX + (cellX + 1) * gridInfo.StepX
                : minX + cellX * gridInfo.StepX;
            float nextBoundaryZ = stepZ > 0
                ? minZ + (cellZ + 1) * gridInfo.StepZ
                : minZ + cellZ * gridInfo.StepZ;
            float tMaxX = stepX == 0 ? float.MaxValue : (nextBoundaryX - origin.x) / dir.x;
            float tMaxZ = stepZ == 0 ? float.MaxValue : (nextBoundaryZ - origin.z) / dir.z;
            float tDeltaX = stepX == 0 ? float.MaxValue : gridInfo.StepX / Mathf.Abs(dir.x);
            float tDeltaZ = stepZ == 0 ? float.MaxValue : gridInfo.StepZ / Mathf.Abs(dir.z);
            if (tMaxX < tEntry) tMaxX = tEntry;
            if (tMaxZ < tEntry) tMaxZ = tEntry;

            bool found = false;
            while (cellX >= 0 && cellX <= maxCell && cellZ >= 0 && cellZ <= maxCell) {
                int i0 = cellZ * resolution + cellX;
                int i1 = i0 + 1;
                int i2 = i0 + resolution;
                int i3 = i2 + 1;
                if (gridInfo.GridDiagonal == 1) {
                    found |= TryHitTriangle(localRay, vertices, i0, i2, i1, ref bestT, ref bestNormal);
                    found |= TryHitTriangle(localRay, vertices, i1, i2, i3, ref bestT, ref bestNormal);
                } else {
                    found |= TryHitTriangle(localRay, vertices, i0, i2, i3, ref bestT, ref bestNormal);
                    found |= TryHitTriangle(localRay, vertices, i0, i3, i1, ref bestT, ref bestNormal);
                }
                if (found && bestT <= Mathf.Min(tMaxX, tMaxZ)) break;

                if (tMaxX < tMaxZ) {
                    if (tMaxX > tExit) break;
                    cellX += stepX;
                    tMaxX += tDeltaX;
                } else {
                    if (tMaxZ > tExit) break;
                    cellZ += stepZ;
                    tMaxZ += tDeltaZ;
                }
            }

            return found;
        }

        private static bool TryGetRayXZInterval(Vector3 origin, Vector3 dir, float minX, float maxX, float minZ, float maxZ, out float tEntry, out float tExit) {
            float txMin;
            float txMax;
            if (Mathf.Abs(dir.x) <= 1e-7f) {
                if (origin.x < minX || origin.x > maxX) {
                    tEntry = 0f;
                    tExit = -1f;
                    return false;
                }
                txMin = float.NegativeInfinity;
                txMax = float.PositiveInfinity;
            } else {
                float tx1 = (minX - origin.x) / dir.x;
                float tx2 = (maxX - origin.x) / dir.x;
                txMin = Mathf.Min(tx1, tx2);
                txMax = Mathf.Max(tx1, tx2);
            }

            float tzMin;
            float tzMax;
            if (Mathf.Abs(dir.z) <= 1e-7f) {
                if (origin.z < minZ || origin.z > maxZ) {
                    tEntry = 0f;
                    tExit = -1f;
                    return false;
                }
                tzMin = float.NegativeInfinity;
                tzMax = float.PositiveInfinity;
            } else {
                float tz1 = (minZ - origin.z) / dir.z;
                float tz2 = (maxZ - origin.z) / dir.z;
                tzMin = Mathf.Min(tz1, tz2);
                tzMax = Mathf.Max(tz1, tz2);
            }

            tEntry = Mathf.Max(0f, Mathf.Max(txMin, tzMin));
            tExit = Mathf.Min(txMax, tzMax);
            return tExit >= tEntry;
        }

        private static bool TryHitTriangle(Ray ray, Vector3[] vertices, int ia, int ib, int ic, ref float bestT, ref Vector3 bestNormal) {
            if (ia < 0 || ib < 0 || ic < 0 || ia >= vertices.Length || ib >= vertices.Length || ic >= vertices.Length) return false;
            Vector3 a = vertices[ia];
            Vector3 b = vertices[ib];
            Vector3 c = vertices[ic];
            float t;
            if (!RayTriangle(ray.origin, ray.direction, a, b, c, out t)) return false;
            if (t <= 0f || t >= bestT) return false;
            bestT = t;
            bestNormal = Vector3.Cross(b - a, c - a).normalized;
            return true;
        }

        private static bool RayTriangle(Vector3 rayOrigin, Vector3 rayDir, Vector3 a, Vector3 b, Vector3 c, out float t) {
            t = 0f;
            Vector3 edge1 = b - a;
            Vector3 edge2 = c - a;
            Vector3 pvec = Vector3.Cross(rayDir, edge2);
            float det = Vector3.Dot(edge1, pvec);
            if (det > -1e-7f && det < 1e-7f) return false;
            float invDet = 1f / det;
            Vector3 tvec = rayOrigin - a;
            float u = Vector3.Dot(tvec, pvec) * invDet;
            if (u < 0f || u > 1f) return false;
            Vector3 qvec = Vector3.Cross(tvec, edge1);
            float v = Vector3.Dot(rayDir, qvec) * invDet;
            if (v < 0f || u + v > 1f) return false;
            t = Vector3.Dot(edge2, qvec) * invDet;
            return true;
        }
        
    }

}
