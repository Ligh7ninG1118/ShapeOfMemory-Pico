using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ShadowWorkshopIntegration.Editor
{
    // The straw hat (Free Low Poly Game Assets' Cap_4_Straw, a solid 32-sided lathe whose underside is one flat disc)
    // made hollow: the disc inside the brim becomes a ring and the crown gets an inside, a wall and ceiling about 3 mm in
    // from the outside, so the hat looks like a hat from below. The source is left as it is; the result is a new mesh in
    // the same frame and with the same material, its weave texture carried on from the underside (and kept off the pink
    // band). Drawn only: the source stays the piece's shadow (ShadowPiece.shadowProxy in the gym, Torch2Piece.shadowMesh
    // in levels), so the puzzle is unchanged. 1,728 triangles (the source has 1,472).
    public static class StrawHatBuilder
    {
        public const string SourcePath="Assets/ShadowWorkshop/Art/Torch2/Free Low Poly Game Assets/Models/Cap_4_Straw.asset";
        public const string MeshPath="Assets/ShadowWorkshop/Art/Torch2/Meshes/Straw hat hollow.asset";
        const int Sides=32;                       // the source's, so the new ring meets its brim exactly
        const float Floor=.0041f,Rim=.0543f;      // the source's underside disc: height and radius
        const float VRim=.091f,VCentre=.403f;     // its texture rows at the rim and the centre
        const float VLow=.10f,VHigh=.40f;         // weave rows clear of the band; the inside folds back and forth in them

        [MenuItem("MetaHackthone/Shadow Workshop/Build hollow straw hat")]
        public static void Build(){var m=BuildMesh();Debug.Log($"STRAW_HAT_OK {m.triangles.Length/3} tris, {m.vertexCount} verts");}
        public static Mesh Source=>AssetDatabase.LoadAssetAtPath<Mesh>(SourcePath);

        public static Mesh BuildMesh()
        {
            var src=Source;var v=new List<Vector3>(src.vertices);var n=new List<Vector3>(src.normals);var uv=new List<Vector2>(src.uv);var t=src.triangles;
            // Drop the disc closing the underside: triangles whose corners all face down at the floor inside the rim.
            bool Disc(int i)=>n[i].y<-.5f&&Mathf.Abs(v[i].y-Floor)<.0015f&&new Vector2(v[i].x,v[i].z).magnitude<Rim+.0005f;
            var tris=new List<int>();
            for(int i=0;i<t.Length;i+=3)if(!(Disc(t[i])&&Disc(t[i+1])&&Disc(t[i+2]))){tris.Add(t[i]);tris.Add(t[i+1]);tris.Add(t[i+2]);}
            // Which way round the source's u runs.
            float turn=1;
            for(int i=0;i<v.Count;i++)
                if(Mathf.Abs(v[i].y-Floor)<.0005f&&Mathf.Abs(new Vector2(v[i].x,v[i].z).magnitude-Rim)<.0005f&&Mathf.Abs(uv[i].x-5f/Sides)<.01f){turn=Mathf.Sign(Mathf.Atan2(v[i].z,v[i].x));break;}

            // The inside, from the top of the ceiling down the wall (a crease at its foot) and out under the brim to the rim.
            var p=new[]{new Vector2(0,.0477f),new Vector2(.0215f,.0455f),new Vector2(.0310f,.0380f),new Vector2(.0330f,.0330f),
                new Vector2(.0330f,Floor),new Vector2(.0330f,Floor),new Vector2(Rim,Floor)};
            int count=p.Length;bool Crease(int i)=>(p[i+1]-p[i]).sqrMagnitude<1e-12f;
            var seg=new Vector2[count-1];for(int i=0;i+1<count;i++){var d=(p[i+1]-p[i])*1000;seg[i]=Crease(i)?Vector2.zero:new Vector2(d.y,-d.x).normalized;}
            Vector2 Normal(int i){bool before=i>0&&!Crease(i-1),after=i+1<count&&!Crease(i);return before&&after?(seg[i-1]+seg[i]).normalized:after?seg[i]:seg[i-1];}
            // Texture rows: on the ring as on the source's disc (by radius); up the wall and ceiling at the same rate.
            const float rate=(VCentre-VRim)/Rim;var row=new float[count];float wallFoot=VCentre-rate*.0330f,along=0;
            for(int i=count-1;i>=0;i--)
            {
                if(p[i].y<=Floor+1e-6f){row[i]=VCentre-rate*p[i].x;continue;}
                along+=Vector2.Distance(p[i],p[i+1]);row[i]=VLow+Mathf.PingPong(wallFoot+rate*along-VLow,VHigh-VLow);
            }
            var ring=new int[count];
            for(int i=0;i<count;i++)
            {
                bool pole=p[i].x<1e-6f;var nn=Normal(i);ring[i]=v.Count;
                for(int k=0;k<=Sides;k++)
                {
                    float s=(k+(pole?.5f:0))/Sides,a=turn*s*2*Mathf.PI;var radial=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                    v.Add(radial*p[i].x+Vector3.up*p[i].y);n.Add((radial*nn.x+Vector3.up*nn.y).normalized);uv.Add(new Vector2(s*5,row[i]));
                }
            }
            void Tri(int a,int b,int c)
            {
                var face=Vector3.Cross(v[b]-v[a],v[c]-v[a]);if(face.sqrMagnitude<1e-22f)return;
                if(Vector3.Dot(face,n[a]+n[b]+n[c])<0){var x=b;b=c;c=x;}
                tris.Add(a);tris.Add(b);tris.Add(c);
            }
            // The ring under the brim takes the source's own corners there, so the seam is closed exactly.
            var under=new List<Vector3>();
            for(int i=0;i<src.vertexCount;i++)if(Mathf.Abs(v[i].y-Floor)<.0003f&&Mathf.Abs(new Vector2(v[i].x,v[i].z).magnitude-Rim)<.0004f)under.Add(v[i]);
            for(int k=0;k<=Sides;k++)
            {
                int at=ring[count-1]+k;var best=v[at];float d=float.MaxValue;
                foreach(var q in under){float e=(q-v[at]).sqrMagnitude;if(e<d){d=e;best=q;}}
                v[at]=best;
            }
            for(int i=0;i+1<count;i++)if(!Crease(i))for(int k=0;k<Sides;k++){int a=ring[i]+k,c=ring[i+1]+k;Tri(a,c,c+1);Tri(a,c+1,a+1);}

            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);if(!mesh){mesh=new Mesh();AssetDatabase.CreateAsset(mesh,MeshPath);}else mesh.Clear();
            mesh.name="Straw hat hollow";mesh.SetVertices(v);mesh.SetNormals(n);mesh.SetUVs(0,uv);mesh.SetTriangles(tris,0);
            mesh.RecalculateBounds();mesh.RecalculateTangents();EditorUtility.SetDirty(mesh);AssetDatabase.SaveAssets();
            return mesh;
        }
    }
}
