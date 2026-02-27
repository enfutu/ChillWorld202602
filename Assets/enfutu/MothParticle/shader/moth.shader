Shader "enfutu/moth"
{
    Properties
    {
        //Color
        _Color ("AddColor", Color) = (1,1,1,1)
        _RandomColor ("AddColorRandomize", Range(0, 1)) = 0
        _MainTex ("Texture", 2D) = "white" {}
        _GradMin ("(Gloss)Gradiant_Min", Color) = (1,1,1,1)
        _GradMax ("(Gloss)Gradiant_Max", Color) = (1,1,1,1)
        _RandomGloss ("(Gloss)ColorRandomize", Range(0, 1)) = 0
        _Boost ("(Gloss)Gradient_Boost", float) = 1
        _MaskPower ("(Gloss)Contrast", float) = 1
        _LightValueMin ("LowerBrightnessLimit", float) = .1
        //Transform
        _ParticleSize ("(Particle)Size", float) = .05
        _OrbitalRange ("(Orbital)Size", float) = 1
        _Flap ("WingFlap", Range(0,1)) = 1
        _WingSpeed ("WingSpeed", float) = 1
        _LightHeight ("LightHeight Offset", float) = 0

        //Mode
        [Toggle]_EnablePhototaxis ("Enable Phototaxis", int) = 0
        [Toggle]_FreeMothMode("Enable FreeMothMode", int) = 0
        [KeywordEnum(FALSE, TRUE)]_UseFakeShadow("UseFakeShadow", int) = 0

        //Extensions
        _Pos0 ("p0", Vector) = (0,0,0,0)
        _Pos1 ("p1", Vector) = (0,0,0,0)
        _Pos2 ("p2", Vector) = (0,0,0,0)
        _Pos3 ("p3", Vector) = (0,0,0,0)
        _Pos4 ("p4", Vector) = (0,0,0,0)
        _Pos5 ("p5", Vector) = (0,0,0,0)
        _Pos6 ("p6", Vector) = (0,0,0,0)
        _Pos7 ("p7", Vector) = (0,0,0,0)
        _Col0 ("c0", Color) =(1,1,1,1)
        _Col1 ("c1", Color) =(1,1,1,1)
        _Col2 ("c2", Color) =(1,1,1,1)
        _Col3 ("c3", Color) =(1,1,1,1)
        _Col4 ("c4", Color) =(1,1,1,1)
        _Col5 ("c5", Color) =(1,1,1,1)
        _Col6 ("c6", Color) =(1,1,1,1)
        _Col7 ("c7", Color) =(1,1,1,1)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry-1" }
        Cull Off
        Pass
        {
            Tags { "LightMode"="Vertex" }
            Stencil
            {
                Ref 100
                Comp Always
                Pass Replace
            }
       
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment fragBase

            #include "UnityCG.cginc"
            #include "mothUtility.cginc"
            #include "mothBase.cginc"

            ENDCG
        }

        Pass
        {
            Tags { "LightMode"="Vertex" }
            ZWrite Off      //このpassの結果は深度テクスチャへ書き込まない
            ZTest Greater
            Blend DstColor Zero

            Stencil
            {
                Ref 101
                Comp Greater
                Pass IncrSat
            }

            CGPROGRAM
            #pragma shader_feature _USEFAKESHADOW_FALSE _USEFAKESHADOW_TRUE
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment fragStencil

            #include "UnityCG.cginc"
            #include "mothUtility.cginc"
            #include "mothBase.cginc"

            ENDCG
        }

    //}

    //本シェーダーはRenderQueueがおかしいのでSubShaderを分ける
    //SubShader
    //{
    //    Tags { "Queue" = "Geometry" "RenderType" = "Opaque" }
        
        Pass
        {
            //cameraの深度テクスチャを生成するためにはこのpassが必要そう
            //影は使わないので頂点移動とalphaClipの記述だけ行う
            Tags{ "LightMode"="ShadowCaster" }
            
            CGPROGRAM
            #pragma target 3.0
            
            #pragma vertex vert
            #pragma fragment fragShadow
            #pragma multi_compile_shadowcaster

            #include "UnityCG.cginc"
            #include "mothUtility.cginc"
            #include "mothBase.cginc"

            ENDCG          
        }
    }
    Fallback "Diffuse"
    CustomEditor "MothEditor"
}