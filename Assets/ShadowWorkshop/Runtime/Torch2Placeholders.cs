using System;
using UnityEngine;

namespace CloudGarden.ShadowPuzzle
{
    // Stand-ins for the closeness cue until real slides and sound layers are imported (Torch2Level.leftSlide, rightSlide,
    // layers). The slide is a made-up place per level and wall: soft colour masses that are all the blurred slide shows,
    // and windows and dashes too fine to survive the blur, so focusing reads. The layers are four seamless loops in A.
    public static class Torch2Placeholders
    {
        public const int SlideWidth=384,SlideHeight=360,Rate=22050;
        public const float LoopSeconds=8;
        static AudioClip[] layers;
        static AudioClip tick;

        public static Texture2D Slide(string key,Color accent)
        {
            var random=new System.Random(Hash(key));float R(float a,float b)=>a+(float)random.NextDouble()*(b-a);
            Color.RGBToHSV(accent,out float h,out _,out _);
            int w=SlideWidth,ht=SlideHeight;var px=new Color32[w*ht];float horizon=R(.36f,.46f);
            Color skyTop=Hsv(h+.55f,.35f,.92f),skyLow=Hsv(h+.08f,.28f,.98f),ground=Hsv(h+.02f,.45f,.55f),groundLow=Hsv(h+.02f,.5f,.36f);
            for(int y=0;y<ht;y++)
            {
                float v=y/(float)(ht-1);Color32 c=v>=horizon?Color.Lerp(skyLow,skyTop,(v-horizon)/(1-horizon)):Color.Lerp(groundLow,ground,v/horizon);
                for(int x=0;x<w;x++)px[y*w+x]=c;
            }
            Disc(px,w,ht,R(.15f,.85f)*w,(horizon+R(.12f,.3f))*ht,R(.06f,.09f)*w,Hsv(h+.12f,.45f,1));
            // A skyline standing on the horizon, with lit windows.
            int ground0=(int)(horizon*ht);var window=Hsv(h+.1f,.35f,1);
            for(float x=R(0,.06f);x<1;)
            {
                float bw=R(.05f,.12f),bh=R(.08f,.3f);int x0=(int)(x*w),x1=(int)((x+bw)*w),top=(int)((horizon+bh)*ht);
                Rect(px,w,ht,x0,ground0,x1-x0,top-ground0,Hsv(h+R(-.12f,.12f),R(.25f,.5f),R(.3f,.55f)));
                for(int wy=ground0+6;wy<top-6;wy+=7)for(int wx=x0+4;wx<x1-4;wx+=6)if(random.NextDouble()<.6)Rect(px,w,ht,wx,wy,3,3,window);
                x+=bw+R(.005f,.05f);
            }
            for(int i=0;i<4;i++)Disc(px,w,ht,R(.05f,.95f)*w,R(.02f,horizon*.75f)*ht,R(.08f,.16f)*w,Hsv(h+.5f+R(-.12f,.12f),R(.45f,.7f),R(.65f,.9f)),.35f);
            var mark=Hsv(h+.15f,.15f,.95f);for(int x=4;x<w;x+=22)Rect(px,w,ht,x,(int)(horizon*ht*.45f),12,2,mark);
            var texture=new Texture2D(w,ht,TextureFormat.RGBA32,true){name="Placeholder slide "+key,filterMode=FilterMode.Trilinear,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.DontSave};
            texture.SetPixels32(px);texture.Apply(true,false);return texture;
        }
        static Color Hsv(float h,float s,float v)=>Color.HSVToRGB(h-Mathf.Floor(h),s,v);
        static void Rect(Color32[] px,int w,int h,int x0,int y0,int rw,int rh,Color color)
        {
            Color32 c=color;for(int y=Mathf.Max(0,y0);y<Mathf.Min(h,y0+rh);y++)for(int x=Mathf.Max(0,x0);x<Mathf.Min(w,x0+rw);x++)px[y*w+x]=c;
        }
        // soft: the edge's width as a fraction of the radius.
        static void Disc(Color32[] px,int w,int h,float cx,float cy,float r,Color color,float soft=.08f)
        {
            Color32 c=color;int x0=Mathf.Max(0,(int)(cx-r)),x1=Mathf.Min(w-1,(int)(cx+r)),y0=Mathf.Max(0,(int)(cy-r)),y1=Mathf.Min(h-1,(int)(cy+r));
            for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)
            {
                float d=Mathf.Sqrt((x-cx)*(x-cx)+(y-cy)*(y-cy))/r;if(d>=1)continue;
                int i=y*w+x;px[i]=Color32.Lerp(px[i],c,Mathf.Clamp01((1-d)/soft));
            }
        }
        static int Hash(string key){unchecked{int h=(int)2166136261;foreach(char ch in key)h=(h^ch)*16777619;return h;}}

        // Generated once per session: a breathing drone (always on), plucks on every beat, bells off the beat, and a motif
        // in the second half, 90 BPM, all LoopSeconds long.
        public static AudioClip[] Layers()
        {
            if(layers!=null&&Array.TrueForAll(layers,c=>c))return layers;
            float beat=LoopSeconds/12;
            // Whole cycles per loop (multiples of 1/8 Hz) keep the drone seamless; 110 against 110.25 Hz beats every 4 s.
            var drone=Buffer();Partial(drone,0,drone.Length,110,.35f,0,0);Partial(drone,0,drone.Length,110.25,.15f,0,0);Partial(drone,0,drone.Length,165,.22f,0,0);Partial(drone,0,drone.Length,220,.1f,0,0);
            for(int i=0;i<drone.Length;i++)drone[i]*=.75f+.25f*Mathf.Sin(2*Mathf.PI*i/drone.Length);
            var plucks=Buffer();var line=new[]{220f,329.63f,277.18f,329.63f,220f,329.63f,293.66f,329.63f,220f,329.63f,277.18f,246.94f};
            for(int k=0;k<line.Length;k++)Note(plucks,k*beat,line[k],3.5f,.5f,0);
            var bells=Buffer();var high=new[]{880f,1108.73f,1318.51f,1108.73f,880f,1318.51f};
            for(int k=0;k<high.Length;k++)Note(bells,(2*k+1.5f)*beat,high[k],2.2f,.22f,.5f);
            var motif=Buffer();var tune=new[]{659.25f,739.99f,659.25f,554.37f,440f};
            for(int k=0;k<tune.Length;k++)Note(motif,LoopSeconds*.5f+k*beat,tune[k],1.6f,.4f,.3f);
            return layers=new[]{Clip("Placeholder drone",drone,.35f),Clip("Placeholder plucks",plucks,.4f),Clip("Placeholder bells",bells,.4f),Clip("Placeholder motif",motif,.4f)};
        }
        // The soft blip when the cue goes up a tier (Torch2Cue raises its pitch per tier).
        public static AudioClip Tick()
        {
            if(tick)return tick;
            var b=new float[Rate/5];Partial(b,0,b.Length,1318.51,.6f,18,Rate/400);Partial(b,0,b.Length,1318.51*2.76,.15f,40,Rate/400);
            return tick=Clip("Placeholder tier tick",b,.5f);
        }
        static float[] Buffer()=>new float[(int)(Rate*LoopSeconds)];
        // A struck note at t seconds: fundamental, a faster-decaying octave and (bell > 0) an inharmonic 2.76x partial.
        static void Note(float[] b,float t,float freq,float decay,float gain,float bell)
        {
            int start=(int)(t*Rate),n=Mathf.Min(b.Length,(int)(Rate*5/decay)),attack=Rate/200;
            Partial(b,start,n,freq,gain,decay,attack);Partial(b,start,n,freq*2,gain*.35f,decay*1.5f,attack);
            if(bell>0)Partial(b,start,n,freq*2.76,gain*bell,decay*2,attack);
        }
        // A sine added from sample start for n samples, wrapping at the end of the buffer so tails continue at the start
        // (seamless loops). decay is per second after an attack of that many samples. A rotating phasor, not Sin per sample.
        static void Partial(float[] b,int start,int n,double freq,float gain,float decay,int attack)
        {
            double step=2*Math.PI*freq/Rate,c=Math.Cos(step),s=Math.Sin(step),x=1,y=0,env=gain,fall=Math.Exp(-decay/(double)Rate);
            for(int i=0;i<n;i++)
            {
                b[(start+i)%b.Length]+=(float)(y*env*(attack>0?Math.Min(1,(i+1)/(double)attack):1));
                double nx=x*c-y*s;y=x*s+y*c;x=nx;env*=fall;
            }
        }
        static AudioClip Clip(string name,float[] samples,float peak)
        {
            float most=0;foreach(var v in samples)most=Mathf.Max(most,Mathf.Abs(v));
            if(most>0)for(int i=0;i<samples.Length;i++)samples[i]*=peak/most;
            var clip=AudioClip.Create(name,samples.Length,1,Rate,false);clip.SetData(samples,0);clip.hideFlags=HideFlags.DontSave;return clip;
        }
    }
}
