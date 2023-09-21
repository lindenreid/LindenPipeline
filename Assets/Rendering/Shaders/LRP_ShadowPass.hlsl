#ifndef LRP_SHADOWPASS
#define LRP_SHADOWPASS

#include "LRP_Core.hlsl"

struct ShadowVertexIn {
    float4 pos : POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct ShadowVertexOut {
    float4 posCS : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

ShadowVertexOut ShadowPassVertex (ShadowVertexIn IN) {
    UNITY_SETUP_INSTANCE_ID(IN);
    ShadowVertexOut output;

    output.posCS = ObjectToClip(IN.pos);
    // clamp vertices to near plane so that they don't intersect the near plane
    // account for reversed z-value for OpenGL
#if UNITY_REVERSED_Z
    output.posCS.z -= _ShadowBias;
    output.posCS.z = min(output.posCS.z, output.posCS.w * UNITY_NEAR_CLIP_VALUE);
#else 
    output.posCS.z += _ShadowBias;
    output.posCS.z = max(output.posCS.z, output.posCS.w * UNITY_NEAR_CLIP_VALUE);
#endif

    return output;
}

// only renders depth, so don't need to draw anything
float4 ShadowPassFragment (ShadowVertexOut IN) : SV_TARGET {
    return 0;
}


#endif //LRP_SHADOWPASS