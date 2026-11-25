// Nappes de brume 2D (CDD : "flat fog or mist layers"). Chaque quad se tourne vers le joueur
// (autour de l'axe vertical seulement), derive lentement et s'epaissit avec le brouillard de la scene.
Shader "NangTani/Mist"
{
    Properties
    {
        _MainTex ("Volutes", 2D) = "white" {}
        _MistColor ("Teinte de la brume", Color) = (0.50, 0.63, 0.80, 1)
        _Tint ("Part de la teinte (0 = couleur du brouillard)", Range(0, 1)) = 0.5
        _Opacity ("Opacite", Range(0, 1)) = 0.38
        _Drift ("Derive (m)", Float) = 0.9
        _Speed ("Vitesse de derive", Float) = 0.09
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Mist"
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
                float4 _MistColor;
                float _Tint;
                float _Opacity;
                float _Drift;
                float _Speed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;   // centre bas de la nappe (identique pour les 4 sommets)
                float2 uv : TEXCOORD0;
                float2 offset : TEXCOORD1;      // decalage du coin, en metres (largeur, hauteur)
                float2 data : TEXCOORD2;        // opacite propre, phase
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 center = TransformObjectToWorld(input.positionOS.xyz);
                float phase = input.data.y;
                center.xz += float2(cos(phase), sin(phase)) * (sin(_Time.y * _Speed + phase * 3.0) * _Drift);

                float3 camera = GetCameraPositionWS();
                float2 toCamera = camera.xz - center.xz;
                float distanceToCamera = length(toCamera);
                float2 dir = toCamera / max(distanceToCamera, 0.001);
                float3 right = float3(-dir.y, 0.0, dir.x);
                float3 world = center + right * input.offset.x + float3(0.0, input.offset.y, 0.0);
                output.positionCS = TransformWorldToHClip(world);
                output.uv = input.uv;

                // Densite du brouillard par rapport au calme (0.03) : la brume s'epaissit avec l'angoisse.
                float density = unity_FogParams.x * 0.8326;   // unity_FogParams.x = densite / sqrt(ln 2)
                float thickness = clamp(density / 0.030, 0.8, 1.8);
                float nearFade = smoothstep(3.0, 9.0, distanceToCamera);   // jamais de nappe au visage
                float farFade = 1.0 - smoothstep(48.0, 66.0, distanceToCamera);
                float breathing = 0.85 + 0.15 * sin(_Time.y * 0.17 + phase * 2.0);
                float alpha = _Opacity * input.data.x * thickness * nearFade * farFade * breathing;

                float f = density * distanceToCamera;
                float visibility = saturate(exp(-f * f) + 0.45);   // reste lisible au loin
                float3 mist = lerp(unity_FogColor.rgb, _MistColor.rgb, _Tint * visibility);
                output.color = float4(mist, alpha);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half wisps = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a;
                return half4(input.color.rgb, saturate(wisps * input.color.a));
            }
            ENDHLSL
        }
    }
}
