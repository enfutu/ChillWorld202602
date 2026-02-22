Shader "nmSeashore/SampleConcrete"
{
    Properties
    {
        _MainTex ("Main", 2D) = "" {}
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
		
		sampler2D _MainTex;
		
        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
			float4 Color:COLOR;
        };
		
        void surf(Input i, inout SurfaceOutputStandard o)
        {
			float2 pos = i.worldPos.xz;
			float3 localPos = mul(unity_WorldToObject, float4(i.worldPos, 1));
			
			float4 col = tex2D(_MainTex, i.uv_MainTex) * i.Color * i.Color;
			o.Albedo = col;
			
			o.Smoothness = 0;
			o.Metallic = 0;
		}
		ENDCG
    }
    FallBack "Diffuse"
}
