using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using System;

namespace nmSeashore
{
	public class SeashoreShaderGUI : ShaderGUI
	{
		//Add
		MaterialProperty _Center;
		MaterialProperty _FogColor;


        MaterialProperty _TimeScale;
		MaterialProperty _ManualTimeOffset;
		MaterialProperty _SideScrollSpeed;
		MaterialProperty _SideScrollOffset;
		MaterialProperty _TimeDistortionPower;
		MaterialProperty _TimeDistortionWidth;
		MaterialProperty _TimeDistortionNoiseLevel;
		
		MaterialProperty _BeachSlope;

		MaterialProperty _WaterGradient;
		MaterialProperty _AttenuationScale;
		MaterialProperty _Interval;
		MaterialProperty _NoiseOctaves;
		MaterialProperty _SeaLevel;

		MaterialProperty _Normal;
		MaterialProperty _NormalAmount;
		MaterialProperty _ReflectionIntensity;
		MaterialProperty _ApplySunSpecular;
		MaterialProperty _SunBrightnessMultiplier;
		MaterialProperty _SpecularSoftEdge;
		MaterialProperty _SourceAngleCos;
		MaterialProperty _FormMask;
		MaterialProperty _BackFormLength;

		MaterialProperty _WavePower;
		MaterialProperty _WaveWidth;
		MaterialProperty _WaveBreakThreshold;
		MaterialProperty _Steepness;
		MaterialProperty _NumberOfWaves;

		MaterialProperty _FarWaveHeight;
		MaterialProperty _FarWaveDecayDistance;

		MaterialProperty _SwellZoneBuffer;
		MaterialProperty _FrontWaveNoiseLevel;
		MaterialProperty _FrontWaveWidth;
		MaterialProperty _BackwashVelocity;

		MaterialProperty _DebugTriangle;
		MaterialProperty _DebugWaveHeightVisualize;
		MaterialProperty _DebugPreviewMask;
		MaterialProperty _DebugDividerLine;

		// Parallax
		MaterialProperty _GroundTex;
		MaterialProperty _WetGroundTint;
		MaterialProperty _FormShadow;
		MaterialProperty _InvIOR;

		// PC
		MaterialProperty _MaxTessellationFactor;
		MaterialProperty _TessellationFalloffThreshold;
		MaterialProperty _TessellationFalloffExponent;

		// PC Transparent
		MaterialProperty _WaterDistortionScale;
		
		private static readonly Dictionary<string, string> Labels = new Dictionary<string, string>
		{
			{ "General Settings", "描画設定" },
			{ "Time Settings", "時間設定" },
			{ "Ground Settings", "地面の描写" },
			{ "Wave Settings", "波形の詳細設定" },
			{ "Common Shore", "全体" },
			{ "Nearshore", "砕波帯" },
			{ "Offshore", "沖合" },
			{ "Swell Zone", "波打ち際" },
			{ "Tessellation", "テッセレーション" },
			{ "Debug", "デバッグ用" }
		};

		private static readonly Dictionary<string, string> EnglishText = new Dictionary<string, string>
		{
			{ "Hint_Model", "The current settings may not be displayed correctly with the default mesh.\nIf you want to broaden the range where waves are displayed, please take measures such as changing the mesh to ExtendedPlane." },
			{ "Hint_Beach", "If the beach is associated with the udon program, most properties will be overwritten at runtime to match the seashore material and common settings." },
			{ "_WaterDistortionScale", "Water Distortion Scale" },
			{ "_InvIOR", "IOR" },
			{ "_TimeScale", "Time Scale" },
			{ "_ManualTimeOffset", "Manual Time Offset" },
			{ "_SideScrollSpeed", "Side Scroll Speed" },
			{ "_SideScrollOffset", "Side Scroll Offset" },
			{ "_TimeDistortionPower", "Time Distortion Power" },
			{ "_TimeDistortionWidth", "Time Distortion Width" },
			{ "_TimeDistortionNoiseLevel", "Time Distortion Noise Level" },
			{ "_BeachSlope", "Beach Slope" },
			{ "_WaterGradient", "Water Gradient" },
			{ "_AttenuationScale", "Attenuation Scale" },
			{ "_Interval", "Interval" },
			{ "_NoiseOctaves", "Wave Noise Octaves" },
			{ "_SeaLevel", "Sea Level" },
			{ "_Normal", "Water Surface" },
			{ "_NormalAmount", "Normal Amount" },
			{ "_ReflectionIntensity", "Reflection Intensity" },
			{ "_ApplySunSpecular", "Apply Sun Specular" },
			{ "_SunBrightnessMultiplier", "Sun Brightness Multiplier" },
			{ "_SourceAngleCos", "Source Angle Cos" },
			{ "_SpecularSoftEdge", "Specular Soft Edge" },
			{ "_FormMask", "Form Mask" },
			{ "_BackFormLength", "Back Form Length" },
			{ "_WavePower", "Wave Power" },
			{ "_WaveWidth", "Wave Width" },
			{ "_Steepness", "Wave Steepness" },
			{ "_WaveBreakThreshold", "Wave Break Threshold" },
			{ "_NumberOfWaves", "Number of Waves" },
			{ "_FarWaveHeight", "Far Wave Power" },
			{ "_FarWaveDecayDistance", "Far Wave Decay Distance" },
			{ "_SwellZoneBuffer", "Swell Zone Buffer" },
			{ "_FrontWaveNoiseLevel", "Front Wave Noise Level" },
			{ "_FrontWaveWidth", "Front Wave Width" },
			{ "_BackwashVelocity", "Backwash Velocity" },
			{ "_MaxTessellationFactor", "Max Tessellation Factor" },
			{ "_TessellationFalloffThreshold", "Tessellation Falloff Threshold" },
			{ "_TessellationFalloffExponent", "Tessellation Falloff Exponent" },
			{ "_GroundTex", "Ground Texture" },
			{ "_WetGroundTint", "Wet Ground Tint" },
			{ "_FormShadow", "Form Shadow" },
			{ "_DebugTriangle", "Debug Triangle" },
			{ "_DebugWaveHeightVisualize", "Wave Height Visualize" },
			{ "_DebugPreviewMask", "Preview Mask" },
			{ "_DebugDividerLine", "Border Line" }

			//Add
			,{ "_Center", "Center" }
			,{ "_FogColor", "FogColor" }

		};

		private static readonly Dictionary<string, string> JapaneseText = new Dictionary<string, string>
		{
			{ "Hint_Model", "現在の設定は、デフォルトで使用しているメッシュでは正常に表示されない可能性があります\n波が表示される範囲を広くしたい場合は、メッシュをExtendedPlaneに変更する等の対策を行ってください" },
			{ "Hint_Beach", "砂浜がSeashoreプログラムに関連付けられている場合は\n波のマテリアルと共通の設定になるよう実行時にほとんどのプロパティが上書きされます" },
			{ "_WaterDistortionScale", "屈折による歪み" },
			{ "_InvIOR", "屈折率 (IOR)" },
			{ "_TimeScale", "再生速度" },
			{ "_ManualTimeOffset", "経過時間オフセット" },
			{ "_SideScrollSpeed", "横スクロール速度" },
			{ "_SideScrollOffset", "横位置オフセット" },
			{ "_TimeDistortionPower", "到達時間のゆらぎの最大幅" },
			{ "_TimeDistortionWidth", "到達時間のゆらぎの密度" },
			{ "_TimeDistortionNoiseLevel", "到達時間のゆらぎのノイズのレイヤー数" },
			{ "_BeachSlope", "地面の傾斜" },
			{ "_WaterGradient", "色減衰" },
			{ "_AttenuationScale", "色減衰の強さ" },
			{ "_Interval", "波の間隔" },
			{ "_NoiseOctaves", "ノイズのレイヤー数" },
			{ "_SeaLevel", "海面の高さ補正" },
			{ "_Normal", "ノーマルマップ" },
			{ "_NormalAmount", "ノーマルマップの強さ" },
			{ "_ReflectionIntensity", "反射の強さ" },
			{ "_ApplySunSpecular", "ディレクショナルライトを太陽光の反射として描画" },
			{ "_SunBrightnessMultiplier", "太陽光の反射の強さ" },
			{ "_SourceAngleCos", "太陽光のサイズ" },
			{ "_SpecularSoftEdge", "ソフトエッジ" },
			{ "_FormMask", "泡のマスク" },
			{ "_BackFormLength", "泡の残存力" },
			{ "_WavePower", "波の高さ" },
			{ "_WaveWidth", "波の横幅" },
			{ "_Steepness", "波の尖り具合" },
			{ "_WaveBreakThreshold", "砕波する高さ" },
			{ "_NumberOfWaves", "白波の数" },
			{ "_FarWaveHeight", "遠方の波の高さ" },
			{ "_FarWaveDecayDistance", "遠方の表示距離" },
			{ "_SwellZoneBuffer", "重複数の限度" },
			{ "_FrontWaveNoiseLevel", "変形後形状ノイズのレイヤー数" },
			{ "_FrontWaveWidth", "変形後形状の横幅" },
			{ "_BackwashVelocity", "寄せ波の速度減衰" },
			{ "_MaxTessellationFactor", "ポリゴン分割の細かさ" },
			{ "_TessellationFalloffThreshold", "ポリゴン削減開始距離" },
			{ "_TessellationFalloffExponent", "ポリゴン削減減衰力" },
			{ "_GroundTex", "地面テクスチャ" },
			{ "_WetGroundTint", "ぬれた地面の色合い" },
			{ "_FormShadow", "泡の影を描画する" },
			{ "_DebugTriangle", "三角波で表示" },
			{ "_DebugWaveHeightVisualize", "形状変化の流れ" },
			{ "_DebugPreviewMask", "表示用マスクの計算" },
			{ "_DebugDividerLine", "処理範囲ボーダーライン" }
			
			//Add
			,{ "_Center", "Center" }
            ,{ "_FogColor", "FogColor" }
        };

		Dictionary<string, string> lang;

		MaterialEditor me;
		bool showAdvancedSettings = false;
		int slopeTypeIndex = -1;

		private float breakerOffset(float num) => (_NumberOfWaves.intValue - num) * _Interval.floatValue;
		private string LABEL(string label)
		{
			if(Labels.ContainsKey(label) && lang == JapaneseText)
			{
				return Labels[label];
			}
			else
			{
				return label;
			}
		}
		
		private void AddHeader(string label)
		{
			EditorGUILayout.Space();
			EditorGUILayout.LabelField(LABEL(label), EditorStyles.boldLabel);
		}

		private void Add(MaterialProperty prop)
		{
			if(prop == null) { return; }

			me.ShaderProperty(prop, lang[prop.name]);
		}
		
		private void AddMiniTexture(MaterialProperty prop)
		{
			if(prop == null) { return; }

			GUIContent gui = new GUIContent(lang[prop.name]);
			me.TexturePropertySingleLine(gui, prop);
		}
		
		private void AddConvertedProperty(MaterialProperty prop, Func<float, float> GUI, Func<float, float> IN, Func<float, float> OUT)
		{
			if(prop == null) { return; }

			float value = IN(prop.floatValue);
			EditorGUI.BeginChangeCheck();
			value = GUI(value);
			if (EditorGUI.EndChangeCheck())
			{
				prop.floatValue = OUT(value);
			}
		}

		private void AddIntSlider(MaterialProperty prop, int min, int max)
		{
			if(prop == null) { return; }

			GUIContent gui = new GUIContent(lang[prop.name]);
			prop.intValue = EditorGUILayout.IntSlider(gui, prop.intValue, min, max);
		}

		private string GetEditorLanguage()
		{
			try
			{
				SystemLanguage lang = (SystemLanguage)Type.GetType("UnityEditor.LocalizationDatabase, UnityEditor").GetProperty("currentEditorLanguage", BindingFlags.Static | BindingFlags.Public).GetValue(null);
				return lang.ToString();
			}
			catch
			{
				return "English";
			}
		}

		public override void OnGUI(MaterialEditor me, MaterialProperty[] props)
		{
			this.me = me;

			Material material = (Material)me.target;
			bool isBeach = material.shader.name.Contains("Beach");

			// メンバ変数と同じ名前のプロパティを探して取得
			foreach(FieldInfo member in GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
			{
				if(member.FieldType != typeof(MaterialProperty)) { continue; }
				member.SetValue(this, FindProperty(member.Name, props, false));	// プロパティが存在しなければnull
			}

			lang = GetEditorLanguage() == "Japanese" ? JapaneseText : EnglishText;
			
			if(isBeach)
			{
				EditorGUILayout.HelpBox(lang["Hint_Beach"], MessageType.Info);
			}

			AddHeader("General Settings");

			Add(_Center);
			Add(_FogColor);


			AddMiniTexture(_GroundTex);
			if(_GroundTex != null)
			{
				me.TextureScaleOffsetProperty(_GroundTex);
			}
			AddMiniTexture(_WaterGradient);
			AddMiniTexture(_FormMask);
			AddMiniTexture(_Normal);
			Add(_WetGroundTint);
			Add(_NormalAmount);
			Add(_AttenuationScale);
			Add(_WaterDistortionScale);
			AddConvertedProperty(_InvIOR, v => EditorGUILayout.FloatField(lang["_InvIOR"], v), v => 1f / v, v => 1f / v);
			Add(_ReflectionIntensity);
			Add(_FormShadow);
			Add(_BackFormLength);

			Add(_ApplySunSpecular);
			if(_ApplySunSpecular?.floatValue == 1f)
			{
				using (new EditorGUI.IndentLevelScope())
				{
					AddConvertedProperty(_SourceAngleCos,
						v => EditorGUILayout.Slider(lang["_SourceAngleCos"], v, 0f, 90f),
						v => Mathf.Acos(v) * Mathf.Rad2Deg * 2f,
						v => Mathf.Cos(v * Mathf.Deg2Rad * 0.5f)
					);
					AddConvertedProperty(_SpecularSoftEdge,
						v => EditorGUILayout.Slider(lang["_SpecularSoftEdge"], v, 0f, 90f),
						v => Mathf.Acos(1f - v) * Mathf.Rad2Deg * 2f,
						v => 1f - Mathf.Cos(v * Mathf.Deg2Rad * 0.5f)
					);
					Add(_SunBrightnessMultiplier);
				}
			}
			
			AddHeader("Time Settings");
			
			Add(_TimeScale);
			Add(_SideScrollSpeed);
			EditorGUILayout.Space();
			Add(_ManualTimeOffset);
			Add(_SideScrollOffset);
			EditorGUILayout.Space();

			showAdvancedSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showAdvancedSettings, LABEL("Wave Settings"));
			if(showAdvancedSettings)
			{
				using (new EditorGUI.IndentLevelScope())
				{
					AddHeader("Common Shore");
					
					Add(_Interval);
					Add(_SeaLevel);
					EditorGUILayout.Space();
					Add(_TimeDistortionPower);
					Add(_TimeDistortionWidth);
					AddIntSlider(_TimeDistortionNoiseLevel, 1, 8);
					
					AddHeader("Nearshore");
					
					AddIntSlider(_NumberOfWaves, 1, 20);
					Add(_WavePower);
					Add(_WaveWidth);
					Add(_Steepness);
					AddIntSlider(_NoiseOctaves, 1, 8);
					Add(_WaveBreakThreshold);
					EditorGUILayout.Space();
					Add(_FarWaveHeight);
					Add(_FarWaveDecayDistance);

					AddHeader("Swell Zone");
					
					AddIntSlider(_FrontWaveNoiseLevel, 1, 8);
					Add(_FrontWaveWidth);
					EditorGUILayout.Space();
					Add(_BackwashVelocity);
					AddIntSlider(_SwellZoneBuffer, 1, 20);
					EditorGUILayout.Space();
					if(_BeachSlope != null)
					{
						if(slopeTypeIndex == -1)
						{
							slopeTypeIndex = _BeachSlope.floatValue switch
							{
								0.01f => 0, 0f => 1, _ => 2
							};
						}
						string[] slopeType = {"デフォルトメッシュに沿う（0.01）", "水平面（0）", "カスタム"};
						EditorGUI.BeginChangeCheck();
						slopeTypeIndex = EditorGUILayout.Popup(lang["_BeachSlope"], slopeTypeIndex, slopeType);
						switch(slopeTypeIndex)
						{
						case 0: _BeachSlope.floatValue = 0.01f; break;
						case 1: _BeachSlope.floatValue = 0f; break;
						default:
							me.ShaderProperty(_BeachSlope, L10n.Tr("Tangent"));
							break;
						}
					}
					
					if(_MaxTessellationFactor != null)
					{
						AddHeader("Tessellation");
					}
				
					Add(_MaxTessellationFactor);
					Add(_TessellationFalloffThreshold);
					Add(_TessellationFalloffExponent);
					
					AddHeader("Debug");
				
					Add(_DebugTriangle);
					Add(_DebugWaveHeightVisualize);
					Add(_DebugPreviewMask);
					Add(_DebugDividerLine);
				}
			}
			EditorGUILayout.EndFoldoutHeaderGroup();
			
			if(isBeach == false)
			{
				float farWaveBorder = breakerOffset(1) + _FarWaveDecayDistance.floatValue;
				float shorelineBorder = -9f / (16f * _BackwashVelocity.floatValue) - _Interval.floatValue * 0.75f;

				if(shorelineBorder < -54.5f || farWaveBorder > 165.5f)
				{
					EditorGUILayout.HelpBox(lang["Hint_Model"], MessageType.Info);
				}
			}
			
			AddHeader("Advanced Options");
			me.RenderQueueField();
			me.EnableInstancingField();
			me.DoubleSidedGIField();
		}
	}
}