using System.Collections.Generic;
using CloudGarden.ShadowPuzzle;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShapeOfMemory
{
    // The moment a memory is found, played where the player is looking (the orb and its shadow, not the book): bells ring
    // up, gold glints burst out of the pile, and a plaque with the memory's name under MEMORY FOUND grows over the orb,
    // facing the player, then shrinks away. By then the book's page carries the picture and its edge beckons to be
    // turned. It lives on the level, so leaving the level ends it.
    public sealed class MemoryFound : MonoBehaviour
    {
        // How long the plaque stays (growing in, then shrinking away at the end), and how high over the orb's middle it
        // sits: above the rings and the top of the found picture on the wall behind.
        public const float Seconds=3.4f,Grow=.45f,Fade=.8f,Above=.43f,Rise=.04f;
        public const int Glints=90;
        public static readonly Color Gold=MemoryPalette.Gold;
        Transform head,banner;
        Vector3 at;
        readonly List<TMP_Text> words=new List<TMP_Text>();
        float time;

        public static MemoryFound Play(Torch2Workshop level,Transform head,MemoryKit kit,Material glow,AudioClip bells)
        {
            var world=level.world;var middle=world.OrbParts.Length>0?world.OrbParts[world.OrbParts.Length-1].position:level.transform.TransformPoint(Torch2Projection.Home);
            var found=new GameObject("Memory found").AddComponent<MemoryFound>();found.transform.SetParent(level.transform,false);found.transform.position=middle;
            found.head=head;found.at=middle+level.transform.up*Above*level.transform.lossyScale.y;
            found.Words(level.level,kit);found.Burst(glow,kit.Owned);
            var voice=found.gameObject.AddComponent<AudioSource>();voice.spatialBlend=0;voice.playOnAwake=false;if(bells)voice.PlayOneShot(bells,.5f);
            found.Update();return found;
        }
        // On a deep leather plaque with a gold edge and a bright gold line inside it: it reads over a lit wall as well as a
        // dark one. The memory's name in its chapter's Light accent.
        void Words(Torch2Level level,MemoryKit kit)
        {
            banner=new GameObject("Banner").transform;banner.SetParent(transform,false);
            MemoryUi.Shape(PrimitiveType.Quad,"Plaque edge",banner,new Vector3(0,.012f,.003f),Quaternion.identity,new Vector3(.64f,.2f,1),kit.Screen,Gold);
            MemoryUi.Shape(PrimitiveType.Quad,"Plaque line",banner,new Vector3(0,.012f,.0015f),Quaternion.identity,new Vector3(.628f,.188f,1),kit.Screen,MemoryPalette.GoldBright);
            MemoryUi.Shape(PrimitiveType.Quad,"Plaque",banner,new Vector3(0,.012f,0),Quaternion.identity,new Vector3(.62f,.18f,1),kit.Screen,MemoryPalette.LeatherDeep);
            words.Add(MemoryUi.Label("MEMORY FOUND",banner,new Vector3(0,.06f,-.004f),Quaternion.identity,.03f,new Vector2(.56f,.05f),Gold));
            words[0].characterSpacing=18;words[0].fontStyle=FontStyles.Bold;
            words.Add(MemoryUi.Label(level.button,banner,new Vector3(0,-.012f,-.004f),Quaternion.identity,.075f,new Vector2(.56f,.1f),MemoryPalette.Light(MemoryPalette.ChapterOf(level))));
            words[1].fontStyle=FontStyles.Bold;words[1].characterSpacing=6;
            foreach(var word in words){word.textWrappingMode=TextWrappingModes.NoWrap;word.enableAutoSizing=true;word.fontSizeMin=word.fontSize*.4f;word.fontSizeMax=word.fontSize;}
        }
        // Small gold diamonds flung out of the pile, slowing and shrinking away (unlit, so they read in any light): half Gold,
        // half GoldBright, each half its own system (the unlit material takes one colour).
        void Burst(Material glow,List<Object> owned)
        {
            foreach(var colour in new[]{Gold,MemoryPalette.GoldBright})Burst(glow,owned,colour,Glints/2);
        }
        void Burst(Material glow,List<Object> owned,Color colour,int count)
        {
            var go=new GameObject("Glints");go.transform.SetParent(transform,false);
            var system=go.AddComponent<ParticleSystem>();system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=system.main;main.duration=1;main.loop=false;main.playOnAwake=false;main.useUnscaledTime=true;main.simulationSpace=ParticleSystemSimulationSpace.World;
            main.startLifetime=new ParticleSystem.MinMaxCurve(.9f,1.7f);main.startSpeed=new ParticleSystem.MinMaxCurve(.25f,.7f);main.startSize=new ParticleSystem.MinMaxCurve(.008f,.018f);
            main.startRotation3D=true;main.startRotationX=main.startRotationY=main.startRotationZ=new ParticleSystem.MinMaxCurve(0,Mathf.PI*2);main.gravityModifier=.04f;main.maxParticles=count;
            var emission=system.emission;emission.rateOverTime=0;emission.SetBursts(new[]{new ParticleSystem.Burst(0,(short)(count*2/3)),new ParticleSystem.Burst(.2f,(short)(count/3))});
            var shape=system.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=.1f;
            var drag=system.limitVelocityOverLifetime;drag.enabled=true;drag.limit=10;drag.drag=2.2f;
            var size=system.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,1),new Keyframe(.6f,.8f),new Keyframe(1,0)));
            var spin=system.rotationOverLifetime;spin.enabled=true;spin.separateAxes=true;spin.x=spin.y=spin.z=new ParticleSystem.MinMaxCurve(-6,6);
            var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.Mesh;renderer.mesh=Diamond(owned);
            var material=new Material(glow){name="Found glint"};material.SetColor("_BaseColor",colour);owned.Add(material);
            renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            system.Play();
        }
        static Mesh diamond;
        // An octahedron, unit across: eight faces are plenty for a glint.
        static Mesh Diamond(List<Object> owned)
        {
            if(diamond)return diamond;
            diamond=new Mesh{name="Glint"};owned.Add(diamond);
            diamond.vertices=new[]{Vector3.up*.5f,Vector3.down*.5f,Vector3.left*.5f,Vector3.right*.5f,Vector3.forward*.5f,Vector3.back*.5f};
            diamond.triangles=new[]{0,4,3,0,3,5,0,5,2,0,2,4,1,3,4,1,5,3,1,2,5,1,4,2};diamond.RecalculateNormals();diamond.RecalculateBounds();
            return diamond;
        }
        void Update()
        {
            time+=Time.unscaledDeltaTime;
            if(time>=Seconds+.1f){Destroy(gameObject);return;}
            // Grows in with a small overshoot, rises a little, shrinks away at the end; always facing the head, upright.
            float grow=Mathf.Clamp01(time/Grow),fade=Mathf.Clamp01((time-(Seconds-Fade))/Fade);
            float scale=(1+2.2f*Mathf.Pow(grow-1,3)+1.2f*Mathf.Pow(grow-1,2))*(1-fade*fade);
            banner.position=at+Vector3.up*Rise*Mathf.Clamp01(time/Seconds);banner.localScale=Vector3.one*Mathf.Max(0,scale);
            if(head){var away=Vector3.ProjectOnPlane(banner.position-head.position,Vector3.up);if(away.sqrMagnitude>1e-4f)banner.rotation=Quaternion.LookRotation(away,Vector3.up);}
        }
    }
}
