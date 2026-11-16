using System;
using System.Collections.Generic;
using CloudGarden.ShadowPuzzle;
using UnityEngine;

namespace ShapeOfMemory
{
    // A chapter's own found moment: its sounds, and paper cut-outs that come alive on the lit page round the found shadow
    // (clouds drifting past Bon Voyage's plane, the birthday candles lighting one by one). MemorySolveMoment plays it as
    // the memory is found, next to the bells and the MEMORY FOUND plaque that every chapter shares.
    // One asset per chapter in Resources/ShapeOfMemory/Cues, matched to its chapter by level (MetaHackthone > Pico Final >
    // Solve cues). A level without one plays only the shared moment.
    // Places and sizes are in spot units: the lamp's spot on its page, (0,0) its middle, 1 its radius (x across the page
    // as the player sees it, y up). Every page has the same spot, so a cue authored on one chapter's picture stays there.
    [CreateAssetMenu(menuName="Shape of Memory/Solve cue")]
    public sealed class MemorySolveCue : ScriptableObject
    {
        public const string ResourceFolder="ShapeOfMemory/Cues";
        // The cut-outs' atlas (Tools/ShadowArt/build_cue_atlas.py): Art cells in a Columns x Columns grid, read row by
        // row from the top left.
        public const int Columns=4;
        public enum Art{CloudA,CloudB,CloudSmall,Bird,Confetti,Streamer,Balloon,Flame,Puff,Sparkle,Heart,Steam,Sun,Dash,Shard,Bang}
        public enum Spawn{Random,Line,Points}
        public enum Depth{Behind,Front}

        [Serializable]
        public sealed class Sound
        {
            public AudioClip clip;
            [Tooltip("Seconds after the memory is found.")]
            [Min(0)] public float delay;
            [Range(0,1)] public float volume=.7f;
        }

        [Serializable]
        public sealed class Layer
        {
            [Tooltip("What these are, for the reader of this asset (not shown in the game).")]
            public string note;
            public Art art;
            [Tooltip("Which lit page: 0 the level's first lamp's, 1 its second's (two-wall levels), -1 every lit page.")]
            public int wall=-1;
            [Tooltip("Behind: under the shadow (hidden where the shadow is). Front: over it.")]
            public Depth depth;
            [Tooltip("Each cut-out takes one of these, in turn. Alpha is its strength.")]
            public Color[] tints={Color.white};
            [Min(1)] public int count=6;
            [Tooltip("Random: anywhere in the box from-to. Line: evenly from from to to, in order. Points: at points, in order.")]
            public Spawn spawn;
            public Vector2 from=new Vector2(-.8f,-.8f),to=new Vector2(.8f,.8f);
            public Vector2[] points=Array.Empty<Vector2>();
            [Tooltip("Seconds after the memory is found that the first appears; the rest follow over stagger (in order for Line and Points, at random otherwise).")]
            [Min(0)] public float delay,stagger;
            [Tooltip("How long each lasts, seconds (min, max).")]
            public Vector2 life=new Vector2(3,4);
            [Tooltip("Above 0: each lasts until this many seconds after the moment starts, whatever life says (things that end together: candles blown out).")]
            [Min(0)] public float until;
            [Tooltip("Each comes back where it started when its life ends, until the moment is over.")]
            public bool loop;
            [Tooltip("Size: its height in spot radii (min, max).")]
            public Vector2 size=new Vector2(.1f,.15f);
            [Tooltip("Spot radii per second, plus up to jitter of that at random each way.")]
            public Vector2 velocity;
            [Min(0)] public float jitter;
            [Tooltip("Spot radii per second each second, downward (negative floats up).")]
            public float gravity;
            [Min(0)] public float drag;
            [Tooltip("A side-to-side sway (spot radii) and how often it swings (per second).")]
            public float sway,swayRate=.6f;
            [Tooltip("Degrees per second (min, max); half of them turn the other way.")]
            public Vector2 spin;
            [Tooltip("Its turn on the page in degrees, plus up to tilt at random each way. Along the line: angle is added to the line's own.")]
            public float angle,tilt;
            public bool alongLine;
            [Tooltip("Half of them face the other way.")]
            public bool flipRandom;
            [Tooltip("Seconds to grow in (with a small overshoot) and to fade out at the end of its life.")]
            [Min(0)] public float pop=.25f,fadeOut=.5f;
            [Tooltip("How much its size flickers (a flame).")]
            [Range(0,1)] public float flicker;
            [Tooltip("Drifting across from-to, it comes back in on the other side.")]
            public bool wrap;
            [Tooltip("How much of its strength it keeps off the lamp's spot, on the dark page.")]
            [Range(0,1)] public float inDark=.3f;
        }

        [Tooltip("The chapter this is for.")]
        public Torch2Level level;
        public Sound[] sounds=Array.Empty<Sound>();
        public Layer[] layers=Array.Empty<Layer>();
        [Tooltip("How long the page's cut-outs last, seconds. They fade out by then, and with the lamps when the memory's things go back to the table.")]
        [Min(1)] public float seconds=5.5f;

        // The cue for a level, or null (cached; tests can Clear it).
        static Dictionary<string,MemorySolveCue> byLevel;
        public static MemorySolveCue For(Torch2Level level)
        {
            if(!level)return null;
            var map=byLevel;
            if(map==null)
            {
                // Built aside: in the editor, loading the assets runs their OnValidate, which clears byLevel.
                map=new Dictionary<string,MemorySolveCue>();
                foreach(var cue in Resources.LoadAll<MemorySolveCue>(ResourceFolder))
                    if(cue&&cue.level){if(map.ContainsKey(cue.level.name))Debug.LogWarning($"SHAPE_OF_MEMORY_CUE two cues for {cue.level.name}: {cue.name} ignored");else map[cue.level.name]=cue;}
                byLevel=map;
            }
            return map.TryGetValue(level.name,out var found)?found:null;
        }
        public static void Clear(){byLevel=null;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetOnPlay(){byLevel=null;}
#if UNITY_EDITOR
        void OnValidate(){byLevel=null;}
#endif

        // An Art cell's rectangle in the atlas (uv, y up).
        public static Rect Cell(Art art)
        {
            int i=(int)art,x=i%Columns,y=i/Columns;float s=1f/Columns;
            return new Rect(x*s,1-(y+1)*s,s,s);
        }
    }
}
