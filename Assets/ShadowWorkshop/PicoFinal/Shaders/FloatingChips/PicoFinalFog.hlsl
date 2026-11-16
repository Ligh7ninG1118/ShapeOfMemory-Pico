#ifndef PICOFINAL_FOG_INCLUDED
#define PICOFINAL_FOG_INCLUDED

// The game's globals for the environment scene's shaders (FloatHaze.hlsl for Floating Chips Baked Lit, and
// EnvCloudSea.shader), outside UnityPerMaterial so the SRP Batcher is unaffected. Without the game (the environment
// scene alone in the editor) they are all 0 and every material renders as authored.
//   _PicoFinalEnvYaw      degrees, the stage's world yaw (the environment turns with it); the haze's cubemap lookup
//                         and the rim's direction turn with it. 0 = as authored.
//   _PicoFinalEnvVisible  0 hidden .. 1 shown, the dissolve while the sky fades in (PICOFINAL_ENV_FADE only).
//   _PicoFinalHazeFromSky 1: the haze reads the game's live sky (_MemorySkyTex turned by _MemorySkyYaw, as the game's
//                         own clouds do), so it follows whichever sky the game shows; 0: each material's _HazeSky.
//   (MemorySky sets these.)
float _PicoFinalEnvYaw;
float _PicoFinalEnvVisible;
float _PicoFinalHazeFromSky;
TEXTURECUBE(_MemorySkyTex);
SAMPLER(sampler_MemorySkyTex);
float _MemorySkyYaw;
half4 _MemorySkyTint;

// The look's fog (MemoryLook.Fog, published by MemoryEnvironment; Runtime/MemoryFog.cs explains the fields).
// _PicoFinalFog 1: one fog for the whole scene, clouds and ruins alike, in place of each material's own haze (each
// material's own Haze Amount / Haze at full only caps the distance part, so the giants stay on the horizon):
//   distance  into the live sky behind, from _PicoFinalFogFar.x to .y metres, up to .z, on a curve between a slow
//             start (.w 0: smoothstep) and the game's own clouds' of 2026-11-12 (.w 1: ease-out, thickest change
//             near: a third at 100 m, half at 150 m).
//   mist      under the cloud tops: from _PicoFinalFogMist.x (world height) thickening over .y metres to full, into
//             _PicoFinalFogMistColor mixed with the sky behind by .z, so the gaps between the clouds fill with it and
//             whatever stands in them sinks into it.
//   wrap      (ruins only) from _PicoFinalFogWrap.x down to .y (world heights, just over the cloud tops), up to .z of
//             _PicoFinalFogWrapColor.rgb (the colour of the cloud tops) taken .a of the way to the mist: a building
//             fades into the cloud and the fog it stands in, so there is no hard line where it meets the cloud.
//   The fog's colour is the sky right above it: whatever lies below the elevation _PicoFinalFogMist.w (a sine, just
//   over the horizon) takes the sky in the same direction just above the cloud horizon (the skies' bright band there:
//   orange toward the glow, pink and lavender round the rest; below the horizon they go a flat dark purple), from
//   _PicoFinalFogRing (Hidden/PicoFinal/Fog Ring: that band averaged sideways, one texel per azimuth of the sky's own
//   cubemap, so no wisp of the band streaks down the screen); higher up, over the next ~9 degrees, the sky right
//   behind (mip _PicoFinalFogWrap.w), for the ruins floating in the sky. Then taken .a of the way to
//   _PicoFinalFogTint.rgb (an optional colour of the fog's own).
float _PicoFinalFog;
float4 _PicoFinalFogFar;
float4 _PicoFinalFogMist;
half4 _PicoFinalFogMistColor;
float4 _PicoFinalFogWrap;
half4 _PicoFinalFogWrapColor;
half4 _PicoFinalFogTint;
float4 _PicoFinalFogTintFar;   // metres over which the fog's own colour gives way to the sky (x to y; far off: never)
TEXTURE2D(_PicoFinalFogRing);
SAMPLER(sampler_PicoFinalFogRing);

float3 PicoFinalTurnY(float3 d, float degrees)
{
    float s, c;
    sincos(radians(degrees), s, c);
    return float3(c * d.x + s * d.z, d.y, -s * d.x + c * d.z);
}

// The fog's colour for a point seen along dir (from the eye) at distance metres: the sky right above it (the ring at
// its azimuth, or higher up the sky behind), tinted toward the fog's own colour (all the way at share 1: one colour
// for all the fog); from _PicoFinalFogTintFar.x to .y metres the tint gives way to the sky itself again, so the
// farthest clouds still melt into the sky's own (no band at the horizon).
half3 PicoFinalFogSky(float3 dir, float distance)
{
    float3 d = PicoFinalTurnY(dir, _MemorySkyYaw);
    float u = atan2(d.x, d.z) * (0.5 / PI) + 0.5;
    half3 ring = SAMPLE_TEXTURE2D_LOD(_PicoFinalFogRing, sampler_PicoFinalFogRing, float2(u, 0.5), 0).rgb;
    half3 behind = SAMPLE_TEXTURECUBE_LOD(_MemorySkyTex, sampler_MemorySkyTex, d, _PicoFinalFogWrap.w).rgb * _MemorySkyTint.rgb;
    half3 sky = lerp(ring, behind, smoothstep(_PicoFinalFogMist.w, _PicoFinalFogMist.w + 0.15, d.y));
    return lerp(sky, _PicoFinalFogTint.rgb, _PicoFinalFogTint.a * (1.0 - smoothstep(_PicoFinalFogTintFar.x, _PicoFinalFogTintFar.y, distance)));
}

float PicoFinalSmooth(float t) { return t * t * (3.0 - 2.0 * t); }

// The distance part (0..1), capped by the material's own amount.
float PicoFinalFogDistance(float distance, float cap)
{
    float t = saturate((distance - _PicoFinalFogFar.x) / max(_PicoFinalFogFar.y - _PicoFinalFogFar.x, 1.0));
    return lerp(PicoFinalSmooth(t), t * (2.0 - t), _PicoFinalFogFar.w) * min(_PicoFinalFogFar.z, cap);
}

// The mist under the cloud tops (0..1) at a world height.
float PicoFinalFogMistAt(float y)
{
    return PicoFinalSmooth(saturate((_PicoFinalFogMist.x - y) / max(_PicoFinalFogMist.y, 0.01)));
}

// The wrap into the cloud tops' colour (0..amount) at a world height.
float PicoFinalFogWrapAt(float y)
{
    return PicoFinalSmooth(saturate((_PicoFinalFogWrap.x - y) / max(_PicoFinalFogWrap.x - _PicoFinalFogWrap.y, 0.01))) * _PicoFinalFogWrap.z;
}

// All of it for a point: the colour to blend toward and how much (lerp(colour, fogColor, amount) gives wrap, then
// mist, then distance, one over the other). wrap 0 leaves the wrap out (the clouds themselves). Also gives the sky
// and the mist colour behind it, for the caller's own edges.
half3 PicoFinalFogAt(float3 positionWS, float cap, float wrap, out float amount, out half3 sky, out half3 mist)
{
    float3 toPoint = positionWS - GetCameraPositionWS();
    float distance = length(toPoint);
    sky = PicoFinalFogSky(toPoint / max(distance, 1e-4), distance);
    mist = lerp(_PicoFinalFogMistColor.rgb, sky, _PicoFinalFogMist.z);
    float a = PicoFinalFogWrapAt(positionWS.y) * wrap;
    float b = PicoFinalFogMistAt(positionWS.y);
    float c = PicoFinalFogDistance(distance, cap);
    half3 far = lerp(sky, mist, b);   // what lies under the tops fades with distance into the mist, the rest into the sky
    amount = 1.0 - (1.0 - a) * (1.0 - b) * (1.0 - c);
    half3 wrapColor = lerp(_PicoFinalFogWrapColor.rgb, mist, _PicoFinalFogWrapColor.a);
    half3 sum = wrapColor * (a * (1.0 - b) * (1.0 - c)) + mist * (b * (1.0 - c)) + far * c;
    return sum / max(amount, 1e-4);
}

#endif
