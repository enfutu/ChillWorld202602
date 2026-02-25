using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GPUInfiniteGrass {
    public sealed class GrassShaderGUI : ShaderGUI {
        static GUIStyle _boxOuter;
        static GUIStyle _boxInner;
        static GUIStyle _foldout;

        static GUIStyle BoxOuter => _boxOuter ??= new GUIStyle("HelpBox") {
        padding = new RectOffset(0, 0, 0, 0),
        margin = new RectOffset(0, 0, 6, 6)
        };

        static GUIStyle BoxInner => _boxInner ??= new GUIStyle {
        padding = new RectOffset(10, 10, 8, 10),
        margin = new RectOffset(0, 0, 0, 0)
        };

        static GUIStyle FoldoutStyle => _foldout ??= new GUIStyle(EditorStyles.foldout) {
        margin = new RectOffset(8, 0, 0, 0),
        fontStyle = FontStyle.Bold,
        fontSize = 12
        };

        static Color HeaderBg => EditorGUIUtility.isProSkin
        ? new Color(0.14f, 0.14f, 0.14f, 1f)
        : new Color(0.90f, 0.90f, 0.90f, 1f);

        static Color HeaderLine => EditorGUIUtility.isProSkin
        ? new Color(1f, 1f, 1f, 0.08f)
        : new Color(0f, 0f, 0f, 0.12f);

        static Color Sep => EditorGUIUtility.isProSkin
        ? new Color(1f, 1f, 1f, 0.06f)
        : new Color(0f, 0f, 0f, 0.10f);

        static void Separator(float top = 6f, float bottom = 6f) {
            if (top > 0) GUILayout.Space(top);
            var r = EditorGUILayout.GetControlRect(false, 1f);
            EditorGUI.DrawRect(r, Sep);
            if (bottom > 0) GUILayout.Space(bottom);
        }

        sealed class Group {
            public readonly string Title;
            public readonly string Key;
            public readonly string[] Props;
            public readonly string EnableProp;
            public readonly Func<Material, bool> VisibleIf;
            public readonly Action<MaterialEditor, Material, MaterialProperty[]> CustomDraw;

            public Group(string title, string key, string[] props,
                string enableProp = null,
                Func<Material, bool> visibleIf = null,
                Action<MaterialEditor, Material, MaterialProperty[]> customDraw = null) {
                Title = title;
                Key = key;
                Props = props;
                EnableProp = enableProp;
                VisibleIf = visibleIf;
                CustomDraw = customDraw;
            }
        }

        static bool GetFoldout(string shaderName, string groupKey, bool def = true)
            => EditorPrefs.GetBool($"GrassShaderGUI::{shaderName}::{groupKey}", def);

        static void SetFoldout(string shaderName, string groupKey, bool v)
            => EditorPrefs.SetBool($"GrassShaderGUI::{shaderName}::{groupKey}", v);

        static readonly Dictionary<string, string> Tooltips = new Dictionary<string, string> {
            ["_MaskTex"] = "Surface mask render texture. Must be 1:1 aspect ratio.",
            ["_TrailTex"] = "Trail custom render texture. Must be 1:1 aspect ratio.",
            ["_GrassTexR"] = "Albedo texture",
            ["_GrassTexG"] = "Albedo texture",
            ["_GrassTexB"] = "Albedo texture",
            ["_GrassArrayR"] = "Texture array of albedo textures",
            ["_GrassArrayG"] = "Texture array of albedo textures",
            ["_GrassArrayB"] = "Texture array of albedo textures",
            ["_TextureModeR"] = "\"Single Texture\" - uses a regular texture for grass.\n\"Array Random\" - uses random textures from a texture array.\n\"Array By Size\" - uses textures from a texture array sorted by size.",
            ["_TextureModeG"] = "\"Single Texture\" - uses a regular texture for grass.\n\"Array Random\" - uses random textures from a texture array.\n\"Array By Size\" - uses textures from a texture array sorted by size.",
            ["_TextureModeB"] = "\"Single Texture\" - uses a regular texture for grass.\n\"Array Random\" - uses random textures from a texture array.\n\"Array By Size\" - uses textures from a texture array sorted by size.",
            ["_ColorLowR"] = "Color tint for low grass particles or bottom parts of high grass particles",
            ["_ColorLowG"] = "Color tint for low grass particles or bottom parts of high grass particles",
            ["_ColorLowB"] = "Color tint for low grass particles or bottom parts of high grass particles",
            ["_ColorHighR"] = "Color tint for upper parts of high grass particles",
            ["_ColorHighG"] = "Color tint for upper parts of high grass particles",
            ["_ColorHighB"] = "Color tint for upper parts of high grass particles",
            ["_BladeHeightR"] = "Base blade height",
            ["_BladeHeightG"] = "Base blade height",
            ["_BladeHeightB"] = "Base blade height",
            ["_BladeWidthR"] = "Base blade width",
            ["_BladeWidthG"] = "Base blade width",
            ["_BladeWidthB"] = "Base blade width",
            ["_SizeWidthImpactR"] = "How much size randomization affects width",
            ["_SizeWidthImpactG"] = "How much size randomization affects width",
            ["_SizeWidthImpactB"] = "How much size randomization affects width",
            ["_SizeRandomR"] = "Random size variance",
            ["_SizeRandomG"] = "Random size variance",
            ["_SizeRandomB"] = "Random size variance",
            ["_BendRandomR"] = "Random bend amount",
            ["_BendRandomG"] = "Random bend amount",
            ["_BendRandomB"] = "Random bend amount",
            ["_WindPowerR"] = "Wind amplitude multiplier",
            ["_WindPowerG"] = "Wind amplitude multiplier",
            ["_WindPowerB"] = "Wind amplitude multiplier",
            ["_BottomBlending"] = "Softens the bottom edge with dithering near the ground",
            ["_Cutoff"] = "Alpha cutoff threshold for grass textures",
            ["_EnableTrail"] = "Enable or disable trail bending and trail color response.",
            ["_TrailBrightness"] = "Dims the color of bended grass",
            ["_TrailBend"] = "How strongly trails bend grass",
            ["_ShadowPassDepthWrite"] = "Enables or disables writing into ShadowCaster depth for this material",
            ["_VisibleAmount"] = "Overall density multiplier",
            ["_EnableTripleCross"] = "Renders each grass particle as a triple crossed cluster (3 stacked blades).",
            ["_EdgeFade"] = "Fade out grass size towards the edge of the render area",
            ["_EdgeCulling"] = "Randomly cull grass near the edge to reduce overdrawing",
            ["_EdgeSimplifying"] = "How early distant grass starts simplifying. Higher value starts simplification closer to the camera.",
            ["_EdgeSimplifyingFade"] = "How smooth the simplification transition is. Higher value makes the transition wider and softer.",
            ["_MaskThreshold"] = "Minimum mask value required to spawn grass.",
            ["_SizeThreshold"] = "Minimum final size required to keep a grass blade rendered.",
            ["_YBias"] = "Vertical offset applied to all blades. Should be 0 in most of the cases.",
            ["_CloudsTex"] = "Cloud noise texture used to modulate lighting. (R) texture channel is used.",
            ["_CloudsScale"] = "Proportional scale for the cloud noise",
            ["_CloudsDir"] = "Cloud movement direction in XZ",
            ["_CloudsMasking"] = "Bias for cloud coverage",
            ["_CloudsSharpness"] = "Edge sharpness of clouds",
            ["_CloudsDarkness"] = "Minimum cloud lighting multiplier",
            ["_CloudsBrightness"] = "Maximum cloud lighting multiplier",
            ["_SSSColor"] = "Subsurface Scattering tint color",
            ["_SSSBrightness"] = "Subsurface Scattering intensity",
            ["_SSSRadius"] = "Subsurface Scattering radius controlling scatter amount",
            ["_SSSPower"] = "Subsurface Scattering angular falloff",
            ["_WindPower"] = "Global wind amplitude multiplier",
            ["_WindSpeed1"] = "Wind layer 1 speed.",
            ["_WindDir1"] = "Wind layer 1 direction (XZ).",
            ["_WindAmp1"] = "Wind layer 1 amplitude.",
            ["_WindFreq1"] = "Wind layer 1 frequency.",
            ["_WindSpeed2"] = "Wind layer 2 speed.",
            ["_WindDir2"] = "Wind layer 2 direction (XZ).",
            ["_WindAmp2"] = "Wind layer 2 amplitude.",
            ["_WindFreq2"] = "Wind layer 2 frequency.",
            ["_WindSpeed3"] = "Wind layer 3 speed.",
            ["_WindDir3"] = "Wind layer 3 direction (XZ).",
            ["_WindAmp3"] = "Wind layer 3 amplitude.",
            ["_WindFreq3"] = "Wind layer 3 frequency."
        };

        static GUIContent Label(MaterialProperty p) {
            if (p == null) return GUIContent.none;
            return Tooltips.TryGetValue(p.name, out var tip)
                ? new GUIContent(p.displayName, tip)
                : new GUIContent(p.displayName);
        }

        static MaterialProperty FindPropOrNull(string name, MaterialProperty[] props) {
            for (int i = 0; i < props.Length; i++)
                if (props[i] != null && props[i].name == name)
                    return props[i];
            return null;
        }


        static void DrawGeneric(MaterialEditor me, MaterialProperty[] props, MaterialProperty p) {
            if (p == null) return;
            if (p.type == MaterialProperty.PropType.Texture)
                me.TexturePropertySingleLine(Label(p), p);
            else
                me.ShaderProperty(p, Label(p));
        }

        static void DrawGenericNamed(MaterialEditor me, MaterialProperty p, string displayName) {
            if (p == null) return;
            var label = Label(p);
            label.text = displayName;
            if (p.type == MaterialProperty.PropType.Texture)
                me.TexturePropertySingleLine(label, p);
            else
                me.ShaderProperty(p, label);
        }

        static void DrawWindDirXZ(MaterialEditor me, MaterialProperty p) {
            if (p == null) return;
            EditorGUI.BeginChangeCheck();
            Vector2 v = new Vector2(p.vectorValue.x, p.vectorValue.z);
            var label = Label(p);
            label.text = label.text + " (XZ)";
            v = EditorGUILayout.Vector2Field(label, v);
            if (EditorGUI.EndChangeCheck())
                p.vectorValue = new Vector4(v.x, 0f, v.y, 0f);
        }

        static void DrawHeader(string title, float topSpace = 6f, float bottomSpace = 1f) {
            if (topSpace > 0f) GUILayout.Space(topSpace);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            if (bottomSpace > 0f) GUILayout.Space(bottomSpace);
        }

        static void ForceInstancing(MaterialEditor me) {
            var targets = me.targets;
            for (int i = 0; i < targets.Length; i++) {
                var mat = targets[i] as Material;
                if (mat != null && !mat.enableInstancing)
                    mat.enableInstancing = true;
            }
        }

        static void SetTexModeKeyword(Material mat, string kwArray, float mode) {
            if (mat == null) return;
            int m = Mathf.Clamp(Mathf.RoundToInt(mode), 0, 2);
            if (m == 0) mat.DisableKeyword(kwArray);
            else mat.EnableKeyword(kwArray);
        }

        static bool TryGetEnableKeyword(string enableProp, out string keyword) {
            if (enableProp == "_EnableTypeR") { keyword = "_GRASS_R"; return true; }
            if (enableProp == "_EnableTypeG") { keyword = "_GRASS_G"; return true; }
            if (enableProp == "_EnableTypeB") { keyword = "_GRASS_B"; return true; }
            if (enableProp == "_EnableTrail") { keyword = "_TRAIL"; return true; }
            if (enableProp == "_EnableTripleCross") { keyword = "_TRIPLE_CROSS"; return true; }
            if (enableProp == "_EnableClouds") { keyword = "_CLOUDS"; return true; }
            if (enableProp == "_EnableSSS") { keyword = "_SSS"; return true; }
            keyword = null;
            return false;
        }

        static void SyncEnableKeyword(Material mat, string enableProp) {
            if (mat == null || !mat.HasProperty(enableProp)) return;
            if (!TryGetEnableKeyword(enableProp, out var keyword)) return;
            if (mat.GetFloat(enableProp) > 0.5f) mat.EnableKeyword(keyword);
            else mat.DisableKeyword(keyword);
        }

        static void SyncEnableKeywords(MaterialEditor me) {
            var targets = me.targets;
            for (int i = 0; i < targets.Length; i++) {
                var mat = targets[i] as Material;
                if (mat == null) continue;
                SyncEnableKeyword(mat, "_EnableTypeR");
                SyncEnableKeyword(mat, "_EnableTypeG");
                SyncEnableKeyword(mat, "_EnableTypeB");
                SyncEnableKeyword(mat, "_EnableTrail");
                SyncEnableKeyword(mat, "_EnableTripleCross");
                SyncEnableKeyword(mat, "_EnableClouds");
                SyncEnableKeyword(mat, "_EnableSSS");
            }
        }

        static bool SyncArrayCount(Material mat, string arrayProp, string countProp) {
            if (mat == null || !mat.HasProperty(arrayProp) || !mat.HasProperty(countProp))
                return false;

            int depth = 1;
            var array = mat.GetTexture(arrayProp) as Texture2DArray;
            if (array != null)
                depth = Mathf.Max(array.depth, 1);

            float current = mat.GetFloat(countProp);
            if (Mathf.Abs(current - depth) < 0.0001f)
                return false;

            mat.SetFloat(countProp, depth);
            EditorUtility.SetDirty(mat);
            return true;
        }

        static void SyncArrayCounts(MaterialEditor me) {
            var targets = me.targets;
            bool changed = false;
            for (int i = 0; i < targets.Length; i++) {
                var mat = targets[i] as Material;
                if (mat == null) continue;
                changed |= SyncArrayCount(mat, "_GrassArrayR", "_ArrayCountR");
                changed |= SyncArrayCount(mat, "_GrassArrayG", "_ArrayCountG");
                changed |= SyncArrayCount(mat, "_GrassArrayB", "_ArrayCountB");
            }
            if (changed)
                GUI.changed = true;
        }

        static void OpenTextureArrayGenerator() {
            EditorWindow.GetWindow<TextureArrayGenerator>("Texture2DArray");
        }

        void DrawGroup(MaterialEditor me, Material mat, MaterialProperty[] props, string shaderName, Group g) {
            bool open = GetFoldout(shaderName, g.Key, true);
            var pEnable = string.IsNullOrEmpty(g.EnableProp) ? null : FindPropOrNull(g.EnableProp, props);
            bool sectionEnabled = pEnable == null || pEnable.floatValue > 0.5f;

            using (new EditorGUILayout.VerticalScope(BoxOuter)) {
                Rect headerRect = GUILayoutUtility.GetRect(0f, 22f, GUILayout.ExpandWidth(true));
                headerRect.xMin += 1f;
                headerRect.xMax -= 1f;

                EditorGUI.DrawRect(headerRect, HeaderBg);

                var line = headerRect;
                line.y = headerRect.yMax - 1f;
                line.height = 1f;
                EditorGUI.DrawRect(line, HeaderLine);

                var fold = headerRect;
                fold.xMin += 8f;
                if (pEnable != null) {
                    const float toggleWidth = 78f;
                    var toggleRect = headerRect;
                    toggleRect.xMin = headerRect.xMax - toggleWidth - 6f;
                    toggleRect.xMax = headerRect.xMax - 2f;
                    fold.xMax = toggleRect.xMin - 4f;

                    EditorGUI.showMixedValue = pEnable.hasMixedValue;
                    EditorGUI.BeginChangeCheck();
                    bool enabledValue = pEnable.floatValue > 0.5f;
                    enabledValue = EditorGUI.ToggleLeft(toggleRect, "Enabled", enabledValue);
                    if (EditorGUI.EndChangeCheck()) {
                        pEnable.floatValue = enabledValue ? 1f : 0f;
                        SyncEnableKeywords(me);
                    }
                    EditorGUI.showMixedValue = false;

                    sectionEnabled = pEnable.floatValue > 0.5f;
                }

                open = EditorGUI.Foldout(fold, open, g.Title, true, FoldoutStyle);
                SetFoldout(shaderName, g.Key, open);

                if (!open)
                    return;

                using (new EditorGUILayout.VerticalScope(BoxInner)) {
                    int prevIndent = EditorGUI.indentLevel;
                    EditorGUI.indentLevel = 0;

                    using (new EditorGUI.DisabledScope(!sectionEnabled)) {
                        g.CustomDraw?.Invoke(me, mat, props);

                        if (g.Props != null) {
                            for (int i = 0; i < g.Props.Length; i++) {
                                string propName = g.Props[i];
                                if (string.IsNullOrEmpty(propName)) {
                                    GUILayout.Space(6);
                                    continue;
                                }
                                var p = FindPropOrNull(propName, props);
                                DrawGeneric(me, props, p);
                            }
                        }
                    }

                    EditorGUI.indentLevel = prevIndent;
                }
            }
        }

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] props) {
            var mat = materialEditor.target as Material;
            if (mat == null) return;

            ForceInstancing(materialEditor);
            SyncArrayCounts(materialEditor);
            SyncEnableKeywords(materialEditor);

            string shaderName = mat.shader ? mat.shader.name : "UnknownShader";
            var groups = BuildGroups();

            foreach (var g in groups) {
                if (g.VisibleIf != null && !g.VisibleIf(mat))
                    continue;
                DrawGroup(materialEditor, mat, props, shaderName, g);
            }

            Separator(4, 8);

            var adv = new Group(
                title: "Advanced",
                key: "Advanced",
                props: Array.Empty<string>(),
                customDraw: (me, m, p) => {
                    DrawGeneric(me, p, FindPropOrNull("_ShadowPassDepthWrite", p));
                    GUILayout.Space(4f);
                    me.RenderQueueField();
                }
            );

            bool advOpen = GetFoldout(shaderName, adv.Key, false);
            using (new EditorGUILayout.VerticalScope(BoxOuter)) {
                Rect headerRect = GUILayoutUtility.GetRect(0f, 22f, GUILayout.ExpandWidth(true));
                headerRect.xMin += 1f;
                headerRect.xMax -= 1f;

                EditorGUI.DrawRect(headerRect, HeaderBg);

                var line = headerRect;
                line.y = headerRect.yMax - 1f;
                line.height = 1f;
                EditorGUI.DrawRect(line, HeaderLine);

                var fold = headerRect;
                fold.xMin += 8f;
                advOpen = EditorGUI.Foldout(fold, advOpen, adv.Title, true, FoldoutStyle);
                SetFoldout(shaderName, adv.Key, advOpen);

                if (advOpen) {
                    using (new EditorGUILayout.VerticalScope(BoxInner)) {
                        int prevIndent = EditorGUI.indentLevel;
                        EditorGUI.indentLevel = 0;
                        adv.CustomDraw?.Invoke(materialEditor, mat, props);
                        EditorGUI.indentLevel = prevIndent;
                    }
                }
            }
        }

        public override void ValidateMaterial(Material mat) {
            if (mat == null) return;
            SyncArrayCount(mat, "_GrassArrayR", "_ArrayCountR");
            SyncArrayCount(mat, "_GrassArrayG", "_ArrayCountG");
            SyncArrayCount(mat, "_GrassArrayB", "_ArrayCountB");
            SyncEnableKeyword(mat, "_EnableTypeR");
            SyncEnableKeyword(mat, "_EnableTypeG");
            SyncEnableKeyword(mat, "_EnableTypeB");
            SyncEnableKeyword(mat, "_EnableTrail");
            SyncEnableKeyword(mat, "_EnableTripleCross");
            SyncEnableKeyword(mat, "_EnableClouds");
            SyncEnableKeyword(mat, "_EnableSSS");
        }

        static void DrawGrassType(MaterialEditor me, MaterialProperty[] props,
            string kwArray,
            string textureMode, string tex2D, string texArray,
            string colorLow, string colorHigh,
            string bladeHeight, string bladeWidth, string sizeWidthImpact,
            string sizeRandom, string bendRandom, string windPower) {

            var pTextureMode = FindPropOrNull(textureMode, props);
            var pTex2D = FindPropOrNull(tex2D, props);
            var pTexArray = FindPropOrNull(texArray, props);

            if (pTextureMode != null)
                me.ShaderProperty(pTextureMode, Label(pTextureMode));
            if (pTextureMode != null) {
                var targets = me.targets;
                for (int i = 0; i < targets.Length; i++) {
                    var mat = targets[i] as Material;
                    if (mat != null)
                        SetTexModeKeyword(mat, kwArray, pTextureMode.floatValue);
                }
            }

            bool useArrayOn = pTextureMode != null && pTextureMode.floatValue > 0.5f;
            if (useArrayOn) {
                using (new EditorGUILayout.HorizontalScope()) {
                    if (pTexArray != null)
                        me.TexturePropertySingleLine(Label(pTexArray), pTexArray);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button(new GUIContent("Create Texture Array", "Open the Texture2DArray Generator tool."),
                        GUILayout.Width(142f), GUILayout.Height(18f)))
                        OpenTextureArrayGenerator();
                }
            } else {
                DrawGeneric(me, props, pTex2D);
            }

            DrawGeneric(me, props, FindPropOrNull(colorLow, props));
            DrawGeneric(me, props, FindPropOrNull(colorHigh, props));
            DrawGeneric(me, props, FindPropOrNull(bladeHeight, props));
            DrawGeneric(me, props, FindPropOrNull(bladeWidth, props));
            DrawGeneric(me, props, FindPropOrNull(sizeWidthImpact, props));
            DrawGeneric(me, props, FindPropOrNull(sizeRandom, props));
            DrawGeneric(me, props, FindPropOrNull(bendRandom, props));
            DrawGeneric(me, props, FindPropOrNull(windPower, props));
        }

        static List<Group> BuildGroups() {
            var list = new List<Group>(8);

            list.Add(
                new Group(
                    title: "Grass Type R",
                    key: "TypeR",
                    props: Array.Empty<string>(),
                    enableProp: "_EnableTypeR",
                    customDraw: (me, mat, props) => {
                        DrawGrassType(me, props,
                            "_TEXMODE_R_ARRAY",
                            "_TextureModeR", "_GrassTexR", "_GrassArrayR",
                            "_ColorLowR", "_ColorHighR",
                            "_BladeHeightR", "_BladeWidthR", "_SizeWidthImpactR",
                            "_SizeRandomR", "_BendRandomR", "_WindPowerR");
                    }
                ));

            list.Add(
                new Group(
                    title: "Grass Type G",
                    key: "TypeG",
                    props: Array.Empty<string>(),
                    enableProp: "_EnableTypeG",
                    customDraw: (me, mat, props) => {
                        DrawGrassType(me, props,
                            "_TEXMODE_G_ARRAY",
                            "_TextureModeG", "_GrassTexG", "_GrassArrayG",
                            "_ColorLowG", "_ColorHighG",
                            "_BladeHeightG", "_BladeWidthG", "_SizeWidthImpactG",
                            "_SizeRandomG", "_BendRandomG", "_WindPowerG");
                    }
                ));

            list.Add(
                new Group(
                    title: "Grass Type B",
                    key: "TypeB",
                    props: Array.Empty<string>(),
                    enableProp: "_EnableTypeB",
                    customDraw: (me, mat, props) => {
                        DrawGrassType(me, props,
                            "_TEXMODE_B_ARRAY",
                            "_TextureModeB", "_GrassTexB", "_GrassArrayB",
                            "_ColorLowB", "_ColorHighB",
                            "_BladeHeightB", "_BladeWidthB", "_SizeWidthImpactB",
                            "_SizeRandomB", "_BendRandomB", "_WindPowerB");
                    }
                ));

            list.Add(
                new Group(
                    title: "Common",
                    key: "Common",
                    props: new[] {
                        "_MaskTex",
                        "",
                        "_VisibleAmount",
                        "_Cutoff",
                        "_BottomBlending",
                        "",
                        "_MaskThreshold",
                        "_SizeThreshold",
                        "",
                        "_YBias",
                        "",
                        "_EnableTripleCross"
                    }
                ));

            list.Add(
                new Group(
                    title: "Grass Simplifying",
                    key: "GrassSimplifying",
                    props: Array.Empty<string>(),
                    customDraw: (me, mat, props) => {
                        DrawGenericNamed(me, FindPropOrNull("_EdgeFade", props), "Fade");
                        DrawGenericNamed(me, FindPropOrNull("_EdgeCulling", props), "Culling");
                        DrawGenericNamed(me, FindPropOrNull("_EdgeSimplifying", props), "Simplifying");
                        DrawGenericNamed(me, FindPropOrNull("_EdgeSimplifyingFade", props), "Simplifying Fade");
                    }
                ));

            list.Add(
                new Group(
                    title: "Trail",
                    key: "Trail",
                    enableProp: "_EnableTrail",
                    props: new[] {
                        "_TrailTex",
                        "_TrailBrightness",
                        "_TrailBend"
                    }
                ));
            
            list.Add(
                new Group(
                    title: "Clouds",
                    key: "Clouds",
                    props: Array.Empty<string>(),
                    enableProp: "_EnableClouds",
                    customDraw: (me, mat, props) => {
                        DrawGeneric(me, props, FindPropOrNull("_CloudsTex", props));
                        DrawGeneric(me, props, FindPropOrNull("_CloudsScale", props));
                        DrawWindDirXZ(me, FindPropOrNull("_CloudsDir", props));
                        DrawGeneric(me, props, FindPropOrNull("_CloudsMasking", props));
                        DrawGeneric(me, props, FindPropOrNull("_CloudsSharpness", props));
                        DrawGeneric(me, props, FindPropOrNull("_CloudsDarkness", props));
                        DrawGeneric(me, props, FindPropOrNull("_CloudsBrightness", props));
                    }
                ));
            
            list.Add(
                new Group(
                    title: "Subsurface Scattering",
                    key: "SSS",
                    enableProp: "_EnableSSS",
                    props: new[] {
                        "_SSSColor",
                        "_SSSBrightness",
                        "_SSSRadius"
                    }
                ));

            list.Add(
                new Group(
                    title: "Wind",
                    key: "Wind",
                    props: Array.Empty<string>(),
                    customDraw: (me, mat, props) => {
                        
                        DrawGeneric(me, props, FindPropOrNull("_WindPower", props));
                        
                        DrawHeader("Wind 1");
                        DrawGeneric(me, props, FindPropOrNull("_WindSpeed1", props));
                        DrawWindDirXZ(me, FindPropOrNull("_WindDir1", props));
                        DrawGeneric(me, props, FindPropOrNull("_WindAmp1", props));
                        DrawGeneric(me, props, FindPropOrNull("_WindFreq1", props));
                        
                        DrawHeader("Wind 2");
                        DrawGeneric(me, props, FindPropOrNull("_WindSpeed2", props));
                        DrawWindDirXZ(me, FindPropOrNull("_WindDir2", props));
                        DrawGeneric(me, props, FindPropOrNull("_WindAmp2", props));
                        DrawGeneric(me, props, FindPropOrNull("_WindFreq2", props));
                        
                        DrawHeader("Wind 3");
                        DrawGeneric(me, props, FindPropOrNull("_WindSpeed3", props));
                        DrawWindDirXZ(me, FindPropOrNull("_WindDir3", props));
                        DrawGeneric(me, props, FindPropOrNull("_WindAmp3", props));
                        DrawGeneric(me, props, FindPropOrNull("_WindFreq3", props));

                    }
                ));

            return list;
        }
    }
}
