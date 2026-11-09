Shader "ShapeOfMemory/Book Page Shadow"
{
    // The shade a turning page casts on the standing pages (MemoryBook): drawn just in front of their content after it
    // (multiplied), darkening by vertex alpha toward _ShadowColor.
    Properties
    {
        _ShadowColor("Shadow colour",Color)=(.30,.27,.24,1)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+50" "IgnoreProjector"="True" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend DstColor Zero
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _ShadowColor;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; half shade : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                output.shade=input.color.a; return output;
            }
            half4 frag(Varyings input) : SV_Target { return half4(lerp(half3(1,1,1),_ShadowColor.rgb,input.shade),1); }
            ENDHLSL
        }
    }
}
