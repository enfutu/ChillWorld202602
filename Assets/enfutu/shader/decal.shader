Shader "enfutu/decal"
{
    Properties
    {
        _Mask ("Mask", 2D) = "white" {}
        _Cutout ("Cutout", range(.001, 1)) = 0
        _MainTex ("Texture", 2D) = "white" {}
        _MainColor ("MainColor", Color) = (0,0,0,1)
        _ShadowMask ("ShadowMask", 2D) = "white" {}
        _Edge ("EdgeValue", range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue" = "Transparent" }
        LOD 100

        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
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

                // single pass instanced rendering
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;

                // single pass instanced rendering
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex, _Mask, _ShadowMask;
            float4 _MainTex_ST, _MainColor;
            float _Cutout, _Edge;

            v2f vert (appdata v)
            {
                v2f o;

                // single pass instanced rendering
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv.xy = TRANSFORM_TEX(v.uv, _MainTex);
                o.uv.zw = v.uv; //tiling off

                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // single pass instanced rendering
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                fixed mask = tex2D(_Mask, i.uv.zw).r;
                clip(mask - _Cutout);

                float2 st = i.uv.xy;
                fixed4 col = tex2D(_MainTex, st);
                
                col.a = saturate(mask + _Edge);

                col *= _MainColor;

                fixed shadow = tex2D(_ShadowMask, i.uv.zw).r;
                col.rgb *= shadow;

                // apply fog
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
