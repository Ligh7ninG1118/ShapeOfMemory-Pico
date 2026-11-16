// Generated from URP 17.3 BakedLit.shader for PicoFinal floating chips (see FloatChips.hlsl). Only the marked lines differ.
Shader "PicoFinal/Floating Chips Baked Lit"
{
    Properties
    {
        [Header(Floating chips)]
        _FloatAmplitude("Float Amplitude (m)", Float) = 0.015
        _FloatSpeed("Float Speed (cycles per second)", Float) = 0.2
        _FloatRotation("Float Rotation (degrees)", Float) = 6
        [Header(Whole object drift)]
        _DriftAngle("Drift (degrees as seen from the player, 0 = off)", Float) = 0
        _DriftSpeed("Drift Speed (cycles per second)", Float) = 0.06
        _DriftWavelength("Drift Wavelength (m)", Float) = 80

        [Header(Distance haze)]
        [NoScaleOffset] _HazeSky("Haze Sky (the game sky cubemap)", Cube) = "grey" {}
        _HazeTint("Haze Tint", Color) = (1, 1, 1, 1)
        _HazeStart("Haze Start (m)", Float) = 15
        _HazeEnd("Haze End (m)", Float) = 220
        _HazeAmount("Haze Amount at the end (0 = off)", Range(0, 1)) = 0
        _HazeSkyRotation("Haze Sky Rotation (degrees, runtime dome 50)", Float) = 50
        _HazeSkyBlur("Haze Sky Blur (mip)", Range(0, 10)) = 6
        _HazeDepthStart("Depth Haze Start (m below the eyes)", Float) = 20
        _HazeDepthEnd("Depth Haze End (m below the eyes)", Float) = 110
        _HazeDepthAmount("Depth Haze Amount at the end (0 = off)", Range(0, 1)) = 0

        [Header(Warm rim light)]
        _RimColor("Rim Colour", Color) = (0.95, 0.62, 0.22, 1)
        _RimLightDir("Rim Light Direction (world, toward the glow)", Vector) = (-0.33, 0.2, 0.92, 0)
        _RimStrength("Rim Strength (0 = off)", Range(0, 4)) = 0
        _RimPower("Rim Tightness (edge)", Range(0.5, 8)) = 4
        _RimFacing("Rim Facing (how squarely a face must turn to the glow to warm; below 0 = side-on faces too)", Range(-1, 1)) = 0.5
        _RimFrontColor("Rim Colour with the glow behind the player (pale gold)", Color) = (1, 0.85, 0.6, 1)
        _RimFront("Rim left with the glow behind the player (0-1)", Range(0, 1)) = 0.45

        [Header(Soft edges (clouds))]
        [ToggleUI] _EdgeSoft("Soft Edges (needs MSAA and Edge Coverage 1)", Float) = 0
        _EdgeWidth("Edge Width (how far in from the silhouette it thins)", Range(0.02, 1)) = 0.35
        _EdgeFuzz("Edge Fuzz (noise breaking up the edge)", Range(0, 1)) = 0.5
        _EdgeNoiseScale("Edge Noise Scale (blobs per object-space metre)", Float) = 8
        _EdgeFade("Edge Fade into the sky behind (0-1)", Range(0, 1)) = 0.5
        [ToggleUI] _EdgeCoverage("Edge Coverage (alpha to coverage; on with Soft Edges)", Float) = 0

        [MainTexture] _BaseMap("Texture", 2D) = "white" {}
        [MainColor]   _BaseColor("Color", Color) = (1, 1, 1, 1)
        _Cutoff("AlphaCutout", Range(0.0, 1.0)) = 0.5
        _BumpMap("Normal Map", 2D) = "bump" {}

        // BlendMode
        _Surface("__surface", Float) = 0.0
        _Blend("__mode", Float) = 0.0
        _Cull("__cull", Float) = 2.0
        [ToggleUI] _AlphaClip("__clip", Float) = 0.0
        [HideInInspector] _BlendOp("__blendop", Float) = 0.0
        [HideInInspector] _SrcBlend("__src", Float) = 1.0
        [HideInInspector] _DstBlend("__dst", Float) = 0.0
        [HideInInspector] _SrcBlendAlpha("__srcA", Float) = 1.0
        [HideInInspector] _DstBlendAlpha("__dstA", Float) = 0.0
        [HideInInspector] _ZWrite("__zw", Float) = 1.0
        [HideInInspector] _AlphaToMask("__alphaToMask", Float) = 0.0
        [HideInInspector] _AddPrecomputedVelocity("_AddPrecomputedVelocity", Float) = 0.0
        [HideInInspector] _XRMotionVectorsPass("_XRMotionVectorsPass", Float) = 1.0

        // Editmode props
        _QueueOffset("Queue offset", Float) = 0.0

        [HideInInspector][NoScaleOffset]unity_Lightmaps("unity_Lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_LightmapsInd("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_ShadowMasks("unity_ShadowMasks", 2DArray) = "" {}
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "IgnoreProjector" = "True"
            "RenderPipeline" = "UniversalPipeline"
            "DisableBatching" = "True"
        }
        LOD 100

        // -------------------------------------
        // Render State Commands
        Blend [_SrcBlend][_DstBlend], [_SrcBlendAlpha][_DstBlendAlpha]
        ZWrite [_ZWrite]
        Cull [_Cull]

        Pass
        {
            Name "BakedLit"
            Tags
            {
                "LightMode" = "UniversalForwardOnly"
            }

            // -------------------------------------
            // Render State Commands
            // PicoFinal: driven by Edge Coverage (soft cloud edges), not by alpha clipping, so the material inspector
            // can't switch it off. Alpha clipping in this shader therefore cuts without alpha to coverage.
            AlphaToMask[_EdgeCoverage]

            HLSLPROGRAM
            #pragma target 2.0

            // -------------------------------------
            // Shader Stages
            #pragma vertex BakedLitForwardPassVertex
            #pragma fragment BakedLitForwardPassFragment

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local_fragment _SURFACE_TYPE_TRANSPARENT
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _ALPHAMODULATE_ON

            // -------------------------------------
            // Universal Pipeline keywords
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fragment _ LIGHTMAP_BICUBIC_SAMPLING
            #pragma multi_compile_fragment _ REFLECTION_PROBE_ROTATION
            #pragma multi_compile _ USE_LEGACY_LIGHTMAPS
            #pragma multi_compile _ DEBUG_DISPLAY
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            // PicoFinal: the environment dissolves in with the game's sky (MemoryEnvironment turns this on only during the
            // fade, so the clip never costs early depth testing the rest of the time)
            #pragma multi_compile_fragment _ PICOFINAL_ENV_FADE

            // -------------------------------------
            // Includes
            // Lighting include is needed because of GI
            #include "FloatBakedLitInput.hlsl"
            #include "FloatBakedLitForwardPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Tags
            {
                "LightMode" = "DepthOnly"
            }

            // -------------------------------------
            // Render State Commands
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 2.0

            // -------------------------------------
            // Shader Stages
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _ALPHATEST_ON

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile _ LOD_FADE_CROSSFADE

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            // -------------------------------------
            // Includes
            #include "FloatBakedLitInput.hlsl"
            #include "FloatDepthOnlyPass.hlsl"
            ENDHLSL
        }

        // This pass is used when drawing to a _CameraNormalsTexture texture with the forward renderer or the depthNormal prepass with the deferred renderer.
        Pass
        {
            Name "DepthNormalsOnly"
            Tags
            {
                "LightMode" = "DepthNormalsOnly"
            }

            // -------------------------------------
            // Render State Commands
            ZWrite On
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 2.0

            // -------------------------------------
            // Shader Stages
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _ _NORMALMAP
            #pragma shader_feature_local_fragment _ALPHATEST_ON

            // -------------------------------------
            // Universal Pipeline keywords
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT // forward-only variant
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            // -------------------------------------
            // Includes
            #include "FloatBakedLitInput.hlsl"
            #include "FloatBakedLitDepthNormalsPass.hlsl"
            ENDHLSL
        }

        // Same as DepthNormals pass, but used for deferred renderer and forwardOnly materials.

        // This pass it not used during regular rendering, only for lightmap baking.
        Pass
        {
            Name "Meta"
            Tags
            {
                "LightMode" = "Meta"
            }

            // -------------------------------------
            // Render State Commands
            Cull Off

            HLSLPROGRAM
            #pragma target 2.0

            // -------------------------------------
            // Shader Stages
            #pragma vertex UniversalVertexMeta
            #pragma fragment UniversalFragmentMetaUnlit

            // -------------------------------------
            // Unity defined keywords
            #pragma shader_feature EDITOR_VISUALIZATION

            // -------------------------------------
            // Includes
            #include "FloatBakedLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/BakedLitMetaPass.hlsl"

            ENDHLSL
        }

        Pass
        {
            Name "Universal2D"
            Tags
            {
                "LightMode" = "Universal2D"
            }

            // -------------------------------------
            // Render State Commands
            Blend[_SrcBlend][_DstBlend]
            ZWrite[_ZWrite]
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 2.0

            // -------------------------------------
            // Shader Stages
            #pragma vertex vert
            #pragma fragment frag

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _ALPHAPREMULTIPLY_ON

            // -------------------------------------
            // Includes
            #include "FloatBakedLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/Utils/Universal2D.hlsl"

            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
