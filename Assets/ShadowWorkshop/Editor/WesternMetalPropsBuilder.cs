using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static ShadowWorkshopIntegration.Editor.PropMesh;

namespace ShadowWorkshopIntegration.Editor
{
    // Western metal props for the WildWest level (2026-11-16, from the user's pictures): a gold pocket watch with a cream
    // enamel dial and a bow on top, a rimmed rifle cartridge (one prop per round; first a pair in one mesh), and a silver
    // rodeo trophy buckle with a rope border, bright scrollwork on an antiqued plate and a raised gold longhorn steer
    // skull (the user, 2026-11-16: the first version's gold star in a wreath looked too much like a national flag; generic:
    // no words or marks). Built like the level's other props: procedural meshes at their own size (metres, +y up, -z the
    // front), smooth normals doing the rounding, the detail painted into textures generated here (the dial, the cartridge
    // brass, the lead, the buckle's scrollwork and its rope twist). Every look sits near the top of the mobile VR budget
    // (about 950, 1100 and 1700 triangles over two to four materials), so each casts its shadow from a light closed proxy
    // of the same outline built here (48 to 62 triangles, for WildWest's shadow budget; within about 1-2 mm of the look's
    // outline, the bow, crown details, rope twist, skull relief and belt hardware left out). Placing them in the level is
    // WildWestBuilder's job; this file only builds the props.
    public static class WesternMetalPropsBuilder
    {
        const string Art="Assets/ShadowWorkshop/Art/Torch2/",Meshes=Art+"Meshes/",Textures=Art+"Textures/",MaterialFolder=Art+"Materials/";
        const string WatchPath=Meshes+"Pocket watch.asset",WatchShadowPath=Meshes+"Pocket watch shadow.asset";
        const string CartridgePath=Meshes+"Cartridge.asset",CartridgeShadowPath=Meshes+"Cartridge shadow.asset";
        const string BucklePath=Meshes+"Rodeo buckle.asset",BuckleShadowPath=Meshes+"Rodeo buckle shadow.asset";
        const string DialPath=Textures+"watch dial.png",BrassPath=Textures+"cartridge brass.png",LeadPath=Textures+"bullet lead.png";
        const string PlatePath=Textures+"rodeo buckle plate.png",RopePath=Textures+"rodeo buckle rope.png",RopeNormalPath=Textures+"rodeo buckle rope normal.png";
        static Texture2D Load(string path)=>AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        // The level's gold (WildWestBuilder's star and buckle trim): found by name, created with these values only if missing.
        static readonly Look Gold=new Look("western gold",new Color(.93f,.72f,.30f),.85f,.70f);
        static readonly Look Dark=new Look("western dark",new Color(.05f,.04f,.035f),0f,.20f);   // WildWestBuilder's, likewise

        // mesh: the drawn look; shadow: the mesh that casts the shadow (a light proxy in the same outline).
        public sealed class Prop{public string label,mesh,shadow;public System.Func<Mesh> build,buildShadow;public System.Func<Material[]> materials;}
        public static readonly Prop[] Props={
            new Prop{label="POCKET WATCH",mesh=WatchPath,shadow=WatchShadowPath,build=()=>Watch(false),buildShadow=()=>Watch(true),materials=()=>PropMesh.Materials(MaterialFolder,new[]{
                Gold,new Look("watch dial",Color.white,0f,.72f){baseMap=()=>Load(DialPath)??Dial()}})},
            new Prop{label="CARTRIDGE",mesh=CartridgePath,shadow=CartridgeShadowPath,build=()=>Cartridge(false),buildShadow=()=>Cartridge(true),materials=()=>PropMesh.Materials(MaterialFolder,new[]{
                new Look("cartridge brass",Color.white,.90f,.72f){baseMap=()=>Load(BrassPath)??Brass()},new Look("bullet lead",Color.white,.25f,.28f){baseMap=()=>Load(LeadPath)??Lead()}})},
            // The silver carries the scrollwork (WildWestBuilder's plain "buckle silver" has no texture), the rope its twist;
            // the skull and horns gold, its eye sockets and nostrils dark.
            new Prop{label="RODEO BUCKLE",mesh=BucklePath,shadow=BuckleShadowPath,build=()=>Buckle(false),buildShadow=()=>Buckle(true),materials=()=>PropMesh.Materials(MaterialFolder,new[]{
                new Look("rodeo buckle silver",Color.white,.90f,.75f){baseMap=()=>Load(PlatePath)??Plate()},
                new Look("rodeo buckle rope",Color.white,.90f,.70f){baseMap=()=>Load(RopePath)??Rope(false),normalMap=()=>Load(RopeNormalPath)??Rope(true),bumpScale=1f},Gold,Dark})}};
        public static Mesh MeshOf(Prop p)=>AssetDatabase.LoadAssetAtPath<Mesh>(p.mesh);
        public static Mesh ShadowOf(Prop p)=>AssetDatabase.LoadAssetAtPath<Mesh>(p.shadow??p.mesh);

        [MenuItem("MetaHackthone/Shadow Workshop/Build Western metal props")]
        public static void Build()
        {
            Dial();Brass();Lead();Plate();Rope(false);Rope(true);
            var built=Props.Select(p=>
            {
                var m=p.build();var s=p.buildShadow();p.materials();
                var looks=string.Join("+",Enumerable.Range(0,m.subMeshCount).Select(i=>(m.GetTriangles(i).Length/3).ToString()));
                return $"{m.name} {m.triangles.Length/3} ({looks}), {m.bounds.size*1000:F1} mm, shadow {s.triangles.Length/3}";
            }).ToArray();
            Debug.Log("WESTERN_METAL_PROPS_OK "+string.Join("; ",built));
        }

        const float Mm=.001f;
        static Vector2 V(float r,float h)=>new Vector2(r,h)*Mm;   // profile points in millimetres: radius, height along the axis

        // --- Pocket watch: a gold case 50 mm across and 13 mm thick, its axis along z with the dial to the front (-z), the
        // pendant, knurled crown and bow on top (+y), the bow looping over the crown in the dial's plane. ----------------
        const float DialH=3.8f,DialR=20.2f,DialSeen=19.95f;   // the dial's height in front of the case's middle, its disc's radius, what the bezel leaves showing
        static Mesh Watch(bool shadow)
        {
            var b=new PropMesh(shadow?1:2);const int GoldM=0,DialM=1;var front=Vector3.back;
            if(shadow)
            {
                // The case as a nine-sided closed lathe (flat back, widest at its edge, flat front at the bezel's crest), and
                // the pendant and crown as a closed triangular prism, flat face to the front; the bow is left out (62
                // triangles; level budget).
                b.SmoothLathe(0,Vector3.zero,front,9,Straddled(9,V(0,-5.9f),V(22.5f,-5.9f),V(25,0),V(22,6.0f),V(0,6.0f)));
                Prism(b,0,4.6f,21f,33.9f);
                return b.Save(WatchShadowPath,"Pocket watch shadow");
            }
            // Case: from the middle of the back (gently domed) out round the edge, a ledge where the bezel snaps on (creased),
            // over the bezel's round crest and down its inner wall to the dial.
            b.SmoothLathe(GoldM,Vector3.zero,front,24,new[]{V(0,-6.9f),V(20.5f,-5.5f),V(24.0f,-3.6f),V(25.0f,-1.0f),V(24.6f,1.6f),V(24.0f,2.5f),V(23.5f,2.6f),
                V(23.3f,3.2f),V(22.6f,5.1f),V(21.6f,6.05f),V(20.5f,5.9f),V(DialSeen,4.7f),V(DialSeen,DialH)},5,6);
            // Dial: a flat disc reaching just under the bezel's wall, the enamel painted on with planar UVs (u right, v up
            // as seen from the front).
            const int DialSides=24;int hub=b.Add(new Vector3(0,0,-DialH*Mm),Vector3.back,new Vector2(.5f,.5f));
            for(int k=0;k<=DialSides;k++)
            {
                float a=k*2*Mathf.PI/DialSides;var p=new Vector2(Mathf.Cos(a),Mathf.Sin(a))*DialR;
                b.Add(new Vector3(p.x,p.y,-DialH)*Mm,Vector3.back,new Vector2(.5f+p.x/(2*DialR),.5f+p.y/(2*DialR)));
            }
            for(int k=0;k<DialSides;k++)b.Tri(DialM,hub,hub+1+k,hub+2+k);
            // Pendant: a neck out of the case's top and a collar the bow's ends pivot in; the crown, knurled, with a domed
            // top; the bow, round wire 2.5 mm thick.
            b.SmoothLathe(GoldM,Vector3.zero,Vector3.up,12,new[]{V(2.6f,22.5f),V(2.6f,25.7f),V(3.5f,26.5f),V(3.3f,27.9f),V(3.0f,28.6f),V(4.3f,28.6f)},4);
            b.Knurl(GoldM,Vector3.zero,Vector3.up,28.6f*Mm,33.0f*Mm,3.95f*Mm,4.3f*Mm,12,false);
            b.SmoothLathe(GoldM,Vector3.zero,Vector3.up,12,new[]{V(4.3f,33.0f),V(3.8f,33.6f),V(0,33.85f)});
            b.Bar(GoldM,Bow(),Vector3.forward,t=>Ellipse(1.25f*Mm,1.25f*Mm,5),0,0);
            return b.Save(WatchPath,"Pocket watch");
        }
        // The bow's centre line in the dial's plane: from inside the collar's left side, up round over the crown, and into
        // the collar's right side (the ends stay buried). A smooth curve through the shape's points, then points evenly
        // spaced along it (19), so the loop reads round on few segments.
        static List<Vector3> Bow()
        {
            var side=new[]{V(2.2f,26.9f),V(5.9f,27.9f),V(8.5f,30.9f),V(9.2f,35.3f),V(7.6f,39.8f),V(4.0f,42.6f)};
            var shape=side.Select(p=>new Vector3(-p.x,p.y,0)).ToList();shape.Add(new Vector3(0,43.4f*Mm,0));
            for(int i=side.Length-1;i>=0;i--)shape.Add(new Vector3(side[i].x,side[i].y,0));
            return Even(Curve(shape,8),19);
        }
        // Shadow proxies: a profile with its radii pushed out so a polygon of that many sides straddles the true circle
        // (corners as far out as edges in), halving the outline's error.
        static Vector2[] Straddled(int sides,params Vector2[] profile)
        {
            float grow=2/(1+Mathf.Cos(Mathf.PI/sides));return profile.Select(p=>new Vector2(p.x*grow,p.y)).ToArray();
        }
        // A closed upright triangular prism round the y axis, corners r mm out (one to the back, +z, so the front face is
        // flat), from y0 to y1 mm: 8 triangles.
        static void Prism(PropMesh b,int m,float r,float y0,float y1)
        {
            var corner=new Vector2[3];for(int i=0;i<3;i++){float a=(90+i*120)*Mathf.Deg2Rad;corner[i]=new Vector2(Mathf.Cos(a),Mathf.Sin(a))*r;}
            Vector3 At(Vector2 c,float y)=>new Vector3(c.x,y,c.y)*Mm;
            foreach(var (y,n) in new[]{(y0,Vector3.down),(y1,Vector3.up)})b.Tri(m,b.Add(At(corner[0],y),n,Vector2.zero),b.Add(At(corner[1],y),n,Vector2.zero),b.Add(At(corner[2],y),n,Vector2.zero));
            for(int i=0;i<3;i++)
            {
                var c0=corner[i];var c1=corner[(i+1)%3];var n=new Vector3(c0.x+c1.x,0,c0.y+c1.y).normalized;
                b.Quad(m,b.Add(At(c0,y0),n,Vector2.zero),b.Add(At(c1,y0),n,Vector2.zero),b.Add(At(c1,y1),n,Vector2.zero),b.Add(At(c0,y1),n,Vector2.zero));
            }
        }
        // count points evenly spaced along a polyline, both ends included.
        static List<Vector3> Even(IList<Vector3> line,int count)
        {
            var along=new float[line.Count];for(int i=1;i<line.Count;i++)along[i]=along[i-1]+Vector3.Distance(line[i-1],line[i]);
            int j=0;var list=new List<Vector3>();
            for(int k=0;k<count;k++)
            {
                float s=along[line.Count-1]*k/(count-1);while(j<line.Count-2&&along[j+1]<s)j++;
                list.Add(Vector3.Lerp(line[j],line[j+1],Mathf.InverseLerp(along[j],along[j+1],s)));
            }
            return list;
        }

        // --- Cartridge: one rimmed rifle round, 61.9 mm long and 12 mm across the rim, its axis along x (bullet to +x) and
        // its frame centred on it (the middle of its length, on the axis). First built as a pair in one mesh; the user then
        // lays them out one by one (2026-11-17: "子弹给我拆成一颗一颗"), so PairOffsets keeps where each round sat in the
        // pair's frame. -------------------------------------------------------------------------------------------------
        // The brass case from the middle of the base out over the rim, along the body, in at the shoulder and down the neck
        // to the mouth; the lead bullet from inside the neck to its round nose. Millimetres: radius, length from the base.
        static readonly Vector2[] CaseProfile={V(0,0),V(5.6f,0),V(6.0f,.35f),V(6.0f,1.15f),V(5.65f,1.5f),V(5.3f,1.5f),V(5.08f,36.8f),V(4.95f,38.6f),
            V(4.45f,40.6f),V(4.15f,41.6f),V(4.12f,42.2f),V(4.12f,49.0f),V(3.92f,49.3f)};
        static readonly Vector2[] BulletProfile={V(3.92f,48.6f),V(3.92f,50.4f),V(3.78f,53.6f),V(3.3f,56.8f),V(2.5f,59.4f),V(1.4f,61.2f),V(0,61.9f)};
        // Shadow: a closed six-sided lathe, its base flat, tapering from the rim to the shoulder, then a straight point over
        // the neck and the bullet's nose (24 triangles; level budget).
        static readonly Vector2[] RoundShadow=Straddled(6,V(0,0),V(5.7f,0),V(4.6f,44f),V(0,63f));
        const int Shoulder=6;   // CaseProfile's index where the shoulder begins (the anneal stain's start)
        const float CartridgeHalf=30.95f;   // half the round's length, base to bullet tip (mm): its frame's origin
        static Mesh Cartridge(bool shadow)
        {
            const int BrassM=0,LeadM=1;var b=new PropMesh(shadow?1:2);var basePoint=new Vector3(-CartridgeHalf,0,0)*Mm;
            if(shadow)b.SmoothLathe(0,basePoint,Vector3.right,6,RoundShadow);
            else
            {
                b.SmoothLathe(BrassM,basePoint,Vector3.right,16,CaseProfile,1,4,5);
                // v from 0 at the middle of the base to 1 at the mouth, so the brass texture's bands land on the profile.
                float caseArc=Arc(CaseProfile);for(int i=0;i<b.v.Count;i++)b.uv[i]=new Vector2(b.uv[i].x,b.uv[i].y/(caseArc*UvPerMetre));
                b.SmoothLathe(LeadM,basePoint,Vector3.right,16,BulletProfile);
            }
            return b.Save(shadow?CartridgeShadowPath:CartridgePath,shadow?"Cartridge shadow":"Cartridge");
        }
        // Where each round sat in the earlier pair's frame (both lying along x on y = 0, bullets to +x, the front one at
        // z -5.8 mm, the back one at z +5.8 mm and 12 mm ahead; each resting on its rim and shoulder, so dipping 1.4
        // degrees nose-down): the single round's frame placed there. The pair's mesh was exactly these two rounds.
        public static readonly Pose[] PairOffsets=PairPoses();
        static Pose[] PairPoses()
        {
            float dip=Mathf.Asin((6.0f-5.08f)/36.8f);var axis=new Vector3(Mathf.Cos(dip),-Mathf.Sin(dip),0);var turn=Quaternion.AngleAxis(-dip*Mathf.Rad2Deg,Vector3.forward);
            return new[]{new Vector3(-37f,6.0f,-5.8f),new Vector3(-25f,6.0f,5.8f)}.Select(basePoint=>new Pose((basePoint+axis*CartridgeHalf)*Mm,turn)).ToArray();
        }
        static float Arc(IList<Vector2> profile){float s=0;for(int i=1;i<profile.Count;i++)s+=Vector2.Distance(profile[i-1],profile[i]);return s;}

        // --- Rodeo buckle: an oval trophy buckle 90 x 68 mm, the body 6 mm thick and gently domed, facing -z (+y up). A
        // round rope border on its rim (the twist in its texture and normal map: modelled, 30 twists would cost ~1700
        // triangles), bright scrollwork on an antiqued plate inside it, a gold longhorn steer skull in relief in the middle
        // (the face 38 mm long, the horns 55 mm tip to tip, standing up to 7 mm off the plate), and behind (+z) a wire belt
        // bar at the left end and a stud hook at the right. ------------------------------------------------------------
        const float BuckleA=45f,BuckleB=34f,BuckleK=BuckleB/BuckleA;   // half width, half height (mm); the oval's squash
        static readonly Vector2 PlainUv=new Vector2(.01f,.01f);         // a corner of the plate texture: plain polished silver
        static Vector2 PlateUv(Vector3 p)=>new Vector2(.5f+p.x/(2*BuckleA*Mm),.5f+p.y/(2*BuckleB*Mm));
        static Mesh Buckle(bool shadow)
        {
            const int SilverM=0,RopeM=1,GoldM=2,DarkM=3;var b=new PropMesh(shadow?1:4);
            if(shadow)
            {
                // One closed twelve-sided oval: a flat back, the edge, the rope's top, and a low cone over the dome and the
                // skull; the bar, stud and skull relief are left out (48 triangles; level budget).
                Oval(b,0,12,Straddled(12,V(0,-2.8f),V(45,-2.1f),V(43.5f,4.0f),V(0,5.6f)),false);
                return b.Save(BuckleShadowPath,"Rodeo buckle shadow");
            }
            // Body: the plain back (on the texture's plain corner) out round the edge; then the front, planar-mapped: the
            // polished rim the rope sits on and the engraved plate, rising to a low dome in the middle.
            Oval(b,SilverM,32,new[]{V(0,-2.8f),V(42.5f,-2.8f),V(44.4f,-2.3f),V(45,-1.2f),V(45,0)},false,1);
            Oval(b,SilverM,32,new[]{V(45,0),V(45,.5f),V(44.2f,1.1f),V(41,1.25f),V(38.8f,1.45f),V(24,2.6f),V(0,3.1f)},true);
            // Rope: a round strand 4.8 mm thick on the rim, its centre 4 mm in from the edge (at the oval's ends), 44 points
            // evenly spaced round, 30 twists in the texture.
            var path=OvalPath(41,2.3f,44);var outward=new Vector3[path.Count];var up=new Vector3[path.Count];
            for(int i=0;i<path.Count;i++){var p=path[i];outward[i]=new Vector3(p.x/(BuckleA*BuckleA),p.y/(BuckleB*BuckleB),0).normalized;up[i]=Vector3.back;}
            b.Sweep(RopeM,path.ToArray(),outward,up,j=>Ellipse(2.4f*Mm,2.4f*Mm,5),true,30);
            Skull(b,GoldM,DarkM);Horn(b,GoldM,1);Horn(b,GoldM,-1);
            BeltBar(b,SilverM);Stud(b,SilverM);
            return b.Save(BucklePath,"Rodeo buckle");
        }
        // The plate's height in front of the body's middle at oval radius rho (mm, in units of the half width): the front
        // profile's dome.
        static float PlateH(float rho)=>rho>24?Mathf.Lerp(2.6f,1.45f,(rho-24)/14.8f):Mathf.Lerp(3.1f,2.6f,rho/24);

        // The steer skull, facing front: rows from the poll (top, where the horns join) down the face to the muzzle, each
        // a half oval across (y, half width, height above the skull's base; mm): broad at the brow round the eyes, long and
        // narrow down the face, a little wider again at the muzzle. Its base follows the plate's dome, sunk half a millimetre.
        static readonly Vector3[] SkullRows={new Vector3(12.5f,8.5f,2.0f),new Vector3(10.5f,10.2f,3.6f),new Vector3(7.0f,10.6f,4.4f),new Vector3(4.0f,10.8f,4.6f),
            new Vector3(1.0f,8.0f,4.5f),new Vector3(-2.5f,6.0f,4.2f),new Vector3(-7,5.6f,3.9f),new Vector3(-12,5.3f,3.7f),new Vector3(-16.5f,5.4f,3.5f),
            new Vector3(-20.5f,6.0f,3.3f),new Vector3(-23.2f,5.2f,2.6f),new Vector3(-24.9f,3.0f,1.4f)};
        static float SkullBase(float y)=>PlateH(Mathf.Abs(y)/BuckleK)-.45f;
        static Vector3 SkullRow(float t){int i=Mathf.Clamp(Mathf.FloorToInt(t),0,SkullRows.Length-2);return Vector3.Lerp(SkullRows[i],SkullRows[i+1],t-i);}
        // The face at row t (0 the poll .. last the muzzle, fractional) and angle a across (0 its right edge, pi its left).
        static Vector3 SkullAt(float t,float a)
        {
            var r=SkullRow(t);return new Vector3(Mathf.Cos(a)*r.y,r.x,-(SkullBase(r.x)+Mathf.Pow(Mathf.Max(0,Mathf.Sin(a)),.8f)*r.z))*Mm;
        }
        static Vector3 SkullNormal(float t,float a)
        {
            const float e=.01f;var n=Vector3.Cross((SkullAt(t,a+e)-SkullAt(t,a-e))*1000,(SkullAt(t+e,a)-SkullAt(t-e,a))*1000).normalized;
            var p=SkullAt(t,a);var axis=new Vector3(0,p.y,-SkullBase(p.y/Mm)*Mm);if(Vector3.Dot(n,p-axis)<0)n=-n;return n;
        }
        // The face over (x, y) mm, and its normal there.
        static Vector3 SkullOn(Vector2 at,out Vector3 n)
        {
            float t=0;
            for(int i=0;i+1<SkullRows.Length;i++)if(at.y<=SkullRows[i].x&&at.y>=SkullRows[i+1].x){t=i+(SkullRows[i].x-at.y)/(SkullRows[i].x-SkullRows[i+1].x);break;}
            float a=Mathf.Acos(Mathf.Clamp(at.x/SkullRow(t).y,-1,1));n=SkullNormal(t,a);return SkullAt(t,a);
        }
        static void Skull(PropMesh b,int gold,int dark)
        {
            const int Across=8;int rows=SkullRows.Length;var grid=new int[rows,Across+1];
            for(int t=0;t<rows;t++)for(int k=0;k<=Across;k++){float a=k*Mathf.PI/Across;grid[t,k]=b.Add(SkullAt(t,a),SkullNormal(t,a),Vector2.zero);}
            for(int t=0;t+1<rows;t++)for(int k=0;k<Across;k++)b.Quad(gold,grid[t,k],grid[t+1,k],grid[t+1,k+1],grid[t,k+1]);
            // The poll and the muzzle rounded off: the end rows fanned to a point just past them.
            foreach(var (t,y,lift,n) in new[]{(0,13.4f,1.0f,new Vector3(0,1,-.6f)),(rows-1,-25.6f,.6f,new Vector3(0,-1,-.6f))})
            {
                int hub=b.Add(new Vector3(0,y,-(SkullBase(y)+lift))*Mm,n.normalized,Vector2.zero);
                for(int k=0;k<Across;k++)b.Tri(gold,hub,grid[t,k],grid[t,k+1]);
            }
            // Eye sockets, narrow and slanting up to their outer corners, and the nostrils: dark ovals lying on the face.
            foreach(var sx in new[]{1f,-1f}){Socket(b,dark,new Vector2(6.3f*sx,4.6f),2.7f,1.3f,22*sx,10);Socket(b,dark,new Vector2(2.0f*sx,-21.8f),.85f,1.4f,18*sx,8);}
        }
        // A dark oval lying on the skull's face round (x, y) mm: radii rx, ry, turned deg, 0.15 mm proud of the face.
        static void Socket(PropMesh b,int m,Vector2 at,float rx,float ry,float deg,int count)
        {
            const float Lift=.00015f;var c=SkullOn(at,out var n);int hub=b.Add(c+n*Lift,n,Vector2.zero),ring=b.v.Count;
            for(int k=0;k<count;k++)
            {
                float a=k*2*Mathf.PI/count;var q=SkullOn(at+Turn(new Vector2(Mathf.Cos(a)*rx,Mathf.Sin(a)*ry),deg),out var nq);b.Add(q+nq*Lift,n,Vector2.zero);
            }
            for(int k=0;k<count;k++)b.Tri(m,hub,ring+k,ring+(k+1)%count);
        }
        // A horn (side 1 right, -1 left) from inside the poll out sideways, dipping a little and turning up at its tip,
        // flattened and lying half sunk in the plate; both ends rounded closed.
        static void Horn(PropMesh b,int m,float side)
        {
            var shape=new[]{new Vector2(4.0f,10.8f),new Vector2(8.0f,11.6f),new Vector2(12.0f,11.8f),new Vector2(16.5f,11.2f),new Vector2(20.5f,10.8f),
                new Vector2(23.6f,11.4f),new Vector2(25.9f,13.2f),new Vector2(26.8f,15.6f)};
            float R(float t)=>Mathf.Lerp(3.2f,.7f,Mathf.Pow(t,1.3f));
            var pts=Even(Curve(shape.Select(p=>new Vector3(p.x*side,p.y,0)).ToList(),6),11);
            for(int i=0;i<pts.Count;i++)
            {
                var p=pts[i];float rho=Mathf.Sqrt(p.x*p.x+p.y*p.y/(BuckleK*BuckleK));pts[i]=new Vector3(p.x,p.y,-(PlateH(rho)+.35f*R(i/(pts.Count-1f))))*Mm;
            }
            b.Bar(m,pts,Vector3.back,t=>Ellipse(.75f*R(t)*Mm,R(t)*Mm,6),1.5f*Mm,1.0f*Mm,2);
        }
        // A surface of revolution round z (profile radii in units of the half width) squashed in y to the buckle's oval,
        // its normals with it; UVs planar on the plate texture, or all on its plain corner.
        static void Oval(PropMesh b,int m,int sides,Vector2[] profile,bool planar,params int[] creases)
        {
            int start=b.v.Count;b.SmoothLathe(m,Vector3.zero,Vector3.back,sides,profile,creases);
            for(int i=start;i<b.v.Count;i++)
            {
                var p=b.v[i];var n=b.n[i];b.v[i]=new Vector3(p.x,p.y*BuckleK,p.z);b.n[i]=new Vector3(n.x,n.y/BuckleK,n.z).normalized;
                b.uv[i]=planar?PlateUv(b.v[i]):PlainUv;
            }
        }
        // count points evenly spaced round the oval of half width r (mm, squashed like the body), h mm in front.
        static List<Vector3> OvalPath(float r,float h,int count)
        {
            var dense=new List<Vector3>();
            for(int i=0;i<=720;i++){float t=i*2*Mathf.PI/720;dense.Add(new Vector3(Mathf.Cos(t)*r,Mathf.Sin(t)*r*BuckleK,-h)*Mm);}
            var list=Even(dense,count+1);list.RemoveAt(count);return list;
        }
        // Behind, near the left end: a wire loop 22 mm long standing 4.4 mm off the back, its ends in the body.
        static void BeltBar(PropMesh b,int m)
        {
            var corners=new List<(Vector2 p,float r,int steps)>{(new Vector2(-11,2.2f),0f,0),(new Vector2(-11,7.2f),2.6f,2),(new Vector2(11,7.2f),2.6f,2),(new Vector2(11,2.2f),0f,0)};
            var path=RoundCorners(corners,false).Select(q=>new Vector3(-33,q.x,q.y)*Mm).ToList();
            int start=b.v.Count;b.Bar(m,path,Vector3.right,t=>Ellipse(1.4f*Mm,1.4f*Mm,5),0,0);
            for(int i=start;i<b.v.Count;i++)b.uv[i]=PlainUv;
        }
        // Behind, near the right end: a stud hook, a stem with a round head 6 mm across.
        static void Stud(PropMesh b,int m)
        {
            int start=b.v.Count;var at=new Vector3(33,0,2.8f)*Mm;
            b.SmoothLathe(m,at,Vector3.forward,8,new[]{V(1.6f,-.6f),V(1.6f,3.0f),V(3.0f,3.3f),V(3.0f,4.3f),V(2.2f,4.9f),V(0,5.0f)},1);
            for(int i=start;i<b.v.Count;i++)b.uv[i]=PlainUv;
        }

        // --- Textures --------------------------------------------------------------------------------------------------
        // The enamel dial, 512 px across the disc (planar: u right, v up as seen from the front): cream with a faint mottle
        // and the bezel's shade at its edge, a railway minute track, radial Roman numerals in plain strokes with bars top and
        // bottom (VI left out for the seconds subdial at six), blued Breguet hands at 10:09 casting a soft shadow, a gold pin.
        static Texture2D Dial()
        {
            const int S=512;const float PerMm=S/(2*DialR);
            var print=new float[S*S];var hands=new float[S*S];
            Vector2 At(int x,int y)=>new Vector2((x+.5f)/PerMm-DialR,(y+.5f)/PerMm-DialR);
            // Coverage, antialiased over a pixel, where distance(p) < 0, over the box lo..hi (mm).
            void Paint(float[] layer,Vector2 lo,Vector2 hi,System.Func<Vector2,float> distance)
            {
                int x0=Mathf.Max(0,Mathf.FloorToInt((lo.x+DialR)*PerMm)-2),x1=Mathf.Min(S-1,Mathf.CeilToInt((hi.x+DialR)*PerMm)+2);
                int y0=Mathf.Max(0,Mathf.FloorToInt((lo.y+DialR)*PerMm)-2),y1=Mathf.Min(S-1,Mathf.CeilToInt((hi.y+DialR)*PerMm)+2);
                for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++){float c=Mathf.Clamp01(.5f-distance(At(x,y))*PerMm);if(c>layer[y*S+x])layer[y*S+x]=c;}
            }
            // A stroke from a to c, w0 wide at a tapering to w1 at c, with round ends.
            void Line(float[] layer,Vector2 a,Vector2 c,float w0,float w1)
            {
                var ac=c-a;var pad=Vector2.one*Mathf.Max(w0,w1);
                Paint(layer,Vector2.Min(a,c)-pad,Vector2.Max(a,c)+pad,p=>{float t=Mathf.Clamp01(Vector2.Dot(p-a,ac)/ac.sqrMagnitude);return Vector2.Distance(p,a+ac*t)-Mathf.Lerp(w0,w1,t)/2;});
            }
            void Ring(float[] layer,Vector2 c,float r,float w)=>Paint(layer,c-Vector2.one*(r+w),c+Vector2.one*(r+w),p=>Mathf.Abs(Vector2.Distance(p,c)-r)-w/2);
            void Disc(float[] layer,Vector2 c,float r)=>Paint(layer,c-Vector2.one*r,c+Vector2.one*r,p=>Vector2.Distance(p,c)-r);
            Vector2 Dir(float deg)=>new Vector2(Mathf.Sin(deg*Mathf.Deg2Rad),Mathf.Cos(deg*Mathf.Deg2Rad));   // clockwise from twelve

            // Railway minute track: two rings, sixty ticks, the five-minute ones bolder and longer.
            Ring(print,Vector2.zero,17.7f,.12f);Ring(print,Vector2.zero,18.9f,.12f);
            for(int m=0;m<60;m++){var d=Dir(m*6);bool five=m%5==0;Line(print,d*(five?17.2f:17.7f),d*18.9f,five?.34f:.13f,five?.34f:.13f);}
            // Numerals, radial (their feet towards the middle), H tall round radius NumR: I a thick upright; V a thick
            // down-stroke and a thin up-stroke; X a thick and a thin diagonal; a thin bar along the top and the bottom.
            const float H=3.5f,NumR=14.4f,Thick=.13f*H,Thin=.055f*H;
            string[] numerals={"XII","I","II","III","IIII","V",null,"VII","VIII","IX","X","XI"};
            for(int hour=0;hour<12;hour++)
            {
                var s=numerals[hour];if(s==null)continue;
                var up=Dir(hour*30);var right=new Vector2(up.y,-up.x);var centre=up*NumR;
                Vector2 On(float lx,float ly)=>centre+right*lx+up*ly;
                float width=s.Sum(ch=>ch=='I'?.30f:.62f)*H,x=-width/2;
                foreach(var ch in s)
                {
                    float adv=(ch=='I'?.30f:.62f)*H,cx=x+adv/2;x+=adv;
                    if(ch=='I')Line(print,On(cx,-H/2),On(cx,H/2),Thick,Thick);
                    else if(ch=='V'){Line(print,On(cx-.27f*H,H/2),On(cx,-H/2),Thick,Thick);Line(print,On(cx,-H/2),On(cx+.27f*H,H/2),Thin,Thin);}
                    else{Line(print,On(cx-.25f*H,H/2),On(cx+.25f*H,-H/2),Thick,Thick);Line(print,On(cx-.25f*H,-H/2),On(cx+.25f*H,H/2),Thin,Thin);}
                }
                foreach(var ly in new[]{H/2,-H/2})Line(print,On(-width/2+.02f*H,ly),On(width/2-.02f*H,ly),Thin,Thin);
            }
            // Seconds subdial at six: two rings, sixty ticks with a bolder one every five, its own small hand.
            var sub=new Vector2(0,-7.6f);
            Ring(print,sub,4.4f,.11f);Ring(print,sub,3.55f,.09f);
            for(int k=0;k<60;k++){var d=Dir(k*6);bool five=k%5==0;Line(print,sub+d*(five?3.1f:3.55f),sub+d*4.4f,five?.20f:.08f,five?.20f:.08f);}
            var sd=Dir(200);Line(hands,sub-sd*1.0f,sub+sd*3.9f,.16f,.05f);Disc(hands,sub,.45f);
            // Hour and minute hands, Breguet style: a tapering shaft with a short tail, a hollow ring short of the tip, a point.
            void Hand(float deg,float tail,float ringAt,float ringR,float tip,float w)
            {
                var d=Dir(deg);
                Line(hands,-d*tail,d*(ringAt-ringR),w,w*.6f);Ring(hands,d*ringAt,ringR,w*.55f);Line(hands,d*(ringAt+ringR*.8f),d*tip,w*.65f,w*.08f);
            }
            Hand(304f,2.4f,7.4f,1.25f,10.8f,.95f);Hand(48f,3.2f,12.6f,.95f,17.0f,.70f);Disc(hands,Vector2.zero,1.2f);

            var tex=new Texture2D(S,S,TextureFormat.RGB24,false,false);
            Color enamel=new Color(.86f,.82f,.70f),ink=new Color(.07f,.065f,.06f),steel=new Color(.07f,.09f,.20f),pin=new Color(.70f,.52f,.22f);
            for(int y=0;y<S;y++)for(int x=0;x<S;x++)
            {
                var p=At(x,y);float r=p.magnitude,u=(float)x/S,v=(float)y/S;
                var c=enamel*(.96f+.05f*Fbm(u,v,6,6,3,61));
                if(r>DialSeen-1.4f)c*=Mathf.Lerp(1,.86f,(r-(DialSeen-1.4f))/1.4f);   // the bezel's shade round the edge
                if(Vector2.Distance(p,sub)<4.4f)c*=.96f;                             // the subdial, a shade deeper
                int sx=x-4,sy=y+5;float shade=sx>=0&&sy<S?hands[sy*S+sx]:0;c*=1-.35f*shade;   // the hands' shadow, down and right
                c=Color.Lerp(c,ink,print[y*S+x]);c=Color.Lerp(c,steel,hands[y*S+x]);
                c=Color.Lerp(c,pin,Mathf.Clamp01(.5f-(r-.45f)*PerMm));
                tex.SetPixel(x,y,c);
            }
            return SaveTexture(DialPath,tex,false,TextureWrapMode.Clamp);
        }
        // The case's brass, u round, v along its profile from the middle of the base (0) to the mouth (1): on the base a
        // primer cup in its dark seat ring and faint turning rings; long faint streaks and a few tarnish clouds along the
        // body; the shoulder and neck dusky from annealing; the mouth bright where it was trimmed.
        static Texture2D Brass()
        {
            const int W=32,H=512;var tex=new Texture2D(W,H,TextureFormat.RGB24,false,false);
            float total=Arc(CaseProfile)/Mm,baseEdge=CaseProfile[1].x/Mm,shoulder=Arc(CaseProfile.Take(Shoulder+1).ToList())/Mm;
            var brass=new Color(.84f,.66f,.38f);
            for(int y=0;y<H;y++)for(int x=0;x<W;x++)
            {
                float u=x/(float)W,f=(y+.5f)/H,a=f*total;
                var c=brass*(.96f+.06f*Fbm(u,f,16,4,3,71));
                c=Color.Lerp(c,new Color(.55f,.38f,.20f),Mathf.Clamp01((Fbm(u,f,2,6,3,73)-.62f)*4)*.22f);
                if(a<baseEdge)
                {
                    if(a<2.2f)c=new Color(.80f,.60f,.42f)*(.95f+.05f*Fbm(u,f,4,4,2,75));
                    else c*=.95f+.05f*Mathf.Sin(a*12f);
                    c=Color.Lerp(c,new Color(.22f,.15f,.08f),Mathf.Clamp01(1-Mathf.Abs(a-2.3f)/.18f));
                }
                c=Color.Lerp(c,new Color(.60f,.44f,.34f),.5f*Mathf.SmoothStep(0,1,(a-(shoulder-6))/7f));
                if(a>total-.5f)c=Color.Lerp(c,new Color(.95f,.80f,.52f),.6f);
                tex.SetPixel(x,y,c);
            }
            return SaveTexture(BrassPath,tex,false,TextureWrapMode.Clamp);
        }
        // Dull lead, mottled grey with a few whitish oxide clouds (kept under 0.6: the gym's light lifts lit faces ~1.5x).
        static Texture2D Lead()
        {
            const int S=64;var tex=new Texture2D(S,S,TextureFormat.RGB24,false,false);
            for(int y=0;y<S;y++)for(int x=0;x<S;x++)
            {
                float u=x/(float)S,v=y/(float)S;
                var c=Color.Lerp(new Color(.33f,.33f,.33f),new Color(.47f,.47f,.46f),Fbm(u,v,4,4,4,81));
                c=Color.Lerp(c,new Color(.55f,.55f,.52f),Mathf.Clamp01((Fbm(u,v,8,8,2,83)-.62f)*5)*.5f);
                tex.SetPixel(x,y,c);
            }
            return SaveTexture(LeadPath,tex,false);
        }

        // A painting surface in millimetres for raised, antiqued metalwork: halo (the dark recess round a raised shape),
        // shine (the raised shape, bright, shaded round across) and cut (fine dark lines on top), each as coverage
        // antialiased over a pixel. Laid down in that order: dark ground, halo, shine, cut.
        sealed class Etch
        {
            readonly int w,h;readonly Vector2 lo,size;readonly float perMm;public readonly float[] halo,shine,cut;
            public Etch(int w,int h,Vector2 lo,Vector2 size)
            {
                this.w=w;this.h=h;this.lo=lo;this.size=size;perMm=Mathf.Min(w/size.x,h/size.y);halo=new float[w*h];shine=new float[w*h];cut=new float[w*h];
            }
            public Vector2 At(int x,int y)=>lo+new Vector2((x+.5f)/w*size.x,(y+.5f)/h*size.y);
            float Cover(float d)=>Mathf.Clamp01(.5f-d*perMm);
            static void Max(float[] layer,int i,float c){if(c>layer[i])layer[i]=c;}
            // Distance from p to the segment a..c, and how far along it (0..1).
            static float Seg(Vector2 p,Vector2 a,Vector2 c,out float t)
            {
                var ac=c-a;t=Mathf.Clamp01(Vector2.Dot(p-a,ac)/Mathf.Max(ac.sqrMagnitude,1e-8f));return Vector2.Distance(p,a+ac*t);
            }
            // Runs paint(pixel index, point) over the box a..b (mm), padded by two pixels.
            void Each(Vector2 a,Vector2 b,System.Action<int,Vector2> paint)
            {
                int x0=Mathf.Max(0,Mathf.FloorToInt((a.x-lo.x)/size.x*w)-2),x1=Mathf.Min(w-1,Mathf.CeilToInt((b.x-lo.x)/size.x*w)+2);
                int y0=Mathf.Max(0,Mathf.FloorToInt((a.y-lo.y)/size.y*h)-2),y1=Mathf.Min(h-1,Mathf.CeilToInt((b.y-lo.y)/size.y*h)+2);
                for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)paint(y*w+x,At(x,y));
            }
            // A fine engraved line along points.
            public void Line(IList<Vector2> points,float wid)
            {
                for(int k=0;k+1<points.Count;k++)
                {
                    Vector2 a=points[k],c=points[k+1];var pad=Vector2.one*wid;
                    Each(Vector2.Min(a,c)-pad,Vector2.Max(a,c)+pad,(i,p)=>Max(cut,i,Cover(Seg(p,a,c,out _)-wid/2)));
                }
            }
            // A raised ribbon along points, w0 wide tapering to w1: a dark recess round it, bright and shaded round across.
            public void Ribbon(IList<Vector2> points,float w0,float w1)
            {
                int n=points.Count;
                for(int k=0;k+1<n;k++)
                {
                    Vector2 a=points[k],c=points[k+1];float wa=Mathf.Lerp(w0,w1,k/(n-1f)),wc=Mathf.Lerp(w0,w1,(k+1)/(n-1f));var pad=Vector2.one*(Mathf.Max(wa,wc)+.4f);
                    Each(Vector2.Min(a,c)-pad,Vector2.Max(a,c)+pad,(i,p)=>
                    {
                        float d=Seg(p,a,c,out var t),half=Mathf.Lerp(wa,wc,t)/2,across=Mathf.Clamp01(d/half);
                        Max(halo,i,Cover(d-half-.25f));Max(shine,i,Cover(d-half)*(.6f+.4f*(1-across*across)));
                    });
                }
            }
            // A raised bead of radius r, lit round.
            public void Bead(Vector2 c,float r)
            {
                Each(c-Vector2.one*(r+.3f),c+Vector2.one*(r+.3f),(i,p)=>
                {
                    float d=Vector2.Distance(p,c),k=Mathf.Clamp01(d/r);Max(halo,i,Cover(d-r-.2f));Max(shine,i,Cover(d-r)*(.55f+.45f*(1-k*k)));
                });
            }
            // A raised leaf from its base along dir, its tip bending sideways by bend (a fraction of its length, + to the
            // left of dir): a dark recess round it, a bright face lit more on one side, a vein cut down most of it.
            public void Leaf(Vector2 b,Vector2 dir,float len,float wid,float bend=0)
            {
                dir.Normalize();var across=new Vector2(-dir.y,dir.x);var tip=b+dir*len+across*bend*len;var pad=Vector2.one*(wid+.5f);
                Each(Vector2.Min(b,tip)-pad,Vector2.Max(b,tip)+pad,(i,p)=>
                {
                    var d=p-b;float s=Vector2.Dot(d,dir);if(s<-.3f||s>len+.3f)return;
                    float f=Mathf.Clamp01(s/len),q=Vector2.Dot(d,across)-bend*len*f*f,half=wid/2*Mathf.Pow(Mathf.Sin(Mathf.PI*Mathf.Clamp(f,.02f,.98f)),.6f);
                    float edge=Mathf.Abs(q)-half,inside=s>=0&&s<=len?Cover(edge):0;
                    Max(halo,i,Cover(edge-.25f));Max(shine,i,inside*(.72f+.28f*Mathf.Clamp(q/Mathf.Max(half,.01f),-1,1)));
                    if(s>len*.08f&&s<len*.72f)Max(cut,i,Cover(Mathf.Abs(q)-.07f)*inside*.85f);
                });
            }
        }
        // Points along a curl round c from angle a0 (deg), turning by sweep degrees (+ anticlockwise) as its radius shrinks
        // from r0 to r1.
        static List<Vector2> Spiral(Vector2 c,float a0,float sweep,float r0,float r1,int count)
        {
            var list=new List<Vector2>();for(int i=0;i<=count;i++){float s=i/(float)count;list.Add(c+Polar(a0+sweep*s,r0*Mathf.Pow(r1/r0,s)));}return list;
        }
        // Carries a path on into a curl turning left (anticlockwise) or right from its end, without a kink.
        static List<Vector2> Curl(List<Vector2> path,bool left,float r0,float r1,float turns)
        {
            var end=path[path.Count-1];var go=(end-path[path.Count-2]).normalized;var side=left?new Vector2(-go.y,go.x):new Vector2(go.y,-go.x);var c=end+side*r0;
            float a0=Mathf.Atan2(end.y-c.y,end.x-c.x)*Mathf.Rad2Deg;var curl=Spiral(c,a0,(left?360:-360)*turns,r0,r1,Mathf.CeilToInt(24*turns));
            var all=new List<Vector2>(path);all.AddRange(curl.Skip(1));return all;
        }
        static List<Vector2> Smooth(params Vector2[] points)=>Curve(points.Select(p=>(Vector3)p).ToList(),6).Select(p=>(Vector2)p).ToList();
        static Vector2 Polar(float deg,float r)=>new Vector2(Mathf.Cos(deg*Mathf.Deg2Rad),Mathf.Sin(deg*Mathf.Deg2Rad))*r;
        static Vector2 Turn(Vector2 p,float deg){float a=deg*Mathf.Deg2Rad,c=Mathf.Cos(a),s=Mathf.Sin(a);return new Vector2(p.x*c-p.y*s,p.x*s+p.y*c);}

        // The buckle's front, 512 px over its 90 x 68 mm (planar: u right, v up from the front), antiqued like a western
        // trophy buckle: outside the plate (under and beyond the rope, and the back's corner) polished silver; a satin band
        // with a fine line each side and a row of raised beads; inside it a darkened ground with bright raised scrollwork,
        // mirrored left and right round the skull: a stem from under the muzzle sweeping out along the bottom and up the side
        // into a big curl, leaf flames rising off it towards the skull; a smaller curl beside the skull's cheek; a stem over
        // each horn along the top ending in a small curl, leaves off it; a leaf spray between each horn's tip and the big
        // curl; and a crest of three leaves over the poll. No letters anywhere.
        static Texture2D Plate()
        {
            const int S=512;const float A=BuckleA,B=BuckleB,K=BuckleK,PlateEdge=38.8f,Ground=35.0f;
            var e=new Etch(S,S,new Vector2(-A,-B),new Vector2(2*A,2*B));
            List<Vector2> Ring(float r,int n){var list=new List<Vector2>();for(int i=0;i<=n;i++){float t=i*2*Mathf.PI/n;list.Add(new Vector2(Mathf.Cos(t)*r,Mathf.Sin(t)*r*K));}return list;}
            // Border.
            e.Line(Ring(37.7f,180),.24f);e.Line(Ring(Ground,180),.2f);
            var beads=Even(Ring(36.35f,720).Select(p=>(Vector3)p).ToList(),101);
            for(int i=0;i<100;i++)e.Bead(beads[i],.5f);
            // The crest over the poll.
            e.Leaf(new Vector2(0,15.2f),Vector2.up,6.0f,2.6f);
            foreach(var sx in new[]{1f,-1f})
            {
                Vector2 M(Vector2 p)=>new Vector2(p.x*sx,p.y);
                List<Vector2> All(List<Vector2> points)=>points.Select(M).ToList();
                void LeafAt(Vector2 at,Vector2 dir,float len,float wid,float bend)=>e.Leaf(M(at),M(dir),len,wid,bend*sx);
                LeafAt(new Vector2(1.1f,15.4f),new Vector2(.75f,1),4.4f,1.8f,-.25f);
                // The big stem and its curl, leaf flames off it.
                var stem=Smooth(new Vector2(1.0f,-25.0f),new Vector2(10,-23.6f),new Vector2(18,-21.0f),new Vector2(24.6f,-16.0f),new Vector2(29.4f,-10.8f),new Vector2(30.6f,-4.0f));
                var flames=new[]{(.12f,new Vector2(-.15f,1),4.0f,1.6f),(.3f,new Vector2(-.35f,1),5.5f,2.1f),(.5f,new Vector2(-.6f,.8f),6.0f,2.3f),(.7f,new Vector2(-.85f,.5f),5.5f,2.1f)};
                foreach(var (f,dir,len,wid) in flames)LeafAt(stem[Mathf.RoundToInt(f*(stem.Count-1))],dir,len,wid,.2f);
                e.Ribbon(All(Curl(stem,true,5.0f,.9f,1.35f)),1.6f,.45f);
                // The smaller curl by the cheek, a leaf off its stalk towards the skull.
                var cheek=Smooth(new Vector2(12.0f,-15.5f),new Vector2(10.6f,-11.5f),new Vector2(11.0f,-7.5f));
                LeafAt(cheek[cheek.Count/2],new Vector2(-1,.2f),3.2f,1.4f,.15f);
                e.Ribbon(All(Curl(cheek,false,3.4f,.7f,1.3f)),1.25f,.4f);
                // Over the horn: a stem along the top ending in a small curl, leaves up towards the border and one down.
                var top=Smooth(new Vector2(4.0f,15.6f),new Vector2(8.5f,18.3f),new Vector2(14,19.6f),new Vector2(19,18.9f),new Vector2(22.2f,16.8f));
                LeafAt(top[Mathf.RoundToInt(.3f*(top.Count-1))],new Vector2(.4f,1),3.2f,1.4f,-.15f);
                LeafAt(top[Mathf.RoundToInt(.58f*(top.Count-1))],new Vector2(.65f,.8f),3.0f,1.3f,-.15f);
                LeafAt(top[Mathf.RoundToInt(.44f*(top.Count-1))],new Vector2(.5f,-1),3.4f,1.4f,.15f);
                e.Ribbon(All(Curl(top,false,1.8f,.4f,1.2f)),1.25f,.4f);
                // The spray between the horn's tip and the big curl.
                LeafAt(new Vector2(22.0f,4.4f),new Vector2(1,.45f),5.5f,2.0f,-.15f);LeafAt(new Vector2(22.0f,4.4f),new Vector2(.55f,1),4.0f,1.6f,.15f);
                e.Bead(M(new Vector2(21.6f,4.2f)),.7f);
            }
            var tex=new Texture2D(S,S,TextureFormat.RGB24,false,false);
            Color rim=new Color(.88f,.88f,.90f),band=new Color(.70f,.70f,.72f),ground=new Color(.40f,.40f,.42f),dark=new Color(.12f,.12f,.13f),bright=new Color(.92f,.92f,.94f);
            for(int y=0;y<S;y++)for(int x=0;x<S;x++)
            {
                var p=e.At(x,y);float rho=Mathf.Sqrt(p.x*p.x+p.y*p.y/(K*K)),u=(float)x/S,v=(float)y/S;int i=y*S+x;
                var c=rho<Ground?ground*(.88f+.24f*Fbm(u,v,64,64,2,91)):rho<PlateEdge?band*(.96f+.06f*Fbm(u,v,48,48,2,92)):rim*(.98f+.03f*Fbm(u,v,8,8,2,93));
                c=Color.Lerp(c,dark,e.halo[i]*.85f);c=Color.Lerp(c,bright,e.shine[i]);c=Color.Lerp(c,dark,e.cut[i]);
                tex.SetPixel(x,y,c);
            }
            return SaveTexture(PlatePath,tex,false,TextureWrapMode.Clamp);
        }
        // The rope's twist, one turn per tile along it (u, 7.6 mm) and once round it (v, 15 mm): two strands crossing on
        // the slant, each shaded round with a dark groove between them, faint wire lines across; or (normal) its normal
        // map from the same strands' height, 0.9 mm deep.
        static Texture2D Rope(bool normal)
        {
            const int W=64,H=128;const float Strands=2,Lu=7.6f,Lv=15.1f,Depth=.9f;
            var tex=new Texture2D(W,H,TextureFormat.RGB24,false,normal);
            for(int y=0;y<H;y++)for(int x=0;x<W;x++)
            {
                float u=(x+.5f)/W,v=(y+.5f)/H,w=Mathf.Repeat(u-Strands*v,1),round=1-Mathf.Pow(2*w-1,2);
                if(normal)
                {
                    float slope=-4*(2*w-1)*Depth;   // d height / d w; w rises 1 per tile along u and falls Strands per turn round v
                    var n=new Vector3(-slope/Lu,slope*Strands/Lv,1).normalized;
                    tex.SetPixel(x,y,new Color(n.x*.5f+.5f,n.y*.5f+.5f,n.z*.5f+.5f));
                }
                else
                {
                    var c=Color.Lerp(new Color(.28f,.28f,.30f),new Color(.90f,.90f,.92f),Mathf.Pow(round,.6f));
                    tex.SetPixel(x,y,c*(.95f+.05f*Mathf.Sin((u+Strands*v)*Mathf.PI*2*4)));
                }
            }
            return SaveTexture(normal?RopeNormalPath:RopePath,tex,normal);
        }
    }
}
