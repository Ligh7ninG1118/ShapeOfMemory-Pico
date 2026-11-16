// Generated from URP 17.3 BakedLitDepthNormalsPass.hlsl for PicoFinal floating chips (see FloatChips.hlsl). Only the marked lines differ.
#ifndef UNIVERSAL_BAKEDLIT_DEPTH_NORMALS_PASS_INCLUDED
#define UNIVERSAL_BAKEDLIT_DEPTH_NORMALS_PASS_INCLUDED

#include "FloatChips.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#if defined(LOD_FADE_CROSSFADE)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif

struct Attributes
{
    float4 positionOS   : POSITION;
    float2 uv           : TEXCOORD0;
    half3 normalOS      : NORMAL;
    half4 tangentOS     : TANGENT;

    float4 floatColor   : COLOR;       // PicoFinal floating chips
    float2 floatPivotXY : TEXCOORD2;
    float2 floatPivotZ  : TEXCOORD3;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS   : SV_POSITION;
    float2 uv           : TEXCOORD0;
    half3 normalWS      : TEXCOORD1;

    #if defined(_NORMALMAP)
        half4 tangentWS : TEXCOORD3;
    #endif

    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

Varyings DepthNormalsVertex(Attributes input)
{
    UNITY_SETUP_INSTANCE_ID(input);   // PicoFinal: first, so the drift's object matrices are the instance's
    // PicoFinal floating chips
    float3 fcPivot; float3x3 fcRot; float3 fcOffset;
    FloatChipsTransform(input.floatColor, input.floatPivotXY, input.floatPivotZ, fcPivot, fcRot, fcOffset);
    input.positionOS.xyz = mul(fcRot, input.positionOS.xyz - fcPivot) + fcPivot + fcOffset;
    input.positionOS.xyz += FloatDriftOS(input.positionOS.xyz);
    input.normalOS = mul(fcRot, input.normalOS);
    input.tangentOS.xyz = mul(fcRot, input.tangentOS.xyz);
    Varyings output = (Varyings)0;
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
    output.positionCS = vertexInput.positionCS;
    output.uv = TRANSFORM_TEX(input.uv, _BaseMap).xy;

    // normalWS and tangentWS already normalize.
    // this is required to avoid skewing the direction during interpolation
    // also required for per-vertex SH evaluation
    VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
    output.normalWS = half3(normalInput.normalWS);
    #if defined(_NORMALMAP)
        real sign = input.tangentOS.w * GetOddNegativeScale();
        output.tangentWS = half4(normalInput.tangentWS.xyz, sign);
    #endif

    return output;
}

void DepthNormalsFragment(
    Varyings input
    , out half4 outNormalWS : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers : SV_Target1
#endif
)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    half4 texColor = (half4) SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
    half alpha = texColor.a * _BaseColor.a;
    AlphaDiscard(alpha, _Cutoff);

    #ifdef LOD_FADE_CROSSFADE
        LODFadeCrossFade(input.positionCS);
    #endif

    #if defined(_GBUFFER_NORMALS_OCT)
        float3 normalWS = normalize(input.normalWS);
        float2 octNormalWS = PackNormalOctQuadEncode(normalWS);           // values between [-1, +1], must use fp32 on some platforms
        float2 remappedOctNormalWS = saturate(octNormalWS * 0.5 + 0.5);   // values between [ 0,  1]
        half3 packedNormalWS = PackFloat2To888(remappedOctNormalWS);      // values between [ 0,  1]
        outNormalWS = half4(packedNormalWS, 0.0);
    #else
        #if defined(_NORMALMAP)
            half3 normalTS = SampleNormal(input.uv, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap)).xyz;
            half sgn = input.tangentWS.w;      // should be either +1 or -1
            half3 bitangent = sgn * cross(input.normalWS.xyz, input.tangentWS.xyz);
            half3 normalWS = TransformTangentToWorld(normalTS, half3x3(input.tangentWS.xyz, bitangent, input.normalWS));
        #else
            half3 normalWS = input.normalWS;
        #endif

        outNormalWS = half4(NormalizeNormalPerPixel(normalWS), 0.0);
    #endif

    #ifdef _WRITE_RENDERING_LAYERS
        outRenderingLayers = EncodeMeshRenderingLayer();
    #endif
}

#endif
