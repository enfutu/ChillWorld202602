
#if UDONSHARP
using UdonSharp;
using VRC.SDKBase;
#else
using VRCGraphics = UnityEngine.Graphics;
#endif
using UnityEngine;
using UnityEngine.Rendering;

namespace GPUInfiniteGrass {
    
#if UDONSHARP
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class ParticleSurfaceManager : UdonSharpBehaviour {
#else
    public class ParticleSurfaceManager : MonoBehaviour {
#endif
        [Header("Surface Settings")]
        [Tooltip("Orthographic camera that renders the surface mask used by the grass shader. Should be in scene, rotated no matter how, can be placed anywhere, but not too far from the world center.")]
        public Camera SurfaceCamera;
        [Tooltip("Layer used by the Surface Camera to render the mask. Should be an empty layer with nothing on it.")]
        [Layer] public int CameraLayer = 2;
        [Tooltip("Material used to render surface meshes into the mask render texture.")]
        public Material MaskMaterial;
        [Tooltip("Editor-only surface RT resolution override. Custom keeps the current texture size.")]
        public TextureResolutionPreset SurfaceRTResolution = TextureResolutionPreset.Custom;
        [Tooltip("Target to override player's camera position. Makes it possible to create static grass areas, which are not following player's head. Useful for small worlds. Leave it empty for regular dynamic behaviour.")]
        public Transform TargetOverride;
        [Tooltip("Radius in meters, in which grass particles will be rendered around the player.")]
        public float DrawDistance = 150;
        [Tooltip("Should be disabled in most cases. Redraws the surface render texture every frame. You should only enable it if your surface shape or mask changes in runtime.")]
        public bool AlwaysUpdateSurface = false;
        [Tooltip("Surface roots for grass mask rendering. Each element should contain either MeshFilter or Terrain.")]
        public Transform[] Surfaces;
        [Tooltip("Generated paint/render meshes aligned with Surfaces.")]
        public Mesh[] SurfaceMeshes;
        
        [Header("Trail Settings")]
        [Tooltip("Enables or disables trail interaction and trail render texture updates.")]
        public bool EnableTrail = true;
        [Tooltip("Material that writes trail data into the trail render texture.")]
        public Material TrailMaterial;
        [Tooltip("Custom Render Texture updated every frame to store trail data.")]
        public CustomRenderTexture TrailCRT;
        [Tooltip("Editor-only trail CRT resolution override. Custom keeps the current texture size.")]
        public TextureResolutionPreset TrailCRTResolution = TextureResolutionPreset.Custom;
        [Tooltip("Trail strength decay per second. Higher values make trails disappear faster.")]
        [Min(0)] public float TrailDecay = 0.01f;
#if UDONSHARP
        [Tooltip("Base radius used for player trails (scaled by avatar height).")]
        public float PlayerTrailRadius = 1.5f;
#endif
        [Tooltip("Additional transforms that write trails (e.g., moving objects). Scale them to increase the trail radius.")]
        public Transform[] TrailTargets;
        
        [Header("Particle Settings")]
        [Tooltip("Material used to render the grass particles.")]
        public Material ParticleMaterial;
        [Tooltip("Layer used for rendering grass particles. Can be any visible layer including the default layer.")]
        [Layer] public int RenderingLayer = 0;
        [Tooltip("Shadow casting from realtime lights. It's better for the performance not cast shadows.")]
        public bool CastShadows = false;
        [Tooltip("Shadow receiving from realtime lights. It's better for the performance not receive shadows.")]
        public bool ReceiveShadows = true;
        [Tooltip("Number of grass batches to draw. Each batch draws exactly 16383 grass particles.")]
        [Min(0)] public int DrawAmount = 50;
        public Texture HeightTex;
        public const int MaxDrawAmount = 10000;
        private const string _tripleCrossToggleProp = "_EnableTripleCross";
        private const int _particlesPerBatch = 16383;
        private const int _instancesPerDrawCall = 511;
        private const int _maxTrailTargets = 256;
        private const float _sqrt3 = 1.73205080757f;
        private const float _minDrawDistance = 0.001f;
        private const float _minSnapStep = 1e-6f;

        private Mesh _grassBatch; // Pregenerated grass particles mesh
        private float _cameraWorldY; // Virtual camera world Y position
        private float _cameraWorldDepth; // Virtual camera render depth
        private Vector4 _cameraData; // Virtual camera's [x, y, z, camera height]
        private Vector4 _realCameraData; // Real camera's [x, y, z, 0]
        private Matrix4x4[] _surfaceDrawMatrix;
        private int _cachedDrawAmount = -1;
        private int _cachedTotalParticleCount;
        private int _cachedMaxHexRing;
        private int _cachedTripleMaxHexRing;

#if UDONSHARP
        private VRCPlayerApi _localPlayer;
        private VRCPlayerApi[] _players;
#endif
        private MaterialPropertyBlock _grassMaterialPropertyBlock;
        private Vector4[] _targetPositions;
        Matrix4x4[] _matrices;
        
        // Update World Height
        private void OnValidate() {
            DrawAmount = Mathf.Clamp(DrawAmount, 0, MaxDrawAmount);
            if (!IsFinite(DrawDistance) || DrawDistance < _minDrawDistance) DrawDistance = _minDrawDistance;
#if UNITY_EDITOR
            ApplyEditorRenderTextureResolutions();
#endif
        }
        
        // Start
        private void OnEnable() {
            DrawAmount = Mathf.Clamp(DrawAmount, 0, MaxDrawAmount);
            if (!IsFinite(DrawDistance) || DrawDistance < _minDrawDistance) DrawDistance = _minDrawDistance;
            if (_surfaceDrawMatrix == null || _surfaceDrawMatrix.Length != 1) _surfaceDrawMatrix = new Matrix4x4[1];
#if UNITY_EDITOR
            ApplyEditorRenderTextureResolutions();
#endif
#if UDONSHARP
            _localPlayer = Networking.LocalPlayer;
            _players = new VRCPlayerApi[VRCPlayerApi.GetPlayerCount()];
            VRCPlayerApi.GetPlayers(_players);
#endif
        }
#if UDONSHARP
        // On Player Joined
        public override void OnPlayerJoined(VRCPlayerApi player) {
            _players = new VRCPlayerApi[VRCPlayerApi.GetPlayerCount()];  
            VRCPlayerApi.GetPlayers(_players);
        }

        // On Player Left
        public override void OnPlayerLeft(VRCPlayerApi player) {
            _players = new VRCPlayerApi[VRCPlayerApi.GetPlayerCount()];  
            VRCPlayerApi.GetPlayers(_players);
        }
#endif
        // Update
        private void Update() {
            UpdateSurface();
            if (EnableTrail) UpdateTrail();
            DrawGrass();
        }
        
        // Sets up the surface camera and renders the surface RT
        public void UpdateSurface(Vector3 renderPosition, Vector3 realCameraPosition, bool forceRender) {
            if (SurfaceCamera == null || SurfaceCamera.targetTexture == null || MaskMaterial == null) return;
            if (_surfaceDrawMatrix == null || _surfaceDrawMatrix.Length != 1) _surfaceDrawMatrix = new Matrix4x4[1];

            int rtWidth = SurfaceCamera.targetTexture.width;
            int rtHeight = SurfaceCamera.targetTexture.height;
            if (rtWidth <= 0 || rtHeight <= 0) return;
            
            // Calculating camera world size
            float drawDistance = !IsFinite(DrawDistance) || DrawDistance < _minDrawDistance ? _minDrawDistance : DrawDistance;
            DrawDistance = drawDistance;
            float cameraHeight = 2f * drawDistance;
            float cameraWidth = cameraHeight * SurfaceCamera.aspect;

            // Snapping camera movements to pixels in world space to prevent pixel jitter
            float previousCameraWorldY = _cameraWorldY;
            float previousCameraWorldDepth = _cameraWorldDepth;
            UpdateCameraWorldHeight();
            bool worldHeightChanged = Mathf.Abs(previousCameraWorldY - _cameraWorldY) > 1e-6f
                || Mathf.Abs(previousCameraWorldDepth - _cameraWorldDepth) > 1e-6f;
            float stepX = cameraWidth / rtWidth;
            float stepZ = cameraHeight / rtHeight;
            Vector4 newCameraData = new Vector4(Snap(renderPosition.x, stepX), _cameraWorldY, Snap(renderPosition.z, stepZ), cameraHeight); // x, y, z, camera height
            _realCameraData = new Vector4(realCameraPosition.x, realCameraPosition.y, realCameraPosition.z, 0);
            bool cameraDataChanged = HasCameraDataChanged(_cameraData, newCameraData);
            _cameraData = newCameraData;
            if (!forceRender && !AlwaysUpdateSurface && !worldHeightChanged && !cameraDataChanged) return;
            
            // Force surface camera parameters
            SurfaceCamera.orthographic = true;
            SurfaceCamera.backgroundColor = new Color(0, 0, 0, float.MinValue);
            SurfaceCamera.nearClipPlane = 0;
            SurfaceCamera.farClipPlane = Mathf.Max(_cameraWorldDepth, 0.01f);
            SurfaceCamera.cullingMask = 1 << CameraLayer;
            SurfaceCamera.orthographicSize = drawDistance;
            SurfaceCamera.enabled = false;
            
            // Drawing all surface meshes to the camera
            // Camera scale breaks surface UV projection, so we use only world position+rotation.
            Matrix4x4 cameraWorldNoScale = Matrix4x4.TRS(SurfaceCamera.transform.position, SurfaceCamera.transform.rotation, Vector3.one);
            Matrix4x4 virtualCameraToCameraWorld = cameraWorldNoScale * Matrix4x4.TRS(_cameraData, Quaternion.LookRotation(Vector3.down, Vector3.forward), Vector3.one).inverse;
            if (Surfaces != null) {
                for (int i = 0; i < Surfaces.Length; i++) {
                    Transform surface = Surfaces[i];
                    if (surface == null) continue;
                    if (!surface.gameObject.activeInHierarchy) continue;
                    Mesh surfaceMesh = GetSurfaceMesh(i);
                    if (surfaceMesh == null) continue;
                    Bounds surfaceBounds = surfaceMesh.bounds;
                    if (!IsFinite(surfaceBounds.center.x) || !IsFinite(surfaceBounds.center.y) || !IsFinite(surfaceBounds.center.z)) continue;
                    if (!IsFinite(surfaceBounds.extents.x) || !IsFinite(surfaceBounds.extents.y) || !IsFinite(surfaceBounds.extents.z)) continue;
                    Matrix4x4 localToWorld = surface.localToWorldMatrix;
                    _surfaceDrawMatrix[0] = virtualCameraToCameraWorld * localToWorld;
                    VRCGraphics.DrawMeshInstanced(surfaceMesh, 0, MaskMaterial, _surfaceDrawMatrix, 1, null, ShadowCastingMode.Off, false, CameraLayer, SurfaceCamera, LightProbeUsage.Off);
                }
            }
            
            // Force render camera
            SurfaceCamera.Render();
        }

        // Backward compatible overload
        public void UpdateSurface(Vector3 renderPosition, Vector3 realCameraPosition) {
            UpdateSurface(renderPosition, realCameraPosition, false);
        }

        // Backward compatible overload: use one position for both render and real camera
        public void UpdateSurface(Vector3 position) {
            UpdateSurface(position, position);
        }
        
        // Update Surface override for local player head
        public void UpdateSurface() {
#if UDONSHARP
            if (!Utilities.IsValid(_localPlayer)) return;
            Vector3 realCameraPosition = _localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;
#else
            Camera mainCamera = Camera.main;
            if (mainCamera == null && TargetOverride == null) return;
            Vector3 realCameraPosition = mainCamera != null ? mainCamera.transform.position : TargetOverride.position;
#endif
            Vector3 renderPosition = TargetOverride == null ? realCameraPosition : TargetOverride.position;
            UpdateSurface(renderPosition, realCameraPosition);
        }

        private Mesh GetSurfaceMesh(int surfaceIndex) {
            if (SurfaceMeshes == null || surfaceIndex < 0 || surfaceIndex >= SurfaceMeshes.Length) return null;
            return SurfaceMeshes[surfaceIndex];
        }

        // Sets data to the trail material and updates the trail CRT
        public void UpdateTrail() {
            if (!EnableTrail || TrailMaterial == null || TrailCRT == null) return;
            TrailMaterial.SetVector("_CameraData", _cameraData);
            TrailMaterial.SetFloat("_Decay", Mathf.Max(0f, TrailDecay));
            int targetCount = 0;
            if (_targetPositions == null || _targetPositions.Length != _maxTrailTargets) _targetPositions = new Vector4[_maxTrailTargets];

            // Trail targets
            if (TrailTargets != null) {
                for (int i = 0; i < TrailTargets.Length && targetCount < _maxTrailTargets; i++) {
                    Transform trailTarget = TrailTargets[i];
                    if (trailTarget == null || !trailTarget.gameObject.activeInHierarchy) continue;
                    Vector3 targetPos = trailTarget.position;
                    Vector3 scale = trailTarget.lossyScale;
                    float radius = (scale.x + scale.y + scale.z) * 0.33333334f;
                    _targetPositions[targetCount] = new Vector4(targetPos.x, targetPos.y, targetPos.z, radius);
                    targetCount++;
                }
            }

#if UDONSHARP
            // Players
            if (_players != null) {
                for (int i = 0; i < _players.Length && targetCount < _maxTrailTargets; i++) {
                    VRCPlayerApi player = _players[i];
                    if (!Utilities.IsValid(player)) continue;
                    Vector3 playerPos = player.GetPosition();
                    float radius = PlayerTrailRadius * player.GetAvatarEyeHeightAsMeters() * 0.5f;
                    _targetPositions[targetCount] = new Vector4(playerPos.x, playerPos.y, playerPos.z, radius);
                    targetCount++;
                }
            }
#endif

            // Setting data
            if (targetCount > 0) TrailMaterial.SetVectorArray("_TrailTargets", _targetPositions);
            TrailMaterial.SetFloat("_TrailTargetCount", targetCount);

            // Update the trail CRT
            TrailCRT.Update();
            
        }
        
        // Draws instanced grass batches to the provided camera
        public void DrawGrass(Camera cam = null) {
            int drawAmount = Mathf.Clamp(DrawAmount, 0, MaxDrawAmount);
            if(ParticleMaterial == null || drawAmount <= 0) return;
            if (_grassBatch == null) RegenerateMesh();
            if(_grassMaterialPropertyBlock == null) _grassMaterialPropertyBlock = new MaterialPropertyBlock();
            if (_matrices == null || _matrices.Length != drawAmount) {
                _matrices = new Matrix4x4[drawAmount];
                for (int i = 0; i < drawAmount; i++) _matrices[i] = Matrix4x4.identity;
            }
            EnsureDrawCache(drawAmount);
            bool tripleCross = ParticleMaterial.GetFloat(_tripleCrossToggleProp) > 0.5f;
            float worldRadius = Mathf.Max(_cameraData.w * 0.5f, 0.001f);
            int maxHexRing = tripleCross ? _cachedTripleMaxHexRing : _cachedMaxHexRing;
            float hexCellSize = worldRadius / Mathf.Max(maxHexRing * 1.5f, 1f);
            Vector2 hexCameraCell = RoundAxial(WorldXZToAxial(new Vector2(_cameraData.x, _cameraData.z), hexCellSize));
            _grassMaterialPropertyBlock.SetVector("_CameraData", _cameraData);
            _grassMaterialPropertyBlock.SetVector("_RealCameraData", _realCameraData);
            _grassMaterialPropertyBlock.SetInteger("_TotalParticleCount", _cachedTotalParticleCount);
            _grassMaterialPropertyBlock.SetInteger("_InstancesPerDrawCall", _instancesPerDrawCall);
            _grassMaterialPropertyBlock.SetInteger("_ParticlesPerInstance", _particlesPerBatch);
            _grassMaterialPropertyBlock.SetFloat("_HexCellSize", hexCellSize);
            _grassMaterialPropertyBlock.SetVector("_HexCameraCell", new Vector4(hexCameraCell.x, hexCameraCell.y, 0, 0));
            // Only 511 instances can be drawn in a single Draw Instanced draw call (Unity docs says, it's 1023, but they lie!)
            int fullDrawCallCount = drawAmount / _instancesPerDrawCall; // Full draw calls count
            int extraDrawCount = drawAmount % _instancesPerDrawCall; // Instances in the last draw call
            var castShadows = CastShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            for (int i = 0; i < fullDrawCallCount; i++) { // Here we draw full drawcalls with 511 instances each
                _grassMaterialPropertyBlock.SetInteger("_DrawCallId", i);
                VRCGraphics.DrawMeshInstanced(_grassBatch, 0, ParticleMaterial, _matrices, _instancesPerDrawCall, _grassMaterialPropertyBlock, castShadows, ReceiveShadows, RenderingLayer, cam, LightProbeUsage.Off);
            }
            if (extraDrawCount > 0) { // Here we draw the rest
                _grassMaterialPropertyBlock.SetInteger("_DrawCallId", fullDrawCallCount);
                VRCGraphics.DrawMeshInstanced(_grassBatch, 0, ParticleMaterial, _matrices, extraDrawCount, _grassMaterialPropertyBlock, castShadows, ReceiveShadows, RenderingLayer, cam, LightProbeUsage.Off);
            }
        }
        
        // Regenerates a grass batch with quad particles that fit UInt16 indexing.
        private void RegenerateMesh(int particleCount = _particlesPerBatch) {
            if (_grassBatch == null) {
                _grassBatch = new Mesh();
                _grassBatch.name = "GPU_Grass_Batch";
                _grassBatch.indexFormat = IndexFormat.UInt16;
            }
            int vCount = particleCount * 4;
            Vector3[] vertices = new Vector3[vCount];
            int[] indices = new int[particleCount * 6];
            for (int i = 0; i < particleCount; i++) {
                int baseVertex = i * 4;
                int baseIndex = i * 6;
                indices[baseIndex] = baseVertex;
                indices[baseIndex + 1] = baseVertex + 1;
                indices[baseIndex + 2] = baseVertex + 3;
                indices[baseIndex + 3] = baseVertex;
                indices[baseIndex + 4] = baseVertex + 2;
                indices[baseIndex + 5] = baseVertex + 3;
            }
            _grassBatch.Clear(false);
            _grassBatch.SetVertices(vertices);
            _grassBatch.SetIndices(indices, MeshTopology.Triangles, 0, false);
            _grassBatch.bounds = new Bounds(Vector3.zero, Vector3.one * 1_000_000f);
        }
        
        // Updates camera world Y position and render depth based on the surface bounding boxes
        public void UpdateCameraWorldHeight() {
            if (Surfaces == null || Surfaces.Length == 0) {
                _cameraWorldY = 1;
                _cameraWorldDepth = 2;
                return;
            }
            float heightMax = float.MinValue;
            float heightMin = float.MaxValue;
            bool surfaceFound = false;
            for (int i = 0; i < Surfaces.Length; i++) {
                Transform surface = Surfaces[i];
                float min;
                float max;
                if (!TryGetSurfaceWorldYExtents(i, surface, out min, out max)) continue;
                if (max > heightMax) heightMax = max;
                if (min < heightMin) heightMin = min;
                surfaceFound =  true;
            }
            if (surfaceFound) {
                _cameraWorldY = heightMax + 1f;
                _cameraWorldDepth = heightMax - heightMin + 2f;
            } else {
                _cameraWorldY = 1;
                _cameraWorldDepth = 2;
            }
        }

        private bool TryGetSurfaceWorldYExtents(int surfaceIndex, Transform surface, out float minY, out float maxY) {
            minY = 0f;
            maxY = 0f;
            if (surface == null || !surface.gameObject.activeInHierarchy) return false;
            Mesh surfaceMesh = GetSurfaceMesh(surfaceIndex);
            if (surfaceMesh == null) return false;
            Bounds bounds = surfaceMesh.bounds;
            Vector3 extents = bounds.extents;
            Matrix4x4 localToWorld = surface.localToWorldMatrix;
            Vector3 worldCenter = localToWorld.MultiplyPoint3x4(bounds.center);

            // Fast exact Y extent for transformed AABB: |rowY| dot local extents.
            float worldExtentY =
                Mathf.Abs(localToWorld.m10) * extents.x +
                Mathf.Abs(localToWorld.m11) * extents.y +
                Mathf.Abs(localToWorld.m12) * extents.z;
            minY = worldCenter.y - worldExtentY;
            maxY = worldCenter.y + worldExtentY;
            return IsFinite(minY) && IsFinite(maxY) && maxY >= minY;
        }
        
        // Snaps a provided value based on the step size
        private float Snap(float v, float step) {
            if (v != v) return 0f;
            if (step <= _minSnapStep && step >= -_minSnapStep) return v;
            float snapped = Mathf.Round(v / step) * step;
            return snapped == snapped ? snapped : v;
        }
        
        // Precalculate hexagonal ring
        private void EnsureDrawCache(int drawAmount) {
            if (_cachedDrawAmount == drawAmount) return;
            _cachedDrawAmount = drawAmount;
            long totalParticleCountLong = (long)drawAmount * _particlesPerBatch;
            if (totalParticleCountLong > int.MaxValue) totalParticleCountLong = int.MaxValue;
            _cachedTotalParticleCount = (int)totalParticleCountLong;
            _cachedMaxHexRing = CalcMaxHexRing(_cachedTotalParticleCount);
            _cachedTripleMaxHexRing = CalcMaxHexRing((_cachedTotalParticleCount + 2) / 3);
        }

        private int CalcMaxHexRing(int particleCount) {
            if (particleCount <= 1) return 0;
            float lastIndex = particleCount - 1f;
            return Mathf.FloorToInt((Mathf.Sqrt(lastIndex * 12f - 3f) + 3f) / 6f);
        }

#if UNITY_EDITOR
        private void ApplyEditorRenderTextureResolutions() {
            RenderTexture surfaceRT = SurfaceCamera != null ? SurfaceCamera.targetTexture : null;
            if (ShouldApplyResolution(surfaceRT, SurfaceRTResolution)) {
                ApplySquareResolution(surfaceRT, (int)SurfaceRTResolution);
            }

            if (ShouldApplyResolution(TrailCRT, TrailCRTResolution)) {
                ApplySquareResolution(TrailCRT, (int)TrailCRTResolution);
                TrailCRT.Initialize();
            }
        }

        private bool ShouldApplyResolution(RenderTexture rt, TextureResolutionPreset preset) {
            if (rt == null || preset == TextureResolutionPreset.Custom) return false;
            int target = (int)preset;
            if (target < 64 || target > 2048) return false;
            return rt.width != target || rt.height != target;
        }

        private void ApplySquareResolution(RenderTexture rt, int size) {
            if (rt == null || size <= 0) return;
            if (rt.width == size && rt.height == size) return;
            rt.Release();
            rt.width = size;
            rt.height = size;
            rt.Create();
        }
#endif

        private bool HasCameraDataChanged(Vector4 oldData, Vector4 newData) {
            return Mathf.Abs(oldData.x - newData.x) > 1e-6f
                || Mathf.Abs(oldData.y - newData.y) > 1e-6f
                || Mathf.Abs(oldData.z - newData.z) > 1e-6f
                || Mathf.Abs(oldData.w - newData.w) > 1e-6f;
        }

        private Vector2 WorldXZToAxial(Vector2 worldXZ, float hexSize) {
            float invHexSize = 1f / Mathf.Max(hexSize, 1e-6f);
            float q = (_sqrt3 * 0.33333334f * worldXZ.x - 0.33333334f * worldXZ.y) * invHexSize;
            float r = (0.6666667f * worldXZ.y) * invHexSize;
            return new Vector2(q, r);
        }

        private Vector2 RoundAxial(Vector2 axial) {
            float q = axial.x;
            float r = axial.y;
            float s = -q - r;

            float rq = Mathf.Round(q);
            float rr = Mathf.Round(r);
            float rs = Mathf.Round(s);

            float qDiff = Mathf.Abs(rq - q);
            float rDiff = Mathf.Abs(rr - r);
            float sDiff = Mathf.Abs(rs - s);

            if (qDiff > rDiff && qDiff > sDiff) rq = -rr - rs;
            else if (rDiff > sDiff) rr = -rq - rs;

            return new Vector2(rq, rr);
        }

        private bool IsFinite(float value) {
            return value == value && value > -float.MaxValue && value < float.MaxValue;
        }
        
    }
    
    public enum TextureResolutionPreset {
        [InspectorName("Custom")]    Custom  =  0,
        [InspectorName("64x64")]     R64     =  64,
        [InspectorName("128x128")]   R128    =  128,
        [InspectorName("256x256")]   R256    =  256,
        [InspectorName("512x512")]   R512    =  512,
        [InspectorName("1024x1024")] R1024   =  1024,
        [InspectorName("2048x2048")] R2048   =  2048
    }
    
}
