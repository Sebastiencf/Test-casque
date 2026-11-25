// Herbes, roseaux, riz, lotus et feuilles mortes : cartes decoupees (alpha), visibles des deux cotes,
// qui ondulent legerement. Un seul materiau et un seul atlas.
Shader "NangTani/Foliage"
{
    Properties
    {
        _BaseMap ("Atlas des plantes", 2D) = "white" {}
        _Cutoff ("Seuil de decoupe", Range(0.05, 0.95)) = 0.5
        _Brightness ("Luminosite", Range(0.4, 2.5)) = 1
        _Sway ("Ondulation (m)", Range(0, 0.15)) = 0.03
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Foliage"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float _Cutoff;
                float _Brightness;
                float _Sway;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float2 data : TEXCOORD1;   // poids d'ondulation, ombrage
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float2 data : TEXCOORD2;   // ombrage, brouillard
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                float wave = sin(_Time.y * 1.4 + world.x * 0.7 + world.z * 0.9);
                world.xz += float2(1.0, 0.6) * (wave * _Sway * input.data.x);

                output.positionWS = world;
                output.positionCS = TransformWorldToHClip(world);
                output.uv = input.uv;
                output.data = float2(input.data.y, ComputeFogFactor(output.positionCS.z));
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float4 base = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                clip(base.a - _Cutoff);

                Light moon = GetMainLight();
                float3 light = SampleSH(float3(0.0, 1.0, 0.0)) + moon.color * (0.55 * moon.distanceAttenuation);

                #if defined(_ADDITIONAL_LIGHTS)
                int count = GetAdditionalLightsCount();
                for (int i = 0; i < count; i++)
                {
                    Light extra = GetAdditionalLight(i, input.positionWS);
                    light += extra.color * (extra.distanceAttenuation * 0.7);
                }
                #endif

                float3 color = base.rgb * light * (input.data.x * _Brightness);
                color = MixFog(color, input.data.y);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
