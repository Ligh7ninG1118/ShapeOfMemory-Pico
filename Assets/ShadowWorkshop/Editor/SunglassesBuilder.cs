using UnityEditor;
using UnityEngine;
using static ShadowWorkshopIntegration.Editor.PropMesh;

namespace ShadowWorkshopIntegration.Editor
{
    // Detailed look for the Torch2 'Sunglasses' prop: the frame front only (no temples), with tortoiseshell rims that
    // thicken into a brow, domed gradient lenses, an arched bridge and gold rivets. Drawn only: the original flat
    // 'Sunglasses' mesh stays the piece's shadow, so the puzzle's picture and scoring are unchanged.
    // Same frame and outline as the original: lenses centred at x = +-33 mm, about 52 x 41 mm each, front facing -Z.
    public static class SunglassesBuilder
    {
        public const string MeshPath="Assets/ShadowWorkshop/Art/Torch2/Meshes/Sunglasses detailed.asset";
        public const string ShadowPath="Assets/ShadowWorkshop/Art/Torch2/Meshes/Sunglasses.asset";
        const string MaterialFolder="Assets/ShadowWorkshop/Art/Torch2/Materials/";
        const string TortoisePath="Assets/ShadowWorkshop/Art/Torch2/Textures/tortoiseshell.png";
        const string GradientPath="Assets/ShadowWorkshop/Art/Torch2/Textures/sunglasses lens gradient.png";
        const int Frame=0,Lens=1,Gold=2;
        static Look[] Looks=>new[]{
            new Look("sunglasses tortoise",Color.white,0f,.82f){baseMap=()=>Load(TortoisePath)??Tortoise()},
            new Look("sunglasses lens",Color.white,.35f,.96f){baseMap=()=>Load(GradientPath)??LensGradient()},
            new Look("sunglasses gold",new Color(.95f,.76f,.40f),.85f,.80f)};
        static Texture2D Load(string path)=>AssetDatabase.LoadAssetAtPath<Texture2D>(path);

        // Lens outline: a superellipse, flatter on top than below, a bit squarer than the original ellipse.
        const float CenterX=.033f,HalfWidth=.0258f,HalfHeight=.0206f,TopPower=2.6f,BottomPower=2.2f;
        const float Depth=.0020f,Rim=.0032f,Brow=.0025f,Corner=.0011f;   // half depth, rim width, extra width on top
        // VR budget (about 1.3k triangles): smooth normals do the rounding, so the rim section is a chamfered rectangle
        // (8 points) and each lens a single fan.
        const int Samples=36,CornerSteps=1,LensRings=1,ArchSteps=8,ArchSides=8,RivetSides=8;

        // Textures are rewritten in place (same GUIDs) on every build, so editing the generators below takes effect.
        [MenuItem("MetaHackthone/Shadow Workshop/Build sunglasses")]
        public static void Build()
        {
            Tortoise();LensGradient();var mesh=BuildMesh();var materials=Materials();
            Debug.Log($"SUNGLASSES_OK verts={mesh.vertexCount} submeshes={mesh.subMeshCount} materials={materials.Length}");
        }
        public static Material[] Materials()=>PropMesh.Materials(MaterialFolder,Looks);

        static Vector2 Outline(float t)
        {
            float c=Mathf.Cos(t),s=Mathf.Sin(t),p=s>0?TopPower:BottomPower;
            return new Vector2(HalfWidth*Mathf.Sign(c)*Mathf.Pow(Mathf.Abs(c),2/p),HalfHeight*Mathf.Sign(s)*Mathf.Pow(Mathf.Abs(s),2/p));
        }
        static float RimWidth(float t){float s=Mathf.Max(0,Mathf.Sin(t));return Rim+Brow*s*s;}

        public static Mesh BuildMesh()
        {
            var b=new PropMesh(3);
            foreach(var side in new[]{1,-1})
            {
                var center=new Vector3(CenterX*side,0,0);var flip=new Vector3(side,1,1);
                var path=new Vector3[Samples];var normal=new Vector3[Samples];var width=new float[Samples];var up=new Vector3[Samples];
                for(int i=0;i<Samples;i++)
                {
                    float t=i*2*Mathf.PI/Samples;var p=Outline(t);var tangent=Outline(t+.001f)-Outline(t-.001f);
                    var nn=new Vector2(tangent.y,-tangent.x).normalized;if(Vector2.Dot(nn,p)<0)nn=-nn;
                    path[i]=center+Vector3.Scale(p,flip);normal[i]=Vector3.Scale(nn,flip);width[i]=RimWidth(t);up[i]=Vector3.back;
                }
                // Rim: rounded section from the outline inwards; its texture seam is on the back face.
                b.Sweep(Frame,path,normal,up,i=>RoundedRect(-width[i],0,-Depth,Depth,Corner,CornerSteps),true,3);

                // Lens: a thin meniscus (domed to the front) whose edge sits 0.8 mm inside the rim.
                const int rings=LensRings;const float dome=.0010f,front=-.0003f,back=.0006f;
                foreach(var (z0,facing) in new[]{(front,-1f),(back,1f)})
                {
                    int start=b.v.Count;
                    for(int r=0;r<=rings;r++)for(int i=0;i<=Samples;i++)
                    {
                        int j=i%Samples;float f=(float)r/rings;var edge=path[j]+normal[j]*(-width[j]+.0008f)-center;
                        var p=center+edge*f;p.z=z0-dome*(1-f*f);
                        float slope=2*dome*f/edge.magnitude;var dir=edge.normalized;
                        var nn=(facing<0?dir*slope+Vector3.back:-dir*slope+Vector3.forward).normalized;
                        b.Add(p,nn,new Vector2((p.x-center.x)/(2*HalfWidth)*side+.5f,p.y/(2*HalfHeight)+.5f));
                    }
                    int row=Samples+1;
                    for(int r=0;r<rings;r++)for(int i=0;i<Samples;i++){int q=start+r*row+i;b.Quad(Lens,q,q+row,q+row+1,q+1);}
                }

                // Two gold rivets on the front of the brow, towards the outer corner.
                foreach(var deg in new[]{28f,40f})
                {
                    float t=deg*Mathf.Deg2Rad;int i=Mathf.RoundToInt(t/(2*Mathf.PI)*Samples)%Samples;
                    var at=path[i]+normal[i]*(-width[i]*.5f);at.z=-Depth+.0001f;
                    b.Lathe(at,Vector3.back,RivetSides,P(.0011f,0,Gold),P(.0009f,.0005f,Gold),P(0,.0006f,Gold));
                }
            }

            // Bridge: an arch between the rims, its ends buried in them.
            const int steps=ArchSteps;var arch=new Vector3[steps];var archSide=new Vector3[steps];var archUp=new Vector3[steps];
            for(int i=0;i<steps;i++){float x=Mathf.Lerp(-.0100f,.0100f,i/(steps-1f));arch[i]=new Vector3(x,.0084f+.0020f*(1-x*x/(.0100f*.0100f)),0);}
            for(int i=0;i<steps;i++)
            {
                var tangent=arch[Mathf.Min(i+1,steps-1)]-arch[Mathf.Max(i-1,0)];
                archSide[i]=new Vector3(-tangent.y,tangent.x,0).normalized;archUp[i]=Vector3.back;
            }
            var tube=Ellipse(.0021f,.0019f,ArchSides);
            b.Sweep(Frame,arch,archSide,archUp,i=>tube,false,.6f);

            return b.Save(MeshPath,"Sunglasses detailed");
        }

        // Tileable tortoiseshell: warped noise mapped dark brown -> amber -> honey, blotches longer along the rim (u).
        static Texture2D Tortoise()
        {
            const int size=512;var tex=new Texture2D(size,size,TextureFormat.RGB24,false,false);
            var stops=new[]{(0f,new Color(.035f,.016f,.006f)),(.55f,new Color(.17f,.065f,.018f)),(.82f,new Color(.55f,.28f,.075f)),(1f,new Color(.78f,.48f,.16f))};
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float u=x/(float)size,v=y/(float)size;
                float wu=Fbm(u,v,2,2,3,5)-.5f,wv=Fbm(u,v,2,2,3,9)-.5f;
                float t=Fbm(u+.35f*wu,v+.35f*wv,3,2,5,21);
                t=Mathf.SmoothStep(0,1,Mathf.Clamp01((t-.38f)/.40f));
                t-=.35f*Mathf.Clamp01((Fbm(u,v,24,16,2,77)-.62f)*5);   // dark speckles
                t=Mathf.Clamp01(t);var c=stops[0].Item2;
                for(int k=1;k<stops.Length;k++)if(t<=stops[k].Item1){c=Color.Lerp(stops[k-1].Item2,stops[k].Item2,(t-stops[k-1].Item1)/(stops[k].Item1-stops[k-1].Item1));break;}
                tex.SetPixel(x,y,c);
            }
            return SaveTexture(TortoisePath,tex,false);
        }
        // Classic gradient tint: near black at the top fading to smoky brown at the bottom (v runs up the lens).
        static Texture2D LensGradient()
        {
            var tex=new Texture2D(4,128,TextureFormat.RGB24,false,false);var bottom=new Color(.22f,.14f,.08f);var top=new Color(.025f,.025f,.03f);
            for(int y=0;y<128;y++){var c=Color.Lerp(bottom,top,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.10f,.85f,y/127f)));for(int x=0;x<4;x++)tex.SetPixel(x,y,c);}
            return SaveTexture(GradientPath,tex,false,TextureWrapMode.Clamp);
        }
    }
}
