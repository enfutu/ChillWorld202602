Shader "Unlit/UnderwaterScreenJack"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags
		{
			"RenderType"="Transparent"
			"Queue" = "Overlay+1000"
			"IgnoreProjector" = "True"
		}
        LOD 100

        Pass
        {
			ZTest Always
			ZWrite Off
			Cull Off
			Blend DstColor Zero
			
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

			fixed4 _Color;
			
            struct appdata
			{
				float2 uv : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};
			
            struct v2f
			{
				float4 vertex : SV_POSITION;
				UNITY_VERTEX_OUTPUT_STEREO
			};
			
            v2f vert (appdata v)
            {
                v2f o;
				
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_INITIALIZE_OUTPUT(v2f, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
				
                o.vertex = float4(v.uv.x * 2 - 1, v.uv.y * 2 - 1, 0, 1);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return _Color;
            }
            ENDCG
        }
    }
}
