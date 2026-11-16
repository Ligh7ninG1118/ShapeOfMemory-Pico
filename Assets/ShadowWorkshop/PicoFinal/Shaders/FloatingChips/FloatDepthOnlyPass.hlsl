// Generated from URP 17.3 DepthOnlyPass.hlsl for PicoFinal floating chips (see FloatChips.hlsl). Only the marked lines differ.
#ifndef UNIVERSAL_DEPTH_ONLY_PASS_INCLUDED
#define UNIVERSAL_DEPTH_ONLY_PASS_INCLUDED

#include "FloatChips.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#if defined(LOD_FADE_CROSSFADE)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif

struct Attributes
{
    float4 position     : POSITION;
    float2 texcoord     : TEXCOORD0;
    float4 floatColor   : COLOR;       // PicoFinal floating chips
    float2 floatPivotXY : TEXCOORD2;
    float2 floatPivotZ  : TEXCOORD3;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    #if defined(_ALPHATEST_ON)
        float2 uv       : TEXCOORD0;
    #endif
    float4 positionCS   : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

Varyings DepthOnlyVertex(Attributes input)
{
    UNITY_SETUP_INSTANCE_ID(input);   // PicoFinal: first, so the drift's object matrices are the instance's
    // PicoFinal floating chips
    float3 fcPivot; float3x3 fcRot; float3 fcOffset;
    FloatChipsTransform(input.floatColor, input.floatPivotXY, input.floatPivotZ, fcPivot, fcRot, fcOffset);
    input.position.xyz = mul(fcRot, input.position.xyz - fcPivot) + fcPivot + fcOffset;
    input.position.xyz += FloatDriftOS(input.position.xyz);
    Varyings output = (Varyings)0;
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    #if defined(_ALPHATEST_ON)
        output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
    #endif
    output.positionCS = TransformObjectToHClip(input.position.xyz);
    return output;
}

half DepthOnlyFragment(Varyings input) : SV_TARGET
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    #if defined(_ALPHATEST_ON)
        Alpha(SampleAlbedoAlpha(input.uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap)).a, _BaseColor, _Cutoff);
    #endif

    #if defined(LOD_FADE_CROSSFADE)
        LODFadeCrossFade(input.positionCS);
    #endif

    return input.positionCS.z;
}
#endif
