Shader "ShadowWorkshop/Projected Slide"
{
    // A torch-2 lit wall: the lamp's light (_BaseMap, drawn as URP Unlit draws it) tinted by the slide the lamp projects
    // (Torch2Cue). The slide is blurred by reading coarser mips (blended between two levels, four taps each, so any
    // imported texture with mipmaps works), desaturated, and kept within [_SlideFloor, 1] so the pile's shadow, the same
    // wall at 10% light, stays the darkest thing on it.
    Properties
    {
        _BaseMap("Lamp light",2D)="white"{}
        _BaseColor("Colour",Color)=(1,1,1,1)
        _Slide("Slide",2D)="white"{}
        _SlideRect("Slide crop (scale, offset)",Vector)=(1,1,0,0)
        _SlideBlur("Blur (fraction of the slide width)",Range(0,.2))=0
        _SlideSaturation("Saturation",Range(0,1))=1
        _SlideMix("Slide strength",Range(0,1))=0
        _SlideFloor("Darkest slide value",Range(0,1))=.55
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull",Float)=0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_Slide); SAMPLER(sampler_Slide);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST; half4 _BaseColor; float4 _Slide_TexelSize; float4 _SlideRect;
                half _SlideBlur, _SlideSaturation, _SlideMix, _SlideFloor, _Cull;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                output.uv=input.uv; return output;
            }
            // Four taps half a texel of that mip apart smooth the blockiness of a magnified coarse mip.
            half3 Taps(float2 uv,float lod)
            {
                float2 d=_Slide_TexelSize.xy*exp2(lod)*.5;
                return (SAMPLE_TEXTURE2D_LOD(_Slide,sampler_Slide,uv+float2(-d.x,-d.y),lod).rgb+SAMPLE_TEXTURE2D_LOD(_Slide,sampler_Slide,uv+float2(d.x,-d.y),lod).rgb
                       +SAMPLE_TEXTURE2D_LOD(_Slide,sampler_Slide,uv+float2(-d.x,d.y),lod).rgb+SAMPLE_TEXTURE2D_LOD(_Slide,sampler_Slide,uv+float2(d.x,d.y),lod).rgb)*.25;
            }
            half4 frag(Varyings input) : SV_Target
            {
                half4 light=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv)*_BaseColor;
                if(_SlideMix<=0) return half4(light.rgb,1);
                float2 uv=input.uv*_SlideRect.xy+_SlideRect.zw;
                float lod=log2(max(1.0,_SlideBlur*_Slide_TexelSize.z));
                half3 slide;
                if(lod<=0) slide=SAMPLE_TEXTURE2D_LOD(_Slide,sampler_Slide,uv,0).rgb;
                else { float low=floor(lod); slide=lerp(Taps(uv,low),Taps(uv,low+1),lod-low); }
                half grey=dot(slide,half3(.299,.587,.114));
                slide=lerp(grey.xxx,slide,_SlideSaturation);
                slide=_SlideFloor+(1-_SlideFloor)*slide;
                return half4(light.rgb*lerp(half3(1,1,1),slide,_SlideMix),1);
            }
            ENDHLSL
        }
    }
}
