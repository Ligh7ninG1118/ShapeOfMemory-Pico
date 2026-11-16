using System.IO;
using CloudGarden.ShadowPuzzle;
using UnityEditor;
using UnityEngine;
using static ShadowWorkshopIntegration.Editor.PropMesh;

namespace ShadowWorkshopIntegration.Editor
{
    // A mouse plushie sitting up, about 20 cm tall with its ears, facing +z: a pear body on a flat base, a round head
    // drawn out into a short snout, big round cupped ears (pink inside), bead eyes, a pink nose, stubby arms and feet,
    // and a long pink tail curling round to its right on the floor. Kept to a mobile VR budget (under 900 triangles; the
    // sides go where the outline shows: 14 round the head and ears, 12 round the body, 8 or 6 on the small parts);
    // smooth normals do the rounding, a small tiling felt texture the fabric. Three materials (plush fur, plush pink,
    // plush eye). It casts its own shadow (the mesh is readable); side on, the ears, snout and tail make the mouse.
    // Pivoted at its bounding box's centre like the other pieces; the prefab carries a ShadowPiece for the gym.
    public static class MousePlushBuilder
    {
        const string Folder="Assets/ShadowWorkshop/Art/Torch2/";
        public const string MeshPath=Folder+"Meshes/Mouse plush.asset",PrefabPath=Folder+"Prefabs/Mouse plush.prefab";
        const string FeltPath=Folder+"Textures/plush felt.png";
        const int Fur=0,Pink=1,Eye=2;
        // Felt texture tiles: one every 8 cm.
        const float FeltPerMetre=12.5f;
        // The head's centre (it is a surface of revolution along +z) before the mesh is re-centred on its bounds.
        static readonly Vector3 Head=new Vector3(0,.118f,.004f);

        [MenuItem("MetaHackthone/Shadow Workshop/Build mouse plush")]
        public static void Build()
        {
            Felt();var mesh=BuildMesh();var materials=Materials();
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            var go=new GameObject("Mouse plush");
            try
            {
                go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterials=materials;
                go.AddComponent<ShadowPiece>().role=ShadowPiece.Role.Picture;
                PrefabUtility.SaveAsPrefabAsset(go,PrefabPath);
            }
            finally{Object.DestroyImmediate(go);}
            var s=mesh.bounds.size*100;
            Debug.Log($"MOUSE_PLUSH_OK {mesh.triangles.Length/3} tris, {mesh.vertexCount} verts, {s.x:0.0} x {s.y:0.0} x {s.z:0.0} cm");
        }
        // Created only when missing, so tweaks made in Unity survive a rebuild.
        public static Material[] Materials()=>PropMesh.Materials(Folder+"Materials/",new[]{
            new Look("plush fur",Color.white,0f,.12f){baseMap=()=>AssetDatabase.LoadAssetAtPath<Texture2D>(FeltPath)??Felt()},
            new Look("plush pink",new Color(.95f,.67f,.71f),0f,.15f),
            new Look("plush eye",new Color(.035f,.035f,.045f),0f,.90f)});

        public static Mesh BuildMesh()
        {
            var b=new PropMesh(3);
            // Body: a pear on a flat base (a crisp edge round it), its top closed inside the head.
            b.SmoothLathe(Fur,Vector3.zero,Vector3.up,12,new[]{V(0,0),V(.038f,0),V(.053f,.016f),V(.055f,.038f),V(.044f,.064f),V(0,.088f)},1);
            // Head: back pole to snout tip along +z.
            b.SmoothLathe(Fur,Head,Vector3.forward,14,new[]{V(0,-.047f),V(.028f,-.039f),V(.044f,-.020f),V(.047f,0),V(.042f,.020f),V(.028f,.038f),V(.013f,.053f),V(0,.061f)});
            Ball(b,Pink,Head+new Vector3(0,.001f,.060f),Vector3.forward,.0095f,8);
            foreach(var side in new[]{-1f,1f})
            {
                // Bead eyes, 15 mm: domes on the face (the head's surface at 26 mm forward, 56 degrees out from the top), sunk a little.
                float a=56*Mathf.Deg2Rad;var radial=new Vector3(side*Mathf.Sin(a),Mathf.Cos(a),0);
                var normal=(radial*.789f+Vector3.forward*.614f).normalized;
                b.SmoothLathe(Eye,Head+radial*.0373f+Vector3.forward*.026f-normal*.0018f,normal,8,new[]{V(.0075f,0),V(.0058f,.0046f),V(0,.0066f)});
                // Ears: cushions 62 mm across, the fur rolling over the lip into a pink cupped front; up and out from the head,
                // facing forward and a little out.
                float e=60*Mathf.Deg2Rad;var at=Head+new Vector3(side*Mathf.Cos(e),Mathf.Sin(e),0)*.060f+Vector3.back*.010f;
                var axis=new Vector3(side*.34f,.12f,.93f).normalized;
                b.SmoothLathe(Fur,at,axis,14,new[]{V(0,-.007f),V(.020f,-.005f),V(.031f,0),V(.027f,.0045f)});
                b.SmoothLathe(Pink,at,axis,14,new[]{V(.027f,.0045f),V(.015f,.0015f),V(0,0)});
                // Stubby arms resting on the belly, and feet sticking out forward; their roots buried in the body.
                b.Bar(Fur,new[]{new Vector3(side*.036f,.066f,.012f),new Vector3(side*.040f,.052f,.030f),new Vector3(side*.034f,.042f,.044f)},Vector3.right,
                    t=>Ellipse(.0105f,.0095f,8),0,.010f,2);
                b.Bar(Fur,new[]{new Vector3(side*.024f,.011f,.020f),new Vector3(side*.028f,.011f,.058f)},Vector3.right,t=>Ellipse(.0135f,.0105f,8),0,.012f,2);
            }
            // Tail: from low on the back, round on the floor to the mouse's right, tapering to a rounded tip.
            var tail=Curve(new[]{new Vector3(0,.016f,-.046f),new Vector3(.010f,.008f,-.076f),new Vector3(.042f,.008f,-.090f),new Vector3(.068f,.016f,-.066f),new Vector3(.066f,.030f,-.042f)},2);
            b.Bar(Pink,tail,Vector3.up,t=>{float r=Mathf.Lerp(.0050f,.0026f,t);return Ellipse(r,r,6);},0,.003f,1);
            // Felt UVs: planar along the normal's main axis (the noise hides the seams), then pivot at the bounds' centre.
            Vector3 low=Vector3.one*float.MaxValue,high=-low;foreach(var p in b.v){low=Vector3.Min(low,p);high=Vector3.Max(high,p);}
            var centre=(low+high)/2;
            for(int i=0;i<b.v.Count;i++)
            {
                var p=b.v[i];var n=b.n[i];float ax=Mathf.Abs(n.x),ay=Mathf.Abs(n.y),az=Mathf.Abs(n.z);
                b.uv[i]=(ax>=ay&&ax>=az?new Vector2(p.z,p.y):ay>=az?new Vector2(p.x,p.z):new Vector2(p.x,p.y))*FeltPerMetre;
                b.v[i]=p-centre;
            }
            return b.Save(MeshPath,"Mouse plush");
        }
        // A ball of radius r round its own axis (8 or so sides, poles on the axis).
        static void Ball(PropMesh b,int m,Vector3 center,Vector3 axis,float r,int sides)
        {
            var profile=new Vector2[5];for(int k=0;k<5;k++){float a=(-90+k*45)*Mathf.Deg2Rad;profile[k]=V(r*Mathf.Cos(a),r*Mathf.Sin(a));}
            profile[0].x=profile[4].x=0;b.SmoothLathe(m,center,axis,sides,profile);
        }
        static Vector2 V(float r,float h)=>new Vector2(r,h);

        // Warm grey felt: a soft mottle and fine fibre speckle, tileable (128 px, mobile).
        static Texture2D Felt()
        {
            const int size=128;var tex=new Texture2D(size,size,TextureFormat.RGB24,false,false);var fur=new Color(.70f,.66f,.64f);
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float u=x/(float)size,v=y/(float)size;
                var c=fur*(.93f+.10f*Fbm(u,v,4,4,3,23)+.07f*(Fbm(u,v,32,32,2,41)-.5f));c.a=1;tex.SetPixel(x,y,c);
            }
            return SaveTexture(FeltPath,tex,false);
        }
    }
}
