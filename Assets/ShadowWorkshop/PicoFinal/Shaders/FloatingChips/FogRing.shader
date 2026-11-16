Shader "Hidden/PicoFinal/Fog Ring"
{
    // The fog's colour round the horizon (MemoryFogRing, Runtime/MemoryFog.cs), drawn once into a strip one texel high:
    // for each azimuth of the sky's own cubemap (u 0..1 = -180..180 degrees, atan2(x, z)), the sky just over the cloud
    // horizon, from _FogRing.x up _FogRing.w (radians), averaged over _FogRing.y either side (radians of azimuth) at
    // mip _FogRing.z. The fog reads it (PicoFinalFog.hlsl) rather than the cubemap straight above each point, which
    // printed every wisp painted in the sky's horizon band down the screen as a vertical streak.
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZTest Always ZWrite Off Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURECUBE(_MemorySkyTex); SAMPLER(sampler_MemorySkyTex);
            half4 _MemorySkyTint;
            float4 _FogRing;   // elevation (rad), half width (rad), mip, height of the band (rad)

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = float4(v.uv * 2.0 - 1.0, 0.5, 1.0);   // the whole target, whatever matrices are set
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float azimuth = (i.uv.x - 0.5) * 2.0 * PI;
                half3 sum = 0;
                float weights = 0;
                for (int a = -6; a <= 6; a++)
                {
                    float w = 1.0 - abs(a) / 7.0;   // a tent across the width
                    float az = azimuth + a / 6.0 * _FogRing.y;
                    for (int e = 0; e < 3; e++)
                    {
                        float el = _FogRing.x + e * 0.5 * _FogRing.w;
                        float3 d = float3(sin(az) * cos(el), sin(el), cos(az) * cos(el));
                        sum += SAMPLE_TEXTURECUBE_LOD(_MemorySkyTex, sampler_MemorySkyTex, d, _FogRing.z).rgb * w;
                        weights += w;
                    }
                }
                return half4(sum / weights * _MemorySkyTint.rgb, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
