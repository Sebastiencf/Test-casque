// Lune dans le ciel : toujours a la meme direction quel que soit le deplacement du joueur
// (comme a l'infini), derriere tout le decor, avec un halo qui grandit quand la brume monte.
Shader "NangTani/Moon"
{
    Properties
    {
        _MainTex ("Lune et halo", 2D) = "white" {}
        _Color ("Teinte", Color) = (1, 1, 1, 1)
        _Direction ("Direction (monde)", Vector) = (-0.656, 0.469, 0.591, 0)
        _Size ("Demi-largeur du halo a 100 m", Float) = 15
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-100" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Moon"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _Direction;
                float _Size;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;   // coins (-1..1)
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float veil : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 dir = normalize(_Direction.xyz);
                float3 right = normalize(cross(float3(0.0, 1.0, 0.0), dir));
                float3 up = cross(dir, right);
                float3 world = GetCameraPositionWS() + dir * 100.0 +
                               (right * input.positionOS.x + up * input.positionOS.y) * _Size;
                output.positionCS = TransformWorldToHClip(world);

                // Colle au plan lointain : derriere tous les objets, sans etre coupe par la distance d'affichage.
                #if UNITY_REVERSED_Z
                    output.positionCS.z = output.positionCS.w * 0.000001;
                #else
                    output.positionCS.z = output.positionCS.w * 0.999999;
                #endif

                output.uv = input.uv;
                output.veil = saturate((unity_FogParams.x * 0.8326 - 0.030) / 0.045);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _Color;
                c.rgb *= 1.0 - 0.25 * input.veil;          // lune voilee
                if (c.a < 0.95) c.a = saturate(c.a * (1.0 + 0.5 * input.veil));   // halo plus large
                return c;
            }
            ENDHLSL
        }
    }
}
