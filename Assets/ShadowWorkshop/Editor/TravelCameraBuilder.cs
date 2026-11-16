using UnityEditor;
using UnityEngine;
using static ShadowWorkshopIntegration.Editor.PropMesh;

namespace ShadowWorkshopIntegration.Editor
{
    // Detailed look for the Torch2 'Travel camera' prop: a retro travel camera with chrome top and bottom plates, a
    // leatherette body and grip, a ringed lens, dials, hot shoe, viewfinder and a back screen. It is only drawn: the
    // original box-and-cylinder 'Travel camera' mesh stays the piece's shadow (ShadowPiece.shadowProxy in the gym,
    // Torch2Piece.shadowMesh in levels), so the puzzle's picture and scoring are unchanged.
    // Same frame and size as the original: lens along -Z, the holder's right hand on -X.
    public static class TravelCameraBuilder
    {
        public const string MeshPath="Assets/ShadowWorkshop/Art/Torch2/Meshes/Travel camera detailed.asset";
        public const string ShadowPath="Assets/ShadowWorkshop/Art/Torch2/Meshes/Travel camera.asset";
        const string MaterialFolder="Assets/ShadowWorkshop/Art/Torch2/Materials/";
        const string LeatherPath="Assets/ShadowWorkshop/Art/Torch2/Textures/leatherette normal.png";
        // Submeshes, in the order of Looks.
        const int Leather=0,Chrome=1,Black=2,Glass=3,Marking=4,Accent=5;
        static Look[] Looks=>new[]{
            new Look("camera leather",new Color(.050f,.050f,.055f),0f,.40f){normalMap=LeatherNormal,bumpScale=.7f},
            new Look("camera chrome",new Color(.80f,.80f,.82f),.80f,.78f),
            new Look("camera black",new Color(.035f,.035f,.040f),.25f,.55f),
            new Look("camera glass",new Color(.015f,.025f,.045f),.50f,.96f),
            new Look("camera marking",new Color(.93f,.93f,.90f),0f,.45f),
            new Look("camera accent",new Color(.96f,.44f,.12f),0f,.60f)};

        [MenuItem("MetaHackthone/Shadow Workshop/Build travel camera")]
        public static void Build()
        {
            var mesh=BuildMesh();var materials=Materials();
            Debug.Log($"TRAVEL_CAMERA_OK verts={mesh.vertexCount} submeshes={mesh.subMeshCount} materials={materials.Length}");
        }
        public static Material[] Materials()=>PropMesh.Materials(MaterialFolder,Looks);

        static Pt B(float r,float h,int m=Black)=>P(r,h,m);
        // VR budget (about 2k triangles): smooth normals do the rounding, so rounded edges are single chamfers, round
        // parts have few sides, profiles keep only the steps that catch light, and ridges have no end caps.
        const int Round=1,LensSides=24,DialSides=16,KnobSides=12,TinySides=8;
        public static Mesh BuildMesh()
        {
            var b=new PropMesh(6);
            // Body: leatherette core between chrome plates; the core runs up into the plates so their rounded edges meet it.
            b.RoundedBox(Leather,new Vector3(-.0545f,-.034f,-.0073f),new Vector3(.0545f,.034f,.0368f),.004f,Round);
            b.RoundedBox(Chrome,new Vector3(-.055f,.024f,-.0078f),new Vector3(.055f,.035f,.0373f),.0035f,Round);
            b.RoundedBox(Chrome,new Vector3(-.055f,-.035f,-.0078f),new Vector3(.055f,-.029f,.0373f),.0025f,Round);
            b.RoundedBox(Leather,new Vector3(-.0535f,-.0265f,-.0140f),new Vector3(-.0365f,.0215f,-.0060f),.004f,Round);  // grip

            // Lens, front to mount.
            var z=Vector3.forward;var lens=Vector3.zero;
            b.Lathe(lens,z,LensSides,B(0,-.0346f,Glass),B(.0125f,-.0340f,Glass),B(.0150f,-.0336f));
            b.Lathe(lens,z,LensSides,
                B(.0150f,-.0336f),B(.0150f,-.0373f),B(.0208f,-.0373f,Chrome),B(.0220f,-.0361f),
                B(.0220f,-.0332f,Marking),B(.0220f,-.0328f),B(.0220f,-.0316f),B(.0212f,-.0316f),B(.0212f,-.0222f),
                B(.0207f,-.0218f,Chrome),B(.0207f,-.0166f),B(.0198f,-.0162f,Chrome),
                B(.0220f,-.0150f,Chrome),B(.0220f,-.0090f,Chrome),B(.0226f,-.0072f));
            b.Knurl(Black,lens,z,-.0312f,-.0224f,.0213f,.0224f,24,false);                                               // focus ring
            b.Box(Marking,new Vector3(-.0004f,.0204f,-.0204f),new Vector3(.0004f,.0210f,-.0178f));                        // aperture index
            b.Box(Accent,new Vector3(-.0008f,.0218f,-.0126f),new Vector3(.0008f,.0223f,-.0110f));                         // mount dot

            // Top plate: speed dial and shutter button on the right hand, rewind knob on the left, hot shoe between.
            var up=Vector3.up;
            var dial=new Vector3(-.026f,.035f,.016f);
            b.Lathe(dial,up,DialSides,B(.0100f,0,Chrome),B(.0090f,.0008f,Chrome),B(.0090f,.0045f,Chrome),B(.0084f,.0051f,Chrome),B(0,.0051f));
            b.Knurl(Chrome,dial,up,.0008f,.0045f,.0089f,.0096f,16,false);
            b.Box(Marking,new Vector3(-.0264f,.0400f,.0090f),new Vector3(-.0256f,.0404f,.0150f));
            var shutter=new Vector3(-.0435f,.035f,.004f);
            b.Lathe(shutter,up,KnobSides,B(.0045f,0),B(.0045f,.0018f),B(.0031f,.0018f,Chrome),B(.0031f,.0034f,Chrome),B(0,.0038f));
            var rewind=new Vector3(.030f,.035f,.014f);
            b.Lathe(rewind,up,DialSides,B(.0074f,0),B(.0074f,.0042f),B(.0066f,.0048f),B(.0035f,.0048f,Chrome),B(.0035f,.0054f,Chrome),B(0,.0054f));
            b.Knurl(Black,rewind,up,.0006f,.0042f,.0073f,.0080f,14,false);
            b.Box(Chrome,new Vector3(-.005f,.035f,.004f),new Vector3(.013f,.0366f,.020f));                                // hot shoe
            b.Box(Chrome,new Vector3(-.005f,.0366f,.004f),new Vector3(-.003f,.0380f,.020f));
            b.Box(Chrome,new Vector3(.011f,.0366f,.004f),new Vector3(.013f,.0380f,.020f));
            b.Box(Black,new Vector3(-.003f,.0366f,.004f),new Vector3(.011f,.0370f,.020f));

            // Front of the top plate: viewfinder window, rangefinder window, self-timer lamp. Strap lugs on the sides.
            var front=Vector3.back;
            b.RoundedBox(Black,new Vector3(.0305f,.0255f,-.0081f),new Vector3(.0495f,.0335f,-.0074f),.0012f,Round);
            b.Box(Glass,new Vector3(.0320f,.0265f,-.0084f),new Vector3(.0480f,.0325f,-.0076f));
            b.Lathe(new Vector3(.016f,.0295f,-.0078f),front,KnobSides,B(.0026f,0),B(.0026f,.0005f),B(.0019f,.0005f,Glass),B(0,.0007f));
            b.Lathe(new Vector3(-.030f,.0295f,-.0078f),front,TinySides,B(.0018f,0,Accent),B(.0018f,.0005f,Accent),B(0,.0007f));
            foreach(var side in new[]{1,-1})
                b.Lathe(new Vector3(.055f*side,.030f,.018f),Vector3.right*side,TinySides,B(.0022f,0,Chrome),B(.0022f,.0034f,Chrome),B(0,.0036f));

            // Back: screen on the holder's left, control wheel and buttons on the right, eyepiece above the screen.
            b.RoundedBox(Black,new Vector3(-.0200f,-.0255f,.0366f),new Vector3(.0470f,.0195f,.0382f),.0015f,Round);
            b.Box(Glass,new Vector3(-.0185f,-.0240f,.0378f),new Vector3(.0455f,.0180f,.0386f));
            var wheel=new Vector3(-.037f,-.011f,.0368f);var back=Vector3.forward;
            b.Lathe(wheel,back,DialSides,B(.0080f,0),B(.0080f,.0014f),B(.0044f,.0014f,Chrome),B(.0040f,.0021f,Chrome),B(0,.0021f));
            b.Knurl(Black,wheel,back,.0002f,.0011f,.0078f,.0083f,16,false);
            b.Lathe(new Vector3(-.044f,.008f,.0368f),back,KnobSides,B(.0030f,0),B(.0030f,.0012f),B(.0025f,.0016f),B(0,.0016f));
            b.Lathe(new Vector3(-.030f,.008f,.0368f),back,KnobSides,B(.0030f,0,Accent),B(.0030f,.0012f,Accent),B(.0025f,.0016f,Accent),B(0,.0016f));
            b.RoundedBox(Black,new Vector3(.031f,.0255f,.0370f),new Vector3(.049f,.0335f,.0395f),.0015f,Round);
            b.Box(Glass,new Vector3(.0345f,.0275f,.0390f),new Vector3(.0455f,.0315f,.0399f));

            return b.Save(MeshPath,"Travel camera detailed");
        }

        // Tileable pebble-grain normal map: Worley cells raised, the borders between them sunk.
        static Texture2D LeatherNormal()
        {
            var existing=AssetDatabase.LoadAssetAtPath<Texture2D>(LeatherPath);if(existing)return existing;
            const int size=512,cells=44;var rnd=new System.Random(7);var pts=new Vector2[cells,cells];
            for(int i=0;i<cells;i++)for(int j=0;j<cells;j++)pts[i,j]=new Vector2((float)rnd.NextDouble(),(float)rnd.NextDouble());
            var height=new float[size,size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float fx=x*cells/(float)size,fy=y*cells/(float)size;int cx=Mathf.FloorToInt(fx),cy=Mathf.FloorToInt(fy);float f1=9,f2=9;
                for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
                {
                    int gx=cx+dx,gy=cy+dy;var p=pts[(gx%cells+cells)%cells,(gy%cells+cells)%cells];
                    float d=Vector2.Distance(new Vector2(fx,fy),new Vector2(gx+p.x,gy+p.y));
                    if(d<f1){f2=f1;f1=d;}else if(d<f2)f2=d;
                }
                height[y,x]=Mathf.SmoothStep(0,1,Mathf.Clamp01((f2-f1)/.32f))*(1-.25f*f1);
            }
            var tex=new Texture2D(size,size,TextureFormat.RGB24,false,true);const float strength=5;
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float hx=(height[y,(x+1)%size]-height[y,(x+size-1)%size])*.5f,hy=(height[(y+1)%size,x]-height[(y+size-1)%size,x])*.5f;
                var n=new Vector3(-hx*strength,-hy*strength,1).normalized;tex.SetPixel(x,y,new Color(n.x*.5f+.5f,n.y*.5f+.5f,n.z*.5f+.5f));
            }
            return SaveTexture(LeatherPath,tex,true);
        }
    }
}
