#ifndef MOTHUNLIT_INCLUDED

#define MOTHUNLITSHADER

v2f vert(appdata v)
{
    v2f o;
    
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_INITIALIZE_OUTPUT(v2f, o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    
    o.pos = UnityObjectToClipPos(v.vertex);
    o.uv = TRANSFORM_TEX(v.uv, _MainTex);    
    o.color = v.color;
    o.normal = v.normal;
    o.screenPos = 0;
    o.cameraToLightVec = 0;
    o.param = 0;
    o.random = 0;
    return o;
}

fixed4 fragBase(v2f i) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    discard;
    return 0;
}

fixed4 fragStencil(v2f i) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    discard;
    return 0;
}
#endif