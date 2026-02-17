//--------------------------------------------
//Stylized Environment Kit
//LittleMarsh CG ART
//version 1.5.0
//--------------------------------------------

Shader "LMArtShader/SimpleArt"
{
    Properties
    {
		[NoScaleOffset]_AlbedoTex("Main Tex", 2D) = "white" {}
		[Toggle(USE_WUV)]_SAWorldUV("Use World UV", Float) = 0
		_SAUVScale("World UV Scale", Range(0,1)) = 0.5

		[Space(16)][Header(Normal)]
		[Space(7)]
		[Toggle(USE_NM)]_UseNormal("Use Normal", Float) = 1.0
		[NoScaleOffset]_SABumpMap("Normal Map", 2D) = "bump" {}
		_SABumpScale("Normal Scale", Range(0,1)) = 1.0

		[Space(16)][Header(Specular)]
		[Space(7)]
		[Toggle(USE_SP)]_UseSpecular("Use Specular", Float) = 1.0
		[HDR]_SpecularColor("Specular Color", Color) = (1, 1, 1, 1)
		[NoScaleOffset]_SpecularTex("Specular ColorTex", 2D) = "white" {}
		_Shininess("Shininess", Range(1,96)) = 12
		_SpecularPower("Specular Power", Range(0,3)) = 1.0

		[Space(16)][Header(Reflection)]
		[Space(7)]
		[Toggle(USE_RF)]_UseReflection("Use Reflection", Float) = 0
		_ReflectionColor("Reflection Light Color", Color) = (0.5,0.5,0.5,1)
		[NoScaleOffset]_ReflectionMask("Reflection Mask", 2D) = "white" {}
		_ReflectionIntensity("Reflection Intensity", Range(0,1)) = 0.3
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
			#pragma shader_feature _ USE_NM
			#pragma shader_feature _ USE_SP
			#pragma shader_feature _ USE_RF
			#pragma shader_feature _ USE_WUV
			#include "LMArtShader.cginc"

            struct v2f
            {
				float2 uv : TEXCOORD0;
				SHADOW_COORDS(1)
				UNITY_FOG_COORDS(2)
				float4 pos : SV_POSITION;
				#ifdef USE_NM
					float3 tspace0 : TEXCOORD3;
					float3 tspace1 : TEXCOORD4;
					float3 tspace2 : TEXCOORD5;
				#else
					half3 normal : TEXCOORD3;
				#endif
				half3 ambient : COLOR0;
				#ifdef USE_SP
					float3 viewDir : TEXCOORD6;
				#endif
				float3 worldPos : TEXCOORD7;

				#ifdef LIGHTMAP_ON
					half2 uv2 : TEXCOORD8;
				#endif
				#ifdef DYNAMICLIGHTMAP_ON
					half2 uv3 : TEXCOORD9;
				#endif
				half4 lightDir : TEXCOORD10;

				UNITY_VERTEX_OUTPUT_STEREO//VR
            };

			v2f vert(appdata_SA v)
            {
				v2f o;

				UNITY_SETUP_INSTANCE_ID(v); //VR
				UNITY_INITIALIZE_OUTPUT(v2f, o); //VR
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); //VR

				o.pos = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;

				half3 worldNormal = UnityObjectToWorldNormal(v.normal);
				#ifdef USE_NM
					float3x3 tspaceC = tspace(worldNormal, v.normal, v.tangent);
					o.tspace0 = tspaceC[0];
					o.tspace1 = tspaceC[1];
					o.tspace2 = tspaceC[2];
				#else
					o.normal = worldNormal;
				#endif

				#ifdef USE_SP
					o.viewDir = normalize(WorldSpaceViewDir(v.vertex));
				#endif
				o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
				o.lightDir = half4(normalize(_WorldSpaceLightPos0.xyz), _WorldSpaceLightPos0.w);
				o.ambient = lerp(ShadeSH9(half4(worldNormal, 1)), 0, o.lightDir.w);
				#ifdef USE_WUV
					half2 worldUV = o.worldPos.xz * -1;
					o.uv = worldUV * _SAUVScale;
				#endif

				TRANSFER_SHADOW(o)
				UNITY_TRANSFER_FOG(o, o.pos);

				#ifdef LIGHTMAP_ON
					o.uv2 = v.texcoord1.xy * unity_LightmapST.xy + unity_LightmapST.zw;
				#endif
				#ifdef DYNAMICLIGHTMAP_ON
					o.uv3 = v.texcoord2.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
				#endif

				return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);//VR

				fixed4 col = tex2D(_AlbedoTex, i.uv);

				#ifdef USE_NM
					half3 tNormal = UnpackScaleNormal(tex2D(_SABumpMap, i.uv), _SABumpScale);
					half3 wNormal = worldNormal(i.tspace0, i.tspace1, i.tspace2, tNormal);
				#else
					half3 wNormal = normalize(i.normal);
				#endif

				fixed shadow = SHADOW_ATTENUATION(i);
				fixed atten = 0;
				fixed lightCal = lerp(shadow, atten, i.lightDir.w);

				half3 specular = fixed3(0, 0, 0);
				#ifdef USE_SP
					fixed4 specularTex = tex2D(_SpecularTex, i.uv);
					specular = computeSpecular(i.lightDir.xyz, wNormal, i.viewDir, specularTex, lightCal);
				#endif

				half3 reflection = fixed3(1, 1, 1);
				fixed reflMask = 0;
				#ifdef USE_RF
					fixed4 reflMaskTex = tex2D(_ReflectionMask, i.uv);
					reflMask = reflMaskTex.r;
					reflection = worldReflect(i.worldPos, wNormal, i.lightDir.xyz);
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
					col.rgb = lightmapSACol(col.rgb, i.lightDir.xyz, wNormal, lightCal, reflection, reflMask, specular, lightmap, lightmask);
				//Subtractive
				#elif defined (LIGHTMAP_ON) && defined(LIGHTMAP_SHADOW_MIXING)
					col.rgb *= lightmap;
				//Baked Indirect
				#elif defined (LIGHTMAP_ON)
					col.rgb = lightmapSACol(col.rgb, i.lightDir.xyz, wNormal, lightCal, reflection, reflMask, specular, lightmap, lightmask);
				#else
					col = computeSACol(col, i.ambient, i.lightDir.xyz, wNormal, lightCal, reflection, reflMask, specular);
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

			Blend One One

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_fwdadd_fullshadows
			#pragma multi_compile_fog
			#pragma shader_feature _ USE_NM
			#pragma shader_feature _ USE_SP
			#pragma shader_feature _ USE_WUV
			#include "LMArtShader.cginc"

			struct v2f
			{
				float2 uv : TEXCOORD0;
				//SHADOW_COORDS(1)
				UNITY_FOG_COORDS(2)
				float4 pos : SV_POSITION;
				#ifdef USE_NM
					float3 tspace0 : TEXCOORD3;
					float3 tspace1 : TEXCOORD4;
					float3 tspace2 : TEXCOORD5;
				#else
					half3 normal : TEXCOORD3;
				#endif
				half3 ambient : COLOR0;
				#ifdef USE_SP
					float3 viewDir : TEXCOORD6;
				#endif
				LIGHTING_COORDS(8, 9)
				half4 lightDir : TEXCOORD10;

				UNITY_VERTEX_OUTPUT_STEREO//VR
			};

			v2f vert(appdata_SA v)
			{
				v2f o;

				UNITY_SETUP_INSTANCE_ID(v);//VR
				UNITY_INITIALIZE_OUTPUT(v2f, o); //VR
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); //VR

				o.pos = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;

				half3 worldNormal = UnityObjectToWorldNormal(v.normal);
				#ifdef USE_NM
					float3x3 tspaceC = tspace(worldNormal, v.normal, v.tangent);
					o.tspace0 = tspaceC[0];
					o.tspace1 = tspaceC[1];
					o.tspace2 = tspaceC[2];
				#else
					o.normal = worldNormal;
				#endif

				#ifdef USE_SP
					o.viewDir = normalize(WorldSpaceViewDir(v.vertex));
				#endif
				half3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
				o.lightDir = half4(normalize(lerp(_WorldSpaceLightPos0.xyz, (_WorldSpaceLightPos0.xyz - worldPos), _WorldSpaceLightPos0.w)), _WorldSpaceLightPos0.w);
				o.ambient = lerp(ShadeSH9(half4(worldNormal, 1)), 0, o.lightDir.w);
				#ifdef USE_WUV
					half2 worldUV = worldPos.xz * -1;
					o.uv = worldUV * _SAUVScale;
				#endif
				
				UNITY_TRANSFER_FOG(o, o.pos);
				TRANSFER_VERTEX_TO_FRAGMENT(o);

				return o;
			}

			fixed4 frag(v2f i) : SV_Target
			{
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);//VR

				fixed4 col = tex2D(_AlbedoTex, i.uv);

				#ifdef USE_NM
					half3 tNormal = UnpackScaleNormal(tex2D(_SABumpMap, i.uv), _SABumpScale);
					half3 wNormal = worldNormal(i.tspace0, i.tspace1, i.tspace2, tNormal);
				#else
					half3 wNormal = i.normal;
				#endif

				fixed shadow = 1.0;
				fixed atten = LIGHT_ATTENUATION(i);
				fixed lightCal = lerp(shadow, atten, i.lightDir.w);

				half3 specular = fixed3(0, 0, 0);
				#ifdef USE_SP
					fixed4 specularTex = tex2D(_SpecularTex, i.uv);
					specular = computeSpecular(i.lightDir.xyz, wNormal, i.viewDir, specularTex, lightCal);
				#endif

				half3 reflection = fixed3(1, 1, 1);
				fixed reflMask = 0;

				col = computeSACol(col, i.ambient, i.lightDir.xyz, wNormal, lightCal, reflection, reflMask, specular);

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

				UNITY_SETUP_INSTANCE_ID(v);//VR
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
