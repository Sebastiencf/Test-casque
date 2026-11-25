// Eau stagnante des mares et de la riziere : surface sombre, rides lentes, faux reflet de la lisiere,
// trainee de lune et lentilles d'eau pres des bords. Opaque et sans texture (peu couteux sur Quest).
Shader "NangTani/Water"
{
    Properties
    {
        _DeepColor ("Couleur de l'eau", Color) = (0.05, 0.09, 0.07, 1)
        _WeedColor ("Lentilles d'eau", Color) = (0.23, 0.32, 0.12, 1)
        _WeedAmount ("Quantite de lentilles d'eau", Range(0, 1)) = 0.95
        _SkyReflect ("Force du reflet du ciel", Range(0, 5)) = 2.6
        _MoonPath ("Trainee de lune", Range(0, 2)) = 0.32
        _Ripples ("Rides", Range(0, 3)) = 1
        _MoonDirection ("Direction de la lune dans le ciel", Vector) = (-0.656, 0.469, 0.591, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Water"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _DeepColor;
                float4 _WeedColor;
                float4 _MoonDirection;
                float _WeedAmount;
                float _SkyReflect;
                float _MoonPath;
                float _Ripples;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 depth : TEXCOORD0;   // profondeur d'eau en metres
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 data : TEXCOORD1;    // profondeur, brouillard
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.data = float2(input.depth.x, ComputeFogFactor(output.positionCS.z));
                return output;
            }

            float Hash(float2 p)
            {
                float3 q = frac(float3(p.xyx) * 0.1031);
                q += dot(q, q.yzx + 33.33);
                return frac((q.x + q.y) * q.z);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1.0, 0.0)), f.x),
                            lerp(Hash(i + float2(0.0, 1.0)), Hash(i + float2(1.0, 1.0)), f.x), f.y);
            }

            float2 Wave(float2 p, float2 dir, float frequency, float speed, float amplitude, float phase)
            {
                return dir * (amplitude * frequency * cos(dot(p, dir) * frequency + _Time.y * speed + phase));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 p = input.positionWS.xz;
                float phase = Noise(p * 0.9) * 6.28;
                float2 slope = Wave(p, float2(0.8, 0.6), 5.1, 0.9, 0.006, phase)
                             + Wave(p, float2(-0.5, 0.87), 8.7, 1.3, 0.0045, phase)
                             + Wave(p, float2(0.2, -0.98), 13.0, 1.7, 0.003, phase);
                slope *= _Ripples;
                float3 normal = normalize(float3(-slope.x, 1.0, -slope.y));
                float3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float3 ray = reflect(-view, normal);

                Light moon = GetMainLight();

                // Reflet : ciel au-dessus d'une lisiere sombre et irreguliere.
                float azimuth = atan2(ray.x, ray.z);
                float treeLine = 0.17 + 0.07 * sin(azimuth * 7.0) + 0.04 * sin(azimuth * 17.0 + 1.3)
                               + 0.025 * sin(azimuth * 41.0);
                float open = smoothstep(-0.03, 0.05, ray.y - treeLine);
                float3 sky = unity_FogColor.rgb * _SkyReflect + moon.color * 0.035;
                float3 reflection = lerp(unity_FogColor.rgb * 0.35, sky, open);
                float fresnel = 0.10 + 0.90 * pow(1.0 - saturate(dot(normal, view)), 4.0);
                float3 color = lerp(_DeepColor.rgb, reflection, saturate(fresnel * 1.8));

                // Trainee de lune, scintillante.
                float delta = azimuth - atan2(_MoonDirection.x, _MoonDirection.z);
                delta = atan2(sin(delta), cos(delta));
                float across = delta / 0.11;
                float along = (ray.y - _MoonDirection.y) / 0.30;
                float path = exp(-across * across - along * along);
                float spark = 0.35 + 0.65 * smoothstep(0.45, 0.8, Noise(p * 7.0 + float2(0.6, -0.4) * _Time.y));
                color += moon.color * (path * spark * _MoonPath);

                // Lentilles d'eau.
                float patches = smoothstep(0.50, 0.68, Noise(p * 0.75 + float2(3.1, 0.0)));
                float shallow = 1.0 - smoothstep(0.03, 0.24, input.data.x);
                float weed = patches * shallow * step(0.48, Noise(p * 16.0)) * _WeedAmount;
                float3 weedLit = _WeedColor.rgb * (SampleSH(float3(0.0, 1.0, 0.0)) + moon.color * 0.6);
                color = lerp(color, weedLit, weed);

                color = MixFog(color, input.data.y);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
