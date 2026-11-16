using System.Collections.Generic;
using CloudGarden.ShadowPuzzle;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace ShadowWorkshopIntegration.Editor
{
    // Detailed look for Home's CozyShops vase of flowers (SM_CozyShops_Vase_Flower, 620 triangles): the pot that falls in
    // the left wall's picture, now chipped and cracked ("broken pots and all"). Its 10-sided body, flower, stem and leaf
    // are the original's; the neck and mouth (from 7.3 cm up, and the inner wall) are rebuilt with 20 sides so a chunk of
    // the rim can break away: over about 120 degrees the lip is gone, the edge ragged and showing terracotta clay, and a
    // thin dark crack runs from the lowest point of the break down the shoulder onto the belly. About 100 triangles more
    // than the original (mobile VR); still one submesh on the CozyShops palette (UVs on its swatches), so it keeps
    // M_CozyShops_Palette. Drawn only: the original stays the piece's shadow (ShadowPiece.shadowProxy in the gym,
    // Torch2Piece.shadowMesh in levels), so both walls' pictures are unchanged.
    public static class BrokenVaseBuilder
    {
        public const string OriginalPath="Assets/Scenes/Store/Sari3D/CozyShops_Lite/Meshes/SM_CozyShops_Vase_Flower.fbx";
        public const string MeshPath="Assets/ShadowWorkshop/Art/Torch2/Meshes/Broken vase.asset";
        public const string MaterialPath="Assets/Scenes/Store/Sari3D/CozyShops_Lite/Materials/M_CozyShops_Palette.mat";
        // The original's body: 10 sides, vertices at 18 + 36k degrees; the neck's outer wall (radius Outer) from NeckY up to
        // RimY, the lip's top at LipY, the mouth's inner wall (radius Inner) down to InnerY.
        const float Outer=.01799f,Inner=.01207f,NeckY=.07283f,InnerY=.05486f,RimY=.0840f,LipY=.0860f;
        const int Columns=20;
        // Palette swatches: the body's sage glaze, terracotta for the broken clay, near black for the crack.
        static readonly Vector2 Glaze=new Vector2(.9434f,.4353f),Clay=new Vector2(.5684f,.5586f),Crack=new Vector2(.5078f,.0078f);
        // The break: these columns (of 20, each 18 degrees) lose the lip, their tops at these heights (metres).
        static readonly Dictionary<int,float> Break=new Dictionary<int,float>{{4,.0800f},{5,.0768f},{6,.0784f},{7,.0745f},{8,.0770f},{9,.0752f},{10,.0806f}};
        const int CrackColumn=7;
        // The body's profile at its vertices (height, radius), for laying the crack on its facets.
        static readonly Vector2[] Profile={new Vector2(.023f,.0348f),new Vector2(.041f,.0394f),new Vector2(.058f,.0318f),new Vector2(NeckY,Outer),new Vector2(RimY,Outer)};

        [MenuItem("MetaHackthone/Shadow Workshop/Build broken vase")]
        public static void Build(){var m=BuildMesh();Debug.Log($"BROKEN_VASE_OK {m.triangles.Length/3} tris (original {Original.triangles.Length/3})");}
        // Puts the broken vase on the selected vase: draws it, keeps the original as its shadow. Undoable.
        [MenuItem("MetaHackthone/Shadow Workshop/Broken vase on selected vase")]
        static void OnSelected(){foreach(var go in Selection.gameObjects)ApplyTo(go);}
        public static Mesh Original=>AssetDatabase.LoadAssetAtPath<Mesh>(OriginalPath);
        public static void ApplyTo(GameObject go)
        {
            var mf=go.GetComponent<MeshFilter>();if(!mf||mf.sharedMesh!=Original){Debug.LogWarning($"{go.name} does not draw {Original.name}; left as it is.");return;}
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath)??BuildMesh();
            Undo.RecordObjects(new Object[]{go,mf},"Broken vase");mf.sharedMesh=mesh;go.name="Broken vase";
            var tag=go.GetComponent<ShadowPiece>();if(!tag)tag=Undo.AddComponent<ShadowPiece>(go);else Undo.RecordObject(tag,"Broken vase");
            tag.role=ShadowPiece.Role.Picture;tag.shadowProxy=Original;
        }

        public static Mesh BuildMesh()
        {
            var source=Original;var v=source.vertices;var n=source.normals;var uv=source.uv;var t=source.triangles;
            var b=new PropMesh(1);var map=new Dictionary<int,int>();
            // Keep every triangle but the body's neck, lip and inner wall (the vase is the part reaching the floor).
            var vase=VasePart(source);
            for(int i=0;i<t.Length;i+=3)
            {
                if(vase.Contains(i)&&Neck(v[t[i]])&&Neck(v[t[i+1]])&&Neck(v[t[i+2]])&&!(Cap(v[t[i]])&&Cap(v[t[i+1]])&&Cap(v[t[i+2]])))continue;
                var q=new int[3];
                for(int j=0;j<3;j++){int s=t[i+j];if(!map.TryGetValue(s,out q[j])){q[j]=b.Add(v[s],n[s],uv[s]);map[s]=q[j];}}
                b.Tri(0,q[0],q[1],q[2]);
            }
            NeckAndMouth(b);CrackDown(b);
            return b.Save(MeshPath,"Broken vase");
        }
        static float R(Vector3 p)=>new Vector2(p.x,p.z).magnitude;
        // On the neck or lip (above the neck's foot), or on the mouth's inner wall; the inner floor (Cap) stays.
        static bool Neck(Vector3 p)=>p.y>NeckY-.0003f||(R(p)<Inner+.0004f&&p.y>InnerY-.0004f);
        static bool Cap(Vector3 p)=>p.y<InnerY+.001f;
        // The original's vase body: the connected part (welded by position) that reaches lowest. First-corner indices.
        static HashSet<int> VasePart(Mesh m)
        {
            var v=m.vertices;var t=m.triangles;var weld=new Dictionary<Vector3Int,int>();var id=new int[v.Length];
            for(int i=0;i<v.Length;i++){var k=Vector3Int.RoundToInt(v[i]*100000);if(!weld.TryGetValue(k,out int g)){g=weld.Count;weld[k]=g;}id[i]=g;}
            var parent=new int[weld.Count];for(int i=0;i<parent.Length;i++)parent[i]=i;
            int Find(int x){while(parent[x]!=x){parent[x]=parent[parent[x]];x=parent[x];}return x;}
            for(int i=0;i<t.Length;i+=3){int a=Find(id[t[i]]);parent[Find(id[t[i+1]])]=a;parent[Find(id[t[i+2]])]=a;}
            int low=-1;float lowest=float.MaxValue;for(int i=0;i<v.Length;i++)if(v[i].y<lowest){lowest=v[i].y;low=Find(id[i]);}
            var part=new HashSet<int>();for(int i=0;i<t.Length;i+=3)if(Find(id[t[i]])==low)part.Add(i);
            return part;
        }

        // Column k of 20: its direction, and its radius scale (1 on the body's vertices, the chord's on the midpoints between
        // them), so the rebuilt walls lie on the original's facets and meet its rings without gaps.
        static Vector3 Dir(int k){float a=(18+18*k)*Mathf.Deg2Rad;return new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));}
        static float Scale(int k)=>k%2==0?1:Mathf.Cos(18*Mathf.Deg2Rad);
        static bool Broken(int k)=>Break.ContainsKey(((k%Columns)+Columns)%Columns);
        // Each column from the neck's foot up the outer wall, over the top (the lip, or the broken edge), down the inner
        // wall to the floor: points as (radius, height).
        static Vector2[] Column(int k)
        {
            float s=Scale(k);
            if(Break.TryGetValue(k,out float h))
                return new[]{new Vector2(Outer*s,NeckY),new Vector2(Outer*s,h),new Vector2(Outer*s-.0004f,h),new Vector2(Inner*s+.0004f,h-.0003f),new Vector2(Inner*s,h-.0005f),new Vector2(Inner*s,InnerY)};
            return new[]{new Vector2(Outer*s,NeckY),new Vector2(Outer*s,RimY),new Vector2(Outer*.883f*s,LipY),new Vector2(Outer*.789f*s,LipY),new Vector2(Inner*s,RimY),new Vector2(Inner*s,InnerY)};
        }
        static void NeckAndMouth(PropMesh b)
        {
            // Bands between consecutive column points: outer wall, outer lip, top, inner lip, inner wall.
            for(int k=0;k<Columns;k++)
            {
                int k1=(k+1)%Columns;var a=Column(k);var c=Column(k1);var da=Dir(k);var dc=Dir(k1);bool broken=Broken(k)||Broken(k1);
                for(int band=0;band<5;band++)
                {
                    var uv=band==0||band==4||!broken?Glaze:Clay;
                    Vector3 Normal(Vector3 d)=>band==0?d:band==1?(d*.6f+Vector3.up*.8f):band==2?Vector3.up:band==3?(-d*.6f+Vector3.up*.8f):-d;
                    Vector3 P(Vector3 d,Vector2 rh)=>d*rh.x+Vector3.up*rh.y;
                    b.Quad(0,b.Add(P(da,a[band]),Normal(da),uv),b.Add(P(dc,c[band]),Normal(dc),uv),b.Add(P(dc,c[band+1]),Normal(dc),uv),b.Add(P(da,a[band+1]),Normal(da),uv));
                }
            }
        }
        // A thin dark crack from the lowest point of the break down the neck and shoulder onto the belly: a strip just
        // proud of the facets, zigzagging, tapering from 2.2 mm to 0.6 mm wide.
        static void CrackDown(PropMesh b)
        {
            float start=(18+18*CrackColumn);
            var steps=new[]{(Break[CrackColumn]-.0008f,0f),(.0700f,4f),(.0640f,-3f),(.0580f,5f),(.0510f,-2f),(.0440f,4f),(.0380f,-1f),(.0335f,2f)};
            // Three points per zigzag step, each put back on the facets, so the strip follows the body's folds.
            var left=new List<int>();var right=new List<int>();const int Sub=3;int count=(steps.Length-1)*Sub+1;
            for(int i=0;i<count;i++)
            {
                int s=Mathf.Min(i/Sub,steps.Length-2);float f=(float)(i-s*Sub)/Sub;if(i==count-1){s=steps.Length-2;f=1;}
                float y=Mathf.Lerp(steps[s].Item1,steps[s+1].Item1,f),off=Mathf.Lerp(steps[s].Item2,steps[s+1].Item2,f);
                float angle=(start+off)*Mathf.Deg2Rad;var radial=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                // On the facet at this angle: the profile's radius at the vertices, brought in to the facet's plane.
                float delta=Mathf.Repeat(start+off,36)-18;float r=ProfileRadius(y)*Mathf.Cos(18*Mathf.Deg2Rad)/Mathf.Cos((delta>=0?18-delta:18+delta)*Mathf.Deg2Rad);
                var slope=ProfileSlope(y);var normal=(radial*slope.y-Vector3.up*slope.x).normalized;
                var at=radial*r+Vector3.up*y+normal*.0005f;var across=new Vector3(-radial.z,0,radial.x)*Mathf.Lerp(.0011f,.0003f,(float)i/(count-1));
                left.Add(b.Add(at-across,normal,Crack));right.Add(b.Add(at+across,normal,Crack));
            }
            for(int i=0;i+1<left.Count;i++)b.Quad(0,left[i],left[i+1],right[i+1],right[i]);
        }
        static float ProfileRadius(float y)
        {
            for(int i=0;i+1<Profile.Length;i++)if(y<=Profile[i+1].x)return Mathf.Lerp(Profile[i].y,Profile[i+1].y,Mathf.InverseLerp(Profile[i].x,Profile[i+1].x,y));
            return Profile[Profile.Length-1].y;
        }
        // (dr, dy) of the profile at y, for the crack's normal.
        static Vector2 ProfileSlope(float y)
        {
            for(int i=0;i+1<Profile.Length;i++)if(y<=Profile[i+1].x)return new Vector2(Profile[i+1].y-Profile[i].y,Profile[i+1].x-Profile[i].x).normalized;
            return Vector2.up;
        }
    }
}
