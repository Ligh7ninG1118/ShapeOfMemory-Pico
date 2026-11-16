using System.Collections.Generic;
using CloudGarden.ShadowPuzzle;
using UnityEditor;
using UnityEngine;
using static ShadowWorkshopIntegration.Editor.PropMesh;

namespace ShadowWorkshopIntegration.Editor
{
    // Detailed look for Home's CozyShops espresso cup and saucer (SM_CozyShops_Espresso_Cup_Saucer, 10-sided, 412
    // triangles): the same cup on the same saucer inside the original's outline (its polygons' corners reach 133 mm across
    // the saucer, which is 20 mm high, and 80 mm across the cup, which stands on the saucer's floor at 7.9 mm with its rim
    // at 60.9 mm; these circles sit between their corners and flats), handle out along -x to 55.5 mm, 24 sides round and
    // smooth normals along the profiles: a footed saucer with a rolled lip round a shallow well, a cup with a rounded
    // base, a rolled terracotta rim and a curved floor inside, and a looped oval-section handle. Drawn only: the original
    // stays the piece's shadow (ShadowPiece.shadowProxy in the gym, Torch2Piece.shadowMesh in levels), so the puzzle is
    // unchanged. One submesh coloured from the CozyShops palette (UVs on its swatches), so it keeps the pack's material
    // (M_CozyShops_Palette) and colours. 864 triangles (mobile VR; the original has 412).
    // The cup (with its handle) and the saucer are separate meshes so they can be placed apart, both still pivoted at the
    // saucer's underside centre (the cup's foot at 8 mm): about 630 and 336 triangles. Their shadows are the original's
    // cup and saucer parts, split out of it (SplitOriginal), so the two placed where the original was cast its shadow.
    // UseInGym swaps a gym object drawing the original for the pair.
    public static class EspressoCupBuilder
    {
        public const string MeshPath="Assets/ShadowWorkshop/Art/Torch2/Meshes/Espresso cup detailed.asset";
        public const string SaucerPath="Assets/ShadowWorkshop/Art/Torch2/Meshes/Espresso saucer detailed.asset";
        public const string CupShadowPath="Assets/ShadowWorkshop/Art/Torch2/Meshes/Espresso cup.asset",SaucerShadowPath="Assets/ShadowWorkshop/Art/Torch2/Meshes/Espresso saucer.asset";
        public const string OriginalPath="Assets/Scenes/Store/Sari3D/CozyShops_Lite/Meshes/SM_CozyShops_Espresso_Cup_Saucer.fbx";
        public const string MaterialPath="Assets/Scenes/Store/Sari3D/CozyShops_Lite/Materials/M_CozyShops_Palette.mat";
        const int Sides=24;
        // Swatch centres on T_CozyShops_Palette_BC, the colours the original uses for each part.
        static readonly Vector2 SaucerUnder=new Vector2(.1500f,.5625f),SaucerTop=new Vector2(.2175f,.5625f),SaucerWell=new Vector2(.2850f,.5625f),
            CupBody=new Vector2(.1800f,.6875f),CupInside=new Vector2(.4400f,.6875f),Rim=new Vector2(.5622f,.5527f),Handle=new Vector2(.2450f,.6875f);

        [MenuItem("MetaHackthone/Shadow Workshop/Build espresso cup")]
        public static void Build()
        {
            var cup=BuildMesh();var saucer=BuildSaucer();var (cupShadow,saucerShadow)=SplitOriginal();
            Debug.Log($"ESPRESSO_CUP_OK cup {cup.triangles.Length/3} tris (shadow {cupShadow.triangles.Length/3}), saucer {saucer.triangles.Length/3} tris (shadow {saucerShadow.triangles.Length/3})");
        }
        public static Mesh Original=>AssetDatabase.LoadAssetAtPath<Mesh>(OriginalPath);
        public static Material[] Materials()=>new[]{AssetDatabase.LoadAssetAtPath<Material>(MaterialPath)};

        // Swaps a gym object drawing the original cup and saucer for the pair: it becomes "Espresso cup" (the detailed cup,
        // its shadow the original's cup part) and a new sibling "Espresso saucer" takes the same place (the detailed saucer,
        // its shadow the original's saucer part). Together they cast exactly the original's shadow. Undoable.
        [MenuItem("MetaHackthone/Shadow Workshop/Split selected espresso cup")]
        static void SplitSelected(){foreach(var go in Selection.gameObjects)UseInGym(go);}
        public static GameObject UseInGym(GameObject go)
        {
            var mf=go.GetComponent<MeshFilter>();var mr=go.GetComponent<MeshRenderer>();
            if(!mf||!mr||mf.sharedMesh!=Original){Debug.LogWarning($"{go.name} does not draw {Original.name}; left as it is.");return null;}
            Mesh Load(string path)=>AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(!Load(MeshPath)||!Load(SaucerPath)||!Load(CupShadowPath)||!Load(SaucerShadowPath))Build();
            Undo.RecordObjects(new Object[]{go,mf,mr},"Split espresso cup");
            mf.sharedMesh=Load(MeshPath);mr.sharedMaterials=Materials();go.name="Espresso cup";Tag(go,Load(CupShadowPath));
            var saucer=new GameObject("Espresso saucer");Undo.RegisterCreatedObjectUndo(saucer,"Split espresso cup");
            var t=saucer.transform;t.SetParent(go.transform.parent,false);t.localPosition=go.transform.localPosition;t.localRotation=go.transform.localRotation;
            t.localScale=go.transform.localScale;t.SetSiblingIndex(go.transform.GetSiblingIndex()+1);
            saucer.AddComponent<MeshFilter>().sharedMesh=Load(SaucerPath);saucer.AddComponent<MeshRenderer>().sharedMaterials=Materials();Tag(saucer,Load(SaucerShadowPath));
            return saucer;
        }
        static void Tag(GameObject go,Mesh shadow)
        {
            var tag=go.GetComponent<ShadowPiece>();if(!tag)tag=Undo.AddComponent<ShadowPiece>(go);else Undo.RecordObject(tag,"Split espresso cup");
            tag.role=ShadowPiece.Role.Picture;tag.shadowProxy=shadow;
        }

        public static Mesh BuildSaucer()
        {
            var b=new PropMesh(1);
            // Saucer: from the centre of its underside (raised 1.2 mm inside the foot) out to the foot, up round the edge,
            // over the lip, down the slope into the well.
            Lathe(b,R(0,.0012f,SaucerUnder),R(.0300f,0,SaucerUnder,true),R(.0550f,.0045f,SaucerUnder),
                R(.0650f,.0150f,SaucerUnder),R(.0640f,.0190f,SaucerTop),R(.0610f,.0200f,SaucerTop),R(.0577f,.0180f,SaucerTop),
                R(.0465f,.0090f,SaucerWell),R(0,.0079f,SaucerWell));
            return b.Save(SaucerPath,"Espresso saucer detailed");
        }

        // The cup and its handle (the saucer is BuildSaucer).
        public static Mesh BuildMesh()
        {
            var b=new PropMesh(1);
            // Cup: from the middle of its foot (on the well's floor) out, round the base, up the side, over the rolled rim
            // (terracotta, from 4.4 mm below the top to just inside) and down the inside to its curved floor.
            Lathe(b,R(0,.0080f,CupBody),R(.0300f,.0080f,CupBody,true),R(.0336f,.0120f,CupBody),R(.0374f,.0210f,CupBody),
                R(.0393f,.0340f,CupBody),R(.0393f,.0565f,Rim),R(.0385f,.0604f,Rim),R(.0368f,.0609f,Rim),R(.0352f,.0597f,CupInside),
                R(.0290f,.0270f,CupInside),R(0,.0240f,CupInside));
            // Handle: a loop out along -x, both ends buried in the wall; oval section, flatter in its own plane. Eight
            // sides and three steps per span, so the loop reads round close up (it is what a hand reaches for in VR).
            int start=b.v.Count;
            var path=Curve(new[]{new Vector3(-.0360f,.0480f,0),new Vector3(-.0470f,.0475f,0),new Vector3(-.0553f,.0360f,0),
                new Vector3(-.0470f,.0235f,0),new Vector3(-.0360f,.0225f,0)},3);
            b.Bar(0,path,Vector3.forward,t=>Ellipse(.0042f,.0030f,8),0,0);
            for(int i=start;i<b.v.Count;i++)b.uv[i]=Handle;
            return b.Save(MeshPath,"Espresso cup detailed");
        }

        // The original's connected parts (welded by position) sorted into the saucer (the wide, flat ones under 21 mm)
        // and the cup with its handle, each saved as its own shadow mesh in the same frame.
        public static (Mesh cup,Mesh saucer) SplitOriginal()
        {
            var source=Original;var v=source.vertices;var t=source.triangles;
            var weld=new Dictionary<Vector3Int,int>();var id=new int[v.Length];
            for(int i=0;i<v.Length;i++){var k=Vector3Int.RoundToInt(v[i]*100000);if(!weld.TryGetValue(k,out int g)){g=weld.Count;weld[k]=g;}id[i]=g;}
            var parent=new int[weld.Count];for(int i=0;i<parent.Length;i++)parent[i]=i;
            int Find(int x){while(parent[x]!=x){parent[x]=parent[parent[x]];x=parent[x];}return x;}
            for(int i=0;i<t.Length;i+=3){int a=Find(id[t[i]]);parent[Find(id[t[i+1]])]=a;parent[Find(id[t[i+2]])]=a;}
            var parts=new Dictionary<int,List<int>>();
            for(int i=0;i<t.Length;i+=3){int c=Find(id[t[i]]);if(!parts.TryGetValue(c,out var list))parts[c]=list=new List<int>();list.Add(i);}
            var cup=new List<int>();var saucer=new List<int>();
            foreach(var part in parts.Values)
            {
                var bounds=new Bounds(v[t[part[0]]],Vector3.zero);foreach(var i in part)for(int j=0;j<3;j++)bounds.Encapsulate(v[t[i+j]]);
                (bounds.max.y<.021f&&bounds.extents.x>.05f?saucer:cup).AddRange(part);
            }
            return (Subset(source,cup,CupShadowPath,"Espresso cup"),Subset(source,saucer,SaucerShadowPath,"Espresso saucer"));
        }
        // The given triangles (indices of their first corner) of source as a readable mesh at path, updated in place.
        static Mesh Subset(Mesh source,List<int> triangles,string path,string name)
        {
            var v=source.vertices;var t=source.triangles;var map=new Dictionary<int,int>();var verts=new List<Vector3>();var tris=new List<int>();
            foreach(var i in triangles)for(int j=0;j<3;j++){int s=t[i+j];if(!map.TryGetValue(s,out int d)){d=verts.Count;map[s]=d;verts.Add(v[s]);}tris.Add(d);}
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(!mesh){mesh=new Mesh();AssetDatabase.CreateAsset(mesh,path);}else mesh.Clear();
            mesh.name=name;mesh.SetVertices(verts);mesh.SetTriangles(tris,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);AssetDatabase.SaveAssets();return mesh;
        }

        struct Ring{public float r,h;public Vector2 uv;public bool corner;}
        // Profile point: radius, height, the swatch of the segment that starts here; corner splits the normals here.
        static Ring R(float r,float h,Vector2 uv,bool corner=false)=>new Ring{r=r,h=h,uv=uv,corner=corner};
        // Surface of revolution round +y, smooth along the profile except at corners. Each segment has its own two rings
        // so its swatch UV is constant across it (a UV running between swatches would cross the rest of the palette);
        // the normals still match where segments meet, so colour changes don't show as creases. Walk the profile with
        // the outside on the side (dh, -dr) points to; points on the axis close it.
        static void Lathe(PropMesh b,params Ring[] p)
        {
            int n=p.Length;var seg=new Vector2[n-1];
            for(int i=0;i+1<n;i++)seg[i]=(new Vector2(p[i+1].h-p[i].h,-(p[i+1].r-p[i].r))*1000).normalized;
            Vector2 Normal(int i,int s)
            {
                if(p[i].r<1e-6f)return new Vector2(0,Mathf.Sign(seg[s].y));
                return i==0||i==n-1||p[i].corner?seg[s]:(seg[i-1]+seg[i]).normalized;
            }
            int MakeRing(int i,int s)
            {
                int first=b.v.Count;var nn=Normal(i,s);bool pole=p[i].r<1e-6f;
                for(int k=0;k<=Sides;k++)
                {
                    float t=(k+(pole?.5f:0))*2*Mathf.PI/Sides;var radial=new Vector3(Mathf.Cos(t),0,Mathf.Sin(t));
                    b.Add(radial*p[i].r+Vector3.up*p[i].h,(radial*nn.x+Vector3.up*nn.y).normalized,p[s].uv);
                }
                return first;
            }
            for(int s=0;s+1<n;s++)
            {
                int from=MakeRing(s,s),to=MakeRing(s+1,s);
                for(int k=0;k<Sides;k++)b.Quad(0,from+k,to+k,to+k+1,from+k+1);
            }
        }
    }
}
