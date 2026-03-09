Shader "enfutu/domeinwarp"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color1("Color1", Color) = (1, 1, 1, 1)
        _Scale ("Scale", Float) = 0.01
        _NoiseScale("Noise Scale", Float) = 10.0
        _Speed("Speed", Float) = 1.0
        _PerlinNoise("Perlin Noise", 2D) = "white" {}
        _Mask ("Mask", 2D) = "white" {}
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
                float4 value : TEXCOORD2;

                // single pass instanced rendering
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex, _PerlinNoise, _Mask;
            float4 _MainTex_ST, _Color1;
            float _Scale, _NoiseScale, _Speed;

            v2f vert (appdata v)
            {
                v2f o;

                // single pass instanced rendering
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.value = 0;
                o.value.xyz = mul(unity_ObjectToWorld, v.vertex);

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv.xy = TRANSFORM_TEX(v.uv, _MainTex);
                o.uv.zw = v.uv;
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // single pass instanced rendering
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float k = _Scale;

                float2 st = i.uv.zw * _NoiseScale;

                float3 q = tex2Dlod(_PerlinNoise, float4(st * k, 0, 0)).rgr;

                float2 r;
                r.xy = tex2Dlod(_PerlinNoise, float4(st * k + q + (0.15 * _Time.x), 0, 0)).rg;

                float2 puv = (st + r) * 0.05;
                puv.y -= _Time.x * _Speed;

                float f = tex2Dlod(_PerlinNoise, float4(puv, 0, 0));

                float coef = (f * f * f + (0.6 * f * f) + (0.5 * f));
                coef = pow(coef, 3) * 10;
                coef = saturate(coef);

                //Å•form
                float d = max(0, length(i.value.xyz - _WorldSpaceCameraPos));
                d = saturate(d * .05) * 4;
                float form = 1 - tex2Dlod(_MainTex, float4(i.uv.xy, 0, fwidth(i.uv.w) * 1920)).r;
                form += coef * .5;
                
                form *= tex2Dlod(_Mask, float4(i.uv.zw, 0, 0)).r;
                //form *= step(.4, form);

                fixed4 color = fixed4(0, 0, 0, 1);
                color = form * coef * _Color1 * 12;

                /*
                // for alpha
                float2 uv = abs(i.uv * 2.0 - 1.0);
                float2 auv = smoothstep(0, 1, 1.0 - uv);

                float a = auv.x * auv.y;

                float3 luminance = float3(0.3, 0.59, 0.11);
                float l = dot(luminance, coef.xxx);

                l = saturate(pow(1.0 - l, 5.0));

                color.rgb = lerp(color.rgb, _Color3, l);

                color.a = a;
                */
                
                color = saturate(color);
                //color = saturate(coef);

                UNITY_APPLY_FOG(i.fogCoord, col);
                return color;
            }
            ENDCG
        }
    }
}
