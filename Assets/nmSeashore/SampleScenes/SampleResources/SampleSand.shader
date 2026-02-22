Shader "nmSeashore/SampleSand"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _Moss ("Moss", Color) = (1,1,1,1)
        _WetSandTex ("Wet Sand", 2D) = "" {}
        _WhiteSandTex ("White Sand", 2D) = "" {}
        _NoiseTex ("Noise", 2D) = "" {}
    }
    SubShader
    {
        Tags {
			"RenderType"="Opaque"
		}
        LOD 200
		
		CGPROGRAM
		#pragma surface surf Standard fullforwardshadows
		#pragma target 3.0
			
		sampler2D _WetSandTex;
		sampler2D _WhiteSandTex;
		sampler2D _NoiseTex;
			
		fixed4 _Color;
		fixed4 _Moss;
		
        struct Input
        {
            float3 worldPos;
			float4 Color:COLOR;
        };
		
        void surf(Input i, inout SurfaceOutputStandard o)
        {
			float2 pos = i.worldPos.xz;
			float3 localPos = mul(unity_WorldToObject, float4(i.worldPos, 1));
			
			float4 whiteSand = tex2D(_WhiteSandTex, pos / 10);
			float4 wetSand = tex2D(_WetSandTex, pos / 10);
			float noiseTex = tex2D(_NoiseTex, pos / 2).x;
				
			float wetSandAmplify = saturate(-localPos.y * 0.5 + 1.1);
			float mossAmplify = 1 - saturate(-localPos.y * 0.12 + 0.8);
			mossAmplify *= mossAmplify;
			float4 sand = lerp(whiteSand * _Color, wetSand, smoothstep(0, noiseTex, wetSandAmplify));
			float4 col = lerp(whiteSand * _Moss, sand, smoothstep(noiseTex, 0, mossAmplify));
			o.Albedo = col;
			
			o.Smoothness = 0;
			o.Metallic = 0;
		}
		ENDCG
    }
    FallBack "Diffuse"
}
