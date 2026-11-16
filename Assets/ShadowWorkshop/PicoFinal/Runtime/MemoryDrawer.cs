using System;
using System.Collections;
using System.Collections.Generic;
using ShapeOfMemory.ArtDressing;
using UnityEngine;

namespace ShapeOfMemory
{
    // The reading stand's drawer, as part of a memory's start: PLAY (or PLAY AGAIN) on the chapter's page opens it (Open),
    // the memory starts once it is out, gold motes fly from it to where the memory's things come up on the table (Burst),
    // and it slides shut as the stand sinks. Nothing else: no hand, look or press moves it. (2026-11-15 evening: as the
    // way to start a memory, players didn't notice it or found it hard to work, so PLAY went back onto the page and the
    // drawer's cues went: its light, glow, halo, band, motes off the page, hum and hint.) It drives the stand's own
    // mechanism (BookStandDrawer, the environment artist's: slide only). Device log: SHAPE_OF_MEMORY_DRAWER.
    public sealed class MemoryDrawer : MonoBehaviour
    {
        public const float RunOut=.22f,ShutAfter=.35f,ShutSeconds=.4f,MoteSeconds=1.3f;
        public BookStandDrawer Mechanism{get;private set;}
        // Running out and shutting again.
        public bool Opening{get;private set;}
        // Metres out of the stand.
        public float Out=>Mechanism?Mechanism.Pull*unit:0;
        public Vector3 Knob=>Mechanism?Mechanism.KnobPoint:transform.position;
        public int MotesFlying{get;private set;}
        MemoryKit kit;
        Func<Camera> view;
        float unit=1;
        AudioSource voice;
        AudioClip knock,slide;
        Texture2D soft;
        Material moteMaterial;
        readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();
        sealed class Mote{public Transform t;public Renderer r;public Vector3 a,b,c,d;public float age,size;}
        readonly List<Mote> motes=new List<Mote>();

        public static MemoryDrawer Add(GameObject host,BookStandDrawer mechanism,MemoryKit kit,Func<Camera> view)
        {
            var go=new GameObject("Drawer");go.transform.SetParent(host.transform,false);
            var d=go.AddComponent<MemoryDrawer>();d.Mechanism=mechanism;d.kit=kit;d.view=view;
            d.unit=mechanism.transform.TransformVector(mechanism.PullAxis.normalized).magnitude;
            d.voice=go.AddComponent<AudioSource>();d.voice.playOnAwake=false;d.voice.spatialBlend=0;
            d.knock=d.Clip("Drawer knock",Knock(.11f,140,1));d.slide=d.Clip("Drawer slide",Sliding());
            d.soft=Soft();d.owned.Add(d.soft);
            return d;
        }
        Vector3 Axis=>Mechanism.transform.TransformDirection(Mechanism.PullAxis).normalized;
        // Runs out, then started (the memory starts), then shuts after a moment. Returns whether it began (not already open).
        public bool Open(Action started)
        {
            if(Opening){Debug.Log("SHAPE_OF_MEMORY_DRAWER busy");return false;}
            StartCoroutine(Run(started));return true;
        }
        IEnumerator Run(Action started)
        {
            Opening=true;Say(slide,1,.25f);Debug.Log("SHAPE_OF_MEMORY_DRAWER opens");
            float from=Mechanism.Open;
            for(float t=0;t<RunOut;t+=Time.unscaledDeltaTime){Mechanism.SetOpen(Mathf.Lerp(from,1,MemoryIntro.Smooth(t/RunOut)));yield return null;}
            Mechanism.SetOpen(1);
            started?.Invoke();
            yield return new WaitForSecondsRealtime(ShutAfter);
            Say(slide,.8f,.15f);
            for(float t=0;t<ShutSeconds;t+=Time.unscaledDeltaTime){Mechanism.SetOpen(1-MemoryIntro.Smooth(t/ShutSeconds));yield return null;}
            Mechanism.SetOpen(0);Say(knock,1.3f,.25f);
            Opening=false;
        }
        // A mote from the drawer's mouth to each place on the table where one of the memory's things comes up.
        public void Burst(IEnumerable<Vector3> to)
        {
            var mouth=Knob+Vector3.up*.05f;int k=0;
            foreach(var target in to)
            {
                var mote=motes.Find(m=>!m.t.gameObject.activeSelf)??NewMote();
                mote.a=mouth;mote.b=mouth+Vector3.up*.12f+Axis*.04f;mote.c=target+Vector3.up*.15f;mote.d=target;mote.age=-.06f*k++;mote.size=.013f;
                mote.t.gameObject.SetActive(true);
            }
        }
        Mote NewMote()
        {
            var r=MemoryUi.Picture("Mote",transform,Vector3.zero,Quaternion.identity,Vector2.one,kit.SeeThrough?kit.SeeThrough:kit.Screen,soft,MemoryPalette.GoldBright,kit.Owned);
            if(moteMaterial)r.sharedMaterial=moteMaterial;else moteMaterial=r.sharedMaterial;
            var m=new Mote{t=r.transform,r=r};motes.Add(m);return m;
        }
        void Update()
        {
            float dt=Time.unscaledDeltaTime;var camera=view?.Invoke();int flying=0;
            foreach(var m in motes)
            {
                if(!m.t.gameObject.activeSelf)continue;
                m.age+=dt;float u=Mathf.Max(0,m.age)/MoteSeconds;if(u>=1){m.t.gameObject.SetActive(false);continue;}
                flying++;float e=MemoryIntro.Smooth(u),k=1-e;
                m.t.position=k*k*k*m.a+3*k*k*e*m.b+3*k*e*e*m.c+e*e*e*m.d;if(camera)m.t.rotation=Quaternion.LookRotation(m.t.position-camera.transform.position);
                float alpha=Mathf.Clamp01(u/.12f)*Mathf.Clamp01((1-u)/.2f);
                m.t.localScale=Vector3.one*m.size*(1+.6f*(1-u));MemoryUi.Tint(m.r,MemoryPalette.Alpha(MemoryPalette.GoldBright,alpha));
            }
            MotesFlying=flying;
        }
        // A soft round spot, white, its alpha falling off from the middle.
        static Texture2D Soft()
        {
            const int N=64;var t=new Texture2D(N,N,TextureFormat.RGBA32,true){name="Drawer soft",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            var pixels=new Color32[N*N];
            for(int y=0;y<N;y++)for(int x=0;x<N;x++){float u=(x+.5f)/N*2-1,v=(y+.5f)/N*2-1,r=Mathf.Sqrt(u*u+v*v);float a=Mathf.Clamp01(1-r);a=a*a*(3-2*a);pixels[y*N+x]=new Color(1,1,1,a);}
            t.SetPixels32(pixels);t.Apply(true,true);return t;
        }

        // ---- Sounds (generated, as MemorySounds')
        void Say(AudioClip clip,float pitch,float volume){if(!clip||!voice)return;voice.pitch=pitch;voice.PlayOneShot(clip,volume);}
        const int Rate=44100;
        AudioClip Clip(string name,float[] samples){var clip=AudioClip.Create(name,samples.Length,1,Rate,false);clip.SetData(samples,0);owned.Add(clip);return clip;}
        // Wood: a low thump (a falling sine) under a short click of noise.
        static float[] Knock(float seconds,float hz,float loud)
        {
            int n=(int)(Rate*seconds);var s=new float[n];var random=new System.Random(7);float phase=0;
            for(int i=0;i<n;i++)
            {
                float t=i/(float)Rate,f=hz*(1+1.5f*Mathf.Exp(-t*60));phase+=f/Rate;
                s[i]=loud*(Mathf.Sin(phase*Mathf.PI*2)*Mathf.Exp(-t*38)*.8f+((float)random.NextDouble()*2-1)*Mathf.Exp(-t*400)*.4f);
            }
            return s;
        }
        // Wood sliding on wood: low noise swelling and falling over a quarter second.
        static float[] Sliding()
        {
            int n=(int)(Rate*.26f);var s=new float[n];var random=new System.Random(9);float low=0;
            for(int i=0;i<n;i++){float t=i/(float)n;low+=.08f*(((float)random.NextDouble()*2-1)-low);s[i]=low*1.6f*Mathf.Sin(t*Mathf.PI);}
            return s;
        }
        void OnDestroy(){foreach(var o in owned)if(o)Destroy(o);}
    }
}
