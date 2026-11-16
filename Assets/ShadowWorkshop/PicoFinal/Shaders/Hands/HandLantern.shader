Shader "ShapeOfMemory/HandLantern"
{
    // The Lantern hand: warm light added over whatever is behind it, brightest where the surface faces the eye (_Core) and
    // dimming to _Edge round the silhouette, with a slow flicker running along the hand's own frame (HandSpace.hlsl). It shades only the
    // front surface (after ShapeOfMemory/HandDepth, ZTest LEqual), so overlapping fingers don't stack.
    // Tips: the index and thumb tips (HandSpace.hlsl) burn aqua, _TipIdle toward _TipGrab as the pinch closes (_Close, set per hand
    // by MemoryHandSkin). The wrist fades out (HandSpace.hlsl). Motes come from MemoryHandSkin (ShapeOfMemory/HandMote).
    Properties
    {
        _Edge("Edge",Color)=(0.55,0.31,0.24,1)
        _Core("Core",Color)=(1,0.89,0.65,1)
        _Base("Base light",Range(0,2))=0.16
        _Gain("Light where it faces the eye",Range(0,4))=0.62
        _CorePower("Core focus",Range(0.5,4))=1.3
        _Flicker("Flicker",Range(0,0.5))=0.1
        _TipIdle("Tip at rest",Color)=(0.37,0.85,0.78,1)
        _TipGrab("Tip pinched",Color)=(0.89,1,0.98,1)
        _TipGain("Tip brightness",Range(0,3))=1.1
        _Close("Pinch closed (0-1)",Range(0,1))=0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Blend One One ZWrite Off ZTest LEqual Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "HandSpace.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Edge, _Core, _TipIdle, _TipGrab;
                half _Base, _Gain, _CorePower, _Flicker, _TipGain, _Close;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 rest : TEXCOORD2;
                half2 mask : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.rest = HandRest(o.positionWS);
                o.mask = HandMasks(o.positionWS);
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 n = normalize(i.normalWS);
                float3 v = normalize(GetCameraPositionWS() - i.positionWS);
                half core = pow(saturate(dot(n, v)), _CorePower);
                half flicker = 1 - _Flicker + _Flicker * sin(_Time.y * 11 + i.rest.y * 140) * sin(_Time.y * 7.3 + i.rest.x * 90);
                half3 c = lerp(_Edge.rgb, _Core.rgb, core) * (_Base + _Gain * core) * flicker;
                half tip = smoothstep(0.5, 1, i.mask.x);
                c = lerp(c, lerp(_TipIdle.rgb, _TipGrab.rgb, _Close) * _TipGain, tip);
                return half4(c * saturate(i.mask.y), 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
