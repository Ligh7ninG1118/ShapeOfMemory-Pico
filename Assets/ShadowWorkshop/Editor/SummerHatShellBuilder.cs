using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static ShadowWorkshopIntegration.Editor.PropMesh;

namespace ShadowWorkshopIntegration.Editor
{
    // Summer (2026-11-17): the SUN HAT and the SEASHELL for the seal level (SummerBuilder), staged in ShadowGym under
    // Summer props for the user to arrange. Built like the Wild West props: procedural, real size in metres, +y up, -z the
    // front, smooth normals doing the rounding, textures drawn here (detail kept at about 0.6: the gym's light lifts lit
    // faces ~1.5x). Each has a coarse closed twin that casts its shadow.
    public static class SummerHatShellBuilder
    {
        const string HatFile="Summer sun hat",HatShadowFile="Summer sun hat shadow",ShellFile="Summer seashell",ShellShadowFile="Summer seashell shadow";
        const string StrawPath=SummerBuilder.Textures+"summer hat straw weave.png",RibbonPath=SummerBuilder.Textures+"summer hat ribbon.png",ShellTexPath=SummerBuilder.Textures+"summer scallop shell.png";
        static Texture2D Load(string path)=>AssetDatabase.LoadAssetAtPath<Texture2D>(path);

        [MenuItem("MetaHackthone/Shadow Workshop/Summer/Build sun hat and seashell")]
        public static void Build()
        {
            StrawTexture();RibbonTexture();ShellTexture();
            var hat=Hat(false);var hatShadow=Hat(true);
            var hatMats=PropMesh.Materials(SummerBuilder.MaterialFolder,new[]{
                new Look("summer hat straw",Color.white,0f,.15f){baseMap=()=>Load(StrawPath)},
                new Look("summer hat ribbon",Color.white,0f,.35f){baseMap=()=>Load(RibbonPath)}});
            SummerBuilder.Stage("SUN HAT",hat,hatShadow,hatMats);
            var shell=Shell();var shellShadow=ShellHull();
            var shellMats=PropMesh.Materials(SummerBuilder.MaterialFolder,new[]{
                new Look("summer scallop shell",Color.white,0f,.35f){baseMap=()=>Load(ShellTexPath)},
                new Look("summer scallop inside",new Color(.60f,.50f,.43f),0f,.60f)});
            SummerBuilder.Stage("SEASHELL",shell,shellShadow,shellMats);
            string Size(Mesh m)=>$"{m.bounds.size.x*1000:0} x {m.bounds.size.y*1000:0} x {m.bounds.size.z*1000:0} mm";
            Debug.Log($"SUMMER_HAT_SHELL_OK sun hat {hat.triangles.Length/3} (shadow {hatShadow.triangles.Length/3}) {Size(hat)}; seashell {shell.triangles.Length/3} (shadow {shellShadow.triangles.Length/3}) {Size(shell)}");
        }

        // A surface over parameter rows (us across, ts along): smooth normals from the surface itself, turned toward 'outward'.
        static int[,] Surface(PropMesh b,int m,float[] us,float[] ts,Func<float,float,Vector3> at,Vector3 outward,Func<float,float,Vector2> uv)
        {
            var grid=new int[ts.Length,us.Length];const float e=.002f;
            for(int j=0;j<ts.Length;j++)for(int i=0;i<us.Length;i++)
            {
                float u=us[i],t=ts[j];var p=at(u,t);
                // Scaled up first: the differences are millimetres, and normalized gives zero below 1e-5.
                var du=(at(u+e,t)-at(u-e,t))*1000;if(du.sqrMagnitude<1e-8f)du=(at(u+e,t+.04f)-at(u-e,t+.04f))*1000;
                var dt=(at(u,t+e)-at(u,t-e))*1000;
                var n=Vector3.Cross(dt,du).normalized;if(Vector3.Dot(n,outward)<0)n=-n;
                grid[j,i]=b.Add(p,n,uv(u,t));
            }
            for(int j=0;j+1<ts.Length;j++)for(int i=0;i+1<us.Length;i++)b.Quad(m,grid[j,i],grid[j+1,i],grid[j+1,i+1],grid[j,i+1]);
            return grid;
        }
        // A flat outline extruded along z between zFront (-z side) and zBack, flat-shaded sides.
        static void Prism(PropMesh b,int m,Vector2[] outline,float zFront,float zBack,Func<Vector2,Vector2> uv)
        {
            var tris=Triangulate(outline);int count=outline.Length;float area=0;
            for(int i=0;i<count;i++){var p=outline[i];var q=outline[(i+1)%count];area+=p.x*q.y-q.x*p.y;}
            foreach(var (z,n) in new[]{(zFront,Vector3.back),(zBack,Vector3.forward)})
            {
                int s=b.v.Count;foreach(var p in outline)b.Add(new Vector3(p.x,p.y,z),n,uv(p));
                for(int k=0;k<tris.Count;k+=3)b.Tri(m,s+tris[k],s+tris[k+1],s+tris[k+2]);
            }
            for(int i=0;i<count;i++)
            {
                var p0=outline[i];var p1=outline[(i+1)%count];var d=(p1-p0).normalized;var n=new Vector3(d.y,-d.x,0)*(area>0?1:-1);
                b.Quad(m,b.Add(new Vector3(p0.x,p0.y,zFront),n,uv(p0)),b.Add(new Vector3(p1.x,p1.y,zFront),n,uv(p1)),b.Add(new Vector3(p1.x,p1.y,zBack),n,uv(p1)),b.Add(new Vector3(p0.x,p0.y,zBack),n,uv(p0)));
            }
        }

        // --- Sun hat -----------------------------------------------------------------------------------------------------
        // A floppy summer sun hat: a round crown 180 mm across and 106 mm tall, a soft brim 420 mm across that droops (more
        // at the sides) and waves at its edge, a coral grosgrain band with a flat bow at the back left. Woven straw.
        const float CrownBase=.088f,BrimIn=CrownBase*.97f,BrimOut=.210f,BrimThick=.004f;
        const float WeavePerMetre=31f,CrownRepeat=18,RibbonRepeat=28;
        static float Droop(float th)=>.030f+.014f*Mathf.Cos(th)*Mathf.Cos(th);
        static float Wave(float th)=>.012f*Mathf.Sin(5*th+.6f)+.006f*Mathf.Sin(3*th+2.2f);
        // th round the crown (x = cos, z = sin, as Revolve), w 0..1 from just inside the crown's foot to the edge.
        static Vector3 BrimPoint(float th,float w,float down)
        {
            float r=Mathf.LerpUnclamped(BrimIn,BrimOut+.006f*Mathf.Sin(4*th+.9f),w),aw=Mathf.Abs(w);
            float y=-Droop(th)*Mathf.Pow(aw,1.6f)+Wave(th)*aw*aw-down;
            return new Vector3(Mathf.Cos(th)*r,y,Mathf.Sin(th)*r);
        }
        static Mesh Hat(bool shadow)
        {
            const int Straw=0,Ribbon=1;var b=new PropMesh(shadow?1:2);int crownSides=shadow?12:16,brimSides=shadow?16:30;
            // Crown: a dome, the weave running round it and up it by arc length.
            var crown=shadow?new[]{new Vector2(CrownBase,-.002f),new Vector2(.0905f,.034f),new Vector2(.075f,.086f),new Vector2(0,.106f)}
                :new[]{new Vector2(CrownBase,-.002f),new Vector2(.0905f,.034f),new Vector2(.083f,.074f),new Vector2(.054f,.099f),new Vector2(0,.106f)};
            int start=b.v.Count;b.Revolve(Straw,crownSides,crown);float arc=0;
            for(int i=0;i<crown.Length;i++)
            {
                if(i>0)arc+=Vector2.Distance(crown[i-1],crown[i]);
                for(int k=0;k<=crownSides;k++){int at=start+i*(crownSides+1)+k;b.uv[at]=new Vector2(b.uv[at].x*CrownRepeat,arc*WeavePerMetre);}
            }
            // Brim: top, underside, the edge between them, and the inside of the crown seen from below.
            var th=Enumerable.Range(0,brimSides+1).Select(k=>k*2*Mathf.PI/brimSides).ToArray();
            var ws=shadow?new[]{0f,1f}:new[]{0f,.48f,1f};
            Vector2 BrimUv(float t,float w){var p=BrimPoint(t,w,0);return new Vector2(p.x,p.z)*WeavePerMetre;}
            Surface(b,Straw,th,ws,(t,w)=>BrimPoint(t,w,0),Vector3.up,BrimUv);
            Surface(b,Straw,th,ws,(t,w)=>BrimPoint(t,w,BrimThick),Vector3.down,BrimUv);
            var edge=new int[2,brimSides+1];
            for(int i=0;i<=brimSides;i++)
            {
                var p=BrimPoint(th[i],1,0);var n=(p-BrimPoint(th[i],.96f,0)).normalized;var uv=new Vector2(th[i]/(2*Mathf.PI)*40,0);
                edge[0,i]=b.Add(p,n,uv);edge[1,i]=b.Add(BrimPoint(th[i],1,BrimThick),n,uv+new Vector2(0,.12f));
            }
            for(int i=0;i<brimSides;i++)b.Quad(Straw,edge[0,i],edge[1,i],edge[1,i+1],edge[0,i+1]);
            int hub=b.Add(new Vector3(0,shadow?-BrimThick:.07f,0),Vector3.down,Vector2.zero);var ring=new int[brimSides+1];
            for(int i=0;i<=brimSides;i++)
            {
                var radial=new Vector3(Mathf.Cos(th[i]),0,Mathf.Sin(th[i]));
                ring[i]=b.Add(BrimPoint(th[i],0,BrimThick),shadow?Vector3.down:(Vector3.down*.8f-radial*.6f).normalized,BrimUv(th[i],0));
            }
            for(int i=0;i<brimSides;i++)b.Tri(Straw,hub,ring[i],ring[i+1]);
            if(!shadow)
            {
                // The band: grosgrain round the crown's foot, 26 mm deep, standing 2.2 mm proud, a flat lip on top.
                const float Y0=.002f,Y1=.028f,Proud=.0022f;
                float R(float y)=>Mathf.Lerp(CrownBase,.0905f,(y+.002f)/.036f);
                var rows=new int[4,crownSides+1];
                for(int k=0;k<=crownSides;k++)
                {
                    float t=k*2*Mathf.PI/crownSides,u=(float)k/crownSides*RibbonRepeat;var radial=new Vector3(Mathf.Cos(t),0,Mathf.Sin(t));
                    var n=(radial*.036f-Vector3.up*.0025f).normalized;
                    rows[0,k]=b.Add(radial*(R(Y0)+Proud)+Vector3.up*Y0,n,new Vector2(u,0));rows[1,k]=b.Add(radial*(R(Y1)+Proud)+Vector3.up*Y1,n,new Vector2(u,1));
                    rows[2,k]=b.Add(radial*(R(Y1)+Proud)+Vector3.up*Y1,Vector3.up,new Vector2(u,1));rows[3,k]=b.Add(radial*(R(Y1)-.0005f)+Vector3.up*Y1,Vector3.up,new Vector2(u,.94f));
                }
                for(int k=0;k<crownSides;k++){b.Quad(Ribbon,rows[0,k],rows[1,k],rows[1,k+1],rows[0,k+1]);b.Quad(Ribbon,rows[2,k],rows[3,k],rows[3,k+1],rows[2,k+1]);}
                // The bow: two loops and two short tails, flat on the band at a crown corner (back left), bent round with it.
                float tb=6*2*Mathf.PI/crownSides,tan=Mathf.Tan(Mathf.PI/crownSides);const float Yb=.018f,Thick=.003f;
                var d=new Vector3(Mathf.Cos(tb),0,Mathf.Sin(tb));var right=new Vector3(d.z,0,-d.x);float rb=R(Yb)+Proud;
                var bow=new[]{new Vector2(.008f,.006f),new Vector2(.030f,.017f),new Vector2(.037f,.004f),new Vector2(.035f,-.008f),new Vector2(.029f,-.013f),new Vector2(.008f,-.006f),
                    new Vector2(.017f,-.016f),new Vector2(.010f,-.018f),new Vector2(.002f,-.010f),new Vector2(-.002f,-.010f),new Vector2(-.010f,-.018f),new Vector2(-.017f,-.016f),
                    new Vector2(-.008f,-.006f),new Vector2(-.029f,-.013f),new Vector2(-.035f,-.008f),new Vector2(-.037f,.004f),new Vector2(-.030f,.017f),new Vector2(-.008f,.006f)};
                float Base(Vector2 p)=>rb-.0008f-Mathf.Abs(p.x)*tan;
                Vector3 World(Vector2 p,float z)=>right*p.x+Vector3.up*(p.y+Yb)+d*z;
                Vector3 Dir(Vector3 l)=>(right*l.x+Vector3.up*l.y+d*l.z).normalized;
                Vector2 BowUv(Vector2 p)=>new Vector2(p.x/.02f,p.y/.03f+.5f);
                int capStart=b.v.Count;
                foreach(var p in bow)b.Add(World(p,Base(p)+Thick),Dir(new Vector3(Mathf.Sign(p.x)*tan,p.y/.02f*.5f,1)),BowUv(p));
                var capTris=Triangulate(bow);for(int k=0;k<capTris.Count;k+=3)b.Tri(Ribbon,capStart+capTris[k],capStart+capTris[k+1],capStart+capTris[k+2]);
                int count=bow.Length;float area=0;for(int i=0;i<count;i++){var p=bow[i];var q=bow[(i+1)%count];area+=p.x*q.y-q.x*p.y;}
                var wall=new int[2,count];
                for(int i=0;i<count;i++)
                {
                    var p=bow[i];var t=(bow[(i+1)%count]-bow[(i+count-1)%count]).normalized;var n2=new Vector2(t.y,-t.x)*(area>0?1:-1);var n=Dir(new Vector3(n2.x,n2.y,0));
                    wall[0,i]=b.Add(World(p,Base(p)+Thick),n,BowUv(p));wall[1,i]=b.Add(World(p,Base(p)),n,BowUv(p)+new Vector2(0,.08f));
                }
                for(int i=0;i<count;i++){int j=(i+1)%count;b.Quad(Ribbon,wall[0,i],wall[1,i],wall[1,j],wall[0,j]);}
            }
            string file=shadow?HatShadowFile:HatFile;
            return b.Save(SummerBuilder.Meshes+file+".asset",file);
        }

        // --- Seashell ----------------------------------------------------------------------------------------------------
        // A scallop 90 mm wide and 84 mm tall: a fan of 11 rounded ribs from the hinge (the rim scalloped by them), the back
        // gently domed toward -z with the beak at the hinge, two square ears either side of it. The inside is a smooth
        // hollow with the ribs showing as grooves near the rim.
        const int Ribs=11,PerRib=4;const float Fan=70*Mathf.Deg2Rad,ShellDepth=.020f,ShellThick=.0016f;
        static float RibU(float a)=>(a+Fan)/(2*Fan)*Ribs;
        static float Rib(float a)=>Mathf.Pow(Mathf.Abs(Mathf.Sin(Mathf.PI*RibU(a))),.8f);  // 0 in the grooves, 1 on the crests
        static float RimRadius(float a){const float c=.032f,r=.045f;float s=c*Mathf.Sin(a);return (c*Mathf.Cos(a)+Mathf.Sqrt(r*r-s*s))*(1+.05f*(Rib(a)-.5f));}
        static float Dome(float s)=>(1-s*s)*(.7f+.8f*s);
        static float Across(float a,float s)=>Mathf.Lerp(1,.45f+.55f*Mathf.Cos(a/Fan*Mathf.PI/2),Mathf.Clamp01(s*3));
        static float RibAmp(float s)=>.0035f*Mathf.Pow(Mathf.Abs(s),.8f);
        // a: angle from straight up (-Fan..Fan, +x to the right), s: 0 at the hinge to 1 at the rim.
        static Vector3 ShellPoint(float a,float s,bool inside)
        {
            var xy=new Vector2(Mathf.Sin(a),Mathf.Cos(a))*RimRadius(a)*s;
            float z=inside?-(ShellDepth*.78f*Dome(s)*Across(a,s)+RibAmp(s)*.85f*Rib(a))+ShellThick:-(ShellDepth*Dome(s)*Across(a,s)+RibAmp(s)*Rib(a));
            return new Vector3(xy.x,xy.y,z);
        }
        static readonly Vector2[] Ears={new Vector2(-.0205f,-.0055f),new Vector2(.0205f,-.0055f),new Vector2(.0200f,.0073f),new Vector2(0,0),new Vector2(-.0200f,.0073f)};
        const float EarFront=-.0068f,EarBack=-.0040f;
        static Mesh Shell()
        {
            const int Out=0,In=1;var b=new PropMesh(2);int cols=Ribs*PerRib;
            var angles=Enumerable.Range(0,cols+1).Select(i=>-Fan+2*Fan*i/cols).ToArray();var ss=new[]{0f,.34f,.68f,1f};
            Surface(b,Out,angles,ss,(a,s)=>ShellPoint(a,s,false),Vector3.back,(a,s)=>new Vector2(RibU(a),s));
            Surface(b,In,angles,ss,(a,s)=>ShellPoint(a,s,true),Vector3.forward,(a,s)=>new Vector2(RibU(a),s));
            // The rim between outside and inside, facing out from the hinge.
            var rim=new int[2,cols+1];
            for(int i=0;i<=cols;i++)
            {
                const float e=.002f;var t=ShellPoint(angles[i]+e,1,false)-ShellPoint(angles[i]-e,1,false);var p=ShellPoint(angles[i],1,false);
                var n=new Vector3(-t.y,t.x,0).normalized;if(n.x*p.x+n.y*p.y<0)n=-n;
                rim[0,i]=b.Add(p,n,new Vector2(RibU(angles[i]),.99f));rim[1,i]=b.Add(ShellPoint(angles[i],1,true),n,new Vector2(RibU(angles[i]),.97f));
            }
            for(int i=0;i<cols;i++)b.Quad(Out,rim[0,i],rim[1,i],rim[1,i+1],rim[0,i+1]);
            // The two straight shoulders from the hinge to the rim.
            foreach(var (col,sign) in new[]{(0,-1f),(cols,1f)})
            {
                var n=new Vector3(sign*Mathf.Cos(Fan),-Mathf.Sin(Fan),0);var row=new int[2,ss.Length];
                for(int j=0;j<ss.Length;j++){row[0,j]=b.Add(ShellPoint(angles[col],ss[j],false),n,new Vector2(.02f,ss[j]));row[1,j]=b.Add(ShellPoint(angles[col],ss[j],true),n,new Vector2(.06f,ss[j]));}
                for(int j=0;j+1<ss.Length;j++)b.Quad(Out,row[0,j],row[1,j],row[1,j+1],row[0,j+1]);
            }
            // The ears: one flat piece across the hinge under the beak, its top along the shoulders.
            Prism(b,Out,Ears,EarFront,EarBack,p=>new Vector2(.5f+p.x*40,.35f+p.y*4));
            return b.Save(SummerBuilder.Meshes+ShellFile+".asset",ShellFile);
        }
        // The shadow: the outline (the rim at every groove and crest, the shoulders, the ears) with a domed front to a point
        // over the shell's highest place and a flat back.
        static Mesh ShellHull()
        {
            var b=new PropMesh(1);var pts=new List<Vector2>();var zf=new List<float>();var zb=new List<float>();
            for(int i=0;i<=Ribs*2;i++){var p=ShellPoint(-Fan+2*Fan*i/(Ribs*2f),1,false);pts.Add(new Vector2(p.x,p.y));zf.Add(-.002f);zb.Add(ShellThick);}
            foreach(var p in new[]{Ears[2],Ears[1],Ears[0],Ears[4]}){pts.Add(p);zf.Add(EarFront);zb.Add(EarBack);}
            int count=pts.Count;float area=0;for(int i=0;i<count;i++){var p=pts[i];var q=pts[(i+1)%count];area+=p.x*q.y-q.x*p.y;}
            foreach(var (apex,front) in new[]{(new Vector3(0,.026f,-.0185f),true),(new Vector3(0,.026f,ShellThick),false)})
            {
                var n=front?Vector3.back:Vector3.forward;int hub=b.Add(apex,n,Vector2.zero),s=b.v.Count;
                for(int i=0;i<count;i++)b.Add(new Vector3(pts[i].x,pts[i].y,front?zf[i]:zb[i]),n,Vector2.zero);
                for(int i=0;i<count;i++)b.Tri(0,hub,s+i,s+(i+1)%count);
            }
            for(int i=0;i<count;i++)
            {
                int j=(i+1)%count;var d=(pts[j]-pts[i]).normalized;var n=new Vector3(d.y,-d.x,0)*(area>0?1:-1);
                b.Quad(0,b.Add(new Vector3(pts[i].x,pts[i].y,zf[i]),n,Vector2.zero),b.Add(new Vector3(pts[j].x,pts[j].y,zf[j]),n,Vector2.zero),
                    b.Add(new Vector3(pts[j].x,pts[j].y,zb[j]),n,Vector2.zero),b.Add(new Vector3(pts[i].x,pts[i].y,zb[i]),n,Vector2.zero));
            }
            return b.Save(SummerBuilder.Meshes+ShellShadowFile+".asset",ShellShadowFile);
        }

        // --- Textures ----------------------------------------------------------------------------------------------------
        // Basket-woven straw, 8 x 8 strands a tile (a tile every 32 mm): each strand rounded across, dipping where it goes
        // under, fibre streaks along it, dark gaps between.
        static Texture2D StrawTexture()
        {
            const int S=256,Cells=8;var tex=new Texture2D(S,S,TextureFormat.RGB24,false,false);
            var light=new Color(.60f,.50f,.31f);var dark=new Color(.37f,.28f,.15f);var gap=new Color(.20f,.14f,.07f);
            for(int y=0;y<S;y++)for(int x=0;x<S;x++)
            {
                float u=(x+.5f)/S,v=(y+.5f)/S;int i=(int)(u*Cells),j=(int)(v*Cells);float fu=u*Cells-i,fv=v*Cells-j;
                bool flat=((i+j)&1)==0;float across=flat?fv:fu,along=flat?fu:fv,edge=Mathf.Abs(across-.5f)*2;
                float round=Mathf.Sqrt(Mathf.Clamp01(1-edge*edge)),dip=.72f+.28f*Mathf.Sin(Mathf.PI*along);
                float fibre=flat?Fbm(u,v,4,96,2,17):Fbm(v,u,4,96,2,19);
                var c=Color.Lerp(dark,light,round*dip*(.78f+.22f*fibre));
                if(edge>.86f)c=Color.Lerp(c,gap,.8f);
                c*=.90f+.12f*Fbm(u,v,4,4,3,5);tex.SetPixel(x,y,c);
            }
            return SaveTexture(StrawPath,tex,false);
        }
        // Coral grosgrain: u along the ribbon (8 fine ribs a tile, a tile every 20 mm), v across, darker selvedges.
        static Texture2D RibbonTexture()
        {
            const int W=64,H=32;var tex=new Texture2D(W,H,TextureFormat.RGB24,false,false);var coral=new Color(.62f,.25f,.21f);
            for(int y=0;y<H;y++)for(int x=0;x<W;x++)
            {
                float u=(x+.5f)/W,v=(y+.5f)/H,rib=Mathf.Sin(u*8*Mathf.PI*2)*.5f+.5f,edge=Mathf.Min(v,1-v);
                var c=coral*(.84f+.16f*rib);if(edge<.07f)c*=.80f;tex.SetPixel(x,y,c);
            }
            return SaveTexture(RibbonPath,tex,false);
        }
        // One rib wide (u: groove, crest at 0.5, groove), the hinge (v 0) to the rim (v 1): peach and coral toward the hinge
        // fading to cream at the rim, coral growth bands, fine growth lines, darker down the grooves.
        static Texture2D ShellTexture()
        {
            const int W=128,H=256;var tex=new Texture2D(W,H,TextureFormat.RGB24,false,false);
            var cream=new Color(.60f,.53f,.42f);var peach=new Color(.60f,.40f,.28f);var coral=new Color(.52f,.23f,.17f);
            for(int y=0;y<H;y++)for(int x=0;x<W;x++)
            {
                float u=(x+.5f)/W,s=(y+.5f)/H,crest=Mathf.Sin(Mathf.PI*u);
                float band=Mathf.SmoothStep(0,1,Mathf.Clamp01(.5f+.6f*Mathf.Sin(s*19f+1.2f+Fbm(u,s,2,4,2,7)*2.5f)));
                var c=Color.Lerp(peach,cream,Mathf.Clamp01(s*1.25f-.1f));c=Color.Lerp(c,coral,band*.5f*(1-s*.45f));
                c*=Mathf.Lerp(.60f,1.02f,Mathf.Pow(crest,.6f));
                c*=1-.12f*Mathf.Pow(Mathf.Abs(Mathf.Sin(s*120f)),14);
                c*=.92f+.12f*Fbm(u,s,4,16,3,11);tex.SetPixel(x,y,c);
            }
            return SaveTexture(ShellTexPath,tex,false);
        }
    }
}
