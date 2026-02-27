#ifndef MOTHUTILITY_INCLUDED

#define MOTHUTILITY

float makeNoise(fixed2 p)
{
    return frac(sin(dot(p, fixed2(13.11217, 91.134))) * 89123.5656);
}

static const float eps = .0001;
static const float id[8] = { 7, 0, 1, 2, 3, 4, 5, 6 };
                        
static const float4 debug[8] =
{
    float4(1, 0, 0, 1),
    float4(1, 1, 0, 1),
    float4(1, 0, 1, 1),
    float4(1, 1, 1, 1),
    float4(0, 1, 0, 1),
    float4(0, 1, 1, 1),
    float4(0, 0, 1, 1),
    float4(0, 0, 0, 1)
};

static const float3 round[32] =
{
    { 0.5000, 0.0, 0.0000 },
    { 0.4904, 0.0, 0.0975 },
    { 0.4619, 0.0, 0.1913 },
    { 0.4157, 0.0, 0.2778 },
    { 0.3536, 0.0, 0.3536 },
    { 0.2778, 0.0, 0.4157 },
    { 0.1913, 0.0, 0.4619 },
    { 0.0975, 0.0, 0.4904 },
    { 0.0000, 0.0, 0.5000 },
    { -0.0975, 0.0, 0.4904 },
    { -0.1913, 0.0, 0.4619 },
    { -0.2778, 0.0, 0.4157 },
    { -0.3536, 0.0, 0.3536 },
    { -0.4157, 0.0, 0.2778 },
    { -0.4619, 0.0, 0.1913 },
    { -0.4904, 0.0, 0.0975 },
    { -0.5000, 0.0, 0.0000 },
    { -0.4904, 0.0, -0.0975 },
    { -0.4619, 0.0, -0.1913 },
    { -0.4157, 0.0, -0.2778 },
    { -0.3536, 0.0, -0.3536 },
    { -0.2778, 0.0, -0.4157 },
    { -0.1913, 0.0, -0.4619 },
    { -0.0975, 0.0, -0.4904 },
    { -0.0000, 0.0, -0.5000 },
    { 0.0975, 0.0, -0.4904 },
    { 0.1913, 0.0, -0.4619 },
    { 0.2778, 0.0, -0.4157 },
    { 0.3536, 0.0, -0.3536 },
    { 0.4157, 0.0, -0.2778 },
    { 0.4619, 0.0, -0.1913 },
    { 0.4904, 0.0, -0.0975 }
};

static const int pattern[16] =
{
    0, 8, 2, 10,
    12, 4, 14, 6,
    3, 11, 1, 9,
    15, 7, 13, 5
};

int BoundsTest(float3 emitPos, float3 lightPos, float scale, float baseLength)
{
    float3 boundsMin = emitPos - (float3(1, 1, 1) * scale);
    float3 boundsMax = emitPos + (float3(1, 1, 1) * scale);
    //baseLengthの方が長ければclampされたと考える
    int minTest = step(baseLength - eps, length(max(boundsMin, lightPos) - emitPos));
    int maxTest = step(baseLength - eps, length(min(boundsMax, lightPos) - emitPos));
                
    return minTest * maxTest;
}

float2 FragTest(int num, float3 testLightPos, float3 center)
{
    half range = 5 * (1 / sqrt(unity_LightAtten[num].z));
    half dist = length(testLightPos - center);
    half power = 1 - saturate(dist);
    
    half kill = step(55.9, range) * step(range, 56.1);       //Range = 56でkill
    half excite = step(32.9, range) * step(range, 33.1);     //Range = 33でexcite
    kill *= power;
    excite *= power;
    
    return float2(kill, excite);
}

float3 RotZ(float3 pos, float rot)
{
    half c, s;
    c = cos(rot);
    s = sin(rot);
    half4x4 rotateMatrixZ = half4x4(c, -s, 0, 0,
                                    s, c, 0, 0,
                                    0, 0, 1, 0,
                                    0, 0, 0, 1);
    return mul(rotateMatrixZ, pos);
}

float3 RotY(float3 pos, float rot)
{
    half c, s;
    c = cos(rot);
    s = sin(rot);
    half4x4 rotateMatrixY = half4x4(c, 0, s, 0,
                                    0, 1, 0, 0,
                                   -s, 0, c, 0,
                                    0, 0, 0, 1);
    return mul(rotateMatrixY, pos);
}

float3 CalcLightColor(float3 wv, float4x4 iv, float lightMin)
{
    float3 lightCol = 0;
    for (int i = 0; i < 8; i++)
    {
        float3 _lightCol = unity_LightColor[i].xyz;
        float3 _lightPos = mul(iv, unity_LightPosition[i]).xyz;
        
        half intensy = dot(_lightCol, _lightCol);
        half3 toL = (_lightPos - wv * unity_LightPosition[i].w);
        half lengthSq = dot(toL, toL);
        half at = 1 / (1 + lengthSq * unity_LightAtten[i].z);
        float blendPower = intensy * at;
        _lightCol = lerp(0, _lightCol, blendPower);
        lightCol += _lightCol * lerp(.5, 1, unity_LightPosition[i].w);  //PointLightなどの色を強く受けて欲しいので、DirectionalLightの影響は控えめにする
    }
    
    lightCol.r = clamp(lightCol.r, lightMin, 1);
    lightCol.g = clamp(lightCol.g, lightMin, 1);
    lightCol.b = clamp(lightCol.b, lightMin, 1);
        
    return lightCol;
}


float CalcTransparency(float3 cameraPos, float3 wv, float3 lightPos)
{
    float3 cameraToWv = wv - cameraPos;
    float3 cameraToLight = lightPos - cameraPos;
    float len_cv = length(cameraToWv);
    float len_cl = length(cameraToLight);
    //cameraがlightより頂点に近い且つ2つのベクトルが一致しているほど透明度が高い
    float transparency0 = saturate(dot(normalize(cameraToWv + eps), normalize(cameraToLight + eps)));
    transparency0 = lerp(0, saturate(transparency0), step(len_cv, len_cl));
    //directionalLightの場合も考える必要があり、directionalLightはめちゃくちゃ高い位置にあると仮定
    float3 cameraToDlight = half3(0, 1, 0); //half3(cameraPos.x, 1000, cameraPos.y) - cameraPos;
    float len_cdl = 1000;
    float transparency1 = saturate(dot(normalize(cameraToWv + eps), cameraToDlight));
    transparency1 = lerp(0, saturate(transparency1), step(len_cv, 1000));
    
    //transparency0の影響度は低めに見積もる
    transparency0 *= .5;
    float result = saturate(transparency0 + transparency1);

    return pow(result, 2);
}

float Dither(float alpha, float4 screen)
{
    float2 screenPos = screen.xy / (screen.w + eps);
    float2 screenPosInPixel = screenPos.xy * _ScreenParams.xy;
    int ditherUV_x = (int) (screenPosInPixel.x % 4);
    int ditherUV_y = (int) (screenPosInPixel.y % 4);
    float dither = pattern[ditherUV_x + (ditherUV_y * 4)];
    dither = (dither + 1) / 16;
    
    return dither - pow(1.02 - alpha, 10);

}
#endif