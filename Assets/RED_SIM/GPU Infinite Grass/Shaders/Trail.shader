Shader "GPU Infinite Grass/Trail" {
    
    Properties {
        _MaskTex ("Mask Render Texture", 2D) = "white" {}
        [HideInInspector] _Decay ("Decay Per Second", Float) = 1.5
    }

    SubShader {
        
        ZWrite Off
        ZTest Always
        Cull Off
        Blend Off

        Pass {
            
            Name "CustomRenderTextureUpdate"

            HLSLPROGRAM
            #pragma vertex   CustomRenderTextureVertexShader
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "UnityCustomRenderTexture.cginc"

            #define TRAIL_TARGETS_MAX 256
            
            sampler2D _MaskTex;           // A channel = world height
            float  _Decay;                  // strength decay per second
            float4 _CameraData;             // xz = cam world pos, w = world size (square side)
            float4 _TrailTargets[TRAIL_TARGETS_MAX]; // xyz = world pos, w = trail radius
            int    _TrailTargetCount;

            float4 _SelfTexture2D_TexelSize; // (1/w, 1/h, w, h)

            float4 frag(v2f_customrendertexture IN) : SV_Target {
                
                float2 uv = IN.globalTexcoord.xy;
                float4 here = tex2D(_SelfTexture2D, uv);
                float surfaceHeight = tex2D(_MaskTex, uv).a + _CameraData.y;

                // RG = bend vector, BA = prev cam XZ
                float2 prevCamXZ = here.ba;
                float2 currCamXZ = _CameraData.xz;
                float invSize = 1.0 / max(_CameraData.w, 1e-6);

                // Scroll by camera movement
                bool isInitialized = any(abs(prevCamXZ) > 1e-6);
                float2 shiftUV = isInitialized ? -((currCamXZ - prevCamXZ) * invSize) : 0.0;
                float2 sampleUV = uv - shiftUV;

                // Avoid clamped smear
                float2 inset = _SelfTexture2D_TexelSize.xy * 0.5;
                bool inside = (sampleUV.x >= inset.x) && (sampleUV.x <= 1.0 - inset.x) && (sampleUV.y >= inset.y) && (sampleUV.y <= 1.0 - inset.y);
                float2 scrolledVec = inside ? tex2D(_SelfTexture2D, sampleUV).rg : 0.0;

                // Linear decay on magnitude
                float dt = max(unity_DeltaTime.x, 0.0);
                float decayStep = max(_Decay, 0.0) * dt;

                float scLen2 = dot(scrolledVec, scrolledVec);
                float scLen  = (scLen2 > 1e-6) ? sqrt(scLen2) : 0.0;
                float2 scDir = (scLen > 1e-6) ? (scrolledVec / scLen) : 0.0;

                float baseLen = max(scLen - decayStep, 0.0);
                float2 vec = scDir * baseLen;

                // Aspect correction (w/h) for non-square CRT
                float aspect = _SelfTexture2D_TexelSize.y * _SelfTexture2D_TexelSize.z;
                float2 aspectScale = float2(aspect, 1.0);

                // Linear response
                float attack = saturate(8.0 * dt);

                // Clamp count to array size
                uint targetCount = (uint)min(_TrailTargetCount, (int)TRAIL_TARGETS_MAX);

                [loop] for (uint i = 0; i < targetCount; i++) {
                    
                    float4 t = _TrailTargets[i];

                    // World: -> UV
                    float2 targetUV = (t.xz - currCamXZ) * invSize + 0.5;
                    float2 d = (uv - targetUV) * aspectScale;

                    // Vertical distance from sphere center to surface
                    float dy = t.y - surfaceHeight;
                    float r = abs(t.w);

                    // No intersection with surface
                    if (abs(dy) >= r) continue;

                    // Sphere cross-section radius at surface (world units)
                    float sliceRadius = sqrt(r * r - dy * dy);

                    // World -> UV
                    float outer = sliceRadius * invSize;
                    if (outer <= 1e-6) continue;

                    // Early reject by squared distance
                    float dist2 = dot(d, d);
                    float outer2 = outer * outer;
                    if (dist2 >= outer2) continue;

                    // Mask: 1 center -> 0 edge
                    float dist = sqrt(dist2);
                    float mask = 1.0 - smoothstep(0.0, outer, dist);
                    if (mask <= 1e-6) continue;

                    // Direction (normalized by dist)
                    float invDist = rsqrt(max(dist2, 1e-6));
                    float2 newDir = d * invDist;

                    // Current strength
                    float oldLen = saturate(length(vec));
                    float newLen = mask;

                    // Strength: rise linearly, drop handled by decay above
                    float len = lerp(oldLen, max(oldLen, newLen), attack);

                    // Direction weight: less change if already strongly bent
                    float oldInv = 1.0 - oldLen;
                    float lock = oldInv * oldInv; 
                    lock *= lock; // (1-oldLen)^4
                    float w = saturate(newLen * lock);

                    float2 oldDir = (oldLen > 1e-6) ? (vec / oldLen) : 0.0;

                    // Anti-cancel: don't average opposite dirs when already bent
                    float dp = dot(oldDir, newDir);
                    float2 outDir;
                    if (dp < 0.0 && oldLen > 0.25) {
                        outDir = oldDir;
                    } else {
                        float2 blended = lerp(oldDir, newDir, w);
                        float bl2 = dot(blended, blended);
                        outDir = (bl2 > 1e-12) ? (blended * rsqrt(bl2)) : oldDir;
                    }

                    vec = outDir * len;
                }

                // RG = bend vector, BA = curr cam XZ
                return float4(vec.x, vec.y, currCamXZ.x, currCamXZ.y);
            }
            ENDHLSL
        }
    }
}
