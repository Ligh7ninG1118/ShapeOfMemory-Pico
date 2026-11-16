Shader "PicoFinal/Env Cloud Sea"
{
    // The environment scene's cloud sea: the game's own cloud look (Shaders/Cloud.shader, ShapeOfMemory/Cloud, on the
    // cloud kit Art/Clouds/CloudKit.fbx), made to stand alone in Scenes/PicoFinal_Environment so it looks the same in the
    // editor and in game without MemoryClouds' globals (_CloudBase, _CloudHide, _MemorySunDir):
    //   light   a four-colour ramp (shadow, body, lit, hot) over wrapped N.L toward the sun (_SunDir, turned with the
    //           stage by _PicoFinalEnvYaw), mixed with the height in the cloud (vertex G) and darkened in the folds
    //           between puffs (vertex R), plus a silver lining on edges seen against the glow. Unlit: nothing is baked.
    //   haze    per pixel (per vertex showed the big triangles of the far meshes as flat bands), toward the sky behind
    //           (as FloatHaze.hlsl: _HazeSky at _HazeSkyRotation, or the game's live sky when _PicoFinalHazeFromSky is 1):
    //           with distance, on the same curve as the ruins' FloatHazeFactor (smoothstep from _HazeNear to _HazeFar,
    //           up to _HazeMax; the sea and the ruins carry the same 0 / 350 / 0.85, so a cloud and a ruin at the same
    //           distance fade alike: solid through the middle distance, about 14% at 90 m and 53% at 200 m, the far
    //           ring blending into the sky's own clouds), and under the cloud base, _BaseClear metres below the eyes and full _BaseFade further
    //           down, so the sea's lumps read and its underside melts away: below, into _Mist (mixed with the sky by
    //           _MistAmount), a lighter, softer cool than the sky straight down.
    //           "The sky behind" is read no lower than _HazeSkyFloor (sine of the elevation, just under the horizon): the
    //           skies go dark below the horizon (Flow about #58446D 25 degrees down, #2D2449 60 down) and the sea is seen
    //           looking down, so fading into the sky straight behind drew a dark line round every lump (2026-11-14).
    //   edges   where the cloud's outline turns away from the eye the colour goes toward that sky colour (_EdgeSoft),
    //           and with _EdgeCoverage the silhouette itself dissolves over _EdgeWidth of N.V, broken up by soft noise
    //           (alpha to coverage, needs MSAA), so the outlines read round instead of following the mesh's facets; tops
    //           (the big shape facing up, _EdgeKeepTops) never dissolve, or broad tops seen low would go patchy.
    //           "Turns away" is measured with the larger of N.V and the big-shape normal's N.V (UV2: the vertex normals
    //           diffused over the mesh, Art/Art Assets/Meshes/CloudSea), so the dips and folds between puffs, which face
    //           away too, keep their colour and don't open into holes; only the outline of the whole cloud softens.
    //           Since 2026-11-14 the sea's materials have the dissolve off and a lighter _EdgeSoft (the user: puffy
    //           clouds, but not see-through), so the outlines stay solid.
    //   billow  vertices breathe along their normals; the sea is otherwise still (no scripts in the scene).
    //   fade    PICOFINAL_ENV_FADE: the same dissolve as the rest of the scene (_PicoFinalEnvVisible).
    // Opaque, writing depth; one cubemap read per pixel (at a blurred mip) and two value noises at the edges.
    Properties
    {
        _Shadow("Shadow", Color) = (0.20, 0.17, 0.36, 1)
        _Body("Body", Color) = (0.38, 0.33, 0.56, 1)
        _Lit("Lit", Color) = (0.70, 0.52, 0.62, 1)
        _Hot("Hot", Color) = (0.98, 0.70, 0.50, 1)
        _Wrap("Light wrap", Range(0, 1)) = 0.35
        _HeightShare("Height share", Range(0, 1)) = 0.3
        _Occlusion("Fold darkening", Range(0, 1)) = 0.3
        _BulkShade("Light the dents like the big shape (0 = surface normal)", Range(0, 1)) = 0.5
        _SunDir("Toward the sun (world, as authored)", Vector) = (-0.33, 0.26, 0.91, 0)
        _Rim("Silver lining", Color) = (0.95, 0.64, 0.32, 1)
        _RimPower("Rim power", Range(1, 8)) = 2.5
        _RimGlow("Rim toward the glow", Range(0, 4)) = 3.2
        _RimAll("Rim everywhere", Range(0, 1)) = 0.12
        _ScatterPower("Glow focus", Range(1, 32)) = 5
        [NoScaleOffset] _HazeSky("Haze Sky (the game sky cubemap)", Cube) = "grey" {}
        _HazeTint("Haze Tint", Color) = (1, 1, 1, 1)
        _HazeSkyRotation("Haze Sky Rotation (degrees, runtime dome 50)", Float) = 50
        _HazeSkyBlur("Haze Sky Blur (mip)", Range(0, 10)) = 5
        _HazeNear("Haze starts (m)", Float) = 0
        _HazeFar("Haze full (m)", Float) = 350
        _HazeMax("Haze at full", Range(0, 1)) = 0.85
        _HazeSkyFloor("Haze sky: lowest elevation read (sine; the sky below is dark)", Range(-1, 0.2)) = -0.06
        _BaseClear("Base: clear to (m below the eyes)", Float) = 12
        _BaseFade("Base: fully hazed over (m)", Float) = 4.5
        _Mist("Base: mist colour (what the clouds fade into below)", Color) = (0.62, 0.58, 0.82, 1)
        _MistAmount("Base: mist colour vs the sky behind", Range(0, 1)) = 0.7
        _BillowAmp("Billow (mesh units)", Float) = 0.12
        _BillowScale("Billow scale (1/mesh units)", Float) = 0.35
        _BillowSpeed("Billow speed", Float) = 0.25
        _EdgeSoft("Soft edges (toward the sky colour)", Range(0, 1)) = 0.85
        _EdgeStart("Soft edge start (1 - N.V)", Range(0, 1)) = 0.45
        [ToggleUI] _EdgeCoverage("Dissolve the silhouettes (alpha to coverage)", Float) = 1
        _EdgeWidth("Dissolve width (N.V)", Range(0.01, 1)) = 0.35
        _EdgeFuzz("Dissolve noise", Range(0, 2)) = 0.7
        _EdgeNoiseScale("Dissolve noise scale (1/mesh units)", Float) = 0.6
        _EdgeKeepTops("Dissolve: keep tops (big shape facing up)", Range(0, 2)) = 0.6
        _Dither("Dither (8-bit steps)", Range(0, 2)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "CloudSea"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back ZWrite On ZTest LEqual
            AlphaToMask [_EdgeCoverage]
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ PICOFINAL_ENV_FADE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

            TEXTURECUBE(_HazeSky); SAMPLER(sampler_HazeSky);
            // PicoFinal globals (the stage's yaw, the dissolve, the live sky, the look's fog), set by the game; 0 in the editor.
            #include "PicoFinalFog.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Shadow, _Body, _Lit, _Hot, _Rim, _HazeTint, _Mist;
                float4 _SunDir;
                half _Wrap, _HeightShare, _Occlusion, _BulkShade, _RimPower, _RimGlow, _RimAll, _ScatterPower, _EdgeSoft, _EdgeStart;
                float _HazeSkyRotation, _HazeSkyBlur, _HazeNear, _HazeFar, _HazeMax, _HazeSkyFloor, _BaseClear, _BaseFade, _MistAmount;
                float _BillowAmp, _BillowScale, _BillowSpeed;
                float _EdgeCoverage, _EdgeWidth, _EdgeFuzz, _EdgeNoiseScale, _EdgeKeepTops;
                half _Dither;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; float3 bulkOS : TEXCOORD2; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half2 shape : TEXCOORD2;      // fold openness, height share
                float3 positionOS : TEXCOORD3; // the dissolve's noise, stuck to the cloud
                float3 bulkWS : TEXCOORD4;     // the big-shape normal (the vertex normal where the mesh has none)
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float3 TurnY(float3 d, float degrees)
            {
                float s, c; sincos(radians(degrees), s, c);
                return float3(c * d.x + s * d.z, d.y, -s * d.x + c * d.z);
            }

            half3 SkyBehind(float3 dir)
            {
                half3 sky;
                if (_PicoFinalHazeFromSky > 0.5)   // uniform: one branch per draw
                    sky = SAMPLE_TEXTURECUBE_LOD(_MemorySkyTex, sampler_MemorySkyTex, TurnY(dir, _MemorySkyYaw), _HazeSkyBlur).rgb * _MemorySkyTint.rgb;
                else
                    sky = SAMPLE_TEXTURECUBE_LOD(_HazeSky, sampler_HazeSky, TurnY(dir, _HazeSkyRotation - _PicoFinalEnvYaw), _HazeSkyBlur).rgb;
                return sky * _HazeTint.rgb;
            }

            // The sky behind, read at the same azimuth but no lower than _HazeSkyFloor (straight down: the sky ahead)
            half3 HazeSky(float3 dir)
            {
                float2 h = dir.xz;
                float hl = length(h);
                h = hl > 1e-4 ? h / hl : float2(0, 1);
                float sy = max(dir.y, _HazeSkyFloor);
                float cy = sqrt(saturate(1 - sy * sy));
                return SkyBehind(float3(h.x * cy, sy, h.y * cy));
            }

            float Hash(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }

            float Noise(float3 p)
            {
                float3 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash(i), b = Hash(i + float3(1, 0, 0)), c = Hash(i + float3(0, 1, 0)), d = Hash(i + float3(1, 1, 0));
                float e = Hash(i + float3(0, 0, 1)), g = Hash(i + float3(1, 0, 1)), h = Hash(i + float3(0, 1, 1)), k = Hash(i + float3(1, 1, 1));
                return lerp(lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y), lerp(lerp(e, g, f.x), lerp(h, k, f.x), f.y), f.z);
            }

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
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
                o.positionOS = v.positionOS.xyz;
                float3 bulk = dot(v.bulkOS, v.bulkOS) > 0.25 ? v.bulkOS : v.normalOS;
                o.bulkWS = TransformObjectToWorldNormal(bulk);
                return o;
            }

            float Ign(float2 pixel) { return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715)))); }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
            #if defined(PICOFINAL_ENV_FADE)
                clip(_PicoFinalEnvVisible - Ign(i.positionCS.xy) - 0.001);
            #endif
                float3 cam = GetCameraPositionWS();
                float3 toPixel = i.positionWS - cam;
                float distance = length(toPixel);
                float3 n = normalize(i.normalWS);
                float3 v = -toPixel / max(distance, 1e-4);
                float3 l = normalize(TurnY(_SunDir.xyz, _PicoFinalEnvYaw));

                // light: partly from the big shape, so the dents between puffs don't fall into shadow and read as holes
                float3 bulkN = normalize(i.bulkWS);
                float3 ln = normalize(lerp(n, bulkN, _BulkShade));
                half fold = lerp(1, i.shape.x, _Occlusion);
                half lit = saturate(dot(ln, l) * (1 - _Wrap) + _Wrap);
                half t = saturate(lerp(lit, i.shape.y, _HeightShare) * fold);
                half3 c = lerp(_Shadow.rgb, _Body.rgb, smoothstep(0, 0.5, t));
                c = lerp(c, _Lit.rgb, smoothstep(0.45, 0.8, t));
                c = lerp(c, _Hot.rgb, smoothstep(0.78, 1, t));
                // outline: the larger of the surface's and the big shape's N.V (a dip faces away, the cloud around it doesn't)
                float nv = max(saturate(dot(n, v)), saturate(dot(bulkN, v)));
                half away = 1 - nv;
                half edge = pow(away, _RimPower);
                half toward = pow(saturate(dot(-v, l)), _ScatterPower);
                c += _Rim.rgb * edge * (toward * _RimGlow + _RimAll) * fold;

                // haze: the sky behind, with distance and under the cloud base; in game with the look's fog on, that
                // fog (PicoFinalFog.hlsl: the same for the ruins), this material's _HazeMax capping its distance part
                half3 sky, mist;
                float far, low;
                if (_PicoFinalFog > 0.5)   // uniform: one branch per draw
                {
                    sky = PicoFinalFogSky(-v, distance);
                    mist = lerp(_PicoFinalFogMistColor.rgb, sky, _PicoFinalFogMist.z);
                    far = PicoFinalFogDistance(distance, _HazeMax);
                    low = PicoFinalFogMistAt(i.positionWS.y);
                }
                else
                {
                    sky = HazeSky(-v);
                    mist = lerp(sky, _Mist.rgb, _MistAmount);
                    far = saturate((distance - _HazeNear) / max(_HazeFar - _HazeNear, 1));
                    far = far * far * (3 - 2 * far) * _HazeMax;   // FloatHazeFactor's curve
                    low = saturate((cam.y - _BaseClear - i.positionWS.y) / max(_BaseFade, 0.01));
                    low = low * low * (3 - 2 * low);
                }
                // below: into a soft mist (lighter than the sky straight down). Outlines go toward what lies behind
                // them: looking down that is more of the sea, its tops seen through the mist between (the mist
                // colour), so an outline melts into the cloud behind instead of showing the sky through it; looking
                // up, the sky. Then distance into the sky, the low parts into the mist (no dark line along bottoms).
                c = lerp(c, mist, low);
                half3 behind = lerp(mist, sky, smoothstep(-0.05, 0.02, -v.y));
                c = lerp(c, behind, smoothstep(_EdgeStart, 1, away) * _EdgeSoft);
                c = lerp(c, lerp(sky, mist, low), far);

                // the silhouette dissolves, broken up by noise stuck to the cloud
                half alpha = 1;
                if (_EdgeCoverage > 0.5)
                {
                    float3 po = i.positionOS * _EdgeNoiseScale;
                    float noise = Noise(po) * 0.65 + Noise(po * 2.3 + 17.0) * 0.35;
                    // tops seen low never dissolve (they'd go patchy): only the sides, where the outline is
                    alpha = smoothstep(0.0, 1.0, (nv + _EdgeKeepTops * saturate(bulkN.y)) / _EdgeWidth + (noise - 0.5) * 2.0 * _EdgeFuzz);
                }

                half dither = (Ign(i.positionCS.xy) - 0.5) * _Dither / 255.0;
                c = SRGBToLinear(saturate(LinearToSRGB(c) + dither));
                return half4(c, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
