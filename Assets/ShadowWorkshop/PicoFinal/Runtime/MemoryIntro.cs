using System;
using System.Collections.Generic;
using CloudGarden.ShadowPuzzle;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShapeOfMemory
{
    // A level's opening. The pile's things grow in where they lie on the table (the level's table layout, or scattered;
    // where the last level's shrank away, MemoryOutro), with the lamps off (the chapter's name is on the book); one at a time
    // they lift and float into their places in the orb (Torch2Transit), at the level's start rotation (never the answer).
    // The wires then draw out to the hub, the rings close around it and the lamps click on, which lights the walls,
    // shows the shadow and starts the cue. Until then the walls are dark, so no shadow shows early, and input is off.
    // On the desktop a level whose intro was seen before can be skipped with a click or Space. On a headset it always
    // plays through: a held pinch used to skip it, and relaxed or just re-tracked hands read as one (2026-11-07 device
    // logs: three unwanted skips, one 0.35 s after the left hand came back from a tracking drop).
    public sealed class MemoryIntro : MonoBehaviour
    {
        public const float Appear=.35f,Lead=.45f,DrawWires=.3f,CloseRings=.4f,Flicker=.4f,Rest=.3f;
        public bool Running{get;private set;}
        public bool Skippable{get;private set;}
        public float Length{get;private set;}
        Torch2Workshop game;
        MemorySounds sounds;
        AudioSource[] voices;
        int voice;
        float time;
        bool lampsClicked,silent;
        Torch2Transit transit;
        Part[] parts;
        bool[] flown,landed;
        Transform[] orb;
        Vector3[] orbScales;
        float flyEnd,lampsAt;

        struct Part{public Transform transform,wire;public Vector3 wirePosition,wireScale,scale;}
        public Torch2Transit Transit=>transit;

        public static MemoryIntro Play(Torch2Workshop game,bool skippable,MemorySounds sounds)
        {
            var intro=game.gameObject.AddComponent<MemoryIntro>();intro.Begin(game,skippable,sounds);return intro;
        }
        void Begin(Torch2Workshop game,bool skippable,MemorySounds sounds)
        {
            this.game=game;this.sounds=sounds;Skippable=skippable;Running=true;game.input.enabled=false;
            var world=game.world;var level=game.level;transit=new Torch2Transit(level,game.Rotation,game.Position,game.Drawn);
            int n=world.Pieces.Length;parts=new Part[n];flown=new bool[transit.movers.Length];landed=new bool[transit.movers.Length];
            for(int i=0;i<n;i++)
            {
                var t=world.Pieces[i];parts[i]=new Part{transform=t,wire=world.Wires[i],scale=t.localScale};
                if(parts[i].wire){parts[i].wirePosition=parts[i].wire.localPosition;parts[i].wireScale=parts[i].wire.localScale;}
            }
            flyEnd=Lead+transit.Length;lampsAt=flyEnd+DrawWires+.1f;Length=lampsAt+Flicker+Rest;
            orb=world.OrbParts;orbScales=new Vector3[orb.Length];for(int i=0;i<orb.Length;i++)orbScales[i]=orb[i].localScale;
            voices=new AudioSource[4];for(int i=0;i<voices.Length;i++){voices[i]=gameObject.AddComponent<AudioSource>();voices[i].playOnAwake=false;voices[i].spatialBlend=0;}
            world.LampLevel=0;Pose(0);
        }
        // Tests and captures: hold the timeline still, or show a given moment.
        public bool Frozen;
        public void Seek(float t){if(!Running)return;time=Mathf.Clamp(t,0,Length);Pose(time);}
        void Update()
        {
            // Never while it waits (the book still rising).
            if(!Running||Frozen)return;
            if(Skippable&&SkipPressed()){Debug.Log("SHAPE_OF_MEMORY_INTRO skipped");Finish();return;}
            time+=Time.unscaledDeltaTime;Pose(time);
            if(time>=Length)Finish();
        }
        // Everything at its place for time t.
        void Pose(float t)
        {
            float flight=t-Lead;
            for(int k=0;k<transit.movers.Length;k++)
            {
                var m=transit.movers[k];
                if(flight>m.start&&flight<m.end&&!flown[k]){flown[k]=true;Say(sounds.Whoosh,.30f,.85f+.3f*(k%5)/4f);}
                if(flight>=m.end&&!landed[k]&&t>0){landed[k]=true;Say(sounds.Settle,.22f,.9f+.2f*(k%3)/2f);}
            }
            for(int i=0;i<parts.Length;i++)
            {
                ref var p=ref parts[i];
                Place(game,p.transform,p.scale*Smooth(t/Appear),transit.Evaluate(i,flight),InOrb(transit,i,flight));
                if(p.wire)
                {
                    float w=Ease(Mathf.Clamp01((t-flyEnd)/DrawWires));
                    p.wire.localPosition=p.wirePosition*w;p.wire.localScale=new Vector3(p.wireScale.x,p.wireScale.y*w,p.wireScale.z);p.wire.gameObject.SetActive(w>0);
                }
            }
            float ring=Mathf.Clamp01((t-flyEnd-.1f)/CloseRings);
            for(int i=0;i<orb.Length;i++){orb[i].localScale=orbScales[i]*Back(ring);orb[i].gameObject.SetActive(ring>0);}
            // The lamps catch, dip and come on.
            float f=t-lampsAt;float lamp=f<0?0:f<.06f?.7f:f<.16f?.15f:Mathf.Clamp01(.15f+(f-.16f)/(Flicker-.16f)*.85f);
            if(f>=0&&!lampsClicked){lampsClicked=true;Say(sounds.Lamp,.5f,1);}
            if(game.world.LampLevel!=lamp)game.world.LampLevel=lamp;
        }
        // Table space is the level root's space (the table top is y = 0); pieces live under the pile, which is drawn at the
        // orb's scale (Torch2Input.OrbScale, MemoryGame.PileScale). A piece lies on the table at its own size and shrinks
        // toward the hub as it flies in, to sit in its place at the orb's scale. inOrb: 0 on the table .. 1 in its place.
        public static void Place(Torch2Workshop game,Transform piece,Vector3 scale,Pose pose,float inOrb)
        {
            float s=Mathf.Max(1e-4f,game.input.OrbScale),f=Mathf.Lerp(1,s,inOrb);var inverse=Quaternion.Inverse(game.Rotation);
            piece.localPosition=inverse*((pose.position-game.Position)*(f/s));piece.localRotation=inverse*pose.rotation;piece.localScale=scale*(f/s);
        }
        // How far a piece's flight has gone at transit time t (0 still on the table, 1 landed), eased.
        public static float InOrb(Torch2Transit transit,int piece,float t){var m=transit.movers[transit.moverOf[piece]];return Smooth((t-m.start)/Mathf.Max(1e-4f,m.end-m.start));}
        static float Ease(float t)=>1-(1-t)*(1-t)*(1-t);
        public static float Smooth(float t){t=Mathf.Clamp01(t);return t*t*(3-2*t);}
        static float Back(float t){const float s=1.7f;t-=1;return t*t*((s+1)*t+s)+1;}
        void Say(AudioClip clip,float volume,float pitch){if(!clip||silent)return;var source=voices[voice++%voices.Length];source.pitch=pitch;source.PlayOneShot(clip,volume);}
        // Jumps to the end: everything in place, lamps on. A skip lands silently.
        public void Finish()
        {
            if(!Running)return;
            silent=time<Length;Pose(Length);silent=false;for(int i=0;i<orb.Length;i++)orb[i].localScale=orbScales[i];
            game.world.LampLevel=1;Running=false;game.input.enabled=true;
        }
        // Desktop only (a click or Space); a headset never skips.
        bool SkipPressed()
        {
            if(game.xr&&game.xr.Active)return false;
            var mouse=Mouse.current;var keyboard=Keyboard.current;
            return mouse!=null&&mouse.leftButton.wasPressedThisFrame||keyboard!=null&&keyboard.spaceKey.wasPressedThisFrame;
        }
    }
}
