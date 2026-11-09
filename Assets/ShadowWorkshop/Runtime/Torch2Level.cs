using System;
using UnityEngine;

namespace CloudGarden.ShadowPuzzle
{
    [Serializable] public struct Torch2Piece
    {
        public string label;
        // What is drawn on the orb.
        public Mesh mesh;
        public Material[] materials;
        // Pose in the pile's own frame (pivot at the hub; the answer rotation is applied on top).
        public Vector3 position,euler;
        [Tooltip("Zero means (1,1,1), for pieces made before scale was stored.")] public Vector3 scale;
        [Tooltip("Readable mesh that casts this piece's shadow; empty uses mesh (which must then be readable).")] public Mesh shadowMesh;
        [Tooltip("Drawn on the orb, casts no shadow.")] public bool visualOnly;
        [Tooltip("Where it lies on the table before the level (level space, table top y = 0), from the gym's table layout. Off on any piece scatters them all automatically (Torch2Transit).")]
        public bool onTable;
        public Vector3 tablePosition,tableEuler;
        [Tooltip("Pieces with the same group travel between the table and the orb as one thing.")] public int tableGroup;
        public Vector3 Scale=>scale==Vector3.zero?Vector3.one:scale;
        public Mesh ShadowMesh=>shadowMesh?shadowMesh:mesh;
    }

    // Pictures that also count, as seen on the wall: upside down (the pile turned half round the lamp's line of sight) and
    // mirrored (half round the picture's upright). Each is an exact second answer (Torch2Projection.Answers). Upside down
    // is the picture itself, turned; mirrored is only near a mirror image (the lamp's perspective swaps near and far, so
    // Birthday's mirror pose was "not even similar", the user's call 2026-11-08) and is off unless a level ticks it.
    [Flags] public enum Torch2Flips{None=0,UpsideDown=1,Mirrored=2}

    // Iteration 2: fixed lamps in a room corner, the player rolls and carries an orb holding the pile.
    // One lamp per wall; a level uses one or both. Made by Tools/ShadowArt/build_torch2.py and
    // MetaHackthone > Shadow Workshop > Import torch levels 2, or exported from the designers' gym (Torch2Gym).
    // Targets are the meshes' own shadows at the solution.
    [CreateAssetMenu(menuName="Shadow Workshop/Torch level 2")]
    public sealed class Torch2Level : ScriptableObject
    {
        public const string ResourceFolder="ShadowArt/Torch2/";
        public string title,button;
        [TextArea(2,4)] public string riddle,hint,complete;
        public Color accent=new Color(.95f,.62f,.30f);
        [Tooltip("Lamp/wall indices in Torch2Projection: 0 = left wall, 1 = right wall.")] public int[] lamps={0};
        public Torch2Piece[] pieces=Array.Empty<Torch2Piece>();
        [Header("Answer and start (pile at Torch2Projection.Home)")]
        public Quaternion solution=Quaternion.identity;
        public Quaternion start=Quaternion.Euler(20,140,0);
        [Header("Eased match per wall (position and size of the shadow do not matter)")]
        public int tolerance=1;
        [Range(0,1)] public float coverMin=.90f,spillMax=.10f,iouMin=.62f;
        [Tooltip("Both faces of every triangle cast, so single-sided and mirrored meshes still do. Gym levels set it.")]
        public bool bothFaces;
        [Tooltip("The picture also counts upside down (exact) and, if ticked, mirrored (only close: perspective swaps near and far). Two-wall levels: mirrored on both, or with both ticked each wall's own upside-down turn; upside down alone has no turn there.")]
        public Torch2Flips flips=Torch2Flips.UpsideDown;
        [Header("Closeness cue (Torch2Cue; empty slides or layers use generated placeholders)")]
        [Tooltip("Picture the left lamp projects, blurred and grey until the pile nears the answer. Any size, mipmaps on; it is cropped to fill the wall.")]
        public Texture2D leftSlide;
        [Tooltip("Picture the right lamp projects.")] public Texture2D rightSlide;
        [Tooltip("Looping layers of one length: the first always plays, the others join one per tier while a low-pass opens.")]
        public AudioClip[] layers=Array.Empty<AudioClip>();
        [Range(0,1)] public float cueVolume=.5f;
        [Tooltip("Warmth that reaches tiers 1-3 (stirring, warm, hot); passing is tier 4.")] public Vector3 tiers=new Vector3(.35f,.60f,.80f);
        [Tooltip("Further than this from the answer (degrees) the cue stays at tier 1: lookalike poses (mirrors, flips) score warm too.")]
        public float lookalikeAngle=50;
        [Tooltip("The music only warms within this many degrees of an answer; further out it stays cold however warm the shadow scores (playtest 2026-11-08: lookalikes swelled it and misled).")]
        public float cueAngle=30;
        [Tooltip("Let go within snapAngle degrees of the answer, passing or at snapWarmth, and the orb turns itself into the answer.")]
        public float snapWarmth=.90f,snapAngle=20;
        [Tooltip("Where the host allows it (Torch2Workshop.SnapWhileHeld): turned within this many degrees of an answer while still held, the orb leaves the hand, turns into the answer and the level is solved.")]
        public float heldSnapAngle=8;
        [Header("Drawn closer (Shape of Memory only; the shadow and the score keep the real places)")]
        [Tooltip("Pieces further than drawGatherFrom from the hub are drawn with that extra distance times this (1 = as placed). Two-wall piles spread wide, since each piece's two shadows pin where it is; this tightens the look without touching either picture. Their shadows then swing a little more than the drawn pieces as the orb turns.")]
        [Range(.2f,1)] public float drawGather=1;
        [Tooltip("Metres from the hub within which pieces are drawn where they are.")] public float drawGatherFrom=.12f;
        // Where a piece at position (pile space) is drawn when the host draws the pile gathered.
        public Vector3 Drawn(Vector3 position)=>Gathered(position,drawGather,drawGatherFrom);
        public static Vector3 Gathered(Vector3 position,float gather,float from)
        {
            float d=position.magnitude;return gather>=1||d<=from?position:position*((from+(d-from)*gather)/d);
        }
        [Header("Table to orb (Torch2Transit)")]
        [Tooltip("Seconds each thing takes between the table and its place in the orb.")] public float transitFly=.8f;
        [Tooltip("How much of one flight the next may share (0 = strictly one at a time).")] [Range(0,.8f)] public float transitOverlap=.3f;
        public Texture2D Slide(int wall)=>wall==0?leftSlide:rightSlide;
        public static Torch2Level Load(string id)=>string.IsNullOrEmpty(id)?null:Resources.Load<Torch2Level>(ResourceFolder+id);
    }
}
