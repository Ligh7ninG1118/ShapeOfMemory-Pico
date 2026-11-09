Shader "ShapeOfMemory/Book Page"
{
    // The page being turned (MemoryBook). Each side is the paper (tinted like the standing pages) under a print of that
    // page's content, rendered once as the turn starts (premultiplied, transparent where there is only paper). The front
    // carries the right page, the back the left page (u mirrored). Shaded by how the side faces a light from the front
    // and above, scaled so a side lying on a page's wall is exactly as bright as the unlit standing pages; light through
    // the paper brightens a side turned away a little, and the page darkens where it bends sharply (vertex colour red).
    Properties
    {
        _FrontPaper("Front paper",2D)="white"{}
        _BackPaper("Back paper",2D)="white"{}
        _FrontPrint("Front print",2D)="black"{}
        _BackPrint("Back print",2D)="black"{}
        _BaseColor("Paper tint",Color)=(1,1,1,1)
        _LightDir("Light direction (world, toward the light)",Vector)=(0,.8,-.6,0)
        _LightOnPage("Light on a standing page",Float)=.42
        _Ambient("Ambient",Range(0,1))=.55
        _Through("Light through the paper",Range(0,1))=.25
        _Crease("Bend shade",Range(0,1))=.3
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_FrontPaper); SAMPLER(sampler_FrontPaper);
            TEXTURE2D(_BackPaper); SAMPLER(sampler_BackPaper);
            TEXTURE2D(_FrontPrint); SAMPLER(sampler_FrontPrint);
            TEXTURE2D(_BackPrint); SAMPLER(sampler_BackPrint);
            CBUFFER_START(UnityPerMaterial)
                float4 _FrontPaper_ST; float4 _BackPaper_ST; float4 _FrontPrint_ST; float4 _BackPrint_ST;
                half4 _BaseColor; float4 _LightDir; half _LightOnPage, _Ambient, _Through, _Crease;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 normalWS : TEXCOORD1; half crease : TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS=TransformObjectToWorldNormal(input.normalOS);
                output.uv=input.uv; output.crease=input.color.r; return output;
            }
            half4 frag(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                bool front=IS_FRONT_VFACE(face,true,false);
                float2 back=float2(1-input.uv.x,input.uv.y);
                // Both sides sampled, then chosen, so the mip derivatives stay whole.
                half3 frontPaper=SAMPLE_TEXTURE2D(_FrontPaper,sampler_FrontPaper,input.uv).rgb;
                half3 backPaper=SAMPLE_TEXTURE2D(_BackPaper,sampler_BackPaper,back).rgb;
                half4 frontPrint=SAMPLE_TEXTURE2D(_FrontPrint,sampler_FrontPrint,input.uv);
                half4 backPrint=SAMPLE_TEXTURE2D(_BackPrint,sampler_BackPrint,back);
                half3 paper=(front?frontPaper:backPaper)*_BaseColor.rgb;
                half4 print=front?frontPrint:backPrint;
                half3 color=paper*(1-print.a)+print.rgb;
                float3 normal=normalize(input.normalWS)*(front?1:-1);
                half facing=dot(normal,normalize(_LightDir.xyz))/max(_LightOnPage,.05);
                half shade=_Ambient+(1-_Ambient)*saturate(facing)+_Through*saturate(-facing);
                shade=min(shade,1.15)*(1-_Crease*input.crease);
                return half4(color*shade,1);
            }
            ENDHLSL
        }
    }
}
