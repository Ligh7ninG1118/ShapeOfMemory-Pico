Shader "ShapeOfMemory/HandInkWash"
{
    // The Ink hand's body: a see-through paper wash (_Wash at _WashAlpha), ink (_Ink) where the surface turns away from the
    // eye, and hatching on the side away from the key light, crossed again where it is darkest. The hatching runs through
    // the hand's own frame (HandSpace.hlsl), so it stays on the skin as the fingers bend. It shades only the front surface (after
    // ShapeOfMemory/HandDepth, ZTest LEqual).
    // Tips: the index and thumb tips (HandSpace.hlsl) go aqua, _TipIdle toward _TipGrab as the pinch closes (_Close, set per hand by
    // MemoryHandSkin), as everything a hand presses is aqua. The wrist fades out (HandSpace.hlsl).
    Properties
    {
        _Wash("Wash",Color)=(0.96,0.91,0.82,1)
        _WashAlpha("Wash opacity",Range(0,1))=0.3
        _Ink("Ink",Color)=(0.17,0.12,0.13,1)
        _InkAlpha("Ink opacity",Range(0,1))=0.92
        _EdgeStart("Edge ink from (N.V)",Range(0,1))=0.08
        _EdgeEnd("Edge ink to (N.V)",Range(0,1))=0.26
        _HatchSpacing("Hatch spacing (m)",Float)=0.0045
        _HatchWidth("Hatch width (share)",Range(0,1))=0.22
        _HatchBelow("Hatch below (wrapped N.L)",Range(0,1))=0.46
        _CrossBelow("Cross-hatch below",Range(0,1))=0.28
        _TipIdle("Tip at rest",Color)=(0.37,0.85,0.78,1)
        _TipGrab("Tip pinched",Color)=(0.89,1,0.98,1)
        _Close("Pinch closed (0-1)",Range(0,1))=0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha ZWrite Off ZTest LEqual Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "HandSpace.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Wash, _Ink, _TipIdle, _TipGrab;
                half _WashAlpha, _InkAlpha, _EdgeStart, _EdgeEnd, _HatchWidth, _HatchBelow, _CrossBelow, _Close;
                float _HatchSpacing;
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
            // One hatch family: lines across direction d, every _HatchSpacing metres, antialiased by their own rate of change.
            half Hatch(float3 rest, float3 d)
            {
                float t = dot(rest, d) / _HatchSpacing;
                float h = abs(frac(t) - 0.5) * 2;           // 0 on a line's middle, 1 between lines
                float aa = max(fwidth(t) * 2, 1e-4);
                return 1 - smoothstep(_HatchWidth - aa, _HatchWidth + aa, h);
            }
            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 n = normalize(i.normalWS);
                float3 v = normalize(GetCameraPositionWS() - i.positionWS);
                half ndv = saturate(dot(n, v));
                half lam = dot(n, GetMainLight().direction) * 0.5 + 0.5;
                half hatch = Hatch(i.rest, normalize(float3(0.80, 0.55, 0.27))) * smoothstep(_HatchBelow, _HatchBelow - 0.16, lam)
                           + Hatch(i.rest, normalize(float3(-0.45, 0.75, 0.35))) * smoothstep(_CrossBelow, _CrossBelow - 0.14, lam);
                half edge = 1 - smoothstep(_EdgeStart, _EdgeEnd, ndv);
                half ink = saturate(max(edge, hatch));
                half3 c = lerp(_Wash.rgb, _Ink.rgb, ink);
                half a = max(_WashAlpha, ink * _InkAlpha);
                half tip = smoothstep(0.5, 1, i.mask.x);
                c = lerp(c, lerp(_TipIdle.rgb, _TipGrab.rgb, _Close), tip);
                a = max(a, tip);
                return half4(c, a * saturate(i.mask.y));
            }
            ENDHLSL
        }
    }
    Fallback Off
}
