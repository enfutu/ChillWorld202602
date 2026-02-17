//--------------------------------------------
//Stylized Environment Kit
//LittleMarsh CG ART
//version 1.5.0
//--------------------------------------------

Shader "LMArtShader/VertexBlending"
{
	Properties
	{
		[Header(Red Channel)]
		[Space(7)][NoScaleOffset]_TopTex("Top Tex(Alpha used for Blending)", 2D) = "white" {}
		[Toggle(USE_TWUV)]_TopWorldUV("Use World UV", Float) = 0
		_TopUVScale("World UV Scale", Range(0,1)) = 0.5

		[NoScaleOffset]_TopBumpMap("Top Normal Map", 2D) = "bump" {}
		_TopBumpScale("Top Normal Scale", Range(0,1)) = 1.0

		[Space(24)][Header(Green Channel)]
		[Space(7)][NoScaleOffset]_MidTex("Middle Tex(Alpha used for Blending)", 2D) = "white" {}
		[Toggle(USE_MWUV)]_MidWorldUV("Use World UV", Float) = 0
		_MidUVScale("World UV Scale", Range(0,1)) = 0.5

		[NoScaleOffset]_MidBumpMap("Middle Normal Map", 2D) = "bump" {}
		_MidBumpScale("Middle Normal Scale", Range(0,1)) = 1.0

		[Space(24)][Header(Blue Channel)]
		[Space(7)][NoScaleOffset]_BtmTex("Bottom Tex(Alpha used for Blending)", 2D) = "white" {}
		[Toggle(USE_BWUV)]_BtmWorldUV("Use World UV", Float) = 0
		_BtmUVScale("World UV Scale", Range(0,1)) = 0.5

		[NoScaleOffset]_BtmBumpMap("Bottom Normal Map", 2D) = "bump" {}
		_BtmBumpScale("Buttom Normal Scale", Range(0,1)) = 1.0

		[Space(24)][Header(Others)]
		[Space(7)]_BlendFactor("BlendFactor", Range(0, 0.5)) = 0.2
		[Toggle(USE_RF)]_UseReflection("Use Reflection", Float) = 0
		_ReflectionColor("Reflection Light Color", Color) = (0.5,0.5,0.5,1)

	}

		SubShader
		{

			Pass
			{
				Tags {
				"LightMode" = "ForwardBase"
				"Queue" = "Geometry"
				"RenderType" = "Opaque"
				}

				LOD 200

				CGPROGRAM
				#pragma vertex vert
				#pragma fragment frag
				#pragma multi_compile_fwdbase_fullshadows
				#pragma multi_compile_fog
				#pragma shader_feature _ USE_RF
				#include "LMArtShader.cginc"

				struct v2f
				{
					float2 uv : TEXCOORD0;
					SHADOW_COORDS(1)
					UNITY_FOG_COORDS(2)
					float4 pos : SV_POSITION;
					float3 tspace0 : TEXCOORD3;
					float3 tspace1 : TEXCOORD4;
					float3 tspace2 : TEXCOORD5;
					half4 lightDir : TEXCOORD6;
					float4 color : COLOR0;
					half3 ambient : COLOR1;
					float3 worldPos : TEXCOORD7;

					#ifdef LIGHTMAP_ON
						half2 uv2 : TEXCOORD8;
					#endif
					#ifdef DYNAMICLIGHTMAP_ON
						half2 uv3 : TEXCOORD9;
					#endif

					UNITY_VERTEX_OUTPUT_STEREO//VR
				};


				v2f vert(appdata_VB v)
				{
					v2f o;

					UNITY_SETUP_INSTANCE_ID(v); //VR
					UNITY_INITIALIZE_OUTPUT(v2f, o); //VR
					UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); //VR

					o.pos = UnityObjectToClipPos(v.vertex);
					o.uv = v.uv;

					half3 worldNormal = UnityObjectToWorldNormal(v.normal);
					float3x3 tspaceC = tspace(worldNormal, v.normal, v.tangent);
					o.tspace0 = tspaceC[0];
					o.tspace1 = tspaceC[1];
					o.tspace2 = tspaceC[2];

					TRANSFER_SHADOW(o)

					o.color = v.color;
					o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
					o.lightDir = half4(normalize(_WorldSpaceLightPos0.xyz), _WorldSpaceLightPos0.w);
					o.ambient = lerp(ShadeSH9(half4(worldNormal, 1)), 0, o.lightDir.w);

					UNITY_TRANSFER_FOG(o,o.pos);

					#ifdef LIGHTMAP_ON
						o.uv2 = v.texcoord1.xy * unity_LightmapST.xy + unity_LightmapST.zw;
					#endif
					#ifdef DYNAMICLIGHTMAP_ON
						o.uv3 = v.texcoord2.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
					#endif
					

					return o;
				}

				fixed4 frag(v2f i) : SV_Target
				{
					UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);//VR

					half2 worldUV = i.worldPos.xz * -1;
					half2 topUV = lerp(i.uv, worldUV * _TopUVScale, _TopWorldUV);
					half2 midUV = lerp(i.uv, worldUV * _MidUVScale, _MidWorldUV);
					half2 btmUV = lerp(i.uv, worldUV * _BtmUVScale, _BtmWorldUV);

					fixed4 topcol = tex2D(_TopTex, topUV);
					fixed4 midcol = tex2D(_MidTex, midUV);
					fixed4 btmcol = tex2D(_BtmTex, btmUV);

					half2 maskBD = maskBlending(i.color, topcol, midcol, btmcol);

					//Color Blending
					fixed4 col = btmcol;
					col = lerp(col, midcol, maskBD.x);
					col = lerp(col, topcol, maskBD.y);

					half3 topNormal = UnpackScaleNormal(tex2D(_TopBumpMap, topUV), _TopBumpScale);
					half3 midNormal = UnpackScaleNormal(tex2D(_MidBumpMap, midUV), _MidBumpScale);
					half3 btmNormal = UnpackScaleNormal(tex2D(_BtmBumpMap, btmUV), _BtmBumpScale);

					//Normal Blending
					half3 tNormal = btmNormal;
					tNormal = lerp(tNormal, midNormal, maskBD.x);
					tNormal = lerp(tNormal, topNormal, maskBD.y);

					half3 wNormal = worldNormal(i.tspace0, i.tspace1, i.tspace2, tNormal);

					fixed shadow = SHADOW_ATTENUATION(i);
					fixed atten = 0;
					fixed lightCal = lerp(shadow, atten, i.lightDir.w);

					half3 reflectLight = fixed3(0,0,0);
					#ifdef USE_RF
						half3 nll = max(0, dot(wNormal, -i.lightDir.xyz));
						reflectLight = _ReflectionColor * nll;
					#endif

					half3 lightmap = half3(0, 0, 0);
					half lightmask = 1.0;

					#ifdef LIGHTMAP_ON
						lightmap = computeLightmap(lightmap, i.uv2, wNormal);
					#endif
					#ifdef DYNAMICLIGHTMAP_ON
						lightmap = computeDynamicLightmap(lightmap, i.uv3, wNormal);
					#endif

					//DistanceShadowmask and Shadowmask
					#if defined (LIGHTMAP_ON) && defined (SHADOWS_SHADOWMASK)
						lightmask = computeShadowmask(lightmask, i.uv2, i.worldPos, shadow);
						col = computeVBCol(col, lightmap, wNormal, i.lightDir.xyz, lightmask * lightCal, reflectLight);
					//Subtractive
					#elif defined (LIGHTMAP_ON) && defined(LIGHTMAP_SHADOW_MIXING)
						col.rgb *= lightmap;
					//Baked Indirect
					#elif defined (LIGHTMAP_ON)
						col = computeVBCol(col, lightmap, wNormal, i.lightDir.xyz, lightmask * lightCal, reflectLight);
					#else
						col = computeVBCol(col, i.ambient, wNormal, i.lightDir.xyz, lightCal, reflectLight);
					#endif

					UNITY_APPLY_FOG(i.fogCoord, col);
					return col;
				}
				ENDCG
			}


			Pass
			{
				Tags {
				"LightMode" = "ForwardAdd"
				"Queue" = "Geometry"
				"RenderType" = "Opaque"
				}

				LOD 200
				Blend One One

				CGPROGRAM
				#pragma vertex vert
				#pragma fragment frag
				#pragma multi_compile_fwdadd_fullshadows
				#pragma multi_compile_fog
				#include "LMArtShader.cginc"

				struct v2f
				{
					float2 uv : TEXCOORD0;
					//SHADOW_COORDS(1)
					UNITY_FOG_COORDS(2)
					float4 pos : SV_POSITION;
					float3 tspace0 : TEXCOORD3; // tangent.x, bitangent.x, normal.x
					float3 tspace1 : TEXCOORD4; // tangent.y, bitangent.y, normal.y
					float3 tspace2 : TEXCOORD5; // tangent.z, bitangent.z, normal.z
					half4 lightDir : TEXCOORD6;
					float4 color : COLOR0;
					half3 ambient : COLOR1;
					float3 worldPos : TEXCOORD7;
					LIGHTING_COORDS(8, 9)

					UNITY_VERTEX_OUTPUT_STEREO//VR
				};


				v2f vert(appdata_VB v)
				{
					v2f o;

					UNITY_SETUP_INSTANCE_ID(v); //VR
					UNITY_INITIALIZE_OUTPUT(v2f, o); //VR
					UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); //VR

					o.pos = UnityObjectToClipPos(v.vertex);
					o.uv = v.uv;

					half3 worldNormal = UnityObjectToWorldNormal(v.normal);
					float3x3 tspaceC = tspace(worldNormal, v.normal, v.tangent);
					o.tspace0 = tspaceC[0];
					o.tspace1 = tspaceC[1];
					o.tspace2 = tspaceC[2];

					o.color = v.color;
					o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
					o.lightDir = half4(normalize(lerp(_WorldSpaceLightPos0.xyz, (_WorldSpaceLightPos0.xyz - o.worldPos), _WorldSpaceLightPos0.w)), _WorldSpaceLightPos0.w);
					o.ambient = lerp(ShadeSH9(half4(worldNormal, 1)), 0, o.lightDir.w);
					
					UNITY_TRANSFER_FOG(o,o.pos);
					TRANSFER_VERTEX_TO_FRAGMENT(o);

					return o;
				}

				fixed4 frag(v2f i) : SV_Target
				{
					UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);//VR

					half2 worldUV = i.worldPos.xz * -1;
					half2 topUV = lerp(i.uv, worldUV * _TopUVScale, _TopWorldUV);
					half2 midUV = lerp(i.uv, worldUV * _MidUVScale, _MidWorldUV);
					half2 btmUV = lerp(i.uv, worldUV * _BtmUVScale, _BtmWorldUV);

					/////////////////////////Base
					fixed4 topcol = tex2D(_TopTex, topUV);
					fixed4 midcol = tex2D(_MidTex, midUV);
					fixed4 btmcol = tex2D(_BtmTex, btmUV);

					half2 maskBD = maskBlending(i.color, topcol, midcol, btmcol);

					//Color Blending
					fixed4 col = btmcol;
					col = lerp(col, midcol, maskBD.x);
					col = lerp(col, topcol, maskBD.y);


					/////////////////////////Normal
					half3 topNormal = UnpackScaleNormal(tex2D(_TopBumpMap, topUV), _TopBumpScale);
					half3 midNormal = UnpackScaleNormal(tex2D(_MidBumpMap, midUV), _MidBumpScale);
					half3 btmNormal = UnpackScaleNormal(tex2D(_BtmBumpMap, btmUV), _BtmBumpScale);

					//Normal Blending
					half3 tNormal = btmNormal;
					tNormal = lerp(tNormal, midNormal, maskBD.x);
					tNormal = lerp(tNormal, topNormal, maskBD.y);

					half3 wNormal = worldNormal(i.tspace0, i.tspace1, i.tspace2, tNormal);

					fixed shadow = 1.0;
					fixed atten = LIGHT_ATTENUATION(i);
					fixed lightCal = lerp(shadow, atten, i.lightDir.w);

					half3 reflectLight = fixed3(0, 0, 0);

					col = computeVBCol(col, i.ambient, wNormal, i.lightDir.xyz, lightCal, reflectLight);

					UNITY_APPLY_FOG(i.fogCoord, col);
					return col;
				}
				ENDCG
			}


			Pass
			{
				Tags {
					"LightMode" = "ShadowCaster"
				}

				ZWrite On ZTest LEqual

				CGPROGRAM
				#pragma vertex vert
				#pragma fragment frag
				#pragma multi_compile_shadowcaster
				#include "UnityCG.cginc"

				struct appdata {
					float4 vertex : POSITION;
					float3 normal : NORMAL;

					UNITY_VERTEX_INPUT_INSTANCE_ID//VR
				};

				struct v2f
				{
					V2F_SHADOW_CASTER;

					UNITY_VERTEX_OUTPUT_STEREO//VR
				};


				v2f vert(appdata v)
				{
					v2f o;

					UNITY_SETUP_INSTANCE_ID(v); //VR
					UNITY_INITIALIZE_OUTPUT(v2f, o); //VR
					UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); //VR

					TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)

					return o;
				}

				float4 frag(v2f i) : SV_Target
				{
					//UNITY_SETUP_INSTANCE_ID(i); //VR
					//UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);//VR

					SHADOW_CASTER_FRAGMENT(i)
				}
				ENDCG
			}
		}
}