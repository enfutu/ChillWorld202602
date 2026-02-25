Shader "enfutu/fakeShadowGrass"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _MainColor ("maincolor",  Color) = (0,0,0,0) 
        _BottomColor ("rootcolor", Color) = (0,0,0,0)
        _Vectle ("windvec", Vector) = (0,0,0,0)
    }
    SubShader
    {
		Tags {
			"Queue"      = "AlphaTest"
			"RenderType" = "TransparentCutout"
		}
        LOD 100

        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;

                // single pass instanced rendering
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
                float4 value : TEXCOORD2;

                // single pass instanced rendering
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST, _BottomColor, _MainColor;
            float4 _Vectle;
            fixed4 _LightColor0;

            float random (fixed2 p) { 
            return frac(sin(dot(p, fixed2(12.9898,78.233))) * 43758.5453);
            }

            v2f vert (appdata v)
            {
                v2f o;

                // single pass instanced rendering
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                //DirectionalLightÇÊÇË
                float3 n = UnityObjectToWorldNormal(v.normal);
                float d = max(0, dot(n, _WorldSpaceLightPos0.xyz));
                //d = pow(d, 2);

                //ínñ Ç©ÇÁÇÃçÇÇ≥Ç…âûÇ∂
                float3 wv = mul(unity_ObjectToWorld, v.vertex);
                float g = saturate((wv.y + 1.5));
                
                o.value = float4(d, pow(1 - g, 5), 0,0);


                //óhÇÁÇ∑
                float3 p0 = mul(unity_ObjectToWorld, float4(0,0,0,1));
                float rand = random(p0.xz) * 11;
                wv.xyz += sin(_Time.y * 2 + rand * 10) * (.001 + rand * .001) * g * _Vectle.xyz * _Vectle.w; 

                v.vertex.xyz = mul(unity_WorldToObject, float4(wv,1));

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // single pass instanced rendering
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                fixed4 col = tex2D(_MainTex, i.uv);

                clip(col.a - .4);

                col.rgb = lerp(col.rgb, _BottomColor.rgb, i.value.y);
                col.rgb *= i.value.x;

                col *= _LightColor0 * _MainColor;
                
                // apply fog
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
