Shader "RARA/StarfieldUnlit_CloudLinked"
{
    Properties
    {
        // ===== 星空（従来機能） =====
        _Exposure          ("Exposure", Range(0.1, 5.0)) = 1.1
        _StarDensity       ("Star Density (cell scale)", Range(0.3, 3.0)) = 0.9
        _Presence          ("Star Presence (0..1)", Range(0.0, 1.0)) = 0.08
        _BaseSize          ("Base Star Size", Range(0.0003, 0.01)) = 0.0024
        _SizeJitter        ("Size Jitter", Range(0.0, 1.0)) = 0.35
        _BrightnessPow     ("Brightness Power", Range(0.5, 6.0)) = 2.6

        _Kelvin            ("Color Temperature (K)", Range(2000,12000)) = 6100
        _HueVariance       ("Hue Variance", Range(0.0, 1.0)) = 0.20
        _Saturation        ("Saturation", Range(0.0, 2.0)) = 1.0

        _MagSteps          ("Magnitude Steps", Range(3, 8)) = 6
        _MagDistSkew       ("Magnitude Skew (dim↑)", Range(0.4, 3.0)) = 1.4
        _BrightCoreBoost   ("Bright Core Scale Boost", Range(0.0, 0.8)) = 0.25

        _TwinkleSpeed      ("Twinkle Speed (base)", Range(0.0, 8.0)) = 1.6
        _TwinkleDepth      ("Twinkle Depth (base)", Range(0.0, 1.0)) = 0.18
        _TwinkleFrac       ("Selective Twinkle Fraction", Range(0.0, 1.0)) = 0.22
        _TwinkleSelDepth   ("Selective Twinkle Depth", Range(0.0, 1.0)) = 0.45
        _TwinkleChromatic  ("Chromatic Twinkle (hue)", Range(0.0, 0.2)) = 0.04
        _TwinkleHorizonBoost ("Horizon Boost", Range(0.0, 1.0)) = 0.55

        _Layer1Scale       ("Layer1 Scale (coarse)", Range(0.2, 4.0)) = 1.0
        _Layer2Scale       ("Layer2 Scale (mid)", Range(0.5, 8.0)) = 2.0
        _Layer3Scale       ("Layer3 Scale (fine)", Range(1.0, 12.0)) = 3.2
        _LayerMix          ("Layer Mix (L1,L2,L3)", Vector) = (1, 0.7, 0.25, 0)

        _HorizonFadeWidth  ("Horizon Fade Width", Range(0.0, 0.4)) = 0.12
        _HorizonFadeBias   ("Horizon Fade Bias", Range(-0.2, 0.2)) = -0.02

        _NoiseScale        ("Cluster Noise Scale", Range(0.5, 8.0)) = 1.6
        _ClusterStrength   ("Cluster Strength", Range(0.0, 2.0)) = 1.0
        _ClusterContrast   ("Cluster Contrast", Range(0.5, 3.0)) = 1.4

        [Toggle] _GALAXY   ("Enable Galaxy Band", Float) = 0
        _GalaxyStrength    ("Galaxy Strength", Range(0.0, 2.0)) = 0.6
        _GalaxyWidth       ("Galaxy Width", Range(0.05, 0.6)) = 0.25
        _GalaxySharp       ("Galaxy Softness", Range(0.2, 8.0)) = 2.0
        _GalaxyAngleDeg    ("Galaxy Angle (deg)", Range(0,180)) = 35

        _Vignette          ("Vignette", Range(0.0, 1.0)) = 0.1

        [Toggle] _AUTO_ROTATE ("Auto Rotate", Float) = 0
        _AutoRotateDegPerSec ("Auto Rotate Speed (deg/s)", Range(-30.0, 30.0)) = 0.0
        _AutoAxis          ("Auto Axis (x,y,z)", Vector) = (0,1,0,0)

        [Toggle] _METEOR   ("Enable Meteors", Float) = 0
        _MeteorRate        ("Meteors per Second", Range(0.0, 5.0)) = 0.6
        _MeteorBrightness  ("Meteor Brightness", Range(0.0, 8.0)) = 2.0
        _MeteorWidthDeg    ("Meteor Width (deg)", Range(0.02, 2.0)) = 0.25
        _MeteorLengthDeg   ("Meteor Length (deg)", Range(1.0, 40.0)) = 8.0
        _MeteorSpeedDegPerSec ("Meteor Speed (deg/s)", Range(10.0,180.0)) = 60.0
        _PathNormalYawDeg  ("Path Normal Yaw (deg)", Range(0,360)) = 0
        _PathNormalPitchDeg ("Path Normal Pitch (deg)", Range(-89,89)) = 0
        _SpawnBandHalfWidthDeg ("Spawn Band Half-Width (deg)", Range(0.0, 30.0)) = 8.0
        _StartSMinDeg      ("Start s Min (deg)", Range(-90.0, 90.0)) = -25.0
        _StartSMaxDeg      ("Start s Max (deg)", Range(-90.0, 90.0)) = -10.0
        _MeteorCoreSharp   ("Core Sharpness", Range(1.0, 16.0)) = 6.0
        _MeteorTailSoft    ("Tail Softness", Range(0.2, 8.0)) = 2.0
        _MeteorSeed        ("Meteor Seed", Float) = 123.0

        // ===== CloudLink（雲で減光） =====
        [Header(CloudLink)]
        [Toggle] _CL_Enable ("Use Cloud Occlusion", Float) = 1
        _CL_NoiseMap       ("CL NoiseMap (same as TowelCloud)", 2D) = "gray" {}
        _CL_Scale          ("CL scale 雲の大きさ", Float) = 80
        _CL_Cloudy         ("CL cloudy 曇り度合い", Range(0,1)) = 0.4
        _CL_Soft           ("CL soft 雲の柔らかさ", Range(0.0001,0.9999)) = 0.2
        [Toggle]_CL_UnderFade ("CL underFade 下の方の雲を消す", Float) = 1
        _CL_UnderFadeStart ("CL underFadeStart", Range(-1,1)) = -0.5
        _CL_UnderFadeWidth ("CL underFadeWidth", Range(0.0001,0.9999)) = 0.2
        [Toggle]_CL_YMirror ("CL yMirror 下方向をミラー", Float) = 0

        _CL_MoveRotation   ("CL moveRotation 移動方向(°)", Range(0,360)) = 0
        _CL_Speed          ("CL speed 速度", Float) = 1
        _CL_ShapeSpeed     ("CL shapeSpeed 変形量", Float) = 1
        _CL_SpeedOffset    ("CL speedOffset 細部速度差", Float) = 0.2
        _CL_SpeedSlide     ("CL speedSlide 横方向速度", Float) = 0.1
        _CL_FbmScaleUnder  ("CL fbmScaleUnder 細部の変形", Float) = 0.43

        _CL_OcclusionStrength ("CL Occlusion Strength", Range(0.0, 2.0)) = 0.9
        _CL_OcclusionFeather  ("CL Occlusion Feather(power)", Range(0.5, 4.0)) = 1.5
    }

    SubShader
    {
        // 雲（Transparent-100=2900）より**前**に描く
        Tags { "RenderType"="Transparent" "Queue"="Transparent-200" "IgnoreProjector"="True" }

        Cull Front
        ZWrite Off
        ZTest LEqual
        Lighting Off
        Fog { Mode Off }
        Blend One One   // 加算（黒＝透過）

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            static const float PI = 3.14159265359;

            // ====== 星空パラメータ ======
            float _Exposure,_StarDensity,_Presence,_BaseSize,_SizeJitter,_BrightnessPow;
            float _Kelvin,_HueVariance,_Saturation;
            float _MagSteps,_MagDistSkew,_BrightCoreBoost;
            float _TwinkleSpeed,_TwinkleDepth,_TwinkleFrac,_TwinkleSelDepth,_TwinkleChromatic,_TwinkleHorizonBoost;
            float _Layer1Scale,_Layer2Scale,_Layer3Scale; float4 _LayerMix;
            float _HorizonFadeWidth,_HorizonFadeBias;
            float _NoiseScale,_ClusterStrength,_ClusterContrast;
            float _GALAXY,_GalaxyStrength,_GalaxyWidth,_GalaxySharp,_GalaxyAngleDeg;
            float _Vignette;
            float _AUTO_ROTATE,_AutoRotateDegPerSec; float4 _AutoAxis;
            float _METEOR,_MeteorRate,_MeteorBrightness;
            float _MeteorWidthDeg,_MeteorLengthDeg,_MeteorSpeedDegPerSec;
            float _PathNormalYawDeg,_PathNormalPitchDeg;
            float _SpawnBandHalfWidthDeg,_StartSMinDeg,_StartSMaxDeg;
            float _MeteorCoreSharp,_MeteorTailSoft,_MeteorSeed;

            // ====== CloudLink ======
            float _CL_Enable;
            sampler2D _CL_NoiseMap; float4 _CL_NoiseMap_TexelSize;
            float _CL_Scale,_CL_Cloudy,_CL_Soft,_CL_UnderFade,_CL_UnderFadeStart,_CL_UnderFadeWidth,_CL_YMirror;
            float _CL_MoveRotation,_CL_Speed,_CL_ShapeSpeed,_CL_SpeedOffset,_CL_SpeedSlide,_CL_FbmScaleUnder;
            float _CL_OcclusionStrength,_CL_OcclusionFeather;

            struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; };
            struct v2f { float4 pos:SV_POSITION; float3 wpos:TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            // ====== 基本ユーティリティ ======
            float2 hash2(float2 p){ float2 k1=float2(127.1,311.7), k2=float2(269.5,183.3); p=float2(dot(p,k1),dot(p,k2)); return frac(sin(p)*43758.5453); }
            float  hash1(float2 p){ return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float  hash1_3(float3 p){ return frac(sin(dot(p,float3(127.1,311.7,74.7)))*43758.5453); }

            float valueNoise3D(float3 p){
                float3 ip=floor(p),fp=frac(p);
                float c000=hash1_3(ip+float3(0,0,0));
                float c100=hash1_3(ip+float3(1,0,0));
                float c010=hash1_3(ip+float3(0,1,0));
                float c110=hash1_3(ip+float3(1,1,0));
                float c001=hash1_3(ip+float3(0,0,1));
                float c101=hash1_3(ip+float3(1,0,1));
                float c011=hash1_3(ip+float3(0,1,1));
                float c111=hash1_3(ip+float3(1,1,1));
                float3 w=fp*fp*(3.0-2.0*fp);
                float x00=lerp(c000,c100,w.x), x10=lerp(c010,c110,w.x);
                float x01=lerp(c001,c101,w.x), x11=lerp(c011,c111,w.x);
                float y0=lerp(x00,x10,w.y), y1=lerp(x01,x11,w.y);
                return lerp(y0,y1,w.z);
            }
            float fbm3(float3 p){ float a=0.0, amp=0.6; [unroll] for(int i=0;i<3;i++){ a+=valueNoise3D(p)*amp; p*=2.02; amp*=0.5; } return a; }

            float2 DirToSphericalUV(float3 dir){
                float u = atan2(dir.z, dir.x)/(2.0*PI)+0.5;
                float v = asin(clamp(dir.y,-1.0,1.0))/PI+0.5;
                return float2(u,v);
            }

            float3 KelvinToRGB(float K){
                K=clamp(K,1000.0,40000.0)/100.0; float3 rgb;
                rgb.r=(K<=66.0)?1.0:saturate(1.29293618606*pow(K-60.0,-0.1332047592));
                rgb.g=(K<=66.0)?saturate(0.390081578769*log(K)-0.631841443788):saturate(1.129890860895*pow(K-60.0,-0.0755148492));
                if(K>=66.0) rgb.b=1.0; else rgb.b=(K<=19.0)?0.0:saturate(0.54320678911*log(K-10.0)-1.19625408914);
                return saturate(rgb);
            }
            float3 RGB2HSV(float3 c){ float4 K=float4(0.,-1./3.,2./3.,-1.); float4 p=(c.g<c.b)?float4(c.bg,K.wz):float4(c.gb,K.xy);
                float4 q=(c.r<p.x)?float4(p.xyw,c.r):float4(c.r,p.yzx); float d=q.x-min(q.w,q.y); float e=1e-10;
                float h=abs(q.z+(q.w-q.y)/(6.0*d+e)); return float3(h,d/(q.x+e),q.x); }
            float3 HSV2RGB(float3 c){ float3 p=abs(frac(c.xxx+float3(0.,1./3.,2./3.))*6.-3.); float3 a=saturate(p-1.); return c.z*lerp(float3(1,1,1),a,c.y); }

            float HorizonFade(float y){ return smoothstep(_HorizonFadeBias, _HorizonFadeBias + _HorizonFadeWidth, y); }
            float3 RotateAroundAxis(float3 v, float3 axis, float ang){ axis=normalize(axis); float s=sin(ang), c=cos(ang); return v*c + cross(axis,v)*s + axis*dot(axis,v)*(1.0-c); }

            // ===== 銀河・流星ユーティリティ（省略せず維持） =====
            float GalaxyBand(float3 dir, float angleDeg, float width, float sharp){
                float ang=radians(angleDeg); float3 n=normalize(float3(cos(ang),0,sin(ang)));
                float d=abs(dot(dir,n));
                float s=pow(saturate(1.0 - smoothstep(0.0,width,d)), sharp);
                float w=0.5+0.5*sin(12.3*dir.x+7.1*dir.y+5.3*dir.z);
                return s*(0.85+0.15*w);
            }
            float3 YawPitchToDir(float yawDeg, float pitchDeg){
                float yaw=radians(yawDeg), pit=radians(pitchDeg);
                float cp=cos(pit), sp=sin(pit); float cy=cos(yaw), sy=sin(yaw);
                return normalize(float3(cp*cy, sp, cp*sy));
            }
            void BuildGreatCircleBasis(float3 n_hat, out float3 t_hat, out float3 b_hat){
                float3 ref=(abs(n_hat.y)<0.99)?float3(0,1,0):float3(1,0,0);
                t_hat=normalize(cross(ref,n_hat)); b_hat=normalize(cross(n_hat,t_hat));
            }
            void GreatCircleCoords(float3 dir, float3 n_hat, float3 t_hat, float3 b_hat, out float d, out float s){
                d=asin(clamp(dot(dir,n_hat),-1.0,1.0)); s=atan2(dot(dir,b_hat),dot(dir,t_hat));
            }
            float MeteorContribution(float3 dir, float3 n_hat, float3 t_hat, float3 b_hat,
                                     float s_head, float lengthRad, float widthRad, float d_offset,
                                     float coreSharp, float tailSoft)
            {
                float d,s; GreatCircleCoords(dir,n_hat,t_hat,b_hat,d,s);
                float dRel=abs(d-d_offset);
                float across=pow(saturate(1.0 - smoothstep(widthRad*0.5, widthRad, dRel)), coreSharp);
                float TWO_PI=6.28318530718; float sRel=s - s_head; sRel = sRel - TWO_PI*floor((sRel+PI)/TWO_PI);
                float inside=smoothstep(-lengthRad,-lengthRad*0.2,sRel)*(1.0-smoothstep(-0.02,0.0,sRel));
                float tail=pow(saturate(inside),tailSoft);
                return across*tail;
            }

            // ===== 星1層（等星＋選択的ツインクル＋AA） =====
            float3 StarLayerMagAA(float2 uv,float layerScale,float layerWeight,float t,float3 baseColor,
                                  float baseSize,float sizeJit,float brightPow,float hueVar,float satMul,
                                  float twSpeed,float twDepth,float presence,float clusterMask,float dirY_world)
            {
                float2 p=uv*layerScale; float2 cell=floor(p); float2 f=frac(p);
                float presMod=presence*lerp(0.5,1.5,clusterMask);
                if(step(hash1(cell+23.17),presMod)<0.5) return 0;

                int steps=(int)_MagSteps; steps=max(steps,1);
                float rmag=hash1(cell+31.1); float u=pow(rmag,_MagDistSkew);
                int magIdx=(int)floor((1.0-u)*steps); magIdx=clamp(magIdx,0,steps-1);
                float idxN=(steps>1)?(magIdx/(float)(steps-1)):0.0;
                float brightnessQuant=pow(2.512,-magIdx);

                float2 ctr=hash2(cell); float2 d2=f-ctr; float dist=length(d2);
                float rJit=lerp(1.0,(0.4+1.6*hash1(cell+17.3)),sizeJit);
                float brightWeight=(1.0-idxN);
                float coreScale=1.0 + _BrightCoreBoost*brightWeight;
                float r=baseSize*rJit*coreScale;

                float sdf=dist-r; float aa=max(fwidth(saturate(0.2 - dist))*1.3,1e-4);
                float core=1.0 - saturate(smoothstep(-aa,aa,sdf));

                float brightRand=pow(max(1e-3,hash1(cell+3.7)),brightPow);
                float bright=brightRand*brightnessQuant;

                float phase1=6.28318*hash1(cell+9.9);
                float tw1=(sin(t*twSpeed+phase1)*0.5+0.5);
                float tw=1.0 + twDepth*tw1;

                float selected=step(hash1(cell+41.3),_TwinkleFrac);
                if(selected>0.5){
                    float phase2=6.28318*hash1(cell+19.17);
                    float altBoost=1.0 + _TwinkleHorizonBoost*(1.0-abs(dirY_world));
                    float magBoost=0.6 + 0.4*brightWeight;
                    float tw2=(sin(t*(twSpeed*1.7)+phase2)*0.5+0.5);
                    tw += _TwinkleSelDepth*tw2*altBoost*magBoost;
                    float3 hsv=RGB2HSV(baseColor);
                    float hueShift=_TwinkleChromatic*magBoost*altBoost*(tw2-0.5);
                    hsv.x=frac(hsv.x+hueShift); baseColor=HSV2RGB(hsv);
                }

                float3 hsv0=RGB2HSV(baseColor);
                hsv0.x=frac(hsv0.x+hueVar*(hash1(cell+5.5)-0.5));
                hsv0.y=saturate(hsv0.y*satMul);
                float3 starCol=HSV2RGB(hsv0);

                float halo=exp(-6.0*dist*dist);
                float star=saturate(core*(1.0+0.35*halo));

                return starCol*(bright*tw*star)*layerWeight;
            }

            // ===== CloudLink: 雲アルファの計算（TowelCloud簡易再現） =====
            float2 rot2(float2 p, float ang){
                float s=sin(ang), c=cos(ang); return float2(c*p.x - s*p.y, s*p.x + c*p.y);
            }

            float CL_CloudAlpha(float3 worldPos)
            {
                // --- 視線ベクトルと上下反転 ---
                float3 viewDir = normalize(UnityWorldSpaceViewDir(worldPos));
                if(_CL_YMirror>0.5 && 0<viewDir.y) viewDir.y = -viewDir.y;
                float3 reViewDir = -viewDir;

                // --- 半球への投影（TowelCloudと同じ概念） ---
                const float km = 1000.0;
                const float planetR_km = 6000.0;
                const float cloudHeight_km = 10.0;
                float planetR = planetR_km * km;
                float cloudHeight = cloudHeight_km * km;
                float vy = reViewDir.y;
                float totalR = cloudHeight + planetR;

                // 地平線比（トップレート）：下方向フェードに使用
                float topRate = asin(clamp(reViewDir.y, -1.0, 1.0)) * 2.0 / PI;

                // 交点距離（大気層）
                float viewDistance = sqrt(max(1e-6, totalR*totalR - (1.0 - vy*vy) * (planetR*planetR))) - vy * planetR;
                float3 ovalCoord = reViewDir * viewDistance;

                // --- 2Dノイズ座標（xz） ---
                // 回転と速度
                float ang = radians(_CL_MoveRotation);
                float2 slide = float2(_CL_SpeedSlide, 0.0);
                float2 base = float2(_CL_Speed, _CL_ShapeSpeed);
                float2 uv0 = ovalCoord.xz / max(1e-6, _CL_Scale * km);
                uv0 = rot2(uv0, ang);
                float t = _Time.y;

                // fBm（2〜3オクターブのテクスチャノイズ）
                float2 u1 = uv0 + rot2(base*t*0.10 + slide*_CL_SpeedOffset, ang);
                float2 u2 = uv0*2.02 + rot2(base*t*0.20 - slide*_CL_SpeedOffset, ang)*0.7;
                float2 u3 = uv0*4.04 + rot2(base*t*0.35, ang)*0.5;

                float n1 = tex2D(_CL_NoiseMap, u1).r;
                float n2 = tex2D(_CL_NoiseMap, u2).r;
                float n3 = tex2D(_CL_NoiseMap, u3).r;

                // 正規化（0..1）
                float noise = (n1*0.6 + n2*0.3 + n3*0.1);

                // --- 曇度/ソフトからアルファ近似を生成 ---
                float soft2 = _CL_Soft * _CL_Soft;
                float cloudSoftUnder = 1.0 - _CL_Cloudy - soft2 * 1.0;
                float cloudSoftTop   = cloudSoftUnder + soft2 * 2.0;

                // 0..1にリマップ
                float cloudPower = saturate( (noise - cloudSoftUnder) / max(1e-6, (cloudSoftTop - cloudSoftUnder)) );
                // ゆるやかなカーブ（cubic）
                cloudPower = cloudPower*cloudPower*(3.0 - 2.0*cloudPower);

                // 地平線下のフェード
                if(_CL_UnderFade>0.5){
                    float fadeMax = (1.0 - _CL_UnderFadeStart) * _CL_UnderFadeWidth + _CL_UnderFadeStart;
                    float fadeRate = saturate( (topRate - _CL_UnderFadeStart) / max(1e-6, (fadeMax - _CL_UnderFadeStart)) );
                    cloudPower *= fadeRate;
                }

                return saturate(cloudPower);
            }

            // ===== 仕上げ =====
            float3 ApplyVignette(float3 col, float2 uv){
                float2 c=abs(uv-0.5);
                float v=1.0 - _Vignette*smoothstep(0.35,0.7,max(c.x,c.y));
                return col*v;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 視線方向（世界）
                float3 cam = _WorldSpaceCameraPos;
                float3 dirW = normalize(i.wpos - cam);

                // 天球座標（オブジェクト回転を反映）
                float3 dirCel = normalize(mul((float3x3)unity_WorldToObject, dirW));
                if(_AUTO_ROTATE>0.5){
                    float3 axis = normalize(_AutoAxis.xyz);
                    axis = (abs(axis.x)+abs(axis.y)+abs(axis.z)<1e-4)?float3(0,1,0):axis;
                    float ang = radians(_AutoRotateDegPerSec) * _Time.y;
                    dirCel = RotateAroundAxis(dirCel, axis, ang);
                }

                float2 uv = DirToSphericalUV(dirCel);
                float3 baseCol = KelvinToRGB(_Kelvin);
                float  t = _Time.y;

                // 大域クラスタ（星の濃淡）
                float3 np = dirCel * _NoiseScale * 8.0;
                float   n = fbm3(np);
                float clusterMask = pow(saturate(n), _ClusterContrast) * _ClusterStrength;
                clusterMask = saturate(clusterMask);

                // 地平線フェード（世界の上下）
                float horizon = HorizonFade(dirW.y);

                // 星レイヤ
                float scaleBase = 400.0 * _StarDensity;
                float3 col = 0;
                float presenceEff = _Presence * horizon;

                col += StarLayerMagAA(uv, scaleBase*_Layer1Scale, _LayerMix.x, t, baseCol,
                                      _BaseSize, _SizeJitter, _BrightnessPow, _HueVariance, _Saturation,
                                      _TwinkleSpeed, _TwinkleDepth, presenceEff, clusterMask, dirW.y);

                col += StarLayerMagAA(uv, scaleBase*_Layer2Scale, _LayerMix.y, t, baseCol,
                                      _BaseSize*0.75, _SizeJitter, _BrightnessPow*1.1, _HueVariance, _Saturation,
                                      _TwinkleSpeed*0.9, _TwinkleDepth*0.8, presenceEff*0.65, clusterMask, dirW.y);

                col += StarLayerMagAA(uv, scaleBase*_Layer3Scale, _LayerMix.z, t, baseCol,
                                      _BaseSize*0.55, _SizeJitter, _BrightnessPow*1.3, _HueVariance, _Saturation,
                                      _TwinkleSpeed*1.1, _TwinkleDepth*0.6, presenceEff*0.45, clusterMask, dirW.y);

                // 銀河帯
                if(_GALAXY>0.5){
                    float g = GalaxyBand(dirCel, _GalaxyAngleDeg, _GalaxyWidth, _GalaxySharp);
                    float3 gcol = lerp(float3(0.6,0.65,0.9), float3(1.0,0.95,0.9), 0.5);
                    col += gcol * g * _GalaxyStrength;
                }

                // 流星
                if(_METEOR>0.5 && _MeteorRate>0.01 && _MeteorSpeedDegPerSec>0.1 && _MeteorLengthDeg>0.1)
                {
                    float3 n_hat = normalize(YawPitchToDir(_PathNormalYawDeg, _PathNormalPitchDeg));
                    float3 t_hat, b_hat; BuildGreatCircleBasis(n_hat, t_hat, b_hat);

                    float widthRad  = radians(_MeteorWidthDeg);
                    float lengthRad = radians(_MeteorLengthDeg);
                    float speedRad  = radians(_MeteorSpeedDegPerSec);

                    int N=3; float rate=_MeteorRate; float kf=floor(t*rate);
                    for(int m=0;m<N;m++){
                        float id = kf - m;
                        float age = t - id/rate;
                        float life= (lengthRad/speedRad) + 1.0;
                        if(age<0 || age>life) continue;

                        float2 h = hash2(float2(id+_MeteorSeed, id*1.37+_MeteorSeed*0.123));
                        float s0deg = lerp(_StartSMinDeg, _StartSMaxDeg, h.x);
                        float s_head= radians(s0deg) + speedRad*age;
                        float d_off = radians((h.y*2.0-1.0)*_SpawnBandHalfWidthDeg);

                        float S = MeteorContribution(dirCel, n_hat, t_hat, b_hat,
                                                     s_head, lengthRad, widthRad, d_off,
                                                     _MeteorCoreSharp, _MeteorTailSoft);

                        float brightVar = 0.6 + 0.8*hash1(float2(id*2.1+7.7, id*5.3+1.1));
                        float intensity = _MeteorBrightness * brightVar;

                        float3 hot=float3(1.0,0.97,0.92), cool=float3(0.85,0.90,1.0);
                        float3 mcol= lerp(hot, cool, frac(id*0.37));
                        float3 mixCol= lerp(baseCol, mcol, 0.65);

                        col += mixCol * (S * intensity);
                    }
                }

                // === CloudLink 減光 ===
                if(_CL_Enable > 0.5)
                {
                    float cloudA = CL_CloudAlpha(i.wpos);                 // 0..1（厚いほど1）
                    float occ = pow( saturate(1.0 - cloudA * _CL_OcclusionStrength), _CL_OcclusionFeather );
                    col *= occ;
                }

                // 露出・ビネット
                col *= _Exposure;
                col  = ApplyVignette(col, uv);

                return float4(saturate(col), 1);
            }
            ENDCG
        }
    }
    FallBack Off
}
