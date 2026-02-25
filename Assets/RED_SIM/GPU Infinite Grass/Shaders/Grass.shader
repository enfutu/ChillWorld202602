Shader "GPU Infinite Grass/Grass" {
    
    Properties {
        
        [NoScaleOffset] _MaskTex ("Surface Mask Render Texture", 2D) = "white" {}
        [NoScaleOffset] _TrailTex ("Trail Render Texture", 2D) = "black" {}
        
        [Toggle(_GRASS_R)] _EnableTypeR ("Enable Type R", Float) = 1
        [NoScaleOffset] _GrassTexR ("Grass Texture", 2D) = "white" {}
        _GrassArrayR ("Grass Array", 2DArray) = "" {}
        _ColorLowR ("Color Low", Color) = (1,1,1,1)
        _ColorHighR ("Color High", Color) = (1,1,1,1)
        _BladeHeightR ("Blade Height", Float) = 0.35
        _BladeWidthR  ("Blade Width",  Float) = 0.05
        _SizeWidthImpactR ("Size Width Impact", Range(0,1)) = 0.5
        _SizeRandomR ("Size Randomization", Range(0,1)) = 0.5
        _BendRandomR ("Bend Randomization", Range(0,1)) = 0.5
        _WindPowerR ("Wind Power", Float) = 1
        [Enum(Single Texture,0,Array Random,1,Array By Size,2)] _TextureModeR ("Texture Mode", Float) = 0
        _ArrayCountR ("Array Elements", Float) = 1
        
        [Toggle(_GRASS_G)] _EnableTypeG ("Enable Type G", Float) = 1
        [NoScaleOffset] _GrassTexG ("Grass Texture", 2D) = "white" {}
        _GrassArrayG ("Grass Array", 2DArray) = "" {}
        _ColorLowG ("Color Low", Color) = (1,1,1,1)
        _ColorHighG ("Color High", Color) = (1,1,1,1)
        _BladeHeightG ("Blade Height", Float) = 0.35
        _BladeWidthG  ("Blade Width",  Float) = 0.05
        _SizeWidthImpactG ("Size Width Impact", Range(0,1)) = 0.5
        _SizeRandomG ("Size Randomization", Range(0,1)) = 0.5
        _BendRandomG ("Bend Randomization", Range(0,1)) = 0.5
        _WindPowerG ("Wind Power", Float) = 1
        [Enum(Single Texture,0,Array Random,1,Array By Size,2)] _TextureModeG ("Texture Mode", Float) = 0
        _ArrayCountG ("Array Elements", Float) = 1
        
        [Toggle(_GRASS_B)] _EnableTypeB ("Enable Type B", Float) = 1
        [NoScaleOffset] _GrassTexB ("Grass Texture", 2D) = "white" {}
        _GrassArrayB ("Grass Array", 2DArray) = "" {}
        _ColorLowB ("Color Low", Color) = (1,1,1,1)
        _ColorHighB ("Color High", Color) = (1,1,1,1)
        _BladeHeightB ("Blade Height", Float) = 0.35
        _BladeWidthB  ("Blade Width",  Float) = 0.05
        _SizeWidthImpactB ("Size Width Impact", Range(0,1)) = 0.5
        _SizeRandomB ("Size Randomization", Range(0,1)) = 0.5
        _BendRandomB ("Bend Randomization", Range(0,1)) = 0.5
        _WindPowerB ("Wind Power", Float) = 1
        [Enum(Single Texture,0,Array Random,1,Array By Size,2)] _TextureModeB ("Texture Mode", Float) = 0
        _ArrayCountB ("Array Elements", Float) = 1
        
        _BottomBlending ("Bottom Blending", Range(0,0.5)) = 0.1
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
        [Toggle(_TRAIL)] _EnableTrail ("Enable Trail", Float) = 1
        _TrailBrightness ("Trail Brightness", Float) = 0.5
        _TrailBend ("Trail Bend", Range(0,2)) = 1.25
        [Toggle(_GRASS_ENABLE_SHADOW_PASS)] _ShadowPassDepthWrite ("Depth Write", Float) = 1
        
        _VisibleAmount ("Visible Amount", Range(0,1)) = 1
        [Toggle(_TRIPLE_CROSS)] _EnableTripleCross ("Triple Cross Clusters", Float) = 0
        _EdgeFade ("Edge Fade", Range(0,1)) = 0.25
        _EdgeCulling ("Edge Culling", Range(0,1)) = 0.5
        _EdgeSimplifying ("Edge Simplifying", Range(0,1)) = 0.5
        _EdgeSimplifyingFade ("Edge Simplifying Fade", Range(0,1)) = 0.5
        
        _MaskThreshold ("Mask Threshold", Range(0,1)) = 0.5
        _SizeThreshold ("Size Threshold", Range(0,1)) = 0.1
        
        _WindPower ("Wind Power", Float) = 1
        
        _WindSpeed1 ("Speed", Float) = 1.2
        _WindDir1 ("Direction", Vector) = (1,0,0,0)
        _WindAmp1 ("Amplitude", Float) = 0.08
        _WindFreq1 ("Frequency", Float) = 0.35
        
        _WindSpeed2 ("Speed", Float) = 2.1
        _WindDir2 ("Direction", Vector) = (0.2,0,1,0)
        _WindAmp2 ("Amplitude", Float) = 0.08
        _WindFreq2 ("Frequency", Float) = 0.70
        
        _WindSpeed3 ("Speed", Float) = 3.4
        _WindDir3 ("Direction", Vector) = (-1,0,0.4,0)
        _WindAmp3 ("Amplitude", Float) = 0.08
        _WindFreq3 ("Frequency", Float) = 1.25
        
        _YBias ("Y Bias", Float) = 0.0
        
        [Toggle(_CLOUDS)] _EnableClouds ("Enable Clouds", Float) = 1
        [NoScaleOffset] _CloudsTex ("Clouds Texture", 2D) = "white" {}
        _CloudsScale ("Scale", Float) = 1
        _CloudsDir ("Direction", Vector) = (1,0,0,0)
        _CloudsMasking ("Masking", Range(0,1)) = 0.5
        _CloudsSharpness ("Sharpness", Range(0,1)) = 0.5
        _CloudsDarkness ("Darkness", Range(0,2)) = 0.5
        _CloudsBrightness ("Brightness", Range(0,2)) = 0.5
        
        [Toggle(_SSS)] _EnableSSS ("Enable SSS", Float) = 1
        _SSSColor ("Color", Color) = (1,1,1,1)
        _SSSBrightness ("Brightness", Float) = 0.5
        _SSSRadius ("Radius", Range(0,2)) = 1
        
    }

    SubShader {
        
        Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
        LOD 100

        Pass {
            Tags { "LightMode"="ForwardBase" }
            
            AlphaToMask On
            ZWrite On
            ZTest LEqual
            Cull Off
            Blend Off

            CGPROGRAM
            #pragma target 3.0
            #pragma exclude_renderers gles3 vulkan
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fwdbase
            #pragma shader_feature_local _TRAIL
            #pragma shader_feature_local _SSS
            #pragma shader_feature_local _CLOUDS
            #pragma shader_feature_local _GRASS_R
            #pragma shader_feature_local _GRASS_G
            #pragma shader_feature_local _GRASS_B
            #pragma shader_feature_local _TRIPLE_CROSS
            #pragma shader_feature_local _TEXMODE_R_ARRAY
            #pragma shader_feature_local _TEXMODE_G_ARRAY
            #pragma shader_feature_local _TEXMODE_B_ARRAY
            
            #include "UnityCG.cginc"
            #include "AutoLight.cginc"
            #include "LightVolumes.cginc"
            #include "GrassPassCommon.cginc"
            ENDCG
        }

        Pass {
            Tags { "LightMode"="ForwardAdd" }
            
            AlphaToMask On
            ZWrite Off
            ZTest LEqual
            Cull Off
            Blend One One

            CGPROGRAM
            #pragma target 3.0
            #pragma exclude_renderers gles3 vulkan
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fwdadd_fullshadows
            #pragma shader_feature_local _TRAIL
            #pragma shader_feature_local _GRASS_R
            #pragma shader_feature_local _GRASS_G
            #pragma shader_feature_local _GRASS_B
            #pragma shader_feature_local _TRIPLE_CROSS
            #pragma shader_feature_local _TEXMODE_R_ARRAY
            #pragma shader_feature_local _TEXMODE_G_ARRAY
            #pragma shader_feature_local _TEXMODE_B_ARRAY
            
            #define GRASS_FORWARD_ADD 1
            #include "UnityCG.cginc"
            #include "AutoLight.cginc"
            #include "GrassPassCommon.cginc"
            ENDCG
        }

        Pass {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            
            AlphaToMask On
            ZWrite On
            ColorMask 0
            ZTest LEqual
            Cull Off
            Blend Off

            CGPROGRAM
            #pragma target 3.0
            #pragma exclude_renderers gles3 vulkan
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_shadowcaster
            #pragma shader_feature_local _TRAIL
            #pragma shader_feature_local _GRASS_R
            #pragma shader_feature_local _GRASS_G
            #pragma shader_feature_local _GRASS_B
            #pragma shader_feature_local _TRIPLE_CROSS
            #pragma shader_feature_local _TEXMODE_R_ARRAY
            #pragma shader_feature_local _TEXMODE_G_ARRAY
            #pragma shader_feature_local _TEXMODE_B_ARRAY
            #pragma shader_feature_local _GRASS_ENABLE_SHADOW_PASS
            
            #define GRASS_SHADOW_PASS 1
            #include "UnityCG.cginc"
            #include "GrassPassCommon.cginc"
            ENDCG
        }
    }

    SubShader {
        
        Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
        LOD 100

        Pass {
            Tags { "LightMode"="ForwardBase" }
            
            AlphaToMask Off
            ZWrite On
            ZTest LEqual
            Cull Off
            Blend Off

            CGPROGRAM
            #pragma target 3.0
            #pragma only_renderers gles3 vulkan
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fwdbase
            #pragma shader_feature_local _TRAIL
            #pragma shader_feature_local _SSS
            #pragma shader_feature_local _CLOUDS
            #pragma shader_feature_local _GRASS_R
            #pragma shader_feature_local _GRASS_G
            #pragma shader_feature_local _GRASS_B
            #pragma shader_feature_local _TRIPLE_CROSS
            #pragma shader_feature_local _TEXMODE_R_ARRAY
            #pragma shader_feature_local _TEXMODE_G_ARRAY
            #pragma shader_feature_local _TEXMODE_B_ARRAY
            
            #include "UnityCG.cginc"
            #include "AutoLight.cginc"
            #include "LightVolumes.cginc"
            #include "GrassPassCommon.cginc"
            ENDCG
        }

        Pass {
            Tags { "LightMode"="ForwardAdd" }
            
            AlphaToMask Off
            ZWrite Off
            ZTest LEqual
            Cull Off
            Blend One One

            CGPROGRAM
            #pragma target 3.0
            #pragma only_renderers gles3 vulkan
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fwdadd_fullshadows
            #pragma shader_feature_local _TRAIL
            #pragma shader_feature_local _GRASS_R
            #pragma shader_feature_local _GRASS_G
            #pragma shader_feature_local _GRASS_B
            #pragma shader_feature_local _TRIPLE_CROSS
            #pragma shader_feature_local _TEXMODE_R_ARRAY
            #pragma shader_feature_local _TEXMODE_G_ARRAY
            #pragma shader_feature_local _TEXMODE_B_ARRAY
            
            #define GRASS_FORWARD_ADD 1
            #include "UnityCG.cginc"
            #include "AutoLight.cginc"
            #include "GrassPassCommon.cginc"
            ENDCG
        }

        Pass {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            
            AlphaToMask Off
            ZWrite On
            ColorMask 0
            ZTest LEqual
            Cull Off
            Blend Off

            CGPROGRAM
            #pragma target 3.0
            #pragma only_renderers gles3 vulkan
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_shadowcaster
            #pragma shader_feature_local _TRAIL
            #pragma shader_feature_local _GRASS_R
            #pragma shader_feature_local _GRASS_G
            #pragma shader_feature_local _GRASS_B
            #pragma shader_feature_local _TRIPLE_CROSS
            #pragma shader_feature_local _TEXMODE_R_ARRAY
            #pragma shader_feature_local _TEXMODE_G_ARRAY
            #pragma shader_feature_local _TEXMODE_B_ARRAY
            #pragma shader_feature_local _GRASS_ENABLE_SHADOW_PASS
            
            #define GRASS_SHADOW_PASS 1
            #include "UnityCG.cginc"
            #include "GrassPassCommon.cginc"
            ENDCG
        }
    }
    CustomEditor "GPUInfiniteGrass.GrassShaderGUI"
}
