Shader "nmSeashore/BeachExtend"
{
	Properties
	{
		_Tint ("Tint", Color) = (0, 0, 0, 1)
        _MainTex ("Main Texture", 2D) = "white" {}
		_Smoothness ("Smoothness", Range(0, 1)) = 0.5
        [Normal] _Normal ("Normal", 2D) = "bump" {}
		_GroundTint ("Beach Tint", Color) = (0, 0, 0, 1)
        _GroundTex ("Beach Texture", 2D) = "white" {}
	}
		SubShader
		{
			Tags { "RenderType"="Opaque" }
			LOD 200
			
			CGPROGRAM
			#pragma surface surf Standard fullforwardshadows
			#pragma target 3.0
			
			#include "UnityCG.cginc"
			
			sampler2D _MainTex, _Normal, _GroundTex;
			float4 _Tint, _GroundTint;
			float _Smoothness;
			
			struct Input
			{
				float2 uv_MainTex;
				float2 uv_Normal;
				float2 uv_GroundTex;
				float4 color:COLOR0;
			};
			
			void surf (Input i, inout SurfaceOutputStandard o)
			{
				float mask = i.color.r;
				float4 tex = tex2D(_MainTex, i.uv_MainTex) * _Tint;
				float3 normal = UnpackNormal(tex2D(_Normal, i.uv_Normal));
				float4 ground = tex2D(_GroundTex, i.uv_GroundTex) * _GroundTint;
				
				o.Albedo = lerp(tex, ground, mask);
				o.Normal = lerp(normal, float3(0, 0, 1), mask);
				o.Smoothness = tex.a * (1 - mask) * _Smoothness;
				
				o.Metallic = 0;
				o.Alpha = 1;
			}
			ENDCG
		}
		FallBack "Diffuse"
	}
