Shader "ShapeOfMemory/Skybox Cubemap (Dithered)"
{
    // A cubemap skybox for baked skies (e.g. the COZY magic-hour bake). Same controls as Skybox/Cubemap, plus a dither:
    // dusk gradients band in an 8-bit sRGB eye buffer, so the output gets +-half a step of noise in sRGB code values
    // (a linear-space offset would be ~13 steps near black). Cost: one cube sample and a few ALU per sky pixel.
    // _Alpha fades it in over a clear eye buffer (premultiplied: passthrough shows through until it is 1); at 1 it is
    // the opaque sky, as a skybox material or on a dome (MemorySky).
    Properties
    {
        [NoScaleOffset] _Tex("Cubemap",Cube)="grey"{}
        _Tint("Tint",Color)=(1,1,1,1)
        _Exposure("Exposure",Range(0,4))=1
        _Rotation("Rotation",Range(0,360))=0
        _Dither("Dither (8-bit steps)",Range(0,2))=1
        _Alpha("Alpha",Range(0,1))=1
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Cull Off ZWrite Off
            Blend One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            TEXTURECUBE(_Tex); SAMPLER(sampler_Tex);
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint; half _Exposure; float _Rotation; half _Dither; half _Alpha;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                // Rotate the lookup by -_Rotation so the sky turns the same way as Skybox/Cubemap's _Rotation.
                float s, c; sincos(radians(_Rotation), s, c);
                float3 p = v.positionOS.xyz;
                o.dir = float3(c * p.x + s * p.z, p.y, -s * p.x + c * p.z);
                o.positionCS = TransformObjectToHClip(p);
                return o;
            }

            float SkyIgn(float2 pixel) { return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715)))); }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half3 col = SAMPLE_TEXTURECUBE(_Tex, sampler_Tex, i.dir).rgb * _Tint.rgb * _Exposure;
                half n = (SkyIgn(i.positionCS.xy) - 0.5) * _Dither / 255.0;
                col = SRGBToLinear(saturate(LinearToSRGB(col) + n));
                return half4(col * _Alpha, _Alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
