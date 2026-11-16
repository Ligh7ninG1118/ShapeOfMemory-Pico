using System;
using CloudGarden.ShadowPuzzle;
using UnityEditor;
using UnityEngine;
using static ShadowWorkshopIntegration.Editor.PropMesh;
using Object=UnityEngine.Object;

namespace ShadowWorkshopIntegration.Editor
{
    // Detailed look for Home's big box ('Cube (3)': Unity's unit cube scaled to 140 x 201 x 84 mm): a printed carton of
    // dog food. Drawn only: the unit cube stays the piece's shadow (ShadowPiece.shadowProxy in the gym,
    // Torch2Piece.shadowMesh in levels) and the piece keeps its scale, so both walls' pictures are unchanged and rescaling
    // the cube in the gym still moves its shadow and the carton together. The carton is built at BoxScale (crisp
    // cardboard faces, edges chamfered 2 mm so they catch the light) and stored in the unit cube's space: 44 triangles.
    // The detail is one 1024 x 512 printed atlas drawn here: the front a red band with DOG FOOD over a cream badge with a
    // floppy-eared dog and a bowl of kibble, the back an ingredients panel and a barcode, the sides a trail of paw prints,
    // the top the flaps' seam and a tear strip.
    public static class DogFoodBoxBuilder
    {
        const string Art="Assets/ShadowWorkshop/Art/Torch2/";
        public const string MeshPath=Art+"Meshes/Dog food box.asset";
        const string PrintPath=Art+"Textures/dog food box.png",MaterialFolder=Art+"Materials/";
        // Cube (3)'s scale in the gym, the size the print is laid out for (other scales stretch it with the box).
        public static readonly Vector3 BoxScale=new Vector3(.14039f,.20124f,.08443f);
        const float Chamfer=.002f,PxPerMm=1.5f;const int AtlasW=1024,AtlasH=512;
        // Atlas regions in pixels (x, y from the bottom left, w, h): front, back, left, right, top, bottom; and a swatch
        // of edge colour for the chamfers.
        static readonly RectInt Front=new RectInt(4,4,211,302),Back=new RectInt(223,4,211,302),Left=new RectInt(442,4,127,302),
            Right=new RectInt(577,4,127,302),Top=new RectInt(712,4,211,127),Bottom=new RectInt(712,139,211,127),Edge=new RectInt(1000,480,16,16);

        [MenuItem("MetaHackthone/Shadow Workshop/Build dog food box")]
        public static void Build()
        {
            Print();var mesh=BuildMesh();Materials();
            Debug.Log($"DOG_FOOD_BOX_OK {mesh.triangles.Length/3} tris");
        }
        // Puts the carton on the selected cube: draws it, keeps the cube as its shadow, names it. Undoable.
        [MenuItem("MetaHackthone/Shadow Workshop/Dog food box on selected cube")]
        static void OnSelected(){foreach(var go in Selection.gameObjects)ApplyTo(go);}
        [MenuItem("MetaHackthone/Shadow Workshop/Dog food box on selected cube",true)]
        static bool CanApply()=>Selection.activeGameObject&&Selection.activeGameObject.GetComponent<MeshFilter>();
        public static void ApplyTo(GameObject go)
        {
            var mf=go.GetComponent<MeshFilter>();var mr=go.GetComponent<MeshRenderer>();if(!mf||!mr)return;
            var cube=Resources.GetBuiltinResource<Mesh>("Cube.fbx");var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath)??BuildMesh();
            Undo.RecordObjects(new Object[]{go,mf,mr},"Dog food box");
            var shadow=mf.sharedMesh&&mf.sharedMesh!=mesh?mf.sharedMesh:cube;
            mf.sharedMesh=mesh;mr.sharedMaterials=Materials();go.name="Dog food box";
            var tag=go.GetComponent<ShadowPiece>();if(!tag)tag=Undo.AddComponent<ShadowPiece>(go);else Undo.RecordObject(tag,"Dog food box");
            tag.role=ShadowPiece.Role.Picture;tag.shadowProxy=shadow;
        }
        public static Material[] Materials()=>PropMesh.Materials(MaterialFolder,new[]{
            new Look("dog food box",Color.white,0,.28f){baseMap=()=>AssetDatabase.LoadAssetAtPath<Texture2D>(PrintPath)??Print()}});

        public static Mesh BuildMesh()
        {
            var b=new PropMesh(1);var half=BoxScale/2;
            var edgeUv=new Vector2((Edge.x+Edge.width/2f)/AtlasW,(Edge.y+Edge.height/2f)/AtlasH);
            ChamferBox(b,half,Chamfer,FaceUv,edgeUv);
            // Into the unit cube's space: positions divided by the scale, normals multiplied (so they come out right once
            // the piece's scale is applied again).
            for(int i=0;i<b.v.Count;i++){b.v[i]=Vector3.Scale(b.v[i],new Vector3(1/BoxScale.x,1/BoxScale.y,1/BoxScale.z));b.n[i]=Vector3.Scale(b.n[i],BoxScale).normalized;}
            return b.Save(MeshPath,"Dog food box");
        }
        // A box of half size h, every edge chamfered by c: a flat quad per face, a quad per edge taking the two faces'
        // mean normal, a triangle per corner. Faces map through faceUv(axis, side, point); edges and corners use edgeUv.
        // Shared with HomeSignBuilder.
        internal static void ChamferBox(PropMesh b,Vector3 h,float c,Func<int,int,Vector3,Vector2> faceUv,Vector2 edgeUv)
        {
            var lo=-h+Vector3.one*c;var hi=h-Vector3.one*c;
            Vector3 P(int a,int s,Vector3Int sign){var p=Vector3.zero;for(int i=0;i<3;i++)p[i]=i==a?s*h[i]:(sign[i]>0?hi[i]:lo[i]);return p;}
            Vector3 N(int a,int s){var n=Vector3.zero;n[a]=s;return n;}
            for(int a=0;a<3;a++)for(int s=-1;s<=1;s+=2)
            {
                int u=(a+1)%3,w=(a+2)%3;var q=new int[4];
                for(int i=0;i<4;i++){var sign=Vector3Int.zero;sign[u]=i==1||i==2?1:-1;sign[w]=i>=2?1:-1;var p=P(a,s,sign);q[i]=b.Add(p,N(a,s),faceUv(a,s,p));}
                b.Quad(0,q[0],q[1],q[2],q[3]);
            }
            for(int a=0;a<3;a++)for(int e=a+1;e<3;e++)for(int sa=-1;sa<=1;sa+=2)for(int se=-1;se<=1;se+=2)
            {
                int along=3-a-e;var low=Vector3Int.zero;low[a]=sa;low[e]=se;low[along]=-1;var high=low;high[along]=1;var n=(N(a,sa)+N(e,se)).normalized;
                b.Quad(0,b.Add(P(a,sa,low),n,edgeUv),b.Add(P(a,sa,high),n,edgeUv),b.Add(P(e,se,high),n,edgeUv),b.Add(P(e,se,low),n,edgeUv));
            }
            for(int sx=-1;sx<=1;sx+=2)for(int sy=-1;sy<=1;sy+=2)for(int sz=-1;sz<=1;sz+=2)
            {
                var sign=new Vector3Int(sx,sy,sz);var n=new Vector3(sx,sy,sz).normalized;
                b.Tri(0,b.Add(P(0,sx,sign),n,edgeUv),b.Add(P(1,sy,sign),n,edgeUv),b.Add(P(2,sz,sign),n,edgeUv));
            }
        }
        // Each face seen from outside, u to the right and v up, in mm from its bottom left: the front is -z (upright),
        // the back +z, the sides +-x (upright), the top +y and the bottom -y (both with the back edge up).
        static Vector2 FaceUv(int axis,int side,Vector3 p)
        {
            var m=p*1000;var s=BoxScale*500;RectInt r;float u,v;
            if(axis==2){r=side<0?Front:Back;u=side<0?m.x+s.x:s.x-m.x;v=m.y+s.y;}
            else if(axis==0){r=side>0?Right:Left;u=side>0?m.z+s.z:s.z-m.z;v=m.y+s.y;}
            else{r=side>0?Top:Bottom;u=side>0?m.x+s.x:s.x-m.x;v=m.z+s.z;}
            return new Vector2((r.x+u*PxPerMm)/AtlasW,(r.y+v*PxPerMm)/AtlasH);
        }

        // --- The print ---------------------------------------------------------------------------------------------------
        static readonly Color Red=new Color(.74f,.13f,.10f),DarkRed=new Color(.50f,.08f,.06f),Yellow=new Color(.99f,.77f,.22f),Orange=new Color(.96f,.57f,.14f),
            Cream=new Color(.99f,.94f,.82f),Brown=new Color(.58f,.34f,.16f),DarkBrown=new Color(.36f,.20f,.09f),Tan=new Color(.91f,.75f,.53f),
            Ink=new Color(.09f,.07f,.06f),Pink=new Color(.93f,.46f,.50f),Paw=new Color(.86f,.44f,.10f),Panel=new Color(.98f,.96f,.92f),Line=new Color(.72f,.70f,.66f);
        static Texture2D Print()
        {
            var px=new Color[AtlasW*AtlasH];for(int i=0;i<px.Length;i++)px[i]=Orange;
            Paint(px,Front,FrontArt);Paint(px,Back,BackArt);Paint(px,Left,SideArt);Paint(px,Right,SideArt);Paint(px,Top,TopArt);Paint(px,Bottom,BottomArt);
            for(int y=Edge.y;y<Edge.yMax;y++)for(int x=Edge.x;x<Edge.xMax;x++)px[y*AtlasW+x]=DarkRed;
            var tex=new Texture2D(AtlasW,AtlasH,TextureFormat.RGB24,false,false);tex.SetPixels(px);
            var saved=SaveTexture(PrintPath,tex,false,TextureWrapMode.Clamp);
            return saved;
        }
        // Paints a face region (and 3 px round it, so filtering at its edges stays on its colours) from art(u, v, w, h) in mm.
        static void Paint(Color[] px,RectInt r,Func<float,float,float,float,Color> art)
        {
            float w=r.width/PxPerMm,h=r.height/PxPerMm;
            for(int y=r.y-3;y<r.yMax+3;y++)for(int x=r.x-3;x<r.xMax+3;x++)
            {
                if(x<0||y<0||x>=AtlasW||y>=AtlasH)continue;
                float u=Mathf.Clamp((x+.5f-r.x)/PxPerMm,0,w),v=Mathf.Clamp((y+.5f-r.y)/PxPerMm,0,h);
                var c=art(u,v,w,h);c.a=1;px[y*AtlasW+x]=c;
            }
        }
        // Coverage of a shape from its signed distance (mm, negative inside), anti-aliased over about a pixel (pxPerMm).
        // These shape helpers are shared with HomeSignBuilder.
        internal static float Cover(float d,float pxPerMm=PxPerMm)=>Mathf.Clamp01(.5f-d*pxPerMm);
        internal static Color Over(Color under,Color c,float a)=>Color.Lerp(under,c,a);
        internal static float Ellipse(float u,float v,float cx,float cy,float rx,float ry,float turn=0)
        {
            float x=u-cx,y=v-cy,cs=Mathf.Cos(turn*Mathf.Deg2Rad),sn=Mathf.Sin(turn*Mathf.Deg2Rad);float px=x*cs+y*sn,py=-x*sn+y*cs;
            return (new Vector2(px/rx,py/ry).magnitude-1)*Mathf.Min(rx,ry);
        }
        internal static float Box(float u,float v,float x0,float y0,float x1,float y1,float round)
        {
            float cx=(x0+x1)/2,cy=(y0+y1)/2,hx=(x1-x0)/2-round,hy=(y1-y0)/2-round;
            var q=new Vector2(Mathf.Abs(u-cx)-hx,Mathf.Abs(v-cy)-hy);return new Vector2(Mathf.Max(q.x,0),Mathf.Max(q.y,0)).magnitude+Mathf.Min(Mathf.Max(q.x,q.y),0)-round;
        }
        // Background shared round the box: yellow fading to orange, a red band at the top with a cream rule, a red foot.
        static Color Ground(float u,float v,float w,float h)
        {
            var c=Color.Lerp(Orange,Yellow,v/h)*(.97f+.05f*Fbm(u/w,v/h,6,8,2,5));
            if(v>h-42)c=Red;else if(v>h-45)c=Cream;
            if(v<10)c=Red;else if(v<12)c=Cream;
            return c;
        }
        static Color FrontArt(float u,float v,float w,float h)
        {
            var c=Ground(u,v,w,h);c=Text(c,u,v,"DOG FOOD",w/2,h-21,2.6f,Cream);
            c=PawPrint(c,u,v,18,150,.9f,25);c=PawPrint(c,u,v,w-18,128,.9f,-20);
            // Badge with a floppy-eared dog.
            float cx=w/2,cy=110;
            c=Over(c,DarkRed,Cover(Ellipse(u,v,cx,cy,42,42)));c=Over(c,Cream,Cover(Ellipse(u,v,cx,cy,39,39)));
            c=Over(c,DarkBrown,Cover(Ellipse(u,v,cx-19,cy+1,9,18,-22)));c=Over(c,DarkBrown,Cover(Ellipse(u,v,cx+19,cy+1,9,18,22)));
            c=Over(c,Brown,Cover(Ellipse(u,v,cx,cy+3,21,20)));
            c=Over(c,Pink,Cover(Ellipse(u,v,cx,cy-17,4.2f,5.5f)));
            c=Over(c,Tan,Cover(Ellipse(u,v,cx,cy-8,13,9.5f)));
            c=Over(c,Ink,Cover(Ellipse(u,v,cx,cy-3.5f,5,3.8f)));
            c=Over(c,Ink,Cover(Box(u,v,cx-.5f,cy-11.5f,cx+.5f,cy-6,.4f)));
            foreach(var ex in new[]{-8.5f,8.5f}){c=Over(c,Ink,Cover(Ellipse(u,v,cx+ex,cy+9,3,3.2f)));c=Over(c,Color.white,Cover(Ellipse(u,v,cx+ex+1,cy+10.2f,1,1)));}
            // A bowl heaped with kibble.
            float bx=w/2,by=40;
            for(int i=0;i<9;i++){float kx=bx-22+i*5.5f,ky=by+4+3.5f*Mathf.Sin(i*1.7f)+(i>1&&i<7?4:0);c=Over(c,i%3==0?DarkBrown:Brown,Cover(Ellipse(u,v,kx,ky,3.4f,2.8f,i*37)));}
            for(int i=0;i<5;i++){float kx=bx-12+i*6,ky=by+11+2*Mathf.Sin(i*2.3f);c=Over(c,i%2==0?Brown:DarkBrown,Cover(Ellipse(u,v,kx,ky,3.2f,2.7f,i*53)));}
            c=Over(c,Red,Cover(Mathf.Max(Ellipse(u,v,bx,by,31,17),v-by)));
            c=Over(c,DarkRed,Cover(Box(u,v,bx-33,by-1,bx+33,by+2.5f,1.2f)));
            c=Over(c,Cream,Cover(Ellipse(u,v,bx-14,by-7,4,1.6f,-15))*.6f);
            return c;
        }
        static Color BackArt(float u,float v,float w,float h)
        {
            var c=Ground(u,v,w,h);c=Text(c,u,v,"DOG FOOD",w/2,h-21,2.6f,Cream);
            // Ingredients panel: a header bar and rows of grey lines.
            float x0=12,x1=w-12,y0=44,y1=h-56;
            c=Over(c,Panel,Cover(Box(u,v,x0,y0,x1,y1,3)));c=Over(c,Red,Cover(Box(u,v,x0+5,y1-13,x1-5,y1-5,1.5f)));
            for(int row=0;row<11;row++){float ly=y1-22-row*8.5f;if(ly<y0+6)break;float len=(row*37%5)*9+40;c=Over(c,Line,Cover(Box(u,v,x0+7,ly-1.3f,Mathf.Min(x0+7+len,x1-7),ly+1.3f,1.2f)));}
            // Barcode.
            float bx=w/2-26;c=Over(c,Color.white,Cover(Box(u,v,bx,15,bx+52,37,1)));
            for(int i=0;i<34;i++){float x=bx+4+i*1.32f;if(((i*7+3)%5)<2)continue;c=Over(c,Ink,Cover(Box(u,v,x,19,x+(i%3==0?.9f:.5f),35,0)));}
            return c;
        }
        static Color SideArt(float u,float v,float w,float h)
        {
            var c=Ground(u,v,w,h);
            for(int i=0;i<5;i++){bool l=i%2==0;c=PawPrint(c,u,v,w/2+(l?-10:10),28+i*27,1.2f,l?-12:12);}
            return Text(c,u,v,"DOG",w/2,h-21,2.6f,Cream);
        }
        static Color TopArt(float u,float v,float w,float h)
        {
            var c=Red*(.97f+.05f*Fbm(u/w,v/h,8,4,2,9));
            c=Over(c,DarkRed,Cover(Mathf.Abs(v-h/2)-.5f));                                        // where the flaps meet
            for(int i=0;i<16;i++){float x=6+i*8.2f;c=Over(c,Cream,Cover(Box(u,v,x,12,x+4.5f,13.2f,.5f)));}   // tear strip
            return PawPrint(c,u,v,w-20,h-20,.8f,0,Cream);
        }
        static Color BottomArt(float u,float v,float w,float h)
        {
            var c=Orange*(.92f+.05f*Fbm(u/w,v/h,8,4,2,11));
            c=Over(c,new Color(.80f,.45f,.12f),Cover(Mathf.Abs(v-h/2)-.6f));
            return Over(c,new Color(.80f,.45f,.12f),Cover(Mathf.Min(Mathf.Abs(u-20),Mathf.Abs(u-w+20))-.6f));
        }
        static Color PawPrint(Color c,float u,float v,float x,float y,float s,float turn,Color? colour=null)
        {
            var col=colour??Paw;float cs=Mathf.Cos(turn*Mathf.Deg2Rad),sn=Mathf.Sin(turn*Mathf.Deg2Rad);
            Vector2 At(float dx,float dy)=>new Vector2(x+(dx*cs-dy*sn)*s,y+(dx*sn+dy*cs)*s);
            var pad=At(0,-3);float d=Ellipse(u,v,pad.x,pad.y,6*s,5*s,turn);
            foreach(var t in new[]{new Vector2(-6.5f,3.5f),new Vector2(-2.3f,6.8f),new Vector2(2.3f,6.8f),new Vector2(6.5f,3.5f)}){var p=At(t.x,t.y);d=Mathf.Min(d,Ellipse(u,v,p.x,p.y,2.3f*s,2.6f*s,turn));}
            return Over(c,col,Cover(d));
        }
        // Block capitals from 5 x 7 cells (cell mm wide), centred on (cx, cy).
        static readonly System.Collections.Generic.Dictionary<char,string[]> Glyphs=new System.Collections.Generic.Dictionary<char,string[]>{
            {'D',new[]{"11110","10001","10001","10001","10001","10001","11110"}},{'O',new[]{"01110","10001","10001","10001","10001","10001","01110"}},
            {'G',new[]{"01110","10001","10000","10111","10001","10001","01111"}},{'F',new[]{"11111","10000","10000","11110","10000","10000","10000"}}};
        static Color Text(Color c,float u,float v,string text,float cx,float cy,float cell,Color col)
        {
            float width=0;foreach(var ch in text)width+=ch==' '?3:6;width=(width-1)*cell;
            float x=cx-width/2,top=cy+3.5f*cell,d=float.MaxValue;
            foreach(var ch in text)
            {
                if(ch==' '){x+=3*cell;continue;}
                var g=Glyphs[ch];
                // Cells overlap a fifth of a cell so strokes run solid; only the strokes' outer corners round off.
                for(int row=0;row<7;row++)for(int k=0;k<5;k++)if(g[row][k]=='1'){float x0=x+k*cell,y1=top-row*cell,o=cell*.2f;d=Mathf.Min(d,Box(u,v,x0-o,y1-cell-o,x0+cell+o,y1+o,cell*.25f));}
                x+=6*cell;
            }
            return Over(c,col,Cover(d));
        }
    }
}
