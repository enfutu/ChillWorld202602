using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace GPUInfiniteGrass {
    [InitializeOnLoad]
    public class ParticleSurfaceManagerEditor : Editor {

        private static ParticleSurfaceManager[] _particleSurfaceManagers;
        
        static ParticleSurfaceManagerEditor() {
            Camera.onPreCull += OnPreCull;
            EditorApplication.delayCall += RefreshManagersListAndInitialize;
            EditorApplication.projectChanged += RefreshManagersListAndInitialize;
            EditorSceneManager.sceneSaved += RefreshManagersListAndInitialize;
            EditorApplication.hierarchyChanged += RefreshManagersListAndInitialize;
        }

        private static void RefreshManagersListAndInitialize(Scene scene) {
            RefreshManagersListAndInitialize();
        }

        // Searching for ParticleVolumeManagers
        private static void RefreshManagersListAndInitialize() {
            _particleSurfaceManagers = FindObjectsOfType<ParticleSurfaceManager>();
        }
        
        // Is this camera last focused scene camera or a game camera
        private static bool IsLastCamera(Camera cam) {
            if (!cam) return false;
            if (cam.cameraType == CameraType.SceneView) {
                var sv = SceneView.lastActiveSceneView;
                return sv && sv.camera == cam;
            }
            if (cam.cameraType == CameraType.Game) {
                var main = Camera.main;
                if (main) return cam == main;
                return cam.targetTexture == null && (cam.hideFlags & HideFlags.HideAndDontSave) == 0 && cam.pixelWidth > 16 && cam.pixelHeight > 16;
            }
            return false;
        }

        // Draw Grass without Play Mode
        private static void OnPreCull(Camera cam) {
            if (_particleSurfaceManagers == null || _particleSurfaceManagers.Length == 0) return;
            if (Application.isPlaying) return;
            if (cam.cameraType != CameraType.SceneView && cam.cameraType != CameraType.Game) return;
            if (cam.pixelWidth < 16 || cam.pixelHeight < 16) return;
            if (cam.cameraType == CameraType.Game && !cam.enabled) return;
            for (int i = 0; i < _particleSurfaceManagers.Length; i++) {
                var manager = _particleSurfaceManagers[i];
                if (manager == null) continue;
                if (IsLastCamera(cam)) {
                    Vector3 realCameraPosition = cam.transform.position;
                    Vector3 renderPosition = manager.TargetOverride == null ? realCameraPosition : manager.TargetOverride.position;
                    manager.UpdateSurface(renderPosition, realCameraPosition, true);
                    if (manager.EnableTrail) manager.UpdateTrail();
                }
                manager.DrawGrass(cam);
            }
        }

    }
}
