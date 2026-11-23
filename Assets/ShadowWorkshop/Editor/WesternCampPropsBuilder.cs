using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static ShadowWorkshopIntegration.Editor.PropMesh;

namespace ShadowWorkshopIntegration.Editor
{
    // Western camp props for the WildWest level (2026-11-16), built like WildWestBuilder's: real size, each in its own
    // frame (metres, +y up, -z the front), smooth normals doing the rounding, the detail in small textures drawn here, a
    // mobile VR budget. This only builds meshes, textures and materials; placing them in the level is WildWestBuilder's job.
    // - Hatchet: 300 mm, after the user's picture: a dark hand-forged bearded head (a tall eye block, the blade's top line
    //   straight out to the toe, the long curved edge sweeping down and back into a beard) with the polished bevel along
    //   the edge, on a curved hickory handle with a lanyard hole near the butt; no maker's marks. Standing on the butt,
    //   the bit to +x, a cheek to the front.
    // - Shoe brush: a horsehair shoe-shine brush standing on its bristles: an oval stained-wood back 140 x 55 x 20 mm and
    //   the golden-yellow bristles a rounded block 25 mm deep under it (hair stripes and tufts in the texture, no hairs).
    // - Bean can: a tin can 74 x 110 mm, standing, opened: rolled seams top and bottom, ribbed silver bands above and
    //   below the label (the ribs in a normal map), a teal paper label with a simple drawing of three beans front and
    //   back (the user, 2026-11-17), and the lid cut away to show the baked beans in tomato sauce inside (the user: it
    //   has to show it is beans). No brand, text or logo on the label (the user, 2026-11-16: not copying anyone's
    //   packaging).
    // - Harmonica: a detailed look for WildWestBuilder's harmonica, in its frame and outline (80 x 21 x 15 mm along x, the
    //   holes in the front) so it can stand in for it: a wooden comb with ten channels open at the front, brass reed
    //   plates, curved chrome covers held by domed screws; no markings.
    // - Revolver, playing cards and dagger (the user, 2026-11-17): a single-action six-shooter, a fanned poker hand (the
    //   dead man's hand) and a Western dagger; see their sections.
    // Each casts a light closed proxy instead (WildWest's shadow budget, 2026-11-16: 60 triangles or fewer each); see
    // the Shadows section.
    public static class WesternCampPropsBuilder
    {
        const string Art="Assets/ShadowWorkshop/Art/Torch2/",Meshes=Art+"Meshes/",Textures=Art+"Textures/",MaterialFolder=Art+"Materials/";
        const string SteelPath=Textures+"hatchet steel.png",HandleWoodPath=Textures+"hatchet handle wood.png",WoodPath=Textures+"wood grain.png";
        const string BristlePath=Textures+"horsehair bristles.png",LabelPath=Textures+"bean can label.png",TinPath=Textures+"can tin.png",TinNormalPath=Textures+"can tin normal.png";
        const string BeansPath=Textures+"baked beans.png",BeansNormalPath=Textures+"baked beans normal.png";
        const string FlutesPath=Textures+"revolver cylinder.png",FlutesNormalPath=Textures+"revolver cylinder normal.png",CasePath=Textures+"revolver case colours.png";
        const string CardsPath=Textures+"playing cards.png",DaggerGripPath=Textures+"dagger grip leather.png";

        static Texture2D Load(string path)=>AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        // Shared with the other levels' props (same names and values, so they load the existing materials).
        static readonly Look KitchenSteel=new Look("kitchen steel",new Color(.86f,.87f,.89f),.75f,.86f);
        static readonly Look Dark=new Look("western dark",new Color(.05f,.04f,.035f),0f,.20f);
        static readonly Look Gold=new Look("western gold",new Color(.93f,.72f,.30f),.85f,.70f);
        // mesh: the drawn mesh; shadow: the light closed mesh that casts its shadow.
        public sealed class Prop{public string label,mesh,shadow;public System.Func<Mesh> build,buildShadow;public System.Func<Material[]> materials;}
        public static readonly Prop[] Props={
            new Prop{label="HATCHET",mesh=Meshes+"Hatchet.asset",shadow=Meshes+"Hatchet shadow.asset",build=Hatchet,buildShadow=HatchetShadow,materials=()=>PropMesh.Materials(MaterialFolder,new[]{
                new Look("hatchet steel",Color.white,.45f,.35f){baseMap=()=>Load(SteelPath)??HatchetSteel()},KitchenSteel,
                new Look("hatchet handle wood",Color.white,0f,.40f){baseMap=()=>Load(HandleWoodPath)??HandleWood()}})},
            new Prop{label="SHOE BRUSH",mesh=Meshes+"Shoe brush.asset",shadow=Meshes+"Shoe brush shadow.asset",build=ShoeBrush,buildShadow=BrushShadow,materials=()=>PropMesh.Materials(MaterialFolder,new[]{
                new Look("shoe brush wood",new Color(.74f,.56f,.44f),0f,.45f){baseMap=()=>Load(WoodPath)},
                new Look("horsehair bristles",Color.white,0f,.28f){baseMap=()=>Load(BristlePath)??Bristles()}})},
            new Prop{label="BEAN CAN",mesh=Meshes+"Bean can.asset",shadow=Meshes+"Bean can shadow.asset",build=BeanCan,buildShadow=BeanCanShadow,materials=()=>PropMesh.Materials(MaterialFolder,new[]{
                new Look("can tin",Color.white,.85f,.72f){baseMap=()=>Load(TinPath)??TinTextures(),normalMap=()=>{if(!Load(TinNormalPath))TinTextures();return Load(TinNormalPath);},bumpScale=1f},
                new Look("bean can label",Color.white,0f,.30f){baseMap=()=>Load(LabelPath)??Label()},
                new Look("baked beans",Color.white,0f,.62f){baseMap=()=>Load(BeansPath)??BeansTextures(),normalMap=()=>{if(!Load(BeansNormalPath))BeansTextures();return Load(BeansNormalPath);},bumpScale=1f}})},
            new Prop{label="HARMONICA",mesh=Meshes+"Harmonica detailed.asset",shadow=Meshes+"Harmonica detailed shadow.asset",build=Harmonica,buildShadow=HarmonicaShadow,materials=()=>PropMesh.Materials(MaterialFolder,new[]{
                new Look("harmonica chrome",new Color(.86f,.87f,.89f),.90f,.85f),new Look("harmonica comb",new Color(.55f,.37f,.21f),0f,.35f),Gold,Dark})},
            new Prop{label="REVOLVER",mesh=Meshes+"Revolver.asset",shadow=Meshes+"Revolver shadow.asset",build=Revolver,buildShadow=RevolverShadow,materials=()=>PropMesh.Materials(MaterialFolder,new[]{
                new Look("revolver blued",Color.white,.80f,.62f){baseMap=()=>Load(FlutesPath)??RevolverFlutes(),normalMap=()=>{if(!Load(FlutesNormalPath))RevolverFlutes();return Load(FlutesNormalPath);},bumpScale=1f},
                new Look("revolver case colours",Color.white,.65f,.55f){baseMap=()=>Load(CasePath)??CaseColours()},
                new Look("revolver grip walnut",new Color(.62f,.46f,.36f),0f,.50f){baseMap=()=>Load(WoodPath)},Gold,Dark})},
            new Prop{label="PLAYING CARDS",mesh=Meshes+"Playing cards.asset",shadow=Meshes+"Playing cards shadow.asset",build=PlayingCards,buildShadow=CardsShadow,materials=()=>PropMesh.Materials(MaterialFolder,new[]{
                new Look("playing cards",Color.white,0f,.35f){baseMap=()=>Load(CardsPath)??CardAtlas()}})},
            new Prop{label="DAGGER",mesh=Meshes+"Dagger.asset",shadow=Meshes+"Dagger shadow.asset",build=Dagger,buildShadow=DaggerShadow,materials=()=>PropMesh.Materials(MaterialFolder,new[]{
                KitchenSteel,Gold,new Look("dagger grip leather",Color.white,0f,.30f){baseMap=()=>Load(DaggerGripPath)??DaggerGrip()}})}};
        public static Mesh MeshOf(Prop p)=>AssetDatabase.LoadAssetAtPath<Mesh>(p.mesh);
        public static Mesh ShadowOf(Prop p)=>AssetDatabase.LoadAssetAtPath<Mesh>(p.shadow);

        // The textures are rewritten in place (same GUIDs) on every build, so editing them here takes effect; the
        // materials are only created when missing.
        [MenuItem("MetaHackthone/Shadow Workshop/Build Western camp props")]
        public static void Build()
        {
            HatchetSteel();HandleWood();Bristles();Label();TinTextures();BeansTextures();RevolverFlutes();CaseColours();CardAtlas();DaggerGrip();
            var built=Props.Select(p=>
            {
                var m=p.build();var s=p.buildShadow();p.materials();var size=m.bounds.size*1000;
                return $"{p.label} {m.triangles.Length/3} tris {size.x:0} x {size.y:0} x {size.z:0} mm (shadow {s.triangles.Length/3})";
            }).ToArray();
            Debug.Log("WESTERN_CAMP_PROPS_OK "+string.Join("; ",built));
        }

        // --- Hatchet -----------------------------------------------------------------------------------------------------
        // The head is a loft of rounded-rectangle sections from the poll (-x) to the edge (+x): three across the eye block,
        // then the blade's. Each station is a line in (x, y from the eye's centre) from a point on the bottom line to one
        // on the top line, with a half thickness (z) and how far its middle stands out sideways (bulge, which curves the
        // edge). The blade's run from the eye block's front to the edge (Heel to Toe): the top line straight, the bottom
        // line (the beard) sagging up a little in the middle. From BevelFrom on it is the polished bevel.
        const float EyeY=.268f,SteelPerMetre=10,EdgeBulge=.006f,BeardSag=.008f;
        const int BevelFrom=8,HandleSides=10,CheekSteps=6;
        static readonly Vector2 EyeBottom=new Vector2(.0175f,-.0260f),EyeTop=new Vector2(.0175f,.0240f),Heel=new Vector2(.0740f,-.0770f),Toe=new Vector2(.0960f,.0360f);
        static readonly (Vector2 b,Vector2 t,float half,float bulge)[] HeadStations=Stations();
        static (Vector2,Vector2,float,float)[] Stations()
        {
            var list=new List<(Vector2,Vector2,float,float)>{
                (new Vector2(-.0180f,-.0240f),new Vector2(-.0180f,.0280f),.0100f,0),(new Vector2(-.0165f,-.0260f),new Vector2(-.0165f,.0300f),.0125f,0),
                (new Vector2(.0165f,-.0260f),new Vector2(.0165f,.0300f),.0125f,0)};
            foreach(var (f,half) in new[]{(.06f,.0105f),(.22f,.0085f),(.42f,.0065f),(.62f,.0048f),(.80f,.0034f),(.90f,.0026f),(.96f,.0013f),(1f,.0005f)})
                list.Add((Vector2.Lerp(EyeBottom,Heel,f)+new Vector2(0,BeardSag*Mathf.Sin(Mathf.PI*f)),Vector2.Lerp(EyeTop,Toe,f),half,EdgeBulge*f*f));
            return list.ToArray();
        }
        // The handle's centre line from the butt up through the eye (bending back under the poll towards the butt, as in
        // the picture), and its oval section (across x, through z) by height.
        static readonly Vector3[] HandlePath={new Vector3(-.0300f,.004f,0),new Vector3(-.0268f,.020f,0),new Vector3(-.0190f,.055f,0),new Vector3(-.0100f,.100f,0),
            new Vector3(-.0040f,.150f,0),new Vector3(-.0010f,.200f,0),new Vector3(0,.240f,0),new Vector3(0,EyeY+.031f,0)};
        static readonly (float y,float rx,float rz)[] HandleWidth={(.004f,.0126f,.0086f),(.022f,.0128f,.0088f),(.060f,.0118f,.0081f),(.150f,.0110f,.0076f),
            (.240f,.0116f,.0080f),(EyeY+.031f,.0118f,.0082f)};
        const float HoleFromButt=.016f;   // the lanyard hole, along the handle (drawn in its texture)

        // A head section in (along the station's line, z): up the front cheek in CheekSteps (so the edge can curve), across
        // the top, down the back cheek and across the bottom; the corners rounded by the normals. 2 (CheekSteps + 3) points.
        static Vector2[] HeadSection(int s)
        {
            var st=HeadStations[s];float len=Vector2.Distance(st.b,st.t),h=st.half,r=Mathf.Min(.003f,h*.9f);var list=new List<Vector2>();
            for(int i=0;i<=CheekSteps;i++)list.Add(new Vector2(Mathf.Lerp(r,len-r,(float)i/CheekSteps),-h));
            list.Add(new Vector2(len,-h+r));list.Add(new Vector2(len,h-r));
            for(int i=CheekSteps;i>=0;i--)list.Add(new Vector2(Mathf.Lerp(r,len-r,(float)i/CheekSteps),h));
            list.Add(new Vector2(0,h-r));list.Add(new Vector2(0,-h+r));
            return list.ToArray();
        }
        // Its points paired across the thickness, top to bottom, to close the ends.
        static (int a,int b)[] EndPairs()
        {
            int n=CheekSteps+1;var pairs=new List<(int,int)>{(n,n+1)};
            for(int i=n-1;i>=0;i--)pairs.Add((i,2*n+1-i));
            pairs.Add((2*n+3,2*n+2));return pairs.ToArray();
        }
        static Vector3 HeadPoint(int s,Vector2 az)
        {
            var st=HeadStations[s];var d=st.t-st.b;float len=d.magnitude;d/=len;var n=new Vector2(d.y,-d.x);float v=Mathf.Clamp01(az.x/len);
            var p=st.b+d*az.x+n*st.bulge*(1-(2*v-1)*(2*v-1));
            return new Vector3(p.x,EyeY+p.y,az.y);
        }
        static Mesh Hatchet()
        {
            const int Steel=0,Edge=1,Wood=2;var b=new PropMesh(3);
            int count=HeadStations.Length;var sections=new Vector2[count][];
            for(int s=0;s<count;s++)sections[s]=HeadSection(s);
            int around=sections[0].Length;var p=new Vector3[count,around];var grid=new int[count,around];
            for(int s=0;s<count;s++)for(int k=0;k<around;k++)p[s,k]=HeadPoint(s,sections[s][k]);
            // Normals from the grid itself (scaled up first: the differences are millimetres), turned away from the section's middle.
            for(int s=0;s<count;s++)for(int k=0;k<around;k++)
            {
                var along=p[Mathf.Min(s+1,count-1),k]-p[Mathf.Max(s-1,0),k];var round=p[s,(k+1)%around]-p[s,(k+around-1)%around];
                var n=Vector3.Cross(round*1000,along*1000).normalized;var st=HeadStations[s];var d=(st.t-st.b).normalized;var az=sections[s][k];
                float off=az.x-Vector2.Distance(st.b,st.t)/2;
                if(Vector3.Dot(n,new Vector3(d.x*off,d.y*off,az.y))<0)n=-n;
                grid[s,k]=b.Add(p[s,k],n,new Vector2(p[s,k].x,p[s,k].y)*SteelPerMetre);
            }
            for(int s=0;s+1<count;s++)for(int k=0;k<around;k++)
                b.Quad(s>=BevelFrom?Edge:Steel,grid[s,k],grid[s+1,k],grid[s+1,(k+1)%around],grid[s,(k+1)%around]);
            // The flat poll, and the thin strip along the edge facing out of its curve.
            HeadEnd(b,Steel,p,0,q=>Vector3.left);
            var e=HeadStations[count-1];var ed=e.t-e.b;float elen=ed.magnitude;ed/=elen;var en=new Vector2(ed.y,-ed.x);
            HeadEnd(b,Edge,p,count-1,q=>
            {
                float v=Mathf.Clamp01(Vector2.Dot(new Vector2(q.x,q.y-EyeY)-e.b,ed)/elen),slope=-4*(2*v-1)*e.bulge/elen;var nn=en-ed*slope;
                return new Vector3(nn.x,nn.y,0).normalized;
            });
            // The handle: a short rounded butt, sawn flat at the top just proud of the eye.
            var path=Curve(HandlePath,2);
            b.Bar(Wood,path,Vector3.right,HandleSection,.002f,0,2);
            int ring=b.v.Count-(HandleSides+1);var top=path[path.Count-1];int hub=b.Add(top,Vector3.up,new Vector2(.995f,.5f));
            var rim=new int[HandleSides];for(int k=0;k<HandleSides;k++)rim[k]=b.Add(b.v[ring+k],Vector3.up,new Vector2(.995f,.5f));
            for(int k=0;k<HandleSides;k++)b.Tri(Wood,hub,rim[k],rim[(k+1)%HandleSides]);
            return b.Save(Props[0].mesh,"Hatchet");
        }
        // Closes the head at station s with quads across between the paired section points, normals from normal(point).
        static void HeadEnd(PropMesh b,int m,Vector3[,] p,int s,System.Func<Vector3,Vector3> normal)
        {
            int prevA=-1,prevB=-1;
            foreach(var (i,j) in EndPairs())
            {
                int a=b.Add(p[s,i],normal(p[s,i]),new Vector2(p[s,i].x,p[s,i].y)*SteelPerMetre),c=b.Add(p[s,j],normal(p[s,j]),new Vector2(p[s,j].x,p[s,j].y)*SteelPerMetre);
                if(prevA>=0)b.Quad(m,prevA,a,c,prevB);
                prevA=a;prevB=c;
            }
        }
        static (Vector2[] at,Vector2[] normal) HandleSection(float t)
        {
            var (rx,rz)=HandleRadii(Mathf.Lerp(HandlePath[0].y,HandlePath[HandlePath.Length-1].y,t));
            return Ellipse(rx,rz,HandleSides);
        }
        static (float rx,float rz) HandleRadii(float y)
        {
            int i=0;while(i+2<HandleWidth.Length&&y>HandleWidth[i+1].y)i++;
            var a=HandleWidth[i];var c=HandleWidth[i+1];float f=Mathf.Clamp01((y-a.y)/(c.y-a.y));f=f*f*(3-2*f);
            return (Mathf.Lerp(a.rx,c.rx,f),Mathf.Lerp(a.rz,c.rz,f));
        }
        // The handle's centre line (as the look draws it) at height y.
        static float HandleXAt(List<Vector3> path,float y)
        {
            for(int i=1;i<path.Count;i++)if(path[i].y>=y){float f=Mathf.Clamp01((y-path[i-1].y)/(path[i].y-path[i-1].y));return Mathf.Lerp(path[i-1].x,path[i].x,f);}
            return path[path.Count-1].x;
        }
        static float HandleLength(){var path=Curve(HandlePath,2);float total=0;for(int i=1;i<path.Count;i++)total+=Vector3.Distance(path[i-1],path[i]);return total;}

        // --- Shoe brush ----------------------------------------------------------------------------------------------------
        // Surfaces of revolution round y on a unit radius (heights in metres), then squeezed to an oval: a superellipse
        // BackA x BackB for the back and TuftA x TuftB for the bristle block (Squeeze, normals with it).
        const float BackA=.070f,BackB=.0275f,TuftA=.0640f,TuftB=.0228f,BrushPower=2.6f,TuftTop=.0262f;
        const int BrushSides=24,TuftRepeats=10,TuftsPerTile=5;
        static Mesh ShoeBrush()
        {
            const int Wood=0,Hair=1;var b=new PropMesh(2);
            // The back: flat underneath, a rounded edge, a slight crown; the wood grain along its length.
            int start=b.v.Count;
            b.Revolve(Wood,BrushSides,new[]{new Vector2(0,.0250f),new Vector2(.93f,.0250f),new Vector2(.93f,.0250f),new Vector2(1,.0288f),new Vector2(1,.0372f),
                new Vector2(.955f,.0424f),new Vector2(.78f,.0446f),new Vector2(0,.0452f)});
            Squeeze(b,start,BackA,BackB);
            for(int i=start;i<b.v.Count;i++){var q=b.v[i];b.uv[i]=new Vector2(q.x*4,(q.z+q.y)*26);}
            // The bristles: a flat trimmed bottom rolling round into straight sides, the top hidden in the back. The
            // bottom's rings are doubled (same normal) so it can take its own planar mapping.
            var tuft=new[]{new Vector2(0,0),new Vector2(.90f,0),new Vector2(.90f,0),new Vector2(.975f,.0015f),new Vector2(1,.0060f),new Vector2(1,.0160f),
                new Vector2(.995f,TuftTop),new Vector2(.995f,TuftTop),new Vector2(0,TuftTop)};
            Vector2 Seg(int i)=>new Vector2(tuft[i+1].y-tuft[i].y,-(tuft[i+1].x-tuft[i].x)).normalized;
            var normals=new[]{Vector2.down,Vector2.down,Vector2.down,(Seg(2)+Seg(3)).normalized,(Seg(3)+Seg(4)).normalized,(Seg(4)+Seg(5)).normalized,Seg(5),Vector2.up,Vector2.up};
            start=b.v.Count;b.Revolve(Hair,BrushSides,tuft,normals);Squeeze(b,start,TuftA,TuftB);
            int ringSize=BrushSides+1;
            for(int r=0;r<tuft.Length;r++)
            {
                int first=start+r*ringSize;
                if(r<=1)for(int k=0;k<ringSize;k++){var q=b.v[first+k];b.uv[first+k]=new Vector2(.5f+q.x/TuftA*.48f,.75f+q.z/TuftB*.23f);}
                else if(r<=6)
                {
                    // Round the sides by arc length (the squeeze bunches the angle steps at the ends), a whole number of tiles.
                    var along=new float[ringSize];for(int k=1;k<ringSize;k++)along[k]=along[k-1]+Vector3.Distance(b.v[first+k-1],b.v[first+k]);
                    for(int k=0;k<ringSize;k++)b.uv[first+k]=new Vector2(along[k]/along[ringSize-1]*TuftRepeats,.02f+.46f*b.v[first+k].y/TuftTop);
                }
                else for(int k=0;k<ringSize;k++)b.uv[first+k]=new Vector2(.5f,.25f);
            }
            return b.Save(Props[1].mesh,"Shoe brush");
        }
        // From the unit circle to the superellipse a x c (the normals by the inverse scale).
        static void Squeeze(PropMesh b,int start,float a,float c)
        {
            for(int i=start;i<b.v.Count;i++)
            {
                var p=b.v[i];float r=new Vector2(p.x,p.z).magnitude;
                if(r>1e-6f)
                {
                    float cs=Mathf.Abs(p.x/r),sn=Mathf.Abs(p.z/r),k=Mathf.Pow(Mathf.Pow(cs,BrushPower)+Mathf.Pow(sn,BrushPower),-1/BrushPower);
                    p=new Vector3(p.x*k*a,p.y,p.z*k*c);
                }
                b.v[i]=p;var n=b.n[i];b.n[i]=new Vector3(n.x/a,n.y,n.z/c).normalized;
            }
        }

        // --- Bean can ------------------------------------------------------------------------------------------------------
        // Profiles (radius, height) round y. Bottom: the recessed end panel, the rolled seam, the ribbed band up to the
        // label. Top: the ribbed band from the label, the seam, and down its inside to the beans (the lid cut away). A
        // repeated point is a crease.
        const float CanBody=.0360f,LabelY0=.0115f,LabelY1=.0985f,BeanSpan=.072f;const int CanSides=24;
        static readonly Vector2[] CanBottom={new Vector2(0,.0028f),new Vector2(.0300f,.0028f),new Vector2(.0300f,.0028f),new Vector2(.0350f,0),new Vector2(.0371f,.0011f),
            new Vector2(.0373f,.0035f),new Vector2(CanBody,.0056f),new Vector2(CanBody,.0056f),new Vector2(CanBody,LabelY0)};
        static readonly Vector2[] CanTop={new Vector2(CanBody,LabelY1),new Vector2(CanBody,.1040f),new Vector2(CanBody,.1040f),new Vector2(.0371f,.1060f),new Vector2(.0373f,.1086f),
            new Vector2(.0363f,.1100f),new Vector2(.0350f,.1095f),new Vector2(.0346f,.1070f),new Vector2(.0344f,.1028f)};
        // Distance along a profile to each point; the tin's v runs along the bottom profile and on along the top one.
        static float[] Arcs(Vector2[] profile,float from)
        {
            var arc=new float[profile.Length];arc[0]=from;
            for(int i=1;i<profile.Length;i++)arc[i]=arc[i-1]+Vector2.Distance(profile[i-1],profile[i]);
            return arc;
        }
        static float[] BottomArcs=>Arcs(CanBottom,0);
        static float[] TopArcs=>Arcs(CanTop,BottomArcs[CanBottom.Length-1]);
        static float TinLength=>TopArcs[CanTop.Length-1];
        static Mesh BeanCan()
        {
            const int Tin=0,Paper=1,Beans=2;var b=new PropMesh(3);
            void Tinned(Vector2[] profile,float[] arcs)
            {
                int start=b.v.Count;b.Revolve(Tin,CanSides,profile);
                for(int r=0;r<profile.Length;r++)for(int k=0;k<=CanSides;k++){int i=start+r*(CanSides+1)+k;b.uv[i]=new Vector2(b.uv[i].x,arcs[r]/TinLength);}
            }
            Tinned(CanBottom,BottomArcs);
            int label=b.v.Count;b.Revolve(Paper,CanSides,new[]{new Vector2(CanBody,LabelY0),new Vector2(CanBody,LabelY1)});
            for(int i=label;i<b.v.Count;i++)b.uv[i]=new Vector2(b.uv[i].x,(b.v[i].y-LabelY0)/(LabelY1-LabelY0));
            Tinned(CanTop,TopArcs);
            // The beans: a low heap filling the can to 6 mm under the rim, mapped from above.
            int beans=b.v.Count;b.Revolve(Beans,CanSides,new[]{new Vector2(.0344f,.1028f),new Vector2(.0300f,.1035f),new Vector2(.0170f,.1044f),new Vector2(0,.1047f)});
            for(int i=beans;i<b.v.Count;i++)b.uv[i]=new Vector2(.5f+b.v[i].x/BeanSpan,.5f+b.v[i].z/BeanSpan);
            return b.Save(Props[2].mesh,"Bean can");
        }

        // --- Harmonica -----------------------------------------------------------------------------------------------------
        // In WildWestBuilder's harmonica's frame (along x, y up through the covers, the holes in the front, -z).
        const float CombY=.0034f,PlateY=.0046f,CoverY=.0105f,HarpX=.040f,HarpZ=.0076f;
        static Mesh Harmonica()
        {
            const int Chrome=0,Wood=1,Brass=2,Hole=3;var b=new PropMesh(4);
            // The comb: a block behind the channels, eleven tines between ten channels, and each channel's dark back.
            const float Front=-.0071f,Back=.0068f,ChannelBack=-.0036f,Pitch=.0072f,Channel=.0048f,End=.0385f;
            b.BevelBox(Wood,new Vector3(-End,-CombY,ChannelBack),new Vector3(End,CombY,Back),.0004f);
            for(int k=0;k<=10;k++)
            {
                float x0=k==0?-End:(k-5.5f)*Pitch+Channel/2,x1=k==10?End:(k-4.5f)*Pitch-Channel/2;
                b.Box(Wood,new Vector3(x0,-CombY,Front),new Vector3(x1,CombY,ChannelBack+.0002f));
            }
            for(int j=0;j<10;j++)
            {
                float c=(j-4.5f)*Pitch,z=ChannelBack-.0001f;var n=Vector3.back;
                b.Quad(Hole,b.Add(new Vector3(c-Channel/2,-CombY,z),n,Vector2.zero),b.Add(new Vector3(c+Channel/2,-CombY,z),n,Vector2.zero),
                    b.Add(new Vector3(c+Channel/2,CombY,z),n,Vector2.zero),b.Add(new Vector3(c-Channel/2,CombY,z),n,Vector2.zero));
            }
            // The brass reed plates either side of the comb.
            foreach(var s in new[]{1f,-1f})b.BevelBox(Brass,new Vector3(-.0395f,s>0?CombY:-PlateY,-.0073f),new Vector3(.0395f,s>0?PlateY:-CombY,.0070f),.0003f);
            // The covers: a domed section (z, y over the plate; repeated points are creases) carried along x, rounded at the
            // ends; the bottom one mirrored. Bar's frame here: side -z, up +y.
            var zy=new[]{new Vector2(-HarpZ,PlateY),new Vector2(-HarpZ,PlateY),new Vector2(-HarpZ,.0072f),new Vector2(-.0070f,.0092f),new Vector2(-.0052f,.0102f),new Vector2(0,CoverY),
                new Vector2(.0052f,.0102f),new Vector2(.0070f,.0092f),new Vector2(HarpZ,.0072f),new Vector2(HarpZ,PlateY),new Vector2(HarpZ,PlateY)};
            var nzy=new[]{new Vector2(0,-1),new Vector2(-1,0),new Vector2(-1,0),new Vector2(-.80f,.60f),new Vector2(-.35f,.94f),new Vector2(0,1),
                new Vector2(.35f,.94f),new Vector2(.80f,.60f),new Vector2(1,0),new Vector2(1,0),new Vector2(0,-1)};
            const float Yc=(PlateY+CoverY)/2,Cap=.0014f;
            foreach(var s in new[]{1f,-1f})
            {
                var at=zy.Select(q=>new Vector2(-q.x,s*(q.y-Yc))).ToArray();var normal=nzy.Select(q=>new Vector2(-q.x,s*q.y).normalized).ToArray();
                b.Bar(Chrome,new[]{new Vector3(-HarpX+Cap,s*Yc,0),new Vector3(HarpX-Cap,s*Yc,0)},Vector3.back,t=>(at,normal),Cap,Cap,2);
                foreach(var x in new[]{-.0352f,.0352f})
                    b.SmoothLathe(Chrome,new Vector3(x,s*(CoverY-.0003f),0),Vector3.up*s,6,new[]{new Vector2(.0017f,0),new Vector2(.0013f,.0007f),new Vector2(0,.0010f)});
            }
            return b.Save(Props[3].mesh,"Harmonica detailed");
        }

        // --- Revolver ------------------------------------------------------------------------------------------------------
        // A single-action six-shooter of the 1870s pattern, no maker's marks: the muzzle to +x, the bore on y = 0, its right
        // side to the front (-z); about 244 x 141 x 41 mm. A blued 140 mm barrel with its ejector rod housing, blade sight
        // and base pin; a blued cylinder of six (its flutes in a texture and normal map, the chamber mouths dark at its
        // front); a colour-case-hardened frame, top strap, hammer and trigger; a brass trigger guard; walnut grips with a
        // screw each side. Frame, hammer, trigger and grips are slabs of their side outlines (tables in mm).
        const float CylX0=-.038f,CylX1=-.0005f,CylY=-.0135f,CylR=.0205f;
        static Vector2[] Mm(params (float x,float y)[] points)=>points.Select(p=>new Vector2(p.x,p.y)/1000).ToArray();
        static readonly Vector2[] FrameOutline=Mm((14,8),(13.5f,-8),(11,-30),(8,-40),(4,-41.5f),(-40,-41.5f),(-45,-44),(-50,-60),(-57,-82),(-63,-100),(-66,-107),
            (-73,-111),(-85,-112.5f),(-97,-110),(-103,-104),(-103.5f,-92),(-99,-72),(-90,-48),(-79,-26),(-68,-8),(-61,3),(-54,8),(-45,9),(-39,8),(-39,-33),(-37,-36),
            (-2,-36),(0,-34),(0,8));
        static readonly Vector2[] HammerOutline=Mm((-52,4),(-42,6),(-41,12),(-44,19),(-50,24),(-57,27.5f),(-63,28),(-65,26),(-61,23),(-56,20),(-54,12));
        static readonly Vector2[] TriggerOutline=Mm((-25,-40.5f),(-20,-40.5f),(-20.5f,-46),(-23,-52),(-27.5f,-57),(-30.5f,-58),(-30,-55.5f),(-26.5f,-51),(-24.5f,-46));
        static readonly Vector2[] GripOutline=Mm((-68,-15),(-48.5f,-46),(-53.5f,-61),(-60,-82),(-65.5f,-99),(-68.5f,-105),(-75,-108.5f),(-85,-109.8f),(-95,-107.5f),
            (-100.5f,-101),(-100.5f,-91),(-96.5f,-72),(-87.5f,-49),(-77,-28));
        static readonly Vector2[] GuardPath=Mm((-6,-38),(-7.5f,-48),(-13,-56),(-23,-60.5f),(-35,-58.5f),(-43.5f,-52),(-47.5f,-45));
        static Mesh Revolver()
        {
            const int Blued=0,Case=1,Wood=2,Brass=3,Hole=4;var b=new PropMesh(5);
            // The blued parts other than the cylinder take a plain spot of its texture.
            void Plain(int from){for(int i=from;i<b.v.Count;i++)b.uv[i]=new Vector2(.5f,.05f);}
            int start=b.v.Count;
            b.SmoothLathe(Blued,Vector3.zero,Vector3.right,14,new[]{new Vector2(.0075f,.0010f),new Vector2(.0068f,.1385f),new Vector2(.0062f,.1400f),new Vector2(.0030f,.1400f)},2);
            b.BevelBox(Blued,new Vector3(.1290f,.0055f,-.0010f),new Vector3(.1365f,.0105f,.0010f),.0004f);   // front sight
            b.Bar(Blued,new[]{new Vector3(.0120f,-.0098f,-.0032f),new Vector3(.0970f,-.0098f,-.0032f)},Vector3.forward,t=>Ellipse(.0037f,.0037f,8),0,.0035f,2);   // ejector housing
            b.SmoothLathe(Blued,new Vector3(.0130f,CylY,0),Vector3.right,8,new[]{new Vector2(.0030f,0),new Vector2(.0026f,.0018f),new Vector2(0,.0022f)});   // base pin head
            Plain(start);
            b.SmoothLathe(Hole,Vector3.zero,Vector3.right,12,new[]{new Vector2(.0030f,.1400f),new Vector2(.0027f,.1370f),new Vector2(0,.1370f)});   // the bore
            // Cylinder: a plain lathe, its texture six flutes round (u) and along it (v); the chamber mouths at the front,
            // the top one in line with the bore.
            start=b.v.Count;float len=CylX1-CylX0;
            b.SmoothLathe(Blued,new Vector3(CylX0,CylY,0),Vector3.right,24,new[]{new Vector2(0,0),new Vector2(CylR-.0012f,0),new Vector2(CylR,.0012f),
                new Vector2(CylR,len-.0012f),new Vector2(CylR-.0012f,len),new Vector2(0,len)},1,4);
            for(int i=start;i<b.v.Count;i++)b.uv[i]=new Vector2(b.uv[i].x*6,(b.v[i].x-CylX0)/len);
            for(int k=0;k<6;k++)
            {
                float a=k*Mathf.PI/3;var c=new Vector3(CylX1+.0002f,CylY+.0135f*Mathf.Cos(a),.0135f*Mathf.Sin(a));int hub=b.Add(c,Vector3.right,Vector2.zero),ring=b.v.Count;
                for(int j=0;j<8;j++){float t=j*Mathf.PI/4;b.Add(c+new Vector3(0,Mathf.Cos(t),Mathf.Sin(t))*.0056f,Vector3.right,Vector2.zero);}
                for(int j=0;j<8;j++)b.Tri(Hole,hub,ring+j,ring+(j+1)%8);
            }
            // Frame (its window holds the cylinder), top strap, hammer, trigger and the grip screws, case-hardened.
            b.Slab(Case,i=>Case,FrameOutline,.0018f,p=>.0100f,p=>0,1,16);
            b.BevelBox(Case,new Vector3(-.0440f,.0070f,-.0052f),new Vector3(.0125f,.0118f,.0052f),.0015f);
            b.Slab(Case,i=>Case,HammerOutline,.0009f,p=>.0034f,p=>0,1,16);
            b.Slab(Case,i=>Case,TriggerOutline,.0008f,p=>.0025f,p=>0,1,16);
            foreach(var s in new[]{-1f,1f})b.SmoothLathe(Case,new Vector3(-.0790f,-.0640f,s*.0155f),new Vector3(0,0,s),6,new[]{new Vector2(.0032f,0),new Vector2(.0026f,.0007f),new Vector2(0,.0010f)});
            b.Bar(Brass,GuardPath.Select(p=>(Vector3)p).ToList(),Vector3.forward,t=>Ellipse(.0040f,.0015f,6),0,0);
            // Walnut grips, the grain along the grip.
            start=b.v.Count;b.Slab(Wood,i=>Wood,GripOutline,.0055f,p=>.0158f,p=>0,1,16);
            var along=new Vector2(-.33f,-.94f).normalized;var across=new Vector2(-along.y,along.x);
            for(int i=start;i<b.v.Count;i++){var p=b.v[i];var q=new Vector2(p.x,p.y);b.uv[i]=new Vector2(Vector2.Dot(q,along)*5,Vector2.Dot(q,across)*45+p.z*45);}
            return b.Save(Props[4].mesh,"Revolver");
        }

        // --- Playing cards -------------------------------------------------------------------------------------------------
        // A hand of five poker-size cards (63 x 88 mm) fanned in the x, y plane, 12 degrees apart round a point 14 mm above
        // their bottom edges, faces to the front (-z), the rightmost in front: the dead man's hand, black aces and eights
        // (Wild Bill Hickok's, 1876), and the nine of diamonds. Pips only, no rank letters or maker's art; plain red lattice
        // backs. Faces and back from one atlas (CardAtlas: 4 x 2 cells).
        const float CardW=.063f,CardH=.088f,CardT=.0003f,CardR=.003f;
        static (Quaternion turn,Vector3 centre,float z) CardPlace(int i)=>(Quaternion.Euler(0,0,(2-i)*12),new Vector3(0,CardH/2-.014f,0),(4-i)*.00045f);
        static Mesh PlayingCards()
        {
            var b=new PropMesh(1);
            var outline=RoundCorners(new List<(Vector2 p,float r,int steps)>{(new Vector2(-CardW/2,-CardH/2),CardR,2),(new Vector2(CardW/2,-CardH/2),CardR,2),
                (new Vector2(CardW/2,CardH/2),CardR,2),(new Vector2(-CardW/2,CardH/2),CardR,2)},true).ToArray();
            for(int i=0;i<5;i++){var (turn,centre,z)=CardPlace(i);Card(b,outline,turn,centre,z,i);}
            return b.Save(Props[5].mesh,"Playing cards");
        }
        // One card: its face (atlas cell face) to the front, the back (cell 5, mirrored) behind, a white edge between.
        static void Card(PropMesh b,Vector2[] outline,Quaternion turn,Vector3 centre,float z,int face)
        {
            Vector3 At(Vector2 p,float dz)=>turn*(centre+(Vector3)p)+new Vector3(0,0,z+dz);
            Vector2 Cell(int c,Vector2 p)=>new Vector2((c%4+.01f+.98f*(p.x/CardW+.5f))/4,(c/4+.005f+.99f*(p.y/CardH+.5f))/2);
            int n=outline.Length;
            foreach(var (side,cell) in new[]{(-1f,face),(1f,5)})
            {
                var normal=new Vector3(0,0,side);int hub=b.Add(At(Vector2.zero,side*CardT/2),normal,Cell(cell,Vector2.zero)),ring=b.v.Count;
                foreach(var p in outline)b.Add(At(p,side*CardT/2),normal,Cell(cell,side<0?p:new Vector2(-p.x,p.y)));
                for(int k=0;k<n;k++)b.Tri(0,hub,ring+k,ring+(k+1)%n);
            }
            var white=new Vector2(.625f,.75f);int edge=b.v.Count;
            for(int k=0;k<n;k++)
            {
                var e=outline[(k+1)%n]-outline[(k+n-1)%n];var o=turn*new Vector3(e.y,-e.x,0).normalized;
                b.Add(At(outline[k],-CardT/2),o,white);b.Add(At(outline[k],CardT/2),o,white);
            }
            for(int k=0;k<n;k++){int a=edge+2*k,c=edge+2*((k+1)%n);b.Quad(0,a,c,c+1,a+1);}
        }

        // --- Dagger --------------------------------------------------------------------------------------------------------
        // A Western dagger, 287 mm: a double-edged spear-point blade (a diamond section, its four facets flat, polished), a
        // brass cross guard with ball ends, a grip of stacked leather washers between brass spacers, and a brass pommel.
        // Lying along x, the point to +x, the edges up and down (y), the flats to the front and back (z).
        static readonly (float x,float w,float t)[] BladeStations={(.003f,.0150f,.0036f),(.020f,.0150f,.0034f),(.060f,.0138f,.0030f),(.100f,.0118f,.0024f),
            (.130f,.0094f,.0018f),(.150f,.0066f,.0013f),(.162f,.0036f,.0008f),(.170f,0,0)};
        static Mesh Dagger()
        {
            const int Steel=0,Brass=1,Leather=2;var b=new PropMesh(3);
            // The blade: four facets between the edges (+-y) and the ridges (+-z), each lofted from the guard to the point.
            Vector3 Corner(int k,(float x,float w,float t) s)=>k==0?new Vector3(s.x,s.w,0):k==1?new Vector3(s.x,0,-s.t):k==2?new Vector3(s.x,-s.w,0):new Vector3(s.x,0,s.t);
            int count=BladeStations.Length;
            for(int f=0;f<4;f++)
            {
                var outward=new Vector3(0,f==0||f==3?1:-1,f<2?-1:1);var last=outward;var rows=new int[count,2];
                for(int s=0;s<count;s++)
                {
                    var st=BladeStations[s];var a=Corner(f,st);var c=Corner((f+1)%4,st);var n=last;
                    if((c-a).sqrMagnitude>1e-10f)
                    {
                        var along=Corner(f,BladeStations[Mathf.Min(s+1,count-1)])-Corner(f,BladeStations[Mathf.Max(s-1,0)]);
                        n=Vector3.Cross((c-a)*1000,along*1000).normalized;if(Vector3.Dot(n,outward)<0)n=-n;last=n;
                    }
                    rows[s,0]=b.Add(a,n,Vector2.zero);rows[s,1]=b.Add(c,n,Vector2.zero);
                }
                for(int s=0;s+1<count;s++)b.Quad(Steel,rows[s,0],rows[s+1,0],rows[s+1,1],rows[s,1]);
            }
            // The guard: a block where the blade goes in, a bar across, a ball at each end.
            b.BevelBox(Brass,new Vector3(-.0045f,-.0165f,-.0062f),new Vector3(.0045f,.0165f,.0062f),.0015f);
            b.Bar(Brass,new[]{new Vector3(0,-.0300f,0),new Vector3(0,.0300f,0)},Vector3.right,t=>Ellipse(.0040f,.0056f,10),0,0);
            foreach(var s in new[]{-1f,1f})
                b.SmoothLathe(Brass,new Vector3(0,s*.0345f,0),Vector3.up,10,new[]{new Vector2(0,-.0062f),new Vector2(.0044f,-.0044f),new Vector2(.0062f,0),new Vector2(.0044f,.0044f),new Vector2(0,.0062f)});
            // The grip, swelling a little in the middle (u along it for the washers), and the pommel.
            b.Bar(Leather,new[]{new Vector3(-.0040f,0,0),new Vector3(-.0200f,0,0),new Vector3(-.0450f,0,0),new Vector3(-.0700f,0,0),new Vector3(-.0880f,0,0),new Vector3(-.0985f,0,0)},
                Vector3.up,t=>{float bulge=Mathf.Sin(Mathf.PI*t);return Ellipse(.0104f+.0016f*bulge,.0087f+.0013f*bulge,12);},0,0);
            b.SmoothLathe(Brass,new Vector3(-.0975f,0,0),Vector3.left,16,new[]{new Vector2(.0108f,0),new Vector2(.0120f,.0030f),new Vector2(.0124f,.0080f),new Vector2(.0105f,.0140f),
                new Vector2(.0060f,.0178f),new Vector2(0,.0188f)});
            return b.Save(Props[6].mesh,"Dagger");
        }

        // --- Shadows -------------------------------------------------------------------------------------------------------
        // Light closed meshes that cast the props' shadows (WildWest's shadow budget, 2026-11-16: 60 triangles or fewer
        // each): vertices shared, every triangle wound outward, outlines within a few millimetres of the looks'.
        sealed class Hull
        {
            public readonly List<Vector3> v=new List<Vector3>();readonly List<int> t=new List<int>();
            public int Add(Vector3 p){v.Add(p);return v.Count-1;}
            // Wound to face outward (Unity: clockwise seen from outside).
            public void Tri(int a,int b,int c,Vector3 outward)
            {
                if(Vector3.Dot(Vector3.Cross(v[b]-v[a],v[c]-v[a]),outward)<0){var s=b;b=c;c=s;}
                t.Add(a);t.Add(b);t.Add(c);
            }
            public void Quad(int a,int b,int c,int d,Vector3 outward){Tri(a,b,c,outward);Tri(a,c,d,outward);}
            Vector3 Centre(int[] ring){var c=Vector3.zero;foreach(var i in ring)c+=v[i];return c/ring.Length;}
            // Rings of the same count, each in order round its axis, joined in sequence; both ends capped (convex rings).
            public void Tube(IList<int[]> rings)
            {
                for(int r=0;r+1<rings.Count;r++)
                {
                    var axis=(Centre(rings[r])+Centre(rings[r+1]))/2;int n=rings[r].Length;
                    for(int k=0;k<n;k++){int a=rings[r][k],b=rings[r][(k+1)%n],c=rings[r+1][(k+1)%n],d=rings[r+1][k];Quad(a,b,c,d,(v[a]+v[b]+v[c]+v[d])/4-axis);}
                }
                var first=rings[0];var last=rings[rings.Count-1];var dir=Centre(last)-Centre(first);
                for(int k=1;k+1<first.Length;k++){Tri(first[0],first[k],first[k+1],-dir);Tri(last[0],last[k],last[k+1],dir);}
            }
            // Saves in place when the asset exists, so references to it survive a rebuild.
            public Mesh Save(string path,string name)
            {
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(!mesh){mesh=new Mesh();AssetDatabase.CreateAsset(mesh,path);}else mesh.Clear();
                mesh.name=name;mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
                EditorUtility.SetDirty(mesh);AssetDatabase.SaveAssets();return mesh;
            }
        }
        // A thick polygon: a side outline in (x, y), either winding, scaled and moved, with a half thickness at each corner:
        // a pair front and back (z), or (0) one vertex on the middle plane. Caps ear-clipped, walls round the edges.
        static void ThickOutline(Hull h,(float x,float y,float half)[] corners,float scale,Vector2 offset)
        {
            int n=corners.Length;var front=new int[n];var back=new int[n];var flat=new Vector2[n];float area=0;
            for(int i=0;i<n;i++)
            {
                var (x,y,half)=corners[i];flat[i]=new Vector2(x,y)*scale+offset;half*=scale;
                front[i]=h.Add(new Vector3(flat[i].x,flat[i].y,-half));back[i]=half>0?h.Add(new Vector3(flat[i].x,flat[i].y,half)):front[i];
            }
            for(int i=0;i<n;i++){var a=flat[i];var c=flat[(i+1)%n];area+=a.x*c.y-c.x*a.y;}
            var caps=Triangulate(flat);
            for(int k=0;k<caps.Count;k+=3){h.Tri(front[caps[k]],front[caps[k+1]],front[caps[k+2]],Vector3.back);h.Tri(back[caps[k]],back[caps[k+1]],back[caps[k+2]],Vector3.forward);}
            for(int i=0;i<n;i++)
            {
                int j=(i+1)%n;var e=flat[j]-flat[i];var outward=new Vector3(e.y,-e.x,0)*Mathf.Sign(area);bool ti=front[i]!=back[i],tj=front[j]!=back[j];
                if(ti&&tj)h.Quad(front[i],front[j],back[j],back[i],outward);
                else if(ti)h.Tri(front[i],front[j],back[i],outward);
                else if(tj)h.Tri(front[i],front[j],back[j],outward);
            }
        }
        // Hatchet (60 triangles): the head's side view as a polygon (anticlockwise), each corner with its half thickness:
        // a pair front and back, or (0) one vertex on the middle plane along the thin edge, whose two inner corners stand
        // a little past the curve so the polygon straddles it (thicknesses and the eye block a little inside the look's,
        // whose rounded corners its square ones stand for); and the handle as rectangles round its line (shrunk so their
        // corners and sides straddle its oval) at the butt, two places along the bend, and up inside the eye.
        static Mesh HatchetShadow()
        {
            var h=new Hull();
            ThickOutline(h,new[]{(-.0180f,-.0254f,.0113f),(.0163f,-.0255f,.0119f),(.0212f,-.0268f,.0098f),(.0458f,-.0428f,.0057f),(.0740f,-.0772f,0f),(.0869f,-.0404f,0f),
                (.0943f,-.0027f,0f),(.0962f,.0362f,0f),(.0222f,.0245f,.0100f),(.0163f,.0296f,.0119f),(-.0180f,.0287f,.0113f)},1,new Vector2(0,EyeY));
            // The two middle rings go where the straight runs between them stray least from the handle's bend.
            var path=Curve(HandlePath,2);float bottom=HandlePath[0].y,top=HandlePath[HandlePath.Length-1].y,best=float.MaxValue,y1=0,y2=0;
            for(float a=.03f;a<=.13f;a+=.002f)for(float c=a+.03f;c<=.24f;c+=.002f)
            {
                var ys=new[]{bottom,a,c,top};float worst=0;
                for(float y=bottom;y<=top&&worst<best;y+=.002f)
                {
                    int s=y<a?0:y<c?1:2;float chord=Mathf.Lerp(HandleXAt(path,ys[s]),HandleXAt(path,ys[s+1]),(y-ys[s])/(ys[s+1]-ys[s]));
                    worst=Mathf.Max(worst,Mathf.Abs(chord-HandleXAt(path,y)));
                }
                if(worst<best){best=worst;y1=a;y2=c;}
            }
            // Each ring square to the handle there (the butt is cut square to it, a little aslant).
            var rings=new List<int[]>();
            foreach(var y in new[]{bottom,y1,y2,top})
            {
                var (rx,rz)=HandleRadii(y);float a=rx-.0010f,c=rz-.0022f;var centre=new Vector3(HandleXAt(path,y),y,0);   // truest from the front
                var tangent=y<=bottom?(path[1]-path[0]).normalized:new Vector3(HandleXAt(path,y+.002f)-HandleXAt(path,y-.002f),.004f,0).normalized;
                var side=new Vector3(tangent.y,-tangent.x,0);var z=Vector3.forward;
                rings.Add(new[]{h.Add(centre+side*a-z*c),h.Add(centre+side*a+z*c),h.Add(centre-side*a+z*c),h.Add(centre-side*a-z*c)});
            }
            h.Tube(rings);
            return h.Save(Props[0].shadow,"Hatchet shadow");
        }
        // Shoe brush (56): the bristle block and the back as two eight-sided ovals, each tapered to follow the look (the
        // bristles in a little at the bottom for its rounded edge, the back in at the top, more at the ends, for its
        // rounded top edge).
        static Mesh BrushShadow()
        {
            var h=new Hull();
            foreach(var (a,c,n,y0,sx0,sz0,y1,sx1,sz1) in new[]{(TuftA,TuftB,8,0f,.985f,.985f,TuftTop,1f,1f),(BackA,BackB,8,.0265f,1f,1f,.0435f,.965f,.985f)})
            {
                var oval=OvalPolygon(a,c,n);var rings=new List<int[]>();
                foreach(var (y,sx,sz) in new[]{(y0,sx0,sz0),(y1,sx1,sz1)})rings.Add(oval.Select(p=>h.Add(new Vector3(p.x*sx,y,p.y*sz))).ToArray());
                h.Tube(rings);
            }
            return h.Save(Props[1].shadow,"Shoe brush shadow");
        }
        // n (6, 8 or 10) corners round the superellipse a x c (power BrushPower), symmetric both ways, corners or flats at
        // the ends and the sides' middles; placed and scaled so the polygon straddles the curve as closely as it can.
        static Vector2[] OvalPolygon(float a,float c,int n)
        {
            Vector2 At(float deg){float t=deg*Mathf.Deg2Rad,cs=Mathf.Cos(t),sn=Mathf.Sin(t);return new Vector2(a*Mathf.Sign(cs)*Mathf.Pow(Mathf.Abs(cs),2/BrushPower),c*Mathf.Sign(sn)*Mathf.Pow(Mathf.Abs(sn),2/BrushPower));}
            var curve=Enumerable.Range(0,180).Select(i=>At(i*2f)).ToArray();
            float Dist(Vector2 p,Vector2[] poly)
            {
                float best=float.MaxValue;
                for(int i=0;i<poly.Length;i++){var q=poly[i];var d=poly[(i+1)%poly.Length]-q;float f=Mathf.Clamp01(Vector2.Dot(p-q,d)/d.sqrMagnitude);best=Mathf.Min(best,(q+d*f-p).magnitude);}
                return best;
            }
            Vector2[] chosen=null;float least=float.MaxValue;
            void Try(float[] degrees,float s)
            {
                var poly=degrees.Select(d=>At(d)*s).ToArray();
                float error=Mathf.Max(curve.Max(p=>Dist(p,poly)),poly.Max(p=>Dist(p,curve)));
                if(error<least){least=error;chosen=poly;}
            }
            for(float s=1;s<=1.08f;s+=.004f)
            {
                if(n==6)for(float t=6;t<=84;t+=2){Try(new[]{0,t,180-t,180,180+t,360-t},s);Try(new[]{t,90,180-t,180+t,270,360-t},s);}
                if(n==8)
                {
                    for(float t1=10;t1<=80;t1+=2)Try(new[]{0,t1,90,180-t1,180,180+t1,270,360-t1},s);
                    for(float t1=4;t1<=40;t1+=3)for(float t2=45;t2<=86;t2+=3)Try(new[]{t1,t2,180-t2,180-t1,180+t1,180+t2,360-t2,360-t1},s);
                }
                if(n==10)for(float t1=6;t1<=60;t1+=3)for(float t2=t1+6;t2<=86;t2+=3)
                {
                    Try(new[]{0,t1,t2,180-t2,180-t1,180,180+t1,180+t2,360-t2,360-t1},s);
                    Try(new[]{t1,t2,90,180-t2,180-t1,180+t1,180+t2,270,360-t2,360-t1},s);
                }
            }
            return chosen;
        }
        // Bean can (44): a twelve-sided prism over the seams' outline (the lid is gone, but the rim still bounds it).
        static Mesh BeanCanShadow()
        {
            const int Sides=12;const float R=.0376f;var h=new Hull();var rings=new List<int[]>();
            foreach(var y in new[]{0f,.110f})
            {
                var ring=new int[Sides];
                for(int s=0;s<Sides;s++){float a=(s+.5f)*2*Mathf.PI/Sides;ring[s]=h.Add(new Vector3(Mathf.Cos(a)*R,y,Mathf.Sin(a)*R));}
                rings.Add(ring);
            }
            h.Tube(rings);
            return h.Save(Props[2].shadow,"Bean can shadow");
        }
        // Harmonica (12): its box.
        static Mesh HarmonicaShadow()
        {
            var h=new Hull();var rings=new List<int[]>();
            foreach(var x in new[]{-HarpX,HarpX})rings.Add(new[]{h.Add(new Vector3(x,-CoverY,-HarpZ)),h.Add(new Vector3(x,CoverY,-HarpZ)),h.Add(new Vector3(x,CoverY,HarpZ)),h.Add(new Vector3(x,-CoverY,HarpZ))});
            h.Tube(rings);
            return h.Save(Props[3].shadow,"Harmonica detailed shadow");
        }
        // Revolver (98): its side view as one thick outline (barrel and ejector housing, frame, the trigger guard filled,
        // grip and hammer; the guard's bottom thin), and the cylinder as a six-sided prism round its axis, corners out at
        // the sides, straddling it. More than the others' 60: five separate features to keep. The filled guard is its
        // biggest miss (the two small openings either side of the trigger, about 7 mm).
        static Mesh RevolverShadow()
        {
            var h=new Hull();
            ThickOutline(h,new[]{(140.5f,-6.9f,7f),(100,-13.6f,7.3f),(14.5f,-13.6f,7.3f),(6,-42,10.2f),(-6,-42,10.2f),(-9.5f,-53,4),(-23,-62.4f,0),(-47,-47,11),(-66.5f,-108,13),
                (-85,-113.2f,11),(-104,-105,12),(-99.5f,-72,13.5f),(-79.5f,-26,13),(-62,4,10.5f),(-55,10,8),(-56.5f,20,2.5f),(-65.5f,27.2f,3),(-49,25,2.5f),(-42.5f,17,3),
                (-44.5f,12,5.4f),(140.5f,7,7)},.001f,Vector2.zero);
            const float Rv=CylR/.933f;var rings=new List<int[]>();
            foreach(var x in new[]{CylX0,CylX1})rings.Add(Enumerable.Range(0,6).Select(j=>{float a=j*Mathf.PI/3;return h.Add(new Vector3(x,CylY+Rv*Mathf.Sin(a),Rv*Mathf.Cos(a)));}).ToArray());
            h.Tube(rings);
            return h.Save(Props[4].shadow,"Revolver shadow");
        }
        // Playing cards (60): each card a thin box, square-cornered.
        static Mesh CardsShadow()
        {
            var h=new Hull();var corners=new[]{new Vector2(-1,-1),new Vector2(1,-1),new Vector2(1,1),new Vector2(-1,1)};
            for(int i=0;i<5;i++)
            {
                var (turn,centre,z)=CardPlace(i);var rings=new List<int[]>();
                foreach(var dz in new[]{-CardT/2,CardT/2})rings.Add(corners.Select(c=>h.Add(turn*(centre+new Vector3(c.x*CardW/2,c.y*CardH/2,0))+new Vector3(0,0,z+dz))).ToArray());
                h.Tube(rings);
            }
            return h.Save(Props[5].shadow,"Playing cards shadow");
        }
        // Dagger (58): the guard, grip and pommel as one thick outline (the guard's ends pointed over the balls; the grip's
        // and pommel's square sections shrunk to straddle their round ones), and the blade as its own diamond section
        // (edges in y, ridges in z) at the guard and four fifths along, then to the point.
        static Mesh DaggerShadow()
        {
            var h=new Hull();
            ThickOutline(h,new[]{(5f,34.6f,6f),(0,41f,3f),(-5,34.6f,6f),(-5,8.8f,7.1f),(-103,10.3f,10.3f),(-114,6.2f,6.2f),
                (-114,-6.2f,6.2f),(-103,-10.3f,10.3f),(-5,-8.8f,7.1f),(-5,-34.6f,6f),(0,-41f,3f),(5,-34.6f,6f)},.001f,Vector2.zero);
            int[] Ring(float x,float w,float t)=>new[]{h.Add(new Vector3(x,w,0)),h.Add(new Vector3(x,0,-t)),h.Add(new Vector3(x,-w,0)),h.Add(new Vector3(x,0,t))};
            var root=Ring(.004f,.0151f,.0036f);var mid=Ring(.135f,.0092f,.0017f);int tip=h.Add(new Vector3(.170f,0,0));
            for(int k=0;k<4;k++)
            {
                int a=root[k],b=root[(k+1)%4],c=mid[(k+1)%4],d=mid[k];var outward=(h.v[a]+h.v[b])/2-new Vector3(h.v[a].x,0,0);
                h.Quad(a,b,c,d,outward);h.Tri(d,c,tip,(h.v[c]+h.v[d])/2-new Vector3(h.v[c].x,0,0));
            }
            h.Tri(root[0],root[1],root[2],Vector3.left);h.Tri(root[0],root[2],root[3],Vector3.left);
            return h.Save(Props[6].shadow,"Dagger shadow");
        }

        // --- Textures ------------------------------------------------------------------------------------------------------
        static float Hash(int i,int seed){unchecked{int h=i*374761393+seed*668265263;h=(h^(h>>13))*1274126177;return ((h^(h>>16))&0xffff)/65535f;}}
        // Blackened hand-forged steel: dark and mottled, patches of greyer scale, and hammer dents (a darker hollow with a
        // catch-light on its rim); one tile every 100 mm.
        static Texture2D HatchetSteel()
        {
            const int S=256;var tex=new Texture2D(S,S,TextureFormat.RGB24,false,false);var rnd=new System.Random(5);
            var dents=Enumerable.Range(0,45).Select(i=>new Vector3((float)rnd.NextDouble(),(float)rnd.NextDouble(),Mathf.Lerp(.03f,.08f,(float)rnd.NextDouble()))).ToArray();
            for(int y=0;y<S;y++)for(int x=0;x<S;x++)
            {
                float u=x/(float)S,v=y/(float)S,n=Fbm(u,v,6,6,4,71),scale=Mathf.Clamp01((Fbm(u,v,3,3,3,73)-.5f)*4);
                var c=Color.Lerp(new Color(.13f,.13f,.14f),new Color(.24f,.24f,.25f),n);c=Color.Lerp(c,new Color(.32f,.31f,.30f),scale*.5f);
                // Broad shallow hammer facets: a soft darker hollow with a faint lighter rim, not spots.
                foreach(var d in dents)
                {
                    float dx=u-d.x,dy=v-d.y;dx-=Mathf.Round(dx);dy-=Mathf.Round(dy);float q=Mathf.Sqrt(dx*dx+dy*dy)/d.z;
                    if(q<1)c*=1-.10f*Mathf.SmoothStep(1,0,q/.8f)+.06f*Mathf.Exp(-Mathf.Pow((q-.88f)/.07f,2));
                }
                c*=.95f+.10f*Fbm(u,v,48,48,1,79);c.a=1;tex.SetPixel(x,y,c);
            }
            return SaveTexture(SteelPath,tex,false);
        }
        // Hickory: u along the handle (butt to top), v round it (the front, -z, at 0.25); straight grain with darker
        // streaks, and the lanyard hole through it near the butt (a dark hole each side).
        static Texture2D HandleWood()
        {
            const int W=512,H=128;var tex=new Texture2D(W,H,TextureFormat.RGB24,false,false);var light=new Color(.74f,.47f,.25f);var dark=new Color(.50f,.28f,.13f);
            float total=HandleLength(),holeU=HoleFromButt/total;const float Girth=.066f;
            for(int y=0;y<H;y++)for(int x=0;x<W;x++)
            {
                float u=(x+.5f)/W,v=(y+.5f)/H,t=v*7+1.6f*Fbm(u,v,3,6,3,81),ring=Mathf.Pow(.5f+.5f*Mathf.Sin(t*2*Mathf.PI),3);
                var c=Color.Lerp(light,dark,Mathf.Clamp01(ring*.55f+.30f*(Fbm(u,v,4,24,2,85)-.3f)));c*=.96f+.08f*Fbm(u,v,96,32,1,87);
                foreach(var side in new[]{.25f,.75f})
                {
                    float d=new Vector2((u-holeU)*total,(v-side)*Girth).magnitude;
                    if(d<.0026f)c=new Color(.07f,.05f,.04f);else if(d<.0034f)c*=.72f;
                }
                c.a=1;tex.SetPixel(x,y,c);
            }
            return SaveTexture(HandleWoodPath,tex,false);
        }
        // Horsehair. Lower half (v 0..0.5, tiling along u): the sides, hairs running down v from the wood (top) to the
        // tips, in tufts TuftsPerTile to a tile with darker gaps between them, darker towards the wood. Upper half: the
        // trimmed bottom seen from below (planar over the whole block), staggered rows of tufts of speckled hair ends.
        static Texture2D Bristles()
        {
            const int S=256;var tex=new Texture2D(S,S,TextureFormat.RGB24,false,false);
            var dark=new Color(.52f,.36f,.11f);var light=new Color(.86f,.68f,.30f);
            for(int y=0;y<S;y++)for(int x=0;x<S;x++)
            {
                float u=(x+.5f)/S,v=(y+.5f)/S;Color c;
                if(v<.5f)
                {
                    float f=Mathf.Clamp01((v-.02f)/.46f);   // 0 at the tips, 1 at the wood
                    int col=x+Mathf.RoundToInt(3*Fbm(u,v,2,2,2,91));col=(col%S+S)%S;
                    float strand=Hash(col,93),band=Fbm(u,v,40,1,2,95);
                    float tuftEdge=Mathf.Abs(Mathf.Repeat(u*TuftsPerTile+.25f*Fbm(u,v,2,2,2,97),1)-.5f)*2;
                    c=Color.Lerp(dark,light,.30f+.25f*band+.45f*strand);
                    if(strand<.12f)c*=.80f;   // the odd dark gap between hairs
                    c*=(1-.20f*Mathf.Pow(tuftEdge,4))*Mathf.Lerp(1.04f,.66f,Mathf.Pow(f,1.6f));
                }
                else
                {
                    // Nearest tuft centre on staggered rows, each tuft nudged a little off the grid.
                    float X=(u-.5f)/.48f*TuftA,Z=(v-.75f)/.23f*TuftB,d=1;const float Row=.0056f,Col=.0062f;
                    int row0=Mathf.RoundToInt(Z/Row);
                    for(int row=row0-1;row<=row0+1;row++)
                    {
                        float off=(row&1)==1?Col/2:0;int ci=Mathf.RoundToInt((X-off)/Col);
                        for(int cc=ci-1;cc<=ci+1;cc++)
                        {
                            int id=row*97+cc;var centre=new Vector2(cc*Col+off+(Hash(id,103)-.5f)*.0014f,row*Row+(Hash(id,107)-.5f)*.0012f);
                            d=Mathf.Min(d,Vector2.Distance(new Vector2(X,Z),centre));
                        }
                    }
                    float gap=Mathf.Clamp01((d-.0024f)/.0014f);
                    c=Color.Lerp(dark,light,.25f+.60f*Hash(y*S+x,99))*(1-.16f*gap)*(.92f+.14f*Fbm(u,v,8,8,2,101));
                }
                c.a=1;tex.SetPixel(x,y,c);
            }
            return SaveTexture(BristlePath,tex,false);
        }
        // The label: teal paper (the user's reference colour) with a simple drawing of three beans on the front (u 0.75,
        // -z) and again on the back (u 0.25), so one faces out however the can is turned (the user, 2026-11-17: "add a
        // bean"; still no text, brand or logo). A faint paper grain, darker hairline edges, and the seam on the +x side
        // (u 0): the overlapping end's edge with its thin shadow. u runs left to right seen from outside.
        static readonly (float u,Vector2 at,float len,float deg)[] LabelBeans={
            (.75f,new Vector2(-.0150f,-.0105f),.022f,22),(.75f,new Vector2(.0150f,-.0105f),.022f,-22),(.75f,new Vector2(0,.0070f),.030f,0),
            (.25f,new Vector2(-.0150f,-.0105f),.022f,22),(.25f,new Vector2(.0150f,-.0105f),.022f,-22),(.25f,new Vector2(0,.0070f),.030f,0)};
        static Texture2D Label()
        {
            const int W=1024,H=384;const float Round=2*Mathf.PI*CanBody,Tall=LabelY1-LabelY0;
            var tex=new Texture2D(W,H,TextureFormat.RGB24,false,false);var teal=new Color(.45f,.75f,.75f);
            for(int y=0;y<H;y++)for(int x=0;x<W;x++)
            {
                float u=(x+.5f)/W,v=(y+.5f)/H;
                var c=teal*(.97f+.03f*Fbm(u,v,24,8,3,61)+.025f*(Fbm(u,v,256,96,1,67)-.5f));
                if(x>=W-2)c*=.82f;else if(x<4)c*=1.03f;   // the seam
                // The beans, 2 x 2 samples a pixel for clean edges; later ones drawn over earlier ones.
                var sum=Color.clear;
                for(int s=0;s<4;s++)
                {
                    float su=(x+.25f+.5f*(s&1))/W,sv=(y+.25f+.5f*(s>>1))/H;var sample=c;
                    foreach(var bean in LabelBeans)
                    {
                        var p=new Vector2((su-bean.u)*Round,(sv-.5f)*Tall)-bean.at;float r=bean.deg*Mathf.Deg2Rad;
                        float a=(p.x*Mathf.Cos(r)+p.y*Mathf.Sin(r))/(bean.len/2),b=(-p.x*Mathf.Sin(r)+p.y*Mathf.Cos(r))/(bean.len/2);
                        if(BeanColour(a,b,out var colour))sample=colour;
                    }
                    sum+=sample;
                }
                c=sum/4;
                if(Mathf.Min(y,H-1-y)<2)c*=.88f;
                c.a=1;tex.SetPixel(x,y,c);
            }
            return SaveTexture(LabelPath,tex,false,TextureWrapMode.Clamp);
        }
        // A drawn kidney bean at (x along it, y across it), both in half its length: an oval 0.54 as wide as long, its
        // ends turned up a touch, with a shallow round notch out of the middle of its upper side and the small pale eye
        // just under it; warm tan, darker towards the rim, a dark outline and a soft highlight. Colours kept low: the
        // gym's light lifts lit faces about 1.5x.
        static bool BeanColour(float x,float y,out Color colour)
        {
            colour=Color.clear;const float Half=.54f,NotchY=1.12f,NotchR=.68f;
            float yy=y-.10f*x*x,q=x*x+yy*yy/(Half*Half);if(q>=1)return false;
            float notch=new Vector2(x,yy-NotchY).magnitude-NotchR;if(notch<=0)return false;
            float edge=Mathf.Min(1-Mathf.Sqrt(q),notch/Half);
            colour=Color.Lerp(new Color(.46f,.26f,.11f),new Color(.62f,.40f,.19f),Mathf.SmoothStep(0,1,edge/.5f));
            float shine=Mathf.Exp(-(x+.40f)*(x+.40f)/.05f-(yy-.24f)*(yy-.24f)/.010f);
            colour=Color.Lerp(colour,new Color(.73f,.54f,.34f),.75f*shine);
            if(x*x/.020f+(yy-.36f)*(yy-.36f)/.0035f<1)colour=new Color(.72f,.60f,.46f);
            if(edge<.09f)colour=new Color(.26f,.13f,.05f);
            return true;
        }
        // The tin along its profile (v from the bottom's middle to the beans): albedo and a normal map of the ribs, three
        // round beads in each band beside the label and two embossed rings in the bottom panel.
        static Texture2D TinTextures()
        {
            const int W=4,H=1024;float total=TinLength;var bottom=BottomArcs;var top=TopArcs;
            var bumps=new List<(float at,float half,float height)>();
            foreach(var r in new[]{.022f,.014f})bumps.Add((r,.0007f,.00025f));
            foreach(var f in new[]{.2f,.5f,.8f}){bumps.Add((Mathf.Lerp(bottom[7],bottom[8],f),.00045f,.0003f));bumps.Add((Mathf.Lerp(top[0],top[1],f),.00045f,.0003f));}
            float Height(float s){float h=0;foreach(var (at,half,height) in bumps){float q=Mathf.Abs(s-at)/half;if(q<1)h+=height*(1+Mathf.Cos(q*Mathf.PI))/2;}return h;}
            var albedo=new Texture2D(W,H,TextureFormat.RGB24,false,false);var normal=new Texture2D(W,H,TextureFormat.RGB24,false,true);
            for(int y=0;y<H;y++)
            {
                float s=(y+.5f)/H*total,ds=total/H,slope=(Height(s+ds)-Height(s-ds))/(2*ds);
                var c=new Color(.70f,.71f,.73f)*(1-.12f*Mathf.Min(1,Mathf.Abs(slope)));c.a=1;
                var n=new Vector3(0,-slope,1).normalized;var nc=new Color(n.x*.5f+.5f,n.y*.5f+.5f,n.z*.5f+.5f);
                for(int x=0;x<W;x++){albedo.SetPixel(x,y,c);normal.SetPixel(x,y,nc);}
            }
            SaveTexture(TinNormalPath,normal,true);
            return SaveTexture(TinPath,albedo,false);
        }
        // Baked beans in tomato sauce seen from above, across the can's opening (u, v = x, z over BeanSpan): about a
        // hundred pale beans, ovals bent a little, at random angles, half sunk in glossy orange-red sauce; albedo and a
        // normal map (beans 2.8 mm proud).
        static Texture2D BeansTextures()
        {
            const int S=512;const float PxPerM=S/BeanSpan,Sauce=.38f,Proud=.0028f;var rnd=new System.Random(7);
            var height=new float[S,S];var tint=new float[S,S];var placed=new List<Vector2>();
            for(int tries=0;tries<6000&&placed.Count<140;tries++)
            {
                float r=Mathf.Sqrt((float)rnd.NextDouble())*.0350f,a=(float)rnd.NextDouble()*2*Mathf.PI;var c=new Vector2(Mathf.Cos(a),Mathf.Sin(a))*r;
                if(placed.Any(o=>Vector2.Distance(o,c)<.0050f))continue;
                placed.Add(c);
                float len=.0045f+.0007f*(float)rnd.NextDouble(),wid=len*.62f,ang=(float)rnd.NextDouble()*Mathf.PI,ca=Mathf.Cos(ang),sa=Mathf.Sin(ang);
                float shade=(float)rnd.NextDouble(),lift=.75f+.25f*(float)rnd.NextDouble();
                var px=c*PxPerM+Vector2.one*S/2;int reach=Mathf.CeilToInt(len*PxPerM)+1;
                for(int y=Mathf.Max(0,(int)px.y-reach);y<Mathf.Min(S,(int)px.y+reach);y++)for(int x=Mathf.Max(0,(int)px.x-reach);x<Mathf.Min(S,(int)px.x+reach);x++)
                {
                    float dx=(x+.5f-px.x)/PxPerM,dy=(y+.5f-px.y)/PxPerM,along=(dx*ca+dy*sa)/len,across=(-dx*sa+dy*ca)/wid-.18f*along*along,q=along*along+across*across;
                    if(q>=1)continue;float hh=lift*Mathf.Sqrt(1-q);
                    if(hh>height[y,x]){height[y,x]=hh;tint[y,x]=shade;}
                }
            }
            float H(int x,int y)=>Mathf.Max(height[Mathf.Clamp(y,0,S-1),Mathf.Clamp(x,0,S-1)],Sauce)*Proud;
            var albedo=new Texture2D(S,S,TextureFormat.RGB24,false,false);var normal=new Texture2D(S,S,TextureFormat.RGB24,false,true);var sauce=new Color(.50f,.14f,.05f);
            for(int y=0;y<S;y++)for(int x=0;x<S;x++)
            {
                float u=x/(float)S,v=y/(float)S,bean=Mathf.SmoothStep(0,1,Mathf.Clamp01((height[y,x]-Sauce)/.30f));
                var beanColour=Color.Lerp(new Color(.66f,.38f,.17f),new Color(.78f,.50f,.25f),tint[y,x])*(.85f+.2f*height[y,x]);
                var c=Color.Lerp(sauce*(.9f+.2f*Fbm(u,v,8,8,3,111)),beanColour,bean);c.a=1;albedo.SetPixel(x,y,c);
                float dhx=(H(x+1,y)-H(x-1,y))*PxPerM/2,dhy=(H(x,y+1)-H(x,y-1))*PxPerM/2;var n=new Vector3(-dhx,-dhy,1).normalized;
                normal.SetPixel(x,y,new Color(n.x*.5f+.5f,n.y*.5f+.5f,n.z*.5f+.5f));
            }
            SaveTexture(BeansNormalPath,normal,true,TextureWrapMode.Clamp);
            return SaveTexture(BeansPath,albedo,false,TextureWrapMode.Clamp);
        }
        // The revolver's blued steel. In each tile (u round the cylinder, one flute to a tile centred on its edge, between
        // two chambers; v along the cylinder) a flute from v 0.30 to 0.72 with round ends, 5.8 mm wide and 1.9 mm deep,
        // darker in its hollow; the rest plain blued steel (the other blued parts use a plain spot). Albedo and normal map.
        static Texture2D RevolverFlutes()
        {
            const int W=128,H=256;const float Girth=2*Mathf.PI*CylR/6,Len=CylX1-CylX0,Half=.0029f,Deep=.0019f;
            float Height(float u,float v)
            {
                float du=(u-Mathf.Round(u))*Girth,y=v*Len,y0=.30f*Len+Half,y1=.72f*Len-Half,dy=y<y0?y0-y:y>y1?y-y1:0,d=Mathf.Sqrt(du*du+dy*dy);
                return d<Half?-Deep*Mathf.Sqrt(1-d*d/(Half*Half)):0;
            }
            var albedo=new Texture2D(W,H,TextureFormat.RGB24,false,false);var normal=new Texture2D(W,H,TextureFormat.RGB24,false,true);var blued=new Color(.16f,.18f,.24f);
            for(int y=0;y<H;y++)for(int x=0;x<W;x++)
            {
                float u=(x+.5f)/W,v=(y+.5f)/H,h=Height(u,v);
                float dhu=(Height(u+1f/W,v)-Height(u-1f/W,v))/(2*Girth/W),dhv=(Height(u,v+1f/H)-Height(u,v-1f/H))/(2*Len/H);
                var n=new Vector3(-dhu,-dhv,1).normalized;normal.SetPixel(x,y,new Color(n.x*.5f+.5f,n.y*.5f+.5f,n.z*.5f+.5f));
                var c=blued*(1+.55f*h/Deep)*(.95f+.10f*Fbm(u,v,4,8,3,161));c.a=1;albedo.SetPixel(x,y,c);
            }
            SaveTexture(FlutesNormalPath,normal,true);
            return SaveTexture(FlutesPath,albedo,false);
        }
        // Colour case-hardening: steel grey mottled with patches of blue, violet and straw, as the bone-charcoal pack leaves
        // it; kept darkish (the gym's light lifts lit faces about 1.5x).
        static Texture2D CaseColours()
        {
            const int S=256;var tex=new Texture2D(S,S,TextureFormat.RGB24,false,false);
            var grey=new Color(.36f,.37f,.39f);var blue=new Color(.16f,.24f,.42f);var violet=new Color(.30f,.22f,.36f);var straw=new Color(.50f,.38f,.20f);
            for(int y=0;y<S;y++)for(int x=0;x<S;x++)
            {
                float u=x/(float)S,v=y/(float)S;var c=grey;
                c=Color.Lerp(c,blue,Mathf.Clamp01((Fbm(u,v,3,3,4,131)-.48f)*4));
                c=Color.Lerp(c,violet,Mathf.Clamp01((Fbm(u,v,4,4,4,137)-.55f)*4)*.8f);
                c=Color.Lerp(c,straw,Mathf.Clamp01((Fbm(u,v,5,5,3,139)-.58f)*5)*.7f);
                c*=.92f+.12f*Fbm(u,v,24,24,2,141);c.a=1;tex.SetPixel(x,y,c);
            }
            return SaveTexture(CasePath,tex,false);
        }
        // Stacked leather washers along the dagger's grip (u, guard to pommel), each its own brown, dark lines between, and
        // brass spacers at both ends and the middle; v round the grip.
        static Texture2D DaggerGrip()
        {
            const int W=512,H=32,Washers=30;var tex=new Texture2D(W,H,TextureFormat.RGB24,false,false);
            for(int y=0;y<H;y++)for(int x=0;x<W;x++)
            {
                float u=(x+.5f)/W,v=(y+.5f)/H;Color c;
                if(u<.03f||u>.97f||Mathf.Abs(u-.5f)<.012f)c=new Color(.60f,.47f,.20f);
                else
                {
                    float w=u*Washers,f=w-Mathf.Floor(w);
                    c=Color.Lerp(new Color(.30f,.17f,.08f),new Color(.46f,.28f,.14f),Hash(Mathf.FloorToInt(w),151))*(.93f+.10f*Fbm(u,v,8,2,2,153));
                    if(f<.07f||f>.95f)c*=.55f;
                }
                c.a=1;tex.SetPixel(x,y,c);
            }
            return SaveTexture(DaggerGripPath,tex,false);
        }
        // The card atlas, 4 x 2 cells of 256 x 512 (row 0 at the bottom): the five faces (CardPips), the back (cell 5), and
        // plain card white (cell 6, for the edges). Suit shapes drawn exactly, 2 x 2 samples a pixel.
        static Texture2D CardAtlas()
        {
            const int W=1024,H=1024,CellW=256,CellH=512;var tex=new Texture2D(W,H,TextureFormat.RGB24,false,false);
            var pips=Enumerable.Range(0,5).Select(i=>CardPips(i)).ToArray();
            for(int y=0;y<H;y++)for(int x=0;x<W;x++)
            {
                int c=x/CellW+(y/CellH)*4;var sum=Color.clear;
                for(int s=0;s<4;s++)
                {
                    float X=((x%CellW+.25f+.5f*(s&1))/CellW-.5f)*CardW*1000,Y=((y%CellH+.25f+.5f*(s>>1))/CellH-.5f)*CardH*1000;   // mm on the card
                    sum+=CardTexel(c,X,Y,c<5?pips[c]:null);
                }
                var col=sum/4*(.97f+.04f*Fbm(x/(float)W,y/(float)H,32,32,2,171));col.a=1;tex.SetPixel(x,y,col);
            }
            return SaveTexture(CardsPath,tex,false,TextureWrapMode.Clamp);
        }
        static Color CardTexel(int cell,float X,float Y,List<(float x,float y,float size,bool flip,char suit)> pips)
        {
            var paper=new Color(.90f,.89f,.85f);
            if(cell==5)
            {
                float m=Mathf.Min(CardW*500-Mathf.Abs(X),CardH*500-Mathf.Abs(Y));   // mm in from the edge
                if(m<3.5f||(m>5.2f&&m<5.7f))return paper;
                float l1=Mathf.Abs(Mathf.Repeat((X+Y)/4.2f,1)-.5f),l2=Mathf.Abs(Mathf.Repeat((X-Y)/4.2f,1)-.5f);
                return Mathf.Max(l1,l2)>.40f?new Color(.66f,.24f,.24f):new Color(.50f,.08f,.09f);
            }
            if(pips==null)return paper;
            foreach(var (px,py,size,flip,suit) in pips)
            {
                float half=size/2;if(Mathf.Abs(X-px)>half||Mathf.Abs(Y-py)>half)continue;
                float sx=(X-px)/(half*.85f),sy=(Y-py)/half;if(flip){sx=-sx;sy=-sy;}
                if(InSuit(suit,sx,sy))return suit=='D'?new Color(.66f,.07f,.09f):new Color(.07f,.07f,.08f);
            }
            return paper;
        }
        // Card c's pips in mm from its middle (x right, y up): size is the pip's height; the lower ones turned round.
        static List<(float x,float y,float size,bool flip,char suit)> CardPips(int c)
        {
            char suit=c==0||c==2?'S':c==1||c==3?'C':'D';var list=new List<(float,float,float,bool,char)>();
            if(c==0)list.Add((0,0,46,false,'S'));
            else if(c==1)list.Add((0,0,26,false,'C'));
            else
            {
                var spots=new List<Vector2>();
                if(c<4){foreach(var y in new[]{29f,0,-29f}){spots.Add(new Vector2(-13.5f,y));spots.Add(new Vector2(13.5f,y));}spots.Add(new Vector2(0,14.5f));spots.Add(new Vector2(0,-14.5f));}
                else{foreach(var y in new[]{29f,9.7f,-9.7f,-29f}){spots.Add(new Vector2(-13.5f,y));spots.Add(new Vector2(13.5f,y));}spots.Add(Vector2.zero);}
                foreach(var p in spots)list.Add((p.x,p.y,12.5f,p.y<-.1f,suit));
            }
            list.Add((-25.5f,33.5f,7.5f,false,suit));list.Add((25.5f,-33.5f,7.5f,true,suit));
            return list;
        }
        // Suit shapes in a box -1..1 (x, y up). Spade: a heart curve turned point up on a flared stem; club: three round
        // leaves on the same stem; diamond: a rhombus.
        static bool InSuit(char suit,float x,float y)
        {
            float X=x*1.15f,Y=y*1.35f-.35f;
            float Heart(float a,float b){float q=a*a+b*b-1;return q*q*q-a*a*b*b*b;}
            bool Stem(float from)=>Y>-1.70f&&Y<from&&Mathf.Abs(X)<.07f+.5f*Mathf.Pow((from-Y)/(from+1.70f),2);
            switch(suit)
            {
                case 'S':return Heart(X,-Y)<=0||Stem(-.85f);
                case 'C':return new Vector2(X,Y-.45f).magnitude<.50f||new Vector2(X+.52f,Y+.32f).magnitude<.50f||new Vector2(X-.52f,Y+.32f).magnitude<.50f
                    ||new Vector2(X,Y+.05f).magnitude<.30f||Stem(-.60f);
                default:return Mathf.Abs(x)/.78f+Mathf.Abs(y)<1;
            }
        }
    }
}
