Shader "enfutu/blur_particle"
{
    Properties
    {
        _Offset("OffsetLength", range(0, 500)) = 0
        _CutOff ("_CutOff", range(0,1)) = 0
        _MainColor ("_MainColor", Color) = (0,0,0,0)
        _Color0 ("Color0", Color) = (0,0,0,0)
        _Color1 ("Color1", Color) = (0,0,0,0)
        _Color2 ("Color2", Color) = (0,0,0,0)
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue" = "Transparent" }
        LOD 100

        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 vc : COLOR;
                float2 uv : TEXCOORD0;
                float3 center : TEXCOORD1;

                // single pass instanced rendering
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 value : TEXCOORD1;
                float4 vc : COLOR;

                // single pass instanced rendering
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST, _MainColor, _Color0, _Color1, _Color2;
            float _CutOff, _Offset;

            v2f vert (appdata v)
            {
                v2f o;

                // single pass instanced rendering
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.value = 0;
                
                float d = length(v.center - _WorldSpaceCameraPos) - _Offset;
                d = saturate(d * .01);

                float3 vec = (v.vertex - v.center);
                vec *= d * 80;

                v.vertex.xyz += vec;

                o.value.x = d;

                o.vc = v.vc;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);

                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // single pass instanced rendering
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float2 st = i.uv;
                
                /*
                float2 st0 = st + float2(.5, 0) * i.value.x;
                float2 st1 = st + float2(0, .5) * i.value.x;
                float2 st2 = st + float2(-.5, 0) * i.value.x;
                float2 st3 = st + float2(0, -.5) * i.value.x;
                */

                float2 st0 = st - float2(0, 0) * i.value.x;
                float2 st1 = st - float2(.4, .25) * i.value.x;
                float2 st2 = st - float2(.4, -.25) * i.value.x;
                //float2 st3 = st + float2(.5, 0) * i.value.x;

                fixed tex0 = tex2D(_MainTex, st0).r;
                fixed tex1 = tex2D(_MainTex, st1).r;
                fixed tex2 = tex2D(_MainTex, st2).r;
                //fixed tex3 = tex2D(_MainTex, st3).r;

                fixed4 col0 = tex0 * _Color0;
                fixed4 col1 = tex1 * _Color1;
                fixed4 col2 = tex2 * _Color2;

                float4 col = saturate((col0 + col1 + col2) * 5);// + tex3);                           
                

                clip(col.a - _CutOff);

                col.a = saturate(col.a - i.value.x);

                col *= i.vc * _MainColor;

                return col;
            }
            ENDCG
        }
    }
}
