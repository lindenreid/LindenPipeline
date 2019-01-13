#ifndef LRP_SIMPLE_LIT
#define LRP_SIMPLE_LIT 

#include "LRP_Core.hlsl"

CBUFFER_START(UnityPerMaterial)
    float4 _Color;
CBUFFER_END

//----------------------------------------------------------------------------------
// STRUCTS
//----------------------------------------------------------------------------------
struct LitVertexIn {
    float4 pos : POSITION;
    float3 normal : NORMAL;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

//----------------------------------------------------------------------------------
struct LitVertexOut {
    float4 posCS : SV_POSITION;
    float3 normalWS : NORMAL;
    float4 posWS : TEXCOORD0;
    float3 vertexLight : TEXCOORD1;
};

//----------------------------------------------------------------------------------
// VERTEX & FRAGMENT FUNCTIONS
//----------------------------------------------------------------------------------
LitVertexOut LitPassVertex (LitVertexIn IN) {
    UNITY_SETUP_INSTANCE_ID(IN);
    LitVertexOut output;

    output.posCS = ObjectToClip(IN.pos);
    output.posWS = ObjectToWorld(IN.pos);
    output.normalWS = normalize(mul((float3x3)UNITY_MATRIX_M, IN.normal));

    output.vertexLight = ApplyDiffuseLowPriLights(output.normalWS, output.posWS);

    return output;
}

//----------------------------------------------------------------------------------
float4 LitPassFragment (LitVertexOut IN) : SV_TARGET {
    float3 diffuseLight = IN.vertexLight;
    diffuseLight += ApplyDiffuseHighPriLights(IN.normalWS, IN.posWS.xyz);
    float3 color = _Color.rgb * diffuseLight;

    return float4(color, 1.0);
    //return float4(IN.normalWS, 1.0); // TEST
}

#endif //LRP_SIMPLE_LIT 