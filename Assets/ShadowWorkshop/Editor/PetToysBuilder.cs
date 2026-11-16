using System.Collections.Generic;
using System.IO;
using CloudGarden.ShadowPuzzle;
using UnityEditor;
using UnityEngine;
using static ShadowWorkshopIntegration.Editor.PropMesh;

namespace ShadowWorkshopIntegration.Editor
{
    // Three dog things for the pile: a tennis ball, a grooming brush and a rope toy. Each is a mesh, its materials and a
    // prefab with a ShadowPiece in Art/Torch2, readable so it casts its own shadow, pivoted at its bounds' centre like the
    // other pieces. Mobile VR: a few hundred triangles each, the detail in small textures drawn here. Built only from
    // PropMesh methods already checked in.
    // - Tennis ball: 67 mm, optic yellow felt with its white seam (the classic curve round the sphere, in the texture).
    // - Pet brush: a bamboo brush, 20 cm: a rounded-square tray head (9 x 7.4 cm) with a raised rim holding a green
    //   silicone pad of soft conical nubs, and a flat oval handle rising a little from it; lying nubs up.
    // - Rope toy: a 28 cm braided rope, three strands of white with navy and teal yarns, an overhand knot near each end,
    //   and the ends frayed into pale tassels; lying along x, sagging into a gentle S.
    public static class PetToysBuilder
    {
        const string Folder="Assets/ShadowWorkshop/Art/Torch2/";
        const string MeshFolder=Folder+"Meshes/",PrefabFolder=Folder+"Prefabs/",TextureFolder=Folder+"Textures/",MaterialFolder=Folder+"Materials/";
        const string FeltPath=TextureFolder+"tennis felt.png",RopePath=TextureFolder+"rope strands.png",BambooPath=TextureFolder+"bamboo strips.png";

        // The textures are rewritten in place (same GUIDs) on every build, so editing them here takes effect.
        [MenuItem("MetaHackthone/Shadow Workshop/Build pet toys")]
        public static void Build()
        {
            Felt();Strands();BambooStrips();var report=new List<string>();
            foreach(var (name,mesh,materials,shadow) in new[]{TennisBall(),PetBrush(),RopeToy()})
            {
                Prefab(name,mesh,materials,shadow);var s=mesh.bounds.size*100;
                report.Add($"{name} {mesh.triangles.Length/3} tris {s.x:0.0} x {s.y:0.0} x {s.z:0.0} cm"+(shadow?$" (shadow {shadow.triangles.Length/3})":""));
            }
            Debug.Log("PET_TOYS_OK "+string.Join("; ",report));
        }
        // shadow: a simpler mesh that casts the piece's shadow (ShadowPiece.shadowProxy); null casts the mesh itself.
        static void Prefab(string name,Mesh mesh,Material[] materials,Mesh shadow)
        {
            Directory.CreateDirectory(PrefabFolder);var go=new GameObject(name);
            try
            {
                go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterials=materials;
                var tag=go.AddComponent<ShadowPiece>();tag.role=ShadowPiece.Role.Picture;tag.shadowProxy=shadow;
                PrefabUtility.SaveAsPrefabAsset(go,PrefabFolder+name+".prefab");
            }
            finally{Object.DestroyImmediate(go);}
        }
        // Turns the mesh (normals too), moves its pivot to the bounds' centre and saves it.
        static Mesh Save(PropMesh b,string name,Quaternion turn)=>SaveAt(b,name,turn,Centre(b,turn));
        // The bounds' centre of b once turned, so a shadow mesh can be saved in its look's frame.
        static Vector3 Centre(PropMesh b,Quaternion turn)
        {
            Vector3 lo=Vector3.one*float.MaxValue,hi=-lo;foreach(var p in b.v){var q=turn*p;lo=Vector3.Min(lo,q);hi=Vector3.Max(hi,q);}
            return (lo+hi)/2;
        }
        static Mesh SaveAt(PropMesh b,string name,Quaternion turn,Vector3 centre)
        {
            for(int i=0;i<b.v.Count;i++){b.v[i]=turn*b.v[i]-centre;b.n[i]=turn*b.n[i];}
            return b.Save(MeshFolder+name+".asset",name);
        }

        // --- Tennis ball ------------------------------------------------------------------------------------------------
        const float BallR=.0335f;const int BallSides=18,BallRings=10;
        // The seam: on the unit sphere (1-k)(cos t, sin t) + k(cos 3t, -sin 3t) across, 2 sqrt(k(1-k)) sin 2t up.
        const float SeamK=.27f,SeamHalf=.055f;   // its half width in radians (about 2 mm)
        static (string,Mesh,Material[],Mesh) TennisBall()
        {
            var b=new PropMesh(1);var profile=new Vector2[BallRings+1];
            for(int i=0;i<=BallRings;i++){float a=Mathf.PI*i/BallRings;profile[i]=new Vector2(BallR*Mathf.Sin(a),-BallR*Mathf.Cos(a));}
            profile[0].x=profile[BallRings].x=0;
            b.SmoothLathe(0,Vector3.zero,Vector3.up,BallSides,profile);
            // u round (as SmoothLathe gives it), v by latitude from the bottom pole, the way Felt draws the seam.
            for(int i=0;i<b.v.Count;i++)b.uv[i]=new Vector2(b.uv[i].x,Mathf.Acos(Mathf.Clamp(-b.v[i].y/BallR,-1,1))/Mathf.PI);
            var mesh=Save(b,"Tennis ball",Quaternion.identity);
            return ("Tennis ball",mesh,PropMesh.Materials(MaterialFolder,new[]{
                new Look("tennis felt",Color.white,0,.08f){baseMap=()=>AssetDatabase.LoadAssetAtPath<Texture2D>(FeltPath)??Felt()}}),(Mesh)null);
        }
        // Optic yellow felt, the seam a white band with a slight groove each side. Texel (u, v) is the point SmoothLathe
        // puts there: round the y axis by u (its frame: e1 = -z, e2 = -x), up from the bottom pole by v.
        static Texture2D Felt()
        {
            const int w=512,h=256;var tex=new Texture2D(w,h,TextureFormat.RGB24,false,false);
            var seam=new Vector3[480];
            for(int i=0;i<seam.Length;i++)
            {
                float t=i*2*Mathf.PI/seam.Length,k=SeamK;
                seam[i]=new Vector3((1-k)*Mathf.Cos(t)+k*Mathf.Cos(3*t),2*Mathf.Sqrt(k*(1-k))*Mathf.Sin(2*t),(1-k)*Mathf.Sin(t)-k*Mathf.Sin(3*t));
            }
            var felt=new Color(.80f,.92f,.16f);var line=new Color(.96f,.96f,.92f);
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)
            {
                float u=(x+.5f)/w,v=(y+.5f)/h,t=u*2*Mathf.PI,phi=v*Mathf.PI;
                var p=new Vector3(-Mathf.Sin(t)*Mathf.Sin(phi),-Mathf.Cos(phi),-Mathf.Cos(t)*Mathf.Sin(phi));
                float best=-1;foreach(var q in seam)best=Mathf.Max(best,Vector3.Dot(p,q));
                float angle=Mathf.Acos(Mathf.Clamp(best,-1,1));
                var c=felt*(.92f+.10f*Fbm(u,v,8,4,3,7)+.06f*(Fbm(u,v,64,32,2,13)-.5f));
                c*=1-.18f*Mathf.Clamp01(1-Mathf.Abs(angle-SeamHalf*1.6f)/(SeamHalf*.8f));   // the groove beside the seam
                c=Color.Lerp(c,line*(.95f+.05f*Fbm(u,v,32,16,2,5)),Mathf.Clamp01((SeamHalf-angle)/(SeamHalf*.25f)+.5f));
                c.a=1;tex.SetPixel(x,y,c);
            }
            return SaveTexture(FeltPath,tex,false);
        }

        // --- Pet brush --------------------------------------------------------------------------------------------------
        // Built with the nubs along +z, then turned nubs up (+y). The head is surfaces of revolution round z, squeezed to
        // HeadA x HeadB and pushed out at the diagonals into a rounded square (Squircle); the handle runs out along +x.
        const float HeadA=.045f,HeadB=.037f,SquirclePower=3.2f,PadZ=.0118f;
        const float BambooPerMetre=12.5f;   // bamboo texture: one tile every 8 cm, strips along x
        static (string,Mesh,Material[],Mesh) PetBrush()
        {
            const int Bamboo=0,Silicone=1;var b=new PropMesh(2);
            // Tray: under the head from the middle out, up the outer wall, over the rounded rim and down inside to the
            // pad's level (radius in head units, height in metres). The pad covers the open middle.
            int start=b.v.Count;
            b.SmoothLathe(Bamboo,Vector3.zero,Vector3.forward,18,new[]{new Vector2(0,0),new Vector2(.90f,0),new Vector2(1,.004f),new Vector2(1,.012f),
                new Vector2(.96f,.0152f),new Vector2(.905f,.0138f),new Vector2(.875f,.0085f)},1);
            Head(b,start);
            // Silicone pad: a low dome tucked under the rim.
            start=b.v.Count;
            b.SmoothLathe(Silicone,Vector3.zero,Vector3.forward,18,new[]{new Vector2(.89f,.0075f),new Vector2(.86f,.0105f),new Vector2(.62f,.0116f),new Vector2(0,PadZ)});
            Head(b,start);
            // Handle: a flat oval bar from inside the rim, rising a little and widening to a rounded end.
            start=b.v.Count;
            b.Bar(Bamboo,new[]{new Vector3(.034f,0,.0075f),new Vector3(.065f,0,.0085f),new Vector3(.110f,0,.0115f),new Vector3(.150f,0,.0150f)},Vector3.up,
                t=>Ellipse(Mathf.Lerp(.0115f,.0135f,t),.0072f,10),0,.012f,2);
            for(int i=start;i<b.v.Count;i++)b.uv[i]=new Vector2(b.v[i].x,b.v[i].y)*BambooPerMetre;
            // Nubs: soft silicone cones on a staggered grid inside the pad, five-sided, round-tipped by their normals.
            for(int row=-3;row<=3;row++)for(int col=-4;col<=4;col++)
            {
                float x=col*.0118f+(row%2==0?0:.0059f),y=row*.0102f;
                if(Mathf.Pow(Mathf.Abs(x/(HeadA*.80f)),SquirclePower)+Mathf.Pow(Mathf.Abs(y/(HeadB*.78f)),SquirclePower)>1)continue;
                b.SmoothLathe(Silicone,new Vector3(x,y,PadZ-.0018f),Vector3.forward,5,new[]{new Vector2(.0026f,0),new Vector2(.0016f,.0055f),new Vector2(0,.0090f)});
            }
            var turn=Quaternion.Euler(-90,0,0);var centre=Centre(b,turn);var mesh=SaveAt(b,"Pet brush",turn,centre);
            var shadow=SaveAt(BrushShadow(),"Pet brush shadow",turn,centre);
            return ("Pet brush",mesh,PropMesh.Materials(MaterialFolder,new[]{
                new Look("bamboo",Color.white,0,.38f){baseMap=()=>AssetDatabase.LoadAssetAtPath<Texture2D>(BambooPath)??BambooStrips()},
                new Look("brush silicone",new Color(.46f,.80f,.30f),0,.40f)}),shadow);
        }
        // The brush's shadow (90 triangles instead of its 813): a solid 12-sided rounded square for the head, under the
        // rim's height, and a 6-sided handle a little slimmer than the look's, in the look's frame. Every part sits inside
        // the look, so its shadow falls inside the look's: the nubs (9 mm) are left out. Gym piles stay under their
        // shadow-triangle budget with it.
        static PropMesh BrushShadow()
        {
            // Between the tray's bevelled foot (full width from 4 mm up) and its rim (which rolls in above 12 mm).
            var b=new PropMesh(1);const int Around=12;const float Bottom=.0042f,Top=.0135f;
            var outline=new Vector2[Around];
            for(int k=0;k<Around;k++)
            {
                float a=k*2*Mathf.PI/Around,c=Mathf.Cos(a),s=Mathf.Sin(a),f=Mathf.Pow(Mathf.Pow(Mathf.Abs(c),SquirclePower)+Mathf.Pow(Mathf.Abs(s),SquirclePower),-1/SquirclePower)*.955f;
                outline[k]=new Vector2(c*f*HeadA,s*f*HeadB);
            }
            for(int k=0;k<Around;k++)
            {
                var p=outline[k];var q=outline[(k+1)%Around];var n=new Vector3(q.y-p.y,p.x-q.x,0).normalized;
                b.Quad(0,b.Add(new Vector3(p.x,p.y,Bottom),n,Vector2.zero),b.Add(new Vector3(q.x,q.y,Bottom),n,Vector2.zero),b.Add(new Vector3(q.x,q.y,Top),n,Vector2.zero),b.Add(new Vector3(p.x,p.y,Top),n,Vector2.zero));
            }
            var caps=Triangulate(outline);
            foreach(var (z,n) in new[]{(Bottom,Vector3.back),(Top,Vector3.forward)})
            {
                int start=b.v.Count;foreach(var p in outline)b.Add(new Vector3(p.x,p.y,z),n,Vector2.zero);
                for(int k=0;k<caps.Count;k+=3)b.Tri(0,start+caps[k],start+caps[k+1],start+caps[k+2]);
            }
            b.Bar(0,new[]{new Vector3(.034f,0,.0075f),new Vector3(.065f,0,.0085f),new Vector3(.110f,0,.0115f),new Vector3(.150f,0,.0150f)},Vector3.up,
                t=>Ellipse(Mathf.Lerp(.0102f,.0122f,t),.0064f,6),0,.0110f,1);   // inside the look's 10-sided section's flats
            return b;
        }
        // Squeezes the head part added since start from the unit circle to the head's rounded square, its normals with
        // it (the inverse scale), and gives the bamboo planar UVs (strips along x).
        static void Head(PropMesh b,int start)
        {
            for(int i=start;i<b.v.Count;i++)
            {
                var p=b.v[i];float r=new Vector2(p.x,p.y).magnitude;
                if(r>1e-6f)
                {
                    float c=Mathf.Abs(p.x/r),s=Mathf.Abs(p.y/r),k=Mathf.Pow(Mathf.Pow(c,SquirclePower)+Mathf.Pow(s,SquirclePower),-1/SquirclePower);
                    p=new Vector3(p.x*k*HeadA,p.y*k*HeadB,p.z);
                }
                b.v[i]=p;var n=b.n[i];b.n[i]=new Vector3(n.x/HeadA,n.y/HeadB,n.z).normalized;
                b.uv[i]=new Vector2(p.x,p.y)*BambooPerMetre;
            }
        }
        // Laminated bamboo: strips across v of a few widths, pale and caramel, a fine grain along them and the odd node.
        static Texture2D BambooStrips()
        {
            const int size=128;var tex=new Texture2D(size,size,TextureFormat.RGB24,false,false);
            var pale=new Color(.92f,.79f,.56f);var dark=new Color(.70f,.49f,.26f);
            float[] edges={0,.13f,.20f,.38f,.47f,.66f,.72f,.88f,1};   // strip edges across the tile, alternating pale and dark
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float u=(x+.5f)/size,v=(y+.5f)/size;int strip=0;while(strip+1<edges.Length-1&&v>=edges[strip+1])strip++;
                var c=strip%2==0?pale:dark;
                c*=.90f+.12f*Fbm(u,v,2,48,3,9);                                         // grain along the strips
                float node=Mathf.Abs(Mathf.Repeat(u+strip*.37f,1)-.5f);c*=1-.18f*Mathf.Clamp01(1-node*60);   // a node per strip
                c.a=1;tex.SetPixel(x,y,c);
            }
            return SaveTexture(BambooPath,tex,false);
        }

        // --- Rope toy ---------------------------------------------------------------------------------------------------
        const float RopeLength=.28f,RopeR=.0105f,KnotR=.021f,KnotAt=.059f,KnotHalf=.022f,FrayLength=.034f,StrandPitch=.03f;
        // Stations along the rope from each end (metres) where its section changes: inside the tassel, the knot, the rope.
        static readonly float[] RopeStations={.026f,.038f,.047f,.059f,.071f,.080f,.110f},FrayStations={0,.012f,.024f,FrayLength};
        static Vector3 RopeAt(float s)=>new Vector3(s-RopeLength/2,-.008f*Mathf.Sin(2*Mathf.PI*s/RopeLength),0);
        static (string,Mesh,Material[],Mesh) RopeToy()
        {
            var b=new PropMesh(1);
            // The rope and its knots, from inside one tassel to inside the other; the texture's lower half, u along in
            // strand pitches.
            var along=new List<float>(RopeStations);along.Add(RopeLength/2);for(int i=RopeStations.Length-1;i>=0;i--)along.Add(RopeLength-RopeStations[i]);
            float s0=along[0],s1=along[along.Count-1];int start=b.v.Count;
            b.Bar(0,along.ConvertAll(RopeAt),Vector3.forward,t=>RopeSection(Mathf.Lerp(s0,s1,t)),0,0);
            for(int i=start;i<b.v.Count;i++)b.uv[i]=new Vector2(Mathf.Lerp(s0,s1,b.uv[i].x)/StrandPitch,b.uv[i].y*.5f);
            // The tassels, from their tips in to where the rope's binding hides their open ends; the texture's upper half.
            foreach(var end in new[]{0f,RopeLength})
            {
                start=b.v.Count;var path=new List<Vector3>();foreach(var d in FrayStations)path.Add(RopeAt(end==0?d:end-d));
                b.Bar(0,path,Vector3.forward,t=>FraySection(t*FrayLength),.004f,0,1);
                for(int i=start;i<b.v.Count;i++)b.uv[i]=new Vector2(b.uv[i].x,.5f+b.uv[i].y*.5f);
            }
            var mesh=Save(b,"Rope toy",Quaternion.identity);
            return ("Rope toy",mesh,PropMesh.Materials(MaterialFolder,new[]{
                new Look("rope strands",Color.white,0,.10f){baseMap=()=>AssetDatabase.LoadAssetAtPath<Texture2D>(RopePath)??Strands()}}),(Mesh)null);
        }
        // The rope's section s metres along: three strands twisting (lobes turning with s), bulging lumpier into the knots.
        static (Vector2[] at,Vector2[] normal) RopeSection(float s)
        {
            float d=Mathf.Min(s,RopeLength-s),r=RopeR,lobes=.05f,k0=Mathf.Abs(d-KnotAt)/KnotHalf;
            if(k0<1){float bulge=Mathf.Pow(Mathf.Cos(k0*Mathf.PI/2),.8f);r=Mathf.Lerp(r,KnotR,bulge);lobes=Mathf.Lerp(lobes,.15f,bulge);}
            return Section(k=>r*(1+lobes*Mathf.Cos(3*k*2*Mathf.PI/12+s*2*Mathf.PI/StrandPitch)));
        }
        // A tassel's section d metres in from its tip: loose fibres flaring into a star, bunching up towards the binding.
        static (Vector2[] at,Vector2[] normal) FraySection(float d)
        {
            float t=d/FrayLength,r=Mathf.Lerp(.0160f,.0110f,t),star=Mathf.Lerp(.24f,.03f,t);
            return Section(k=>r*(1+(k%2==0?.5f:-.5f)*star));
        }
        // 12 points round at radius(k), with outward normals from the outline.
        static (Vector2[] at,Vector2[] normal) Section(System.Func<int,float> radius)
        {
            const int n=12;var at=new Vector2[n];var normal=new Vector2[n];
            for(int k=0;k<n;k++){float a=k*2*Mathf.PI/n;at[k]=new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius(k);}
            for(int k=0;k<n;k++){var e=at[(k+1)%n]-at[(k+n-1)%n];normal[k]=new Vector2(e.y,-e.x).normalized;}
            return (at,normal);
        }
        // Lower half (v 0..0.5, tiling in itself): three strands twisting one strand per pitch, grooves between them, each
        // strand white with short navy and teal yarn dashes slanting the other way. Upper half: pale tassel fibres along
        // u (tip to binding), with a few coloured ones, a little darker near the binding. Tiles along u.
        static Texture2D Strands()
        {
            const int w=128,h=256;var tex=new Texture2D(w,h,TextureFormat.RGB24,false,false);
            var white=new Color(.94f,.93f,.89f);var navy=new Color(.20f,.25f,.48f);var teal=new Color(.08f,.56f,.49f);
            var yarn=new[]{white,navy,white,teal,white,white,navy,white,teal,white};
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)
            {
                float u=(x+.5f)/w;Color c;
                if(y<h/2)
                {
                    float v=(y+.5f)/(h/2),a=v*3+u,fa=a-Mathf.Floor(a);int strand=Mathf.FloorToInt(a);
                    int dash=Mathf.FloorToInt(v*30-u*10)+strand*3;c=yarn[((dash%yarn.Length)+yarn.Length)%yarn.Length];
                    c*=1-.38f*Mathf.Pow(Mathf.Abs(fa-.5f)*2,4);                  // grooves between strands
                    c*=.92f+.12f*Fbm(u,v,8,32,2,3);
                }
                else
                {
                    float v=(y+.5f-h/2)/(h/2),fibre=Fbm(u,v,2,64,2,21),tint=Fbm(u,v,1,32,1,27);
                    c=new Color(.88f,.88f,.86f)*(.82f+.25f*fibre);
                    if(tint>.72f)c=Color.Lerp(c,navy,.6f);else if(tint<.26f)c=Color.Lerp(c,teal,.5f);
                    c*=1-.22f*Mathf.Pow(u,3);                                       // darker towards the binding
                }
                c.a=1;tex.SetPixel(x,y,c);
            }
            return SaveTexture(RopePath,tex,false);
        }
    }
}
