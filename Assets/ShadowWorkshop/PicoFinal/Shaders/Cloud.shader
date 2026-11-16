Shader "ShapeOfMemory/Cloud"
{
    // Stylised cumulus (meshes from Tools/ShadowArt/build_cloud_kit.py; MemoryClouds places them round the player).
    // Drawn with the opaque geometry, front to back, writing depth, so each pixel is shaded once and the sky behind is
    // never shaded (MemorySky draws after opaque geometry). No texture is read per pixel.
    // Light: a four-colour ramp (shadow, body, lit, hot) over wrapped N.L toward the sky's glow (_MemorySunDir) mixed
    // with the height in the cloud (vertex G), darkened in the folds between puffs (vertex R), plus a silver lining on
    // edges seen against the glow. The normals are the kit's smoothed ones, so light reads as one soft mass.
    // Haze, per vertex: toward the sky's own colour along the view (_MemorySkyTex, as MemorySky turns it, at a blurred
    // mip), with distance and below the cloud base (_CloudBase), so far clouds and the sea's underside melt into the sky.
    // Edges: where the surface turns away from the eye, the colour goes to that same sky colour (_EdgeSoft from
    // _EdgeStart), so outlines against the sky are soft and a coarse mesh's facets don't show along them.
    // Billow: vertices breathe along their normals. Dither: +-half an 8-bit sRGB step, as the sky, against dusk banding.
    // Fade: premultiplied, alpha 1 - _CloudHide (MemoryClouds follows the sky's own fade-in with it, then holds 0), so
    // sky and clouds come up over passthrough together. At alpha 1 the blend leaves exactly the opaque colour; blending
    // costs next to nothing on a tiled GPU and keeps early depth rejection (no discard, no depth from the shader).
    Properties
    {
        _Shadow("Shadow",Color)=(0.22,0.19,0.40,1)
        _Body("Body",Color)=(0.50,0.40,0.63,1)
        _Lit("Lit",Color)=(0.93,0.58,0.63,1)
        _Hot("Hot",Color)=(1.00,0.80,0.62,1)
        _Wrap("Light wrap",Range(0,1))=0.35
        _HeightShare("Height share",Range(0,1))=0.3
        _Occlusion("Fold darkening",Range(0,1))=0.3
        _Rim("Silver lining",Color)=(1.0,0.82,0.58,1)
        _RimPower("Rim power",Range(1,8))=2.5
        _RimGlow("Rim toward the glow",Range(0,4))=3.2
        _RimAll("Rim everywhere",Range(0,1))=0.12
        _ScatterPower("Glow focus",Range(1,32))=5
        _HazeNear("Haze starts (m)",Float)=25
        _HazeFar("Haze full (m)",Float)=400
        _HazeMax("Haze at full",Range(0,1))=0.9
        _HazeMip("Haze blur (mip)",Range(0,8))=5
        _BillowAmp("Billow (mesh units)",Float)=0.12
        _BillowScale("Billow scale (1/mesh units)",Float)=0.35
        _BillowSpeed("Billow speed",Float)=0.25
        _EdgeSoft("Soft edges",Range(0,1))=0.85
        _EdgeStart("Soft edge start (1 - N.V)",Range(0,1))=0.45
        _Dither("Dither (8-bit steps)",Range(0,2))=1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Cull Back ZWrite On ZTest LEqual
            Blend One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

            // Set by MemorySky (the sky and its glow) and MemoryClouds (the base, the fade; unset is 0: shown).
            TEXTURECUBE(_MemorySkyTex); SAMPLER(sampler_MemorySkyTex);
            float _MemorySkyYaw; half4 _MemorySkyTint; float4 _MemorySunDir;
            float _CloudBase, _CloudBaseFade, _CloudHide;

            CBUFFER_START(UnityPerMaterial)
                half4 _Shadow, _Body, _Lit, _Hot, _Rim;
                half _Wrap, _HeightShare, _Occlusion, _RimPower, _RimGlow, _RimAll, _ScatterPower, _EdgeSoft, _EdgeStart;
                float _HazeNear, _HazeFar, _HazeMax, _HazeMip, _BillowAmp, _BillowScale, _BillowSpeed;
                half _Dither;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half2 shape : TEXCOORD2;  // fold openness, height share
                half4 haze : TEXCOORD3;   // haze colour (the sky along the view), amount
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // A world direction to the sky cubemap's own, as MemorySky's dome and SkyboxDithered's _Rotation turn it.
            float3 SkyLookup(float3 d)
            {
                float s, c; sincos(radians(_MemorySkyYaw), s, c);
                return float3(c * d.x + s * d.z, d.y, -s * d.x + c * d.z);
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 p = v.positionOS.xyz;
                float t = _Time.y * _BillowSpeed;
                float3 q = p * _BillowScale;
                float b = sin(q.x * 1.7 + t * 1.3 + sin(q.z * 1.1 - t * 0.7)) * sin(q.y * 2.1 - t * 0.9 + q.x * 0.5) + 0.6 * sin(q.z * 1.9 + t * 1.1 + q.y * 0.8);
                p += v.normalOS * (b * _BillowAmp);
                float3 positionWS = TransformObjectToWorld(p);
                o.positionCS = TransformWorldToHClip(positionWS);
                o.positionWS = positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.shape = v.color.rg;
                float3 toVertex = positionWS - GetCameraPositionWS();
                float distance = length(toVertex);
                half3 sky = SAMPLE_TEXTURECUBE_LOD(_MemorySkyTex, sampler_MemorySkyTex, SkyLookup(toVertex / max(distance, 1e-4)), _HazeMip).rgb * _MemorySkyTint.rgb;
                float far = saturate((distance - _HazeNear) / max(_HazeFar - _HazeNear, 1));
                far = far * (2 - far) * _HazeMax;
                float low = saturate((_CloudBase + _CloudBaseFade - positionWS.y) / max(_CloudBaseFade, 0.01));
                low = low * low * (3 - 2 * low);
                o.haze = half4(sky, max(far, low));
                return o;
            }

            float CloudIgn(float2 pixel) { return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715)))); }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 n = normalize(i.normalWS);
                float3 v = normalize(GetCameraPositionWS() - i.positionWS);
                float3 l = _MemorySunDir.xyz;
                half fold = lerp(1, i.shape.x, _Occlusion);
                half lit = saturate(dot(n, l) * (1 - _Wrap) + _Wrap);
                half t = saturate(lerp(lit, i.shape.y, _HeightShare) * fold);
                half3 c = lerp(_Shadow.rgb, _Body.rgb, smoothstep(0, 0.5, t));
                c = lerp(c, _Lit.rgb, smoothstep(0.45, 0.8, t));
                c = lerp(c, _Hot.rgb, smoothstep(0.78, 1, t));
                half away = 1 - saturate(dot(n, v));
                half edge = pow(away, _RimPower);
                half toward = pow(saturate(dot(-v, l)), _ScatterPower);
                c += _Rim.rgb * edge * (toward * _RimGlow + _RimAll) * fold;
                c = lerp(c, i.haze.rgb, max(i.haze.a, smoothstep(_EdgeStart, 1, away) * _EdgeSoft));
                half dither = (CloudIgn(i.positionCS.xy) - 0.5) * _Dither / 255.0;
                c = SRGBToLinear(saturate(LinearToSRGB(c) + dither));
                half alpha = 1 - saturate(_CloudHide);
                return half4(c * alpha, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
