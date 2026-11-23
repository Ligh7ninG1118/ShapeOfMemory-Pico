using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static ShadowWorkshopIntegration.Editor.PropMesh;

namespace ShadowWorkshopIntegration.Editor
{
    // Two more Western things for WildWest (2026-11-16): a hurricane oil lantern and a whiskey bottle, built like the other
    // props (procedural, smooth normals doing the rounding, detail in textures generated here, mobile VR budget).
    // Lantern: 170 mm tall without its bail: a squat blue-grey fuel tank, a bulging globe of the shared CozyShops glass
    // round a burner and a small emissive flame, six wire guards, a vented top cap and a wire bail standing over it.
    // Bottle: 200 mm tall and 70 mm across: opaque glossy amber glass, a cork, a cream paper label with borders and an
    // XXX (no words).
    // Bourbon bottle (added 2026-11-16 from the user's photo, "加一个这种whiskey"): a square-shouldered bottle of clear
    // glass (the shared CozyShops glass), 80 x 55 mm and 180 mm tall, whiskey inside to about 85 percent, a plain pale
    // label on the front and a cream screw cap.
    // Shadows: each casts a light closed hull instead of its look (WildWest's shadow budget is 4000 triangles for all its
    // props): lantern 108, bottle 80, bourbon 60. The lantern's leaves out the guard wires and the bail: kept within 2 mm
    // the bail would cost about 50 more.
    // Each in its own frame: metres, +y up, -z the front, standing on y = 0 round the y axis.
    public static class WesternGlassPropsBuilder
    {
        const string Art="Assets/ShadowWorkshop/Art/Torch2/",Meshes=Art+"Meshes/",Textures=Art+"Textures/",MaterialFolder=Art+"Materials/";
        const string GlassPath="Assets/Scenes/Store/Sari3D/CozyShops_Lite/Materials/M_CozyShops_Glass.mat";
        const string PaintPath=Textures+"lantern paint.png",BottleGlassPath=Textures+"whiskey bottle glass.png",LabelPath=Textures+"whiskey label.png",CorkPath=Textures+"bottle cork.png";
        const string BourbonLabelPath=Textures+"bourbon label.png",ScrewCapPath=Textures+"bottle screw cap.png";
        public const string LanternPath=Meshes+"Oil lantern.asset",LanternShadowPath=Meshes+"Oil lantern shadow.asset",BottlePath=Meshes+"Whiskey bottle.asset";
        public const string BottleShadowPath=Meshes+"Whiskey bottle shadow.asset",BourbonPath=Meshes+"Bourbon bottle.asset",BourbonShadowPath=Meshes+"Bourbon bottle shadow.asset";

        // mesh: the drawn look; shadow: the light hull that casts the shadow; materials in submesh order.
        public sealed class Prop{public string label,mesh,shadow;public System.Func<Material[]> materials;}
        public static readonly Prop[] Props={
            new Prop{label="OIL LANTERN",mesh=LanternPath,shadow=LanternShadowPath,materials=LanternMaterials},
            new Prop{label="WHISKEY BOTTLE",mesh=BottlePath,shadow=BottleShadowPath,materials=BottleMaterials},
            new Prop{label="BOURBON BOTTLE",mesh=BourbonPath,shadow=BourbonShadowPath,materials=BourbonMaterials}};
        public static Mesh MeshOf(Prop p)=>AssetDatabase.LoadAssetAtPath<Mesh>(p.mesh);
        public static Mesh ShadowOf(Prop p)=>AssetDatabase.LoadAssetAtPath<Mesh>(p.shadow);

        static Texture2D Load(string path)=>AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        static Vector2 V(float r,float h)=>new Vector2(r,h);
        static readonly Look Paint=new Look("lantern paint",Color.white,.25f,.45f){baseMap=()=>Load(PaintPath)??PaintTexture()};
        static readonly Look Steel=new Look("kitchen steel",new Color(.86f,.87f,.89f),.75f,.86f);
        static readonly Look Dark=new Look("western dark",new Color(.05f,.04f,.035f),0f,.20f);
        static readonly Look Flame=new Look("lantern flame",new Color(1f,.62f,.22f),0f,.50f);
        static readonly Color FlameGlow=new Color(1.75f,.82f,.18f,1f);
        static readonly Look BottleGlass=new Look("whiskey bottle glass",Color.white,0f,.92f){baseMap=()=>Load(BottleGlassPath)??BottleGlassTexture()};
        static readonly Look Label=new Look("whiskey label",Color.white,0f,.15f){baseMap=()=>Load(LabelPath)??LabelTexture()};
        static readonly Look Cork=new Look("bottle cork",Color.white,0f,.12f){baseMap=()=>Load(CorkPath)??CorkTexture()};
        // Not the shot glass's "whiskey": behind the pale, teal-tinted shared glass that one reads grey-brown. This is more
        // saturated (the glass in front lightens and cools it), with a faint glow for light passing through it.
        static readonly Look Whiskey=new Look("bourbon whiskey",new Color(.42f,.17f,.010f),0f,.90f);
        static readonly Color WhiskeyGlow=new Color(.36f,.15f,.004f,1f);
        static readonly Look BourbonLabel=new Look("bourbon label",Color.white,0f,.18f){baseMap=()=>Load(BourbonLabelPath)??BourbonLabelTexture()};
        static readonly Look ScrewCap=new Look("bottle screw cap",Color.white,0f,.35f){baseMap=()=>Load(ScrewCapPath)??ScrewCapTexture()};

        // Lantern submeshes: paint, steel (burner, bail), dark (vents, wick), glass, flame.
        const int PaintM=0,SteelM=1,DarkM=2,GlassM=3,FlameM=4;
        public static Material[] LanternMaterials()
        {
            var m=PropMesh.Materials(MaterialFolder,new[]{Paint,Steel,Dark,Flame});
            return new[]{m[0],m[1],m[2],AssetDatabase.LoadAssetAtPath<Material>(GlassPath),Glow(m[3],FlameGlow)};
        }
        // Emission, set when the material is new (PropMesh.Materials leaves existing ones alone, so tweaks survive). In this
        // order: URP's material check (BaseShaderGUI.SetMaterialKeywords, run when the material is loaded and on every
        // SetColor here) keeps _EMISSION only with an emissive GI flag; a new material's EmissiveIsBlack, or None, turns it off.
        static Material Glow(Material m,Color emission)
        {
            if(m.IsKeywordEnabled("_EMISSION"))return m;
            m.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;m.SetColor("_EmissionColor",emission);
            m.EnableKeyword("_EMISSION");EditorUtility.SetDirty(m);AssetDatabase.SaveAssetIfDirty(m);return m;
        }
        public static Material[] BottleMaterials()=>PropMesh.Materials(MaterialFolder,new[]{BottleGlass,Label,Cork});
        // Bourbon submeshes: glass, whiskey, label, cap.
        public static Material[] BourbonMaterials()
        {
            var m=PropMesh.Materials(MaterialFolder,new[]{Whiskey,BourbonLabel,ScrewCap});
            return new[]{AssetDatabase.LoadAssetAtPath<Material>(GlassPath),Glow(m[0],WhiskeyGlow),m[1],m[2]};
        }

        [MenuItem("MetaHackthone/Shadow Workshop/Build Western lantern and bottle")]
        public static void Build()
        {
            PaintTexture();BottleGlassTexture();LabelTexture();CorkTexture();BourbonLabelTexture();ScrewCapTexture();
            var built=new[]{(Lantern(),LanternShadow()),(Bottle(),BottleShadow()),(Bourbon(),BourbonShadow())};
            foreach(var p in Props)p.materials();
            Debug.Log("WESTERN_GLASS_PROPS_OK "+string.Join(", ",built.Select(m=>$"{m.Item1.name} {m.Item1.triangles.Length/3} (shadow {m.Item2.triangles.Length/3})")));
        }

        // Planar-ish paint UVs: Revolve runs u once round, so a wide ring would stretch the paint's noise; times repeats
        // (whole, so the seam meets) it is about as dense round as up.
        static void ScaleU(PropMesh b,int from,float repeats){for(int i=from;i<b.uv.Count;i++)b.uv[i]=new Vector2(b.uv[i].x*repeats,b.uv[i].y);}

        // --- Lantern -----------------------------------------------------------------------------------------------------
        const int CapSides=12;
        static readonly Vector2[] GlobeProfile={V(.0305f,.0560f),V(.0365f,.0660f),V(.0418f,.0830f),V(.0428f,.0990f),V(.0395f,.1180f),V(.0298f,.1390f)};
        // A guard wire in its plane (r, y): out of the collar's side, bowed round the globe's belly, in under the cap's rim.
        static readonly Vector2[] Bow={V(.0335f,.0500f),V(.0395f,.0580f),V(.0450f,.0740f),V(.0475f,.0960f),V(.0445f,.1200f),V(.0335f,.1460f)};
        // The bail's arch: half widths and height over its pivots, which sit at y BailY on the cap's sides (+-x).
        const float BailY=.1465f,BailRx=.0445f,BailRy=.0755f;
        static Vector3 Arch(float deg)=>new Vector3(BailRx*Mathf.Cos(deg*Mathf.Deg2Rad),BailY+.0035f+BailRy*Mathf.Sin(deg*Mathf.Deg2Rad),0);

        static Mesh Lantern()
        {
            var b=new PropMesh(5);
            // Fuel tank, 98 mm across: flat bottom, a barrel side, a shoulder in to the collar that holds the globe, and the
            // deck inside the collar (the burner stands in its hole).
            int from=b.v.Count;
            b.Revolve(PaintM,14,new[]{V(0,0),V(.0440f,0),V(.0440f,0),V(.0490f,.0060f),V(.0490f,.0230f),V(.0420f,.0370f),V(.0350f,.0420f),V(.0350f,.0420f),
                V(.0350f,.0560f),V(.0350f,.0560f),V(.0155f,.0560f)});
            ScaleU(b,from,8);
            // Burner, wick, flame.
            b.Revolve(SteelM,8,new[]{V(.0160f,.0550f),V(.0150f,.0622f),V(.0032f,.0650f)});
            b.Revolve(DarkM,6,new[]{V(.0032f,.0640f),V(.0032f,.0688f),V(0,.0692f)});
            b.Revolve(FlameM,8,new[]{V(0,.0690f),V(.0036f,.0705f),V(.0054f,.0745f),V(.0050f,.0800f),V(.0030f,.0865f),V(0,.0935f)});
            // Globe: its inside first (the far wall seen through the near one; one submesh's transparent triangles draw in
            // index order), coarser and 1.8 mm in, then the outside.
            var inner=new[]{0,2,3,5}.Select(i=>V(GlobeProfile[i].x-.0018f,GlobeProfile[i].y)).Reverse().ToArray();
            b.Revolve(GlassM,8,inner);
            b.Revolve(GlassM,14,GlobeProfile);
            // Top cap: a flat rim over the globe, a band carrying the vents, a dome.
            from=b.v.Count;
            b.Revolve(PaintM,CapSides,new[]{V(.0290f,.1385f),V(.0365f,.1385f),V(.0375f,.1400f),V(.0375f,.1515f),V(.0335f,.1580f),V(.0210f,.1650f),V(.0100f,.1690f),V(0,.1700f)});
            ScaleU(b,from,6);
            // Vents: dark slots on the band's facets, all but the four beside the bail's pivots at +-x (vertices 0 and
            // CapSides/2), each lying 0.3 mm proud of its flat facet.
            for(int k=0;k<CapSides;k++)
            {
                if(k==0||k==CapSides/2-1||k==CapSides/2||k==CapSides-1)continue;
                float a=(k+.5f)*2*Mathf.PI/CapSides;var n=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var t=new Vector3(-n.z,0,n.x)*.0015f;
                var c=n*(.0375f*Mathf.Cos(Mathf.PI/CapSides)+.0003f);var lo=Vector3.up*.1428f;var hi=Vector3.up*.1494f;
                b.Quad(DarkM,b.Add(c-t+lo,n,Vector2.zero),b.Add(c+t+lo,n,Vector2.right),b.Add(c+t+hi,n,Vector2.one),b.Add(c-t+hi,n,Vector2.up));
            }
            // Guards: six wires (none straight in front of the flame), their ends buried in the collar and the cap.
            for(int j=0;j<6;j++)
            {
                float a=j*Mathf.PI/3;var radial=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var side=new Vector3(-radial.z,0,radial.x);
                b.Bar(PaintM,Bow.Select(p=>radial*p.x+Vector3.up*p.y).ToList(),side,t=>Ellipse(.0011f,.0011f,3),0,0);
            }
            // Bail: a steel wire hooked into the cap's sides, bent up and arching over the top (a three-sided section,
            // shaded round, so the arch can have points every 15 degrees).
            var bail=new List<Vector3>{new Vector3(-.0310f,BailY,0),new Vector3(-.0400f,BailY,0),new Vector3(-.0438f,BailY+.0028f,0)};
            for(int deg=165;deg>=15;deg-=15)bail.Add(Arch(deg));
            bail.Add(new Vector3(.0438f,BailY+.0028f,0));bail.Add(new Vector3(.0400f,BailY,0));bail.Add(new Vector3(.0310f,BailY,0));
            b.Bar(SteelM,bail,Vector3.forward,t=>Ellipse(.0014f,.0014f,3),0,0);
            return b.Save(LanternPath,"Oil lantern");
        }

        // The lantern's shadow: a 7-sided hull up the tank, in at the collar, round the globe's belly, in under the cap's rim,
        // up the rim and over the dome (eight rings beat 8 sides with fewer, or mixed counts, at this budget). The guard wires
        // (thin lines) and the bail are left out: the bail within 2 mm would cost about 50 more. 108 triangles.
        static Mesh LanternShadow()
        {
            var b=new PropMesh(1);
            Hull(b,new[]{V(.0468f,0),V(.0493f,.0210f),V(.0370f,.0425f),V(.0350f,.0565f),V(.0428f,.0950f),V(.0335f,.1355f),V(.0382f,.1495f),V(.0170f,.1680f)}.Select(p=>Round(p.x,p.y,7,0)).ToList());
            return b.Save(LanternShadowPath,"Oil lantern shadow");
        }

        // --- Shadow hulls -----------------------------------------------------------------------------------------------------
        // Rings of points round the y axis (each a convex loop round it), stitched band to band by angle round the axis (so
        // rings may differ in point count) and closed flat at both ends: closed and wound outward. Normals point away from
        // the axis; they only wind the triangles (a shadow is not shaded).
        static void Hull(PropMesh b,IList<Vector3[]> rings)
        {
            float Angle(Vector3 p){float a=Mathf.Atan2(p.z,p.x);return a<0?a+2*Mathf.PI:a;}
            var sorted=rings.Select(r=>r.OrderBy(Angle).ToArray()).ToList();
            var ids=sorted.Select(r=>r.Select(p=>b.Add(p,new Vector3(p.x,0,p.z).normalized,Vector2.zero)).ToArray()).ToList();
            for(int j=0;j+1<sorted.Count;j++)
            {
                var A=sorted[j];var B=sorted[j+1];int n=A.Length,m=B.Length,i=0,k=0;
                float At(Vector3[] r,int t)=>Angle(r[t%r.Length])+2*Mathf.PI*(t/r.Length);
                while(i<n||k<m)
                {
                    bool stepA=k>=m||(i<n&&At(A,i+1)<=At(B,k+1));
                    if(stepA){b.Tri(0,ids[j][i%n],ids[j+1][k%m],ids[j][(i+1)%n]);i++;}
                    else{b.Tri(0,ids[j][i%n],ids[j+1][k%m],ids[j+1][(k+1)%m]);k++;}
                }
            }
            foreach(var (ring,dir) in new[]{(sorted[0],Vector3.down),(sorted[sorted.Count-1],Vector3.up)})
            {
                int start=b.v.Count;foreach(var p in ring)b.Add(p,dir,Vector2.zero);
                for(int k=1;k+1<ring.Length;k++)b.Tri(0,start,start+k,start+k+1);
            }
        }
        // A ring of n points round a circle of radius r at height y, pushed out so the polygon straddles the circle (its
        // corners as far outside as its flats are inside).
        static Vector3[] Round(float r,float y,int n,float phase)
        {
            float grow=2/(1+Mathf.Cos(Mathf.PI/n));
            return Enumerable.Range(0,n).Select(k=>{float a=phase+k*2*Mathf.PI/n;return new Vector3(Mathf.Cos(a)*r*grow,y,Mathf.Sin(a)*r*grow);}).ToArray();
        }
        // The four corners of a rectangle standing in for a rounded one (half sizes hx, hz, corner radius r) at height y,
        // pulled in so its corners stand as far outside the rounded corners as its sides are inside the flats.
        static Vector3[] Box(float hx,float hz,float r,float y)
        {
            float d=.1716f*r;hx-=d;hz-=d;
            return new[]{new Vector3(hx,y,hz),new Vector3(-hx,y,hz),new Vector3(-hx,y,-hz),new Vector3(hx,y,-hz)};
        }

        // --- Bottle -------------------------------------------------------------------------------------------------------
        const int BottleSides=20;const float BottleTop=.205f;
        static Mesh Bottle()
        {
            var b=new PropMesh(3);const int Glass=0,Paper=1,Stopper=2;
            // Body: a shallow punt, a round heel, the straight side, a round shoulder into a tapering neck, a rolled lip.
            int from=b.v.Count;
            b.Revolve(Glass,BottleSides,new[]{V(0,.0020f),V(.0300f,0),V(.0350f,.0100f),V(.0350f,.1120f),V(.0322f,.1270f),V(.0230f,.1410f),V(.0140f,.1525f),
                V(.0120f,.1780f),V(.0120f,.1780f),V(.0146f,.1795f),V(.0149f,.1865f),V(.0105f,.1885f)});
            // The glass's shading runs up the bottle: v 0..1 from the base to the top.
            for(int i=from;i<b.uv.Count;i++)b.uv[i]=new Vector2(b.uv[i].x,b.v[i].y/BottleTop);
            // Label: cream paper round the front, 58 mm tall, over 8 of the body's 20 facets and 0.3 mm proud of them; u runs
            // left to right as seen from the front.
            const float Y0=.040f,Y1=.098f,R=.0353f;const int K0=11,K1=19;var bottom=new int[K1-K0+1];var top=new int[K1-K0+1];
            for(int k=K0;k<=K1;k++)
            {
                float a=k*2*Mathf.PI/BottleSides,u=(float)(k-K0)/(K1-K0);var radial=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                bottom[k-K0]=b.Add(radial*R+Vector3.up*Y0,radial,new Vector2(u,0));top[k-K0]=b.Add(radial*R+Vector3.up*Y1,radial,new Vector2(u,1));
            }
            for(int i=0;i<K1-K0;i++)b.Quad(Paper,bottom[i],bottom[i+1],top[i+1],top[i]);
            // Cork: a little wider at the top, 14 mm out of the lip, its top edge rounded.
            from=b.v.Count;
            b.Revolve(Stopper,12,new[]{V(.0104f,.1850f),V(.0113f,.1995f),V(.0104f,.2022f),V(0,.2028f)});
            ScaleU(b,from,2);
            return b.Save(BottlePath,"Whiskey bottle");
        }
        // Its shadow: a 7-sided hull round the heel, up the straight side, down the shoulder in two slopes, then one prism for
        // the neck, lip and cork (10.4 to 15 mm in radius, within 2 mm of each). 80 triangles.
        static Mesh BottleShadow()
        {
            var b=new PropMesh(1);
            Hull(b,new[]{V(.0315f,0),V(.0350f,.0110f),V(.0350f,.1120f),V(.0290f,.1330f),V(.0128f,.1550f),V(.0128f,.2025f)}.Select(p=>Round(p.x,p.y,7,0)).ToList());
            return b.Save(BottleShadowPath,"Whiskey bottle shadow");
        }

        // --- Bourbon bottle -------------------------------------------------------------------------------------------------
        // Rings up the y axis of a rounded-rectangle section: height, half width (x), half depth (z), corner radius.
        struct Ring{public float y,hx,hz,r;public Ring(float y,float hx,float hz,float r){this.y=y;this.hx=hx;this.hz=hz;this.r=r;}}
        const int CornerSteps=3;
        // The section's own normals round the ring, tilted by the slope between neighbouring rings, so the flat sides shade
        // flat, the corners round and the shoulder slopes; u round the ring, v up.
        static void Loft(PropMesh b,int m,Ring[] rings)
        {
            int count=rings.Length;var sections=rings.Select(q=>RoundedRect(-q.hx,q.hx,-q.hz,q.hz,q.r,CornerSteps)).ToArray();int n=sections[0].at.Length;
            Vector3 At(int j,int k){var p=sections[j].at[k%n];return new Vector3(p.x,rings[j].y,p.y);}
            var grid=new int[count,n+1];
            for(int j=0;j<count;j++)for(int k=0;k<=n;k++)
            {
                var a=At(Mathf.Min(j+1,count-1),k)-At(Mathf.Max(j-1,0),k);var h=sections[j].normal[k%n];var out2=new Vector3(h.x,0,h.y);
                grid[j,k]=b.Add(At(j,k),(out2*a.y-Vector3.up*Vector3.Dot(a,out2)).normalized,new Vector2((float)k/n,rings[j].y*UvPerMetre));
            }
            for(int j=0;j+1<count;j++)for(int k=0;k<n;k++)b.Quad(m,grid[j,k],grid[j+1,k],grid[j+1,k+1],grid[j,k+1]);
        }
        // A flat end on a ring: a fan to a hub at hubY, facing along normal.
        static void LoftCap(PropMesh b,int m,Ring ring,float hubY,Vector3 normal)
        {
            var at=RoundedRect(-ring.hx,ring.hx,-ring.hz,ring.hz,ring.r,CornerSteps).at;int hub=b.Add(new Vector3(0,hubY,0),normal,Vector2.zero),start=b.v.Count;
            foreach(var p in at)b.Add(new Vector3(p.x,ring.y,p.y),normal,new Vector2(p.x,p.y)*UvPerMetre);
            for(int k=0;k<at.Length;k++)b.Tri(m,hub,start+k,start+(k+1)%at.Length);
        }
        // 80 x 55 mm across the flat sides, 180 mm tall to the top of the cap. The glass: a thick base with a rounded heel,
        // straight sides, a square shoulder rounding into a short neck under the cap. The whiskey: a block 2.5 mm inside the
        // walls from the top of the base (9 mm) to the fill line, flat on top. The label: flat on the front (-z).
        static Mesh Bourbon()
        {
            var b=new PropMesh(4);const int Glass=0,Liquor=1,Paper=2,Cap=3;
            var glass=new[]{new Ring(0,.0372f,.0247f,.0055f),new Ring(.0030f,.0396f,.0271f,.0076f),new Ring(.0090f,.0400f,.0275f,.0080f),new Ring(.1200f,.0400f,.0275f,.0080f),
                new Ring(.1310f,.0393f,.0268f,.0085f),new Ring(.1385f,.0360f,.0245f,.0105f),new Ring(.1440f,.0280f,.0200f,.0130f),new Ring(.1480f,.0185f,.0160f,.0150f),
                new Ring(.1510f,.0138f,.0138f,.0138f),new Ring(.1600f,.0132f,.0132f,.0132f)};
            Loft(b,Glass,glass);LoftCap(b,Glass,glass[0],.0015f,Vector3.down);
            const float Fill=.1300f;var liquor=new[]{new Ring(.0095f,.0372f,.0247f,.0058f),new Ring(Fill,.0372f,.0247f,.0058f)};
            Loft(b,Liquor,liquor);LoftCap(b,Liquor,liquor[0],liquor[0].y,Vector3.down);LoftCap(b,Liquor,liquor[1],Fill,Vector3.up);
            // Label, 0.4 mm proud of the flat front.
            const float LW=.031f,L0=.036f,L1=.098f,LZ=-.0279f;
            b.Quad(Paper,b.Add(new Vector3(-LW,L0,LZ),Vector3.back,new Vector2(0,0)),b.Add(new Vector3(LW,L0,LZ),Vector3.back,new Vector2(1,0)),
                b.Add(new Vector3(LW,L1,LZ),Vector3.back,new Vector2(1,1)),b.Add(new Vector3(-LW,L1,LZ),Vector3.back,new Vector2(0,1)));
            // Screw cap over the neck: chamfered at both ends; ridges round its side from the texture (v 0.25), plain top (v 0.75).
            int from=b.v.Count;
            b.Revolve(Cap,14,new[]{V(.0150f,.1570f),V(.0158f,.1585f),V(.0158f,.1585f),V(.0158f,.1785f),V(.0158f,.1785f),V(.0146f,.1800f),V(0,.1802f)});
            for(int i=from;i<b.uv.Count;i++)b.uv[i]=new Vector2(b.uv[i].x,b.n[i].y>.7f?.75f:.25f);
            return b.Save(BourbonPath,"Bourbon bottle");
        }
        // Its shadow: plain rectangles (cheaper than chamfered ones, and as close: see Box) up the body and round the
        // shoulder in three steps, then an octagon prism for the neck and cap (13.8 and 15.8 mm in radius). 60 triangles.
        static Mesh BourbonShadow()
        {
            var b=new PropMesh(1);
            Hull(b,new[]{Box(.0392f,.0267f,.0078f,0),Box(.0400f,.0275f,.0080f,.1240f),Box(.0367f,.0250f,.0101f,.1370f),Box(.0256f,.0190f,.0135f,.1450f),
                Round(.0150f,.1510f,8,Mathf.PI/8),Round(.0150f,.1802f,8,Mathf.PI/8)});
            return b.Save(BourbonShadowPath,"Bourbon bottle shadow");
        }

        // --- Textures (detail colours kept at about 0.6 or below: the gym's light lifts lit faces about 1.5x) ------------------
        // Blue-grey paint, mottled, with dull patches and a few small chips down to dark metal edged with rust.
        static Texture2D PaintTexture()
        {
            const int S=256;var tex=new Texture2D(S,S,TextureFormat.RGB24,false,false);
            for(int y=0;y<S;y++)for(int x=0;x<S;x++)
            {
                float u=x/(float)S,v=y/(float)S,n=Fbm(u,v,6,6,4,61),chip=Fbm(u,v,8,8,4,67),grime=Fbm(u,v,3,3,3,71);
                var c=Color.Lerp(new Color(.26f,.33f,.39f),new Color(.33f,.41f,.47f),n);
                c=Color.Lerp(c,new Color(.24f,.26f,.27f),Mathf.Clamp01((grime-.58f)*4)*.35f);
                c=Color.Lerp(c,new Color(.34f,.22f,.14f),Mathf.Clamp01((chip-.735f)*40)*.6f);
                c=Color.Lerp(c,new Color(.16f,.16f,.17f),Mathf.Clamp01((chip-.76f)*40));
                tex.SetPixel(x,y,c);
            }
            return SaveTexture(PaintPath,tex,false);
        }
        // Amber glass up the bottle (v = height / 205 mm): deepest at the thick heel, warmest down the straight side,
        // darker again over the shoulder and up the neck.
        static Texture2D BottleGlassTexture()
        {
            const int W=4,H=128;var tex=new Texture2D(W,H,TextureFormat.RGB24,false,false);
            var deep=new Color(.17f,.075f,.022f);var amber=new Color(.44f,.21f,.06f);var neck=new Color(.27f,.12f,.035f);
            for(int y=0;y<H;y++)
            {
                float v=(y+.5f)/H;
                var c=v<.06f?Color.Lerp(deep,amber,v/.06f):v<.55f?Color.Lerp(amber,amber*1.06f,Mathf.Sin((v-.06f)/.49f*Mathf.PI)):Color.Lerp(amber,neck,Mathf.Clamp01((v-.55f)/.2f));
                c.a=1;for(int x=0;x<W;x++)tex.SetPixel(x,y,c);
            }
            return SaveTexture(BottleGlassPath,tex,false,TextureWrapMode.Clamp);
        }
        // The label, drawn in millimetres (88.7 x 58 mm on the bottle): aged cream paper, a thick and a thin border, maroon
        // bands top and bottom dotted with cream diamonds, and a serifed XXX between two diamonds. No words.
        static Texture2D LabelTexture()
        {
            const int W=512,H=256;const float Wmm=88.7f,Hmm=58f;var tex=new Texture2D(W,H,TextureFormat.RGB24,false,false);
            var paper=new Color(.76f,.68f,.50f);var ink=new Color(.17f,.09f,.05f);var red=new Color(.48f,.10f,.07f);
            float Segment(Vector2 p,Vector2 a,Vector2 c){var d=c-a;float t=Mathf.Clamp01(Vector2.Dot(p-a,d)/d.sqrMagnitude);return Vector2.Distance(p,a+d*t);}
            for(int y=0;y<H;y++)for(int x=0;x<W;x++)
            {
                float u=(x+.5f)/W,v=(y+.5f)/H;var p=new Vector2(u*Wmm,v*Hmm);
                float edge=Mathf.Min(Mathf.Min(p.x,Wmm-p.x),Mathf.Min(p.y,Hmm-p.y));
                var c=paper*(.93f+.10f*Fbm(u,v,10,6,4,91));c*=Mathf.Lerp(.80f,1,Mathf.Clamp01(edge/5));
                if(Mathf.Abs(edge-3.0f)<.75f||Mathf.Abs(edge-5.1f)<.28f)c=ink;
                // Bands, inside the thin border.
                bool inBand=p.x>6.2f&&p.x<Wmm-6.2f&&((p.y>6.2f&&p.y<13.2f)||(p.y>Hmm-13.2f&&p.y<Hmm-6.2f));
                if(inBand)
                {
                    c=red*(.92f+.10f*Fbm(u,v,10,6,3,93));
                    float cy=p.y<Hmm/2?9.7f:Hmm-9.7f,cx=Wmm/2+Mathf.Round((p.x-Wmm/2)/6f)*6f;
                    if(Mathf.Abs(p.x-cx)+Mathf.Abs(p.y-cy)<1.7f&&Mathf.Abs(cx-Wmm/2)<Wmm/2-9f)c=paper*.95f;
                }
                // XXX: three crosses 10 x 14 mm, strokes 2.6 mm, a short serif across each end.
                for(int i=-1;i<=1;i++)
                {
                    var o=new Vector2(Wmm/2+i*13.5f,Hmm/2);const float hw=5f,hh=7f;
                    float d=Mathf.Min(Segment(p,o+new Vector2(-hw,-hh),o+new Vector2(hw,hh)),Segment(p,o+new Vector2(-hw,hh),o+new Vector2(hw,-hh)));
                    foreach(var sx in new[]{-1f,1f})foreach(var sy in new[]{-1f,1f})
                        d=Mathf.Min(d,Segment(p,o+new Vector2(sx*hw-2.2f,sy*hh),o+new Vector2(sx*hw+2.2f,sy*hh))+.75f);
                    if(d<1.3f)c=ink;
                }
                foreach(var sx in new[]{-1f,1f})if(Mathf.Abs(p.x-(Wmm/2+sx*31f))+Mathf.Abs(p.y-Hmm/2)<3.2f)c=red;
                c.a=1;tex.SetPixel(x,y,c);
            }
            return SaveTexture(LabelPath,tex,false,TextureWrapMode.Clamp);
        }
        // The bourbon's label, 62 x 62 mm: pale paper, a thin and a thick black rule round it, an oval with a diamond in it
        // and a short rule above and below. No words.
        static Texture2D BourbonLabelTexture()
        {
            const int S=256;const float Mm=62f;var tex=new Texture2D(S,S,TextureFormat.RGB24,false,false);
            var paper=new Color(.78f,.76f,.69f);var ink=new Color(.10f,.09f,.08f);var c0=new Vector2(Mm/2,Mm/2);
            for(int y=0;y<S;y++)for(int x=0;x<S;x++)
            {
                float u=(x+.5f)/S,v=(y+.5f)/S;var p=new Vector2(u*Mm,v*Mm);
                float edge=Mathf.Min(Mathf.Min(p.x,Mm-p.x),Mathf.Min(p.y,Mm-p.y));
                var c=paper*(.95f+.07f*Fbm(u,v,8,8,4,111));c*=Mathf.Lerp(.88f,1,Mathf.Clamp01(edge/4));
                if(Mathf.Abs(edge-2.2f)<.22f||Mathf.Abs(edge-3.6f)<.5f)c=ink;
                var q=(p-c0);float oval=new Vector2(q.x/15f,q.y/10.5f).magnitude;
                if(Mathf.Abs(oval-1)*12.5f<.45f)c=ink;
                if(Mathf.Abs(q.x)+Mathf.Abs(q.y)*1.3f<3.4f)c=ink;
                foreach(var ry in new[]{-16f,16f})
                {
                    if(Mathf.Abs(q.y-ry)<.35f&&Mathf.Abs(q.x)<15f)c=ink;
                    foreach(var dx in new[]{-17.5f,17.5f})if(new Vector2(q.x-dx,q.y-ry).magnitude<.9f)c=ink;
                }
                c.a=1;tex.SetPixel(x,y,c);
            }
            return SaveTexture(BourbonLabelPath,tex,false,TextureWrapMode.Clamp);
        }
        // Cream plastic: 48 ridges round the cap's side in the lower half (v < 0.5), the plain top in the upper half.
        static Texture2D ScrewCapTexture()
        {
            const int W=256,H=4;var tex=new Texture2D(W,H,TextureFormat.RGB24,false,false);var cream=new Color(.76f,.73f,.66f);
            for(int y=0;y<H;y++)for(int x=0;x<W;x++)
            {
                float u=(x+.5f)/W,ridge=.5f+.5f*Mathf.Cos(u*48*2*Mathf.PI);
                var c=y<H/2?cream*Mathf.Lerp(.74f,1f,Mathf.Pow(ridge,.6f)):cream*.98f;c.a=1;tex.SetPixel(x,y,c);
            }
            return SaveTexture(ScrewCapPath,tex,false);
        }
        static Texture2D CorkTexture()
        {
            const int S=128;var tex=new Texture2D(S,S,TextureFormat.RGB24,false,false);
            for(int y=0;y<S;y++)for(int x=0;x<S;x++)
            {
                float u=x/(float)S,v=y/(float)S,speck=Fbm(u,v,24,24,2,103);
                var c=Color.Lerp(new Color(.44f,.31f,.18f),new Color(.60f,.45f,.29f),Fbm(u,v,6,6,3,101));
                c=Color.Lerp(c,new Color(.22f,.14f,.08f),Mathf.Clamp01((speck-.60f)*10));tex.SetPixel(x,y,c);
            }
            return SaveTexture(CorkPath,tex,false);
        }
    }
}
