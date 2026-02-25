using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace GPUInfiniteGrass {
    
    public class TextureArrayGenerator : EditorWindow {
        
        private static readonly int[] Sizes = { 32, 64, 128, 256, 512, 1024, 2048, 4096 };
        private static readonly string[] SizeLabels = { "32", "64", "128", "256", "512", "1024", "2048", "4096" };
        
        private readonly List<Texture2D> _textures = new List<Texture2D>();
        private ReorderableList _list;
        private int _widthIndex = 5;
        private int _heightIndex = 5;
        
        private void OnEnable() {
            _list = new ReorderableList(_textures, typeof(Texture2D), true, true, true, true);
            _list.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Textures");
            _list.elementHeight = EditorGUIUtility.singleLineHeight + 6;
            _list.drawElementCallback = (rect, index, active, focused) => {
                rect.y += 2;
                rect.height = EditorGUIUtility.singleLineHeight;
                _textures[index] = (Texture2D)EditorGUI.ObjectField(rect, _textures[index], typeof(Texture2D), false);
            };
            _list.onAddCallback = list => _textures.Add(null);
        }
        
        [MenuItem("Tools/GPU Grass/Texture2DArray Generator")]
        private static void Open() {
            GetWindow<TextureArrayGenerator>("Texture2DArray");
        }
        
        private void OnGUI() {
            EditorGUILayout.LabelField("Resolution", EditorStyles.boldLabel);
            _widthIndex = EditorGUILayout.Popup("Width", _widthIndex, SizeLabels);
            _heightIndex = EditorGUILayout.Popup("Height", _heightIndex, SizeLabels);
            
            EditorGUILayout.Space();
            if (_list != null) {
                Rect listRect = GUILayoutUtility.GetRect(0, _list.GetHeight(), GUILayout.ExpandWidth(true));
                _list.DoList(listRect);
                HandleDragDrop(listRect);
            }
            
            if (GUILayout.Button("Generate")) Generate();
        }
        
        private void Generate() {
            List<Texture2D> textures = new List<Texture2D>();
            for (int i = 0; i < _textures.Count; i++) {
                if (_textures[i] != null) textures.Add(_textures[i]);
            }
            if (textures.Count == 0) {
                EditorUtility.DisplayDialog("Texture2DArray Generator", "Add at least one texture.", "OK");
                return;
            }
            
            string path = EditorUtility.SaveFilePanelInProject(
                "Save Texture2DArray",
                "NewTextureArray",
                "asset",
                "Choose location and name for the Texture2DArray asset."
            );
            if (string.IsNullOrEmpty(path)) return;
            
            int width = Sizes[Mathf.Clamp(_widthIndex, 0, Sizes.Length - 1)];
            int height = Sizes[Mathf.Clamp(_heightIndex, 0, Sizes.Length - 1)];
            
            Texture2D first = textures[0];
            var settings = GetTextureSettings(first);
            
            bool useMipmaps = settings.Mipmaps;
            bool linear = settings.Linear;
            TextureFormat arrayFormat = first.format;
            if (!SystemInfo.SupportsTextureFormat(arrayFormat)) arrayFormat = TextureFormat.RGBA32;
            
            if (settings.PreserveCoverage) {
                Debug.LogWarning("Texture2DArray Generator: Preserve Coverage uses per-mip resample; exact match gives the best result.");
            }
            
            bool needsReadable = settings.PreserveCoverage || AnyResampleNeeded(textures, width, height);
            List<ImporterState> restored = null;
            if (needsReadable) restored = EnsureReadable(textures);
            
            TextureFormat finalFormat = TextureFormat.RGBA32;
            var array = new Texture2DArray(width, height, textures.Count, finalFormat, useMipmaps, linear) {
                wrapModeU = settings.WrapU,
                wrapModeV = settings.WrapV,
                wrapModeW = settings.WrapW,
                filterMode = settings.Filter,
                anisoLevel = settings.Aniso,
                mipMapBias = settings.MipBias
            };
            SetOptionalBoolProperty(array, "alphaIsTransparency", settings.AlphaIsTransparency);
            SetOptionalBoolProperty(array, "mipmapPreserveCoverage", settings.PreserveCoverage);
            SetOptionalFloatProperty(array, "alphaTestReferenceValue", settings.AlphaTestRef);
            
            int mipCount = useMipmaps ? array.mipmapCount : 1;
            
            for (int i = 0; i < textures.Count; i++) {
                Texture2D src = textures[i];
                int srcMipCount = Mathf.Max(1, src.mipmapCount);
                
                for (int mip = 0; mip < mipCount; mip++) {
                    int arrW = Mathf.Max(1, width >> mip);
                    int arrH = Mathf.Max(1, height >> mip);
                    int srcMip = Mathf.Min(FindBestMip(src.width, src.height, arrW, arrH), srcMipCount - 1);
                    int srcW = Mathf.Max(1, src.width >> srcMip);
                    int srcH = Mathf.Max(1, src.height >> srcMip);
                    
                    Texture2D mipTex = ExtractMipTexture(src, srcMip, linear);
                    float targetCoverage = settings.PreserveCoverage
                        ? ComputeCoverage(mipTex.GetPixels(0), settings.AlphaTestRef, 1f)
                        : -1f;
                    
                    Texture2D scaled = (srcW == arrW && srcH == arrH)
                        ? mipTex
                        : ResizeTexture(mipTex, arrW, arrH, linear, false);
                    
                    if (settings.PreserveCoverage && targetCoverage >= 0f) {
                        MatchCoverage(scaled, settings.AlphaTestRef, targetCoverage);
                    }
                    
                    array.SetPixels(scaled.GetPixels(), i, mip);
                    
                    if (scaled != mipTex) DestroyImmediate(scaled);
                    DestroyImmediate(mipTex);
                }
            }
            
            array.Apply(false, false);
            if (restored != null && restored.Count > 0) RestoreImporters(restored);
            
            var existing = AssetDatabase.LoadAssetAtPath<Texture2DArray>(path);
            if (existing != null) {
                EditorUtility.CopySerialized(array, existing);
                EditorUtility.SetDirty(existing);
                DestroyImmediate(array);
                AssetDatabase.SaveAssets();
                EditorUtility.FocusProjectWindow();
                Selection.activeObject = existing;
            } else {
                AssetDatabase.CreateAsset(array, path);
                AssetDatabase.SaveAssets();
                EditorUtility.FocusProjectWindow();
                Selection.activeObject = array;
            }
        }
        
        private static Texture2D ResizeTexture(Texture2D src, int width, int height, bool linear, bool mipmaps) {
            RenderTextureReadWrite readWrite = linear ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.sRGB;
            RenderTexture rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, readWrite);
            RenderTexture previous = RenderTexture.active;
            Graphics.Blit(src, rt);
            RenderTexture.active = rt;
            
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, mipmaps, linear);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply(mipmaps, false);
            
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            return tex;
        }
        
        private static Texture2D ExtractMipTexture(Texture2D src, int mip, bool linear) {
            int w = Mathf.Max(1, src.width >> mip);
            int h = Mathf.Max(1, src.height >> mip);
            if (src.isReadable) {
                Color[] pixels = src.GetPixels(mip);
                Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false, linear);
                tex.SetPixels(pixels);
                tex.Apply(false, false);
                return tex;
            }
            
            RenderTextureReadWrite readWrite = linear ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.sRGB;
            RenderTexture rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, readWrite);
            RenderTexture previous = RenderTexture.active;
            // CopyTexture is not compatible with some compressed source formats.
            Graphics.Blit(src, rt);
            RenderTexture.active = rt;
            
            Texture2D texOut = new Texture2D(w, h, TextureFormat.RGBA32, false, linear);
            texOut.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            texOut.Apply(false, false);
            
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            return texOut;
        }
        
        private static bool AnyResampleNeeded(List<Texture2D> textures, int targetW, int targetH) {
            for (int i = 0; i < textures.Count; i++) {
                Texture2D t = textures[i];
                if (t == null) continue;
                if (t.width != targetW || t.height != targetH) return true;
            }
            return false;
        }
        
        private static int FindBestMip(int srcW, int srcH, int targetW, int targetH) {
            int bestMip = 0;
            int bestOversize = int.MaxValue;
            int bestClosest = int.MaxValue;
            int mip = 0;
            int w = srcW;
            int h = srcH;
            while (true) {
                bool sameAspect = (w * targetH) == (h * targetW);
                int oversize = (w >= targetW && h >= targetH) ? (w - targetW) + (h - targetH) : int.MaxValue;
                int closest = Mathf.Abs(w - targetW) + Mathf.Abs(h - targetH);
                
                if (sameAspect && oversize < bestOversize) {
                    bestOversize = oversize;
                    bestMip = mip;
                }
                if (bestOversize == int.MaxValue && closest < bestClosest) {
                    bestClosest = closest;
                    bestMip = mip;
                }
                
                if (w == 1 && h == 1) break;
                w = Mathf.Max(1, w >> 1);
                h = Mathf.Max(1, h >> 1);
                mip++;
            }
            return bestMip;
        }
        
        private static void MatchCoverage(Texture2D tex, float alphaRef, float target) {
            Color[] pixels = tex.GetPixels();
            float scale = FindAlphaScale(pixels, alphaRef, target);
            for (int i = 0; i < pixels.Length; i++) pixels[i].a = Mathf.Clamp01(pixels[i].a * scale);
            tex.SetPixels(pixels);
            tex.Apply(false, false);
        }
        
        private struct ImporterState {
            public string Path;
            public bool Readable;
        }
        
        private static List<ImporterState> EnsureReadable(List<Texture2D> textures) {
            List<ImporterState> states = new List<ImporterState>();
            for (int i = 0; i < textures.Count; i++) {
                string path = AssetDatabase.GetAssetPath(textures[i]);
                if (string.IsNullOrEmpty(path)) continue;
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                if (importer.isReadable) continue;
                states.Add(new ImporterState { Path = path, Readable = importer.isReadable });
                importer.isReadable = true;
                importer.SaveAndReimport();
            }
            return states;
        }
        
        private static void RestoreImporters(List<ImporterState> states) {
            for (int i = 0; i < states.Count; i++) {
                var importer = AssetImporter.GetAtPath(states[i].Path) as TextureImporter;
                if (importer == null) continue;
                importer.isReadable = states[i].Readable;
                importer.SaveAndReimport();
            }
        }
        
        private static float FindAlphaScale(Color[] pixels, float alphaRef, float target) {
            float lo = 0f;
            float hi = 8f;
            for (int i = 0; i < 12; i++) {
                float mid = (lo + hi) * 0.5f;
                float c = ComputeCoverage(pixels, alphaRef, mid);
                if (c > target) hi = mid;
                else lo = mid;
            }
            return (lo + hi) * 0.5f;
        }
        
        private static float ComputeCoverage(Color[] pixels, float alphaRef, float scale) {
            int count = pixels.Length;
            int covered = 0;
            for (int i = 0; i < count; i++) {
                if (pixels[i].a * scale > alphaRef) covered++;
            }
            return count > 0 ? (float)covered / count : 0f;
        }
        
        
        private void HandleDragDrop(Rect rect) {
            Event e = Event.current;
            if (e == null) return;
            if (!rect.Contains(e.mousePosition)) return;
            
            if (e.type == EventType.DragUpdated || e.type == EventType.DragPerform) {
                bool hasTexture = false;
                UnityEngine.Object[] refs = DragAndDrop.objectReferences;
                for (int i = 0; i < refs.Length; i++) {
                    if (refs[i] is Texture2D) { hasTexture = true; break; }
                }
                if (!hasTexture) return;
                
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                if (e.type == EventType.DragPerform) {
                    DragAndDrop.AcceptDrag();
                    for (int i = 0; i < refs.Length; i++) {
                        if (refs[i] is Texture2D tex) _textures.Add(tex);
                    }
                }
                e.Use();
            }
        }
        
        private struct TextureSettings {
            public bool Linear;
            public bool Mipmaps;
            public bool AlphaIsTransparency;
            public bool PreserveCoverage;
            public float AlphaTestRef;
            public FilterMode Filter;
            public TextureWrapMode WrapU;
            public TextureWrapMode WrapV;
            public TextureWrapMode WrapW;
            public int Aniso;
            public float MipBias;
        }
        
        private static TextureSettings GetTextureSettings(Texture2D tex) {
            TextureSettings s = new TextureSettings {
                Linear = false,
                Mipmaps = tex.mipmapCount > 1,
                AlphaIsTransparency = false,
                PreserveCoverage = false,
                AlphaTestRef = 0.5f,
                Filter = tex.filterMode,
                WrapU = tex.wrapModeU,
                WrapV = tex.wrapModeV,
                WrapW = tex.wrapModeW,
                Aniso = tex.anisoLevel,
                MipBias = tex.mipMapBias
            };
            
            string path = AssetDatabase.GetAssetPath(tex);
            if (string.IsNullOrEmpty(path)) return s;
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return s;
            
            s.Linear = !importer.sRGBTexture;
            s.Mipmaps = importer.mipmapEnabled;
            s.AlphaIsTransparency = importer.alphaIsTransparency;
            s.PreserveCoverage = GetOptionalBoolProperty(importer, "mipmapPreserveCoverage", false);
            s.AlphaTestRef = GetOptionalFloatProperty(importer, "alphaTestReferenceValue", 0.5f);
            s.Filter = importer.filterMode;
            s.WrapU = importer.wrapModeU;
            s.WrapV = importer.wrapModeV;
            s.WrapW = importer.wrapModeW;
            s.Aniso = importer.anisoLevel;
            return s;
        }
        
        private static void SetOptionalBoolProperty(Texture tex, string name, bool value) {
            PropertyInfo prop = typeof(Texture).GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            if (prop == null || !prop.CanWrite) return;
            prop.SetValue(tex, value, null);
        }
        
        private static void SetOptionalFloatProperty(Texture tex, string name, float value) {
            PropertyInfo prop = typeof(Texture).GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            if (prop == null || !prop.CanWrite) return;
            prop.SetValue(tex, value, null);
        }
        
        private static bool GetOptionalBoolProperty(object obj, string name, bool fallback) {
            PropertyInfo prop = obj.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            if (prop == null || !prop.CanRead) return fallback;
            return (bool)prop.GetValue(obj, null);
        }
        
        private static float GetOptionalFloatProperty(object obj, string name, float fallback) {
            PropertyInfo prop = obj.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            if (prop == null || !prop.CanRead) return fallback;
            return (float)prop.GetValue(obj, null);
        }
        
    }
    
}
