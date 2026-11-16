using CloudGarden.ShadowPuzzle;
using UnityEditor;
using UnityEngine;
using static ShadowWorkshopIntegration.Editor.PropMesh;
using static ShadowWorkshopIntegration.Editor.DogFoodBoxBuilder;
using Object=UnityEngine.Object;

namespace ShadowWorkshopIntegration.Editor
{
    // Detailed look for Home's thin board ('Cube (2)': Unity's unit cube scaled to 132 x 137 x 5 mm): a rustic wooden
    // HOME sign, the O a pale blue paw print, the letters cream serif capitals with worn paint, on three stained planks
    // with a grain, seams and a knot. Drawn only: the unit cube stays the piece's shadow and the piece keeps its scale, as
    // for the dog food carton (DogFoodBoxBuilder, whose chamfered box and shape helpers it shares), so both walls'
    // pictures are unchanged. 44 triangles; one 1024 x 512 atlas drawn here (front, plain back, end-grain sides).
    public static class HomeSignBuilder
    {
        const string Art="Assets/ShadowWorkshop/Art/Torch2/";
        public const string MeshPath=Art+"Meshes/Home sign.asset";
        const string PrintPath=Art+"Textures/home sign.png",MaterialFolder=Art+"Materials/";
        // Cube (2)'s scale in the gym, the size the sign is laid out for (front -z, its top +y).
        public static readonly Vector3 BoardScale=new Vector3(.13153f,.13679f,.00528f);
        const float Chamfer=.0012f,Px=3.6f;const int AtlasW=1024,AtlasH=512;
        // Atlas regions in pixels (x, y from the bottom left, w, h): front, back, the four thin sides (shared) and edge wood.
        static readonly RectInt Front=new RectInt(4,4,474,493),Back=new RectInt(486,4,474,493),Side=new RectInt(968,4,52,493),Edge=new RectInt(968,500,16,8);

        [MenuItem("MetaHackthone/Shadow Workshop/Build home sign")]
        public static void Build()
        {
            Print();var mesh=BuildMesh();Materials();
            Debug.Log($"HOME_SIGN_OK {mesh.triangles.Length/3} tris");
        }
        // Puts the sign on the selected board: draws it, keeps the board's cube as its shadow, names it. Undoable.
        [MenuItem("MetaHackthone/Shadow Workshop/Home sign on selected board")]
        static void OnSelected(){foreach(var go in Selection.gameObjects)ApplyTo(go);}
        [MenuItem("MetaHackthone/Shadow Workshop/Home sign on selected board",true)]
        static bool CanApply()=>Selection.activeGameObject&&Selection.activeGameObject.GetComponent<MeshFilter>();
        public static void ApplyTo(GameObject go)
        {
            var mf=go.GetComponent<MeshFilter>();var mr=go.GetComponent<MeshRenderer>();if(!mf||!mr)return;
            var cube=Resources.GetBuiltinResource<Mesh>("Cube.fbx");var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath)??BuildMesh();
            Undo.RecordObjects(new Object[]{go,mf,mr},"Home sign");
            var shadow=mf.sharedMesh&&mf.sharedMesh!=mesh?mf.sharedMesh:cube;
            mf.sharedMesh=mesh;mr.sharedMaterials=Materials();go.name="Home sign";
            var tag=go.GetComponent<ShadowPiece>();if(!tag)tag=Undo.AddComponent<ShadowPiece>(go);else Undo.RecordObject(tag,"Home sign");
            tag.role=ShadowPiece.Role.Picture;tag.shadowProxy=shadow;
        }
        public static Material[] Materials()=>PropMesh.Materials(MaterialFolder,new[]{
            new Look("home sign",Color.white,0,.25f){baseMap=()=>AssetDatabase.LoadAssetAtPath<Texture2D>(PrintPath)??Print()}});

        public static Mesh BuildMesh()
        {
            var b=new PropMesh(1);var edgeUv=new Vector2((Edge.x+Edge.width/2f)/AtlasW,(Edge.y+Edge.height/2f)/AtlasH);
            ChamferBox(b,BoardScale/2,Chamfer,FaceUv,edgeUv);
            for(int i=0;i<b.v.Count;i++){b.v[i]=Vector3.Scale(b.v[i],new Vector3(1/BoardScale.x,1/BoardScale.y,1/BoardScale.z));b.n[i]=Vector3.Scale(b.n[i],BoardScale).normalized;}
            return b.Save(MeshPath,"Home sign");
        }
        // Front (-z) and back (+z) seen from outside, u right and v up in mm from the bottom left; the four thin sides all
        // take the side strip, its width across the board's thickness and its height along the side.
        static Vector2 FaceUv(int axis,int side,Vector3 p)
        {
            var m=p*1000;var s=BoardScale*500;
            if(axis==2){var r=side<0?Front:Back;float u=side<0?m.x+s.x:s.x-m.x;return new Vector2((r.x+u*Px)/AtlasW,(r.y+(m.y+s.y)*Px)/AtlasH);}
            float across=(m.z+s.z)/(2*s.z),along=axis==0?m.y+s.y:m.x+s.x;
            return new Vector2((Side.x+across*Side.width)/AtlasW,(Side.y+along*Px)/AtlasH);
        }

        // --- The print ---------------------------------------------------------------------------------------------------
        static readonly Color Stain=new Color(.60f,.34f,.16f),StainDark=new Color(.38f,.19f,.08f),Raw=new Color(.78f,.60f,.40f),
            Cream=new Color(.95f,.93f,.86f),PawBlue=new Color(.62f,.80f,.87f);
        static Texture2D Print()
        {
            var px=new Color[AtlasW*AtlasH];for(int i=0;i<px.Length;i++)px[i]=StainDark;
            Paint(px,Front,FrontArt);Paint(px,Back,(u,v,w,h)=>Wood(u,v,w,h,Raw,new Color(.62f,.45f,.28f),3));
            Paint(px,Side,(u,v,w,h)=>Wood(v,u,h,w,Stain*.85f,StainDark,5));
            var tex=new Texture2D(AtlasW,AtlasH,TextureFormat.RGB24,false,false);tex.SetPixels(px);
            return SaveTexture(PrintPath,tex,false,TextureWrapMode.Clamp);
        }
        static void Paint(Color[] px,RectInt r,System.Func<float,float,float,float,Color> art)
        {
            float w=r.width/Px,h=r.height/Px;
            for(int y=r.y-3;y<r.yMax+3;y++)for(int x=r.x-3;x<r.xMax+3;x++)
            {
                if(x<0||y<0||x>=AtlasW||y>=AtlasH)continue;
                float u=Mathf.Clamp((x+.5f-r.x)/Px,0,w),v=Mathf.Clamp((y+.5f-r.y)/Px,0,h);var c=art(u,v,w,h);c.a=1;px[y*AtlasW+x]=c;
            }
        }
        // Three planks along u: a stained grain (streaks along the planks), a little tint per plank, dark seams between them.
        static Color Wood(float u,float v,float w,float h,Color light,Color dark,int seed)
        {
            int plank=Mathf.Min(2,Mathf.FloorToInt(v/(h/3)));float local=v-plank*h/3;
            float g=Fbm(u/w,v/h+plank*.31f,3,40,3,seed+plank);float streak=Mathf.Repeat(v*.55f+g*7,1);
            var c=Color.Lerp(dark,light,.45f+.5f*g)*(1-.18f*Mathf.Clamp01(1-streak/.12f))*(.94f+.06f*(plank%2));
            float seam=Mathf.Min(local,h/3-local);if(plank>0&&local<.6f||plank<2&&h/3-local<.6f)c*=.55f+.45f*Mathf.Clamp01(seam/.6f);
            return c;
        }
        static Color FrontArt(float u,float v,float w,float h)
        {
            var c=Wood(u,v,w,h,Stain,StainDark,7);
            c=Over(c,StainDark*.8f,Cover(Ellipse(u,v,104,22,4.5f,2.6f,8),Px)*.6f);c=Over(c,StainDark,Cover(Ellipse(u,v,104,22,1.8f,1f,8),Px));   // a knot
            float edge=Mathf.Min(Mathf.Min(u,w-u),Mathf.Min(v,h-v));c*=.82f+.18f*Mathf.Clamp01(edge/4);                                 // stain pools at the edges
            // H, paw, M, E: one line centred, 34 mm high, the paint worn through in patches.
            const float s=.9f,gap=4.5f;float total=(27+29+32+23+3*gap)*s,x=(w-total)/2,y=(h-38*s)/2;
            float wear=Mathf.Lerp(.55f,1,Mathf.SmoothStep(0,1,(Fbm(u/w,v/h,32,32,2,19)-.27f)/.08f));   // small chips, not stains
            c=Over(c,Cream,Cover(H((u-x)/s,(v-y)/s)*s,Px)*wear);x+=(27+gap)*s;
            c=Over(c,PawBlue,Cover(PawO((u-x)/s,(v-y)/s)*s,Px)*wear);x+=(29+gap)*s;
            c=Over(c,Cream,Cover(M((u-x)/s,(v-y)/s)*s,Px)*wear);x+=(32+gap)*s;
            return Over(c,Cream,Cover(E((u-x)/s,(v-y)/s)*s,Px)*wear);
        }
        // Serif capitals 38 mm high from (0, 0), as signed distances in mm.
        const float Cap=38,Serif=2.4f;
        static float Rect(float u,float v,float x0,float y0,float x1,float y1)=>Box(u,v,x0,y0,x1,y1,.3f);
        static float Segment(float u,float v,Vector2 a,Vector2 b,float half)
        {
            var p=new Vector2(u,v);var ab=b-a;float t=Mathf.Clamp01(Vector2.Dot(p-a,ab)/ab.sqrMagnitude);return Mathf.Max(Vector2.Distance(p,a+ab*t)-half,Mathf.Max(-v,v-Cap));
        }
        static float H(float u,float v)
        {
            float d=Mathf.Min(Rect(u,v,0,0,6.5f,Cap),Rect(u,v,20.5f,0,27,Cap));d=Mathf.Min(d,Rect(u,v,6,17,21,20.5f));
            foreach(var x in new[]{0f,20.5f}){d=Mathf.Min(d,Rect(u,v,x-3,0,x+9.5f,Serif));d=Mathf.Min(d,Rect(u,v,x-3,Cap-Serif,x+9.5f,Cap));}
            return d;
        }
        static float M(float u,float v)
        {
            float d=Mathf.Min(Rect(u,v,2,0,5,Cap),Rect(u,v,25,0,31,Cap));
            d=Mathf.Min(d,Segment(u,v,new Vector2(4,Cap-1),new Vector2(16,1),3f));d=Mathf.Min(d,Segment(u,v,new Vector2(16,1),new Vector2(27,Cap-1),1.6f));
            d=Mathf.Min(d,Rect(u,v,-.5f,0,8.5f,Serif));d=Mathf.Min(d,Rect(u,v,22,0,34,Serif));d=Mathf.Min(d,Rect(u,v,-.5f,Cap-Serif,7,Cap));d=Mathf.Min(d,Rect(u,v,25,Cap-Serif,34,Cap));
            return d;
        }
        static float E(float u,float v)
        {
            float d=Rect(u,v,0,0,6.5f,Cap);
            d=Mathf.Min(d,Rect(u,v,0,Cap-3.6f,23,Cap));d=Mathf.Min(d,Rect(u,v,0,17.5f,17,21));d=Mathf.Min(d,Rect(u,v,0,0,23,3.6f));
            d=Mathf.Min(d,Rect(u,v,20.6f,Cap-9,23,Cap));d=Mathf.Min(d,Rect(u,v,20.6f,0,23,9));d=Mathf.Min(d,Rect(u,v,15.5f,15.5f,17,23));
            d=Mathf.Min(d,Rect(u,v,-3,0,0,Serif));d=Mathf.Min(d,Rect(u,v,-3,Cap-Serif,0,Cap));
            return d;
        }
        // The O: a paw print, its pad low and four toes fanned above, as tall as the capitals.
        static float PawO(float u,float v)
        {
            float d=Ellipse(u,v,14.5f,11,9.5f,8);
            d=Mathf.Min(d,Ellipse(u,v,3.6f,23,3.6f,4.6f,25));d=Mathf.Min(d,Ellipse(u,v,10.3f,31,3.8f,4.8f,8));
            d=Mathf.Min(d,Ellipse(u,v,18.7f,31,3.8f,4.8f,-8));d=Mathf.Min(d,Ellipse(u,v,25.4f,23,3.6f,4.6f,-25));
            return d;
        }
    }
}
