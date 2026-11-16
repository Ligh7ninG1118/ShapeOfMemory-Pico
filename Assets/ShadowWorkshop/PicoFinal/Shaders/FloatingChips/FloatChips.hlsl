#ifndef PICOFINAL_FLOAT_CHIPS_INCLUDED
#define PICOFINAL_FLOAT_CHIPS_INCLUDED

// Floating chips: small pieces of a mesh drift slowly in place (up/down, a little sideways) and turn a few degrees
// about their own centre. Each vertex says whether it floats and where its chip's centre is (written in Maya):
//   COLOR.r     1 = part of a floating chip, 0 = still
//   COLOR.g     the chip's random seed (0..1), so chips move out of step
//   TEXCOORD2   chip centre x, y  (object space, metres)
//   TEXCOORD3.x chip centre z
// Material: _FloatAmplitude (metres), _FloatSpeed (cycles per second), _FloatRotation (degrees); for the whole-object
// drift below, _DriftAngle (degrees), _DriftSpeed (cycles per second), _DriftWavelength (metres). The chip positions are in
// object space, so a mesh using this must not be static-batched (batching bakes vertices into world space).

void FloatChipsTransform(float4 color, float2 pivotXY, float2 pivotZ, out float3 pivot, out float3x3 rot, out float3 offset)
{
    float mask = step(0.5, color.r);
    float seed = color.g;
    pivot = float3(pivotXY, pivotZ.x);

    float t = _Time.y * _FloatSpeed * 6.2831853;
    float3 phase = seed * float3(37.1, 53.7, 71.3);
    offset = _FloatAmplitude * mask * float3(0.6 * sin(t * 0.83 + phase.x), sin(t + phase.y), 0.6 * sin(t * 0.71 + phase.z));

    float3 axis = normalize(float3(sin(seed * 91.7), 0.6 + 0.4 * cos(seed * 47.3), sin(seed * 13.1 + 1.7)));
    float angle = radians(_FloatRotation) * mask * sin(t * 0.61 + seed * 29.0);
    float s, c;
    sincos(angle, s, c);
    float k = 1.0 - c;
    float3 u = axis;
    rot = float3x3(
        c + u.x * u.x * k,       u.x * u.y * k - u.z * s, u.x * u.z * k + u.y * s,
        u.y * u.x * k + u.z * s, c + u.y * u.y * k,       u.y * u.z * k - u.x * s,
        u.z * u.x * k - u.y * s, u.z * u.y * k + u.x * s, c + u.z * u.z * k);
}

// Drift: the whole object sways slowly as if the air carries it. Made for VR comfort:
// - the same apparent (angular) motion near and far: the reach grows with the distance from the player's place
//   (_DriftAngle degrees, a fraction of a degree), so far clouds move metres and near ruins centimetres;
// - slow (_DriftSpeed cycles per second, a period of 10-20 s), mostly up and down;
// - out of step across space (_DriftWavelength metres): neighbours drift differently, so the sky never moves as one
//   block, which is what reads as the player moving (vection) and makes people dizzy.
// Everything at one spot moves the same, so ivy laid on a ruin stays on it. Returns an object-space offset.
float3 FloatDriftOS(float3 positionOS)
{
    float3 pw = TransformObjectToWorld(positionOS);
    float reach = length(pw - float3(0, 1.4, 0)) * tan(radians(_DriftAngle));
    float3 k = pw * (6.2831853 / max(_DriftWavelength, 1.0));
    float t = _Time.y * _DriftSpeed * 6.2831853;
    float3 w = float3(0.45 * sin(t * 0.77 + k.x + 1.3 * k.z),
                      sin(t + k.z + 0.7 * k.x),
                      0.45 * sin(t * 0.61 + k.y + 1.1 * k.x));
    return mul((float3x3)GetWorldToObjectMatrix(), w * reach);
}

#endif
