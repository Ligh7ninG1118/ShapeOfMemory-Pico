Shader "ShapeOfMemory/PageCue"
{
    // A found memory's paper cut-outs on the lit page (MemorySolveMoment): one mesh of quads from one atlas, tinted and
    // faded per vertex (colour.a). uv1 is where the vertex lies on its page (0..1 across and up), so whatever drifts off
    // the page fades out in its last _EdgeFade instead of hanging in the air past the paper. Alpha blended, no depth write;
    // drawn after the opaque walls, so a cut-out set behind the shadow is hidden where the shadow is.
    Properties
    {
        _BaseMap("Atlas", 2D) = "white" {}
        _EdgeFade("Fade at the page's edges (share of the page)", Range(0.001, 0.3)) = 0.06
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha, Zero One
            ZWrite Off ZTest LEqual Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half _EdgeFade;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; float2 page : TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; float2 page : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.color = v.color;
                o.uv = v.uv;
                o.page = v.page;
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half4 c = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * i.color;
                float2 edge = min(i.page, 1 - i.page);
                c.a *= smoothstep(0, _EdgeFade, min(edge.x, edge.y));
                return c;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
