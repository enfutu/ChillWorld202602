Shader "nmSeashore/SeashoreTransparentNoTessNoGrab"
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
		
		_BeachSlope ("Beach Slope", Float) = 0.01
		
        [NoScaleOffset] _WaterGradient ("Water Gradient", 2D) = "white" {}
		_AttenuationScale ("Attenuation Scale", Float) = 0.01
		_Interval ("Interval", Range(0.01, 100)) = 20
		_NoiseOctaves ("Wave Noise Octaves", Integer) = 3
		_SeaLevel ("Sea Level", Float) = 0.2
        [NoScaleOffset] [Normal] _Normal ("Water Surface", 2D) = "bump" {}
		_NormalAmount ("Normal Amount", Range(0, 1)) = 1
		_ReflectionIntensity ("Reflection Intensity", Range(0,5)) = 1
		[Toggle(APPLY_SUN_SPECULAR)] _ApplySunSpecular ("Apply Sun Specular", Float) = 1
		_SunBrightnessMultiplier ("Sun Brightness Multiplier", Float) = 10
		_SourceAngleCos ("Source Angle Cos", Float) = 0.999989072841314473	// 太陽の視直径0.5357°より、cos(radians(0.5357/2))
		_SpecularSoftEdge ("Specular Soft Edge", Float) = 0.0005
        [NoScaleOffset] _FormMask ("Form Mask", 2D) = "white" {}
		[PowerSlider(2.0)] _BackFormLength ("Back Form Length", Range(0, 500)) = 50
		
		[PowerSlider(2.0)] _WavePower ("Wave Power", Range(0, 50)) = 3.52
		[PowerSlider(3.0)] _WaveWidth ("Wave Width", Range(0.1, 1000)) = 16
		_WaveBreakThreshold ("Wave Break Threshold", Range(0, 1)) = 0.2
		[PowerSlider(3.0)] _Steepness ("Wave Steepness", Range(0, 5000)) = 1000
		_NumberOfWaves ("Number of Waves", Integer) = 0
		
		[PowerSlider(2.0)] _FarWaveHeight ("Far Wave Power", Range(0, 50)) = 1.52
		[PowerSlider(2.0)] _FarWaveDecayDistance ("Far Wave Decay Distance", Range(1, 1000)) = 65.5
		
		_SwellZoneBuffer ("Swell Zone Buffer", Integer) = 4
		_FrontWaveNoiseLevel ("Front Wave Noise Level", Integer) = 2
		[PowerSlider(3.0)] _FrontWaveWidth ("Front Wave Width", Range(0.1, 1000)) = 64
		_BackwashVelocity ("Backwash Velocity", Range(0, 0.03)) = 0.01425
		
		[Toggle(DEBUG_TRIANGLE)] _DebugTriangle ("Debug Triangle", Float) = 0
		[Toggle(DEBUG_WAVE_VISUALIZE)] _DebugWaveHeightVisualize ("Wave Height Visualize", Float) = 0
		[Toggle(DEBUG_PREVIEW_MASK)] _DebugPreviewMask ("Preview Mask", Float) = 0
		[Toggle(DEBUG_BORDER_LINE)] _DebugDividerLine ("Border Line", Float) = 0
    }
	CustomEditor "nmSeashore.SeashoreShaderGUI"
	
    SubShader
    {
        Tags {
			"RenderType"="Transparent"
			"Queue"="Transparent+100"
			"DisableBatching" = "True"
			"LightMode"="ForwardBase"
		}
		
		CGINCLUDE
			#pragma vertex vert
			#pragma fragment frag
            #pragma multi_compile_fog
			#pragma multi_compile_fwdbase nolightmap nodirlightmap nodynlightmap novertexlight
			
			#pragma target 3.0
			#include "UnityCG.cginc"
			#include "MathUtils.cginc"
			#include "SeashoreCommonModel.cginc"
			
			#pragma shader_feature_local __ DEBUG_TRIANGLE
			#pragma shader_feature_local __ DEBUG_WAVE_VISUALIZE
			#pragma shader_feature_local __ DEBUG_PREVIEW_MASK
			
			struct appdata
			{
				float4 vertex : POSITION;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};
		ENDCG
		
        Pass
        {
			Offset -1, 0
			Blend DstColor Zero
			
			CGPROGRAM
			
			UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
			
			struct v2f
			{
				float4 pos : SV_Position;
				float3 localPos : TEXCOORD0;
				float4 screen : TEXCOORD1;
				
				UNITY_VERTEX_OUTPUT_STEREO
			};
			
			v2f vert(appdata v)
			{
                v2f o;
				
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_INITIALIZE_OUTPUT(v2f, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
				
				float3 pos = v.vertex.xyz;
				float2 scroll = float2(0, scrollTime());
				
				// 頂点の移動
				float h = vertexHeight(pos.xz + scroll);
				pos.y = h;
				
				o.localPos = pos.xyz;
				o.pos = UnityObjectToClipPos(float4(pos.xyz, 1.0));
				
				// 深度取得用のクリップ座標はz未使用、ビュー空間の奥行き計測用の座標はzのみ使用する
				// 両方同じfloat4に入れてTEXCOORDを節約
				o.screen = ComputeScreenPos(o.pos);
				COMPUTE_EYEDEPTH(o.screen.z);
				
				return o;
			}
			
			float4 frag(v2f i) : SV_Target
			{
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
				
				float4 col = 0;
				float2 scroll = float2(0, scrollTime());
				float2 pos = i.localPos.xz + scroll;
				
				float4 shore = shoreline(pos);
				float beachMask = step(1, shore.w);	// 水に浸かっている範囲
				
				if(beachMask == 0) { discard; }
				
				// 深度に応じた色減衰
				float waterDepth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, i.screen)) - i.screen.z;	// 線形補間部の深度を正確に出せないためリサンプリングを行う
				col = tex2D(_WaterGradient, float2(waterDepth * _AttenuationScale, 0));
				
                return col;
			}
	        ENDCG
		}
		
        Pass
        {
			Cull Off
			Offset -2, 0
			Blend SrcAlpha OneMinusSrcAlpha
			
			CGPROGRAM
			
			#include "AutoLight.cginc"
			#include "Lighting.cginc"
		
			#pragma shader_feature_local __ APPLY_SUN_SPECULAR
			
			#pragma shader_feature_local __ DEBUG_BORDER_LINE
			
			struct v2f
			{
				float4 pos : SV_Position;
				float3 localPos : TEXCOORD0;
                half3 tspace0 : TEXCOORD1; // tangent.x, bitangent.x, normal.x
                half3 tspace1 : TEXCOORD2; // tangent.y, bitangent.y, normal.y
                half3 tspace2 : TEXCOORD3; // tangent.z, bitangent.z, normal.z
                UNITY_FOG_COORDS(4)
                SHADOW_COORDS(5)
                fixed3 diff : COLOR0;
                fixed3 ambient : COLOR1;
				
				UNITY_VERTEX_OUTPUT_STEREO
			};
			
			v2f vert(appdata v)
			{
                v2f o;
				
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_INITIALIZE_OUTPUT(v2f, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
				
				float3 pos = v.vertex.xyz;
				float2 scroll = float2(0, scrollTime());
				
				// 頂点の移動
				float h = vertexHeight(pos.xz + scroll);
				pos.y = h;
				
				o.localPos = pos.xyz;
				o.pos = UnityObjectToClipPos(float4(pos.xyz, 1.0));
				
				// 高さ変化の数値微分から変形後の法線を作成
				float delta = 0.64;
				
				float hdx = vertexHeight(float2(pos.x + delta, pos.z) + scroll) - h;
				float hdy = vertexHeight(float2(pos.x, pos.z + delta) + scroll) - h;
				float3 pdx = float3(pos.x + delta, pos.y + hdx, pos.z);
				float3 pdy = float3(pos.x, pos.y + hdy, pos.z + delta);
				
				// ノーマルマップを接空間に貼るためのTBN行列
                half3 worldTangent = UnityObjectToWorldDir(pos - pdx);
                half3 worldBinormal = UnityObjectToWorldDir(pdy - pos);
                half3 worldNormal = cross(worldTangent, worldBinormal);
				
                o.tspace0 = half3(worldTangent.x, worldBinormal.x, worldNormal.x);
                o.tspace1 = half3(worldTangent.y, worldBinormal.y, worldNormal.y);
                o.tspace2 = half3(worldTangent.z, worldBinormal.z, worldNormal.z);
				
				// ディレクショナルライトとフォグ
				half nl = max(0, dot(worldNormal, _WorldSpaceLightPos0.xyz));
                o.diff = nl * _LightColor0.rgb;
                o.ambient = ShadeSH9(half4(worldNormal,1));
                TRANSFER_SHADOW(o)
				UNITY_TRANSFER_FOG(o, o.pos);
				
				return o;
			}
			
			float4 frag(v2f i, fixed facing : VFACE) : SV_Target
			{
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
				
				float4 col = 0;
				float2 scroll = float2(0, scrollTime());
				
				float3 cameraPos = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos, 1));
				float3 viewDir = normalize(i.localPos.xyz - cameraPos);
				float2 pos = i.localPos.xz + scroll;
				
				float2 form = formMask(pos);
				float4 shore = shoreline(pos);
				shore.xy = step(pos.x, 0) * shore.xy + step(0, pos.x) * form;
				float beachMask = step(1, shore.w);	// 水に浸かっている範囲
				
				if(beachMask == 0) { discard; }
				
				float3 normal = waterSurfaceNormal(pos, beachMask);
                float3 worldNormal = normalize(mul(float3x3(i.tspace0, i.tspace1, i.tspace2), normal));
				
                float shadow = SHADOW_ATTENUATION(i);
                float3 lighting = i.diff * shadow + i.ambient;
				
				// 表面の泡
				#if DEBUG_PREVIEW_MASK
				col = shore;
				#else
				float frontFormTex = shore.y * 2;
				float backFormTex = shore.x;
				float formMask = frontFormTex + lerp(backFormTex, 0, saturate(frontFormTex));
				float formTex = tex2D(_FormMask, pos * 0.03333333 + normal.xy * 0.01).x;
				float formBorderRamp = max(fwidth(formTex), 0.05);
				float formAmount = smoothstep(formMask + formBorderRamp * saturate(1 - POW2(formMask * 2 - 1)), formMask - formBorderRamp, formTex);
				col.rgb += formAmount * lighting;
				
				// 表面から見た反射面を下面からは直接見ているので裏面の処理は不要
				if(facing == 1)
				{
					// フレネル反射
					float3 worldViewDir = UnityObjectToWorldDir(viewDir);
					float fresnel = 1 - dot(worldNormal, -worldViewDir);
					fresnel = saturate(fresnel * fresnel * fresnel * fresnel * fresnel * 0.98 + 0.02);
					
					float smoothness = lerp((smoothstep(1.0, 0.9, fresnel) * 0.5 + 0.5) * shore.w, 0.5, formAmount);
					float mipLevel = (1 - smoothness) * UNITY_SPECCUBE_LOD_STEPS;
					float3 reflDir = reflect(worldViewDir, normalize(worldNormal));
					
					half3 reflColor = DecodeHDR(UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0, reflDir, mipLevel), unity_SpecCube0_HDR);
					col.rgb = lerp(reflColor, col, formAmount);
					col.a = lerp(_ReflectionIntensity * fresnel, 1, formAmount) * shore.z;
					
					#if APPLY_SUN_SPECULAR
					// 太陽の映り込み
					float highlight = smoothstep(_SourceAngleCos - _SpecularSoftEdge * (mipLevel + 1), _SourceAngleCos, dot(reflDir, _WorldSpaceLightPos0));	
					col.rgb += (_LightColor0 * highlight * _SunBrightnessMultiplier * beachMask);
					#endif
				}
				
                UNITY_APPLY_FOG(i.fogCoord, col);
				#endif
				
				#if DEBUG_BORDER_LINE
				col += showBorderLine(pos.x);
				#endif
				
                return col;
			}
	        ENDCG
		}
    }
    FallBack "VertexLit"
}
