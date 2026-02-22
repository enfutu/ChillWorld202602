Shader "nmSeashore/EquipGuide"
{
    Properties
    {
		_Tint ("Tint", Color) = (0, 1, 1, 1)
    }
    SubShader
    {
        Tags {
			"RenderType"="Transparent"
			"Queue"="Transparent"
		}
        LOD 100

        Pass
        {
			Blend SrcAlpha One
			ZWrite Off
			
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
			
			float4 _Tint;

            struct appdata
            {
                float4 vertex : POSITION;
				float3 normal : NORMAL;
				
				UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
				float4 color : COLOR0;
				
				UNITY_VERTEX_OUTPUT_STEREO
            };
			
            v2f vert (appdata v)
            {
				v2f o;
				
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_INITIALIZE_OUTPUT(v2f, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
				
                o.vertex = UnityObjectToClipPos(v.vertex);
				float3 viewDir = normalize(ObjSpaceViewDir(v.vertex));
				float alpha = 1 - dot(viewDir, v.normal);
				o.color = _Tint;
				o.color.a *= alpha * alpha * alpha;
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                return i.color;
            }
            ENDCG
        }
    }
}
