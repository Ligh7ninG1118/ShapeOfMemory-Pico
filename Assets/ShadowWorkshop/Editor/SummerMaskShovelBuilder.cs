using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static ShadowWorkshopIntegration.Editor.PropMesh;

namespace ShadowWorkshopIntegration.Editor
{
    // Two props for the Summer level (2026-11-17, see SummerBuilder), built like the WildWest ones (procedural, real size,
    // smooth normals doing the rounding, detail in textures generated here, mobile VR budget).
    // Diving mask ("潜水镜"): a single-lens snorkel mask upright, facing -z. The frame, 160 x 75 mm in bright blue plastic,
    // holds one lens of the shared CozyShops glass; behind it a clear silicone skirt 50 mm deep, flaring to a rolled face
    // seal and bulging into a nose pocket under the lens; a ribbed blue strap loops round 95 mm behind the frame from a
    // buckle on each side (20 mm wide, 30 at the back).
    // Sand shovel (from the user's photo): a kids' beach spade lying on y = 0, 272 mm long, glossy golden yellow: a flat
    // scoop 110 x 120 mm at +x with low walls along the sides and the back (16 mm at the back, running out towards the
    // front, which is open, thinned and slightly pointed), a straight 25 mm handle with a groove along its top rising
    // 12 degrees from the back wall, and an oval loop grip round a 37 x 24 mm hole.
    // Sand rake (added later that day, the picture now a seal; the user: "沙滩耙子 ... 做一个三个齿的 齿细一点",
    // "你这个弧度做巧妙一点哦，我要用来做胡子的"; then, over a first try with three curved tines fanning like whiskers, a sketch of
    // a comb: "这个不好看 这样"): the shovel's yellow plastic, 221 mm long, upright in the xy plane facing -z: a straight handle
    // with a rounded end, a crossbar 85 mm tall square across its end and four straight teeth 75 mm long running on from
    // the bar parallel to the handle, evenly spaced, the outer two level with the bar's ends. Made after the level was
    // opened in the gym, so it goes straight into the gym's pile (SummerBuilder.AddToGym). The user set it at 0.405 for
    // the seal's whiskers ("加粗齿"), so the teeth are 8.6 mm wide at the root and 7.6 at the tip, about 3.5 and 3.1 there
    // (thinner won't register on the wall's 8.8 mm grid).
    // Shadows: light closed hulls: the mask's frame, skirt and nose pocket as one block plus the strap as a thin band; the
    // shovel's scoop as a wedge, the handle as a bar and the grip as a ring round its hole (the hole shows in the shadow);
    // the rake's handle and crossbar coarse, its teeth at full width.
    public static class SummerMaskShovelBuilder
    {
        const string GlassPath="Assets/Scenes/Store/Sari3D/CozyShops_Lite/Materials/M_CozyShops_Glass.mat";
        const string StrapTexPath=SummerBuilder.Textures+"summer mask strap.png",ShovelTexPath=SummerBuilder.Textures+"summer sand shovel.png";
        public const string MaskPath=SummerBuilder.Meshes+"Summer diving mask.asset",MaskShadowPath=SummerBuilder.Meshes+"Summer diving mask shadow.asset";
        public const string ShovelPath=SummerBuilder.Meshes+"Summer sand shovel.asset",ShovelShadowPath=SummerBuilder.Meshes+"Summer sand shovel shadow.asset";
        public const string RakePath=SummerBuilder.Meshes+"Summer sand rake.asset",RakeShadowPath=SummerBuilder.Meshes+"Summer sand rake shadow.asset";

        static Texture2D Load(string path)=>AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        // Colours at about 0.6 or below: the gym's light lifts lit faces about 1.5x.
        static readonly Look Frame=new Look("summer mask frame",new Color(.04f,.30f,.58f),0f,.62f);
        static readonly Look Strap=new Look("summer mask strap",Color.white,0f,.30f){baseMap=()=>Load(StrapTexPath)??StrapTexture()};
        static readonly Look Skirt=new Look("summer mask skirt",new Color(.60f,.66f,.70f,.55f),0f,.78f);
        static readonly Look Plastic=new Look("summer shovel plastic",Color.white,0f,.72f){baseMap=()=>Load(ShovelTexPath)??ShovelTexture()};

        // Mask submeshes: frame (and buckles), strap, skirt (and nose pocket), lens. The skirt draws before the lens: both
        // are see-through, and one renderer's transparent submeshes draw in order.
        const int FrameM=0,StrapM=1,SkirtM=2,LensM=3;
        public static Material[] MaskMaterials()
        {
            var m=PropMesh.Materials(SummerBuilder.MaterialFolder,new[]{Frame,Strap,Skirt});
            return new[]{m[0],m[1],Translucent(m[2]),AssetDatabase.LoadAssetAtPath<Material>(GlassPath)};
        }
        public static Material[] ShovelMaterials()=>PropMesh.Materials(SummerBuilder.MaterialFolder,new[]{Plastic});
        // URP Lit made transparent (alpha blended, its alpha from the base colour), once, when the material is new.
        static Material Translucent(Material m)
        {
            if(!m.HasProperty("_Surface")||m.GetFloat("_Surface")>.5f)return m;
            m.SetFloat("_Surface",1);m.SetFloat("_Blend",0);m.SetFloat("_ZWrite",0);
            if(m.HasProperty("_AlphaClip"))m.SetFloat("_AlphaClip",0);
            m.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);m.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
            if(m.HasProperty("_SrcBlendAlpha")){m.SetFloat("_SrcBlendAlpha",(float)BlendMode.One);m.SetFloat("_DstBlendAlpha",(float)BlendMode.OneMinusSrcAlpha);}
            m.SetOverrideTag("RenderType","Transparent");m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");m.DisableKeyword("_ALPHATEST_ON");
            m.renderQueue=(int)RenderQueue.Transparent;EditorUtility.SetDirty(m);AssetDatabase.SaveAssetIfDirty(m);return m;
        }

        // Into the gym's pile like the rake (first staged under Summer props; since the level was opened in the gym, Stage
        // would set out a second pair outside the pile and save the scene): built again, each keeps where the user put it.
        [MenuItem("MetaHackthone/Shadow Workshop/Summer/Build diving mask and sand shovel")]
        public static void Build()
        {
            StrapTexture();ShovelTexture();
            Mesh mask=Mask(),maskShadow=MaskShadow(),shovel=Shovel(),shovelShadow=ShovelShadow();
            SummerBuilder.AddToGym("DIVING MASK",mask,maskShadow,MaskMaterials());
            SummerBuilder.AddToGym("SAND SHOVEL",shovel,shovelShadow,ShovelMaterials());
            Debug.Log("SUMMER_MASK_SHOVEL_OK "+string.Join(", ",new[]{(mask,maskShadow),(shovel,shovelShadow)}.Select(p=>
                $"{p.Item1.name} {p.Item1.triangles.Length/3} (shadow {p.Item2.triangles.Length/3}), {p.Item1.bounds.size.x*1000:0} x {p.Item1.bounds.size.y*1000:0} x {p.Item1.bounds.size.z*1000:0} mm")));
        }

        // Into the gym's pile (not Stage: the staging group is gone and the user is arranging the pile); built again, it keeps
        // where the user put it. The scene is left unsaved.
        [MenuItem("MetaHackthone/Shadow Workshop/Summer/Build sand rake")]
        public static void BuildRake()
        {
            Mesh rake=Rake(),shadow=RakeShadow();
            SummerBuilder.AddToGym("SAND RAKE",rake,shadow,ShovelMaterials());
            Debug.Log($"SUMMER_RAKE_OK {rake.name} {rake.triangles.Length/3} (shadow {shadow.triangles.Length/3}), {rake.bounds.size.x*1000:0} x {rake.bounds.size.y*1000:0} x {rake.bounds.size.z*1000:0} mm");
        }

        // --- Shared shapes ------------------------------------------------------------------------------------------------
        // Outward normals of a closed outline (either winding).
        static Vector2[] Normals(IList<Vector2> o)
        {
            int c=o.Count;float area=0;for(int i=0;i<c;i++){var a=o[i];var d=o[(i+1)%c];area+=a.x*d.y-d.x*a.y;}
            var n=new Vector2[c];
            for(int i=0;i<c;i++){var t=(o[(i+1)%c]-o[(i+c-1)%c]).normalized;n[i]=new Vector2(t.y,-t.x)*(area>0?1:-1);}
            return n;
        }
        // Rings of equal point count stitched band to band, smooth both ways (normals from the neighbouring points), all
        // turned so that the first ring's face away from its middle (outward) or towards it. caps closes both ends with a
        // fan to the ring's middle.
        static void Loft(PropMesh b,int m,IList<Vector3[]> rings,bool outward,bool caps)
        {
            int R=rings.Count,N=rings[0].Length;var mid=rings.Select(r=>r.Aggregate(Vector3.zero,(s,p)=>s+p)/r.Length).ToArray();
            var normal=new Vector3[R,N];float sign=0;
            for(int j=0;j<R;j++)for(int k=0;k<N;k++)
            {
                var around=(rings[j][(k+1)%N]-rings[j][(k+N-1)%N])*1000;var along=(rings[Mathf.Min(j+1,R-1)][k]-rings[Mathf.Max(j-1,0)][k])*1000;
                normal[j,k]=Vector3.Cross(along,around).normalized;if(j==0)sign+=Vector3.Dot(normal[j,k],rings[j][k]-mid[j]);
            }
            float flip=(sign>0)==outward?1:-1;var ids=new int[R,N];
            for(int j=0;j<R;j++)for(int k=0;k<N;k++){var p=rings[j][k];ids[j,k]=b.Add(p,normal[j,k]*flip,new Vector2(p.x,p.y)*UvPerMetre);}
            for(int j=0;j+1<R;j++)for(int k=0;k<N;k++)b.Quad(m,ids[j,k],ids[j+1,k],ids[j+1,(k+1)%N],ids[j,(k+1)%N]);
            if(!caps)return;
            foreach(var (j,o) in new[]{(0,1),(R-1,R-2)})
            {
                var nn=(mid[j]-mid[o]).normalized;int hub=b.Add(mid[j],nn,Vector2.zero),start=b.v.Count;
                foreach(var p in rings[j])b.Add(p,nn,Vector2.zero);
                for(int k=0;k<N;k++)b.Tri(m,hub,start+k,start+(k+1)%N);
            }
        }

        // --- Diving mask --------------------------------------------------------------------------------------------------
        // The frame's outer edge seen from the front, 160 x 75 mm round (0, 0), the notch for the nose up into its bottom.
        static List<Vector2> MaskOutline()=>RoundCorners(new (Vector2 p,float r,int steps)[]{
            (new Vector2(.080f,.0375f),.024f,3),(new Vector2(.080f,-.0375f),.022f,3),(new Vector2(.030f,-.0375f),.010f,1),(new Vector2(0,-.0090f),.012f,2),
            (new Vector2(-.030f,-.0375f),.010f,1),(new Vector2(-.080f,-.0375f),.022f,3),(new Vector2(-.080f,.0375f),.024f,3)},true);
        // Pulls the outline's bottom under the nose down towards y (weight 0..1), fading out beside the nose.
        static Vector2 UnderNose(Vector2 p,float y,float weight)
        {
            float w=weight*Mathf.Clamp01((.044f-Mathf.Abs(p.x))/.012f);
            return p.y<0&&p.y>y?new Vector2(p.x,Mathf.Lerp(p.y,y,w)):p;
        }
        static List<Vector3> StrapPath(int steps)
        {
            var path=new List<Vector3>{new Vector3(.0870f,0,.0040f),new Vector3(.0950f,0,.0240f)};
            for(int i=0;i<=steps;i++){float a=i*Mathf.PI/steps;path.Add(new Vector3(.0970f*Mathf.Cos(a),0,.0500f+.0470f*Mathf.Sin(a)));}
            path.Add(new Vector3(-.0950f,0,.0240f));path.Add(new Vector3(-.0870f,0,.0040f));
            return path;
        }
        // The strap's section (x up, y its 2.5 mm thickness): 20 mm wide, widening to 30 round the back.
        static (Vector2[] at,Vector2[] normal) StrapSection(float t)
        {
            float back=Mathf.Clamp01(1-Mathf.Abs(t-.5f)/.32f),w=.010f+.005f*back*back*(3-2*back);const float h=.00125f;
            return (new[]{new Vector2(w,h),new Vector2(-w,h),new Vector2(-w,-h),new Vector2(w,-h)},
                new[]{new Vector2(.25f,1).normalized,new Vector2(-.25f,1).normalized,new Vector2(-.25f,-1).normalized,new Vector2(.25f,-1).normalized});
        }

        static Mesh Mask()
        {
            var b=new PropMesh(4);var o=MaskOutline();var n=Normals(o);int N=o.Count;
            // Skirt, from the inside out (the far wall draws before the near one): the inner wall from just behind the lens
            // back to the rolled face seal (its sides 6 mm further back), the outer wall bulging forward to the frame's back.
            Vector3[] Ring(float grow,float z,float noseY,float weight,float sideBack)=>Enumerable.Range(0,N).Select(k=>{
                var p=UnderNose(o[k]+n[k]*grow,noseY,weight);return new Vector3(p.x,p.y,z+sideBack*(p.x/.08f)*(p.x/.08f));}).ToArray();
            Loft(b,SkirtM,new[]{Ring(-.0080f,.0030f,0,0,0),Ring(.0010f,.0490f,-.0400f,1,.006f),Ring(.0075f,.0500f,-.0460f,1,.006f),
                Ring(.0045f,.0260f,-.0440f,.8f,0),Ring(-.0015f,.0020f,0,0,0)},false,false);
            // Nose pocket: a rounded wedge filling the notch, 6 mm below the frame, its front sloping back towards the top.
            var nose=RoundCorners(new (Vector2 p,float r,int steps)[]{(new Vector2(.034f,-.044f),.009f,1),(new Vector2(0,.002f),.010f,1),(new Vector2(-.034f,-.044f),.009f,1)},true).ToArray();
            float Front(Vector2 p)=>Mathf.Lerp(-.0070f,.0010f,Mathf.Clamp01((p.y+.044f)/.046f));const float Back=.0380f;
            b.Slab(SkirtM,i=>SkirtM,nose,.006f,p=>(Back-Front(p))/2,p=>(Back+Front(p))/2,1,UvPerMetre);
            // Frame: a band 9 mm wide round the outline, 16.5 mm deep, its front crowned and stepping in to the lens.
            var path=Enumerable.Range(0,N).Select(k=>new Vector3(o[k].x-n[k].x*.0045f,o[k].y-n[k].y*.0045f,0)).ToArray();
            var rim=(new[]{new Vector2(-.0045f,-.0040f),new Vector2(-.0040f,.0105f),new Vector2(.0040f,.0125f),new Vector2(.0045f,-.0040f)},
                new[]{new Vector2(-1,-.15f).normalized,new Vector2(-.6f,.8f),new Vector2(.5f,.866f),new Vector2(1,-.1f).normalized});
            b.Sweep(FrameM,path,n.Select(q=>new Vector3(q.x,q.y,0)).ToArray(),Enumerable.Repeat(Vector3.back,N).ToArray(),j=>rim,true,1);
            // Buckles on the frame's sides, holding the strap.
            foreach(var s in new[]{1f,-1f})
            {
                var a=new Vector3(s*.0790f,-.0110f,-.0060f);var c=new Vector3(s*.0950f,.0110f,.0140f);
                b.Box(FrameM,Vector3.Min(a,c),Vector3.Max(a,c));
            }
            b.Bar(StrapM,StrapPath(8),Vector3.up,StrapSection,0,0);
            // Lens: one pane inside the frame (its edge 1 mm under the band), faced both ways.
            var lens=Enumerable.Range(0,N).Select(k=>o[k]-n[k]*.0080f).ToArray();var tris=Triangulate(lens);
            foreach(var side in new[]{-1f,1f})
            {
                int start=b.v.Count;foreach(var p in lens)b.Add(new Vector3(p.x,p.y,-.0030f+side*.0002f),new Vector3(0,0,side),p*UvPerMetre);
                for(int k=0;k<tris.Count;k+=3)b.Tri(LensM,start+tris[k],start+tris[k+1],start+tris[k+2]);
            }
            return b.Save(MaskPath,"Summer diving mask");
        }
        // Its shadow: the frame, skirt and nose pocket as one block (a 16-point outline at the frame's front, grown round the
        // skirt's bulge and seal), and the strap as a band at 30-degree steps round the back.
        static Mesh MaskShadow()
        {
            var b=new PropMesh(1);
            var o=RoundCorners(new (Vector2 p,float r,int steps)[]{(new Vector2(.081f,.0385f),.024f,2),(new Vector2(.081f,-.0385f),.022f,2),(new Vector2(.035f,-.0395f),0,0),
                (new Vector2(.022f,-.0455f),0,0),(new Vector2(-.022f,-.0455f),0,0),(new Vector2(-.035f,-.0395f),0,0),(new Vector2(-.081f,-.0385f),.022f,2),(new Vector2(-.081f,.0385f),.024f,2)},true);
            var n=Normals(o);
            Vector3[] Ring(float grow,float z)=>Enumerable.Range(0,o.Count).Select(k=>{
                var p=o[k]+n[k]*grow;if(Mathf.Abs(o[k].x)<.036f&&o[k].y<-.039f)p.y=o[k].y-grow*.2f;return new Vector3(p.x,p.y,z);}).ToArray();
            Loft(b,0,new[]{Ring(0,-.0125f),Ring(.0045f,.0260f),Ring(.0075f,.0540f)},true,true);
            b.Bar(0,StrapPath(6),Vector3.up,StrapSection,0,0);
            return b.Save(MaskShadowPath,"Summer diving mask shadow");
        }

        // --- Sand shovel --------------------------------------------------------------------------------------------------
        const float Tilt=12f*Mathf.Deg2Rad;
        // The handle starts inside the back wall and rises towards -x; the grip's oval (centre line A x B) sits so the whole
        // spade is 272 mm long.
        static readonly Vector3 HandleDir=new Vector3(-Mathf.Cos(Tilt),Mathf.Sin(Tilt),0),HandleStart=new Vector3(.1520f,.0085f,0);
        static readonly Vector3 GripCentre=HandleStart+HandleDir*.1249f,GripUp=Vector3.Cross(HandleDir,Vector3.forward);
        const float GripA=.0245f,GripB=.01775f,ScoopBack=.150f;
        static float FloorHalf(Vector2 p)=>Mathf.Lerp(.0016f,.0008f,Mathf.Clamp01((p.x-.235f)/.035f));
        static float WallHeight(float x)=>.016f*Mathf.Clamp01((.232f-x)/.070f);
        // The handle's section (x across, y up): 25 x 9 mm, edges rounded, a round-bottomed groove 9 mm wide along the top.
        static readonly (Vector2[] at,Vector2[] normal) HandleSection=(
            new[]{new Vector2(.0125f,0),new Vector2(.0110f,.0045f),new Vector2(.0055f,.0045f),new Vector2(.0045f,.0020f),new Vector2(-.0045f,.0020f),new Vector2(-.0055f,.0045f),
                new Vector2(-.0110f,.0045f),new Vector2(-.0125f,0),new Vector2(-.0110f,-.0045f),new Vector2(.0110f,-.0045f)},
            new[]{new Vector2(1,0),new Vector2(.5f,.866f),new Vector2(0,1),new Vector2(-.7f,.7f),new Vector2(.7f,.7f),new Vector2(0,1),
                new Vector2(-.5f,.866f),new Vector2(-1,0),new Vector2(-.5f,-.866f),new Vector2(.5f,-.866f)});
        static void GripPath(int count,out Vector3[] path,out Vector3[] side,out Vector3[] up)
        {
            path=new Vector3[count];side=new Vector3[count];up=new Vector3[count];
            for(int k=0;k<count;k++)
            {
                float a=k*2*Mathf.PI/count;path[k]=GripCentre+HandleDir*GripA*Mathf.Cos(a)+Vector3.forward*GripB*Mathf.Sin(a);
                side[k]=(HandleDir*Mathf.Cos(a)/GripA+Vector3.forward*Mathf.Sin(a)/GripB).normalized;up[k]=GripUp;
            }
        }

        static Mesh Shovel()
        {
            var b=new PropMesh(1);
            // Scoop floor: 3.2 mm thick, thinning over its last 35 mm to a slightly pointed front edge; built flat in (x, y)
            // and laid down (its thickness up y).
            int start=b.v.Count;
            var floor=RoundCorners(new (Vector2 p,float r,int steps)[]{(new Vector2(ScoopBack,.055f),.012f,2),(new Vector2(ScoopBack,-.055f),.012f,2),
                (new Vector2(.265f,-.055f),.016f,3),(new Vector2(.272f,0),0,0),(new Vector2(.265f,.055f),.016f,3)},true).ToArray();
            b.Slab(0,i=>0,floor,.0012f,FloorHalf,FloorHalf,1,UvPerMetre);
            for(int i=start;i<b.v.Count;i++){var p=b.v[i];var q=b.n[i];b.v[i]=new Vector3(p.x,p.z,-p.y);b.n[i]=new Vector3(q.x,q.z,-q.y);}
            // Walls along the sides and the back, standing on the floor's edge: 16 mm tall at the back, the sides running
            // out to nothing 40 mm short of the front.
            var plan=RoundCorners(new (Vector2 p,float r,int steps)[]{(new Vector2(.232f,.0534f),0,0),(new Vector2(ScoopBack+.0016f,.0534f),.0104f,2),
                (new Vector2(ScoopBack+.0016f,-.0534f),.0104f,2),(new Vector2(.232f,-.0534f),0,0)},false);
            int count=plan.Count;var wall=new Vector3[count];var outward=new Vector3[count];var sections=new (Vector2[] at,Vector2[] normal)[count];
            for(int i=0;i<count;i++)
            {
                var p=plan[i];var t=plan[Mathf.Min(i+1,count-1)]-plan[Mathf.Max(i-1,0)];var s=new Vector2(t.y,-t.x).normalized;if(Vector2.Dot(s,p-new Vector2(.215f,0))<0)s=-s;
                wall[i]=new Vector3(p.x,2*FloorHalf(p),p.y);outward[i]=new Vector3(s.x,0,s.y);
                float h=Mathf.Max(.0004f,WallHeight(p.x)),th=Mathf.Lerp(.0006f,.0016f,Mathf.Clamp01(WallHeight(p.x)/.004f));
                sections[i]=(new[]{new Vector2(th,-.0010f),new Vector2(th,h-th),new Vector2(0,h),new Vector2(-th,h-th),new Vector2(-th,-.0010f),new Vector2(0,-.0016f)},
                    new[]{Vector2.right,new Vector2(.7f,.7f),Vector2.up,new Vector2(-.7f,.7f),Vector2.left,Vector2.down});
            }
            b.Sweep(0,wall,outward,Enumerable.Repeat(Vector3.up,count).ToArray(),j=>sections[j],false,1);
            // Handle, from inside the back wall into the grip's near side, and the grip round its oval hole.
            b.Bar(0,new List<Vector3>{HandleStart,GripCentre-HandleDir*(GripA-.0020f)},Vector3.forward,t=>HandleSection,0,0);
            GripPath(12,out var gp,out var gs,out var gu);
            b.Sweep(0,gp,gs,gu,j=>RoundedRect(-.006f,.006f,-.0045f,.0045f,.0028f,1),true,2);
            return b.Save(ShovelPath,"Summer sand shovel");
        }
        // Its shadow: the scoop as a wedge (cut across x: full height over the back wall, the side walls' tops running down
        // to the floor, the floor's rounded front), the handle as a bar, the grip as a ring of 10 round its hole.
        static Mesh ShovelShadow()
        {
            var b=new PropMesh(1);
            Vector3[] Cut(float x,float w,float h)=>new[]{new Vector3(x,0,w),new Vector3(x,0,-w),new Vector3(x,h,-w),new Vector3(x,h,w)};
            Loft(b,0,new[]{Cut(ScoopBack,.047f,.0192f),Cut(.158f,.055f,.0192f),Cut(.232f,.055f,.0050f),Cut(.249f,.055f,.0032f),Cut(.267f,.040f,.0030f),Cut(.2725f,.004f,.0016f)},true,true);
            Vector3[] Across(Vector3 c)=>new[]{c+Vector3.forward*.0125f+GripUp*.0045f,c-Vector3.forward*.0125f+GripUp*.0045f,c-Vector3.forward*.0125f-GripUp*.0045f,c+Vector3.forward*.0125f-GripUp*.0045f};
            Loft(b,0,new[]{Across(HandleStart),Across(GripCentre-HandleDir*(GripA-.0020f))},true,true);
            GripPath(10,out var gp,out var gs,out var gu);
            b.Sweep(0,gp,gs,gu,j=>(new[]{new Vector2(.0062f,.0046f),new Vector2(-.0062f,.0046f),new Vector2(-.0062f,-.0046f),new Vector2(.0062f,-.0046f)},
                new[]{new Vector2(1,1).normalized,new Vector2(-1,1).normalized,new Vector2(-1,-1).normalized,new Vector2(1,-1).normalized}),true,1);
            return b.Save(ShovelShadowPath,"Summer sand shovel shadow");
        }

        // --- Sand rake --------------------------------------------------------------------------------------------------------
        // The handle from its rounded end at x = 0 to the crossbar (HeadBack to HeadFront, HeadHalf either side of the handle).
        const float HeadBack=.128f,HeadFront=.146f,HeadHalf=.0425f,TineRoot=.0086f,TineTip=.0076f,ToothLength=.075f;
        // The teeth's heights, 25 mm apart; the outer ones' edges level with the bar's ends.
        static readonly float[] Teeth={.0375f,.0125f,-.0125f,-.0375f};
        // A tooth's centre line, straight along +x from 4 mm inside the bar.
        static List<Vector3> ToothPath(float y)=>new List<Vector3>{new Vector3(HeadFront-.004f,y,0),new Vector3(HeadFront+ToothLength,y,0)};
        // A tine's oval section (x its depth along z, y its width in the rake's plane), tapering along it; corners at 90 and
        // 270 degrees, so the full width shows seen flat.
        static (Vector2[] at,Vector2[] normal) TineSection(float t,int points)
        {
            float w=Mathf.Lerp(TineRoot,TineTip,t),rx=w*.42f,ry=w/2;var at=new Vector2[points];var normal=new Vector2[points];
            for(int k=0;k<points;k++){float a=Mathf.PI/2+k*2*Mathf.PI/points;at[k]=new Vector2(Mathf.Cos(a)*rx,Mathf.Sin(a)*ry);normal[k]=new Vector2(Mathf.Cos(a)/rx,Mathf.Sin(a)/ry).normalized;}
            return (at,normal);
        }

        static Mesh Rake()
        {
            var b=new PropMesh(1);
            // Handle: 9 mm deep, 22 mm wide at its rounded end narrowing to 17 at the head, which it runs into.
            b.Bar(0,new List<Vector3>{new Vector3(.011f,0,0),new Vector3(HeadBack+.003f,0,0)},Vector3.forward,t=>{
                float h=Mathf.Lerp(.011f,.0085f,t);return RoundedRect(-.0045f,.0045f,-h,h,.003f,1);},.011f,0,3);
            // Crossbar: 10 mm thick, 18 x 85 mm, corners rounded.
            var head=RoundCorners(new (Vector2 p,float r,int steps)[]{(new Vector2(HeadBack,HeadHalf),.005f,2),(new Vector2(HeadBack,-HeadHalf),.005f,2),
                (new Vector2(HeadFront,-HeadHalf),.005f,2),(new Vector2(HeadFront,HeadHalf),.005f,2)},true).ToArray();
            b.Slab(0,i=>0,head,.0025f,p=>.0050f,p=>0,1,UvPerMetre);
            // Teeth, their roots in the bar, rounded tips.
            foreach(var y in Teeth)b.Bar(0,ToothPath(y),Vector3.forward,t=>TineSection(t,6),0,TineTip/2,2);
            return b.Save(RakePath,"Summer sand rake");
        }
        // Its shadow: the handle cut across x (round its end in two steps), the bar as a plain block, the teeth at full
        // width (a diamond section), each ending in a short point.
        static Mesh RakeShadow()
        {
            var b=new PropMesh(1);
            Vector3[] Cut(float x,float h,float d)=>new[]{new Vector3(x,h,d),new Vector3(x,-h,d),new Vector3(x,-h,-d),new Vector3(x,h,-d)};
            Loft(b,0,new[]{Cut(.0002f,.0050f,.0030f),Cut(.0032f,.0083f,.0045f),Cut(.0110f,.0114f,.0046f),Cut(HeadBack+.002f,.0088f,.0046f)},true,true);
            Loft(b,0,new[]{Cut(HeadBack,HeadHalf-.0005f,.0050f),Cut(HeadFront,HeadHalf-.0005f,.0050f)},true,true);
            foreach(var y in Teeth)b.Bar(0,ToothPath(y),Vector3.forward,t=>{float w=Mathf.Lerp(TineRoot,TineTip,t);return Ellipse(w*.42f,w/2,4);},0,TineTip/2,1);
            return b.Save(RakeShadowPath,"Summer sand rake shadow");
        }

        // --- Textures (detail colours kept at about 0.6 or below: the gym's light lifts lit faces about 1.5x) ------------------
        // Deep blue silicone ribbed across the strap (u runs along it: 64 ribs, about 4 mm apart).
        static Texture2D StrapTexture()
        {
            const int W=512,H=8;var tex=new Texture2D(W,H,TextureFormat.RGB24,false,false);var blue=new Color(.07f,.18f,.36f);
            for(int y=0;y<H;y++)for(int x=0;x<W;x++)
            {
                float u=(x+.5f)/W,rib=.5f+.5f*Mathf.Cos(u*64*2*Mathf.PI);
                var c=blue*Mathf.Lerp(.70f,1.05f,Mathf.Pow(rib,.7f))*(.96f+.07f*Fbm(u,y/(float)H,16,1,2,131));c.a=1;tex.SetPixel(x,y,c);
            }
            return SaveTexture(StrapTexPath,tex,false);
        }
        // Golden yellow moulded plastic, faintly mottled.
        static Texture2D ShovelTexture()
        {
            const int S=128;var tex=new Texture2D(S,S,TextureFormat.RGB24,false,false);var gold=new Color(.62f,.46f,.05f);
            for(int y=0;y<S;y++)for(int x=0;x<S;x++)
            {
                float u=x/(float)S,v=y/(float)S;var c=gold*(.94f+.08f*Fbm(u,v,4,4,3,141));c.a=1;tex.SetPixel(x,y,c);
            }
            return SaveTexture(ShovelTexPath,tex,false);
        }
    }
}
