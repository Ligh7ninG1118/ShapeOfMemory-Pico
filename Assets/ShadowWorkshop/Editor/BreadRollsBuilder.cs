using UnityEditor;
using UnityEngine;
using static ShadowWorkshopIntegration.Editor.PropMesh;

namespace ShadowWorkshopIntegration.Editor
{
    // Three pull-apart bread rolls baked together in a row along x, the middle one bigger and higher, for GoodMorning:
    // seen side on, their shadow is the puffed top of the chef's toque. A piece of its own, casting its own shadow (the
    // mesh is readable). Crust pale where they sat on the tray, golden up the sides, browned on top. About 400
    // triangles (mobile VR): smooth normals round the buns.
    public static class BreadRollsBuilder
    {
        public const string MeshPath="Assets/ShadowWorkshop/Art/Torch2/Meshes/Bread rolls.asset";
        const string MaterialFolder="Assets/ShadowWorkshop/Art/Torch2/Materials/",CrustPath="Assets/ShadowWorkshop/Art/Torch2/Textures/bread crust.png";
        const int Sides=16;
        // Each roll: centre x, radius, height (metres), its bottom on y = 0. Neighbours overlap, as baked rolls do.
        static readonly (float x,float r,float h)[] Rolls={(-.034f,.023f,.025f),(0f,.027f,.034f),(.034f,.023f,.025f)};

        // The crust texture is rewritten in place (same GUID) on every build, so editing Crust takes effect.
        [MenuItem("MetaHackthone/Shadow Workshop/Build bread rolls")]
        public static void Build()
        {
            Crust();var mesh=BuildMesh();Materials();
            Debug.Log($"BREAD_ROLLS_OK {mesh.triangles.Length/3} tris");
        }
        public static Material[] Materials()=>PropMesh.Materials(MaterialFolder,new[]{
            new Look("bread crust",Color.white,0f,.42f){baseMap=()=>AssetDatabase.LoadAssetAtPath<Texture2D>(CrustPath)??Crust()}});

        // Each roll a bun turned on y: flat bottom (a crisp edge), bulging sides, domed top. v is the height up that roll.
        public static Mesh BuildMesh()
        {
            var b=new PropMesh(1);
            foreach(var (x,r,h) in Rolls)
            {
                int start=b.v.Count;
                b.SmoothLathe(0,new Vector3(x,0,0),Vector3.up,Sides,new[]{new Vector2(0,0),new Vector2(r*.82f,0),new Vector2(r,h*.28f),
                    new Vector2(r*.90f,h*.66f),new Vector2(r*.58f,h*.93f),new Vector2(0,h)},1);
                for(int i=start;i<b.v.Count;i++)b.uv[i]=new Vector2(b.uv[i].x,b.v[i].y/h);
            }
            return b.Save(MeshPath,"Bread rolls");
        }

        // Up the roll (v): pale base, golden sides, browned top, with a little mottling; u wraps round.
        static Texture2D Crust()
        {
            const int w=64,hgt=128;var tex=new Texture2D(w,hgt,TextureFormat.RGB24,false,false);
            var stops=new[]{(0f,new Color(.93f,.84f,.62f)),(.12f,new Color(.91f,.70f,.40f)),(.45f,new Color(.80f,.52f,.24f)),(.85f,new Color(.62f,.35f,.13f)),(1f,new Color(.58f,.32f,.12f))};
            for(int y=0;y<hgt;y++)for(int x=0;x<w;x++)
            {
                float v=y/(hgt-1f),u=x/(float)w;var c=stops[0].Item2;
                for(int k=1;k<stops.Length;k++)if(v<=stops[k].Item1){c=Color.Lerp(stops[k-1].Item2,stops[k].Item2,(v-stops[k-1].Item1)/(stops[k].Item1-stops[k-1].Item1));break;}
                c*=.94f+.12f*Fbm(u,v,4,2,3,17);c.a=1;tex.SetPixel(x,y,c);
            }
            var saved=SaveTexture(CrustPath,tex,false,TextureWrapMode.Clamp);
            var importer=(TextureImporter)AssetImporter.GetAtPath(CrustPath);
            if(importer.wrapModeU!=TextureWrapMode.Repeat){importer.wrapModeU=TextureWrapMode.Repeat;importer.SaveAndReimport();}
            return AssetDatabase.LoadAssetAtPath<Texture2D>(CrustPath)??saved;
        }
    }
}
