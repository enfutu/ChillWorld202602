Shader "nmSeashore/SeashoreTransparent"
{
    Properties
    {
		_WaterDistortionScale ("Water Distortion Scale", Float) = 0.2
		
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
		
		_MaxTessellationFactor ("Max Tessellation Factor", Range(1, 64)) = 30
		_TessellationFalloffThreshold ("Tessellation Falloff Threshold", Range(0, 1000)) = 100
		[PowerSlider(2.0)] _TessellationFalloffExponent ("Tessellation Falloff Exponent", Range(0, 0.001)) = 0.0001
		
		[Toggle(DEBUG_TRIANGLE)] _DebugTriangle ("Debug Triangle", Float) = 0
		[Toggle(DEBUG_WAVE_VISUALIZE)] _DebugWaveHeightVisualize ("Wave Height Visualize", Float) = 0
		[Toggle(DEBUG_PREVIEW_MASK)] _DebugPreviewMask ("Preview Mask", Float) = 0
		[Toggle(DEBUG_BORDER_LINE)] _DebugDividerLine ("Border Line", Float) = 0
    }
	CustomEditor "nmSeashore.SeashoreShaderGUI"
	
    SubShader
    {
        Tags {
			"RenderType"="Opaque"
			"Queue"="Transparent+100"
			"DisableBatching" = "True"
			"LightMode"="ForwardBase"
		}
		
		GrabPass { "_SeashoreTransparentGrabTexture" }
		
        Pass
        {
			Cull Off
			Offset -1, 0
			
			CGPROGRAM
			
			#pragma vertex vert
			#pragma fragment frag
			#pragma hull hull
			#pragma domain domain
            #pragma multi_compile_fog
			#pragma multi_compile_fwdbase nolightmap nodirlightmap nodynlightmap novertexlight
			#include "AutoLight.cginc"
			#include "Lighting.cginc"
		
			#pragma target 5.0
		
			#pragma shader_feature_local __ FORM_SHADOW
			#pragma shader_feature_local __ APPLY_SUN_SPECULAR
			
			#pragma shader_feature_local __ DEBUG_TRIANGLE
			#pragma shader_feature_local __ DEBUG_WAVE_VISUALIZE
			#pragma shader_feature_local __ DEBUG_PREVIEW_MASK
			#pragma shader_feature_local __ DEBUG_BORDER_LINE
		
			#include "UnityCG.cginc"
			#include "UnityLightingCommon.cginc"
			#include "Tessellation.cginc"
			
			#include "MathUtils.cginc"
			#include "SeashoreCommonModel.cginc"
			
			sampler2D _SeashoreTransparentGrabTexture;
			UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
			float4 _CameraDepthTexture_TexelSize;
			
			struct appdata
			{
				float4 vertex : POSITION;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};
			
			struct v2f
			{
				float4 vertex : SV_POSITION;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			struct d2f
			{
				float4 pos : SV_Position;
				float3 localPos : TEXCOORD0;
                half3 tspace0 : TEXCOORD1; // tangent.x, bitangent.x, normal.x
                half3 tspace1 : TEXCOORD2; // tangent.y, bitangent.y, normal.y
                half3 tspace2 : TEXCOORD3; // tangent.z, bitangent.z, normal.z
                UNITY_FOG_COORDS(4)
                SHADOW_COORDS(5)
				float4 grabPos : TEXCOORD6;
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
				
				o.vertex = v.vertex;
                return o;
			}
			
			[domain("tri")]
			[partitioning("fractional_even")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("hullConst")]
			[outputcontrolpoints(3)]
			v2f hull(InputPatch<v2f, 3> inputPatch, uint i : SV_OutputControlPointID)
			{
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(inputPatch[i])
				return inputPatch[i];
			}
	
			struct HS_CONSTANT_DATA_OUTPUT
			{
				float Edges[3] : SV_TessFactor;
				float Inside : SV_InsideTessFactor;
			};
			
			inline float edgeFactor(float4 p0, float4 p1)
			{
				float3 edge = UnityObjectToViewPos((p0 + p1) * 0.5);
				float minLength = max(length(edge), _TessellationFalloffThreshold) - _TessellationFalloffThreshold;
				float slope = _TessellationFalloffExponent;
				
				if(UNITY_MATRIX_P._11 > 3)
				{
					slope /= UNITY_MATRIX_P._11 - 2;
				}
				
				return _MaxTessellationFactor / (1.0 + slope * POW2(minLength));
			}

			HS_CONSTANT_DATA_OUTPUT hullConst(InputPatch<v2f, 3> p)
			{
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(p[0]);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(p[1]);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(p[2]);
				
				HS_CONSTANT_DATA_OUTPUT o;
				
				float frontThreshold = max(max(p[0].vertex.x, p[1].vertex.x), p[2].vertex.x);
				float backThreshold = min(min(p[0].vertex.x, p[1].vertex.x), p[2].vertex.x);
				
				if(step(-9 / (16 * _BackwashVelocity) - _Interval * 0.75, frontThreshold) * step(backThreshold, breakerOffset(1) + _FarWaveDecayDistance) == 0)
				{
					// 凹凸表現範囲外でのテッセレーションを省略
					return (HS_CONSTANT_DATA_OUTPUT)1;
				}
				
				float3 p0 = mul(unity_ObjectToWorld, p[0].vertex).xyz;
				float3 p1 = mul(unity_ObjectToWorld, p[1].vertex).xyz;
				float3 p2 = mul(unity_ObjectToWorld, p[2].vertex).xyz;
				
				if(UnityWorldViewFrustumCull(p0, p1, p2, 10.0f))
				{
					// 画面外カリング
					return (HS_CONSTANT_DATA_OUTPUT)0;
				}
				
				// 画面上での頂点間の距離に応じた分割
				o.Edges[0] = edgeFactor(p[1].vertex, p[2].vertex);
				o.Edges[1] = edgeFactor(p[2].vertex, p[0].vertex);
				o.Edges[2] = edgeFactor(p[0].vertex, p[1].vertex);
				o.Inside = max(max(o.Edges[0], o.Edges[1]), o.Edges[2]);
				
				return o;
			}
			
			d2f domainVert(v2f v)
			{
				d2f o;
				UNITY_INITIALIZE_OUTPUT(d2f, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
				
				float3 pos = v.vertex;
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
				
				// GrabPass用のクリップ座標はz未使用、ビュー空間の奥行き計測用の座標はzのみ使用する
				// 両方同じfloat4に入れてTEXCOORDを節約
				o.grabPos = ComputeGrabScreenPos(o.pos);
				o.grabPos.z = -UnityObjectToViewPos(pos).z;	// COMPUTE_EYEDEPTH(o.grabPos.z)と書くべきところだが、COMPUTE_EYEDEPTHは参照先の頂点がv.vertexという名前で直書きされているので、マクロを展開している
				
				return o;
			}

			[domain("tri")]
			d2f domain(HS_CONSTANT_DATA_OUTPUT hsConst, OutputPatch<v2f, 3> p, float3 bary : SV_DomainLocation)
			{
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(p[0]);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(p[1]);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(p[2]);
				
				v2f o;
				
				o.vertex = p[0].vertex * bary.x + p[1].vertex * bary.y + p[2].vertex * bary.z;
				
				UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(p[0], o)
				
				return domainVert(o);
			}
			
			float4 frag(d2f i, fixed facing : VFACE) : SV_Target
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
				
				// GrabPassによる簡易疑似屈折
				float2 straightUV = i.grabPos.xy / i.grabPos.w;
				float straightCameraDepth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, straightUV));
				float straightWaterDepth = straightCameraDepth - i.grabPos.z;
				
				float2 uvOffset = normal.xy * _WaterDistortionScale * saturate(straightWaterDepth);	// 浅水部のズレを減らす
				float2 distortionUV = saturate(i.grabPos.xy / i.grabPos.w + uvOffset);	// NDC座標にしてからずらす
				float distortionCameraDepth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, distortionUV));
				float distortionWaterDepth = distortionCameraDepth - i.grabPos.z;
				
				float2 grabUV = lerp(straightUV, distortionUV, saturate(distortionWaterDepth));	// 手前なら0になるのでズレを解消、水面近くは線形補間される
				grabUV = (floor(grabUV * _CameraDepthTexture_TexelSize.zw) + 0.5) * abs(_CameraDepthTexture_TexelSize.xy);	// テクスチャ補間の打ち消し
				col = tex2D(_SeashoreTransparentGrabTexture, grabUV);
				
				// 深度に応じた色減衰
				if(facing == 1)
				{
					float waterDepth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, grabUV)) - i.grabPos.z;	// 線形補間部の深度を正確に出せないためリサンプリングを行う
					col *= tex2D(_WaterGradient, float2(waterDepth * _AttenuationScale, 0));
				}
				
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
					col.rgb = lerp(col.rgb, reflColor, saturate(_ReflectionIntensity * fresnel * shore.z));
				
					#if APPLY_SUN_SPECULAR
					// 太陽の映り込み
					float highlight = smoothstep(_SourceAngleCos - _SpecularSoftEdge * (mipLevel + 1), _SourceAngleCos, dot(reflDir, _WorldSpaceLightPos0));	
					col += (_LightColor0 * highlight * _SunBrightnessMultiplier * beachMask);
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
