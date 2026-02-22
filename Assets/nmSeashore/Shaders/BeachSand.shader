Shader "nmSeashore/BeachSand"
{
	Properties
	{
        _TimeScale ("Time Scale", Float) = 0.175
		_ManualTimeOffset ("Manual Time Offset", Float) = 0
        _SideScrollSpeed ("Side Scroll Speed", Float) = 0
        _SideScrollOffset ("Side Scroll Offset", Float) = 0
		_TimeDistortionPower ("Time Distortion Power", Float) = 1.5
		_TimeDistortionWidth ("Time Distortion Width", Float) = 0.05
		_TimeDistortionNoiseLevel ("Time Distortion Noise Level", Integer) = 2
		
        _GroundTex ("Ground Texture", 2D) = "white" {}
		_WetGroundTint ("Wet Ground Tint", Color) = (0, 0, 0, 0.3)
		[Toggle(FORM_SHADOW)] _FormShadow ("Form Shadow", Float) = 1
		
		_Interval ("Interval", Range(0.01, 100)) = 20
		_NoiseOctaves ("Wave Noise Octaves", Integer) = 3
        [NoScaleOffset] [Normal] _Normal ("Water Surface", 2D) = "bump" {}
        [NoScaleOffset] _FormMask ("Form Mask", 2D) = "white" {}
		[PowerSlider(2.0)] _BackFormLength ("Back Form Length", Range(0, 500)) = 50
		
		[PowerSlider(2.0)] _WavePower ("Wave Power", Range(0, 50)) = 3.52
		[PowerSlider(3.0)] _WaveWidth ("Wave Width", Range(0.1, 1000)) = 16
		_WaveBreakThreshold ("Wave Break Threshold", Range(0, 1)) = 0.2
		_NumberOfWaves ("Number of Waves", Integer) = 0
		
		_SwellZoneBuffer ("Swell Zone Buffer", Integer) = 4
		_FrontWaveNoiseLevel ("Front Wave Noise Level", Integer) = 2
		[PowerSlider(3.0)] _FrontWaveWidth ("Front Wave Width", Range(0.1, 1000)) = 64
		_BackwashVelocity ("Backwash Velocity", Range(0, 0.03)) = 0.0
		
		[Toggle(DEBUG_TRIANGLE)] _DebugTriangle ("Debug Triangle", Float) = 0
		[Toggle(DEBUG_PREVIEW_MASK)] _DebugPreviewMask ("Preview Mask", Float) = 0
		[Toggle(DEBUG_BORDER_LINE)] _DebugDividerLine ("Border Line", Float) = 0
	}
	CustomEditor "nmSeashore.SeashoreShaderGUI"
	
	SubShader
	{
		Tags {
			"RenderType"="Opaque"
			"DisableBatching" = "True"
		}
		LOD 200
		
		CGPROGRAM
		#pragma surface surf Standard fullforwardshadows
		#pragma target 3.0
		
		#pragma shader_feature_local __ FORM_SHADOW
		#pragma shader_feature_local __ APPLY_SUN_SPECULAR
		
		#pragma shader_feature_local __ DEBUG_TRIANGLE
		#pragma shader_feature_local __ DEBUG_PREVIEW_MASK
		#pragma shader_feature_local __ DEBUG_BORDER_LINE
		
		#include "UnityCG.cginc"
		#include "MathUtils.cginc"
		#include "SeashoreCommonModel.cginc"
		
		sampler2D _GroundTex;
		float4 _WetGroundTint;
		
		struct Input
		{
			float2 uv_GroundTex;
			float3 worldPos;
		};
		
		void surf (Input i, inout SurfaceOutputStandard o)
		{
			float4 col = 0;
			
			float2 pos = mul(unity_WorldToObject, float4(i.worldPos, 1)).xz;
			pos.y += scrollTime();
			
			float4 shore = shoreline(pos);
			float beachMask = step(1, shore.w) * shore.z;
			float4 ground = tex2D(_GroundTex, i.uv_GroundTex);
			col = float4(ground * (1 - shore.w * (1 - _WetGroundTint.rgb) * _WetGroundTint.a), 1);
			
			// 表面の泡
			#if DEBUG_PREVIEW_MASK
			col = shore;
			#else
			float frontFormTex = shore.y * 2;
			float backFormTex = shore.x;
			float formMask = frontFormTex + lerp(backFormTex, 0, saturate(frontFormTex));
			float formTex = tex2D(_FormMask, pos / 30).x;
			float formBorderRamp = max(fwidth(formTex), 0.05);
			float formAmount = smoothstep(formMask + formBorderRamp * saturate(1 - POW2(formMask * 2 - 1)), formMask - formBorderRamp, formTex);
			col += formAmount * (1 - beachMask);
			#if FORM_SHADOW
			col = lerp(col, 0, saturate(smoothstep(formMask + formBorderRamp * saturate(1 - POW2(formMask * 2 - 1)), formMask - formBorderRamp, formTex) * (1 - shore.z) * beachMask * 0.5));
			#endif
			#endif
			
			#if DEBUG_BORDER_LINE
			col += showBorderLine(pos.x);
			#endif
			
			o.Albedo = lerp(ground, col, saturate(-pos.x * 0.07));
			o.Metallic = 0;
			o.Smoothness = saturate(shore.w - beachMask);
			o.Alpha = 1;
		}
		ENDCG
	}
	FallBack "Diffuse"
}