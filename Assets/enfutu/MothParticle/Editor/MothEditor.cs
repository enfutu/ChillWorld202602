using UnityEditor;
using UnityEngine;

public class MothEditor : ShaderGUI
{
    // 折りたたみ用の状態を保持
    private bool showColorSection = true;
    private bool showTransformSection = true;
    private bool showModeSection = true;
    private bool showExtensionsSection = true;
    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        // 各プロパティを取得
        MaterialProperty _Color = FindProperty("_Color", properties);
        MaterialProperty _RandomColor = FindProperty("_RandomColor", properties);
        MaterialProperty _MainTex = FindProperty("_MainTex", properties);
        MaterialProperty _GradMin = FindProperty("_GradMin", properties);
        MaterialProperty _GradMax = FindProperty("_GradMax", properties);
        MaterialProperty _RandomGloss = FindProperty("_RandomGloss", properties);
        MaterialProperty _Boost = FindProperty("_Boost", properties);
        MaterialProperty _MaskPower = FindProperty("_MaskPower", properties);
        MaterialProperty _LightValueMin = FindProperty("_LightValueMin", properties);
        MaterialProperty _ParticleSize = FindProperty("_ParticleSize", properties);
        MaterialProperty _OrbitalRange = FindProperty("_OrbitalRange", properties);
        MaterialProperty _Flap = FindProperty("_Flap", properties);
        MaterialProperty _WingSpeed = FindProperty("_WingSpeed", properties);
        MaterialProperty _LightHeight = FindProperty("_LightHeight", properties);
        MaterialProperty _EnablePhototaxis = FindProperty("_EnablePhototaxis", properties);
        MaterialProperty _FreeMothMode = FindProperty("_FreeMothMode", properties);
        MaterialProperty _UseFakeShadow = FindProperty("_UseFakeShadow", properties);

        MaterialProperty[] _Pos = new MaterialProperty[8];
        MaterialProperty[] _Col = new MaterialProperty[8];
        for (int i = 0; i < 8; i++)
        {
            _Pos[i] = FindProperty($"_Pos{i}", properties);
            _Col[i] = FindProperty($"_Col{i}", properties);
        }


        //Color
        showColorSection = EditorGUILayout.Foldout(showColorSection, "---Color---", true);
        if (showColorSection)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            materialEditor.ColorProperty(_Color, "AddColor");
            materialEditor.ShaderProperty(_RandomColor, "AddColorRandomize");
            
            EditorGUILayout.Space();
            
            materialEditor.TexturePropertySingleLine(new GUIContent("Texture"), _MainTex);

            EditorGUILayout.Space();
            
            materialEditor.ColorProperty(_GradMin, "(Gloss)Gradiant_Min");
            materialEditor.ColorProperty(_GradMax, "(Gloss)Gradiant_Max");
            materialEditor.ShaderProperty(_RandomGloss, "(Gloss)ColorRandomize");
            materialEditor.FloatProperty(_Boost, "(Gloss)Gradient_Boost");
            materialEditor.FloatProperty(_MaskPower, "(Gloss)Contrast");

            EditorGUILayout.Space();
            
            materialEditor.FloatProperty(_LightValueMin, "LowerBrightnessLimit");
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space();

        //Transform
        showTransformSection = EditorGUILayout.Foldout(showTransformSection, "---Transform---", true);
        if (showTransformSection)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            materialEditor.FloatProperty(_ParticleSize, "(Particle)Size");
            materialEditor.FloatProperty(_OrbitalRange, "(Orbital)Size");
            materialEditor.ShaderProperty(_Flap, "WingFlap");
            materialEditor.FloatProperty(_WingSpeed, "WingSpeed");
            materialEditor.FloatProperty(_LightHeight, "LightHeight Offset");
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space();

        //Mode
        showModeSection = EditorGUILayout.Foldout(showModeSection, "---Mode---", true);
        if (showModeSection)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            materialEditor.ShaderProperty(_EnablePhototaxis, "Enable Phototaxis");
            materialEditor.ShaderProperty(_FreeMothMode, "Enable FreeMothMode");
            materialEditor.ShaderProperty(_UseFakeShadow, "UseFakeShadow");
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space();

        //Extensions
        showExtensionsSection = EditorGUILayout.Foldout(showExtensionsSection, "--- Extensions ---", true);
        if (showExtensionsSection)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            for (int i = 0; i < 8; i++)
            {
                EditorGUILayout.BeginHorizontal();

                //VectorPropertyの領域
                Rect vectorRect = EditorGUILayout.GetControlRect(GUILayout.Height(EditorGUIUtility.singleLineHeight));

                //ベクトル入力部
                EditorGUI.BeginChangeCheck();
                Vector4 newVector = EditorGUI.Vector3Field(vectorRect, "", _Pos[i].vectorValue);
                if (EditorGUI.EndChangeCheck())
                {
                    _Pos[i].vectorValue = newVector;
                    materialEditor.PropertiesChanged();
                }

                //範囲を合わせる
                Rect dropRect = vectorRect;

                //ドロップゾーン
                GUI.Box(dropRect, GUIContent.none);

                //D&Dの処理
                Event currentEvent = Event.current;
                if (dropRect.Contains(currentEvent.mousePosition))
                {
                    if (currentEvent.type == EventType.DragUpdated || currentEvent.type == EventType.DragPerform)
                    {
                        //ドラッグ中のオブジェクトがGameObjectか確認
                        if (DragAndDrop.objectReferences.Length > 0 && DragAndDrop.objectReferences[0] is GameObject)
                        {
                            DragAndDrop.visualMode = DragAndDropVisualMode.Link; // ドラッグ中のカーソル表示

                            if (currentEvent.type == EventType.DragPerform)
                            {
                                GameObject droppedObject = DragAndDrop.objectReferences[0] as GameObject;
                                if (droppedObject != null)
                                {
                                    //ドロップされたGameObjectの位置を取得してセット
                                    Vector3 pos = droppedObject.transform.position;
                                    _Pos[i].vectorValue = new Vector4(pos.x, pos.y, pos.z, 1);
                                    materialEditor.PropertiesChanged();

                                    DragAndDrop.AcceptDrag();    //ドラッグ操作を完了
                                    GUI.FocusControl(null);      //フォーカス解除
                                }
                            }
                        }
                    }
                }

                //カラー入力部
                _Col[i].colorValue = EditorGUILayout.ColorField(_Col[i].colorValue, GUILayout.Width(50));

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space();

        materialEditor.RenderQueueField(); //RenderQueueの表示
    }
}
