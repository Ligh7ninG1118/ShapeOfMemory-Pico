using System.Collections.Generic;
using UnityEngine;

namespace ShapeOfMemory
{
    // Generated placeholder sounds until real ones arrive: a quiet room tone, the lamps' switch and hum, a whoosh as a
    // piece lifts off the table and a soft knock as it settles in the orb; for the book's pages, a crinkle as a corner is
    // picked up, a rustle (looped, as loud as the page is fast) and the flap of a page landing; a memory found. All mono,
    // 44.1 kHz, made once per game.
    public sealed class MemorySounds
    {
        const int Rate=44100;
        public readonly AudioClip Room,Lamp,Whoosh,Settle,Ending,Crinkle,Rustle,Flap,Found;
        public MemorySounds(List<Object> owned)
        {
            Room=Make("Memory room tone",RoomTone(),owned);Lamp=Make("Memory lamp",LampSwitch(),owned);
            Whoosh=Make("Memory whoosh",Air(),owned);Settle=Make("Memory settle",Knock(),owned);Ending=Make("Memory ending",Arpeggio(),owned);Found=Make("Memory found",Bells(),owned);
            Crinkle=Make("Memory page crinkle",PaperCrinkle(),owned);Rustle=Make("Memory page rustle",PaperRustle(),owned);Flap=Make("Memory page flap",PaperFlap(),owned);
        }
        // Paper: bright noise (high-passed) broken into crackles, each a burst of it that dies away in a few milliseconds.
        static float[] Crackles(int length,float perSecond,float floor,int seed)
        {
            var s=new float[length];var random=new System.Random(seed);float low=0,previous=0,envelope=floor,next=0;
            for(int i=0;i<length;i++)
            {
                float t=i/(float)Rate;
                if(t>=next){envelope+=.5f+(float)random.NextDouble()*.7f;next=t+(float)(-System.Math.Log(1-random.NextDouble())/perSecond);}
                envelope=floor+(envelope-floor)*Mathf.Exp(-1f/(Rate*.004f));
                float noise=(float)random.NextDouble()*2-1;low+=.25f*(noise-low);float high=noise-low;s[i]=(high*.8f+(noise-previous)*.2f)*envelope*.35f;previous=noise;
            }
            return s;
        }
        // A corner picked up: a handful of crackles over a fifth of a second, fading.
        static float[] PaperCrinkle(){var s=Crackles((int)(Rate*.22f),45,.05f,21);for(int i=0;i<s.Length;i++)s[i]*=Mathf.Clamp01(i/(Rate*.004f))*(1-i/(float)s.Length);return s;}
        // Two seconds of steady crackle under a soft swish, the ends crossfaded so it loops.
        static float[] PaperRustle()
        {
            int n=Rate*2,fade=Rate/5;var s=Crackles(n+fade,70,.35f,23);
            for(int i=0;i<s.Length;i++){float t=i/(float)Rate;s[i]*=.8f+.2f*Mathf.Sin(t*Mathf.PI*2*1.5f);}
            for(int i=0;i<fade;i++){float w=i/(float)fade;s[i]=s[i]*w+s[n+i]*(1-w);}
            var looped=new float[n];System.Array.Copy(s,looped,n);return looped;
        }
        // A page landing: a soft low thump of air under a short slap of paper.
        static float[] PaperFlap()
        {
            var s=new float[(int)(Rate*.3f)];var random=new System.Random(25);float low=0;
            for(int i=0;i<s.Length;i++)
            {
                float t=i/(float)Rate,noise=(float)random.NextDouble()*2-1;low+=.12f*(noise-low);
                s[i]=Mathf.Sin(t*Mathf.PI*2*90)*.45f*Mathf.Exp(-t*30)+low*1.3f*Mathf.Exp(-t*55)+(noise-low)*.18f*Mathf.Exp(-t*160);
            }
            return s;
        }
        // The ending: a slow rising major arpeggio (A, C#, E, A) that rings out.
        static float[] Arpeggio()
        {
            var s=new float[Rate*4];float[] notes={440,554.37f,659.25f,880};
            for(int k=0;k<notes.Length;k++)
            {
                int start=(int)(Rate*.32f*k);
                for(int i=start;i<s.Length;i++){float t=(i-start)/(float)Rate;s[i]+=(Mathf.Sin(t*Mathf.PI*2*notes[k])+.3f*Mathf.Sin(t*Mathf.PI*4*notes[k]))*.16f*Mathf.Exp(-t*1.3f)*Mathf.Min(1,t*200);}
            }
            return s;
        }
        // A memory found: a quick rising run of bells (E, G#, B, E, G#) over a soft swell on the low E, all ringing out.
        static float[] Bells()
        {
            var s=new float[(int)(Rate*2.6f)];float[] notes={659.25f,830.61f,987.77f,1318.51f,1661.22f};
            for(int i=0;i<s.Length;i++){float t=i/(float)Rate;s[i]=Mathf.Sin(t*Mathf.PI*2*164.81f)*.10f*Mathf.Sin(Mathf.PI*Mathf.Clamp01(t/2.4f));}
            for(int k=0;k<notes.Length;k++)
            {
                int start=(int)(Rate*.075f*k);
                for(int i=start;i<s.Length;i++){float t=(i-start)/(float)Rate;s[i]+=(Mathf.Sin(t*Mathf.PI*2*notes[k])+.25f*Mathf.Sin(t*Mathf.PI*2*notes[k]*2.76f)*Mathf.Exp(-t*6))*.11f*Mathf.Exp(-t*2.2f)*Mathf.Min(1,t*300);}
            }
            return s;
        }
        static AudioClip Make(string name,float[] samples,List<Object> owned)
        {
            var clip=AudioClip.Create(name,samples.Length,1,Rate,false);clip.SetData(samples,0);owned.Add(clip);return clip;
        }
        // 8 s that loop seamlessly: low tones on whole multiples of 1/8 Hz under smoothed noise, the noise's ends crossfaded.
        static float[] RoomTone()
        {
            int n=Rate*8,fade=Rate/2;var s=new float[n];var random=new System.Random(11);float low=0,lower=0;
            for(int i=0;i<n;i++)
            {
                float t=i/(float)Rate,noise=(float)random.NextDouble()*2-1;low+=.02f*(noise-low);lower+=.05f*(low-lower);
                s[i]=lower*.9f+.05f*Mathf.Sin(t*Mathf.PI*2*55)*(.6f+.4f*Mathf.Sin(t*Mathf.PI*2*.125f))+.03f*Mathf.Sin(t*Mathf.PI*2*82.5f);
            }
            for(int i=0;i<fade;i++){float w=i/(float)fade;s[i]=s[i]*w+s[n-fade+i]*(1-w);}
            var looped=new float[n-fade];System.Array.Copy(s,looped,looped.Length);return looped;
        }
        // Two dry clicks of a switch, then a short mains hum as the bulb warms.
        static float[] LampSwitch()
        {
            var s=new float[(int)(Rate*.6f)];var random=new System.Random(5);
            for(int i=0;i<s.Length;i++)
            {
                float t=i/(float)Rate,click=0;
                foreach(float at in new[]{0f,.028f})if(t>=at)click+=((float)random.NextDouble()*2-1)*Mathf.Exp(-(t-at)*900)*.8f+Mathf.Sin((t-at)*Mathf.PI*2*2400)*Mathf.Exp(-(t-at)*500)*.3f;
                float hum=(Mathf.Sin(t*Mathf.PI*2*100)+.4f*Mathf.Sin(t*Mathf.PI*2*200))*.12f*Mathf.Clamp01(t*8)*Mathf.Exp(-t*4.5f);
                s[i]=click+hum;
            }
            return s;
        }
        // Noise through a low-pass that opens and closes, swelling and fading.
        static float[] Air()
        {
            var s=new float[(int)(Rate*.55f)];var random=new System.Random(3);float y=0;
            for(int i=0;i<s.Length;i++)
            {
                float t=i/(float)s.Length,cutoff=Mathf.Lerp(.02f,.16f,Mathf.Sin(t*Mathf.PI));
                y+=cutoff*(((float)random.NextDouble()*2-1)-y);s[i]=y*1.6f*Mathf.Sin(t*Mathf.PI)*Mathf.Sin(t*Mathf.PI);
            }
            return s;
        }
        // A short wooden knock.
        static float[] Knock()
        {
            var s=new float[(int)(Rate*.16f)];var random=new System.Random(9);
            for(int i=0;i<s.Length;i++){float t=i/(float)Rate;s[i]=(Mathf.Sin(t*Mathf.PI*2*320)+.5f*Mathf.Sin(t*Mathf.PI*2*740))*.35f*Mathf.Exp(-t*38)+((float)random.NextDouble()*2-1)*.25f*Mathf.Exp(-t*400);}
            return s;
        }
    }
}
