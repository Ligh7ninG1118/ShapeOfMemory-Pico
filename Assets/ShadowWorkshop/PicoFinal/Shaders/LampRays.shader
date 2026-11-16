Shader "ShapeOfMemory/LampRays"
{
    // A few soft cards of light leaving a lamp's lens toward the pile (MemoryLampOrb): added, no texture. uv.x runs across
    // a card (its sides fade out), uv.y along it from the lens (0) to its end (1). Slow streaks drift along the light; a
    // card seen edge-on fades away, so no card ever shows as a line. Alpha out is 0: nothing in the eye buffer's alpha changes.
    Properties
    {
        _Color("Colour", Color) = (1, 0.906, 0.722, 1)
        _Intensity("Brightness (MemoryLampOrb sets it from the lamp's level)", Range(0, 2)) = 1
        _Strength("Strength", Range(0, 1)) = 0.16
        _Streaks("Streak contrast", Range(0, 1)) = 0.55
        _Drift("Streak drift (per second)", Range(0, 1)) = 0.12
        _EdgeOn("Fade when seen edge-on (|N.V| from x to y)", Vector) = (0.12, 0.55, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+50" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Pass
        {
            Blend One One ZWrite Off ZTest LEqual Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensity, _Strength, _Streaks, _Drift;
                half4 _EdgeOn;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 normalWS : TEXCOORD1; float3 positionWS : TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv;
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 view = normalize(GetWorldSpaceViewDir(i.positionWS));
                half facing = smoothstep(_EdgeOn.x, _EdgeOn.y, abs(dot(normalize(i.normalWS), view)));
                float x = i.uv.x, y = i.uv.y;
                half across = sin(saturate(x) * PI);
                across *= across;
                half along = pow(saturate(1 - y), 1.6) * smoothstep(0.0, 0.1, y);
                float t = _Time.y * _Drift;
                half s1 = sin(x * 23.0 + y * 1.7 - t * 6.0) * 0.5 + 0.5;
                half s2 = sin(x * 61.0 - y * 2.3 + t * 3.7) * 0.5 + 0.5;
                half streak = lerp(1.0, 0.35 + 1.3 * s1 * s2, _Streaks);
                half a = across * along * streak * facing * _Strength * _Intensity;
                return half4(_Color.rgb * a, 0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
