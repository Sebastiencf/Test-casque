// Sol marecageux : quatre matieres (herbe, terre, boue, feuilles) melangees selon les couleurs des sommets.
// L'alpha de chaque texture sert de relief : les transitions suivent les brins, les cailloux et les feuilles.
// Les zones mouillees sont plus sombres et accrochent la lumiere de la lune.
Shader "NangTani/Ground"
{
    Properties
    {
        _GrassMap ("Herbe", 2D) = "gray" {}
        _DirtMap ("Terre", 2D) = "gray" {}
        _MudMap ("Boue", 2D) = "gray" {}
        _LitterMap ("Feuilles mortes", 2D) = "gray" {}
        _Tiles ("Taille d'une tuile en m (herbe, terre, boue, feuilles)", Vector) = (1.7, 2.3, 2.1, 1.8)
        _Brightness ("Luminosite", Range(0.4, 2.5)) = 1
        _WetGloss ("Reflets du sol mouille", Range(0, 3)) = 1
        _MoonDirection ("Direction de la lune dans le ciel", Vector) = (-0.656, 0.469, 0.591, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Ground"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_GrassMap);
            TEXTURE2D(_DirtMap);
            TEXTURE2D(_MudMap);
            TEXTURE2D(_LitterMap);
            SAMPLER(sampler_GrassMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _Tiles;
                float4 _MoonDirection;
                float _Brightness;
                float _WetGloss;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 splat : COLOR;        // herbe, terre, boue, feuilles
                float2 surface : TEXCOORD0;  // humidite, ombre de contact
                float2 variation : TEXCOORD1; // variation de teinte
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 splat : TEXCOORD2;
                float4 surface : TEXCOORD3;  // humidite, ombre, teinte, brouillard
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.splat = input.splat;
                output.surface = float4(input.surface.xy, input.variation.x, ComputeFogFactor(output.positionCS.z));
                return output;
            }

            float2 Turned(float2 uv)
            {
                return float2(uv.x * 0.8 - uv.y * 0.6, uv.x * 0.6 + uv.y * 0.8);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 p = input.positionWS.xz;
                float4 grass = SAMPLE_TEXTURE2D(_GrassMap, sampler_GrassMap, p / _Tiles.x);
                float4 dirt = SAMPLE_TEXTURE2D(_DirtMap, sampler_GrassMap, Turned(p / _Tiles.y));
                float4 mud = SAMPLE_TEXTURE2D(_MudMap, sampler_GrassMap, p / _Tiles.z);
                float4 litter = SAMPLE_TEXTURE2D(_LitterMap, sampler_GrassMap, Turned(p / _Tiles.w));

                // Melange selon le relief des textures.
                float4 weight = input.splat;
                float4 present = step(0.004, weight);
                float4 relief = float4(grass.a, dirt.a, mud.a, litter.a);
                float4 score = (weight * 2.2 + relief) * present;
                float top = max(max(score.x, score.y), max(score.z, score.w));
                float4 blend = max(score - (top - 0.28), 0.0) * present;
                blend /= max(dot(blend, 1.0), 0.0001);

                float3 albedo = grass.rgb * blend.x + dirt.rgb * blend.y + mud.rgb * blend.z + litter.rgb * blend.w;
                float bump = dot(relief, blend);
                float wet = input.surface.x;
                albedo *= (0.72 + 0.5 * input.surface.z) * (0.82 + 0.36 * bump) * input.surface.y * _Brightness;
                albedo *= 1.0 - 0.45 * wet;

                float3 normal = normalize(input.normalWS);
                Light moon = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float3 moonColor = moon.color * moon.distanceAttenuation;
                float3 light = SampleSH(normal) +
                               moonColor * (saturate(dot(normal, moon.direction)) * moon.shadowAttenuation);

                #if defined(_ADDITIONAL_LIGHTS)
                int count = GetAdditionalLightsCount();
                for (int i = 0; i < count; i++)
                {
                    Light extra = GetAdditionalLight(i, input.positionWS);
                    light += extra.color * (extra.distanceAttenuation * saturate(dot(normal, extra.direction)));
                }
                #endif

                float3 color = albedo * light;

                // Boue et flaques : reflet de la lune.
                float3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float3 halfway = normalize(_MoonDirection.xyz + view);
                float gloss = wet * wet * saturate(1.15 - 1.5 * bump) * _WetGloss;
                color += moonColor * (gloss * pow(saturate(dot(normal, halfway)), 90.0) * 0.30);
                color += unity_FogColor.rgb * (gloss * pow(1.0 - saturate(dot(normal, view)), 4.0));

                color = MixFog(color, input.surface.w);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
