#ifndef MOTHVERTEXSHADER_INCLUDED

#define MOTHVERTEXSHADER


v2f vert(appdata v)
{
    v2f o;

    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_INITIALIZE_OUTPUT(v2f, o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                
    //■Value■■■■■■■■■■■■■
    
    
    //ParticleSystemの中心座標を取得(※particleSystem-MainモジュールのStartColorが(1,1,1,1)でなければ正しく動作しない)
    float shapeScale = 10;
    
    float lifeTime = 10;
    float3 emitPos = v.center - v.velocity.xyz * v.uv.w * lifeTime;
    emitPos -= normalize(v.velocity.xyz) * (shapeScale - _OrbitalRange);
    
    
    //v.color.r = pow(v.color.r, 1 / 2.2);                  //ガンマ空間→リニア空間
    v.color.r = floor(v.color.r * 10.9) * .1;               //本当は*11のはずだけど変なズレがあるので*10.9

    emitPos += (v.color.r - .5) * 2 * shapeScale * v.normal;
    emitPos = lerp(emitPos, v.center, _FreeMothMode);       //Enableなら蛾を元の位置へ戻す使用しない

    //particleSystemの中心を探るため滅茶苦茶な情報が入った頂点位置と法線情報をリセットする
    float3 inputNormal = v.normal;
 
    float3 wv = v.vertex;
    _ParticleSize *= saturate(v.uv.w * 10);
    float2 st = v.uv.xy * fixed2(1, 4);
    st.y = (st.y + v.velocity.w) - 4;
    wv.xz = ((st - .5) * _ParticleSize) + emitPos.xz;
    wv.y = emitPos.y;
    float3 local = wv - emitPos; 
    v.normal = float3(0, 1, 0);
    
    //_FreeMothModeがオンならリセット不要
    wv = lerp(wv, v.vertex, _FreeMothMode);
    v.normal = lerp(v.normal, inputNormal, _FreeMothMode);
    
    float particleSize = max(v.noise.w, .3); //乱数をサイズとして使用
    local *= particleSize;                              //モジュールではなくshaderでランダムなサイズを設定
    float sizeOffset = 1 / (particleSize + eps);        //particleサイズで変更したい値にかけると良い感じになるやつ

    float pindex = v.center.w;
    int myNum = pindex % 16;
          
    //左右の目の中心をcamera座標とした逆マトリクスを作る
    float3 cameraPos = _WorldSpaceCameraPos;
    #if defined(USING_STEREO_MATRICES)
        cameraPos = (unity_StereoWorldSpaceCameraPos[0] + unity_StereoWorldSpaceCameraPos[1]) * .5;
    #endif
    float4x4 iv = UNITY_MATRIX_I_V;
    iv._m03_m13_m23 = cameraPos;
    
    float4 lightPositions[16];
    float4 lightColors[16];
    //一括で計算
    for (int i = 0; i < 8; i++)
    {
        lightPositions[i].xyz = mul(iv, unity_LightPosition[i]).xyz;
        lightPositions[i].w = unity_LightPosition[i].w;
        lightColors[i] = unity_LightColor[i];
    }
    //Extentions分を必要なら追加
    lightPositions[8].xyz = _Pos0.xyz; 
    lightPositions[9].xyz = _Pos1.xyz;
    lightPositions[10].xyz = _Pos2.xyz;
    lightPositions[11].xyz = _Pos3.xyz;
    lightPositions[12].xyz = _Pos4.xyz;
    lightPositions[13].xyz = _Pos5.xyz;
    lightPositions[14].xyz = _Pos6.xyz;
    lightPositions[15].xyz = _Pos7.xyz;
    //0以外が入っていれば有効化される
    lightPositions[8].w = step(eps, length(_Pos0.xyz));
    lightPositions[9].w = step(eps, length(_Pos1.xyz));
    lightPositions[10].w = step(eps, length(_Pos2.xyz));
    lightPositions[11].w = step(eps, length(_Pos3.xyz));
    lightPositions[12].w = step(eps, length(_Pos4.xyz));
    lightPositions[13].w = step(eps, length(_Pos5.xyz));
    lightPositions[14].w = step(eps, length(_Pos6.xyz));
    lightPositions[15].w = step(eps, length(_Pos7.xyz));
    //Colorはとりあえず入力
    lightColors[8] = _Col0;
    lightColors[9] = _Col1;
    lightColors[10] = _Col2;
    lightColors[11] = _Col3;
    lightColors[12] = _Col4;
    lightColors[13] = _Col5;
    lightColors[14] = _Col6;
    lightColors[15] = _Col7;
    
    //ライトの座標
    float3 lightPos = lightPositions[myNum].xyz;
    lightPos.y += _LightHeight;                                             //lightに蛾が埋まる場合の高さ調整
    float3 _lightPos = lightPos;                                            //翅の透け表現に使用するため保存
    float3 astray = v.noise.xyz * 5;                                        //ライトへ向かう途中で蛾が迷走するための値
    astray = lerp(astray, v.noise.xyz, _FreeMothMode);
    lightPos += astray;
    
    float len0 = length(lightPos - emitPos); //ベースとなる距離 
    
    float age = v.uv.w;
    //lightが存在しない場合はageを0に
    age = lerp(0, age, lightPositions[myNum].w);
    //lightのカラーが0の場合はageを0に(DPSなどに反応させない)
    age = lerp(0, age, step(.01, length(lightColors[myNum].rgb)));
                
    //■Vertex(local) Position■■■■■■■■■■■■■
    //パーティクルを羽ばたかせる
    //half3 getMask = tex2Dlod(_Map, fixed4(v.uv.xy, 0, 0));
    //half setMask = lerp(1, -1, getMask.r);
    //setMask = lerp(setMask, 0, getMask.g);
    half getMask = v.uv.x;
    half setMask = lerp(1, -1, step(getMask, .5));
    setMask *= 1 - (step(.4, getMask) * step(getMask, .6));
    setMask *= _Flap;
    float flyOffset = v.noise.w * 5;
    float wave = v.uv.y * particleSize * 12; //翅の揺らめき
    float waveSpeed = _WingSpeed + v.noise.w * .001;
    float timeScale = (waveSpeed + flyOffset) * sizeOffset;
    half rot = sin(_Time.y * timeScale + age + wave) * setMask * (.5 + v.noise.w);
    half3 wave_local = RotZ(local, rot);   //翅を動かすだけの動き

    //法線の再計算①
    float3 m0 = wave_local - local;         //翅の頂点の移動量を調べる
    float3 wave_normal = v.normal + m0;     //現在のローカル頂点直上に存在する法線
    //頂点と同じように回転
    wave_normal = RotZ(wave_normal, rot);
    wave_normal = normalize(wave_normal + eps);
    o.param.x = rot;
   
    //■EndPosition(isPhototaxis)■■■■■■■■■■■■■
    //蛾の移動テスト        
    int bt = BoundsTest(emitPos, lightPos, shapeScale, len0);       //到達が想定される距離か(lightPosが、Bounds内であるか)
    age = lerp(0, age, bt);

    float3 endPoint = lerp(emitPos, lightPos, saturate(age * 2));   //saturate(age * 2)→1/2でたどり着いて1/2滞在
    float speed = 2;                                                //蛾は等速で進む
    endPoint = lerp(endPoint, lightPos, step(speed, len0));         //等速で進んでたどり着けない距離であるならlightPosへワープする
    
    float len1 = length(endPoint - emitPos);                        //今回到達する距離
    
    //■SwichPosition■■■■■■■■■■■■■
    //集光性フラグ
    int phototaxisFrag = bt * lightPositions[myNum].w * _EnablePhototaxis;
    
    float3 phototaxis_wv = lerp(endPoint, lightPos, step(len0, len1));
    float3 default_wv = emitPos;
    wv = lerp(default_wv, phototaxis_wv, phototaxisFrag);
    
    //それぞれの進行方向
    float3 phototaxisVec = normalize(lightPos - emitPos + eps);
    float3 defaultVec = cross(inputNormal, normalize(v.velocity.xyz));
    defaultVec = lerp(defaultVec, -normalize(v.velocity.xyz), _FreeMothMode);    //FreeModeならば進行方向を変えてはいけない
    float3 vec = lerp(defaultVec, -phototaxisVec, phototaxisFrag);
    

    //パーティクル正面を進行方向へ向かせる
    //https://zenn.dev/kento_o/articles/95ec904fef8b2c 
    float3 forward0 = vec;
    float3 forward1 = normalize(v.noise.xyz + eps);                     //主にlightへたどり着いたとき色んな方向を向くように
    float3 forward = lerp(forward0, forward1, lerp(0, age, phototaxisFrag));
    float3 up = float3(0, 1, 0);
    float3 right = normalize(cross(forward, up) + eps);
    float3x3 vecMat = (float3x3(right, up, forward));
    local = mul(vecMat, wave_local);

    //法線の再計算
    float3 m1 = local - wave_local;             //翅の頂点の移動量を調べる
    o.normal = mul(vecMat, wave_normal + m1);   //頂点と同じように回転
    o.normal = normalize(o.normal + eps);

    //■Fin■■■■■■■■■■■■■
    
    float kill = 0;    //killフラグ
    float excite = 0;  //exciteフラグ
    for (int j = 0; j < 8; j++)
    {
        float2 answer = FragTest(j, lightPositions[j], wv);
        
        kill += answer.x;
        excite += answer.y;
    }
    kill = saturate(kill);
    excite = saturate(excite * .5);
    
    wv += local;
    
    //excite
    wv += v.noise.xyz * excite * 50;
    
    //■Effects■■■■■■■■■■■■■
    //上下に動く
    //wv.y += (v.noise.w - .5) * 2 * .03;
    wv.y -= cos(_Time.y * timeScale + age) * (v.noise.w - .5) * .3 * particleSize * _ParticleSize; //翅の動きへ同期
    
    //test 
    //wv.xz = (st - .5) * v.uv.z + emitPos.xz;
    //wv.y = emitPos.y;
    
    //カラーに関する
    float3 col = CalcLightColor(wv, iv, _LightValueMin);
    //col = max(col, ShadeSH9(float4(o.normal, 1)));
    o.color.rgb = col;
    o.color.a = 1 - pow(v.uv.w, 2);
    o.color.a = saturate(lerp(o.color.a, -1, kill)); //kill
                
    //翅の透けに関する
    o.param.y = CalcTransparency(cameraPos, wv, _lightPos);
    o.cameraToLightVec = normalize(_lightPos - cameraPos + eps);
    o.cameraToLightVec = lerp(fixed3(0, 1, 0), o.cameraToLightVec, phototaxisFrag);

    //その他
    v.vertex = mul(unity_WorldToObject, float4(wv, 1));
    o.pos = UnityObjectToClipPos(v.vertex);
    o.uv = TRANSFORM_TEX(v.uv.xy, _MainTex);
    o.screenPos = ComputeScreenPos(o.pos);
    o.random.xyz = saturate(v.noise.xyz * 20);
    o.random.w = v.noise.w;
    
    return o;
}

#endif