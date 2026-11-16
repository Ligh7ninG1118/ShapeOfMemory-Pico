#ifndef SHAPE_OF_MEMORY_HAND_SPACE
#define SHAPE_OF_MEMORY_HAND_SPACE
// What a hand look needs besides the skinned surface, set per hand every frame by MemoryHandSkin (MaterialPropertyBlock)
// instead of read from extra mesh channels (2026-11-15). The hands may be meshes we may not modify (the Unity XR Hands
// sample hand), so nothing here may come from UV2/UV3 any more:
//   _HandToLocal          world -> the hand's own frame: the wrist at 0, x toward the thumb, y toward the fingers, z out
//                         of the back. Patterns (hatching, the outline's boil, the lantern's flicker) are drawn in it, so
//                         they ride on the hand.
//   _IndexPad, _ThumbPad  the pads just behind the index and thumb tips (world): their aqua.
//   _WristWS, _HandFwdWS  the wrist joint and the way the hand points (world): the wrist stub fades out behind the wrist.
// Unset (an editor preview of the material alone): no pattern offset, no tips, nothing faded.
float4x4 _HandToLocal;
float4 _IndexPad, _ThumbPad, _WristWS, _HandFwdWS;
float4 _WristFade;   // x..y: metres along the hand from the wrist joint where the stub fades in (unset: -4.2..-1.4 cm)

float3 HandRest(float3 positionWS)
{
    return mul(_HandToLocal, float4(positionWS, 1)).xyz;
}
// x: the index and thumb tips (1 on the pad, 0 by 1.7 cm); y: the wrist's fade over _WristFade (the look's: each model's
// forearm stub ends at its own length behind the wrist, and the cut must be gone by then).
float2 HandMasks(float3 positionWS)
{
    float d = min(distance(positionWS, _IndexPad.xyz), distance(positionWS, _ThumbPad.xyz));
    float tip = 1 - smoothstep(0.006, 0.017, d);
    float2 range = _WristFade.x < _WristFade.y ? _WristFade.xy : float2(-0.042, -0.014);
    float fade = smoothstep(range.x, range.y, dot(positionWS - _WristWS.xyz, _HandFwdWS.xyz));
    return float2(tip, fade);
}
#endif
