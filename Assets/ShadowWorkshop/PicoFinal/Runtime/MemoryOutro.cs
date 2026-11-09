using System;
using CloudGarden.ShadowPuzzle;
using UnityEngine;

namespace ShapeOfMemory
{
    // Leaving a level, the intro played backwards: the lamps go out, the orb opens (rings and hub shrink, wires draw in),
    // the pile's things go back to their places on the table one at a time, the last in first out, faster than they came
    // (Torch2Transit in reverse), and shrink away; the next level's things then grow in where they lie (MemoryIntro).
    // keep: they stay on the table instead (a found memory, until its page is turned); Vanish then shrinks them away.
    // Input and the level's own updates are off throughout; done runs at the end.
    public sealed class MemoryOutro : MonoBehaviour
    {
        public const float Dim=.35f,Open=.3f,FallFrom=.25f,Speed=1.8f,Vanish=.3f;
        public bool Running{get;private set;}
        public float Length{get;private set;}
        Torch2Workshop game;
        MemorySounds sounds;
        Action done;
        AudioSource voice;
        float time,fallEnd;
        bool keep,vanishing;
        Torch2Transit transit;
        bool[] landed;
        Part[] parts;
        Transform[] orb;
        Vector3[] orbScales;

        struct Part{public Transform transform,wire;public Vector3 scale,wirePosition,wireScale;}
        public static MemoryOutro Play(Torch2Workshop game,MemorySounds sounds,Action done,bool keep=false)
        {
            var outro=game.gameObject.AddComponent<MemoryOutro>();outro.keep=keep;outro.Begin(game,sounds,done);return outro;
        }
        // Whatever of the level is still showing (the things on the table, or an orb folded away) shrinks to nothing over
        // seconds, then done.
        public static void Shrink(Torch2Workshop game,float seconds,Action done)
        {
            var outro=game.GetComponent<MemoryOutro>();
            if(!outro){outro=game.gameObject.AddComponent<MemoryOutro>();outro.keep=true;outro.Begin(game,null,null);outro.time=outro.Length;outro.Running=false;}
            outro.StartShrink(seconds,done);
        }
        float shrinkTime,shrinkSeconds;Vector3[] shrinkScales;Transform[] shrinking;
        void StartShrink(float seconds,Action then)
        {
            var all=new System.Collections.Generic.List<Transform>();
            foreach(var p in parts){if(p.transform)all.Add(p.transform);if(p.wire)all.Add(p.wire);}
            foreach(var o in orb)if(o)all.Add(o);
            foreach(var light in game.GetComponentsInChildren<MemoryFlashlight>())all.Add(light.transform);
            shrinking=all.ToArray();shrinkScales=new Vector3[shrinking.Length];for(int i=0;i<shrinking.Length;i++)shrinkScales[i]=shrinking[i].localScale;
            shrinkTime=0;shrinkSeconds=Mathf.Max(.01f,seconds);vanishing=true;done=then;Running=true;
        }
        void Begin(Torch2Workshop game,MemorySounds sounds,Action done)
        {
            this.game=game;this.sounds=sounds;this.done=done;Running=true;
            // The level stops scoring and refreshing (which would bring the hint and found cards back); the pile stays still.
            game.input.enabled=false;game.enabled=false;
            foreach(Transform child in game.transform)if(child.name.StartsWith("Picture card"))child.gameObject.SetActive(false);
            // Planned for the pile where it stands (the found pose), then played from the end.
            var world=game.world;int n=world.Pieces.Length;transit=new Torch2Transit(game.level,game.Rotation,game.Position,game.Drawn);landed=new bool[transit.movers.Length];
            parts=new Part[n];
            for(int i=0;i<n;i++)
            {
                var t=world.Pieces[i];parts[i]=new Part{transform=t,wire=world.Wires[i],scale=t.localScale};
                if(parts[i].wire){parts[i].wirePosition=parts[i].wire.localPosition;parts[i].wireScale=parts[i].wire.localScale;}
            }
            fallEnd=FallFrom+transit.Length/Speed;Length=keep?fallEnd:fallEnd+Vanish;
            orb=world.OrbParts;orbScales=new Vector3[orb.Length];for(int i=0;i<orb.Length;i++)orbScales[i]=orb[i].localScale;
            voice=gameObject.AddComponent<AudioSource>();voice.playOnAwake=false;voice.spatialBlend=0;
            if(sounds!=null){voice.pitch=.8f;voice.PlayOneShot(sounds.Lamp,.4f);}
        }
        void Update()
        {
            if(!Running)return;
            if(vanishing)
            {
                shrinkTime+=Time.unscaledDeltaTime;float s=1-MemoryIntro.Smooth(shrinkTime/shrinkSeconds);
                for(int i=0;i<shrinking.Length;i++)if(shrinking[i])shrinking[i].localScale=shrinkScales[i]*s;
                if(shrinkTime>=shrinkSeconds){vanishing=false;Running=false;var then=done;done=null;then?.Invoke();}
                return;
            }
            time+=Time.unscaledDeltaTime;Pose(time);
            if(time>=Length){Running=false;var then=done;done=null;then?.Invoke();}
        }
        void Pose(float t)
        {
            game.world.LampLevel=1-MemoryIntro.Smooth(t/Dim);
            float open=MemoryIntro.Smooth((t-.1f)/Open);
            for(int i=0;i<orb.Length;i++){orb[i].localScale=orbScales[i]*(1-open);orb[i].gameObject.SetActive(open<1);}
            float vanish=keep?1:1-MemoryIntro.Smooth((t-fallEnd)/Vanish);
            // Transit time, running backwards from the pile to the table.
            float back=transit.Length-Mathf.Max(0,t-FallFrom)*Speed;
            for(int k=0;k<transit.movers.Length;k++)if(back<=transit.movers[k].start&&!landed[k]){landed[k]=true;if(sounds!=null){voice.pitch=.85f+.1f*(k%3);voice.PlayOneShot(sounds.Settle,.15f);}}
            for(int i=0;i<parts.Length;i++)
            {
                ref var p=ref parts[i];
                if(p.wire){p.wire.localPosition=p.wirePosition*(1-open);p.wire.localScale=new Vector3(p.wireScale.x,p.wireScale.y*(1-open),p.wireScale.z);p.wire.gameObject.SetActive(open<1);}
                // Growing back to its own size on the way to the table (the orb is drawn smaller, MemoryIntro.Place).
                MemoryIntro.Place(game,p.transform,p.scale*vanish,transit.Evaluate(i,back),MemoryIntro.InOrb(transit,i,back));
            }
        }
    }
}
