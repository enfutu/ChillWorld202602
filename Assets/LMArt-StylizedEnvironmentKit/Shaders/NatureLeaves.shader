//--------------------------------------------
//Stylized Environment Kit
//LittleMarsh CG ART
//version1.5.0
//--------------------------------------------

Shader "LMArtShader/NatureLeaves"
{
	Properties
	{
		[NoScaleOffset] _AlbedoTex("Albedo Tex", 2D) = "white" {}
		_CutOff("Alpha Cutoff", Range(0,1)) = 0.5

		[NoScaleOffset]_NormalMap("Normal Map", 2D) = "bump" {}
		_NormalScale("Normal Scale", Range(0,1)) = 1.0

		[Space(16)][Header(Aninmation)]
		[Space(7)]
		[Toggle(USE_VA)]_UseAnimation("Use Animation", Float) = 1.0
		_AnimationScale("Animation Scale", Range(0,1)) = 1.0

		[Space(16)][Header(Back Leaf)]
		[Space(7)]
		[HDR]_TransColor("BackLeaf Color", Color) = (1, 1, 1, 1)
		[NoScaleOffset]_TransTex("BackLeaf ColorTex", 2D) = "white" {}
		_TransArea("BackLeaf Range", Range(0.01,1)) = 0.5
		_TransPower("Translucent Scale", Range(0,1)) = 1.0

		[Space(16)][Header(Specular)]
		[Space(7)]
		[Toggle(USE_SP)]_UseSpecular("Use Specular", Float) = 1.0
		[HDR]_SpecularColor("Specular Color", Color) = (1, 1, 1, 1)
		[NoScaleOffset]_SpecularTex("Specular ColorTex", 2D) = "white" {}
		_Shininess("Shininess", Range(1,96)) = 12
		_SpecularPower("Specular Power", Range(0,3)) = 1.0

		[Space(16)][Header(Shadow)]
		[Space(7)]
		_ShadowIntensity("Shadow Intensity", Range(0,1)) = 1.0
	}

	SubShader
	{


		Pass
		{
				Tags {
				"LightMode" = "ForwardBase"
				"Queue" = "AlphaTest"
				"RenderType" = "TransparentCutout"
				}

				Cull Off

				CGPROGRAM
				#pragma vertex vert
				#pragma fragment frag
				#pragma target 3.0
				#pragma multi_compile_fwdbase_fullshadows
				#pragma multi_compile_fog
				#pragma shader_feature _ USE_VA
				#pragma shader_feature _ USE_SP
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
				half3 ambient : COLOR0;

				half3 ambient2 : COLOR1;

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


			v2f vert(appdata_lf v)
			{
				v2f o;

				UNITY_SETUP_INSTANCE_ID(v);//VR
				UNITY_INITIALIZE_OUTPUT(v2f, o); //VR
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); //VR

				o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;

				#ifdef USE_VA
					float3 vertexMove = fixed3(0,0,0);
					vertexMove = vertexAnimation(o.worldPos, v.vertex, v.color);
					v.vertex.x += vertexMove.x;
					v.vertex.y += vertexMove.y;
					v.vertex.z += vertexMove.z;
				#endif

				o.pos = UnityObjectToClipPos(v.vertex);
				o.uv = v.texcoord;

				half3 worldNormal = UnityObjectToWorldNormal(v.normal);
				float3x3 tspaceC = tspace(worldNormal, v.normal, v.tangent);
				o.tspace0 = tspaceC[0];
				o.tspace1 = tspaceC[1];
				o.tspace2 = tspaceC[2];
				o.lightDir = half4(normalize(_WorldSpaceLightPos0.xyz), _WorldSpaceLightPos0.w);
				o.ambient = lerp(ShadeSH9(half4(worldNormal, 1)), 0, o.lightDir.w);

				o.ambient2 = lerp(ShadeSH9(half4(-worldNormal, 1)), 0, o.lightDir.w);

				#ifdef USE_SP
					o.viewDir = normalize(WorldSpaceViewDir(v.vertex));
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


			fixed4 frag(v2f i, fixed facing : VFACE) : SV_Target
			{
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);//VR

				fixed4 col = tex2D(_AlbedoTex, i.uv);

				half3 tNormal = UnpackScaleNormal(tex2D(_NormalMap, i.uv), _NormalScale);
				half3 wNormal = worldNormalLF(i.tspace0, i.tspace1, i.tspace2, tNormal, facing);

				fixed shadow = lerp(1.0, SHADOW_ATTENUATION(i), _ShadowIntensity);
				fixed atten = 0;
				fixed lightCal = lerp(shadow, atten, i.lightDir.w);

				fixed3 ambientFA = lerp(i.ambient2, i.ambient, step(0, facing));
				
				fixed4 transTex = tex2D(_TransTex, i.uv);

				half3 specular = fixed3(0, 0, 0);
				#ifdef USE_SP
					fixed4 specularTex = tex2D(_SpecularTex, i.uv);
					specular = computeSpecular(i.lightDir.xyz, wNormal, i.viewDir, specularTex, lightCal);
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
					col = computeLFCol(col, lightmap, i.lightDir.xyz, wNormal, lightmask * lightCal, specular, transTex);
				//Subtractive
				#elif defined (LIGHTMAP_ON) && defined(LIGHTMAP_SHADOW_MIXING)
					col.rgb *= lightmap;
				//Baked Indirect
				#elif defined (LIGHTMAP_ON)
					col = computeLFCol(col, lightmap, i.lightDir.xyz, wNormal, lightmask * lightCal, specular, transTex);
				#else
					col = computeLFCol(col, ambientFA, i.lightDir.xyz, wNormal, lightCal, specular, transTex);
				#endif
				
				clip(col.a - _CutOff);
				col.a = 1;

				UNITY_APPLY_FOG(i.fogCoord, col);

				return col;
			}
			ENDCG

		}

		Pass
		{
				Tags {
				"LightMode" = "ForwardAdd"
				"Queue" = "AlphaTest"
				"RenderType" = "TransparentCutout"
				}

				Cull Off
				Blend One One

				CGPROGRAM
				#pragma vertex vert
				#pragma fragment frag
				#pragma target 3.0
				#pragma multi_compile_fwdadd_fullshadows
				#pragma multi_compile_fog
				#pragma shader_feature _ USE_VA
				#pragma shader_feature _ USE_SP
				#include "LMArtShader.cginc"

			


			struct v2f
			{
				float2 uv : TEXCOORD0;
				//SHADOW_COORDS(1)
				UNITY_FOG_COORDS(2)
				float4 pos : SV_POSITION;
				float3 tspace0 : TEXCOORD3;
				float3 tspace1 : TEXCOORD4;
				float3 tspace2 : TEXCOORD5;
				half3 ambient : COLOR0;
				#ifdef USE_SP
					float3 viewDir : TEXCOORD6;
				#endif
				LIGHTING_COORDS(8, 9)
				half4 lightDir : TEXCOORD10;

				UNITY_VERTEX_OUTPUT_STEREO//VR
			};

			v2f vert(appdata_lf v)
			{
				v2f o;

				UNITY_SETUP_INSTANCE_ID(v); //VR
				UNITY_INITIALIZE_OUTPUT(v2f, o); //VR
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); //VR

				float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;

				#ifdef USE_VA
					float3 vertexMove = fixed3(0, 0, 0);
					vertexMove = vertexAnimation(worldPos, v.vertex, v.color);
					v.vertex.x += vertexMove.x;
					v.vertex.y += vertexMove.y;
					v.vertex.z += vertexMove.z;
				#endif

				o.pos = UnityObjectToClipPos(v.vertex);
				o.uv = v.texcoord;

				half3 worldNormal = UnityObjectToWorldNormal(v.normal);
				float3x3 tspaceC = tspace(worldNormal, v.normal, v.tangent);
				o.tspace0 = tspaceC[0];
				o.tspace1 = tspaceC[1];
				o.tspace2 = tspaceC[2];
				o.lightDir = half4(normalize(lerp(_WorldSpaceLightPos0.xyz, (_WorldSpaceLightPos0.xyz - worldPos), _WorldSpaceLightPos0.w)), _WorldSpaceLightPos0.w);
				o.ambient = lerp(ShadeSH9(half4(worldNormal, 1)), 0, o.lightDir.w);

				#ifdef USE_SP
					o.viewDir = normalize(WorldSpaceViewDir(v.vertex));
				#endif
				
				UNITY_TRANSFER_FOG(o, o.pos);
				TRANSFER_VERTEX_TO_FRAGMENT(o);

				return o;
			}


			fixed4 frag(v2f i, fixed facing : VFACE) : SV_Target
			{
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);//VR

				fixed4 col = tex2D(_AlbedoTex, i.uv);

				half3 tNormal = UnpackScaleNormal(tex2D(_NormalMap, i.uv), _NormalScale);
				half3 wNormal = worldNormalLF(i.tspace0, i.tspace1, i.tspace2, tNormal, facing);

				fixed shadow = 1.0;
				fixed atten = LIGHT_ATTENUATION(i);
				fixed lightCal = lerp(shadow, atten, i.lightDir.w);

				fixed4 transTex = tex2D(_TransTex, i.uv);

				half3 specular = fixed3(0, 0, 0);
				#ifdef USE_SP
					fixed4 specularTex = tex2D(_SpecularTex, i.uv);
					specular = computeSpecular(i.lightDir.xyz, wNormal, i.viewDir, specularTex, lightCal);
				#endif

				col = computeLFCol(col, i.ambient, i.lightDir.xyz, wNormal, lightCal, specular, transTex * 0.6);
	
				clip(col.a - _CutOff);
				col.a = 1;

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
			Cull Off

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_shadowcaster
			#pragma shader_feature _ USE_VA
			#include "LMArtShader.cginc"


			struct v2f
			{

				float4 pos : SV_POSITION;
				float2 uv : TEXCOORD0;

				UNITY_VERTEX_OUTPUT_STEREO//VR
			};


			v2f vert(appdata_shd v)
			{
				v2f o;

				UNITY_SETUP_INSTANCE_ID(v); //VR
				UNITY_INITIALIZE_OUTPUT(v2f, o); //VR
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); //VR

				float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;

				#ifdef USE_VA
					float3 vertexMove = fixed3(0, 0, 0);
					vertexMove = vertexAnimation(worldPos, v.vertex, v.color);
					v.vertex.x += vertexMove.x;
					v.vertex.y += vertexMove.y;
					v.vertex.z += vertexMove.z;
				#endif

				o.uv = v.texcoord;
				o.pos = UnityObjectToClipPos(v.vertex);
				TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)

				return o;
			}

			float4 frag(v2f i) : SV_Target
			{
				//UNITY_SETUP_INSTANCE_ID(i); //VR
				//UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);//VR

				fixed4 texcol = tex2D(_AlbedoTex, i.uv);
				clip(texcol.a - _CutOff);
				texcol.a = 1;

				return texcol;

			}
			ENDCG
		}

	}
}
