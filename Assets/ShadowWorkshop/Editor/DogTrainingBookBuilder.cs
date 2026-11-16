using System;
using System.Collections.Generic;
using CloudGarden.ShadowPuzzle;
using UnityEditor;
using UnityEngine;
using static ShadowWorkshopIntegration.Editor.PropMesh;

namespace ShadowWorkshopIntegration.Editor
{
    // Home's open book (lying open face down, a tent) made a dog training book: GOOD DOG on a teal front cover with an
    // orange paw badge, PUPPY CLASS over the title and STEP BY STEP under it, a blurb and barcode on the back, a rounded
    // spine, and printed pages inside the tent. The same outline as the 48-triangle box book it stands in for
    // (Meshes/Open book), which stays its shadow (ShadowPiece.shadowProxy in the gym, Torch2Piece.shadowMesh in levels),
    // so the puzzle is unchanged. 76 triangles, one material with a 1024 x 512 print, for mobile VR.
    // Home's piece is mirrored (scale x -1) and squashed (z 0.875): the mesh is built mirrored and the print drawn to
    // the squashed size, so the cover reads the right way round and at the right shape there.
    public static class DogTrainingBookBuilder
    {
        const string Art="Assets/ShadowWorkshop/Art/Torch2/";
        public const string MeshPath=Art+"Meshes/Dog training book.asset",OriginalPath=Art+"Meshes/Open book.asset";
        const string PrintPath=Art+"Textures/dog training book.png",MaterialFolder=Art+"Materials/";
        const float ZScale=.875f;   // Home's piece's z scale

        [MenuItem("MetaHackthone/Shadow Workshop/Build dog training book")]
        public static void Build()
        {
            Print();var mesh=BuildMesh();Materials();
            Debug.Log($"DOG_TRAINING_BOOK_OK {mesh.triangles.Length/3} tris, {mesh.vertexCount} verts, {mesh.bounds.size*100} cm");
        }
        [MenuItem("MetaHackthone/Shadow Workshop/Make selected a dog training book")]
        static void MakeSelected()=>Apply(Selection.activeTransform);
        [MenuItem("MetaHackthone/Shadow Workshop/Make selected a dog training book",true)]
        static bool CanMake()=>Selection.activeTransform&&Selection.activeTransform.GetComponent<MeshFilter>();

        public static Mesh Original=>AssetDatabase.LoadAssetAtPath<Mesh>(OriginalPath);
        public static Material[] Materials()=>PropMesh.Materials(MaterialFolder,new[]{
            new Look("dog training book",Color.white,0,.25f){baseMap=()=>AssetDatabase.LoadAssetAtPath<Texture2D>(PrintPath)??Print()}});

        // Turns target (the open book) into the training book in place, undoably; its old shadow stays its shadow.
        public static void Apply(Transform target)
        {
            if(!AssetDatabase.LoadAssetAtPath<Texture2D>(PrintPath))Print();
            var mesh=BuildMesh();var materials=Materials();
            var filter=target.GetComponent<MeshFilter>();var renderer=target.GetComponent<MeshRenderer>();
            var tag=target.GetComponent<ShadowPiece>();var shadow=tag&&tag.shadowProxy?tag.shadowProxy:Original;
            Undo.RecordObjects(new UnityEngine.Object[]{target.gameObject,filter,renderer},"Make dog training book");
            target.name="Dog training book";filter.sharedMesh=mesh;renderer.sharedMaterials=materials;
            if(!tag)tag=Undo.AddComponent<ShadowPiece>(target.gameObject);else Undo.RecordObject(tag,"Make dog training book");
            tag.role=ShadowPiece.Role.Picture;tag.shadowProxy=shadow;
            EditorUtility.SetDirty(target.gameObject);
        }

        // --- The book ----------------------------------------------------------------------------------------------------
        // The original's right board, seen end on (x, y): its outer face runs from the spine at Top down to Foot; the
        // left board is its mirror. Measured along a board: s from Top, t out of its outer face (negative inwards).
        static readonly Vector2 Top=new Vector2(.0017f,.0444f),Foot=new Vector2(.1260f,-.0395f);
        const float Z=.055f,Zb=.052f;            // half length along the spine: covers, pages
        const float Tc=.0018f,Tb=.0058f;         // cover board thickness; the pages reach to Tb (the original's depth)
        const float Sp=.004f,Inset=.003f;        // where the boards turn into the round spine; pages short of the foot
        const int K=4;                           // spine segments
        static float L=>(Foot-Top).magnitude;
        static Vector2 D=>(Foot-Top).normalized;
        static Vector2 Out=>new Vector2(-D.y,D.x);
        static Vector2 Local(float s,float t)=>Top+D*s+Out*t;
        static Vector2 Side(Vector2 p,int side)=>new Vector2(p.x*side,p.y);
        // The spine: a circular arc meeting both outer faces tangentially at s = Sp.
        static float SpineR=>Local(Sp,0).x/Out.x;
        static Vector2 SpineC=>new Vector2(0,Local(Sp,0).y-SpineR*Out.y);
        static float Phi0=>Mathf.Atan2(Out.y,Out.x);
        static Vector2 Arc(int k,float r){float a=Mathf.PI-Phi0-k*(Mathf.PI-2*Phi0)/K;return SpineC+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*r;}

        public static Mesh BuildMesh()
        {
            var b=new PropMesh(1);
            // Built as it should look, then mirrored in x to undo the Home piece's x -1.
            int A(Vector2 p,float z,Vector3 n,Vector2 uv)=>b.Add(new Vector3(-p.x,p.y,z),new Vector3(-n.x,n.y,n.z),uv);
            Vector3 N(Vector2 n)=>new Vector3(n.x,n.y,0);
            Vector2 Centre(RectInt r)=>Uv(r,.5f,.5f);

            // Cover: one smooth strip from the back's foot over the spine to the front's foot, printed back, spine, front.
            var outer=new List<Vector2>();var inner=new List<Vector2>();var normal=new List<Vector2>();var u=new List<float>();
            outer.Add(Side(Local(L,0),-1));inner.Add(Side(Local(L,-Tc),-1));normal.Add(Side(Out,-1));u.Add(Back.x);
            for(int k=0;k<=K;k++){outer.Add(Arc(k,SpineR));inner.Add(Arc(k,SpineR-Tc));normal.Add((Arc(k,SpineR)-SpineC).normalized);u.Add(Spine.x+k*(float)Spine.width/K);}
            outer.Add(Local(L,0));inner.Add(Local(L,-Tc));normal.Add(Out);u.Add(Front.xMax);
            for(int i=0;i+1<outer.Count;i++)
            {
                int a0=A(outer[i],-Z,N(normal[i]),new Vector2(u[i]/AtlasW,Front.y/(float)AtlasH)),a1=A(outer[i],Z,N(normal[i]),new Vector2(u[i]/AtlasW,Front.yMax/(float)AtlasH));
                int c0=A(outer[i+1],-Z,N(normal[i+1]),new Vector2(u[i+1]/AtlasW,Front.y/(float)AtlasH)),c1=A(outer[i+1],Z,N(normal[i+1]),new Vector2(u[i+1]/AtlasW,Front.yMax/(float)AtlasH));
                b.Quad(0,a0,c0,c1,a1);
            }
            // Its ends (the board edges and the spine's crescent), the boards' insides and their feet.
            foreach(var z in new[]{-Z,Z})for(int i=0;i+1<outer.Count;i++)
            {
                var n=new Vector3(0,0,Mathf.Sign(z));var e=Centre(Edge);
                b.Quad(0,A(outer[i],z,n,e),A(outer[i+1],z,n,e),A(inner[i+1],z,n,e),A(inner[i],z,n,e));
            }
            // The spine's inside, which shows past the pages' ends.
            for(int k=0;k<K;k++)
            {
                var p=Centre(Endpaper);Vector2 a=Arc(k,SpineR-Tc),c=Arc(k+1,SpineR-Tc);var na=N((SpineC-a).normalized);var nc=N((SpineC-c).normalized);
                b.Quad(0,A(a,-Z,na,p),A(c,-Z,nc,p),A(c,Z,nc,p),A(a,Z,na,p));
            }
            foreach(var side in new[]{-1,1})
            {
                var p=Centre(Endpaper);var inside=N(Side(-Out,side));
                b.Quad(0,A(Side(Local(L,-Tc),side),-Z,inside,p),A(Side(Local(Sp,-Tc),side),-Z,inside,p),A(Side(Local(Sp,-Tc),side),Z,inside,p),A(Side(Local(L,-Tc),side),Z,inside,p));
                var e=Centre(Edge);var foot=N(Side(D,side));
                b.Quad(0,A(Side(Local(L,0),side),-Z,foot,e),A(Side(Local(L,-Tc),side),-Z,foot,e),A(Side(Local(L,-Tc),side),Z,foot,e),A(Side(Local(L,0),side),Z,foot,e));
            }

            // Pages: under each board down to Tb, short of the foot and the cover's ends, meeting in the middle and filling
            // the spine. Their faces inside the tent are printed; their edges are page edges.
            float lp=L-Inset,s4=(Tb*Out.x-Top.x)/D.x;
            Vector2 EdgeUv(Vector2 p,int side){var q=Side(p,side)-Top;return Uv(PageEdge,Mathf.Clamp01(Vector2.Dot(q,D)/lp),Mathf.Clamp01((Vector2.Dot(q,Out)+Tb)/(Tb-Tc)));}
            foreach(var side in new[]{-1,1})
            {
                Vector2 q2=Side(Local(lp,-Tc),side),q3=Side(Local(lp,-Tb),side),q4=new Vector2(0,Local(s4,-Tb).y),tin=Side(Local(Sp,-Tc),side);
                // The printed page, read the right way round from its own side: from the foot up for the right board.
                var face=N(Side(-Out,side));float f3=side>0?0:1,f4=1-f3;
                b.Quad(0,A(q3,-Zb,face,Uv(Pages,f3,0)),A(q4,-Zb,face,Uv(Pages,f4,0)),A(q4,Zb,face,Uv(Pages,f4,1)),A(q3,Zb,face,Uv(Pages,f3,1)));
                var fore=N(Side(D,side));
                b.Quad(0,A(q3,-Zb,fore,Uv(PageEdge,0,0)),A(q2,-Zb,fore,Uv(PageEdge,0,1)),A(q2,Zb,fore,Uv(PageEdge,1,1)),A(q3,Zb,fore,Uv(PageEdge,1,0)));
                foreach(var z in new[]{-Zb,Zb})
                {
                    var n=new Vector3(0,0,Mathf.Sign(z));
                    b.Quad(0,A(q2,z,n,EdgeUv(q2,side)),A(tin,z,n,EdgeUv(tin,side)),A(q4,z,n,EdgeUv(q4,side)),A(q3,z,n,EdgeUv(q3,side)));
                    // Under the spine: a fan from the middle of the pages' inner faces up the cover's inside to the top.
                    for(int j=0;j<K/2;j++)
                    {
                        int k0=side>0?K-j:j,k1=side>0?K-j-1:j+1;Vector2 a=Arc(k0,SpineR-Tc),c=Arc(k1,SpineR-Tc);
                        b.Tri(0,A(q4,z,n,EdgeUv(q4,side)),A(a,z,n,EdgeUv(a,side)),A(c,z,n,EdgeUv(c,side)));
                    }
                }
            }
            return b.Save(MeshPath,"Dog training book");
        }

        // --- The print ---------------------------------------------------------------------------------------------------
        const int AtlasW=1024,AtlasH=512;
        const float PxPerMm=3.2f,PagePxPerMm=1.6f;
        static int Px(float metres,float ppm)=>Mathf.RoundToInt(metres*1000*ppm);
        static int CoverH=>Px(2*Z*ZScale,PxPerMm);
        // Back, spine and front side by side, as the cover wraps from the back's foot to the front's.
        static RectInt Back=>new RectInt(24,188,Px(L-Sp,PxPerMm),CoverH);
        static RectInt Spine=>new RectInt(Back.xMax,188,Px(SpineR*(Mathf.PI-2*Phi0),PxPerMm),CoverH);
        static RectInt Front=>new RectInt(Spine.xMax,188,Back.width,CoverH);
        static RectInt Pages=>new RectInt(24,16,Px(L-Inset-(Tb*Out.x-Top.x)/D.x,PagePxPerMm),Px(2*Zb*ZScale,PagePxPerMm));
        static readonly RectInt PageEdge=new RectInt(300,16,160,64),Edge=new RectInt(480,16,48,48),Endpaper=new RectInt(548,16,48,48);
        static Vector2 Uv(RectInt r,float fu,float fv)=>new Vector2((r.x+fu*r.width)/AtlasW,(r.y+fv*r.height)/AtlasH);

        static readonly Color Teal=new Color(.10f,.47f,.46f),DeepTeal=new Color(.05f,.28f,.29f),Cream=new Color(.99f,.94f,.80f),
            Orange=new Color(.97f,.55f,.13f),Paper=new Color(.97f,.95f,.89f),Ink=new Color(.55f,.55f,.54f),Blurb=new Color(.55f,.66f,.64f),
            EndpaperColor=new Color(.97f,.78f,.50f),Bars=new Color(.10f,.10f,.10f);

        static Texture2D Print()
        {
            var px=new Color[AtlasW*AtlasH];for(int i=0;i<px.Length;i++)px[i]=DeepTeal;
            Paint(px,Back,PxPerMm,BackArt);Paint(px,Spine,PxPerMm,SpineArt);Paint(px,Front,PxPerMm,FrontArt);
            Paint(px,Pages,PagePxPerMm,PageArt);
            Paint(px,PageEdge,PxPerMm,(u,v,w,h)=>Color.Lerp(Paper,Ink,Mathf.Repeat(v*PxPerMm,3)<1?.25f:0));
            Paint(px,Edge,PxPerMm,(u,v,w,h)=>DeepTeal);Paint(px,Endpaper,PxPerMm,(u,v,w,h)=>EndpaperColor);
            // Lettering, in mm on its face from the bottom left (the 5 x 7 rounded font).
            void Text(RectInt r,string s,float x,float y,float cell,Color c,float weight=1)=>
                PropCanvas.RoundText(px,AtlasW,AtlasH,s,r.x+x*PxPerMm,r.y+y*PxPerMm,cell*PxPerMm,cell*PxPerMm,c,weight);
            Text(Front,"PUPPY CLASS",12,76,.95f,Orange);
            foreach(var (word,y) in new[]{("GOOD",49f),("DOG",26f)}){Text(Front,word,12.7f,y-.7f,2.5f,DeepTeal,1.05f);Text(Front,word,12,y,2.5f,Cream,1.05f);}
            Text(Front,"STEP BY STEP",12,11,.72f,Cream);
            Text(Back,"GOOD DOG",17,74.6f,.7f,DeepTeal);
            var tex=new Texture2D(AtlasW,AtlasH,TextureFormat.RGB24,false,false);tex.SetPixels(px);
            return SaveTexture(PrintPath,tex,false,TextureWrapMode.Clamp);
        }
        // Front: a cream hairline frame, the title down the left (lettered after) and a cream badge with an orange paw.
        static Color FrontArt(float u,float v,float w,float h)
        {
            var c=Frame(Teal,u,v,w,h);float cx=w-33,cy=h/2-3;
            c=Over(c,Cream,Cover(Circle(u,v,cx,cy,25)));c=Over(c,Orange,Cover(Mathf.Abs(Circle(u,v,cx,cy,22.6f))-.45f));
            return Over(c,Orange,Cover(Paw(u,v,cx,cy-1.5f,1.1f)));
        }
        // Back (seen from outside its left edge is the foot): a blurb panel under an orange header, a small paw, a barcode.
        static Color BackArt(float u,float v,float w,float h)
        {
            var c=Frame(Teal,u,v,w,h);
            c=Over(c,Cream,Cover(Box(u,v,10,26,94,84,3)));c=Over(c,Orange,Cover(Box(u,v,14,73,90,81,1.5f)));
            for(int i=0;i<6;i++){float y=66-i*6.5f;c=Over(c,Blurb,Cover(Box(u,v,16,y,i==5?60:88,y+1.6f,.8f)));}
            c=Over(c,Orange,Cover(Paw(u,v,w-25,h-34,.6f)));
            c=Over(c,Color.white,Cover(Box(u,v,w-40,10,w-8,30,1)));
            if(u>w-37&&u<w-11&&v>13.5f&&v<27){int k=Mathf.FloorToInt((u-(w-37))/.65f);if((k*7+k/3)%5<2)c=Bars;}
            return c;
        }
        // Spine: deep teal with cream bands top and bottom, a cream title bar and a small orange paw.
        static Color SpineArt(float u,float v,float w,float h)
        {
            var c=DeepTeal;
            c=Over(c,Cream,Cover(Mathf.Abs(v-7.6f)-.6f));c=Over(c,Cream,Cover(Mathf.Abs(v-(h-7.6f))-.6f));
            c=Over(c,Cream,Cover(Box(u,v,w/2-1.3f,28,w/2+1.3f,70,1.3f)));
            return Over(c,Orange,Cover(Paw(u,v,w/2,h-20,.28f)));
        }
        // A page: a chapter header, a round illustration with a paw, and lines of print wrapping round it.
        static Color PageArt(float u,float v,float w,float h)
        {
            var c=Paper;c=Over(c,Orange,Cover(Box(u,v,9,h-12,48,h-8,1)));
            float cx=w-27,cy=h-27;c=Over(c,Orange,Cover(Mathf.Abs(Circle(u,v,cx,cy,13))-.5f));c=Over(c,Orange,Cover(Paw(u,v,cx,cy-1,.55f)));
            int row=0;
            for(float y=h-20;y>=9;y-=5,row++){float end=y>h-44?w-46:w-9;if(row%5==4)end-=30;c=Over(c,Ink,Cover(Box(u,v,9,y,end,y+1.4f,.7f)));}
            return c;
        }
        static Color Frame(Color c,float u,float v,float w,float h)=>Over(c,Cream,Cover(Mathf.Abs(Box(u,v,4,4,w-4,h-4,3))-.3f));

        // Paints a face (and 3 px round it, so filtering at its edges keeps its colours) from art(u, v, w, h) in mm.
        static float ppm=PxPerMm;
        static void Paint(Color[] px,RectInt r,float pxPerMm,Func<float,float,float,float,Color> art)
        {
            ppm=pxPerMm;float w=r.width/ppm,h=r.height/ppm;
            for(int y=r.y-3;y<r.yMax+3;y++)for(int x=r.x-3;x<r.xMax+3;x++)
            {
                if(x<0||y<0||x>=AtlasW||y>=AtlasH)continue;
                float u=Mathf.Clamp((x+.5f-r.x)/ppm,0,w),v=Mathf.Clamp((y+.5f-r.y)/ppm,0,h);
                var c=art(u,v,w,h);c.a=1;px[y*AtlasW+x]=c;
            }
            ppm=PxPerMm;
        }
        static float Cover(float d)=>Mathf.Clamp01(.5f-d*ppm);
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
        // A paw print s times 3 cm across: a broad pad and four toes fanning out above it.
        static float Paw(float u,float v,float cx,float cy,float s)
        {
            float d=Ellipse(u,v,cx,cy-3.5f*s,10.5f*s,9f*s);
            d=Mathf.Min(d,Ellipse(u,v,cx-11.5f*s,cy+6.5f*s,4.2f*s,5.4f*s,25));d=Mathf.Min(d,Ellipse(u,v,cx-4.2f*s,cy+12.5f*s,4.3f*s,5.6f*s,8));
            d=Mathf.Min(d,Ellipse(u,v,cx+4.2f*s,cy+12.5f*s,4.3f*s,5.6f*s,-8));d=Mathf.Min(d,Ellipse(u,v,cx+11.5f*s,cy+6.5f*s,4.2f*s,5.4f*s,-25));
            return d;
        }
    }
}
