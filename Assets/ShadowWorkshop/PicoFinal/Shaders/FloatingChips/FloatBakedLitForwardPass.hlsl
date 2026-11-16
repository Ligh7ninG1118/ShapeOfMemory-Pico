// Generated from URP 17.3 BakedLitForwardPass.hlsl for PicoFinal floating chips (see FloatChips.hlsl). Only the marked lines differ.

#include "FloatChips.hlsl"
#include "FloatHaze.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#if defined(LOD_FADE_CROSSFADE)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif

struct Attributes
{
    float4 positionOS       : POSITION;
    float2 uv               : TEXCOORD0;
    float2 staticLightmapUV : TEXCOORD1;
    float3 normalOS         : NORMAL;
    float4 tangentOS        : TANGENT;

    float4 floatColor   : COLOR;       // PicoFinal floating chips
    float2 floatPivotXY : TEXCOORD2;
    float2 floatPivotZ  : TEXCOORD3;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float3 uv0AndFogCoord : TEXCOORD0; // xy: uv0, z: fogCoord
    DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 1);
    half3 normalWS : TEXCOORD2;

    #if defined(_NORMALMAP)
    half4 tangentWS : TEXCOORD3;
    #endif

    #if defined(DEBUG_DISPLAY) || (defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2))
    float3 positionWS : TEXCOORD4;
    float3 viewDirWS : TEXCOORD5;
    #endif

    #ifdef USE_APV_PROBE_OCCLUSION
    float4 probeOcclusion : TEXCOORD6;
    #endif

    float3 hazePosWS : TEXCOORD7;   // PicoFinal distance haze
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

void InitializeInputData(Varyings input, half3 normalTS, out InputData inputData)
{
    inputData = (InputData)0;

    #if defined(DEBUG_DISPLAY)
    inputData.positionWS = input.positionWS;
    inputData.positionCS = input.positionCS;
    inputData.viewDirectionWS = input.viewDirWS;
    #else
    inputData.positionWS = float3(0, 0, 0);
    inputData.viewDirectionWS = half3(0, 0, 1);
    #endif

    #if defined(_NORMALMAP)
    float sgn = input.tangentWS.w;      // should be either +1 or -1
    float3 bitangent = sgn * cross(input.normalWS.xyz, input.tangentWS.xyz);

    inputData.tangentToWorld = half3x3(input.tangentWS.xyz, bitangent.xyz, input.normalWS.xyz);
    inputData.normalWS = TransformTangentToWorld(normalTS, inputData.tangentToWorld);
    #else
    inputData.normalWS = input.normalWS;
    #endif

    inputData.shadowCoord = float4(0, 0, 0, 0);
    inputData.fogCoord = input.uv0AndFogCoord.z;
    inputData.vertexLighting = half3(0, 0, 0);
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
    inputData.shadowMask = half4(1, 1, 1, 1);

    #if defined(DEBUG_DISPLAY)
    #if defined(LIGHTMAP_ON)
    inputData.staticLightmapUV = input.staticLightmapUV;
    #else
    inputData.vertexSH = input.vertexSH;
    #endif
    #if defined(USE_APV_PROBE_OCCLUSION)
    inputData.probeOcclusion = input.probeOcclusion;
    #endif
    #endif
}

void InitializeBakedGIData(Varyings input, inout InputData inputData)
{
#if !defined(LIGHTMAP_ON) && (defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2))
    inputData.bakedGI = SAMPLE_GI(input.vertexSH,
        GetAbsolutePositionWS(input.positionWS),
        inputData.normalWS,
        input.viewDirWS,
        input.positionCS.xy,
        input.probeOcclusion,
        inputData.shadowMask);
#else
    inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.vertexSH, inputData.normalWS);
#endif
}

Varyings BakedLitForwardPassVertex(Attributes input)
{
    UNITY_SETUP_INSTANCE_ID(input);   // PicoFinal: first, so the drift's object matrices are the instance's
    // PicoFinal floating chips
    float3 fcPivot; float3x3 fcRot; float3 fcOffset;
    FloatChipsTransform(input.floatColor, input.floatPivotXY, input.floatPivotZ, fcPivot, fcRot, fcOffset);
    input.positionOS.xyz = mul(fcRot, input.positionOS.xyz - fcPivot) + fcPivot + fcOffset;
    input.positionOS.xyz += FloatDriftOS(input.positionOS.xyz);
    input.normalOS = mul(fcRot, input.normalOS);
    input.tangentOS.xyz = mul(fcRot, input.tangentOS.xyz);
    Varyings output;

    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
    output.positionCS = vertexInput.positionCS;
    output.hazePosWS = vertexInput.positionWS;   // PicoFinal distance haze
    output.uv0AndFogCoord.xy = TRANSFORM_TEX(input.uv, _BaseMap);
    #if defined(_FOG_FRAGMENT)
    output.uv0AndFogCoord.z = vertexInput.positionVS.z;
    #else
    output.uv0AndFogCoord.z = ComputeFogFactor(vertexInput.positionCS.z);
    #endif

    // normalWS and tangentWS already normalize.
    // this is required to avoid skewing the direction during interpolation
    // also required for per-vertex SH evaluation
    VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
    output.normalWS = normalInput.normalWS;
    #if defined(_NORMALMAP)
    real sign = input.tangentOS.w * GetOddNegativeScale();
    output.tangentWS = half4(normalInput.tangentWS.xyz, sign);
    #endif
    OUTPUT_LIGHTMAP_UV(input.staticLightmapUV, unity_LightmapST, output.staticLightmapUV);
    OUTPUT_SH4(vertexInput.positionWS, output.normalWS.xyz, GetWorldSpaceNormalizeViewDir(vertexInput.positionWS), output.vertexSH, output.probeOcclusion);

    #if defined(DEBUG_DISPLAY) || (defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2))
    output.positionWS = vertexInput.positionWS;
    output.viewDirWS = GetWorldSpaceViewDir(vertexInput.positionWS);
    #endif

    return output;
}

void BakedLitForwardPassFragment(
    Varyings input
    , out half4 outColor : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers : SV_Target1
#endif
    )
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

#if defined(PICOFINAL_ENV_FADE)
    // PicoFinal: dissolve in with the sky. Same interleaved-gradient noise as SkyboxDithered's SkyIgn, so the sky and
    // the environment dissolve alike. _PicoFinalEnvVisible (FloatHaze.hlsl) is set by MemoryEnvironment.
    float envIgn = frac(52.9829189 * frac(dot(input.positionCS.xy, float2(0.06711056, 0.00583715))));
    clip(_PicoFinalEnvVisible - envIgn - 0.001);
#endif

    half2 uv = input.uv0AndFogCoord.xy;
    #if defined(_NORMALMAP)
    half3 normalTS = SampleNormal(uv, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap)).xyz;
    #else
    half3 normalTS = half3(0, 0, 1);
    #endif
    InputData inputData;
    InitializeInputData(input, normalTS, inputData);
    SETUP_DEBUG_TEXTURE_DATA(inputData, UNDO_TRANSFORM_TEX(input.uv0AndFogCoord.xy, _BaseMap));

    half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
    half3 color = texColor.rgb * _BaseColor.rgb;
    half alpha = texColor.a * _BaseColor.a;

    alpha = AlphaDiscard(alpha, _Cutoff);
    color = AlphaModulate(color, alpha);

#ifdef LOD_FADE_CROSSFADE
    LODFadeCrossFade(input.positionCS);
#endif

#if defined(_DBUFFER)
    ApplyDecalToBaseColorAndNormal(input.positionCS, color, inputData.normalWS);
#endif

    InitializeBakedGIData(input, inputData);

    half4 finalColor = UniversalFragmentBakedLit(inputData, color, alpha, normalTS);

    // PicoFinal soft cloud edges: coverage thins toward the silhouette (alpha to coverage) and the edge fades into the sky
    half edgeAlpha = 1.0;
    float edgeHaze = 0.0;
    if (_EdgeSoft > 0.5)
        FloatSoftEdge(input.normalWS, input.hazePosWS, edgeAlpha, edgeHaze);

    finalColor.rgb = FloatRimAndHaze(finalColor.rgb, color, input.normalWS, input.hazePosWS, edgeHaze);   // PicoFinal warm rim light + distance haze
    finalColor.a = OutputAlpha(finalColor.a, _Surface);
    if (_EdgeSoft > 0.5)
    {
        clip(edgeAlpha - 0.01);
        // Only alpha to coverage needs the edge in alpha. Without it the surface is opaque, so alpha stays 1: an edge
        // alpha left in the eye buffer let the compositor show passthrough through every giant cloud's rim (dark
        // outlines) for as long as passthrough was on, the sky's 2.5 s fade-in after the cover opens (2026-11-15).
        finalColor.a = _EdgeCoverage > 0.5 ? edgeAlpha : 1.0;
    }
    outColor = finalColor;

#ifdef _WRITE_RENDERING_LAYERS
    outRenderingLayers = EncodeMeshRenderingLayer();
#endif
}
