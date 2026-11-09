using System;
using System.Collections.Generic;
using UnityEngine;

namespace CloudGarden.ShadowPuzzle
{
    // One closeness meter in tiers: 0 cold, 1-3 from warmth, 4 passing. A tier is entered at its threshold and left
    // Hysteresis below it, only after the change has held for a moment (shorter going up), so hand tremor (.01-.08 warmth
    // per degree on the shipped levels) never flickers it. Passing is entered at once. Shown eases toward the tier.
    public sealed class Torch2Tier
    {
        public const float Hysteresis=.08f,UpDwell=.25f,DownDwell=.6f,UpEase=.25f,DownEase=.6f;
        public int Tier{get;private set;}
        public float Shown{get;private set;}
        int candidate;
        float held;
        public static int Target(float warmth,bool passing,bool lookalike,Vector3 tiers,int current)
        {
            if(passing)return 4;
            int target=0;for(int i=1;i<=3;i++)if(warmth>=tiers[i-1]-(i<=current?Hysteresis:0))target=i;
            return lookalike?Mathf.Min(target,1):target;
        }
        // +1 when the tier went up in this update, -1 when it went down, otherwise 0.
        public int Update(float warmth,bool passing,bool lookalike,Vector3 tiers,float dt)
        {
            int target=Target(warmth,passing,lookalike,tiers,Tier),change=0;
            if(target==Tier){candidate=Tier;held=0;}
            else
            {
                if(target!=candidate){candidate=target;held=0;}
                held+=dt;
                if(target==4||held>=(target>Tier?UpDwell:DownDwell)){change=target>Tier?1:-1;Tier=target;held=0;}
            }
            Shown=Mathf.Lerp(Shown,Tier,1-Mathf.Exp(-dt/(Tier>Shown?UpEase:DownEase)));
            if(Mathf.Abs(Shown-Tier)<.002f)Shown=Tier;
            return change;
        }
        public void Reset(){Tier=candidate=0;Shown=held=0;}
    }

    // How close the pile is, without numbers: each lit lamp projects a slide that comes into focus and colour as its own
    // wall nears the picture (so on two-wall levels the lagging wall shows), and looping layers join one per tier while a
    // low-pass opens. Warmth drives the tiers, but further than lookalikeAngle from the answer they stop at 1: on one-wall
    // levels most random poses that score 0.6 or more are mirrors and flips far from the answer. The layers are stricter:
    // past cueAngle they stay cold (tier 0). Angles are to the nearest answer, flips the level accepts included.
    public sealed class Torch2Cue : MonoBehaviour
    {
        // Per tier, 0 cold to 4 passing or found: slide blur as a fraction of its width, saturation, how strongly the slide
        // tints the lamp light, and the layers' low-pass cutoff (Hz).
        static readonly float[] Blur={.09f,.05f,.022f,.008f,0},Saturation={0,.15f,.45f,.8f,1},Mix={.35f,.5f,.7f,.85f,1},Cutoff={450,1100,3000,8000,22000};
        public Torch2Workshop game;
        public readonly Torch2Tier Overall=new Torch2Tier();
        public Torch2Tier[] Walls{get;private set;}
        public AudioSource[] Layers{get;private set;}
        AudioLowPassFilter[] filters;
        AudioSource tick;
        Torch2Room room;
        float[] shownWalls;
        float shownAudio,shownLevel=-1;
        // How loud the layers are overall: 0 while the lamps are off (an intro), 1 in play. No tier ticks below half.
        public float Level=1;

        public static void Look(float tier,out float blur,out float saturation,out float mix){blur=Curve(Blur,tier);saturation=Curve(Saturation,tier);mix=Curve(Mix,tier);}
        static float Curve(float[] values,float tier){tier=Mathf.Clamp(tier,0,values.Length-1);int i=Mathf.Min((int)tier,values.Length-2);return Mathf.Lerp(values[i],values[i+1],tier-i);}
        // In octaves, so each tier opens the sound by a similar amount.
        public static float CutoffAt(float tier){tier=Mathf.Clamp(tier,0,4);int i=Mathf.Min((int)tier,3);return Cutoff[i]*Mathf.Pow(Cutoff[i+1]/Cutoff[i],tier-i);}
        // The first layer always plays; of the rest, spread over tiers 1-3, each fades in across the tier below its own.
        public static float LayerVolume(int layer,int count,float tier)
        {
            if(layer==0)return .6f+.4f*Mathf.Clamp01(tier/4);
            float joins=layer*3f/Mathf.Max(1,count-1);return Mathf.Clamp01(tier-(joins-1));
        }
        // The level's slide for a wall, or a generated placeholder (added to owned).
        public static Texture2D SlideFor(Torch2Level level,int wall,List<UnityEngine.Object> owned)
        {
            var slide=level.Slide(wall);if(slide)return slide;
            var made=Torch2Placeholders.Slide(level.name+"/"+wall,level.accent);owned.Add(made);return made;
        }
        public static AudioClip[] LayersFor(Torch2Level level)=>level.layers!=null&&level.layers.Length>0&&Array.TrueForAll(level.layers,c=>c)?level.layers:Torch2Placeholders.Layers();

        public void Build(Torch2Workshop game,Torch2Room room,List<UnityEngine.Object> owned)
        {
            this.game=game;this.room=room;var walls=game.Projection.walls;
            Walls=new Torch2Tier[walls.Length];shownWalls=new float[walls.Length];
            for(int j=0;j<walls.Length;j++){Walls[j]=new Torch2Tier();room.Slide(walls[j],SlideFor(game.level,walls[j],owned));}
            // From between the lit walls; all layers start on the same sample so equal-length loops stay locked together.
            var clips=LayersFor(game.level);var at=Vector3.zero;foreach(int k in walls)at+=room.wallCentres[k];at/=walls.Length;
            Layers=new AudioSource[clips.Length];filters=new AudioLowPassFilter[clips.Length];double start=AudioSettings.dspTime+.1;
            for(int i=0;i<clips.Length;i++)
            {
                var go=new GameObject("Cue layer "+i);go.transform.SetParent(transform,false);go.transform.localPosition=at;
                var source=go.AddComponent<AudioSource>();source.clip=clips[i];source.loop=true;source.playOnAwake=false;source.volume=0;source.dopplerLevel=0;
                source.spatialBlend=.6f;source.rolloffMode=AudioRolloffMode.Linear;source.minDistance=1;source.maxDistance=8;
                filters[i]=go.AddComponent<AudioLowPassFilter>();source.PlayScheduled(start);Layers[i]=source;
            }
            tick=gameObject.AddComponent<AudioSource>();tick.playOnAwake=false;tick.spatialBlend=0;tick.volume=.08f;tick.clip=Torch2Placeholders.Tick();
            Apply(true);
        }
        void Update()
        {
            if(!game||Walls==null)return;
            var p=game.Projection;var level=game.level;float dt=Time.unscaledDeltaTime;
            float angle=p.AngleToAnswer(game.Rotation);bool lookalike=angle>level.lookalikeAngle;
            // The music (and its tick) only warms within cueAngle of an answer: further out it reads cold however warm the
            // shadow scores, so a lookalike never sounds close. The slides keep the wider lookalikeAngle.
            float heard=angle>level.cueAngle?0:p.Score.warmth;
            if(Overall.Update(heard,game.Completed||p.Score.Ready,lookalike,level.tiers,dt)>0&&Level>.5f){tick.pitch=1+.12f*Overall.Tier;tick.PlayOneShot(tick.clip);}
            for(int j=0;j<Walls.Length;j++)Walls[j].Update(p.scores[j].warmth,game.Completed||p.scores[j].Ready,lookalike,level.tiers,dt);
            Apply(false);
        }
        void Apply(bool force)
        {
            for(int j=0;j<Walls.Length;j++)
            {
                float shown=Walls[j].Shown;if(!force&&shown==shownWalls[j])continue;shownWalls[j]=shown;
                Look(shown,out float blur,out float saturation,out float mix);room.Focus(game.Projection.walls[j],blur,saturation,mix);
            }
            float tier=Overall.Shown;if(!force&&tier==shownAudio&&Level==shownLevel)return;shownAudio=tier;shownLevel=Level;
            for(int i=0;i<Layers.Length;i++){Layers[i].volume=game.level.cueVolume*Level*LayerVolume(i,Layers.Length,tier);filters[i].cutoffFrequency=CutoffAt(tier);}
        }
        public void ResetTiers(){if(Walls==null)return;Overall.Reset();foreach(var wall in Walls)wall.Reset();Apply(true);}
    }
}
