Shader "ShapeOfMemory/HandGhost"
{
    // The ghost of the tracked hand (MemoryGhostHand, MemoryHandSkin): faint, brighter at its outline than its middle, and
    // gone toward the wrist stub's end (HandSpace.hlsl), so it reads as the hand and not as a tube. MemoryHandSkin
    // sets _BaseColor (its alpha is how much the ghost shows).
    Properties
    {
        _BaseColor("Colour", Color) = (0.93, 0.91, 1, 0.3)
        _Rim("Outline share", Range(0, 1)) = 0.65
        _RimPower("Outline sharpness", Range(0.5, 6)) = 2
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+60" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha ZWrite Off ZTest LEqual Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "HandSpace.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Rim, _RimPower;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; float fade : TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.fade = HandMasks(o.positionWS).y;
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 view = normalize(GetWorldSpaceViewDir(i.positionWS));
                half rim = pow(1 - saturate(abs(dot(normalize(i.normalWS), view))), _RimPower);
                half a = _BaseColor.a * lerp(1, rim, _Rim) * saturate(i.fade);
                return half4(_BaseColor.rgb, a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
