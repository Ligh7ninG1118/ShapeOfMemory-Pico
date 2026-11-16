#ifndef PICOFINAL_FLOAT_HAZE_INCLUDED
#define PICOFINAL_FLOAT_HAZE_INCLUDED

// Distance haze (aerial perspective) for the sky environment, in place of volumetric fog, which the mobile renderer
// can't afford (no depth texture) and which RenderSettings fog can't give an additive scene (the active scene owns it,
// and it would fog the book too). Each pixel fades, with distance, into the sky that lies behind it: the game's own sky
// cubemap, turned like the runtime dome (MemorySky turns it by the look's GlowFacing - GlowAzimuth: -50 for Flow and for
// Windward Evening, so _HazeSkyRotation 50) and read at a
// blurred mip so no stars or lines print onto the stone.
//   _HazeStart / _HazeEnd  metres from the eyes where the haze begins and reaches _HazeAmount (smoothstep between)
//   _HazeAmount            how much of the sky colour at _HazeEnd (0 = off)
//   _HazeSkyRotation       degrees, the dome's lookup turn; _HazeSkyBlur the mip level; _HazeTint multiplies the sky
//   _HazeDepthStart / _HazeDepthEnd / _HazeDepthAmount   extra haze with depth below the eyes (see FloatHazeFactor)
//   In game, with the look's fog on (_PicoFinalFog, PicoFinalFog.hlsl), all of the above but _HazeAmount (which caps
//   its distance part) gives way to the one fog the whole scene shares.

TEXTURECUBE(_HazeSky);
SAMPLER(sampler_HazeSky);

// PicoFinal globals, set by the game (MemorySky, MemoryEnvironment): the stage's yaw, the dissolve, the live sky and
// the look's fog. With the Flow sky the live sky and each material's _HazeSky are the same.
#include "PicoFinalFog.hlsl"

// How much haze at this point: by distance (up to _HazeAmount), and more with depth below the eyes (up to
// _HazeDepthAmount between _HazeDepthStart and _HazeDepthEnd metres down), so what lies under the player sinks into
// the sky below rather than reading as a floor. The two combine like two layers of mist (1 - (1-a)(1-b)).
float FloatHazeFactor(float3 positionWS)
{
    float3 cam = GetCameraPositionWS();
    float d = length(positionWS - cam);
    float f = saturate((d - _HazeStart) / max(_HazeEnd - _HazeStart, 0.01));
    f = f * f * (3.0 - 2.0 * f) * _HazeAmount;
    float g = saturate((cam.y - positionWS.y - _HazeDepthStart) / max(_HazeDepthEnd - _HazeDepthStart, 0.01));
    g = g * g * (3.0 - 2.0 * g) * _HazeDepthAmount;
    return 1.0 - (1.0 - f) * (1.0 - g);
}

// The blurred sky colour behind this point, and how much of it to mix in (f). With the look's fog on: that fog's
// colour and amount, wrap included (PicoFinalFogAt).
half3 FloatHazeSky(float3 positionWS, out float f)
{
    half3 sky;
    f = 0.0;
    if (_PicoFinalFog > 0.5)   // uniform: one branch per draw
    {
        half3 fogSky, fogMist;
        sky = PicoFinalFogAt(positionWS, _HazeAmount, 1.0, f, fogSky, fogMist);
    }
    else
    {
        float3 v = positionWS - GetCameraPositionWS();
        float d = length(v);
        f = FloatHazeFactor(positionWS);
        float3 dir = v / max(d, 1e-4);
        float s, c;
        bool live = _PicoFinalHazeFromSky > 0.5;   // uniform: one branch per draw
        sincos(radians(live ? _MemorySkyYaw : _HazeSkyRotation - _PicoFinalEnvYaw), s, c);   // PicoFinal: turned with the stage
        dir = float3(c * dir.x + s * dir.z, dir.y, -s * dir.x + c * dir.z);
        if (live)
            sky = SAMPLE_TEXTURECUBE_LOD(_MemorySkyTex, sampler_MemorySkyTex, dir, _HazeSkyBlur).rgb * _MemorySkyTint.rgb * _HazeTint.rgb;
        else
            sky = SAMPLE_TEXTURECUBE_LOD(_HazeSky, sampler_HazeSky, dir, _HazeSkyBlur).rgb * _HazeTint.rgb;
    }
    return sky;
}

half3 FloatHaze(half3 color, float3 positionWS)
{
    float f;
    half3 sky = FloatHazeSky(positionWS, f);
    return lerp(color, sky, f);
}

// Warm rim light: the painted look of stone lit by the low sun from behind or the side, a saturated orange-yellow on
// only a small part of each model (aim: 10-20% of what's in sight). It blends toward the colour (doesn't add), so it
// stays saturated and never lifts the stone toward pink. View dependent, so it can't be baked; a few ALU, no texture.
//   _RimLightDir   world direction toward the glow (the Sun's light comes from there)
//   _RimColor      the orange-yellow, tinted a little by the surface's own albedo; _RimStrength 0 = off
//   _RimFacing     how squarely a face must turn toward the glow before it warms (-1 .. 1: 0 = any face toward it,
//                  1 = only head-on, below 0 = faces side-on to it too); the warmth then fades in over the next 0.3
//   _RimPower      how tight the backlit edge is to the silhouette
//   _RimFrontColor / _RimFront   colour and share of it left with the glow behind the player (1 = unchanged)
// Two parts: faces turned well toward the glow (strongest seen side-on), and a thin silhouette edge when the player
// looks into the glow (the backlit view, where the faces in sight all point away from it), on the glow's side only.
// Everything follows _RimLightDir, so the warmth comes from one side like the Sun, never all round like ambient.
//
// The haze is applied here too. Looking into the glow the rim goes on top of the haze, as light scattered toward the
// eye, so a far backlit giant keeps a clean warm edge (the haze only halves it). Looking anywhere else it is light on
// the stone and is hazed with it, so far buildings to the side and behind stay soft. extraHaze (0..1) adds more of the
// same sky on top, e.g. the soft-edge fade (FloatSoftEdge).
half3 FloatRimAndHaze(half3 color, half3 albedo, float3 normalWS, float3 positionWS, float extraHaze)
{
    float3 n = normalize(normalWS);
    float3 v = normalize(GetCameraPositionWS() - positionWS);
    // PicoFinal: the rim direction turned with the environment root (Quaternion.Euler(0, yaw, 0) * dir)
    float sy, cy;
    sincos(radians(_PicoFinalEnvYaw), sy, cy);
    float3 l = normalize(float3(cy * _RimLightDir.x + sy * _RimLightDir.z, _RimLightDir.y, -sy * _RimLightDir.x + cy * _RimLightDir.z));
    float nv = saturate(dot(n, v));
    // a face seen head-on takes only a little of the colour (its baked shading keeps the form); seen side-on, most of it
    float facing = smoothstep(_RimFacing, _RimFacing + 0.3, dot(n, l)) * (0.25 + 0.75 * (1.0 - nv));
    float lookTo = dot(-v, l);
    float toward = smoothstep(-0.9, -0.45, lookTo);
    facing *= lerp(_RimFront, 1.0, toward);
    float back = saturate(lookTo);
    // only the edges on the glow's side (top and left for a sun up and to the left), not all round the silhouette
    float edge = pow(1.0 - nv, _RimPower) * back * back * saturate(dot(n, l) * 2.0 + 0.3);
    float mask = saturate(_RimStrength * max(facing, edge));
    half3 tint = lerp(_RimFrontColor.rgb, _RimColor.rgb, toward) * lerp(half3(1, 1, 1), albedo, 0.15);

    float f;
    half3 sky = FloatHazeSky(positionWS, f);
    f = 1.0 - (1.0 - f) * (1.0 - extraHaze);
    half3 under = lerp(lerp(color, tint, mask), sky, f);                 // rim lights the stone, hazed with it
    half3 over = lerp(lerp(color, sky, f), tint, mask * (1.0 - 0.5 * f)); // rim scattered toward the eye
    return lerp(under, over, smoothstep(0.3, 0.8, lookTo));
}

// Soft, fuzzy edges for clouds, without transparency (no sorting, no overdraw): toward the silhouette the surface
// covers less of each pixel's MSAA samples (alpha to coverage, so the material needs _EdgeCoverage = 1 and the camera
// MSAA), broken up by a little object-space noise so it reads as a fuzzy rim rather than a clean fade, and the band
// also fades into the sky colour behind it, which keeps it soft where MSAA's few coverage steps show.
//   _EdgeWidth       how far in from the silhouette (in N.V) it starts to thin
//   _EdgeFuzz        how much the noise breaks the edge up; _EdgeNoiseScale noise blobs per object-space metre
//   _EdgeFade        how much of the outer band takes the sky colour
// Needs smooth normals (the clouds have them). Object-space noise, so it sticks to the cloud as it drifts.
float FloatEdgeHash(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.zyx + 31.32);
    return frac((p.x + p.y) * p.z);
}

float FloatEdgeNoise(float3 p)
{
    float3 i = floor(p), f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = FloatEdgeHash(i), b = FloatEdgeHash(i + float3(1, 0, 0)), c = FloatEdgeHash(i + float3(0, 1, 0)), d = FloatEdgeHash(i + float3(1, 1, 0));
    float e = FloatEdgeHash(i + float3(0, 0, 1)), g = FloatEdgeHash(i + float3(1, 0, 1)), h = FloatEdgeHash(i + float3(0, 1, 1)), k = FloatEdgeHash(i + float3(1, 1, 1));
    return lerp(lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y), lerp(lerp(e, g, f.x), lerp(h, k, f.x), f.y), f.z);
}

void FloatSoftEdge(float3 normalWS, float3 positionWS, out half alpha, out float haze)
{
    float3 n = normalize(normalWS);
    float3 v = normalize(GetCameraPositionWS() - positionWS);
    float nv = saturate(dot(n, v));
    float3 po = TransformWorldToObject(positionWS) * _EdgeNoiseScale;
    float noise = FloatEdgeNoise(po) * 0.65 + FloatEdgeNoise(po * 2.3 + 17.0) * 0.35;
    float t = nv / max(_EdgeWidth, 0.01) + (noise - 0.5) * 2.0 * _EdgeFuzz;
    alpha = smoothstep(0.0, 1.0, t);
    haze = (1.0 - smoothstep(0.0, 2.0, t)) * _EdgeFade;
}

#endif
