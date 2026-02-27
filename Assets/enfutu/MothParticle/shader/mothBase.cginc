#ifndef MOTHBASE_INCLUDED

#define MOTHBASE

struct appdata
{
    float4 vertex : POSITION;
    float4 color : COLOR;
    float3 normal : NORMAl;
    float4 uv : TEXCOORD0;          //xy:uv, z:size.x, w:age
    float4 center : TEXCOORD1;      //xyz:center, w:particleIndex
    float4 noise : TEXCOORD2;       //xyz:noise, w:random
    float4 velocity : TEXCOORD3;    //xyz:velocity, w:animFrame


    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct v2f
{
    float4 pos : SV_POSITION;
    float4 color : COLOR;
    float3 normal : NORMAL;
    float2 uv : TEXCOORD0;          //xy:uv
    float4 screenPos : TEXCOORD1;
    float3 cameraToLightVec : TEXCOORD2;
    float2 param : TEXCOORD3;       //x:翅の回転角, y:翅の透過率
    float4 random : TEXCOORD4;      //xyz:noise w:random
        
    UNITY_VERTEX_OUTPUT_STEREO
};

sampler2D _MainTex;
float _Boost, _MaskPower, _RandomColor, _RandomGloss, _LightValueMin;
float _ParticleSize, _OrbitalRange, _LightHeight, _Flap, _WingSpeed, _EnablePhototaxis, _FreeMothMode;
float4 _MainTex_ST, _Color, _GradMin, _GradMax;

float4 _Pos0, _Pos1, _Pos2, _Pos3, _Pos4, _Pos5, _Pos6, _Pos7;
float4 _Col0, _Col1, _Col2, _Col3, _Col4, _Col5, _Col6, _Col7;

#if _USEFAKESHADOW_FALSE
#include "mothUnlit.cginc"
#else
#include "mothVertexShader.cginc"

fixed4 fragBase(v2f i) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                
    half2 st = i.uv.xy * fixed2(1, 4);
    fixed noise = saturate(makeNoise(st) + .5);
    half rot = i.param.x;

    //アトラス化されたTextureサンプリング用
    half2 coord0 = i.uv.xy * fixed2(.25, 1); //mainTex
    half2 coord1 = coord0 + fixed2(.25, 0);  //glossMask
    half2 coord2 = coord1 + fixed2(.25, 0);  //colorMask
    half2 coord3 = coord2 + fixed2(.25, 0);  //fakeShadowMask

    i.random = saturate(i.random * 5);
    fixed4 texCol = tex2D(_MainTex, coord0);
    fixed3 addColor = saturate(_Color.rgb + lerp(0, i.random, _RandomColor));
    fixed4 col = 0;
    col.rgb = lerp(texCol.rgb, texCol.rgb * addColor, tex2D(_MainTex, coord2).r);
    col.a = texCol.a;

    //cutout
    clip(col.a - .9);
                
    //ambient
    half ambient = dot(fixed3(.05, 1, .02), i.normal); //directionalLightの代替値を使用
    ambient = saturate(ambient + .5);
    col.rgb *= ambient;

    //翅の明るさ(光の透過)
    fixed transMask = tex2D(_MainTex, coord3);
    float transPower = saturate(dot(abs(i.normal), i.cameraToLightVec)); //法線とcamera→lightベクトルが一致するほど高い
    transPower *= transMask * i.param.y;
    fixed3 transColor = (col.rgb + i.color) * .5;
    col.rgb = lerp(col.rgb, transColor, transPower);
                               
    //lightColor
    col.rgb *= i.color;
    
    col.rgb = saturate(col.rgb);
               
    //gloss
    float glossOffset = sin(abs(rot));
    float glossMask = tex2D(_MainTex, coord1).r;
    glossMask = pow(glossMask, max(_MaskPower, 1));
    
    _GradMax.rgb = lerp(_GradMax.rgb, i.random.rgb, _RandomGloss);
    _GradMin.rgb = lerp(_GradMin.rgb, i.random.grb, _RandomGloss);
    fixed3 gloss = lerp(_GradMin, _GradMax, pow(glossOffset, .5)) * _Boost * glossMask;
    float brightness = saturate(abs(rot) + saturate(.2 - _Flap) + .1);       //羽ばたきが小さいほどbrightnessの変化を少なくする
    gloss = gloss * brightness * (noise + glossMask);
    col.rgb += gloss;

    //alpha
    //col.a = saturate(col.a * i.color.a);
                
    //dither
    clip(Dither(i.color.a, i.screenPos));
    
    //col.a = 1;
    return col;
}

fixed4 fragStencil(v2f i) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    //アトラス化されたTextureサンプリング用
    half2 coord0 = i.uv.xy * fixed2(.25, 1); //mainTex
    half2 coord1 = coord0 + fixed2(.25, 0); //glossMask
    half2 coord2 = coord1 + fixed2(.25, 0); //colorMask
    half2 coord3 = coord2 + fixed2(.25, 0); //fakeShadowMask

    fixed4 texCol = tex2D(_MainTex, coord0);
    
    //cutout
    clip(texCol.a - .9);
    
        
    i.random = saturate(i.random * 5);
    fixed3 addColor = saturate(_Color.rgb + lerp(0, i.random, _RandomColor));
    fixed transMask = tex2D(_MainTex, coord3);
    
    fixed4 col = texCol;
    col.rgb = lerp(texCol.rgb, texCol.rgb * addColor, tex2D(_MainTex, coord2).r);
    
    //補色の計算
    float maxValue = max(max(col.r, col.b), col.g);
    float minValue = min(min(col.r, col.b), col.g);
    float3 complementaryColor = (maxValue + minValue) - col.rgb;
       
    half rot = i.param.x;
    col.rgb = complementaryColor;
    col.rgb *= abs(rot);
    col = saturate(col + .5);
    
    //dither
    float transPower = saturate(dot(abs(i.normal), i.cameraToLightVec)); //法線とcamera→lightベクトルが一致するほど高い
    transPower *= i.param.y;
    transPower = saturate(1 - transPower);
    
    clip(Dither(i.color.a - transPower, i.screenPos));
   
    //col.a = 1;
    return col;
}

fixed4 fragShadow(v2f i) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    
    //depthを書き込むことが目的なので影の描画は必要ない
    
    half2 coord0 = i.uv.xy * fixed2(.25, 1); //mainTex
    fixed4 texCol = tex2D(_MainTex, coord0);
    
    //cutout
    clip(texCol.a - .9);
    
    //dither
    clip(Dither(i.color.a, i.screenPos));
    
    return 0;
}
#endif

#endif