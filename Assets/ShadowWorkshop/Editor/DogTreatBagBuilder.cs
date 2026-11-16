using System;
using System.Collections.Generic;
using CloudGarden.ShadowPuzzle;
using UnityEditor;
using UnityEngine;
using static ShadowWorkshopIntegration.Editor.PropMesh;
using Object=UnityEngine.Object;

namespace ShadowWorkshopIntegration.Editor
{
    // A small bag of dog treats to stand in for Home's standing picture frame (picture_frame.obj, the gym's 'default': 85 mm
    // wide, 109 mm tall, leaning back about 12.5 degrees on a kickstand). Drawn only: the frame stays the piece's shadow
    // (ShadowPiece.shadowProxy in the gym, Torch2Piece.shadowMesh in levels), so both walls' pictures are unchanged. The bag
    // is built in the frame's own space, filling its face at its lean: a pillow pouch puffed to 20 mm in the middle,
    // pinched at the sides, with crimped seals top and bottom and a hang hole. About 650 triangles (mobile VR), one
    // material; the detail is a 1024 x 512 print drawn here: the front navy with DOG over a cream bone reading TREATS and a
    // window full of treats, the back a feeding panel and a barcode. A mirrored twin (u flipped) is used on pieces with a
    // mirrored scale, like the gym's frame at x -1, so the print still reads the right way round.
    public static class DogTreatBagBuilder
    {
        const string Art="Assets/ShadowWorkshop/Art/Torch2/";
        public const string MeshPath=Art+"Meshes/Dog treat bag.asset",MirroredMeshPath=Art+"Meshes/Dog treat bag mirrored.asset";
        const string PrintPath=Art+"Textures/dog treat bag.png",MaterialFolder=Art+"Materials/";

        // The bag in the frame's space: its middle, its lean (top toward -z, the frame's kickstand side) and its size.
        static readonly Vector3 Centre=new Vector3(0,.0545f,.0115f);
        const float Lean=12.5f,Width=.085f,Height=.108f,Seal=.009f,Puff=.010f,Fin=.0006f;
        const int Columns=10,PanelRows=12;
        const float PxPerMm=4.4f;const int AtlasW=1024,AtlasH=512;
        static readonly RectInt Front=new RectInt(8,18,374,475),Back=new RectInt(398,18,374,475),Edge=new RectInt(1000,480,16,16);

        [MenuItem("MetaHackthone/Shadow Workshop/Build dog treat bag")]
        public static void Build()
        {
            Print();var mesh=BuildMesh(false);BuildMesh(true);Materials();
            Debug.Log($"DOG_TREAT_BAG_OK {mesh.triangles.Length/3} tris, {mesh.bounds.size*1000} mm");
        }
        // Draws the bag on the selected frame, keeps the frame as its shadow, names it. Undoable.
        [MenuItem("MetaHackthone/Shadow Workshop/Dog treat bag on selected frame")]
        static void OnSelected(){foreach(var go in Selection.gameObjects)ApplyTo(go);}
        [MenuItem("MetaHackthone/Shadow Workshop/Dog treat bag on selected frame",true)]
        static bool CanApply()=>Selection.activeGameObject&&Selection.activeGameObject.GetComponent<MeshFilter>();

        public static void ApplyTo(GameObject go)
        {
            var mf=go.GetComponent<MeshFilter>();var mr=go.GetComponent<MeshRenderer>();if(!mf||!mr)return;
            var s=go.transform.lossyScale;bool mirrored=s.x*s.y*s.z<0;
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(mirrored?MirroredMeshPath:MeshPath);if(!mesh){Print();mesh=BuildMesh(mirrored);}
            var tag=go.GetComponent<ShadowPiece>();
            bool isBag=mf.sharedMesh&&mf.sharedMesh.name.StartsWith("Dog treat bag");
            var shadow=isBag&&tag&&tag.shadowProxy?tag.shadowProxy:mf.sharedMesh;
            Undo.RecordObjects(new Object[]{go,mf,mr},"Dog treat bag");
            mf.sharedMesh=mesh;mr.sharedMaterials=Materials();go.name="Dog treat bag";
            if(!tag)tag=Undo.AddComponent<ShadowPiece>(go);else Undo.RecordObject(tag,"Dog treat bag");
            tag.role=ShadowPiece.Role.Picture;tag.shadowProxy=shadow;
            EditorUtility.SetDirty(go);
        }
        public static Material[] Materials()=>PropMesh.Materials(MaterialFolder,new[]{
            new Look("dog treat bag",Color.white,0,.5f){baseMap=()=>AssetDatabase.LoadAssetAtPath<Texture2D>(PrintPath)??Print()}});

        // --- The bag --------------------------------------------------------------------------------------------------
        static readonly Vector3 Right=Vector3.right;
        static Vector3 Up=>new Vector3(0,Mathf.Cos(Lean*Mathf.Deg2Rad),-Mathf.Sin(Lean*Mathf.Deg2Rad));
        static Vector3 Face=>new Vector3(0,Mathf.Sin(Lean*Mathf.Deg2Rad),Mathf.Cos(Lean*Mathf.Deg2Rad));
        // How far the panel stands off its middle plane at s (-1..1 across) and t (0..1 up): rising quickly off the seals
        // and the pinched sides, full in the middle.
        static float Depth(float s,float t)
        {
            float lo=Seal/Height,hi=1-lo;if(t<=lo||t>=hi)return Fin;
            float along=Mathf.Pow(Mathf.Sin(Mathf.PI*(t-lo)/(hi-lo)),.55f),across=Mathf.Pow(Mathf.Max(0,1-s*s),.4f);
            return Fin+Puff*along*across;
        }
        // A point on one side (+1 the printed front, -1 the back): the panel pulls in a little at the sides as it puffs.
        static Vector3 Point(float s,float t,int side)
        {
            float lo=Seal/Height,hi=1-lo,full=t>lo&&t<hi?Mathf.Pow(Mathf.Sin(Mathf.PI*(t-lo)/(hi-lo)),.55f):0;
            return Centre+Right*(s*Width/2*(1-.05f*full))+Up*((t-.5f)*Height)+Face*(side*Depth(s,t));
        }
        static Vector3 Normal(float s,float t,int side)
        {
            float lo=Seal/Height,hi=1-lo;if(t<=lo||t>=hi)return Face*side;
            const float e=.002f;float s0=Mathf.Max(-1,s-e),s1=Mathf.Min(1,s+e),t0=Mathf.Max(lo,t-e),t1=Mathf.Min(hi,t+e);
            // The tangents are normalised first: their cross product (fractions of a millimetre squared) is under the size
            // Vector3.normalized treats as zero.
            var n=Vector3.Cross((Point(s1,t,side)-Point(s0,t,side)).normalized,(Point(s,t1,side)-Point(s,t0,side)).normalized).normalized*side;
            return Vector3.Dot(n,Face*side)<0?-n:n;
        }
        // Print coordinates (mm from the face's bottom left, seen from outside) to the atlas. Seen from the front (+z side)
        // its right is -x; seen from the back it is +x.
        static Vector2 Uv(float s,float t,int side,bool mirrored)
        {
            float u=(1-s*side)/2*Width*1000;if(mirrored)u=Width*1000-u;var r=side>0?Front:Back;
            return new Vector2((r.x+u*PxPerMm)/AtlasW,(r.y+t*Height*1000*PxPerMm)/AtlasH);
        }

        public static Mesh BuildMesh(bool mirrored)
        {
            var b=new PropMesh(1);float lo=Seal/Height,hi=1-lo;
            var ts=new List<float>{0};for(int i=0;i<=PanelRows;i++)ts.Add(Mathf.Lerp(lo,hi,i/(float)PanelRows));ts.Add(1);
            foreach(var side in new[]{1,-1})
            {
                // The puffed panel, smooth; the two seals flat, with their own vertices so the crimp creases sharply.
                var grid=new int[Columns+1,PanelRows+1];
                for(int j=0;j<=PanelRows;j++)for(int i=0;i<=Columns;i++)
                {
                    float s=-1+2f*i/Columns,t=ts[j+1];grid[i,j]=b.Add(Point(s,t,side),Normal(s,t,side),Uv(s,t,side,mirrored));
                }
                for(int j=0;j<PanelRows;j++)for(int i=0;i<Columns;i++)b.Quad(0,grid[i,j],grid[i+1,j],grid[i+1,j+1],grid[i,j+1]);
                foreach(var (t0,t1) in new[]{(0f,lo),(hi,1f)})
                {
                    int start=b.v.Count;
                    for(int i=0;i<=Columns;i++){float s=-1+2f*i/Columns;b.Add(Point(s,t0,side),Face*side,Uv(s,t0,side,mirrored));b.Add(Point(s,t1,side),Face*side,Uv(s,t1,side,mirrored));}
                    for(int i=0;i<Columns;i++){int q=start+i*2;b.Quad(0,q,q+2,q+3,q+1);}
                }
            }
            // The rim: a thin strip closing the gap between the two sides all round, in the seal colour.
            var edgeUv=new Vector2((Edge.x+Edge.width/2f)/AtlasW,(Edge.y+Edge.height/2f)/AtlasH);
            var loop=new List<(float s,float t,Vector3 n)>();
            for(int j=0;j<ts.Count;j++)loop.Add((1,ts[j],Right));
            for(int i=Columns;i>=0;i--)loop.Add((-1+2f*i/Columns,1,Up));
            for(int j=ts.Count-1;j>=0;j--)loop.Add((-1,ts[j],-Right));
            for(int i=0;i<=Columns;i++)loop.Add((-1+2f*i/Columns,0,-Up));
            for(int k=0;k+1<loop.Count;k++)
            {
                var (sa,ta,na)=loop[k];var (sb,tb,nb)=loop[k+1];if(na!=nb)continue;
                b.Quad(0,b.Add(Point(sa,ta,1),na,edgeUv),b.Add(Point(sb,tb,1),na,edgeUv),b.Add(Point(sb,tb,-1),na,edgeUv),b.Add(Point(sa,ta,-1),na,edgeUv));
            }
            return b.Save(mirrored?MirroredMeshPath:MeshPath,mirrored?"Dog treat bag mirrored":"Dog treat bag");
        }

        // --- The print ---------------------------------------------------------------------------------------------------
        static readonly Color Navy=new Color(.13f,.24f,.50f),DeepNavy=new Color(.06f,.12f,.28f),SealBlue=new Color(.27f,.43f,.72f),
            Orange=new Color(.97f,.55f,.13f),Cream=new Color(.99f,.94f,.80f),Biscuit=new Color(.91f,.76f,.52f),Brown=new Color(.58f,.34f,.16f),
            DarkBrown=new Color(.36f,.20f,.09f),Red=new Color(.72f,.17f,.12f),Window=new Color(.95f,.90f,.80f),Ink=new Color(.09f,.07f,.06f),
            Panel=new Color(.98f,.96f,.92f),Line=new Color(.72f,.70f,.66f);
        static Texture2D Print()
        {
            var px=new Color[AtlasW*AtlasH];for(int i=0;i<px.Length;i++)px[i]=SealBlue;
            Paint(px,Front,FrontArt);Paint(px,Back,BackArt);
            for(int y=Edge.y;y<Edge.yMax;y++)for(int x=Edge.x;x<Edge.xMax;x++)px[y*AtlasW+x]=SealBlue;
            var tex=new Texture2D(AtlasW,AtlasH,TextureFormat.RGB24,false,false);tex.SetPixels(px);
            return SaveTexture(PrintPath,tex,false,TextureWrapMode.Clamp);
        }
        // Paints a face (and 3 px round it, so filtering at its edges keeps its colours) from art(u, v, w, h) in mm.
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
        static float Cover(float d)=>Mathf.Clamp01(.5f-d*PxPerMm);
        static Color Over(Color under,Color c,float a)=>Color.Lerp(under,c,a);
        static float Circle(float u,float v,float cx,float cy,float r)=>new Vector2(u-cx,v-cy).magnitude-r;
        static float Ellipse(float u,float v,float cx,float cy,float rx,float ry,float turn=0)
        {
            float x=u-cx,y=v-cy,cs=Mathf.Cos(turn*Mathf.Deg2Rad),sn=Mathf.Sin(turn*Mathf.Deg2Rad);float px=x*cs+y*sn,py=-x*sn+y*cs;
            return (new Vector2(px/rx,py/ry).magnitude-1)*Mathf.Min(rx,ry);
        }
        static float Box(float u,float v,float x0,float y0,float x1,float y1,float round)
        {
            float cx=(x0+x1)/2,cy=(y0+y1)/2,hx=(x1-x0)/2-round,hy=(y1-y0)/2-round;
            var q=new Vector2(Mathf.Abs(u-cx)-hx,Mathf.Abs(v-cy)-hy);return new Vector2(Mathf.Max(q.x,0),Mathf.Max(q.y,0)).magnitude+Mathf.Min(Mathf.Max(q.x,q.y),0)-round;
        }
        // A dog bone len long and thick across (a bar with two round knobs at each end), turned by turn degrees.
        static float Bone(float u,float v,float cx,float cy,float len,float thick,float turn)
        {
            float cs=Mathf.Cos(turn*Mathf.Deg2Rad),sn=Mathf.Sin(turn*Mathf.Deg2Rad),x=(u-cx)*cs+(v-cy)*sn,y=-(u-cx)*sn+(v-cy)*cs;
            float knob=thick*.52f,end=len/2-knob,d=Box(x,y,-end,-thick*.35f,end,thick*.35f,0);
            foreach(var ex in new[]{-end,end})foreach(var ey in new[]{-knob*.75f,knob*.75f})d=Mathf.Min(d,Circle(x,y,ex,ey,knob));
            return d;
        }
        // The bag's ground: navy with a soft sheen up the middle, and the crimped seals with a hang hole in the top one.
        static Color Ground(float u,float v,float w,float h)
        {
            if(v<Seal*1000||v>h-Seal*1000)
            {
                float crimp=Mathf.Repeat(u,1.4f)<.55f?.82f:1f;var c=SealBlue*crimp;
                if(Mathf.Abs(v-Seal*1000)<.5f||Mathf.Abs(v-(h-Seal*1000))<.5f)c=DeepNavy;
                return Over(c,DeepNavy*.6f,Cover(Box(u,v,w/2-6,h-6.3f,w/2+6,h-2.7f,1.8f)));
            }
            float sheen=1-Mathf.Abs(u/w-.5f)*2;
            return Color.Lerp(DeepNavy,Navy,.55f+.45f*sheen)*(.96f+.06f*Fbm(u/w,v/h,6,8,2,13));
        }
        static Color FrontArt(float u,float v,float w,float h)
        {
            var c=Ground(u,v,w,h);if(v<Seal*1000||v>h-Seal*1000)return c;
            c=Text(c,u,v,"DOG",w/2,89,2.3f,Orange);
            // The cream bone with TREATS on it, outlined in orange.
            c=Over(c,Orange,Cover(Bone(u,v,w/2,61,72,20,0)-1.1f));c=Over(c,Cream,Cover(Bone(u,v,w/2,61,72,20,0)));
            c=Text(c,u,v,"TREATS",w/2,61,1.35f,Navy);
            // A window onto the treats: small bones in three colours behind a clear panel with a white rim and a glint.
            float wx0=w/2-24,wx1=w/2+24,wy0=17,wy1=39;
            c=Over(c,Color.white,Cover(Box(u,v,wx0-1,wy0-1,wx1+1,wy1+1,5)));
            if(Box(u,v,wx0,wy0,wx1,wy1,4.2f)<.5f)
            {
                var inside=Window;
                for(int i=0;i<11;i++)
                {
                    float bx=wx0+4+(i*7.9f)%44,by=wy0+4+(i*5.3f)%15,turn=i*47;var col=i%3==0?Red:i%3==1?Brown:Biscuit;
                    float d=Bone(u,v,bx,by,11,4.6f,turn);inside=Over(inside,DarkBrown,Cover(d-.35f));inside=Over(inside,col,Cover(d));
                }
                c=Over(c,inside,Cover(Box(u,v,wx0,wy0,wx1,wy1,4.2f)));
                c=Over(c,Color.white,Cover(Box(u,v,wx0+3,wy1-6,wx0+15,wy1-4.6f,.7f))*.7f);
            }
            c=PawPrint(c,u,v,9,17,.5f,-15);c=PawPrint(c,u,v,w-9,17,.5f,15);
            return c;
        }
        static Color BackArt(float u,float v,float w,float h)
        {
            var c=Ground(u,v,w,h);if(v<Seal*1000||v>h-Seal*1000)return c;
            c=Text(c,u,v,"DOG TREATS",w/2,89,1.15f,Orange);
            // Feeding guide: a cream panel with an orange header and rows of grey lines.
            float x0=8,x1=w-8,y0=34,y1=80;
            c=Over(c,Panel,Cover(Box(u,v,x0,y0,x1,y1,3)));c=Over(c,Orange,Cover(Box(u,v,x0+3,y1-9,x1-3,y1-3,1.5f)));
            for(int row=0;row<7;row++){float ly=y1-15-row*5.5f;if(ly<y0+4)break;float len=(row*37%5)*7+30;c=Over(c,Line,Cover(Box(u,v,x0+5,ly-.8f,Mathf.Min(x0+5+len,x1-5),ly+.8f,.8f)));}
            // Barcode.
            float bx=w/2-20;c=Over(c,Color.white,Cover(Box(u,v,bx,13,bx+40,29,1)));
            for(int i=0;i<28;i++){float x=bx+3+i*1.22f;if(((i*7+3)%5)<2)continue;c=Over(c,Ink,Cover(Box(u,v,x,16,x+(i%3==0?.8f:.45f),27,0)));}
            return PawPrint(c,u,v,w-11,20,.6f,10);
        }
        static Color PawPrint(Color c,float u,float v,float x,float y,float s,float turn)
        {
            float cs=Mathf.Cos(turn*Mathf.Deg2Rad),sn=Mathf.Sin(turn*Mathf.Deg2Rad);
            Vector2 At(float dx,float dy)=>new Vector2(x+(dx*cs-dy*sn)*s,y+(dx*sn+dy*cs)*s);
            var pad=At(0,-3);float d=Ellipse(u,v,pad.x,pad.y,6*s,5*s,turn);
            foreach(var t in new[]{new Vector2(-6.5f,3.5f),new Vector2(-2.3f,6.8f),new Vector2(2.3f,6.8f),new Vector2(6.5f,3.5f)}){var p=At(t.x,t.y);d=Mathf.Min(d,Ellipse(u,v,p.x,p.y,2.3f*s,2.6f*s,turn));}
            return Over(c,Orange,Cover(d));
        }
        // Block capitals from 5 x 7 cells (cell mm wide), centred on (cx, cy).
        static readonly Dictionary<char,string[]> Glyphs=new Dictionary<char,string[]>{
            {'D',new[]{"11110","10001","10001","10001","10001","10001","11110"}},{'O',new[]{"01110","10001","10001","10001","10001","10001","01110"}},
            {'G',new[]{"01110","10001","10000","10111","10001","10001","01111"}},{'T',new[]{"11111","00100","00100","00100","00100","00100","00100"}},
            {'R',new[]{"11110","10001","10001","11110","10100","10010","10001"}},{'E',new[]{"11111","10000","10000","11110","10000","10000","11111"}},
            {'A',new[]{"01110","10001","10001","11111","10001","10001","10001"}},{'S',new[]{"01111","10000","10000","01110","00001","00001","11110"}}};
        static Color Text(Color c,float u,float v,string text,float cx,float cy,float cell,Color col)
        {
            float width=0;foreach(var ch in text)width+=ch==' '?3:6;width=(width-1)*cell;
            float x=cx-width/2,top=cy+3.5f*cell,d=float.MaxValue;
            if(u<x-cell||u>x+width+cell||v<top-8*cell||v>top+cell)return c;
            foreach(var ch in text)
            {
                if(ch==' '){x+=3*cell;continue;}
                var g=Glyphs[ch];
                for(int row=0;row<7;row++)for(int k=0;k<5;k++)if(g[row][k]=='1'){float x0=x+k*cell,y1=top-row*cell,o=cell*.2f;d=Mathf.Min(d,Box(u,v,x0-o,y1-cell-o,x0+cell+o,y1+o,cell*.25f));}
                x+=6*cell;
            }
            return Over(c,col,Cover(d));
        }
    }
}
