using System;
using UnityEditor;
using UnityEngine;
using static ShadowWorkshopIntegration.Editor.PropMesh;

namespace ShadowWorkshopIntegration.Editor
{
    // Detailed look for the Torch2 'Dinner plate' (GoodMorning's "SPATULA (1)"): the same 22.4 cm plate inside the same
    // outline, but 40 sides instead of 24 and smooth normals along the profile, with a rolled rim, a foot ring and a dark
    // line round the rim. Drawn only: the original 'Dinner plate' stays the piece's shadow, so the puzzle is unchanged.
    // About 1,100 triangles (mobile VR). Materials: the piece's own utensil (body) and utensil black (rim line).
    public static class PlateBuilder
    {
        public const string MeshPath="Assets/ShadowWorkshop/Art/Torch2/Meshes/Dinner plate detailed.asset";
        public const string ShadowPath="Assets/ShadowWorkshop/Art/Torch2/Meshes/Dinner plate.asset";
        const string MaterialFolder="Assets/ShadowWorkshop/Art/Torch2/Materials/";
        const int Body=0,Line=1;

        [MenuItem("MetaHackthone/Shadow Workshop/Build dinner plate")]
        public static void Build()
        {
            var mesh=BuildMesh();Debug.Log($"DINNER_PLATE_OK verts={mesh.vertexCount} tris={mesh.triangles.Length/3}");
        }
        public static Material[] Materials()=>new[]{
            AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder+"utensil.mat"),AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder+"utensil black.mat")};

        // Original profile: well at y -3 mm out to r 68, rising to the rim (r 105-112) at y +9, underside down to a 70 mm
        // base at -9. Walked from the bottom centre out under the plate, round the rim and back in over the top.
        public static Mesh BuildMesh()
        {
            var b=new PropMesh(2);
            SmoothLathe(b,40,
                R(0,-.0077f,Body),R(.0640f,-.0077f,Body,true),R(.0655f,-.0090f,Body,true),R(.0700f,-.0090f,Body,true),
                R(.0725f,-.0076f,Body),R(.1060f,.0043f,Body),R(.1106f,.0056f,Body),R(.1120f,.0073f,Body),
                R(.1116f,.0085f,Body),R(.1088f,.0090f,Body),R(.1032f,.0084f,Line),R(.1012f,.0079f,Body),
                R(.0980f,.0068f,Body),R(.0715f,-.0016f,Body),R(.0640f,-.0030f,Body),R(0,-.0030f,Body));
            return b.Save(MeshPath,"Dinner plate detailed");
        }

        struct Ring{public float r,h;public int m;public bool corner;}
        // Profile point: radius, height, submesh of the segment that starts here; corner splits the normals here.
        static Ring R(float r,float h,int m,bool corner=false)=>new Ring{r=r,h=h,m=m,corner=corner};
        // Surface of revolution round +Y, smooth along the profile except at corners, so a few profile points shade
        // round (PropMesh.Lathe is flat along the profile). Walk the profile with the outside on the side (dh, -dr)
        // points to. Planar UVs from above.
        static void SmoothLathe(PropMesh b,int sides,params Ring[] profile)
        {
            int n=profile.Length;var seg=new Vector2[n-1];
            for(int i=0;i+1<n;i++)seg[i]=(new Vector2(profile[i+1].h-profile[i].h,-(profile[i+1].r-profile[i].r))*1000).normalized;
            int MakeRing(Ring p,Vector2 nn)
            {
                int start=b.v.Count;
                for(int k=0;k<=sides;k++)
                {
                    float t=k*2*Mathf.PI/sides;var radial=new Vector3(Mathf.Cos(t),0,Mathf.Sin(t));var pos=radial*p.r+Vector3.up*p.h;
                    b.Add(pos,(radial*nn.x+Vector3.up*nn.y).normalized,new Vector2(pos.x,pos.z)*UvPerMetre);
                }
                return start;
            }
            int carried=-1;
            for(int i=0;i+1<n;i++)
            {
                int from=carried>=0&&!profile[i].corner?carried:MakeRing(profile[i],i==0||profile[i].corner?seg[i]:(seg[i-1]+seg[i]).normalized);
                int to=MakeRing(profile[i+1],i+2==n||profile[i+1].corner?seg[i]:(seg[i]+seg[i+1]).normalized);
                for(int k=0;k<sides;k++)b.Quad(profile[i].m,from+k,to+k,to+k+1,from+k+1);
                carried=to;
            }
        }
    }
}
