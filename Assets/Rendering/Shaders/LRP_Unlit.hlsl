#ifndef LRP_UNLIT
#define LRP_UNLIT 

#include "LRP_Core.hlsl"

CBUFFER_START(UnityPerMaterial)
    float4 _Color;
CBUFFER_END

struct SimpleVertexIn {
    float4 pos : POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct SimpleVertexOut {
    float4 posCS : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

SimpleVertexOut UnlitPassVertex (SimpleVertexIn IN) {
    UNITY_SETUP_INSTANCE_ID(IN);
    SimpleVertexOut output;

    output.posCS = ObjectToClip(IN.pos);
    return output;
}

float4 UnlitPassFragment (SimpleVertexOut IN) : SV_TARGET {
    return _Color;
}

#endif //LRP_UNLIT 