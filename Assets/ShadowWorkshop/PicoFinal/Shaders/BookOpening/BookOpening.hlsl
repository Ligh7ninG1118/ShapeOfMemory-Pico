#ifndef PICOFINAL_BOOK_OPENING_INCLUDED
#define PICOFINAL_BOOK_OPENING_INCLUDED

// Book opening: the book mesh is modelled closed, its hinge (where the pages meet the spine) on the object's Z axis
// through the origin. COLOR.r is each vertex's share of the opening (written in Maya): 1 the front half (turns
// rigidly), 0 the back half (stays), between on the spine and the cover wrap (bends smoothly). _Opening is the code's
// MemoryBook.Opening in degrees: 0 closed, 90 standing on the walls, 180 lying open flat.
// Maya's +Z turn becomes -Z in Unity (the FBX import mirrors X), hence the minus.

float3x3 BookOpeningRotation(float4 color)
{
    float angle = -radians(_Opening) * saturate(color.r);
    float s, c;
    sincos(angle, s, c);
    return float3x3(c, -s, 0,
                    s,  c, 0,
                    0,  0, 1);
}

#endif
