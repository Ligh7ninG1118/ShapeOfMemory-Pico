using System.Collections.Generic;
using System.Linq;
using CloudGarden.ShadowPuzzle;
using UnityEditor;
using UnityEngine;
using static ShadowWorkshopIntegration.Editor.PropMesh;

namespace ShadowWorkshopIntegration.Editor
{
    // WildWest (2026-11-16, the user: "按照我们的思路生成一个新的关卡，所有东西都是西部特色的，拼装成一个牛仔靴 ... sheriff
    // 星星（可以组成 spur），然后牛头装饰，腰带"): WildWest's own props: the horseshoe, the shot glass, the harmonica, the
    // sheriff's star, the cowboy hat and (WildWestKeyBuilder) the room key. The level itself is the Shadow Gym's (16 pieces,
    // since cs:293): edit it there. This file first built a seven-piece boot of its own (a wanted poster for the shaft, a
    // longhorn skull for the foot, a folded belt for the sole); those three props and that level builder were removed on
    // 2026-11-17, as the gym's boot uses none of them.
    // Props: built here at their own size (mobile VR: 60-600 triangles each; the hat and key cast a coarser shadow mesh),
    // textures drawn here.
    public static class WildWestBuilder
    {
        const string Art="Assets/ShadowWorkshop/Art/Torch2/",Meshes=Art+"Meshes/",Textures=Art+"Textures/",MaterialFolder=Art+"Materials/";
        const string IronPath=Textures+"horseshoe iron.png",FeltPath=Textures+"hat felt.png",BraidPath=Textures+"hat band braid.png";
        const string GlassPath="Assets/Scenes/Store/Sari3D/CozyShops_Lite/Materials/M_CozyShops_Glass.mat";

        static Texture2D Load(string path)=>AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        static readonly Look Gold=new Look("western gold",new Color(.93f,.72f,.30f),.85f,.70f);
        static readonly Look Dark=new Look("western dark",new Color(.05f,.04f,.035f),0f,.20f);
        // shadowFile/buildShadow: a coarser mesh casting the shadow instead of the drawn one (null: the drawn one casts it).
        public sealed class Prop{public string label,file,shadowFile;public System.Func<Mesh> build,buildShadow;public System.Func<Material[]> materials;}
        public static readonly Prop[] Props={
            new Prop{label="HORSESHOE",file="Horseshoe",build=Horseshoe,materials=()=>PropMesh.Materials(MaterialFolder,new[]{
                new Look("horseshoe iron",Color.white,.55f,.35f){baseMap=()=>Load(IronPath)??Iron()},Dark})},
            new Prop{label="SHOT GLASS",file="Shot glass",build=ShotGlass,materials=()=>new[]{AssetDatabase.LoadAssetAtPath<Material>(GlassPath)}.Concat(PropMesh.Materials(MaterialFolder,new[]{
                new Look("whiskey",new Color(.50f,.24f,.05f),0f,.90f)})).ToArray()},
            new Prop{label="HARMONICA",file="Harmonica",build=Harmonica,materials=()=>PropMesh.Materials(MaterialFolder,new[]{
                new Look("harmonica chrome",new Color(.86f,.87f,.89f),.90f,.85f),new Look("harmonica comb",new Color(.55f,.37f,.21f),0f,.35f),Dark})},
            new Prop{label="SHERIFF STAR",file="Sheriff star",build=SheriffStar,materials=()=>PropMesh.Materials(MaterialFolder,new[]{Gold})},
            new Prop{label="COWBOY HAT",file="Cowboy hat",shadowFile="Cowboy hat shadow",build=()=>Hat(false),buildShadow=()=>Hat(true),materials=()=>PropMesh.Materials(MaterialFolder,new[]{
                new Look("hat felt",Color.white,0f,.12f){baseMap=()=>Load(FeltPath)??Felt()},new Look("hat band",Color.white,0f,.40f){baseMap=()=>Load(BraidPath)??Braid()}})},
            WildWestKeyBuilder.Prop};   // ROOM KEY: a hotel key on its fob, built in its own file
        public static Mesh MeshOf(Prop p)=>AssetDatabase.LoadAssetAtPath<Mesh>(Meshes+p.file+".asset");
        public static Mesh ShadowOf(Prop p)=>p.shadowFile!=null?AssetDatabase.LoadAssetAtPath<Mesh>(Meshes+p.shadowFile+".asset"):MeshOf(p);
        [MenuItem("MetaHackthone/Shadow Workshop/Build Wild West props")]
        public static void BuildProps()
        {
            Iron();Felt();Braid();
            var built=Props.Select(p=>{var m=p.build();var s=p.buildShadow?.Invoke();p.materials();return $"{m.name} {m.triangles.Length/3}"+(s?$" (shadow {s.triangles.Length/3})":"");}).ToArray();
            Debug.Log("WILD_WEST_PROPS_OK "+string.Join(", ",built));
        }

        // --- Props (each in its own frame, metres; -z is the front, as a Unity quad faces) --------------------------------

        // A horseshoe 112 mm across, arms up (+y), 9 mm thick, six nail holes in its front.
        static Mesh Horseshoe()
        {
            var b=new PropMesh(2);const int N=16;const float Half=.0045f;
            Vector2 Outer(float deg)=>new Vector2(.056f*Mathf.Cos(deg*Mathf.Deg2Rad),.058f*Mathf.Sin(deg*Mathf.Deg2Rad));
            Vector2 Inner(float deg)=>new Vector2(.037f*Mathf.Cos(deg*Mathf.Deg2Rad),.039f*Mathf.Sin(deg*Mathf.Deg2Rad));
            var corners=new List<(Vector2 p,float r,int steps)>();
            for(int i=0;i<=N;i++)corners.Add((Outer(Mathf.Lerp(160,380,i/(float)N)),i==0||i==N?.003f:0,2));
            for(int i=N;i>=0;i--)corners.Add((Inner(Mathf.Lerp(160,380,i/(float)N)),i==0||i==N?.002f:0,2));
            b.Slab(0,i=>0,RoundCorners(corners,true).ToArray(),.0015f,p=>Half,p=>0,1,1/.05f);
            foreach(var deg in new[]{188f,214f,240f,300f,326f,352f})
            {
                float a=deg*Mathf.Deg2Rad;var c=new Vector2(.0465f*Mathf.Cos(a),.0485f*Mathf.Sin(a));var t=new Vector2(-Mathf.Sin(a),Mathf.Cos(a))*.0026f;var r=new Vector2(Mathf.Cos(a),Mathf.Sin(a))*.0015f;
                Vector3 At(Vector2 p)=>new Vector3(p.x,p.y,-Half-.0002f);
                b.Quad(1,b.Add(At(c-t-r),Vector3.back,Vector2.zero),b.Add(At(c+t-r),Vector3.back,Vector2.zero),b.Add(At(c+t+r),Vector3.back,Vector2.zero),b.Add(At(c-t+r),Vector3.back,Vector2.zero));
            }
            return b.Save(Meshes+"Horseshoe.asset","Horseshoe");
        }

        // A squat shot glass on its base (+y), 47 mm across the rim, 31 mm tall, a thick bottom and a finger of whiskey.
        static Mesh ShotGlass()
        {
            var b=new PropMesh(2);
            b.Revolve(0,16,new[]{new Vector2(0,0),new Vector2(.0175f,0),new Vector2(.0175f,0),new Vector2(.0190f,.0015f),new Vector2(.0236f,.0300f),new Vector2(.0236f,.0300f),
                new Vector2(.0229f,.0307f),new Vector2(.0222f,.0300f),new Vector2(.0222f,.0300f),new Vector2(.0180f,.0095f),new Vector2(.0180f,.0095f),new Vector2(0,.0095f)});
            b.Revolve(1,16,new[]{new Vector2(0,.0097f),new Vector2(.0179f,.0097f),new Vector2(.0179f,.0097f),new Vector2(.0200f,.0205f),new Vector2(.0200f,.0205f),new Vector2(0,.0205f)});
            return b.Save(Meshes+"Shot glass.asset","Shot glass");
        }

        // A ten-hole harmonica 80 x 21 x 15 mm along x: a wooden comb between two chrome covers, the holes on the front (-z).
        static Mesh Harmonica()
        {
            var b=new PropMesh(3);
            b.BevelBox(1,new Vector3(-.0385f,-.0056f,-.0068f),new Vector3(.0385f,.0056f,.0068f),.0006f);
            b.BevelBox(0,new Vector3(-.040f,.0050f,-.0075f),new Vector3(.040f,.0105f,.0075f),.0016f);
            b.BevelBox(0,new Vector3(-.040f,-.0105f,-.0075f),new Vector3(.040f,-.0050f,.0075f),.0016f);
            for(int k=0;k<10;k++)
            {
                float x=(k-4.5f)*.0072f,z=-.0069f;var n=Vector3.back;
                b.Quad(2,b.Add(new Vector3(x-.0024f,-.0028f,z),n,Vector2.zero),b.Add(new Vector3(x+.0024f,-.0028f,z),n,Vector2.zero),b.Add(new Vector3(x+.0024f,.0028f,z),n,Vector2.zero),b.Add(new Vector3(x-.0024f,.0028f,z),n,Vector2.zero));
            }
            return b.Save(Meshes+"Harmonica.asset","Harmonica");
        }

        // A six-pointed sheriff's star 57 mm across the balls, 3 mm thick, a ball on every point and a raised disc each side.
        static Mesh SheriffStar()
        {
            var b=new PropMesh(1);const int Points=6;const float Tip=.0245f,Inner=.0125f,Half=.0015f,Ball=.0040f;
            var corners=new List<(Vector2 p,float r,int steps)>();
            for(int i=0;i<Points*2;i++){float a=Mathf.PI/2+i*Mathf.PI/Points;corners.Add((new Vector2(Mathf.Cos(a),Mathf.Sin(a))*(i%2==0?Tip:Inner),i%2==0?.0015f:.0012f,1));}
            b.Slab(0,i=>0,RoundCorners(corners,true).ToArray(),.0008f,p=>Half,p=>0,1,UvPerMetre);
            for(int i=0;i<Points;i++)
            {
                float a=Mathf.PI/2+i*2*Mathf.PI/Points;var c=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*(Tip-Ball*.15f);
                b.SmoothLathe(0,c,Vector3.forward,6,new[]{new Vector2(0,-Ball),new Vector2(Ball*.87f,-Ball*.5f),new Vector2(Ball*.87f,Ball*.5f),new Vector2(0,Ball)});
            }
            foreach(var s in new[]{1f,-1f})b.SmoothLathe(0,Vector3.zero,Vector3.forward*s,10,new[]{new Vector2(.0100f,Half-.0002f),new Vector2(.0094f,Half+.0010f),new Vector2(0,Half+.0010f)},1);
            return b.Save(Meshes+"Sheriff star.asset","Sheriff star");
        }

        // A cattleman cowboy hat at full size, like the user's references (2026-11-16): dark felt, the brim 310 x 360 mm
        // curled up at the sides and dipping front and back, the crown 110 mm tall at the front and 126 behind with a
        // crease down its middle and pinched in either side of the front, a braided leather band. +z is the front, y up.
        // The shadow is the same shape with a twelfth of the triangles (coarse: it is hidden inside the boot's shadow).
        const float CrownRx=.080f,CrownRz=.098f,BrimRx=.155f,BrimRz=.180f,BrimThick=.004f;
        static float RimHeight(float th)=>.118f-.008f*Mathf.Cos(th);
        // t 0..1 up the crown's side from the brim to the top edge, then 1..2 across the top to the middle.
        static Vector3 CrownPoint(float th,float t)
        {
            var oval=new Vector2(Mathf.Sin(th)*CrownRx,Mathf.Cos(th)*CrownRz);
            if(t<=1)
            {
                float off=Mathf.Abs(Mathf.DeltaAngle(0,th*Mathf.Rad2Deg));
                float pinch=1-.22f*t*t*Mathf.Exp(-Mathf.Pow((off-38f)/24f,2));
                var xz=oval*(Mathf.Lerp(1,.86f,t)*pinch*(1+.04f*Mathf.Sin(Mathf.PI*t)));
                return new Vector3(xz.x,RimHeight(th)*t,xz.y);
            }
            float s=2-t;var rim=CrownPoint(th,1);var p=new Vector3(rim.x*s,0,rim.z*s);
            float crease=.022f*Mathf.Pow(Mathf.Clamp01(1-Mathf.Abs(p.x)/.045f),1.5f)*Mathf.SmoothStep(0,1,Mathf.Clamp01((1-s)*2.2f));
            p.y=RimHeight(th)+.004f*(1-s)+.006f*Mathf.Sin(Mathf.PI*s)-crease;
            return p;
        }
        static Vector3 BrimPoint(float th,float w,float down)
        {
            float lift=(.050f*Mathf.Pow(Mathf.Abs(Mathf.Sin(th)),2.5f)-.012f*Mathf.Pow(Mathf.Cos(th),2))*Mathf.Pow(Mathf.Max(w,0),1.7f);
            // From just inside the crown's foot (so no slit shows where they meet) out to the edge.
            return new Vector3(Mathf.Sin(th)*Mathf.Lerp(CrownRx*.97f,BrimRx,w),lift-down,Mathf.Cos(th)*Mathf.Lerp(CrownRz*.97f,BrimRz,w));
        }
        static Mesh Hat(bool shadow)
        {
            const int Felt=0,Band=1;var b=new PropMesh(shadow?1:2);int around=shadow?12:32;
            // A surface over (angle round, t) rows: smooth normals from the surface itself, turned to face 'outward'.
            int[,] Surface(int m,float[] ts,System.Func<float,float,Vector3> at,System.Func<Vector3,Vector3> outward,System.Func<float,float,Vector2> uv)
            {
                var ring=new int[ts.Length,around+1];const float e=.002f;
                for(int j=0;j<ts.Length;j++)for(int i=0;i<=around;i++)
                {
                    float th=i*2*Mathf.PI/around,t=ts[j];var p=at(th,t);
                    // Scaled up first: the differences are millimetres, and normalized gives zero below 1e-5.
                    var n=Vector3.Cross((at(th,t+e)-at(th,t-e))*1000,(at(th+e,t)-at(th-e,t))*1000).normalized;if(Vector3.Dot(n,outward(p))<0)n=-n;
                    ring[j,i]=b.Add(p,n,uv(th,t));
                }
                for(int j=0;j+1<ts.Length;j++)for(int i=0;i<around;i++)b.Quad(m,ring[j,i],ring[j+1,i],ring[j+1,i+1],ring[j,i+1]);
                return ring;
            }
            void Fan(int m,int[,] ring,int row,Vector3 centre,Vector3 n){int hub=b.Add(centre,n,new Vector2(.5f,.5f));for(int i=0;i<around;i++)b.Tri(m,hub,ring[row,i],ring[row,i+1]);}
            Vector3 Out(Vector3 p)=>new Vector3(p.x,0,p.z);
            // Crown: up the side and over the top, closed with a fan in the middle of the crease.
            var crownT=shadow?new[]{0f,.5f,1f,1.5f,1.88f}:new[]{0f,.25f,.5f,.75f,1f,1.15f,1.35f,1.6f,1.88f};
            // Outward is away from a point inside the crown (the crease dips the top below the side's upper edge).
            var crown=Surface(Felt,crownT,CrownPoint,p=>p-new Vector3(0,.04f,0),(th,t)=>new Vector2(th/Mathf.PI*1.5f,t*.6f));
            var top=CrownPoint(0,2);Fan(Felt,crown,crownT.Length-1,new Vector3(0,top.y,0),Vector3.up);
            // Brim: its top, its underside, the edge between them, and the felt lining across the head opening.
            var brimW=shadow?new[]{0f,1f}:new[]{0f,.4f,.75f,1f};
            var brimTop=Surface(Felt,brimW,(th,w)=>BrimPoint(th,w,0),p=>Vector3.up,(th,w)=>new Vector2(BrimPoint(th,w,0).x*4,BrimPoint(th,w,0).z*4));
            var brimUnder=Surface(Felt,brimW,(th,w)=>BrimPoint(th,w,BrimThick),p=>Vector3.down,(th,w)=>new Vector2(BrimPoint(th,w,0).x*4,BrimPoint(th,w,0).z*4));
            int last=brimW.Length-1;
            for(int i=0;i<around;i++)
            {
                float th0=i*2*Mathf.PI/around,th1=(i+1)*2*Mathf.PI/around;
                Vector3 n0=Out(BrimPoint(th0,1,0)).normalized,n1=Out(BrimPoint(th1,1,0)).normalized;
                b.Quad(Felt,b.Add(BrimPoint(th0,1,0),n0,Vector2.zero),b.Add(BrimPoint(th1,1,0),n1,Vector2.zero),b.Add(BrimPoint(th1,1,BrimThick),n1,Vector2.zero),b.Add(BrimPoint(th0,1,BrimThick),n0,Vector2.zero));
            }
            Fan(Felt,brimUnder,0,new Vector3(0,-BrimThick,0),Vector3.down);
            if(!shadow)
            {
                // The band: braided leather round the foot of the crown, 20 mm deep, standing 2.5 mm proud, its lips flat.
                const float Y0=.005f,Y1=.025f,Proud=.0025f;var rows=new int[6,around+1];
                for(int i=0;i<=around;i++)
                {
                    float th=i*2*Mathf.PI/around,u=th/(2*Mathf.PI)*14;
                    var lo=CrownPoint(th,Y0/RimHeight(th));var hi=CrownPoint(th,Y1/RimHeight(th));var nLo=Out(lo).normalized;var nHi=Out(hi).normalized;
                    rows[0,i]=b.Add(lo,Vector3.down,new Vector2(u,0));rows[1,i]=b.Add(lo+nLo*Proud,Vector3.down,new Vector2(u,0));
                    rows[2,i]=b.Add(lo+nLo*Proud,nLo,new Vector2(u,0));rows[3,i]=b.Add(hi+nHi*Proud,nHi,new Vector2(u,1));
                    rows[4,i]=b.Add(hi+nHi*Proud,Vector3.up,new Vector2(u,1));rows[5,i]=b.Add(hi,Vector3.up,new Vector2(u,1));
                }
                for(int i=0;i<around;i++)foreach(var j in new[]{0,2,4})b.Quad(Band,rows[j,i],rows[j+1,i],rows[j+1,i+1],rows[j,i+1]);
            }
            return b.Save(Meshes+(shadow?"Cowboy hat shadow.asset":"Cowboy hat.asset"),shadow?"Cowboy hat shadow":"Cowboy hat");
        }

        // --- Textures --------------------------------------------------------------------------------------------------
        // Dark blue-black felt, faintly mottled and dusty (kept low: the gym's light lifts lit faces about 1.5x).
        static Texture2D Felt()
        {
            const int S=256;var tex=new Texture2D(S,S,TextureFormat.RGB24,false,false);
            for(int y=0;y<S;y++)for(int x=0;x<S;x++)
            {
                float u=x/(float)S,v=y/(float)S,n=Fbm(u,v,8,8,4,21),fibre=Fbm(u,v,48,48,2,23);
                var c=Color.Lerp(new Color(.075f,.085f,.115f),new Color(.12f,.13f,.16f),n*.8f+fibre*.2f);
                c=Color.Lerp(c,new Color(.22f,.21f,.20f),Mathf.Clamp01((Fbm(u,v,3,3,3,25)-.6f)*3)*.35f);tex.SetPixel(x,y,c);
            }
            return SaveTexture(FeltPath,tex,false);
        }
        // One plait of the band per repeat along u, v across: three strands as chevrons, each shaded round, with dark gaps.
        static Texture2D Braid()
        {
            const int W=64,H=32;var tex=new Texture2D(W,H,TextureFormat.RGB24,false,false);
            for(int y=0;y<H;y++)for(int x=0;x<W;x++)
            {
                float u=x/(float)W,v=(y+.5f)/H,f=Mathf.Repeat(u*2+Mathf.Abs(v-.5f)*1.6f,1);
                float round=Mathf.Sin(f*Mathf.PI),edge=Mathf.Min(v,1-v);
                var c=Color.Lerp(new Color(.16f,.09f,.05f),new Color(.42f,.26f,.14f),round);c*=Mathf.Lerp(.6f,1,Mathf.Clamp01(edge*8));tex.SetPixel(x,y,c);
            }
            return SaveTexture(BraidPath,tex,false);
        }
        static Texture2D Iron()
        {
            const int S=256;var tex=new Texture2D(S,S,TextureFormat.RGB24,false,false);
            for(int y=0;y<S;y++)for(int x=0;x<S;x++)
            {
                float u=x/(float)S,v=y/(float)S,n=Fbm(u,v,8,8,4,5),rust=Mathf.Clamp01((Fbm(u,v,4,4,3,9)-.52f)*5);
                var c=Color.Lerp(new Color(.26f,.25f,.25f),new Color(.42f,.41f,.40f),n);c=Color.Lerp(c,new Color(.50f,.27f,.13f),rust*.7f);tex.SetPixel(x,y,c);
            }
            return SaveTexture(IronPath,tex,false);
        }
    }
}
