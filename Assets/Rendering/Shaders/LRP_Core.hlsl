#ifndef LRP_CORE
#define LRP_CORE 

// POSITION NAMES
// CS : clip space
// VS : view space
// WS : world space
// OS : object space

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

//----------------------------------------------------------------------------------
// GLOBAL SHADER VARIABLES
//----------------------------------------------------------------------------------

CBUFFER_START(UnityPerDraw)
    float4x4 unity_ObjectToWorld;
    float4 unity_LightIndicesOffsetAndCount;
    float4 unity_4LightIndices0, unity_4LightIndices1;
CBUFFER_END

CBUFFER_START(UnityPerFrame)
    float4x4 unity_MatrixVP;
CBUFFER_END

#define MAX_VISIBLE_LIGHTS 4

CBUFFER_START(_LightBuffer)
    float4 _VisibleLightColors[MAX_VISIBLE_LIGHTS];
    float4 _VisibleLightVectors[MAX_VISIBLE_LIGHTS];
    float4 _VisibleLightAttenuations[MAX_VISIBLE_LIGHTS];
    float4 _VisibleLightSpotDirections[MAX_VISIBLE_LIGHTS];
CBUFFER_END

#define UNITY_MATRIX_M unity_ObjectToWorld
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"

//----------------------------------------------------------------------------------
// SPACE TRANSFORMATIONS
//----------------------------------------------------------------------------------
float4 ObjectToWorld (float4 posOS) {
    return mul(UNITY_MATRIX_M, float4(posOS.xyz, 1.0));
}

//----------------------------------------------------------------------------------
float4 ObjectToClip (float4 posOS) {
    float4 posWS = ObjectToWorld(posOS);
    return mul(unity_MatrixVP, posWS);
}

//----------------------------------------------------------------------------------
// LIGHTING CALCULATIONS
//----------------------------------------------------------------------------------
float3 DiffuseLight (int i, float3 normalWS, float3 posWS) {
    float3 lightColor = _VisibleLightColors[i].rgb;
    float4 lightVec = _VisibleLightVectors[i];
    float4 lightAttenuation = _VisibleLightAttenuations[i];
    float3 spotDir = _VisibleLightSpotDirections[i].xyz;

    // get direction from obj world position to light
    // lightVec.w is 0 for directional lights, in which case lightVec
    //    is already a ws direction from obj to light
    lightVec = float4(lightVec.xyz - posWS * lightVec.w, 1.0);
    float3 lightDir = normalize(lightVec.xyz);

    // dot product tells us how bright the light contribution is at this point
    float d = saturate(dot(normalWS, lightDir));

    // attenuation tells us about the light range
    float lightVecDot = dot(lightVec, lightVec);
    float rangeFade = lightVecDot * lightAttenuation.x;
    rangeFade = saturate(1.0 - rangeFade * rangeFade);
    rangeFade *= rangeFade;

    // extra attenuation info for spot lights
    float spotFade = dot(spotDir, lightDir);
    spotFade = saturate(spotFade * lightAttenuation.z + lightAttenuation.w);
    spotFade *= spotFade;

    // distance squared calc tells us the contribution based on how 
    //    far away the light is
    float distanceSqr = max(lightVecDot, 0.00001);
    d *= spotFade * rangeFade / distanceSqr;

    return d * lightColor;
}

//----------------------------------------------------------------------------------
float3 ApplyDiffuseLights (float3 normalWS, float3 posWS) {
    float3 diffuse = float3(0,0,0);

    // only apply number of lights actually affecting this obj
    for(int i = 0; i < unity_LightIndicesOffsetAndCount.y; i++) {
        int lightIndex = unity_4LightIndices0[i];
        diffuse += DiffuseLight(lightIndex, normalWS, posWS);
    }

    return diffuse;
}

#endif //LRP_CORE