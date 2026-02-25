sampler2D _MaskTex;
#if defined(_GRASS_R)
    #if defined(_TEXMODE_R_ARRAY)
        UNITY_DECLARE_TEX2DARRAY(_GrassArrayR);
    #else
        sampler2D _GrassTexR;
    #endif
#endif
#if defined(_GRASS_G)
    #if defined(_TEXMODE_G_ARRAY)
        UNITY_DECLARE_TEX2DARRAY(_GrassArrayG);
    #else
        sampler2D _GrassTexG;
    #endif
#endif
#if defined(_GRASS_B)
    #if defined(_TEXMODE_B_ARRAY)
        UNITY_DECLARE_TEX2DARRAY(_GrassArrayB);
    #else
        sampler2D _GrassTexB;
    #endif
#endif
#if defined(_TRAIL)
    sampler2D _TrailTex;
#endif
#if defined(_CLOUDS)
    sampler2D _CloudsTex;
    float _CloudsScale;
    float4 _CloudsDir;
    float _CloudsMasking;
    float _CloudsSharpness;
    float _CloudsDarkness;
    float _CloudsBrightness;
#endif

#if defined(_GRASS_R)
    fixed4 _ColorLowR, _ColorHighR;
#endif
#if defined(_GRASS_G)
    fixed4 _ColorLowG, _ColorHighG;
#endif
#if defined(_GRASS_B)
    fixed4 _ColorLowB, _ColorHighB;
#endif

float _EdgeFade;
float _EdgeCulling;
float _EdgeSimplifying;
float _EdgeSimplifyingFade;

float _MaskThreshold;
float _SizeThreshold;
float _YBias;
#if defined(_GRASS_R)
    float _BladeHeightR;
    float _BladeWidthR;
    float _SizeRandomR;
    float _BendRandomR;
    float _WindPowerR;
    float _ArrayCountR;
    float _TextureModeR;
#endif
#if defined(_GRASS_G)
    float _BladeHeightG;
    float _BladeWidthG;
    float _SizeRandomG;
    float _BendRandomG;
    float _WindPowerG;
    float _ArrayCountG;
    float _TextureModeG;
#endif
#if defined(_GRASS_B)
    float _BladeHeightB;
    float _BladeWidthB;
    float _SizeRandomB;
    float _BendRandomB;
    float _WindPowerB;
    float _ArrayCountB;
    float _TextureModeB;
#endif
float _VisibleAmount;
float _Cutoff;
float _SizeWidthImpactR;
float _SizeWidthImpactG;
float _SizeWidthImpactB;

float _WindPower;
float _WindAmp1, _WindAmp2, _WindAmp3;
float4 _WindDir1, _WindDir2, _WindDir3;
float _WindFreq1, _WindFreq2, _WindFreq3;
float _WindSpeed1, _WindSpeed2, _WindSpeed3;

float4 _CameraData;
float4 _RealCameraData;
float _HexCellSize;
float2 _HexCameraCell;
#if !defined(GRASS_SHADOW_PASS)
    #if defined(_TRAIL)
        float _TrailBrightness;
    #endif
    float4 _LightColor0;
    #if !defined(GRASS_FORWARD_ADD)
        float4 _SSSColor;
        float _SSSBrightness;
        float _SSSRadius;
    #endif
#endif
#if defined(_TRAIL)
    float _TrailBend;
#endif
float _BottomBlending;
uint _DrawCallId = 0;
uint _TotalParticleCount = 1;
uint _InstancesPerDrawCall = 511;
uint _ParticlesPerInstance = 16383;

#define PI2 6.28318530718
#define SQRT3 1.73205080757
#define EDGE_CULL_SIZE_FADE 0.08
#define SIDE_CULL_SIZE_FADE 0.12
#define BOTTOM_DITHER_PAD_MIN 0.02
#define BOTTOM_DITHER_PAD_SCALE 0.35
#define BOTTOM_DITHER_PAD_MAX 0.08
#define TOP_HEIGHT_CLAMP_MULT 1.25

#if defined(SHADER_API_GLES3) || defined(SHADER_API_VULKAN)
    #define GRASS_MOBILE 1
#else
    #define GRASS_MOBILE 0
#endif

#if !defined(GRASS_SHADOW_PASS)
    #if !GRASS_MOBILE // Simplification for mobile
        #if defined(GRASS_FORWARD_ADD)
            #define CULL_VERTEX(o) { o.pos=float4(0,0,0,-1); o.col=0; o.uv=0; o.arrayIndex=0; o.screenPos=float4(0,0,0,0); o.typeId=0; o.lodMask=0; o.worldPos=0; return o; }
        #else
            #define CULL_VERTEX(o) { o.pos=float4(0,0,0,-1); o.col=0; o.uv=0; o.arrayIndex=0; o.screenPos=float4(0,0,0,0); o.typeId=0; o.lodMask=0; o.worldPos=0; o.lightDirect=0; return o; }
        #endif
    #else
        #if defined(GRASS_FORWARD_ADD)
            #define CULL_VERTEX(o) { o.pos=float4(0,0,0,-1); o.col=0; o.uv=0; o.arrayIndex=0; o.typeId=0; o.worldPos=0; return o; }
        #else
            #define CULL_VERTEX(o) { o.pos=float4(0,0,0,-1); o.col=0; o.uv=0; o.arrayIndex=0; o.typeId=0; o.worldPos=0; o.lightIndirect=0; o.lightDirect=0; return o; }
        #endif
    #endif
#else
    #if !GRASS_MOBILE // Simplification for mobile
        #define CULL_VERTEX(o) { o.pos=float4(0,0,0,-1); o.uv=0; o.arrayIndex=0; o.screenPos=float4(0,0,0,0); o.typeId=0; o.lodMask=0; return o; }
    #else
        #define CULL_VERTEX(o) { o.pos=float4(0,0,0,-1); o.uv=0; o.arrayIndex=0; o.typeId=0; return o; }
    #endif
#endif

struct appdata {
    float4 vertex : POSITION;
    uint vertexID : SV_VertexID;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

#if !defined(GRASS_SHADOW_PASS)
struct v2f {
    float4 pos : SV_POSITION;
    float3 col : COLOR0;
    float2 uv : TEXCOORD0;
    nointerpolation float typeId : TEXCOORD1;
    nointerpolation float arrayIndex : TEXCOORD2;
    
    #if !GRASS_MOBILE
        float4 screenPos : TEXCOORD3;
        nointerpolation float lodMask : TEXCOORD4;
    #endif
    
    #if !defined(GRASS_FORWARD_ADD)
        #if !GRASS_MOBILE
            float3 lightDirect : TEXCOORD8;
        #else
            nointerpolation float3 lightIndirect : TEXCOORD3;
            nointerpolation float3 lightDirect : TEXCOORD4;
        #endif
    #endif
    
    float3 worldPos : TEXCOORD5;
    UNITY_LIGHTING_COORDS(6, 7)
    
    UNITY_VERTEX_OUTPUT_STEREO
};
#else
struct v2f {
    float4 pos : SV_POSITION;
    float2 uv : TEXCOORD0;
    nointerpolation float typeId : TEXCOORD1;
    nointerpolation float arrayIndex : TEXCOORD2;
    
    #if !GRASS_MOBILE
        float4 screenPos : TEXCOORD3;
        nointerpolation float lodMask : TEXCOORD4;
    #endif
    
    UNITY_VERTEX_OUTPUT_STEREO
};
#endif

// Provoking vertex ID
#if defined(SHADER_API_GLCORE) || defined(SHADER_API_GLES) || defined(SHADER_API_GLES3)
    #define PROVOKING_VERTEX_ID 3u // OpenGL / OpenGL ES default: last vertex
#else
    #define PROVOKING_VERTEX_ID 0u // D3D / Vulkan default / Metal: first vertex
#endif

#if !GRASS_MOBILE
// Bayer 4x4 thresholds
static const float4 B0 = float4( 0,  8,  2, 10) / 16.0;
static const float4 B1 = float4(12,  4, 14,  6) / 16.0;
static const float4 B2 = float4( 3, 11,  1,  9) / 16.0;
static const float4 B3 = float4(15,  7, 13,  5) / 16.0;
#endif

// Remaps value to 01
float Remap01(float value, float min, float max){
    return (value - min) / (max - min);
}
float3 Remap01(float3 value, float3 min, float3 max){
    return (value - min) / (max - min);
}

// Positive mask range keeps threshold behavior, negative range can drive signed scale from float RT.
float DecodeScaleMask(float maskValue) {
    [branch] if (maskValue >= _MaskThreshold) return Remap01(maskValue, _MaskThreshold, 1);
    [branch] if (maskValue < 0.0) return maskValue;
    return 0.0;
}

float SignedEdgeScale(float rawScale, float sizeRandom, float sizeThreshold) {
    float rawScaleSign = rawScale >= 0 ? 1.0 : -1.0;
    float normalizedScale = saturate(abs(rawScale) / max(1 + sizeRandom, 1e-6));
    return normalizedScale * sizeThreshold * rawScaleSign;
}

#if defined(_TRAIL)
    // Sample Trail Texture reprojected because the trail CRT is always one frame behind
    float2 SampleTrailReprojected(float2 uv) {
        float2 camXZ = tex2Dlod(_TrailTex, float4(uv, 0, 0)).zw;
        float worldSize = max(_CameraData.w, 1e-6);
        float2 shiftedUV = uv + ((_CameraData.xz - camXZ) / worldSize);
        return tex2Dlod(_TrailTex, float4(shiftedUV, 0, 0)).xy;
    }
#endif

// Rotates 2D vector to a provided angle
float2 Rotate2D(float2 v, float a) {
    float s = sin(a), c = cos(a);
    return float2(c*v.x - s*v.y, s*v.x + c*v.y);
}

// UV of a quad grass blade based on the corner ID.
// Corner layout:
// 0 = bottom-left, 1 = bottom-right, 2 = top-left, 3 = top-right
float2 QuadUV(uint cornerID) {
    if (cornerID == 0u) return float2(0, 0);
    if (cornerID == 1u) return float2(1, 0);
    if (cornerID == 2u) return float2(0, 1);
    return float2(1, 1);
}

// World-space multi-sine wind field in XZ
float2 WindXZ(float2 worldXZ, float t) {
    
    float2 d1 = _WindDir1.xz; float l1 = max(length(d1), 1e-4); d1 /= l1;
    float2 d2 = _WindDir2.xz; float l2 = max(length(d2), 1e-4); d2 /= l2;
    float2 d3 = _WindDir3.xz; float l3 = max(length(d3), 1e-4); d3 /= l3;

    float p1 = dot(worldXZ, d1) * _WindFreq1 + t * _WindSpeed1;
    float p2 = dot(worldXZ, d2) * _WindFreq2 + t * _WindSpeed2 + 1.7;
    float p3 = dot(worldXZ, d3) * _WindFreq3 + t * _WindSpeed3 + 3.1;
    
    float s1 = sin(p1);
    float s2 = sin(p2);
    float s3 = sin(p3);
    
    float2 v = d1 * s1 * _WindAmp1 + d2 * s2 * _WindAmp2 + d3 * s3 * _WindAmp3;
    
    return v;
}

// Bends a vertex in local space based on XZ bend direction and power. MaxLean is (0..1)
float3 BendVertex(float3 localPos, float2 bendXZ, float maxLean) {

    float ySign = localPos.y >= 0 ? 1.0 : -1.0;
    float y = abs(localPos.y);

    // Normalize bend dir and get magnitude
    float b2 = dot(bendXZ, bendXZ);
    float bendMag = saturate(sqrt(b2));

    // If no bend, early out
    if (bendMag <= 1e-6 || maxLean <= 1e-6) return localPos;

    float2 dirXZ = bendXZ * rsqrt(max(b2, 1e-8));
    float r = saturate(bendMag * maxLean); // sin(theta) in [0..1]
    r = min(r, 0.999); // safety to avoid sqrt(0) issues
    localPos.xz += dirXZ * (r * y); // Horizontal offset grows with height
    localPos.y = ySign * y * sqrt(1.0 - r * r); // Keep stem length: y' = y * cos(theta)
    return localPos;
    
}

// Get Instance ID
uint GetInstanceID() {
    #if defined(UNITY_INSTANCING_ENABLED) || defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
        return unity_InstanceID;
    #else
        return 0;
    #endif
}

// Random
uint HashUint(uint x) {
    x ^= x >> 16;
    x *= 0x7feb352du;
    x ^= x >> 15;
    x *= 0x846ca68bu;
    x ^= x >> 16;
    return x;
}
float UintToFloat01(uint h) {
    uint bits = 0x3F800000u | (h >> 9);
    return asfloat(bits) - 1.0;
}
// Pointy-top hex axial to world XZ
float2 AxialToWorldXZ(int2 axial, float hexSize) {
    float q = (float)axial.x;
    float r = (float)axial.y;
    return float2(hexSize * SQRT3 * (q + r * 0.5), hexSize * 1.5 * r);
}

// Convert linear index to a hex spiral axial coordinate
int2 HexSpiralIndexToAxial(uint index) {
    if (index == 0u) return int2(0, 0);

    int ring = (int)floor((sqrt((float)index * 12.0 - 3.0) + 3.0) * 0.16666667);
    uint ringU = (uint)ring;
    uint ringStart = 1u + 3u * (ringU - 1u) * ringU;
    uint offset = index - ringStart;
    uint side = offset / ringU;
    uint step = offset - side * ringU;
    int s = (int)step;

    if (side == 0u) return int2(-ring + s, ring);
    if (side == 1u) return int2(s, ring - s);
    if (side == 2u) return int2(ring, -s);
    if (side == 3u) return int2(ring - s, -ring);
    if (side == 4u) return int2(-s, -ring + s);
    return int2(-ring, s);
}

v2f vert(appdata v) {
    
    v2f o;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_INITIALIZE_OUTPUT(v2f, o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

    #if defined(GRASS_SHADOW_PASS) && !defined(_GRASS_ENABLE_SHADOW_PASS)
    CULL_VERTEX(o);
    #endif

    // IDs
    uint vid = v.vertexID;      // Vertex ID
    uint pid = vid / 4;         // Particle ID
    uint cid = vid - pid * 4;   // Corner ID
    uint iid = GetInstanceID(); // Instance ID
    uint dcid = _DrawCallId;    // Draw Call ID
    
    // Global particle ID across all draw calls and instances
    uint drawInstanceId = dcid * max(_InstancesPerDrawCall, 1u) + iid;
    uint globalParticleId = drawInstanceId * max(_ParticlesPerInstance, 1u) + pid;
    uint totalParticleCount = max(_TotalParticleCount, 1u);
    [branch] if (globalParticleId >= totalParticleCount) CULL_VERTEX(o);
    uint clusterParticleId = globalParticleId;
    uint crossBladeId = 0u;
    #if defined(_TRIPLE_CROSS)
        clusterParticleId = globalParticleId / 3u;
        crossBladeId = globalParticleId - clusterParticleId * 3u;
    #endif
    
    // World params
    float worldSize = max(_CameraData.w, 0.001); // Render Texture World Size
    float worldSizeInv = 1 / worldSize;
    float worldRadius = worldSize * 0.5;
    float3 rtCameraPos = _CameraData.xyz;
    float2 rtCameraPos2D = rtCameraPos.xz;
    float2 realCameraPos2D = _RealCameraData.xz;
    
    // Tiling around player (snapped world hex grid + hex spiral from camera cell)
    float hexSize = max(_HexCellSize, 1e-6);
    int2 cameraCell = int2((int)_HexCameraCell.x, (int)_HexCameraCell.y);
    int2 cellCoord = cameraCell + HexSpiralIndexToAxial(clusterParticleId);
    uint2 cellBits = asuint(cellCoord);

    // Random values (0..1) from world cell coordinates
    uint h = HashUint(cellBits.x * 0x9E3779B9u ^ cellBits.y * 0x85EBCA6Bu);
    float rnd0 = UintToFloat01(h);
    [branch] if (rnd0 > _VisibleAmount) CULL_VERTEX(o); // Density cull
    h = HashUint(h); float rnd1 = UintToFloat01(h);
    h = HashUint(h); float rnd2 = UintToFloat01(h);
    h = HashUint(h); float rnd3 = UintToFloat01(h);
    h = HashUint(h); float rnd4 = UintToFloat01(h);

    float jitterStrength = 1.5;
    #if defined(_TRIPLE_CROSS)
        jitterStrength *= 3; // sqrt(3): compensate 3x lower cluster density
    #endif
    float2 jitter = (float2(rnd1, rnd2) - 0.5) * jitterStrength;
    float2 cellOffset = float2(SQRT3 * (jitter.x + jitter.y * 0.5), jitter.y * 1.5) * (hexSize * 0.5);
    float2 particleWorldXZ = AxialToWorldXZ(cellCoord, hexSize) + cellOffset;
    // Radius culling
    float2 particleRTCameraPos2D = particleWorldXZ - rtCameraPos2D; // RT camera-space particle 2D position
    float particleRTDistanceSq = dot(particleRTCameraPos2D, particleRTCameraPos2D);
    [branch] if (particleRTDistanceSq > worldRadius * worldRadius) CULL_VERTEX(o);

    // Random far particles culling
    float particleRTDistance = sqrt(particleRTDistanceSq);
    float edgeCullingMask = saturate(Remap01(particleRTDistance, 0, worldRadius));
    float edgeCullValue = edgeCullingMask * (1 - rnd3);
    float edgeCullThreshold = 1 - _EdgeCulling;
    float edgeCullFade = saturate((edgeCullThreshold - edgeCullValue) / EDGE_CULL_SIZE_FADE);
    [branch] if (edgeCullValue > edgeCullThreshold) CULL_VERTEX(o);
    
    // Grass LOD mask (0 is close to real camera, 1 is far from real camera)
    float2 particleRealCameraPos2D = particleWorldXZ - realCameraPos2D;
    float particleRealDistanceSq = dot(particleRealCameraPos2D, particleRealCameraPos2D);
    float particleRealDistance = sqrt(particleRealDistanceSq);
    float lodMask = saturate(Remap01(particleRealDistance, (1 -_EdgeSimplifying) * worldRadius, worldRadius) / max(_EdgeSimplifyingFade, 1e-6));
    
    // Culling far grass that is facing to the side
    float rndRotation = rnd3 * PI2 * 100;
    float2 rndDirection2D = Rotate2D(float2(0,1), rndRotation);
    float2 cameraDirection2D = particleRealCameraPos2D * rsqrt(max(particleRealDistanceSq, 1e-8));
    float sideCullValue = abs(dot(rndDirection2D, cameraDirection2D));
    float sideCullThreshold = lodMask * 0.5;
    float sideCullFade = 1.0;
    [branch] if (lodMask > 1e-4) sideCullFade = saturate((sideCullValue - sideCullThreshold) / (SIDE_CULL_SIZE_FADE * lodMask));
    [branch] if (sideCullValue < sideCullThreshold) CULL_VERTEX(o);
    float cullSizeFade = min(edgeCullFade, sideCullFade);
    
    // Mask texture per particle
    float2 maskTexUV = saturate(particleRTCameraPos2D * worldSizeInv + 0.5); // Mask RT UV per particle
    float4 maskTex = tex2Dlod(_MaskTex, float4(maskTexUV, 0, 0));
    
    // Scaling particles
    float edgeFadeMask = 1 - saturate(Remap01(particleRTDistance, (1 -_EdgeFade) * worldRadius, worldRadius)); // Mask for descaling particles far from the RT center
    float sizeThreshold = max(1e-6, _SizeThreshold);
    float3 baseScale = 0; // Particle scales based on the vertex color
    float3 rndScale = 1; // Particles extra random scale factor 
    float3 finalScale = 0; // Final particle scale
    float rndSigned = rnd3 * 2 - 1;

    #if defined(_GRASS_R)
        baseScale.x = DecodeScaleMask(maskTex.r);
        rndScale.x = 1 + rndSigned * _SizeRandomR;
        float rawScaleR = baseScale.x * rndScale.x;
        float edgeScaleR = SignedEdgeScale(rawScaleR, _SizeRandomR, sizeThreshold);
        finalScale.x = lerp(edgeScaleR, rawScaleR, edgeFadeMask) * cullSizeFade;
    #endif
    #if defined(_GRASS_G)
        baseScale.y = DecodeScaleMask(maskTex.g);
        rndScale.y = 1 + rndSigned * _SizeRandomG;
        float rawScaleG = baseScale.y * rndScale.y;
        float edgeScaleG = SignedEdgeScale(rawScaleG, _SizeRandomG, sizeThreshold);
        finalScale.y = lerp(edgeScaleG, rawScaleG, edgeFadeMask) * cullSizeFade;
    #endif
    #if defined(_GRASS_B)
        baseScale.z = DecodeScaleMask(maskTex.b);
        rndScale.z = 1 + rndSigned * _SizeRandomB;
        float rawScaleB = baseScale.z * rndScale.z;
        float edgeScaleB = SignedEdgeScale(rawScaleB, _SizeRandomB, sizeThreshold);
        finalScale.z = lerp(edgeScaleB, rawScaleB, edgeFadeMask) * cullSizeFade;
    #endif

    // Culling small/inactive particles
    #if defined(_GRASS_R)
        bool activeR = abs(finalScale.x) > sizeThreshold;
    #else
        bool activeR = false;
    #endif
    #if defined(_GRASS_G)
        bool activeG = abs(finalScale.y) > sizeThreshold;
    #else
        bool activeG = false;
    #endif
    #if defined(_GRASS_B)
        bool activeB = abs(finalScale.z) > sizeThreshold;
    #else
        bool activeB = false;
    #endif
    int activeCount = (activeR ? 1 : 0) + (activeG ? 1 : 0) + (activeB ? 1 : 0);
    [branch] if (activeCount == 0) CULL_VERTEX(o);

    // Picking particle types 
    int typeId;
    #if defined(_GRASS_R) && !defined(_GRASS_G) && !defined(_GRASS_B)
        typeId = 0;
    #elif !defined(_GRASS_R) && defined(_GRASS_G) && !defined(_GRASS_B)
        typeId = 1;
    #elif !defined(_GRASS_R) && !defined(_GRASS_G) && defined(_GRASS_B)
        typeId = 2;
    #elif defined(_GRASS_R) && defined(_GRASS_G) && !defined(_GRASS_B)
        [branch] if (activeCount == 1) {
            typeId = activeR ? 0 : 1;
        } else {
            typeId = rnd3 < 0.5 ? 0 : 1;
        }
    #elif defined(_GRASS_R) && !defined(_GRASS_G) && defined(_GRASS_B)
        [branch] if (activeCount == 1) {
            typeId = activeR ? 0 : 2;
        } else {
            typeId = rnd3 < 0.5 ? 0 : 2;
        }
    #elif !defined(_GRASS_R) && defined(_GRASS_G) && defined(_GRASS_B)
        [branch] if (activeCount == 1) {
            typeId = activeG ? 1 : 2;
        } else {
            typeId = rnd3 < 0.5 ? 1 : 2;
        }
    #else
        [branch] if (activeCount == 1) {
            typeId = activeR ? 0 : (activeG ? 1 : 2);
        } else [branch] if (activeCount == 2) {
            bool pickFirst = rnd3 < 0.5;
            int a = activeR ? 0 : (activeG ? 1 : 2);
            int b = activeB ? 2 : 1;
            typeId = pickFirst ? a : b;
        } else {
            typeId = rnd3 < 0.33333334 ? 0 : (rnd3 < 0.6666667 ? 1 : 2);
        }
    #endif
    
    // Particle type params
    float particleHeight = 0;
    float particleWidth = 0;
    float heightGradientScale = 0;
    float bendRandomPower = 0;
    float4 colorLow = 1;
    float4 colorHigh = 1;
    float arrayCount = 1;
    float arrayMode = 0;
    float arraySize01 = 0;
    float windPowerMul = 1;
    float particleHeight01 = 0;
    
    // Choosing params
    #if defined(_GRASS_R) && !defined(_GRASS_G) && !defined(_GRASS_B)
        particleHeight = _BladeHeightR * finalScale.x;
        heightGradientScale = abs(baseScale.x * rndScale.x) / (_SizeRandomR + 1);
        particleWidth = _BladeWidthR * lerp(1, abs(finalScale.x), _SizeWidthImpactR);
        bendRandomPower = _BendRandomR;
        colorLow = _ColorLowR;
        colorHigh = _ColorHighR;
        arrayCount = max(_ArrayCountR, 1);
        arrayMode = _TextureModeR;
        arraySize01 = saturate(Remap01(baseScale.x, _SizeThreshold, 1));
        windPowerMul = _WindPowerR;
        particleHeight01 = abs(finalScale.x);
    #elif !defined(_GRASS_R) && defined(_GRASS_G) && !defined(_GRASS_B)
        particleHeight = _BladeHeightG * finalScale.y;
        heightGradientScale = abs(baseScale.y * rndScale.y) / (_SizeRandomG + 1);
        particleWidth = _BladeWidthG * lerp(1, abs(finalScale.y), _SizeWidthImpactG);
        bendRandomPower = _BendRandomG;
        colorLow = _ColorLowG;
        colorHigh = _ColorHighG;
        arrayCount = max(_ArrayCountG, 1);
        arrayMode = _TextureModeG;
        arraySize01 = saturate(Remap01(baseScale.y, _SizeThreshold, 1));
        windPowerMul = _WindPowerG;
        particleHeight01 = abs(finalScale.y);
    #elif !defined(_GRASS_R) && !defined(_GRASS_G) && defined(_GRASS_B)
        particleHeight = _BladeHeightB * finalScale.z;
        heightGradientScale = abs(baseScale.z * rndScale.z) / (_SizeRandomB + 1);
        particleWidth = _BladeWidthB * lerp(1, abs(finalScale.z), _SizeWidthImpactB);
        bendRandomPower = _BendRandomB;
        colorLow = _ColorLowB;
        colorHigh = _ColorHighB;
        arrayCount = max(_ArrayCountB, 1);
        arrayMode = _TextureModeB;
        arraySize01 = saturate(Remap01(baseScale.z, _SizeThreshold, 1));
        windPowerMul = _WindPowerB;
        particleHeight01 = abs(finalScale.z);
    #else
        #if defined(_GRASS_R)
        [branch] if (typeId == 0) {
            particleHeight = _BladeHeightR * finalScale.x;
            heightGradientScale = abs(baseScale.x * rndScale.x) / (_SizeRandomR + 1);
            particleWidth = _BladeWidthR * lerp(1, abs(finalScale.x), _SizeWidthImpactR);
            bendRandomPower = _BendRandomR;
            colorLow = _ColorLowR;
            colorHigh = _ColorHighR;
            arrayCount = max(_ArrayCountR, 1);
            arrayMode = _TextureModeR;
            arraySize01 = saturate(Remap01(baseScale.x, _SizeThreshold, 1));
            windPowerMul = _WindPowerR;
            particleHeight01 = abs(finalScale.x);
        }
        #endif
        #if defined(_GRASS_G)
        #if defined(_GRASS_R)
        else
        #endif
        [branch] if (typeId == 1) {
            particleHeight = _BladeHeightG * finalScale.y;
            heightGradientScale = abs(baseScale.y * rndScale.y) / (_SizeRandomG + 1);
            particleWidth = _BladeWidthG * lerp(1, abs(finalScale.y), _SizeWidthImpactG);
            bendRandomPower = _BendRandomG;
            colorLow = _ColorLowG;
            colorHigh = _ColorHighG;
            arrayCount = max(_ArrayCountG, 1);
            arrayMode = _TextureModeG;
            arraySize01 = saturate(Remap01(baseScale.y, _SizeThreshold, 1));
            windPowerMul = _WindPowerG;
            particleHeight01 = abs(finalScale.y);
        }
        #endif
        #if defined(_GRASS_B)
        #if defined(_GRASS_R) || defined(_GRASS_G)
        else
        #endif
        [branch] if (typeId == 2) {
            particleHeight = _BladeHeightB * finalScale.z;
            heightGradientScale = abs(baseScale.z * rndScale.z) / (_SizeRandomB + 1);
            particleWidth = _BladeWidthB * lerp(1, abs(finalScale.z), _SizeWidthImpactB);
            bendRandomPower = _BendRandomB;
            colorLow = _ColorLowB;
            colorHigh = _ColorHighB;
            arrayCount = max(_ArrayCountB, 1);
            arrayMode = _TextureModeB;
            arraySize01 = saturate(Remap01(baseScale.z, _SizeThreshold, 1));
            windPowerMul = _WindPowerB;
            particleHeight01 = abs(finalScale.z);
        }
        #endif
    #endif
    
    // Particle Vertical World position
    float particleWorldY = rtCameraPos.y + maskTex.a + _YBias;
    float particleHeightAbs = abs(particleHeight);
    
    // Calculating max possible particle size
    float maxXZ = abs(particleWidth) * 0.5 + particleHeightAbs * bendRandomPower + abs(_WindAmp1) + abs(_WindAmp2) + abs(_WindAmp3);
    #if defined(_TRAIL)
        maxXZ += particleHeightAbs * saturate(_TrailBend);
    #endif
    float rWorld = sqrt(maxXZ * maxXZ + particleHeightAbs * particleHeightAbs);
    float3 centerWS = float3(particleWorldXZ.x, particleWorldY + particleHeight * 0.5, particleWorldXZ.y);
    float3 centerVS = mul(UNITY_MATRIX_V, float4(centerWS, 1.0)).xyz;
    float z = -centerVS.z; // positive distance forward

    // Near/Far check
    float nearP = _ProjectionParams.y;
    float farP  = _ProjectionParams.z;
    [branch] if (z < nearP - rWorld || z > farP + rWorld) CULL_VERTEX(o);
    
    // Perspective frustum check
    float tanHalfX = 1.0 / abs(UNITY_MATRIX_P[0][0]);
    float tanHalfY = 1.0 / abs(UNITY_MATRIX_P[1][1]);
    [branch] if (z > 1e-4 && (abs(centerVS.x) > z * tanHalfX + rWorld || abs(centerVS.y) > z * tanHalfY + rWorld)) CULL_VERTEX(o);
    
    // Calculating particle UV and local vertex offset
    float2 particleUV = QuadUV(cid);
    bool isTopVertex = cid >= 2u;
    float2 quadLocalXZ = float2((particleUV.x - 0.5) * particleWidth, 0);
    float quadLocalY = particleUV.y * particleHeight;
    float heightGradient = particleUV.y * heightGradientScale;
    
    // Initial bend randomization (same tip offset for both top vertices to keep quad seam stable)
    [branch] if (isTopVertex) quadLocalXZ.y = lerp(0, rnd0 * 2 - 1, bendRandomPower) * particleHeight;
    
    // Random initial particle rotation (+cross rotation for triple mode)
    float bladeRotation = rndRotation;
    #if defined(_TRIPLE_CROSS)
        bladeRotation += (PI2 / 6.0) * crossBladeId; // 0, 60, 120 degrees
    #endif
    quadLocalXZ = Rotate2D(quadLocalXZ, bladeRotation);

    // Per-vertex Local Pos
    float3 vertexLocalPos = float3(quadLocalXZ.x, quadLocalY, quadLocalXZ.y);
    
    // Wind and Trail bend - move top edge only (2 top vertices per quad)
    float trailStrength = 0;
    #if defined(_TRAIL)
        float2 trail = SampleTrailReprojected(maskTexUV);
        trailStrength = saturate(length(trail));
    #endif
    [branch] if (isTopVertex) {
        float windWeight = saturate(1 - trailStrength) * saturate(abs(quadLocalY) / max(particleHeightAbs, 1e-6)); // Wind power relative to the particle height
        float2 wind = WindXZ(particleWorldXZ.xy, _Time.y) * (windWeight * _WindPower * windPowerMul * particleHeight01);
        vertexLocalPos += float3(wind.x, 0, wind.y);
        #if defined(_TRAIL)
            vertexLocalPos = BendVertex(vertexLocalPos, trail, _TrailBend);
        #endif
    } 
    
    // Per-vertex World Pos
    float3 vertexWorldPos = float3(particleWorldXZ.x, particleWorldY, particleWorldXZ.y) + vertexLocalPos;
    
    // Sampling per-vertex heightmap
    float2 maskVertexUV = saturate((vertexWorldPos.xz - rtCameraPos2D) * worldSizeInv + 0.5); // Mask Tex per-vertex UV
    float vertexHeight = tex2Dlod(_MaskTex, float4(maskVertexUV, 0, 0)).a;
    float vertexWorldY = _CameraData.y + vertexHeight + _YBias;
    
    [branch] if (!isTopVertex) { // Align bottom to surface and skip offset on steep height delta
        vertexWorldPos.y = abs(vertexWorldY - particleWorldY) <= particleHeightAbs ? vertexWorldY : particleWorldY;
    } else { // Keep top snapping but clamp extreme stretching on steep height deltas
        float topYClamp = particleWorldY + particleHeight * TOP_HEIGHT_CLAMP_MULT;
        [branch] if (particleHeight >= 0) {
            float topYSnap = min(vertexWorldY + 0.01, topYClamp);
            vertexWorldPos.y = max(topYSnap, vertexWorldPos.y);
        } else {
            float topYSnap = max(vertexWorldY - 0.01, topYClamp);
            vertexWorldPos.y = min(topYSnap, vertexWorldPos.y);
        }
    }
    
    // Texture index in the texture array
    float arrayIndex = 0;
    [branch] if (arrayCount > 1.0 && arrayMode > 0.5) {
        arrayIndex = arrayMode < 1.5 ? floor(rnd4 * arrayCount) : floor(arraySize01 * (arrayCount - 1e-4));
        arrayIndex = min(arrayIndex, arrayCount - 1.0);
    }
    #if !defined(GRASS_SHADOW_PASS) && !defined(GRASS_FORWARD_ADD)
    float3 lightIndirect = 0;
    float3 lightDirect = 0;
    
    #if GRASS_MOBILE
    // Calculate Light Volumes and other light data only on the shared provoking quad vertex to save performance on mobile
    [branch] if (cid == PROVOKING_VERTEX_ID) {
    #endif
    
        // Indirect from Light Volumes
        float3 lIndirect;
        #if defined(_SSS)
            float3 l1r, l1g, l1b;
            LightVolumeSH(vertexWorldPos, lIndirect, l1r, l1g, l1b);
            float3 lvDir = normalize(l1r + l1g + l1b);
            float3 vertexDir = normalize(vertexWorldPos - _WorldSpaceCameraPos);
        #else
            lIndirect = LightVolumeSH_L0(vertexWorldPos);
        #endif

        // Direct from main directional light (assumes grass normals face up)
        float3 lDirect = 0;
        [branch] if (_WorldSpaceLightPos0.w == 0) {
            float ndl = saturate(dot(float3(0, 1, 0), _WorldSpaceLightPos0.xyz));
            lDirect = _LightColor0.rgb * ndl;
        }
    
        float noise = 1;
        #if defined(_CLOUDS)
            // Clouds
            float cloudScale = max(_CloudsScale, 1e-4);
            float2 noiseUV = (vertexWorldPos.xz + _CloudsDir.xz * _Time.yy) * cloudScale;
            noise = tex2Dlod(_CloudsTex, float4(noiseUV, 0, 0)).x;
            noise += (_CloudsMasking * 2 - 1);
            noise = smoothstep(0, 1, (saturate(noise) - 0.5) / (1 -_CloudsSharpness) + 0.5);
            float clouds = lerp(_CloudsDarkness, _CloudsBrightness, noise);
            lIndirect *= clouds;
            lDirect *= clouds;
        #endif
        
        // Subsurface Scattering
        #if defined(_SSS)
            float sssMask = noise * _SSSBrightness;
            #if !GRASS_MOBILE // Mask SSS with Height Gradient on non-mobile
                sssMask *= heightGradient;
            #endif
            float3 sss = sssMask * _SSSColor.rgb;
            float sssNdl = _WorldSpaceLightPos0.w == 0 ? max(_SSSRadius + saturate(dot(_WorldSpaceLightPos0.xyz, vertexDir)) - 1, 0) : 0;
            lIndirect += sss * (lIndirect * max(_SSSRadius + saturate(dot(lvDir, vertexDir)) - 1, 0));
            lDirect += sss * (_LightColor0.rgb * sssNdl);
        #endif
        
        lightIndirect = max(lIndirect, 0.0);
        lightDirect = max(lDirect, 0.0);
        
    #if GRASS_MOBILE    
    }
    #endif
    #endif
    // Output
    o.uv = particleUV;
    o.arrayIndex = arrayIndex;
    o.pos = UnityObjectToClipPos(float4(vertexWorldPos, 1.0));
    o.typeId = typeId;
    #if !GRASS_MOBILE
        o.lodMask = lodMask;
        o.screenPos = ComputeScreenPos(o.pos);
    #endif
    #if !defined(GRASS_SHADOW_PASS)
        o.worldPos = vertexWorldPos;
        UNITY_TRANSFER_LIGHTING(o, float2(0, 0));
        float trailBrightnessMul = 1;
        #if defined(_TRAIL)
            trailBrightnessMul = lerp(1, _TrailBrightness, trailStrength);
        #endif
        float3 baseColor = saturate(lerp(colorLow, colorHigh, saturate(heightGradient)) * trailBrightnessMul);
        #if defined(GRASS_FORWARD_ADD)
            o.col = baseColor;
        #else
            #if GRASS_MOBILE
                o.col = baseColor;
                o.lightIndirect = lightIndirect;
                o.lightDirect = lightDirect;
            #else
                o.col = baseColor * lightIndirect;
                o.lightDirect = baseColor * lightDirect;
            #endif
        #endif
    #endif
    
    return o;
    
}

fixed4 frag(v2f i) : SV_Target {
    
    fixed4 tex;
    float2 uv = saturate(i.uv);
    
    // Sampling texture based on type
    #if defined(_GRASS_R) && !defined(_GRASS_G) && !defined(_GRASS_B)
        #if defined(_TEXMODE_R_ARRAY)
            tex = UNITY_SAMPLE_TEX2DARRAY(_GrassArrayR, float3(uv, i.arrayIndex));
        #else
            tex = tex2D(_GrassTexR, uv);
        #endif
    #elif !defined(_GRASS_R) && defined(_GRASS_G) && !defined(_GRASS_B)
        #if defined(_TEXMODE_G_ARRAY)
            tex = UNITY_SAMPLE_TEX2DARRAY(_GrassArrayG, float3(uv, i.arrayIndex));
        #else
            tex = tex2D(_GrassTexG, uv);
        #endif
    #elif !defined(_GRASS_R) && !defined(_GRASS_G) && defined(_GRASS_B)
        #if defined(_TEXMODE_B_ARRAY)
            tex = UNITY_SAMPLE_TEX2DARRAY(_GrassArrayB, float3(uv, i.arrayIndex));
        #else
            tex = tex2D(_GrassTexB, uv);
        #endif
    #else
        #if defined(_GRASS_R)
        [branch] if (i.typeId < 0.5) {
            #if defined(_TEXMODE_R_ARRAY)
                tex = UNITY_SAMPLE_TEX2DARRAY(_GrassArrayR, float3(uv, i.arrayIndex));
            #else
                tex = tex2D(_GrassTexR, uv);
            #endif
        } else
        #endif
        #if defined(_GRASS_G)
        [branch] if (i.typeId < 1.5) {
            #if defined(_TEXMODE_G_ARRAY)
                tex = UNITY_SAMPLE_TEX2DARRAY(_GrassArrayG, float3(uv, i.arrayIndex));
            #else
                tex = tex2D(_GrassTexG, uv);
            #endif
        } else
        #endif
        #if defined(_GRASS_B)
        {
            #if defined(_TEXMODE_B_ARRAY)
                tex = UNITY_SAMPLE_TEX2DARRAY(_GrassArrayB, float3(uv, i.arrayIndex));
            #else
                tex = tex2D(_GrassTexB, uv);
            #endif
        }
        #else
        {
            tex = 0;
        }
        #endif
    #endif
    clip(tex.a - _Cutoff);
    
    // No fwidth and dithering for mobile
    #if GRASS_MOBILE
        float a = 1;
    #else
        float a = saturate((tex.a - _Cutoff) / max(fwidth(tex.a), 1e-4) + 0.5);
        // Bottom blending dither: use extra padded range to avoid a hard transition line
        float bottomBlendRange = max(_BottomBlending, 1e-6);
        float bottomBlendPad = min(BOTTOM_DITHER_PAD_MAX, max(BOTTOM_DITHER_PAD_MIN, bottomBlendRange * BOTTOM_DITHER_PAD_SCALE));
        float bottomBlendRangePadded = min(1.0, bottomBlendRange + bottomBlendPad);
        [branch] if (_BottomBlending > 1e-6 && i.uv.y < bottomBlendRangePadded) {
                float ditherLodMask = saturate(1.0 - i.lodMask / 0.35);
            [branch] if (ditherLodMask > 1e-4) {
                float bottomBlend01 = saturate(i.uv.y / bottomBlendRange);
                float bottomBlendPadRange = max(bottomBlendRangePadded - bottomBlendRange, 1e-6);
                float bottomBlendPadFade = saturate((bottomBlendRangePadded - i.uv.y) / bottomBlendPadRange);
                float2 uvSS = i.screenPos.xy / i.screenPos.w;
                int2 pix = (int2)floor(uvSS * _ScreenParams.xy); // pixel coords
                int2 p = pix & 3; // mod 4
                float t = (p.y == 0) ? B0[p.x] : (p.y == 1) ? B1[p.x] : (p.y == 2) ? B2[p.x] : B3[p.x];
                float dither = t - 0.5;
                float bottomDither = saturate(bottomBlend01 + dither);
                a *= lerp(1.0, bottomDither, ditherLodMask * bottomBlendPadFade);
            }
        }
    #endif

    #if defined(GRASS_SHADOW_PASS)
        // Depth-only output (coverage is controlled by alpha/a2c and clip above)
        return float4(0, 0, 0, a);
    #elif defined(GRASS_FORWARD_ADD)
        UNITY_LIGHT_ATTENUATION(shadowAtten, i, i.worldPos);
        float ndl = 1.0;
        [branch] if (_WorldSpaceLightPos0.w == 0) {
            float3 lightDir = normalize(UnityWorldSpaceLightDir(i.worldPos));
            ndl = saturate(dot(float3(0, 1, 0), lightDir));
        }
        float3 addLighting = i.col * (_LightColor0.rgb * ndl * shadowAtten);
        return float4(tex.rgb * addLighting, a);
    #else
        UNITY_LIGHT_ATTENUATION(shadowAtten, i, i.worldPos);
        #if GRASS_MOBILE
            float3 lighting = max(i.col * (i.lightIndirect + i.lightDirect * shadowAtten), 0.0);
        #else
            float3 lighting = max(i.col + i.lightDirect * shadowAtten, 0.0);
        #endif
        float4 color = float4(tex.rgb * lighting, a);
        return color;
    #endif
}
