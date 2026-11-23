using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ShapeOfMemory.Editor
{
    // Summer's found-moment sounds (the seal at the shore, MemorySolveCue), synthesized here rather than cut from a download
    // (no ffmpeg or Python on this machine, 2026-11-17):
    // - Summer_Splash: the seal's flippers slapping the shallows as it flops in. A low thump of the body through the
    //   surface, a sheet of white water, a cloud of bubbles (each a Minnaert bubble: a sine that rings a few milliseconds
    //   and rises in pitch as it dies, the bigger the lower and longer) and the spray pattering back down.
    // - Summer_SealBark: its bark, four honking "arf"s (see Bark), one every MemorySolveCueTools.BarkEvery.
    // MetaHackthone > Pico Final > Solve cues > Make Summer sounds writes them into Audio/Cues as 44.1 kHz 16-bit mono WAV,
    // peaking at -1 dBFS like trim_cue_audio.py's cuts, and logs the cue volume that puts each one's loudest 400 ms at
    // -20 dBFS (MemorySolveCueTools' defaults use those). Seeded: every run writes the same files.
    // Summer_Dolphin.wav in Audio/Cues is the call of the level's first picture, a dolphin (written by this class until the
    // picture became a seal, 2026-11-17); nothing uses it now.
    public static class MemorySummerSounds
    {
        const int Rate=44100;
        const float Target=-20,TwoPi=Mathf.PI*2;

        [MenuItem("MetaHackthone/Pico Final/Solve cues/Make Summer sounds")]
        public static void Make()
        {
            Write("Summer_Splash",Splash());
            Write("Summer_SealBark",Seal());
            AssetDatabase.Refresh();MemorySolveCueTools.SetUpAudio();
        }

        // ---- The splash (1.8 s)
        static float[] Splash()
        {
            int n=(int)(Rate*1.8f);var s=new float[n];var random=new System.Random(41);
            float Rand()=>(float)random.NextDouble();
            // The body through the surface: a thump gliding down from 130 to 55 Hz.
            double phase=0;
            for(int i=0;i<n;i++)
            {
                float t=i/(float)Rate;phase+=TwoPi*Mathf.Lerp(130,55,Mathf.Clamp01(t/.15f))/Rate;
                s[i]+=(float)Math.Sin(phase)*.6f*Mathf.Exp(-t*16)*Mathf.Clamp01(t/.003f);
            }
            // White water: noise through a band closing from bright to dull, loud at the break and again as the sheet falls back.
            var band=new Biquad();
            for(int i=0;i<n;i++)
            {
                float t=i/(float)Rate;if(i%32==0)band.BandPass(Mathf.Lerp(3200,1100,Mathf.Clamp01(t/.9f)),.6f);
                float envelope=Mathf.Clamp01(t/.006f)*Mathf.Exp(-t*7)+.32f*Bump(t,.26f,1.3f);
                s[i]+=band.Run(Rand()*2-1)*envelope*.6f;
            }
            // Bubbles: a cloud of them at the break, then smaller ones as the spray lands.
            for(int k=0;k<170;k++)
            {
                bool first=k<110;float at=first?Exp(random,.1f):.22f+Exp(random,.35f);if(at>1.5f)continue;
                float radius=first?Mathf.Lerp(.8f,5,Rand()*Rand()):Mathf.Lerp(.6f,2.5f,Rand());// mm
                float f0=3260/radius,damping=.043f*f0+.0014f*Mathf.Pow(f0,1.5f);
                Bubble(s,at,f0,damping,(first?.3f:.18f)*(.4f+.6f*Rand())*Mathf.Sqrt(radius/3)*Mathf.Exp(-at*1.1f));
            }
            // The spray pattering down: ticks of bright noise.
            var patter=new float[n];
            for(int k=0;k<150;k++)
            {
                float at=.18f+Exp(random,.35f);if(at>1.6f)continue;
                int start=(int)(at*Rate);float gain=.3f*(.3f+.7f*Rand())*Mathf.Exp(-at*1.4f);
                for(int j=0;j<Rate/500&&start+j<n;j++)patter[start+j]+=(Rand()*2-1)*gain*Mathf.Exp(-j/(Rate*.0005f));
            }
            var high=new Biquad();high.HighPass(2500,.7f);
            for(int i=0;i<n;i++)s[i]+=high.Run(patter[i]);
            Fade(s,.001f,.3f);return s;
        }
        // One bubble ringing from at: f0 Hz, dying at damping per second, rising in pitch as it goes.
        static void Bubble(float[] s,float at,float f0,float damping,float gain)
        {
            int start=(int)(at*Rate),length=(int)(Rate*Mathf.Min(.25f,6.9f/damping));double phase=0;
            for(int j=0;j<length&&start+j<s.Length;j++)
            {
                float t=j/(float)Rate;phase+=TwoPi*f0*(1+.1f*damping*t)/Rate;
                s[start+j]+=(float)Math.Sin(phase)*gain*Mathf.Exp(-damping*t)*Mathf.Clamp01(t/.0008f);
            }
        }

        // ---- The seal (1.4 s)
        // Four barks a beat apart (MemorySolveCueTools.BarkEvery, so the cue's bark lines pop in with them), the last a
        // little higher and longer: "arf, arf, arf, ARF!".
        static float[] Seal()
        {
            int n=(int)(Rate*1.4f);var s=new float[n];var random=new System.Random(47);float every=MemorySolveCueTools.BarkEvery;
            Bark(s,.02f,.19f,1f,.9f,random);Bark(s,.02f+every,.18f,1.06f,1f,random);
            Bark(s,.02f+2*every,.2f,.96f,.85f,random);Bark(s,.02f+3*every,.27f,1.1f,1f,random);
            Fade(s,.001f,.06f);return s;
        }
        // One bark (length s, pitch scaled by scale): a buzzing voice, the first 14 harmonics of a pulse train gliding up
        // from 300 Hz and down to 250, made rough the way a sea lion's honk is: every other cycle louder (a growl an octave
        // below, strongest at the bark's start and end), a little jitter, a breath of noise and a 29 Hz flutter. Through the
        // mouth: a chest resonance at the pitch and three formants, the first two falling as the mouth closes ("ar-f").
        static void Bark(float[] s,float at,float length,float scale,float gain,System.Random random)
        {
            int start=(int)(at*Rate),count=(int)(length*Rate);
            var weights=new float[15];for(int h=1;h<weights.Length;h++)weights[h]=1/Mathf.Pow(h,.8f);
            Biquad chest=new Biquad(),f1=new Biquad(),f2=new Biquad(),f3=new Biquad();chest.BandPass(320*scale,.8f);f3.BandPass(2600,5);
            double phase=0;float jitter=0;
            for(int j=0;j<count&&start+j<s.Length;j++)
            {
                float t=j/(float)Rate,u=t/length;
                if(j%32==0){f1.BandPass(Mathf.Lerp(780,470,u),3);f2.BandPass(Mathf.Lerp(1300,960,u),4.5f);}
                jitter=.995f*jitter+.0015f*((float)random.NextDouble()*2-1);
                phase+=TwoPi*Glide(u,300,375,345,250)*scale*(1+jitter)/Rate;
                float voice=0;for(int h=1;h<weights.Length;h++)voice+=(float)Math.Sin(h*phase)*weights[h];
                voice*=1+(.3f+.25f*(1-Mathf.Sin(Mathf.PI*u)))*(float)Math.Sin(phase/2);
                voice+=.25f*((float)random.NextDouble()*2-1);
                float mouth=.55f*chest.Run(voice)+f1.Run(voice)+.75f*f2.Run(voice)+.3f*f3.Run(voice);
                float envelope=Mathf.Clamp01(t/.008f)*(.6f+.4f*Mathf.Exp(-t*12))*Mathf.Clamp01((length-t)/.045f)*(1+.15f*Mathf.Sin(TwoPi*29*t));
                s[start+j]+=mouth*envelope*gain;
            }
        }
        // Eased through evenly spaced points over 0..1.
        static float Glide(float u,params float[] points)
        {
            float x=Mathf.Clamp01(u)*(points.Length-1);int i=Mathf.Min((int)x,points.Length-2);
            return Mathf.Lerp(points[i],points[i+1],.5f-.5f*Mathf.Cos(Mathf.PI*(x-i)));
        }

        // ---- Shared
        // Exponentially distributed, mean seconds.
        static float Exp(System.Random random,float mean)=>-mean*(float)Math.Log(1-random.NextDouble());
        // 0 outside from..to; inside, quick to rise and slow to die.
        static float Bump(float t,float from,float to){if(t<from||t>to)return 0;float u=(t-from)/(to-from);return Mathf.Min(1,u*8)*(1-u)*(1-u);}
        static void Fade(float[] s,float fadeIn,float fadeOut)
        {
            int a=(int)(fadeIn*Rate),b=(int)(fadeOut*Rate);
            for(int i=0;i<a&&i<s.Length;i++)s[i]*=i/(float)a;
            for(int i=0;i<b&&i<s.Length;i++)s[s.Length-1-i]*=.5f-.5f*Mathf.Cos(Mathf.PI*i/b);
        }
        static void Write(string name,float[] s)
        {
            float peak=0;foreach(var v in s)peak=Mathf.Max(peak,Mathf.Abs(v));
            float scale=Mathf.Pow(10,-1/20f)/Mathf.Max(1e-9f,peak);for(int i=0;i<s.Length;i++)s[i]*=scale;
            int window=(int)(Rate*.4f);double loudest=0;
            for(int i=0;i<Mathf.Max(1,s.Length-window);i+=Rate/50)
            {
                double sum=0;for(int j=i;j<Mathf.Min(s.Length,i+window);j++)sum+=s[j]*s[j];
                loudest=Math.Max(loudest,Math.Sqrt(sum/window));
            }
            float db=20*Mathf.Log10((float)loudest+1e-12f),volume=Mathf.Clamp(Mathf.Pow(10,(Target-db)/20),.15f,1);
            string path=MemorySolveCueTools.AudioFolder+"/"+name+".wav";Directory.CreateDirectory(MemorySolveCueTools.AudioFolder);
            using(var writer=new BinaryWriter(new FileStream(path,FileMode.Create)))
            {
                int bytes=s.Length*2;
                writer.Write(Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+bytes);writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
                writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(Rate);writer.Write(Rate*2);writer.Write((short)2);writer.Write((short)16);
                writer.Write(Encoding.ASCII.GetBytes("data"));writer.Write(bytes);
                foreach(var v in s)writer.Write((short)Mathf.RoundToInt(Mathf.Clamp(v,-1,1)*32767));
            }
            Debug.Log($"SHAPE_OF_MEMORY_CUE wrote {path}: {s.Length/(float)Rate:0.00} s, loudest 400 ms {db:0.0} dBFS -> cue volume {volume:0.00}");
        }

        // An RBJ biquad: band-pass (0 dB at the peak) or high-pass. Coefficients may change while it runs.
        sealed class Biquad
        {
            float b0,b1,b2,a1,a2,x1,x2,y1,y2;
            public void BandPass(float f,float q)=>Set(f,q,true);
            public void HighPass(float f,float q)=>Set(f,q,false);
            void Set(float f,float q,bool band)
            {
                float w=TwoPi*f/Rate,cos=Mathf.Cos(w),alpha=Mathf.Sin(w)/(2*q),a0=1+alpha;
                if(band){b0=alpha;b1=0;b2=-alpha;}else{b0=(1+cos)/2;b1=-(1+cos);b2=(1+cos)/2;}
                b0/=a0;b1/=a0;b2/=a0;a1=-2*cos/a0;a2=(1-alpha)/a0;
            }
            public float Run(float x){float y=b0*x+b1*x1+b2*x2-a1*y1-a2*y2;x2=x1;x1=x;y2=y1;y1=y;return y;}
        }
    }
}
