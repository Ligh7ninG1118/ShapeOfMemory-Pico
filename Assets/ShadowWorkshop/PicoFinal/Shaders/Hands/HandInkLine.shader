Shader "ShapeOfMemory/HandInkLine"
{
    // The Ink hand's outline: the mesh pushed out along its normals by _Width (metres) and drawn from inside (back
    // faces), so only a band shows round the silhouette and where a finger crosses another. The Ink look draws two: a wide
    // light one, then a narrow dark one within it, so the outline reads on the paper and against the dusk alike.
    // Boil: the line is redrawn _BoilRate times a second, each vertex nudged up to _Boil metres by a pattern of its rest
    // position in the hand's frame (HandSpace.hlsl) and the redraw count, as a pen line drawn again. _Seed keeps two
    // outlines from moving together. The wrist: cut where its fade falls under _FadeCut, so the line never shows inside the
    // fading stub.
    Properties
    {
        _Color("Colour",Color)=(0.17,0.12,0.13,1)
        _Width("Width (m)",Float)=0.0012
        _Boil("Boil (m)",Float)=0.0008
        _BoilRate("Redraws a second",Float)=8
        _Seed("Seed",Float)=0
        _FadeCut("Wrist cut (fade)",Range(0,1))=0.9
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-9" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Cull Front ZWrite On ZTest LEqual
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "HandSpace.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Width, _Boil, _BoilRate, _Seed;
                half _FadeCut;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; half fade : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 p = TransformObjectToWorld(v.positionOS.xyz);
                float3 n = normalize(TransformObjectToWorldNormal(v.normalOS));
                float s = floor(_Time.y * _BoilRate) * 1.37 + _Seed;
                float3 r = HandRest(p);
                float3 boil = float3(sin(dot(r, float3(91, 57, 33)) + s * 1.7), sin(dot(r, float3(41, 77, 63)) + s * 2.3), sin(dot(r, float3(23, 49, 97)) + s * 3.1));
                p += n * _Width + boil * _Boil;
                o.positionCS = TransformWorldToHClip(p);
                o.fade = HandMasks(TransformObjectToWorld(v.positionOS.xyz)).y;
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                clip(i.fade - _FadeCut);
                return half4(_Color.rgb, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
