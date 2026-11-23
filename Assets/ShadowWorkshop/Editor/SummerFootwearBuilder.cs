using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static ShadowWorkshopIntegration.Editor.PropMesh;

namespace ShadowWorkshopIntegration.Editor
{
    // Summer footwear (2026-11-17, for the Summer level's seal; see SummerBuilder): a pair of thong flip flops and a pair
    // of short snorkelling fins, each at its real size. Built here: the right one of each pair (its big toe's side +x, y up,
    // the toe or the blade's tip toward -z); the left one is its mirror image (x negated, triangles rewound), not a negative
    // scale. Each casts its shadow from a light hull: the sole or the blade as a straight prism of its outline, the thong or
    // the foot pocket as a few coarse bars.
    public static class SummerFootwearBuilder
    {
        const string Meshes=SummerBuilder.Meshes,Textures=SummerBuilder.Textures,MaterialFolder=SummerBuilder.MaterialFolder;
        const string FootbedPath=Textures+"summer flip flop footbed.png",BladePath=Textures+"summer fin blade.png";
        static Texture2D Load(string path)=>AssetDatabase.LoadAssetAtPath<Texture2D>(path);

        [MenuItem("MetaHackthone/Shadow Workshop/Summer/Build footwear")]
        public static void Build()
        {
            FootbedTexture();BladeTexture();
            var flopLooks=PropMesh.Materials(MaterialFolder,new[]{
                new Look("summer flip flop sole",new Color(.04f,.42f,.46f),0f,.25f),
                new Look("summer flip flop footbed",Color.white,0f,.18f){baseMap=()=>Load(FootbedPath)},
                new Look("summer flip flop strap",new Color(.62f,.22f,.17f),0f,.50f)});
            var finLooks=PropMesh.Materials(MaterialFolder,new[]{
                new Look("summer fin blade",Color.white,0f,.55f){baseMap=()=>Load(BladePath)},
                new Look("summer fin rail",new Color(.50f,.25f,.09f),0f,.40f),
                new Look("summer fin pocket",new Color(.20f,.47f,.46f),0f,.35f),
                new Look("summer fin opening",new Color(.035f,.06f,.06f),0f,.10f)});
            var flop=FlipFlop();var flopShadow=FlipFlopShadow();var fin=SwimFin();var finShadow=SwimFinShadow();
            var pieces=new[]{
                ("FLIP FLOP L",Mirror(flop,"Summer flip flop L"),Mirror(flopShadow,"Summer flip flop L shadow"),flopLooks),
                ("FLIP FLOP R",flop,flopShadow,flopLooks),
                ("SWIM FIN L",Mirror(fin,"Summer swim fin L"),Mirror(finShadow,"Summer swim fin L shadow"),finLooks),
                ("SWIM FIN R",fin,finShadow,finLooks)};
            var notes=new List<string>();
            foreach(var (label,mesh,shadow,looks) in pieces)
            {
                SummerBuilder.Stage(label,mesh,shadow,looks);var s=mesh.bounds.size*1000;
                notes.Add($"{label} {mesh.triangles.Length/3} (shadow {shadow.triangles.Length/3}) {s.x:0}x{s.y:0}x{s.z:0} mm");
            }
            Debug.Log("SUMMER_FOOTWEAR_OK "+string.Join(", ",notes));
        }

        // --- Flip flop ---------------------------------------------------------------------------------------------------
        // The right sole's outline (mm; x across, the big toe +x; y from the heel to the toe): 261 long, 95 across the ball.
        static readonly Vector2[] Sole=new[]{
            new Vector2(0,-130),new Vector2(20,-127),new Vector2(32,-115),new Vector2(36,-95),new Vector2(35,-70),new Vector2(32,-42),new Vector2(32,-15),
            new Vector2(37,15),new Vector2(44,42),new Vector2(47,62),new Vector2(46,84),new Vector2(41,103),new Vector2(32,118),new Vector2(19,128),
            new Vector2(5,131),new Vector2(-9,128),new Vector2(-23,119),new Vector2(-35,103),new Vector2(-44,82),new Vector2(-48,60),new Vector2(-47,38),
            new Vector2(-42,10),new Vector2(-37,-20),new Vector2(-35,-50),new Vector2(-36,-80),new Vector2(-34,-105),new Vector2(-25,-122),new Vector2(-13,-129)}.Select(p=>p*.001f).ToArray();
        // Half its thickness: 16.8 mm at the heel to 12.4 at the toe, the bottom flat; the footbed lies 0.8 mm on top.
        static float SoleHalf(Vector2 p)=>Mathf.Lerp(.0084f,.0062f,Mathf.InverseLerp(-.13f,.13f,p.y));
        static float BedTop(Vector2 p)=>2*SoleHalf(p)+.0008f;
        // The thong (final frame): the toe post from between the first two toes up to a knob 40 mm over the sole, and from
        // the knob a strap to each side at mid-foot, arching over the instep and down into the sole's edge.
        static readonly Vector3[] Post={new Vector3(.016f,.008f,-.063f),new Vector3(.016f,.022f,-.060f),new Vector3(.014f,.033f,-.055f)};
        static readonly Vector3 Knot=new Vector3(.014f,.031f,-.055f);
        static readonly Vector3[][] Arms={
            new[]{Knot,new Vector3(.022f,.040f,-.032f),new Vector3(.028f,.031f,-.006f),new Vector3(.028f,.007f,.012f)},
            new[]{Knot,new Vector3(-.004f,.043f,-.034f),new Vector3(-.024f,.038f,-.010f),new Vector3(-.032f,.007f,.012f)}};
        static (Vector2[] at,Vector2[] normal) PostSection(float t){float k=t*t;return Ellipse(Mathf.Lerp(.0030f,.0072f,k),Mathf.Lerp(.0030f,.0062f,k),6);}
        // The strap stands on edge: 7 mm deep where it leaves the knob, 16 mm from a third of the way on, 3.5 mm thick.
        static float StrapWidth(float t)=>Mathf.Lerp(.007f,.016f,Mathf.Clamp01(t*2.2f));

        static Mesh FlipFlop()
        {
            const int Outsole=0,Footbed=1,Strap=2;const float R=.005f;var b=new PropMesh(3);
            b.Slab(Outsole,i=>Outsole,Sole,R,SoleHalf,SoleHalf,1,UvPerMetre);
            // The footbed: a lighter layer on top, 2.5 mm in from where the edge starts to round, the foot's print on it.
            var bed=Inset(Sole,R+.0025f);var tris=Triangulate(bed);int start=b.v.Count;const float e=.0005f;
            foreach(var p in bed)
            {
                float slope=(BedTop(p+Vector2.up*e)-BedTop(p-Vector2.up*e))/(2*e);
                b.Add(new Vector3(p.x,p.y,BedTop(p)),new Vector3(0,-slope,1).normalized,new Vector2(Mathf.InverseLerp(-.05f,.05f,p.x),Mathf.InverseLerp(-.135f,.135f,p.y)));
            }
            for(int k=0;k<tris.Count;k+=3)b.Tri(Footbed,start+tris[k],start+tris[k+1],start+tris[k+2]);
            LayDown(b,0);
            b.Bar(Strap,Post,Vector3.right,PostSection,0,.0065f,2);
            foreach(var arm in Arms)b.Bar(Strap,Curve(arm,2),Vector3.up,t=>RoundedRect(-StrapWidth(t)/2,StrapWidth(t)/2,-.00175f,.00175f,.0015f,1),0,0);
            return b.Save(Meshes+"Summer flip flop R.asset","Summer flip flop R");
        }
        static Mesh FlipFlopShadow()
        {
            var b=new PropMesh(1);
            Prism(b,Sole.Where((p,i)=>i%2==0).ToArray(),p=>0,BedTop);
            b.Bar(0,Post,Vector3.right,t=>Ellipse(Mathf.Lerp(.0035f,.0068f,t*t),Mathf.Lerp(.0035f,.006f,t*t),4),0,.006f,1);
            foreach(var arm in Arms)b.Bar(0,Curve(arm,1),Vector3.up,t=>Ellipse(StrapWidth(t)/2,.002f,4),0,0);
            return b.Save(Meshes+"Summer flip flop R shadow.asset","Summer flip flop R shadow");
        }

        // --- Swim fin ----------------------------------------------------------------------------------------------------
        // The blade's half width at y (m from the fin's middle toward the tip; the blade runs from -0.10, inside the pocket,
        // to the tip at 0.24): 36 mm inside the pocket, 50 where it leaves it, flaring to 95 at the tip.
        static float BladeHalf(float y)=>y<-.03f?Mathf.Lerp(.036f,.050f,Mathf.InverseLerp(-.10f,-.03f,y)):.050f+.045f*Mathf.Pow(Mathf.Clamp01((y+.03f)/.27f),.85f);
        // Half the blade's thickness: 2.4 mm at the pocket to 1.4 at the tip.
        static float BladeThick(float y)=>Mathf.Lerp(.0024f,.0014f,Mathf.InverseLerp(-.10f,.24f,y));
        static Vector2[] BladeOutline()
        {
            var ys=new[]{-.10f,-.06f,-.02f,.03f,.08f,.13f,.18f,.212f};var list=new List<Vector2>();
            foreach(var y in ys)list.Add(new Vector2(BladeHalf(y),y));
            // The tip: rounded corners and a shallow notch, 11 mm deep, in the middle.
            list.Add(new Vector2(.0925f,.229f));list.Add(new Vector2(.084f,.238f));list.Add(new Vector2(.042f,.236f));list.Add(new Vector2(0,.227f));
            list.Add(new Vector2(-.042f,.236f));list.Add(new Vector2(-.084f,.238f));list.Add(new Vector2(-.0925f,.229f));
            foreach(var y in Enumerable.Reverse(ys))list.Add(new Vector2(-BladeHalf(y),y));
            return list.ToArray();
        }
        // The foot pocket (final frame), heel opening to toe: oval sections, flat underneath, leaning a little to the big toe.
        static readonly (float z,float w,float yb,float yt,float x)[] PocketStations={(.240f,.040f,-.004f,.044f,0f),(.226f,.045f,-.006f,.051f,0f),
            (.180f,.048f,-.006f,.053f,.001f),(.130f,.047f,-.006f,.047f,.002f),(.085f,.044f,-.006f,.037f,.0025f),(.050f,.039f,-.005f,.025f,.003f),(.022f,.029f,-.004f,.012f,.0035f)};
        static readonly Vector3 PocketToe=new Vector3(.004f,.002f,.006f);
        static Vector3 PocketPoint(int s,float phi)
        {
            var (z,w,yb,yt,x)=PocketStations[s];float mid=(yb+yt)/2,hy=(yt-yb)/2,sn=Mathf.Sin(phi);
            float lift=sn<0?-Mathf.Pow(-sn,.35f):Mathf.Pow(sn,.75f);
            return new Vector3(x+w*Mathf.Cos(phi),mid+hy*lift,z);
        }
        static Vector3 PocketCentre(int s){var st=PocketStations[s];return new Vector3(st.x,(st.yb+st.yt)/2,st.z);}

        static Mesh SwimFin()
        {
            const int Blade=0,Rail=1,Pocket=2,Opening=3;var b=new PropMesh(4);
            // The blade, the rail colour round its edge. Its faces' UVs are (x, y) + 0.25 m at 2 per metre, so the texture's
            // 0..1 spans the blade's 0.5 m (BladeTexture draws it so).
            var off=new Vector2(.25f,.25f);
            b.Slab(Blade,i=>Rail,BladeOutline().Select(p=>p+off).ToArray(),.0013f,p=>BladeThick(p.y-off.y),p=>0,1,2);
            for(int i=0;i<b.v.Count;i++)b.v[i]-=new Vector3(off.x,off.y,0);
            LayDown(b,0);
            // The foot pocket: a loft of its sections, a rail-coloured rim round the heel opening, the toe closed to a point.
            const int Around=10;const float e=.01f;int count=PocketStations.Length;var grid=new int[count,Around+1];
            for(int s=0;s<count;s++)for(int k=0;k<=Around;k++)
            {
                float phi=k*2*Mathf.PI/Around;int s0=Mathf.Max(0,s-1),s1=Mathf.Min(count-1,s+1);var p=PocketPoint(s,phi);
                var along=PocketPoint(s1,phi)-PocketPoint(s0,phi);var round=PocketPoint(s,phi+e)-PocketPoint(s,phi-e);
                var n=Vector3.Cross(round*1000,along*1000).normalized;if(Vector3.Dot(n,p-PocketCentre(s))<0)n=-n;
                grid[s,k]=b.Add(p,n,new Vector2((float)k/Around,p.z*4));
            }
            for(int s=0;s+1<count;s++)for(int k=0;k<Around;k++)b.Quad(s==0?Rail:Pocket,grid[s,k],grid[s+1,k],grid[s+1,k+1],grid[s,k+1]);
            int tip=b.Add(PocketToe,new Vector3(0,.3f,-1).normalized,new Vector2(.5f,0));
            for(int k=0;k<Around;k++)b.Tri(Pocket,tip,grid[count-1,k],grid[count-1,k+1]);
            // The opening: a dark funnel 35 mm into the pocket, so it reads as a hole from any side.
            int hub=b.Add(new Vector3(0,.018f,.205f),Vector3.forward,new Vector2(.5f,.5f));var rim=new int[Around+1];
            for(int k=0;k<=Around;k++)rim[k]=b.Add(b.v[grid[0,k]],Vector3.forward,Vector2.zero);
            for(int k=0;k<Around;k++)b.Tri(Opening,hub,rim[k],rim[k+1]);
            // Raised side rails from the pocket to the tip's corners, and a centre rib down the blade.
            var railY=new[]{-.06f,0f,.06f,.12f,.18f,.224f};
            foreach(var side in new[]{1f,-1f})
                b.Bar(Rail,railY.Select(y=>new Vector3(side*(BladeHalf(y)-.0025f),0,-y)).ToList(),Vector3.right,t=>Ellipse(.0035f,.0050f,6),.005f,.003f,1);
            b.Bar(Rail,new[]{new Vector3(0,.0012f,.035f),new Vector3(0,.0012f,-.040f),new Vector3(0,.0012f,-.120f),new Vector3(0,.0012f,-.195f)},Vector3.right,t=>Ellipse(.003f,.0045f,6),0,.008f,1);
            return b.Save(Meshes+"Summer swim fin R.asset","Summer swim fin R");
        }
        static Mesh SwimFinShadow()
        {
            var b=new PropMesh(1);var outline=BladeOutline();
            // The blade (as thick as its rails stand), the notch kept; then the pocket on four sections of six points.
            Prism(b,new[]{0,2,4,6,8,9,11,13,14,16,18,20,22}.Select(i=>outline[i]).ToArray(),p=>-.0045f,p=>.0045f);
            var stations=new[]{0,2,4,6};const int A=6;var ring=new int[stations.Length,A];
            for(int i=0;i<stations.Length;i++)for(int k=0;k<A;k++)
            {
                var p=PocketPoint(stations[i],k*2*Mathf.PI/A);ring[i,k]=b.Add(p,(p-PocketCentre(stations[i])).normalized,Vector2.zero);
            }
            for(int i=0;i+1<stations.Length;i++)for(int k=0;k<A;k++)b.Quad(0,ring[i,k],ring[i+1,k],ring[i+1,(k+1)%A],ring[i,(k+1)%A]);
            int heel=b.Add(PocketCentre(0),Vector3.forward,Vector2.zero),toe=b.Add(PocketToe,Vector3.back,Vector2.zero);
            for(int k=0;k<A;k++){b.Tri(0,heel,ring[0,k],ring[0,(k+1)%A]);b.Tri(0,toe,ring[stations.Length-1,k],ring[stations.Length-1,(k+1)%A]);}
            return b.Save(Meshes+"Summer swim fin R shadow.asset","Summer swim fin R shadow");
        }

        // --- Helpers -----------------------------------------------------------------------------------------------------
        // A flat piece built in Slab's frame (outline in x, y; thickness along z) laid down: z becomes up, +y the front (-z).
        static void LayDown(PropMesh b,int from){for(int i=from;i<b.v.Count;i++){var p=b.v[i];var n=b.n[i];b.v[i]=new Vector3(p.x,p.z,-p.y);b.n[i]=new Vector3(n.x,n.z,-n.y);}}
        // The outline moved in by d (as Slab moves it in by its edge radius).
        static Vector2[] Inset(Vector2[] outline,float d)
        {
            int count=outline.Length;float area=0;
            for(int i=0;i<count;i++){var a=outline[i];var c=outline[(i+1)%count];area+=a.x*c.y-c.x*a.y;}
            var inner=new Vector2[count];
            for(int i=0;i<count;i++){var t=(outline[(i+1)%count]-outline[(i+count-1)%count]).normalized;inner[i]=outline[i]-new Vector2(t.y,-t.x)*(area>0?d:-d);}
            return inner;
        }
        // A straight prism over a flat outline (x, y; +y the front), laid down like LayDown, from bottom(p) up to top(p).
        static void Prism(PropMesh b,IList<Vector2> outline,System.Func<Vector2,float> bottom,System.Func<Vector2,float> top)
        {
            int count=outline.Count;float area=0;
            for(int i=0;i<count;i++){var a=outline[i];var c=outline[(i+1)%count];area+=a.x*c.y-c.x*a.y;}
            Vector3 At(Vector2 p,float h)=>new Vector3(p.x,h,-p.y);
            for(int i=0;i<count;i++)
            {
                var a=outline[i];var c=outline[(i+1)%count];var d=(c-a).normalized*(area>0?1:-1);var n=new Vector3(d.y,0,d.x);
                b.Quad(0,b.Add(At(a,bottom(a)),n,Vector2.zero),b.Add(At(c,bottom(c)),n,Vector2.zero),b.Add(At(c,top(c)),n,Vector2.zero),b.Add(At(a,top(a)),n,Vector2.zero));
            }
            var tris=Triangulate(outline.ToArray());
            foreach(var up in new[]{true,false})
            {
                int start=b.v.Count;foreach(var p in outline)b.Add(At(p,up?top(p):bottom(p)),up?Vector3.up:Vector3.down,Vector2.zero);
                for(int k=0;k<tris.Count;k+=3)b.Tri(0,start+tris[k],start+tris[k+1],start+tris[k+2]);
            }
        }
        // The other foot's piece: x negated (positions and normals), each triangle rewound, UVs kept (so a print mirrors too).
        static Mesh Mirror(Mesh src,string name)
        {
            string path=Meshes+name+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(!mesh){mesh=new Mesh();AssetDatabase.CreateAsset(mesh,path);}else mesh.Clear();
            mesh.name=name;mesh.indexFormat=src.indexFormat;
            var v=src.vertices;var n=src.normals;for(int i=0;i<v.Length;i++){v[i].x=-v[i].x;n[i].x=-n[i].x;}
            mesh.vertices=v;mesh.normals=n;mesh.uv=src.uv;mesh.subMeshCount=src.subMeshCount;
            for(int s=0;s<src.subMeshCount;s++){var t=src.GetTriangles(s);for(int k=0;k<t.Length;k+=3){int x=t[k+1];t[k+1]=t[k+2];t[k+2]=x;}mesh.SetTriangles(t,s);}
            mesh.RecalculateBounds();mesh.RecalculateTangents();EditorUtility.SetDirty(mesh);AssetDatabase.SaveAssets();
            return mesh;
        }

        // --- Textures ----------------------------------------------------------------------------------------------------
        // The footbed: pale aqua with a fine pebbled grain and the right foot's print worn into it (heel, ball, five toes);
        // u across x -0.05..0.05 m, v along y -0.135..0.135 m (the left one's mirror prints the left foot).
        static Texture2D FootbedTexture()
        {
            const int W=128,H=256;var tex=new Texture2D(W,H,TextureFormat.RGB24,false,false);
            var prints=new(Vector2 c,Vector2 r,float d)[]{(new Vector2(.000f,-.092f),new Vector2(.029f,.034f),.08f),(new Vector2(.004f,.042f),new Vector2(.039f,.025f),.07f),
                (new Vector2(.030f,.094f),new Vector2(.013f,.017f),.08f),(new Vector2(.007f,.103f),new Vector2(.008f,.010f),.06f),(new Vector2(-.010f,.098f),new Vector2(.007f,.009f),.06f),
                (new Vector2(-.024f,.088f),new Vector2(.0065f,.008f),.06f),(new Vector2(-.035f,.074f),new Vector2(.006f,.007f),.06f)};
            for(int y=0;y<H;y++)for(int x=0;x<W;x++)
            {
                float u=(x+.5f)/W,v=(y+.5f)/H;var p=new Vector2(Mathf.Lerp(-.05f,.05f,u),Mathf.Lerp(-.135f,.135f,v));
                var c=Color.Lerp(new Color(.44f,.56f,.53f),new Color(.50f,.62f,.59f),Fbm(u,v,16,40,3,71));
                float wear=0;
                foreach(var (centre,radius,depth) in prints)
                {
                    float d=new Vector2((p.x-centre.x)/radius.x,(p.y-centre.y)/radius.y).magnitude;wear=Mathf.Max(wear,depth*(1-Mathf.SmoothStep(.55f,1.05f,d)));
                }
                tex.SetPixel(x,y,c*(1-wear));
            }
            return SaveTexture(FootbedPath,tex,false,TextureWrapMode.Clamp);
        }
        // The blade: aqua, mottled, paler toward the thin tip, a moulded groove each side halfway to the rail with a light lip
        // beside it. u, v are (x, y) + 0.25 m at 2 per metre (see SwimFin).
        static Texture2D BladeTexture()
        {
            const int S=512;var tex=new Texture2D(S,S,TextureFormat.RGB24,false,false);
            for(int y=0;y<S;y++)for(int x=0;x<S;x++)
            {
                float u=(x+.5f)/S,v=(y+.5f)/S,bx=u/2-.25f,by=v/2-.25f;
                var c=Color.Lerp(new Color(.22f,.50f,.49f),new Color(.28f,.57f,.55f),Fbm(u,v,10,10,3,61));
                c=Color.Lerp(c,new Color(.33f,.60f,.58f),Mathf.Clamp01((by-.04f)/.20f)*.55f);
                float along=Mathf.Clamp01((by+.005f)/.025f)*Mathf.Clamp01((.205f-by)/.025f),line=BladeHalf(by)*.56f;
                c=Color.Lerp(c,c*.68f,along*Mathf.Clamp01(1-Mathf.Abs(Mathf.Abs(bx)-line)/.0018f));
                c=Color.Lerp(c,c*1.12f,along*Mathf.Clamp01(1-Mathf.Abs(Mathf.Abs(bx)-line-.0028f)/.0010f));
                tex.SetPixel(x,y,c);
            }
            return SaveTexture(BladePath,tex,false,TextureWrapMode.Clamp);
        }
    }
}
