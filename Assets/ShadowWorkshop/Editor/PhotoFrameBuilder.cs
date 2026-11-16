using UnityEditor;
using UnityEngine;
using static ShadowWorkshopIntegration.Editor.PropMesh;

namespace ShadowWorkshopIntegration.Editor
{
    // Detailed look for Home's thin card ('Cube': Unity's unit cube scaled to a card about 90 x 151 x 2 mm): an oval photo
    // frame, navy with a cream vine of leaves inlaid round its face, holding a photo of a dog. Drawn only:
    // the unit cube stays the piece's shadow (ShadowPiece.shadowProxy in the gym, Torch2Piece.shadowMesh in levels) and
    // the piece keeps its scale, so the picture and scoring are unchanged, and rescaling the card in the gym still moves
    // its shadow and the frame together. The frame is built at the card's size (CardScale: its outline fills the card,
    // 10 mm deep about its plane, the photo facing -z), then stored in the unit cube's space. About 450 triangles
    // (mobile VR); the detail is in the two textures. The inlay is drawn here. The photo is not: 'dog photo.png' is a
    // cartoon bull terrier by biancavandijk (Pixabay, free licence, cartoon-dog-10451678_640.jpg), cropped once to the
    // opening as the gym's frame has it (77 x 131 mm), its cut-off neck carried on down out of sight, at 256 x 512.
    public static class PhotoFrameBuilder
    {
        const string Art="Assets/ShadowWorkshop/Art/Torch2/";
        public const string MeshPath=Art+"Meshes/Oval photo frame.asset";
        const string InlayPath=Art+"Textures/oval frame inlay.png",PhotoPath=Art+"Textures/dog photo.png",MaterialFolder=Art+"Materials/";
        // The card's scale in the gym the frame is designed at; other scales stretch the frame with the card.
        public static readonly Vector3 CardScale=new Vector3(.0895575657f,.151311681f,.00227543735f);
        const int Frame=0,Photo=1;
        // Outline: a superellipse (between an ellipse and a rounded rectangle); the face is Border wide all round.
        const int Around=32;const float Power=2.6f,Border=.013f,Half=.005f,PhotoZ=-.0015f;
        const float LeafPitch=.010f;   // one pair of leaves per this much vine (Inlay draws one pair this long)
        static Vector2 Opening=>new Vector2(CardScale.x/2-Border,CardScale.y/2-Border);

        // The inlay is rewritten in place (same GUID) on every build, so editing it here takes effect; the photo is left alone.
        [MenuItem("MetaHackthone/Shadow Workshop/Build oval photo frame")]
        public static void Build()
        {
            Inlay();var mesh=BuildMesh();Materials();
            Debug.Log($"PHOTO_FRAME_OK {mesh.triangles.Length/3} tris");
        }
        public static Material[] Materials()=>PropMesh.Materials(MaterialFolder,new[]{
            new Look("oval frame inlay",Color.white,0f,.55f){baseMap=()=>AssetDatabase.LoadAssetAtPath<Texture2D>(InlayPath)??Inlay()},
            new Look("dog photo",Color.white,0f,.70f){baseMap=()=>AssetDatabase.LoadAssetAtPath<Texture2D>(PhotoPath)}});

        static Vector2 Super(Vector2 half,float t)
        {
            float c=Mathf.Cos(t),s=Mathf.Sin(t);
            return new Vector2(half.x*Mathf.Sign(c)*Mathf.Pow(Mathf.Abs(c),2/Power),half.y*Mathf.Sign(s)*Mathf.Pow(Mathf.Abs(s),2/Power));
        }

        // The frame: a section carried round the opening's edge (pushed out along its normals, so the face is the same
        // width all round), a flat back, and the photo set 3.5 mm behind the face.
        public static Mesh BuildMesh()
        {
            var b=new PropMesh(2);
            var inner=new Vector2[Around];var normal=new Vector2[Around];var along=new float[Around+1];
            for(int i=0;i<Around;i++)
            {
                float t=i*2*Mathf.PI/Around;inner[i]=Super(Opening,t);var tg=Super(Opening,t+.001f)-Super(Opening,t-.001f);normal[i]=new Vector2(tg.y,-tg.x).normalized;
            }
            for(int i=1;i<=Around;i++)along[i]=along[i-1]+Vector2.Distance(inner[i-1]+normal[i-1]*Border*.5f,inner[i%Around]+normal[i%Around]*Border*.5f);
            int repeats=Mathf.Max(1,Mathf.RoundToInt(along[Around]/LeafPitch));
            // Section: (out from the opening's edge, z, v). Up the inner wall from the photo, over the face (v 0..1 is the
            // inlaid band; off it the texture is plain navy), round the outer edge to the back.
            var section=new[]{(0f,PhotoZ,-.3f),(0f,-.0040f,-.1f),(.0012f,-Half,0f),(Border-.0020f,-Half,1f),(Border,-.0030f,1.1f),(Border,.0040f,1.2f),(Border-.0010f,Half,1.3f)};
            int rows=section.Length;var sn=new Vector2[rows];
            for(int j=0;j<rows;j++)
            {
                Vector2 Seg(int k){var d=new Vector2(section[k+1].Item1-section[k].Item1,section[k+1].Item2-section[k].Item2);return new Vector2(d.y,-d.x).normalized;}
                sn[j]=(j==0?Seg(0):j==rows-1?Seg(rows-2):(Seg(j-1)+Seg(j))).normalized;
            }
            int start=b.v.Count;
            for(int i=0;i<=Around;i++)
            {
                int k=i%Around;
                for(int j=0;j<rows;j++)
                {
                    var (d,z,v)=section[j];var p=inner[k]+normal[k]*d;
                    b.Add(new Vector3(p.x,p.y,z),new Vector3(normal[k].x*sn[j].x,normal[k].y*sn[j].x,sn[j].y),new Vector2(along[i]/along[Around]*repeats,v));
                }
            }
            for(int i=0;i<Around;i++)for(int j=0;j+1<rows;j++){int q=start+i*rows+j;b.Quad(Frame,q,q+rows,q+rows+1,q+1);}
            // Back: a flat panel inside the outer edge's last row. Photo: just wider than the opening, its edge under the wall.
            Fan(b,Frame,i=>inner[i]+normal[i]*section[rows-1].Item1,Half,Vector3.forward,p=>new Vector2(.5f,-.5f));
            Fan(b,Photo,i=>inner[i]+normal[i]*.0005f,PhotoZ,Vector3.back,p=>new Vector2(p.x/(2*Opening.x)+.5f,p.y/(2*Opening.y)+.5f));
            // Into the unit cube's space (the piece's scale brings it back); normals by the inverse transpose.
            for(int i=0;i<b.v.Count;i++){b.v[i]=new Vector3(b.v[i].x/CardScale.x,b.v[i].y/CardScale.y,b.v[i].z/CardScale.z);b.n[i]=Vector3.Scale(b.n[i],CardScale).normalized;}
            return b.Save(MeshPath,"Oval photo frame");
        }
        static void Fan(PropMesh b,int m,System.Func<int,Vector2> at,float z,Vector3 n,System.Func<Vector2,Vector2> uv)
        {
            int hub=b.Add(new Vector3(0,0,z),n,uv(Vector2.zero)),ring=b.v.Count;
            for(int i=0;i<Around;i++){var p=at(i);b.Add(new Vector3(p.x,p.y,z),n,uv(p));}
            for(int i=0;i<Around;i++)b.Tri(m,hub,ring+i,ring+(i+1)%Around);
        }
        // --- Textures ----------------------------------------------------------------------------------------------------
        // One repeat of the inlay: LeafPitch along the vine (u) by the face's width (v), 16 px/mm. A cream stem down the
        // middle, a leaf off each side in turn, leaning along the vine; navy all round.
        static Texture2D Inlay()
        {
            const float mm=16f,width=LeafPitch*1000,depth=9.8f;int w=(int)(width*mm),h=(int)(depth*mm);
            var navy=new Color(.055f,.075f,.19f);var cream=new Color(.93f,.90f,.81f);var px=new Color[w*h];
            float Leaf(Vector2 p,Vector2 a,Vector2 c,float r0,float r1)
            {
                var pa=p-a;var ba=c-a;float t=Mathf.Clamp01(Vector2.Dot(pa,ba)/ba.sqrMagnitude);return (pa-ba*t).magnitude-Mathf.Lerp(r0,r1,Mathf.Pow(t,.7f));
            }
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)
            {
                float cover=0;
                for(int s=0;s<4;s++)
                {
                    var p=new Vector2((x+.25f+.5f*(s&1))/mm,(y+.25f+.5f*(s>>1))/mm);float d=Mathf.Abs(p.y-depth/2)-.22f;
                    for(int k=-1;k<=1;k++)
                    {
                        var q=p+new Vector2(k*width,0);
                        d=Mathf.Min(d,Leaf(q,new Vector2(1.2f,depth/2+.2f),new Vector2(6.4f,8.2f),.25f,1.45f));
                        d=Mathf.Min(d,Leaf(q,new Vector2(6.2f,depth/2-.2f),new Vector2(11.4f,1.6f),.25f,1.45f));
                    }
                    cover+=Mathf.Clamp01(.5f-d*mm)*.25f;
                }
                px[y*w+x]=Color.Lerp(navy,cream,cover);
            }
            var tex=new Texture2D(w,h,TextureFormat.RGB24,false,false);tex.SetPixels(px);tex.Apply();
            SaveTexture(InlayPath,tex,false,TextureWrapMode.Clamp);
            var importer=(TextureImporter)AssetImporter.GetAtPath(InlayPath);
            if(importer.wrapModeU!=TextureWrapMode.Repeat){importer.wrapModeU=TextureWrapMode.Repeat;importer.SaveAndReimport();}
            return AssetDatabase.LoadAssetAtPath<Texture2D>(InlayPath);
        }
    }
}
