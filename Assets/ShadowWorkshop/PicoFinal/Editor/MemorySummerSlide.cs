using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ShapeOfMemory.Editor
{
    // Summer's lamp slide (Torch2Level.leftSlide: the picture its lamp projects, blurred and grey while the shadow is far
    // off, sharp and in colour once found), drawn here in C# the way Tools/ShadowArt/build_slides.py draws the chapters'
    // (no Python on this machine, 2026-11-17): flat storybook shapes at 2x, reduced to 768 x 720 (the wall's 128:120).
    // Only the lamp's spot shows; on the left wall, spot units (MemorySolveCue's: 0 its middle, 1 its radius) are UV
    // .561 + .368 x, .411 + .393 y.
    // A summer shore laid out round the seal and its found-moment cut-outs (MemorySolveCueTools.Summer): a glow in the sky
    // where the cut-out sun stands (SunAt), the sea from the horizon down to the shore line where the cut-out shallows lap
    // (Water), sand in front with a sandcastle, a bucket and a starfish, a striped beach umbrella at the spot's right edge,
    // a sail and a headland on the horizon. The middle is left plain for the seal's shadow. MetaHackthone > Pico Final >
    // Slides > Draw Summer slide redraws it (after moving SunAt or Water); the file keeps its GUID, so the level and the gym
    // keep it. Seeded: every run draws the same picture.
    public static class MemorySummerSlide
    {
        public const string SlidePath=ShapeOfMemoryBuilder.Root+"/Art/Slides/Slide_Summer.png";
        const int W=768,H=720,SS=2,CW=W*SS,CH=H*SS;
        static Color[] canvas;

        [MenuItem("MetaHackthone/Pico Final/Slides/Draw Summer slide")]
        public static void Draw()
        {
            canvas=new Color[CW*CH];for(int i=0;i<canvas.Length;i++)canvas[i]=Color.white;
            var random=new System.Random(53);float Rand()=>(float)random.NextDouble();
            float water=Spot(0,MemorySolveCueTools.Water).y,horizon=.36f;var sun=Spot(MemorySolveCueTools.SunAt.x,MemorySolveCueTools.SunAt.y);
            // The sky, pale and warm at the horizon, a glow where the cut-out sun stands, two clouds.
            VGrad(horizon,1,(horizon,Hex(0xFFF1D6)),(horizon+.12f,Hex(0xDDF0F4)),(.75f,Hex(0x9FD3EE)),(1,Hex(0x6CB6E4)));
            Glow(sun.x,sun.y,.17f,Hex(0xFFF4CC,.85f));Glow(sun.x,sun.y,.09f,Hex(0xFFF9E4,.9f));
            Cloud(.66f,.73f,.04f);Cloud(.24f,.52f,.03f);
            // A hazy headland on the right of the horizon.
            Poly(Hex(0xA9C8D4),(.64f,horizon),(.70f,horizon+.022f),(.78f,horizon+.042f),(.87f,horizon+.05f),(.95f,horizon+.036f),(1,horizon+.03f),(1,horizon));
            // The sea, deep at the horizon and turquoise in the shallows, with glints that shorten toward the horizon; a
            // sail far out on the left.
            VGrad(water,horizon,(water,Hex(0x86D8DC)),(water+.06f,Hex(0x5BBDD8)),(horizon,Hex(0x2F8BC4)));
            for(int k=0;k<16;k++)
            {
                float t=Rand(),v=Mathf.Lerp(water+.025f,horizon-.012f,t);
                Ellipse(Mathf.Lerp(.08f,.95f,Rand()),v,Mathf.Lerp(.035f,.012f,t),Mathf.Lerp(.0045f,.0025f,t),Hex(0xB4E8F0,.85f));
            }
            Boat(.29f,horizon);
            // The sand, freckled; the wet band under the shore line and the foam along it.
            VGrad(0,water,(0,Hex(0xEFCF98)),(water-.05f,Hex(0xF6E0B4)),(water,Hex(0xF2D9A8)));
            for(int k=0;k<320;k++){float u=Rand(),v=Mathf.Lerp(.005f,water-.04f,Rand());Circle(u,v,Mathf.Lerp(.0022f,.004f,Rand()),Rand()<.6f?Hex(0xE0BE86):Hex(0xFBEBCB));}
            Rect(0,water-.032f,1,water,Hex(0xDDB683));
            for(float u=-.01f;u<1.02f;u+=.028f)Ellipse(u,water+.003f*Mathf.Sin(u*41),.02f,.009f,Hex(0xF4FBFA));
            Rect(0,water-.003f,1,water+.003f,Hex(0xF4FBFA));
            // Things on the beach. The umbrella stands right at the spot's edge, only its canopy showing: further in, its pole
            // ran through the seal's bark lines (spot x .78-.87) and its canopy into the cut-out sun.
            Umbrella(.92f,.60f);Castle(.62f,.055f);Bucket(.575f,.055f);Starfish(.43f,.08f);
            Save();
        }

        // ---- The things
        static void Boat(float u,float v)
        {
            Poly(Hex(0x5B6E8C),(u-.022f,v+.007f),(u+.022f,v+.007f),(u+.015f,v-.001f),(u-.015f,v-.001f));
            Line(u,v+.007f,u,v+.05f,.0025f,Hex(0x5B6E8C));
            Poly(Hex(0xFFFDF5),(u+.002f,v+.011f),(u+.002f,v+.05f),(u+.022f,v+.011f));
            Poly(Hex(0xF3E6D2),(u-.002f,v+.013f),(u-.002f,v+.042f),(u-.017f,v+.013f));
        }
        // A red and cream striped umbrella, its canopy's rim at v, its pole planted in the sand below.
        static void Umbrella(float u,float v)
        {
            const float ru=.115f,rv=.065f;var red=Hex(0xE36D5A);var cream=Hex(0xFFF6EA);
            Line(u+.025f,.11f,u,v+.04f,.008f,Hex(0x8C5A3C));
            // Scallops hanging under the rim, then the six panels over their tops.
            for(int k=0;k<6;k++)
            {
                float u0=u+ru*Mathf.Cos(Mathf.PI*(k+1)/6),u1=u+ru*Mathf.Cos(Mathf.PI*k/6);
                Ellipse((u0+u1)/2,v,(u1-u0)/2,.012f,k%2==0?red:cream);
            }
            for(int k=0;k<6;k++)
            {
                var panel=new List<(float,float)>{(u,v)};
                for(int s=0;s<=8;s++){float a=Mathf.PI*(k+s/8f)/6;panel.Add((u+ru*Mathf.Cos(a),v+rv*Mathf.Sin(a)));}
                Poly(k%2==0?red:cream,panel.ToArray());
            }
            Circle(u,v+rv+.004f,.007f,Hex(0x8C5A3C));
        }
        // Two towers and a wall, the gate between, a flag on the left tower; its foot at u, v.
        static void Castle(float u,float v)
        {
            var sand=Hex(0xE9C88F);var shade=Hex(0xD6AE74);
            Rect(u,v,u+.1f,v+.035f,sand);Rect(u+.075f,v,u+.1f,v+.035f,shade);
            Rect(u+.008f,v+.035f,u+.036f,v+.075f,sand);Rect(u+.036f,v+.035f,u+.064f,v+.058f,sand);
            Rect(u+.064f,v+.035f,u+.092f,v+.075f,sand);Rect(u+.082f,v+.035f,u+.092f,v+.075f,shade);
            for(int k=0;k<3;k++)
            {
                Rect(u+.008f+k*.0107f,v+.075f,u+.0145f+k*.0107f,v+.084f,sand);
                Rect(u+.064f+k*.0107f,v+.075f,u+.0705f+k*.0107f,v+.084f,k==2?shade:sand);
            }
            Rect(u+.042f,v,u+.058f,v+.012f,shade);Ellipse(u+.05f,v+.012f,.008f,.01f,shade);
            Line(u+.022f,v+.084f,u+.022f,v+.116f,.003f,Hex(0x8C5A3C));Poly(Hex(0xE36D5A),(u+.022f,v+.116f),(u+.022f,v+.1f),(u+.045f,v+.108f));
        }
        static void Bucket(float u,float v)
        {
            var handle=Hex(0x4C79C4);
            for(int s=0;s<10;s++)
            {
                float a0=Mathf.PI*s/10,a1=Mathf.PI*(s+1)/10;
                Line(u+.018f*Mathf.Cos(a0),v+.04f+.022f*Mathf.Sin(a0),u+.018f*Mathf.Cos(a1),v+.04f+.022f*Mathf.Sin(a1),.003f,handle);
            }
            Poly(Hex(0x6C9BE8),(u-.02f,v+.04f),(u+.02f,v+.04f),(u+.015f,v),(u-.015f,v));
            Rect(u-.022f,v+.034f,u+.022f,v+.043f,Hex(0x8DB4F0));
        }
        static void Starfish(float u,float v)
        {
            var points=new (float,float)[10];
            for(int k=0;k<10;k++){float a=Mathf.PI/2+k*Mathf.PI/5,r=k%2==0?.026f:.011f;points[k]=(u+r*Mathf.Cos(a)*H/W,v+r*Mathf.Sin(a));}
            Poly(Hex(0xF0A45E),points);Circle(u,v,.006f,Hex(0xE8874A));
        }
        static void Cloud(float u,float v,float s)
        {
            foreach(var (du,dv,r) in new[]{(-.9f,0f,.55f),(-.35f,.35f,.75f),(.35f,.28f,.65f),(.9f,.02f,.5f),(0f,-.1f,.7f)})Circle(u+du*s*.9f,v+dv*s,r*s,Hex(0xCFE0EE));
            foreach(var (du,dv,r) in new[]{(-.9f,.06f,.5f),(-.35f,.42f,.7f),(.35f,.34f,.6f),(.9f,.08f,.45f),(0f,0f,.62f)})Circle(u+du*s*.9f,v+dv*s,r*s,Color.white);
        }

        // ---- Drawing on the 2x canvas: UV with v up, colours sRGB as in build_slides.py.
        static Vector2 Spot(float x,float y)=>new Vector2(.561f+.368f*x,.411f+.393f*y);
        static Color Hex(int rgb,float a=1)=>new Color(((rgb>>16)&255)/255f,((rgb>>8)&255)/255f,(rgb&255)/255f,a);
        // c over canvas pixel i, j (j up from the bottom), at its alpha times cover.
        static void Put(int i,int j,Color c,float cover=1)
        {
            float a=c.a*cover;if(a<=0)return;
            ref var d=ref canvas[j*CW+i];d.r+=(c.r-d.r)*a;d.g+=(c.g-d.g)*a;d.b+=(c.b-d.b)*a;
        }
        // Every canvas pixel near u0..u1, v0..v1, with the UV of its middle.
        static void Each(float u0,float v0,float u1,float v1,Action<int,int,float,float> each)
        {
            int i0=Mathf.Max(0,Mathf.FloorToInt(u0*CW)),i1=Mathf.Min(CW-1,Mathf.CeilToInt(u1*CW));
            int j0=Mathf.Max(0,Mathf.FloorToInt(v0*CH)),j1=Mathf.Min(CH-1,Mathf.CeilToInt(v1*CH));
            for(int j=j0;j<=j1;j++)for(int i=i0;i<=i1;i++)each(i,j,(i+.5f)/CW,(j+.5f)/CH);
        }
        static void Rect(float u0,float v0,float u1,float v1,Color c)=>Each(u0,v0,u1,v1,(i,j,u,v)=>{if(u>=u0&&u<=u1&&v>=v0&&v<=v1)Put(i,j,c);});
        static void Ellipse(float cu,float cv,float ru,float rv,Color c)=>Each(cu-ru,cv-rv,cu+ru,cv+rv,(i,j,u,v)=>{float x=(u-cu)/ru,y=(v-cv)/rv;if(x*x+y*y<=1)Put(i,j,c);});
        // A circle, r in v (the canvas is not square).
        static void Circle(float cu,float cv,float r,Color c)=>Ellipse(cu,cv,r*H/W,r,c);
        static void Poly(Color c,params (float u,float v)[] p)
        {
            float u0=float.MaxValue,v0=float.MaxValue,u1=float.MinValue,v1=float.MinValue;
            foreach(var q in p){u0=Mathf.Min(u0,q.u);u1=Mathf.Max(u1,q.u);v0=Mathf.Min(v0,q.v);v1=Mathf.Max(v1,q.v);}
            Each(u0,v0,u1,v1,(i,j,u,v)=>
            {
                bool inside=false;
                for(int a=0,b=p.Length-1;a<p.Length;b=a++)
                    if((p[a].v>v)!=(p[b].v>v)&&u<(p[b].u-p[a].u)*(v-p[a].v)/(p[b].v-p[a].v)+p[a].u)inside=!inside;
                if(inside)Put(i,j,c);
            });
        }
        // A stroke from a to b, width in v, round ends (measured in pixels, so as wide every way).
        static void Line(float au,float av,float bu,float bv,float width,Color c)
        {
            float ax=au*CW,ay=av*CH,dx=(bu-au)*CW,dy=(bv-av)*CH,r=width*CH*.5f;
            Each(Mathf.Min(au,bu)-r/CW,Mathf.Min(av,bv)-r/CH,Mathf.Max(au,bu)+r/CW,Mathf.Max(av,bv)+r/CH,(i,j,u,v)=>
            {
                float px=i+.5f-ax,py=j+.5f-ay,t=Mathf.Clamp01((px*dx+py*dy)/Mathf.Max(1e-6f,dx*dx+dy*dy)),ex=px-t*dx,ey=py-t*dy;
                if(ex*ex+ey*ey<=r*r)Put(i,j,c);
            });
        }
        // A vertical gradient over v0..v1 through (v, colour) stops.
        static void VGrad(float v0,float v1,params (float v,Color c)[] stops)=>Each(0,v0,1,v1,(i,j,u,v)=>
        {
            if(v<v0||v>v1)return;
            var c=v<=stops[0].v?stops[0].c:stops[stops.Length-1].c;
            for(int k=0;k+1<stops.Length;k++)
                if(v>stops[k].v&&v<=stops[k+1].v){c=Color.Lerp(stops[k].c,stops[k+1].c,(v-stops[k].v)/Mathf.Max(1e-6f,stops[k+1].v-stops[k].v));break;}
            Put(i,j,c);
        });
        // A soft round glow, r in v, fading smoothly to nothing at its edge.
        static void Glow(float cu,float cv,float r,Color c)
        {
            float x0=cu*CW,y0=cv*CH,R=r*CH;
            Each(cu-R/CW,cv-r,cu+R/CW,cv+r,(i,j,u,v)=>
            {
                float x=i+.5f-x0,y=j+.5f-y0,d=Mathf.Sqrt(x*x+y*y)/R;
                if(d<1){float f=1-d;Put(i,j,c,f*f*(3-2*f));}
            });
        }
        // Reduced 2x2 to the slide, written as PNG and imported as the chapters' slides are.
        static void Save()
        {
            var pixels=new Color32[W*H];
            for(int j=0;j<H;j++)for(int i=0;i<W;i++)
            {
                int a=2*j*CW+2*i,b=a+CW;var c=(canvas[a]+canvas[a+1]+canvas[b]+canvas[b+1])*.25f;c.a=1;pixels[j*W+i]=c;
            }
            canvas=null;
            var texture=new Texture2D(W,H,TextureFormat.RGB24,false);texture.SetPixels32(pixels);
            Directory.CreateDirectory(Path.GetDirectoryName(SlidePath));File.WriteAllBytes(SlidePath,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(SlidePath,ImportAssetOptions.ForceUpdate);
            if(AssetImporter.GetAtPath(SlidePath) is TextureImporter importer)
            {
                importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;importer.alphaSource=TextureImporterAlphaSource.None;
                importer.mipmapEnabled=true;importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Bilinear;importer.npotScale=TextureImporterNPOTScale.None;
                importer.maxTextureSize=1024;importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.SaveAndReimport();
            }
            Debug.Log($"SHAPE_OF_MEMORY_SLIDE wrote {SlidePath} ({W} x {H})");
        }
    }
}
