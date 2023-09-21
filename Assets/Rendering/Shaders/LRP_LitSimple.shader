Shader "Linden RP/Simple Lit"
{
    Properties {
        _Color ("Color", Color) = (1, 1, 1, 1)
    }

    SubShader {
        // Regular lighting pass
        Pass {
            HLSLPROGRAM

            #pragma target 3.5
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling

            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment

            #include "LRP_LitSimple.hlsl"

            ENDHLSL
        }

        // Shadow casting pass
        Pass {
            Tags {
                "LightMode" = "ShadowCaster"
            }

            HLSLPROGRAM

            #pragma target 3.5
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling

            #pragma multi_compile _ _SHADOWS_SOFT

            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "LRP_ShadowPass.hlsl"

            ENDHLSL
        }
    }
}
