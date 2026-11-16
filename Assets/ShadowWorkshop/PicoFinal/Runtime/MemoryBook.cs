using System;
using System.Collections;
using System.Collections.Generic;
using CloudGarden.Integration;
using CloudGarden.ShadowPuzzle;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShapeOfMemory
{
    // The book the game is played in. Built as a book lying open (BookShape.Flat, its own units: a page is the size of a
    // lamp's wall), it is placed (Placement) in one of three ways, and flies between them (Fly):
    // - On the stand (Stand): at FlatScale on the reading stand, tilted toward the reader. Its pages turn here: pinch a
    //   page anywhere along its outer edge (the aqua band) and the page comes up by that point and follows the hand round
    //   the spine; let go and it turns or falls back by how far and how fast it was going (PageTurn). A poke or a click on
    //   the band, or a look-and-pinch, turns it by itself. Closed (Opening 0, the left half folded onto the right with its
    //   cover up), only a hand opens it: pinch the cover's outer edge and carry it past upright (CoverHand), or look at it,
    //   pinch and sweep (GazeCoverGrab). Nothing opens it by itself on a headset; the desktop's click does (OpenCover).
    // - On the walls (Walls): full size, standing open at a right angle exactly where the lamps' walls are, so the shadows
    //   fall on its pages (the level's own lit walls are drawn on them). Nothing turns here.
    // - Presented (any pose the host gives): the closed book showing its cover, such as the title before it comes down.
    // Pages lying on the stand call attention to themselves with a breath of wind (Beckon): the page lifts a few degrees
    // and settles, while no hand is near. The host fills a spread's two pages (Fill); the page being turned is a bendable
    // paper sheet (PageSheet) printed with the page it leaves on one side and the next on the other, and it shades the
    // pages it passes over.
    public sealed class MemoryBook : MonoBehaviour
    {
        public const float RiffleSeconds=.26f,Block=.028f,Cover=.012f,Overhang=.022f,Ear=.10f;
        public const float FlatScale=.31f;
        public static readonly Color Leather=MemoryPalette.Leather,Edge=new Color(.86f,.80f,.68f),PaperTone=MemoryPalette.Paper;
        // The pages' colour on the walls (where the lamps do not reach), which they take on as the book rises onto them.
        public static Color Dark=MemoryPalette.Ink700;   // MemoryGame.WallTone sets it
        // A page whose edge is lit: brighter, toward white (the unlit paper shader passes over 1 through to the clamp, so
        // cream lifts to ivory).
        public static readonly Color PageLit=new Color(1.07f,1.04f,.95f);
        // A page's size, in the book's own units.
        public static float Width=>Torch2Room.WallSize.x;
        public static float Height=>Torch2Room.WallSize.y;
        // The book's thickness under its pages (the stand's board lies just below).
        public static float Thickness=>.002f+Block+Cover;
        public static BookShape Shape=>BookShape.Flat;

        // ---- Placements
        public struct Placement
        {
            public Vector3 position;public Quaternion rotation;public float scale,opening;
            public Placement(Vector3 position,Quaternion rotation,float scale,float opening){this.position=position;this.rotation=rotation;this.scale=scale;this.opening=opening;}
            public static Placement Lerp(Placement a,Placement b,float t,float arc)=>new Placement(Vector3.Lerp(a.position,b.position,t)+Vector3.up*(arc*Mathf.Sin(t*Mathf.PI)),
                Quaternion.Slerp(a.rotation,b.rotation,t),Mathf.Lerp(a.scale,b.scale,t),Mathf.Lerp(a.opening,b.opening,t));
        }
        public enum Where{Presented,Stand,Walls}
        // The stand's pose for the book's root (the spine's near end), in the stage's space. Set by the host.
        public Pose StandPose=new Pose(Vector3.zero,Quaternion.identity);
        public Placement Stand(float opening)=>new Placement(StandPose.position,StandPose.rotation,FlatScale,opening);
        // Standing open on the lamps' walls: the right page on the right wall, the left on the left, full size.
        public static Placement Walls
        {
            get
            {
                var flatRight=Quaternion.LookRotation(-Shape.Facing(true),Shape.Up);
                var wallRight=Quaternion.LookRotation(-Torch2Projection.WallNormal[1],Vector3.up);
                return new Placement(Torch2Projection.Corner,wallRight*Quaternion.Inverse(flatRight),1,90);
            }
        }
        public Where At{get;private set;}=Where.Stand;
        public bool Flying=>flight!=null;
        public bool OnStand=>At==Where.Stand&&flight==null;
        public bool OnWalls=>At==Where.Walls&&flight==null;
        // 0 on the stand .. 1 on the walls, eased while it flies between them (the stand sinks away as it rises).
        public float Up{get;private set;}
        // How far open: 0 closed, 180 lying open flat, 90 on the walls.
        public float Opening=>placed.opening;
        // Not lying fully open: shut, its cover being lifted, or swinging open.
        public bool Closed=>placed.opening<179||coverOpening||coverGrabber>=0;
        // The host's "about to rise" cue, 0..1: the book lifts a little off the stand.
        public float Anticipate;
        float appliedAnticipate;
        // No breath of wind (tests and captures).
        public bool Calm;
        // The cover will not open (no press, look or band) while the host's title plays.
        public bool Sealed;

        public int Spread{get;private set;}
        public int Count;
        public bool Turning=>flip!=null&&!flip.breeze;
        public bool Dragging=>flip!=null&&!flip.breeze&&(flip.direct||flip.indirect);
        // How far the turning page is over (0 lying where it was .. 1 landed on the far side); 0 with none turning.
        public float Turn=>flip!=null&&!flip.breeze?flip.turn.Tip:0;
        // Builds one page: spread, right page or left, the page's content root (place things at (u - Width/2, v -
        // Height/2, z) with -z toward the reader), and whether its buttons should work.
        public Action<int,bool,Transform,bool> Fill;
        // After a page the player turned lands (TurnTo has its own done instead).
        public Action<int> Turned;
        // The cover was opened.
        public Action Opened;
        // Whether a page may turn now (forward = the right page's), and whether it calls attention to itself.
        public Func<bool,bool> Open,Beckon;
        // Why hands may not turn pages now (the host's reason, for the device log).
        public Func<string> HandsWhy;
        // The page a look is on (0 left, 1 right, -1 none; set each frame by MemoryGaze): it lights as a hand would.
        public int GazeSide=-1;
        public bool GazeCover;
        public Transform LeftFace=>faces[0];
        public Transform RightFace=>faces[1];
        public Pressable Corner(bool forward)=>corners[forward?1:0].press;
        public Pressable CoverPress=>coverPress;
        // Where the cover's title goes: the outside of the left board (facing out once the book is closed).
        public Transform CoverFace=>coverFace;

        // ---- The book's model (the environment artist's SM_Book_01, Art/Art Assets; MemoryGame.BookModel)
        // Modelled closed and cut through the middle of its pages along the hinge (the model's Z axis, through its origin):
        // each half ArtHalf thick, its page face ArtFaceGap from the cut, ArtFaceStart..ArtFaceEnd out from the hinge and
        // ArtFaceLength long (centred ArtFaceCentre along Z); vertex colour R is how much of the turn a vertex takes (1 the
        // half that opens), so Shaders/BookOpening's _Opening opens it all the way to 180, its two page faces then side by
        // side. Measured from the mesh 2026-11-13. Dress fits it so at every opening (shut, the cover lifting, open on the
        // stand, flying, on the walls): its hinge on the spine, its page faces just under the paper and as big as it, opened
        // with Opening. The game's own boxes (covers, page blocks, gilt rules, spine) stop drawing; the paper, the edges, the
        // prints and the turning sheet stay, since they are the interface. (Before, the environment artist's add-on showed it
        // only shut and swapped back to the boxes past 12 degrees.)
        public const float ArtHalf=.105f,ArtFaceGap=.004f,ArtFaceStart=.032f,ArtFaceEnd=1.085f,ArtFaceLength=1.043f,ArtFaceCentre=.0025f;
        // Scaled evenly across its cut (thickness and width alike, so a half turning about the hinge keeps its shape) until its
        // page face reaches the paper's outer edge, and along the spine until the face is as long as the paper.
        public static float ArtScale=>Width/ArtFaceEnd;
        public static float ArtLength=>Height/ArtFaceLength;
        // The model's axes onto the book's: its thickness (x) up through the closed book, out from the hinge (y) across the
        // pages, along the hinge (z) back down the spine.
        public static readonly Quaternion ArtTurn=Quaternion.LookRotation(Vector3.back,Vector3.right);
        public Transform Model{get;private set;}
        // How far the book reaches below its pages (book units): the stand puts its board that far under them.
        public float Underside=>Model?ArtHalf*ArtScale:Thickness;
        readonly List<Renderer> boxes=new List<Renderer>();
        Renderer[] modelRenderers=Array.Empty<Renderer>();
        MaterialPropertyBlock modelBlock;
        static readonly int OpeningId=Shader.PropertyToID("_Opening");
        public void Dress(Transform model)
        {
            if(!model||Model)return;
            var fit=new GameObject("Model fit").transform;fit.SetParent(transform,false);
            fit.localPosition=new Vector3(0,0,Height*.5f+ArtFaceCentre*ArtLength);fit.localRotation=ArtTurn;
            model.SetParent(fit,false);model.localPosition=Vector3.zero;model.localRotation=Quaternion.identity;model.localScale=new Vector3(ArtScale,ArtScale,ArtLength);
            Model=model;modelBlock=new MaterialPropertyBlock();modelRenderers=model.GetComponentsInChildren<Renderer>(true);
            // Opened, the turning half sweeps round the far side of the hinge: keep it from being culled (model space).
            foreach(var r in modelRenderers)r.localBounds=new Bounds(Vector3.zero,new Vector3(2.4f,2.4f,1.2f));
            foreach(var r in boxes)if(r)r.enabled=false;
            // The cover's title, band and press onto the model's cover.
            coverFace.localPosition=new Vector3(coverFace.localPosition.x,coverFace.localPosition.y,Underside+.0015f);
            Apply(placed);
            Debug.Log($"SHAPE_OF_MEMORY_BOOK dressed in {model.name} (x{ArtScale:0.000}, x{ArtLength:0.000} along the spine; underside {Underside:0.000})");
        }
        void OpenModel(float opening)
        {
            if(!Model)return;
            foreach(var r in modelRenderers){if(!r)continue;r.GetPropertyBlock(modelBlock);modelBlock.SetFloat(OpeningId,opening);r.SetPropertyBlock(modelBlock);}
        }

        MetaShadowPlayer xr;
        MemoryKit kit;
        MemorySounds sounds;
        AudioSource voice,rustle;
        Func<bool> handsAllowed;
        // Pages: left 0, right 1. Their frames, their paper, and the print roots the turning page's two sides are built on.
        readonly Transform[] frames=new Transform[2],faces=new Transform[2],prints=new Transform[2];
        readonly List<Renderer> paper=new List<Renderer>();
        Transform spine,coverFace;
        Pressable coverPress;
        Renderer coverBand,coverRim;
        float coverLift;
        PageSheet sheet;
        float brightness=1;
        Placement placed;
        sealed class Flight{public Placement from,to;public Where arrive;public float seconds,time,arc;public Action done;}
        Flight flight;
        sealed class PageCorner{public Transform root;public Pressable press;public Renderer band,rim;public float lift,glow;public Color tint;}
        readonly PageCorner[] corners=new PageCorner[2];
        sealed class Flip
        {
            public bool forward,last,player,breeze;public int from,to;public readonly PageTurn turn=new PageTurn();
            // How much faster than real time it runs (the blank pages of a riffle).
            public float rate=1;
            public Vector3 start;public float began;public Action done;
            // Held in the hand itself (direct) or by a look and a pinch (indirect: the hand's sweep across draws it): by
            // which hand (-1: Grab called directly), how far up the edge (0 the near end .. 1 the far end; the row that
            // leads), and how far the hand has moved since (metres).
            public bool direct,indirect;public int grabber=-1;public float grab,moved;
        }
        Flip flip;

        public static MemoryBook Build(Transform stage,MemoryKit kit,MemorySounds sounds,MetaShadowPlayer xr,Func<bool> handsAllowed)
        {
            var go=new GameObject("Book");go.transform.SetParent(stage,false);var book=go.AddComponent<MemoryBook>();
            book.kit=kit;book.sounds=sounds;book.xr=xr;book.handsAllowed=handsAllowed;
            book.Make();book.Place(book.Stand(180),Where.Stand);return book;
        }
        void Make()
        {
            voice=gameObject.AddComponent<AudioSource>();voice.playOnAwake=false;voice.spatialBlend=0;
            rustle=gameObject.AddComponent<AudioSource>();rustle.playOnAwake=false;rustle.spatialBlend=0;rustle.loop=true;rustle.volume=0;rustle.clip=sounds?.Rustle;
            float w=Width,h=Height;var right=Paper(true);var left=Paper(false);
            // Two page blocks behind the paper, the covers behind them (overhanging the outer edge, the top and the near
            // edge), the spine round the corner.
            for(int k=0;k<2;k++)
            {
                float outward=k==1?1:-1,overH=Overhang*2;
                var frame=new GameObject(k==1?"Right page":"Left page").transform;frame.SetParent(transform,false);frames[k]=frame;
                paper.Add(MemoryUi.Picture("Paper",frame,new Vector3(0,0,.002f),Quaternion.identity,new Vector2(w,h),kit.Screen,k==1?right:left,PaperTone,kit.Owned));
                // Its front face 1.5 mm behind the paper (they were one plane, and fought in the depth buffer).
                boxes.Add(MemoryUi.Shape(PrimitiveType.Cube,"Page block",frame,new Vector3(0,0,.0035f+(Block-.0015f)*.5f),Quaternion.identity,new Vector3(w,h,Block-.0015f),kit.Surface,Edge).GetComponent<Renderer>());
                boxes.Add(MemoryUi.Shape(PrimitiveType.Cube,"Cover",frame,new Vector3(outward*Overhang*.5f,0,.002f+Block+Cover*.5f),Quaternion.identity,new Vector3(w+Overhang,h+overH,Cover),kit.Surface,Leather).GetComponent<Renderer>());
                // Gilt rule along the cover's outer edge, seen past the paper.
                boxes.Add(MemoryUi.Shape(PrimitiveType.Cube,"Cover rule",frame,new Vector3(outward*(w*.5f+Overhang*.55f),0,.002f+Block-.001f),Quaternion.identity,new Vector3(.006f,h+overH*.4f,.004f),kit.Surface,MemoryPalette.Gold).GetComponent<Renderer>());
                faces[k]=new GameObject("Face").transform;faces[k].SetParent(frame,false);faces[k].localPosition=new Vector3(0,0,-.001f);
                // Where the turning page's side for this page is built (lit as the page is) before it is printed.
                prints[k]=new GameObject("Print").transform;prints[k].SetParent(frame,false);prints[k].localPosition=faces[k].localPosition;prints[k].gameObject.SetActive(false);
                corners[k]=MakeEdge(frame,k==1);
            }
            MakeCover(frames[0]);
            var page=Shape.Page(true);frames[1].localPosition=page.position+page.rotation*new Vector3(w*.5f,h*.5f,0);frames[1].localRotation=page.rotation;
            spine=MemoryUi.Shape(PrimitiveType.Cylinder,"Spine",transform,Vector3.zero,Quaternion.identity,Vector3.one,kit.Surface,Leather).transform;boxes.Add(spine.GetComponent<Renderer>());
            // The turning page: its front is a right page, its back a left page.
            sheet=new PageSheet(transform,kit,Shape,right,left);paper.Add(sheet.Renderer);MemoryUi.Tint(sheet.Renderer,PaperTone);
            Decorate();
        }
        // Our own book's leather (Dress hides all of it for the environment artist's model): a fine grain on the covers and
        // the spine in the artist's book's burgundy, and on the front cover's outside the tooling that book had (2026-11-15:
        // the user brought our book back and asked for that cover): a wide gilt Greek-key border between a rule outside and
        // a double rule inside, a square at each corner of it, a gilt corner piece in each inner corner; the title prints
        // over it (MemoryGame.CoverTitle).
        // A shade redder and the gilt warmer than the artist's texture: the scene's dusk ambient pulls both toward plum and
        // beige (2026-11-15 capture).
        public static readonly Color Burgundy=new Color32(0x92,0x24,0x30,255),BurgundyDeep=new Color32(0x5E,0x18,0x22,255),Gilt=new Color32(0xFF,0xBE,0x55,255);
        void Decorate()
        {
            var grain=new Material(kit.Surface){name="Book leather"};grain.SetTexture("_BaseMap",Grain());kit.Owned.Add(grain);
            foreach(var r in boxes)if(r&&(r.name=="Cover"||r.name=="Spine")){r.sharedMaterial=grain;MemoryUi.Tint(r,Burgundy);}
            // On the left board's outside, just under the cover face (the title, the band); not under the face itself, so
            // it comes up with the book after the title, not with it (MemoryGame's title shades).
            var art=MemoryUi.Picture("Cover art",frames[0],new Vector3(-Overhang*.5f,0,.002f+Block+Cover+.0007f),Quaternion.Euler(0,180,0),new Vector2(Width+Overhang,Height+Overhang*2),kit.Surface,CoverArt(),Color.white,kit.Owned);
            art.receiveShadows=true;boxes.Add(art);
        }
        Texture2D Grain()
        {
            const int N=128;var t=new Texture2D(N,N,TextureFormat.RGBA32,true){name="Book leather grain",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear,anisoLevel=4};kit.Owned.Add(t);
            var pixels=new Color32[N*N];var random=new System.Random(43);
            for(int i=0;i<pixels.Length;i++){float c=.88f+(float)random.NextDouble()*.12f;pixels[i]=new Color(c,c,c,1);}
            t.SetPixels32(pixels);t.Apply(true,true);return t;
        }
        // The front cover, 512 square: leather (Burgundy, darker toward the edges, mottled and grained) and gilt tooling.
        Texture2D CoverArt()
        {
            // C: a key cell; I: the border's inset; B: its depth (5 cells, about 5% of the cover as on the artist's); P: one
            // key (6 cells). The run between the corner squares, N - 2 (I + B) = 420, is a whole 14 keys.
            const int N=512,C=5,I=21,B=5*C,P=6*C;
            var t=new Texture2D(N,N,TextureFormat.RGBA32,true){name="Book cover",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Trilinear,anisoLevel=4};kit.Owned.Add(t);
            var gilt=new bool[N*N];
            void Fill(int x0,int y0,int w,int h){for(int y=Mathf.Max(0,y0);y<Mathf.Min(N,y0+h);y++)for(int x=Mathf.Max(0,x0);x<Mathf.Min(N,x0+w);x++)gilt[y*N+x]=true;}
            void Frame(int inset,int thick){Fill(inset,inset,N-2*inset,thick);Fill(inset,N-inset-thick,N-2*inset,thick);Fill(inset,inset,thick,N-2*inset);Fill(N-inset-thick,inset,thick,N-2*inset);}
            Frame(I-8,3);Frame(I+B+6,3);Frame(I+B+12,1);
            // The key: a hooked spiral every P along each side, its base line on the outside.
            bool[,] key={{true,true,true,true,true,true},{true,false,false,false,false,false},{true,false,true,true,true,false},{true,false,false,false,true,false},{true,true,true,true,true,false}};
            int run0=I+B,run1=N-I-B;
            for(int r=run0;r<run1;r++)for(int d=0;d<B;d++)
            {
                int cx=((r-run0)/C)%(P/C),cy=d/C;if(!key[cy,cx])continue;
                gilt[(I+d)*N+r]=true;gilt[(N-1-I-d)*N+(N-1-r)]=true;gilt[(N-1-r)*N+(I+d)]=true;gilt[r*N+(N-1-I-d)]=true;
            }
            // The border's corners: a square one cell thick with a dot in its middle.
            foreach(var (x0,y0) in new[]{(I,I),(N-I-B,I),(I,N-I-B),(N-I-B,N-I-B)})
            {Fill(x0,y0,B,C);Fill(x0,y0+B-C,B,C);Fill(x0,y0,C,B);Fill(x0+B-C,y0,C,B);Fill(x0+2*C,y0+2*C,C,C);}
            // In each inner corner, a gilt corner piece: a triangle with a concave edge and a tooled line along it.
            int o=I+B+16;
            foreach(var (sx,sy) in new[]{(1,1),(-1,1),(1,-1),(-1,-1)})
            {
                int ox=sx>0?o:N-1-o,oy=sy>0?o:N-1-o;
                for(int y=0;y<40;y++)for(int x=0;x<40;x++)
                {
                    float hollow=Mathf.Sqrt((x-40f)*(x-40f)+(y-40f)*(y-40f));
                    if(hollow>34&&x+y<46&&!(hollow>37&&hollow<39))gilt[(oy+sy*y)*N+(ox+sx*x)]=true;
                }
            }
            Color leather=Burgundy,deep=BurgundyDeep,gold=Gilt;
            var pixels=new Color32[N*N];var random=new System.Random(41);
            for(int y=0;y<N;y++)for(int x=0;x<N;x++)
            {
                float u=(x+.5f)/N,v=(y+.5f)/N,edge=Mathf.Min(Mathf.Min(u,1-u),Mathf.Min(v,1-v));
                float mottle=1+.05f*Mathf.Sin(u*23+Mathf.Sin(v*17)*2)*Mathf.Sin(v*19+Mathf.Sin(u*13)*2),grain=1+((float)random.NextDouble()-.5f)*.07f;
                var c=Color.Lerp(deep,leather,Mathf.SmoothStep(0,1,edge*9))*mottle*grain;
                if(gilt[y*N+x])c=gold*(.86f+.24f*v)*(1+((float)random.NextDouble()-.5f)*.1f);
                c.a=1;pixels[y*N+x]=c;
            }
            t.SetPixels32(pixels);t.Apply(true,true);return t;
        }
        // Cream paper with a faint grain, a thin margin rule and the gutter's shade on the spine side.
        Texture2D Paper(bool right)
        {
            const int W=256,H=240;var t=new Texture2D(W,H,TextureFormat.RGBA32,true){name=right?"Book right paper":"Book left paper",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Trilinear,anisoLevel=4};kit.Owned.Add(t);
            var pixels=new Color32[W*H];var random=new System.Random(right?11:12);
            for(int y=0;y<H;y++)for(int x=0;x<W;x++)
            {
                float u=(x+.5f)/W,v=(y+.5f)/H,gutter=right?u:1-u;
                float shade=1-.22f*Mathf.Exp(-gutter*18)-.05f*Mathf.Exp(-(1-gutter)*30)-.04f*Mathf.Exp(-v*40)-.04f*Mathf.Exp(-(1-v)*40);
                float grain=1+((float)random.NextDouble()-.5f)*.035f;
                float rule=Mathf.Abs(gutter-.06f)<.002f||Mathf.Abs(gutter-.955f)<.002f||Mathf.Abs(v-.045f)<.002f||Mathf.Abs(v-.955f)<.002f?.86f:1;
                float c=Mathf.Clamp01(shade*grain*rule);pixels[y*W+x]=new Color(c,c*.985f,c*.96f,1);
            }
            t.SetPixels32(pixels);t.Apply(true,true);return t;
        }
        // A page's turning edge: an aqua band EdgeBand wide down the page's whole outer edge, shown while the page may turn
        // and brighter while a pinch or a press would turn it. A hand pinches anywhere along it (Hands); a poke, a click or
        // a look-and-pinch turns the page by itself. A dark rim EdgeRim wide (about 3 mm on the stand) runs round its inner
        // side and ends, so the aqua reads on the cream paper (1.4:1 alone). Both see-through (MemoryPalette.StrokeRest):
        // the paper shows through the band at rest, less as it lights.
        public const float EdgeBand=.09f,EdgeRim=.01f;
        static readonly Color RimColor=MemoryPalette.Alpha(MemoryPalette.Ink800,MemoryPalette.RimAlpha);
        static Color Rest(Color aqua)=>MemoryPalette.Alpha(aqua,MemoryPalette.StrokeRest);
        // An edge's colour by how lit it is (0 rest .. 1 a hand on it), or held.
        static Color Lit(float lit,bool held)=>held?MemoryPalette.Alpha(MemoryPalette.AquaGrab,MemoryPalette.StrokeHeld):
            MemoryPalette.Alpha(Color.Lerp(MemoryPalette.AquaIdle,MemoryPalette.AquaHover,lit),Mathf.Lerp(MemoryPalette.StrokeRest,MemoryPalette.StrokeLit,lit));
        PageCorner MakeEdge(Transform frame,bool right)
        {
            float w=Width,h=Height,s=right?1:-1;var edge=new PageCorner();
            edge.root=new GameObject(right?"Edge (turn forward)":"Edge (turn back)").transform;edge.root.SetParent(frame,false);
            edge.root.localPosition=new Vector3(s*(w*.5f-Ear*.3f),0,-.003f);
            edge.band=MemoryUi.Shape(PrimitiveType.Quad,"Band",edge.root,new Vector3(s*(Ear*.3f-EdgeBand*.5f),0,0),Quaternion.identity,new Vector3(EdgeBand,h*.96f,1),kit.Stroke,Rest(MemoryPalette.AquaIdle)).GetComponent<Renderer>();
            // Between the paper and the band.
            edge.rim=MemoryUi.Shape(PrimitiveType.Quad,"Band rim",edge.root,new Vector3(s*(Ear*.3f-EdgeBand*.5f-EdgeRim*.5f),0,.002f),Quaternion.identity,new Vector3(EdgeBand+EdgeRim,h*.96f+EdgeRim*2,1),kit.Flat,RimColor).GetComponent<Renderer>();
            var hit=new GameObject("Press",typeof(BoxCollider));hit.transform.SetParent(edge.root,false);hit.transform.localScale=new Vector3(Ear*.9f,h,.03f);
            edge.press=Pressable.Add(hit,()=>Press(right),()=>Ready(right),handsAllowed,new Vector3(0,0,-.51f),Quaternion.identity);
            edge.press.Label=right?"book/forward":"book/back";edge.press.Why=()=>Refusal(right);
            return edge;
        }
        // The left board's outside, where the title is, its outer edge's band and a press over it: the desktop's click opens
        // the closed book. Hands never press it (a fingertip on the cover opened it by itself); they carry it (CoverHand).
        void MakeCover(Transform left)
        {
            float w=Width,h=Height,z=.002f+Block+Cover+.0015f;
            coverFace=new GameObject("Cover face").transform;coverFace.SetParent(left,false);coverFace.localPosition=new Vector3(-Overhang*.5f,0,z);coverFace.localRotation=Quaternion.Euler(0,180,0);
            // The left page's outer edge is at its frame's -x; seen from outside (turned) it is the face's +x.
            // The band, with its dark rim between it and the leather (a layer apart, so they never fight in the depth buffer).
            coverBand=MemoryUi.Shape(PrimitiveType.Quad,"Cover band",coverFace,new Vector3((w+Overhang)*.5f-EdgeBand*.5f,0,-.0025f),Quaternion.identity,new Vector3(EdgeBand,h*.96f,1),kit.Stroke,Rest(MemoryPalette.AquaIdle)).GetComponent<Renderer>();
            coverRim=MemoryUi.Shape(PrimitiveType.Quad,"Cover band rim",coverFace,new Vector3((w+Overhang)*.5f-EdgeBand*.5f-EdgeRim*.5f,0,-.0005f),Quaternion.identity,new Vector3(EdgeBand+EdgeRim,h*.96f+EdgeRim*2,1),kit.Flat,RimColor).GetComponent<Renderer>();
            var hit=new GameObject("Cover press",typeof(BoxCollider));hit.transform.SetParent(coverFace,false);hit.transform.localPosition=new Vector3(0,0,-.01f);hit.transform.localScale=new Vector3(w+Overhang,h+Overhang*2,.02f);
            coverPress=Pressable.Add(hit,OpenCover,()=>Closed&&!Sealed&&flight==null&&At!=Where.Walls,()=>false,new Vector3(0,0,-.51f),Quaternion.identity);
            coverPress.Label="book/cover";coverPress.Why=()=>!Closed?"open already":Sealed?"title playing":flight!=null?"flying":"hands carry it open by its edge";
        }
        bool Ready(bool forward)=>(flip==null||flip.breeze)&&OnStand&&!Closed&&(forward?Spread<Count-1:Spread>0)&&(Open==null||Open(forward));
        // Why an edge would not turn its page now (null: it would), for the device log.
        string Refusal(bool forward)=>flip!=null&&!flip.breeze?"book turning":!OnStand?"not on the stand":Closed?"closed":!(forward?Spread<Count-1:Spread>0)?"no page that way":
            Open!=null&&!Open(forward)?"closed now":handsAllowed!=null&&!handsAllowed()?HandsWhy?.Invoke()??"hands not allowed":null;
        // 1: the paper's own colour; 0: the walls' Dark (the book on the walls); between, as it flies. Corners() then re-tints
        // the pages it lights and the sheet (their caches are cleared).
        public void SetBrightness(float value)
        {
            brightness=Mathf.Clamp01(value);foreach(var r in paper)MemoryUi.Tint(r,Tone(PaperTone));
            foreach(var ear in corners)if(ear!=null)ear.tint=Color.clear;sheetTint=Color.clear;
        }
        Color Tone(Color paperColour){var c=Color.Lerp(Dark,paperColour,brightness);c.a=1;return c;}
        Color sheetTint;
        public float Brightness=>brightness;

        // ---- Placing and flying
        // Puts the book there at once.
        public void Place(Placement at,Where where){flight=null;At=where;Up=where==Where.Walls?1:0;Apply(at);}
        // How high a flight's root arcs up on the way (metres, at the middle of the flight). Coming down from the title
        // (Presented) at .05 its spine end cut through the stand's board: the straight line from there is shallower than
        // the board's tilt. From .12 nothing of it enters the stand (measured 2026-11-12, 401 samples: 4 mm short of
        // landing at the closest; .10 still cut in). The walls flights use it too.
        public const float FlyArc=.12f;
        // Flies there over seconds (eased, with an arc up), then done. The book lands in where.
        public void Fly(Placement to,Where where,float seconds,Action done=null,float arc=FlyArc)
        {
            if(flip!=null)EndFlip(true);
            flight=new Flight{from=placed,to=to,arrive=where,seconds=Mathf.Max(.01f,seconds),arc=arc,done=done};
            Debug.Log($"SHAPE_OF_MEMORY_BOOK fly {At}>{where} {seconds:0.0}s");
        }
        void FlyStep(float dt)
        {
            var f=flight;f.time+=dt;float t=Mathf.Clamp01(f.time/f.seconds),e=t*t*(3-2*t);
            Apply(Placement.Lerp(f.from,f.to,e,f.arc));
            Up=f.arrive==Where.Walls?(At==Where.Walls?1:e):(At==Where.Walls?1-e:0);
            if(t<1)return;
            flight=null;At=f.arrive;Up=At==Where.Walls?1:0;Debug.Log($"SHAPE_OF_MEMORY_BOOK landed at {At}");
            f.done?.Invoke();
        }
        void Apply(Placement at)
        {
            placed=at;appliedAnticipate=At==Where.Stand&&flight==null?Mathf.Clamp01(Anticipate):0;
            transform.localPosition=at.position+Vector3.up*(.015f*appliedAnticipate);
            transform.localRotation=at.rotation;transform.localScale=Vector3.one*at.scale;
            // The left half swings round the spine (0 folded onto the right, 180 flat, 90 a right angle).
            var shape=new BookShape(Vector3.zero,Vector3.right,Vector3.up,Vector3.forward,at.opening,0);var page=shape.Page(false);
            frames[0].localPosition=page.position+page.rotation*new Vector3(Width*.5f,Height*.5f,0);frames[0].localRotation=page.rotation;
            var behind=-(shape.Facing(true)+shape.Facing(false));behind=behind.sqrMagnitude<1e-6f?-Vector3.right:behind.normalized;
            float length=Height+Overhang*2,thick=Block+Cover;
            // Just below the paper lying open (a ridge in the gutter otherwise), round the corner standing. Shut, it wraps the
            // hinge (centred on it, its radius the book's half thickness, so it meets both boards' outsides): set beside
            // the hinge, it left a wedge each side where the page blocks' cream ends showed as a seam (2026-11-15). It
            // moves back behind the hinge over the cover's first Unwrap degrees.
            const float Unwrap=40;
            spine.localPosition=behind*thick*1.1f*Mathf.SmoothStep(0,1,at.opening/Unwrap)+Vector3.forward*(Height*.5f);spine.localRotation=Quaternion.FromToRotation(Vector3.up,Vector3.forward);
            spine.localScale=new Vector3(thick*2.1f,length*.5f,thick*2.1f);
            OpenModel(at.opening);
        }

        // ---- The cover
        bool coverOpening,coverSweep;
        float coverFrom,coverTo,coverTime;
        int coverGrabber=-1;
        Vector3 coverStart;
        // Opens the closed cover by itself, flying down first if it is presented: the desktop's click only. On a headset a
        // hand carries it (CoverHand, GazeCoverGrab).
        public void OpenCover()
        {
            if(!Closed||coverOpening||Sealed)return;
            if(At!=Where.Stand||flight!=null){Fly(Stand(0),Where.Stand,.8f,()=>SwingCover(180));return;}
            SwingCover(180);
        }
        void SwingCover(float to){coverOpening=true;coverFrom=placed.opening;coverTo=to;coverTime=0;if(to>90)Say(sounds?.Crinkle,.8f,.22f);}
        void CoverStep(float dt)
        {
            coverTime+=dt;float seconds=Mathf.Lerp(.25f,.7f,Mathf.Abs(coverTo-coverFrom)/180),t=Mathf.Clamp01(coverTime/seconds),e=1-(1-t)*(1-t);
            var at=placed;at.opening=Mathf.Lerp(coverFrom,coverTo,e);Apply(at);
            if(t<1)return;
            coverOpening=false;
            if(coverTo>90){Say(sounds?.Flap,.8f,.3f);Debug.Log("SHAPE_OF_MEMORY_BOOK cover opened");Opened?.Invoke();}
        }
        // A hand pinching the closed cover's outer edge lifts it round the spine and it follows the hand; let go past upright
        // it swings the rest of the way open, short of it it falls shut. Only the carry opens it: a quick pinch leaves it
        // shut (the user, 2026-11-11: it used to open by itself). Taken by a look and a pinch (coverSweep), the hand's
        // sweep across the book lifts it instead, as GazeGrab draws a page. Hands() calls it each frame; tests drive it.
        public void CoverHand(int hand,Vector3 tip,bool pinching,bool fresh)
        {
            var p=transform.InverseTransformPoint(tip);
            if(coverGrabber==hand)
            {
                if(!pinching){LetGoCover("let go");return;}
                var at=placed;
                at.opening=coverSweep?Mathf.Clamp01(Vector3.Dot(tip-coverStart,-transform.right)/IndirectReach)*180:Mathf.Clamp(Mathf.Atan2(Mathf.Max(0,p.y),p.x)*Mathf.Rad2Deg,0,180);
                Apply(at);return;
            }
            if(coverGrabber>=0||!Closed||coverOpening||Sealed||!OnStand)return;
            float w=Width;bool at_=p.x>w-ReachIn&&p.x<w+ReachOut&&p.z>-ReachOut&&p.z<Height+ReachOut&&p.y>-.1f&&p.y<ReachUp;
            if(!at_)return;
            handAt[hand]=true;coverNear=true;
            if(pinching&&fresh&&(handsAllowed==null||handsAllowed())){coverGrabber=hand;coverSweep=false;coverStart=tip;Say(sounds?.Crinkle,1,.18f);Debug.Log($"SHAPE_OF_MEMORY_BOOK cover grab hand={Side(hand)}");}
        }
        // A look at the closed cover and a pinch: the hand's sweep across the book carries the cover open. Returns whether
        // it took the cover.
        public bool GazeCoverGrab(int hand,Vector3 tip)
        {
            if(coverGrabber>=0||!Closed||coverOpening||Sealed||!OnStand||handsAllowed!=null&&!handsAllowed())return false;
            coverGrabber=hand;coverSweep=true;coverStart=tip;Say(sounds?.Crinkle,1,.18f);Debug.Log($"SHAPE_OF_MEMORY_BOOK gaze cover grab hand={Side(hand)}");return true;
        }
        void LetGoCover(string why)
        {
            bool open=placed.opening>=90;coverGrabber=-1;coverSweep=false;
            Debug.Log($"SHAPE_OF_MEMORY_BOOK cover {why} at {placed.opening:0} deg -> {(open?"opens":"falls shut")}");
            SwingCover(open?180:0);
        }
        public bool CoverHeld=>coverGrabber>=0;
        bool coverNear;

        // ---- Spreads
        // Shows a spread at once, both pages live.
        public void Show(int spread){StopAllCoroutines();EndFlip(false);Spread=Mathf.Clamp(spread,0,Mathf.Max(0,Count-1));Refill();}
        // Rebuilds the open spread's pages (after the host's state changed).
        public void Refill(){Build(faces[0],Spread,false,true);Build(faces[1],Spread,true,true);Corners();}
        void Build(Transform face,int spread,bool right,bool live)
        {
            for(int i=face.childCount-1;i>=0;i--){var child=face.GetChild(i).gameObject;child.SetActive(false);Destroy(child);}
            if(spread>=0&&spread<Count)Fill?.Invoke(spread,right,face,live);
        }
        // Turns to a spread: one page, or a riffle through several (the pages between stay blank). done runs on landing.
        public void TurnTo(int spread,Action done=null)=>StartCoroutine(Riffle(Mathf.Clamp(spread,0,Mathf.Max(0,Count-1)),done));
        // One page turned straight to a spread, however far (a ribbon's jump: the user, 2026-11-15, one flip and that's
        // it): its front the open spread's page, its back the arriving one's.
        public void TurnStraight(int spread,Action done=null)=>StartCoroutine(Straight(Mathf.Clamp(spread,0,Mathf.Max(0,Count-1)),done));
        IEnumerator Straight(int to,Action done)
        {
            if(flip!=null&&flip.breeze)EndFlip(true);
            while(flip!=null||flight!=null||coverOpening)yield return null;
            if(to!=Spread)
            {
                Begin(to>Spread,Spread,to);flip.turn.Auto();Say(sounds?.Crinkle,.9f,.16f);
                while(flip!=null)yield return null;
            }
            done?.Invoke();
        }
        IEnumerator Riffle(int to,Action done)
        {
            if(flip!=null&&flip.breeze)EndFlip(true);
            while(flip!=null||flight!=null||coverOpening)yield return null;
            bool forward=to>Spread;int steps=Mathf.Abs(to-Spread);
            for(int i=0;i<steps;i++)
            {
                int from=Spread,next=from+(forward?1:-1);bool first=i==0,last=i==steps-1;
                Begin(forward,from,next,first,last);flip.turn.Auto();flip.rate=last?1:PageTurn.AutoSeconds/RiffleSeconds;
                if(i==0)Say(sounds?.Crinkle,.9f,.16f);
                while(flip!=null)yield return null;
            }
            done?.Invoke();
        }
        // Lays the turning page over the page it leaves: front (right page) and back (left page) printed, the page beneath
        // it showing what the turn uncovers. first/last: whether the leaving and the arriving pages are real or blank.
        void Begin(bool forward,int from,int to,bool first=true,bool last=true)
        {
            flip=new Flip{forward=forward,from=from,to=to,last=last};flip.turn.Finish=PageTurn.FlatFinish;
            int rightSpread=forward?from:to,leftSpread=forward?to:from;bool rightReal=forward?first:last,leftReal=forward?last:first;
            Print(1,rightReal?rightSpread:-1);Print(0,leftReal?leftSpread:-1);
            // The page beneath: forward, the right page of the next spread; back, the left page of the previous one. The page
            // it lands on stays; if it has tiles, it is built again without buttons so they lie flat under the landing sheet.
            if(forward)Build(faces[1],last?to:-1,true,false);else Build(faces[0],last?to:-1,false,false);
            int under=forward?0:1;if(faces[under].GetComponentInChildren<MemoryTile>())Build(faces[under],first?from:-1,under==1,false);
            sheet.Show(true);Pose();
            if(rustle.clip){rustle.volume=0;rustle.Play();}
        }
        void Print(int side,int spread){prints[side].gameObject.SetActive(true);Build(prints[side],spread,side==1,false);sheet.Print(prints[side],side);}
        void Pose()=>sheet.Pose(flip.turn.Root,flip.turn.Tip,flip.forward,flip.grab);
        // Landed on the far side, or fallen back.
        void Land()
        {
            var f=flip;bool turned=f.turn.Goal==1;
            if(turned)
            {
                Spread=f.to;
                // Mid-riffle the faces stay blank until the last page lands.
                if(f.last)Refill();else{Build(f.forward?faces[0]:faces[1],-1,!f.forward,false);}
            }
            else Refill();
            EndFlip(false);
            if(f.breeze)return;
            if(f.last||!turned)Debug.Log($"SHAPE_OF_MEMORY_BOOK landed spread={Spread} {(turned?"turned":"fell back")} by={(f.player?"player":"game")}");
            var done=f.done;done?.Invoke();
            if(turned&&f.player)Turned?.Invoke(Spread);
        }
        // restore: the pages a breeze or a flight cut short are rebuilt as they were.
        void EndFlip(bool restore){bool was=flip!=null;flip=null;sheet?.Show(false);if(rustle)rustle.Stop();if(restore&&was)Refill();}

        // ---- Edges
        void Press(bool forward)
        {
            if(flip!=null&&flip.breeze)EndFlip(true);
            if(flip!=null||!Ready(forward))return;
            // A hand at an edge takes the page itself (Hands); its poke or pinch there is not a press.
            if(handAt[0]||handAt[1]){Debug.Log($"SHAPE_OF_MEMORY_BOOK press on the {(forward?"right":"left")} edge left to the hand at an edge");return;}
            Begin(forward,Spread,Spread+(forward?1:-1));flip.player=true;Say(sounds?.Crinkle,1,.2f);flip.turn.Auto();
        }
        // Tests and captures: hold a turn where it is, or show it as a tapped turn is at p of its way (0 = the page it
        // leaves, 1 = landed).
        public bool Frozen;
        public void Seek(float p){if(flip==null)return;PageTurn.Sample(p,out flip.turn.Root,out flip.turn.Tip);Pose();}
        void Update()
        {
            float dt=Time.unscaledDeltaTime;
            if(flight!=null)FlyStep(dt);
            else if(At==Where.Stand&&!Mathf.Approximately(Mathf.Clamp01(Anticipate),appliedAnticipate))Apply(placed);
            if(coverOpening)CoverStep(dt);
            if(!Frozen)Hands();
            if(flip!=null&&!Frozen)Step();
            Breeze();Corners();
        }

        // ---- Hands on the book
        // A pinch (thumb to index tip, metres; it lets go a little wider than it takes) at a page's outer edge takes the
        // page by that point. How near the edge counts, in page units: in over the page, out past the edge, up off it.
        public const float PinchOn=.022f,PinchOff=.04f,ReachIn=.32f,ReachOut=.22f,ReachUp=.45f;
        // A pinch that closed at most GrabWindow seconds before the hand reached the edge still takes the page (hands
        // often close on the way in); one closed longer ago, or already closed when the hand came into view, is stale and
        // must open first. The edge lights only while a pinch would take it. A held page outlasts LostGrace of lost
        // tracking (it stays where it is) and OpenGrace of a pinch that reads open: one bad frame must not drop it.
        // (GrabWindow: the 2026-11-07 playtest logged pinches that closed .23-.82 s before the hand reached the edge.)
        public const float GrabWindow=.4f,LostGrace=.3f,OpenGrace=.1f;
        // A page drawn by a look and a pinch turns all the way over this much hand travel across the book (metres).
        public const float IndirectReach=.25f;
        readonly bool[] pinching=new bool[2],seen=new bool[2],handAt=new bool[2],wasAt=new bool[2],sideNear=new bool[2];
        readonly float[] closedAt={float.NegativeInfinity,float.NegativeInfinity},lostFor=new float[2],openFor=new float[2];
        // Whether a hand is at the book's edge, at its closed cover, or holds a page.
        public bool HandAt(bool left)=>handAt[left?0:1];
        // Whether any hand is near the book at all (no breeze then), metres.
        public bool HandNear(float within=.15f)
        {
            if(!xr||!xr.Active)return false;
            for(int hand=0;hand<2;hand++)
            {
                if(!xr.ReadHand(hand==0,out _,out var tip,out _))continue;
                var p=transform.InverseTransformPoint(tip);float pad=within/Mathf.Max(.01f,transform.lossyScale.x);
                if(p.x>-Width-pad&&p.x<Width+pad&&p.z>-pad&&p.z<Height+pad&&p.y>-pad&&p.y<pad*2)return true;
            }
            return false;
        }
        static string Side(int hand)=>hand==0?"left":hand==1?"right":"none";
        void Hands()
        {
            for(int k=0;k<2;k++)handAt[k]=sideNear[k]=false;coverNear=false;
            if(!xr||!xr.Active)return;
            float now=Time.unscaledTime,dt=Time.unscaledDeltaTime;
            for(int hand=0;hand<2;hand++)
            {
                bool held=flip!=null&&(flip.direct||flip.indirect)&&flip.grabber==hand,at=false;
                if(!xr.ReadHand(hand==0,out _,out var tip,out float pinch,held||coverGrabber==hand))
                {
                    if(held){handAt[hand]=true;lostFor[hand]+=dt;if(lostFor[hand]>=LostGrace)Drop("hand lost: "+xr.HandProblem(hand==0,true));}
                    else{if(coverGrabber==hand)LetGoCover("hand lost");pinching[hand]=seen[hand]=wasAt[hand]=false;}
                    continue;
                }
                lostFor[hand]=0;
                bool was=pinching[hand];pinching[hand]=pinch<(was?PinchOff:PinchOn);
                if(pinching[hand]&&!was)closedAt[hand]=seen[hand]?now:float.NegativeInfinity;
                seen[hand]=true;
                bool fresh=pinching[hand]&&now-closedAt[hand]<=GrabWindow;
                if(held)
                {
                    handAt[hand]=true;
                    if(pinching[hand]){openFor[hand]=0;if(flip.indirect)Sweep(tip);else Drag(tip);}
                    else{openFor[hand]+=dt;if(openFor[hand]>=OpenGrace)Drop($"opened to {pinch*100:0.0} cm");}
                    continue;
                }
                if(At!=Where.Stand||flight!=null){wasAt[hand]=false;continue;}
                if(Closed||coverGrabber==hand){if(!coverOpening)CoverHand(hand,tip,pinching[hand],fresh&&coverGrabber<0);if(coverGrabber==hand&&fresh)closedAt[hand]=float.NegativeInfinity;continue;}
                int side=EdgeAt(tip);
                if(side>=0)
                {
                    handAt[hand]=at=true;
                    bool ready=Ready(side==1),allowed=handsAllowed==null||handsAllowed(),open=!pinching[hand]||fresh;
                    if(open&&ready&&allowed)
                    {
                        sideNear[side]=true;
                        if(pinching[hand]&&Grab(tip)&&flip!=null&&flip.direct)
                        {
                            flip.grabber=hand;closedAt[hand]=float.NegativeInfinity;openFor[hand]=0;
                            Debug.Log($"SHAPE_OF_MEMORY_BOOK grab {(side==1?"forward":"back")} hand={Side(hand)} at={flip.grab:0.00} pinch={pinch*100:0.0} cm");
                        }
                    }
                    else if(pinching[hand]&&(!was||!wasAt[hand]))
                        Debug.Log($"SHAPE_OF_MEMORY_BOOK edge {(side==1?"forward":"back")} hand={Side(hand)} pinch not taken: "+(!open?$"closed {(float.IsNegativeInfinity(closedAt[hand])?"before tracking":$"{now-closedAt[hand]:0.00}s before")}, open to grab":!ready?Refusal(side==1)??"not ready":HandsWhy?.Invoke()??"hands not allowed"));
                }
                wasAt[hand]=at;
            }
        }
        // Which page's outer edge a point (world space) is at: 1 the right page's (it turns forward), 0 the left page's,
        // -1 neither. On the stand only.
        public int EdgeAt(Vector3 point)
        {
            var p=transform.InverseTransformPoint(point)-Shape.Corner;float up=Vector3.Dot(p,Shape.Up);
            if(up<-ReachOut||up>Height+ReachOut)return -1;
            for(int k=1;k>=0;k--)
            {
                float across=Vector3.Dot(p,Shape.Direction(k==1)),off=Vector3.Dot(p,Shape.Facing(k==1));
                if(across>Width-ReachIn&&across<Width+ReachOut&&off>-.1f&&off<ReachUp)return k;
            }
            return -1;
        }
        // Takes the page whose edge is at point (world space) by that point, if it may turn. Returns whether it did.
        public bool Grab(Vector3 point)
        {
            int side=EdgeAt(point);if(side<0)return false;bool forward=side==1;
            if(!Ready(forward))return false;
            if(flip!=null&&flip.breeze)EndFlip(true);
            Begin(forward,Spread,Spread+(forward?1:-1));flip.player=true;flip.direct=true;flip.start=point;flip.began=Time.unscaledTime;
            flip.grab=Mathf.Clamp01(Vector3.Dot(transform.InverseTransformPoint(point)-Shape.Corner,Shape.Up)/Height);
            Say(sounds?.Crinkle,1,.2f);Drag(point);return true;
        }
        // A look and a pinch: the hand's sweep across the book draws the page (toward the spine and over), as if its edge
        // were in the hand. Returns whether it took the page.
        public bool GazeGrab(bool forward,int hand,Vector3 tip)
        {
            if(!Ready(forward))return false;
            if(flip!=null&&flip.breeze)EndFlip(true);
            Begin(forward,Spread,Spread+(forward?1:-1));flip.player=true;flip.indirect=true;flip.grabber=hand;flip.start=tip;flip.began=Time.unscaledTime;flip.grab=.5f;
            flip.turn.Hold(0);Say(sounds?.Crinkle,1,.2f);Debug.Log($"SHAPE_OF_MEMORY_BOOK gaze grab {(forward?"forward":"back")} hand={Side(hand)}");return true;
        }
        void Sweep(Vector3 tip)
        {
            float across=Vector3.Dot(tip-flip.start,transform.right.normalized)*(flip.forward?-1:1);
            flip.moved=Mathf.Max(flip.moved,Vector3.Distance(tip,flip.start));flip.turn.Hold(Mathf.Clamp01(across/IndirectReach));
        }
        // The held edge goes where the hand is round the spine; the page bends after it. Or, whichever turns it further,
        // the edge follows the hand across the book as if the hand kept to the circle the edge sweeps: upright with the
        // hand over the spine, over beyond it. (Sitting back from the stand, the arm reaches the edge nearly straight and
        // can't lift over the book: the page lay flat until the hand crossed the spine, then jumped, 2026-11-15, sticky.)
        public void Drag(Vector3 point)
        {
            if(flip==null||!flip.direct)return;
            var p=transform.InverseTransformPoint(point)-Shape.Corner;
            float x=Vector3.Dot(p,Shape.Direction(flip.forward)),y=Vector3.Dot(p,Shape.Facing(flip.forward));
            float from=Mathf.Max(.05f,Vector3.Dot(transform.InverseTransformPoint(flip.start)-Shape.Corner,Shape.Direction(flip.forward)));
            float across=Mathf.Acos(Mathf.Clamp(x/from,-1,1));
            float angle=Mathf.Clamp(Mathf.Max(Mathf.Atan2(Mathf.Max(0,y),x),across),0,Shape.Opening);
            flip.moved=Mathf.Max(flip.moved,Vector3.Distance(point,flip.start));
            flip.turn.Hold(PageBend.TipFor(angle,flip.turn.Root,Shape.Opening,flip.grab));
        }
        // Let go: a quick pinch that hardly moved turns the page by itself; otherwise it turns or falls back.
        public void Drop()=>Drop("let go");
        void Drop(string why)
        {
            if(flip==null||!(flip.direct||flip.indirect))return;
            int hand=flip.grabber;float after=Time.unscaledTime-flip.began,tip=flip.turn.Tip;
            bool tap=after<.3f&&flip.moved<.03f;flip.direct=flip.indirect=false;flip.grabber=-1;
            if(hand>=0){lostFor[hand]=openFor[hand]=0;}
            if(tap)flip.turn.Auto();else flip.turn.Release();
            Debug.Log($"SHAPE_OF_MEMORY_BOOK drop {(flip.forward?"forward":"back")} hand={Side(hand)} why={why} after={after:0.00}s tip={tip:0.00} -> {(tap?"tap turns":flip.turn.Goal==1?"turns":"falls back")}");
        }

        // ---- A breath of wind
        // A beckoning page (or the closed cover) lifts BreezeLift of its turn, holds BreezeHold and settles, every
        // BreezeEvery seconds while no hand is near. A hand or a press takes over from it at once.
        public const float BreezeEvery=2.5f,BreezeLift=.045f,BreezeHold=.35f,CoverBreeze=9;
        float nextBreeze;
        public bool Breezing=>flip!=null&&flip.breeze;
        void Breeze()
        {
            float now=Time.unscaledTime;
            if(flip!=null&&flip.breeze&&now-flip.began>=BreezeHold&&flip.turn.Held){flip.turn.Release(0);return;}
            if(Calm||flip!=null||flight!=null||coverOpening||coverGrabber>=0||At!=Where.Stand||Beckon==null){if(flip==null)nextBreeze=Mathf.Max(nextBreeze,now+1);return;}
            if(now<nextBreeze)return;
            if(HandNear()){nextBreeze=now+1;return;}
            nextBreeze=now+BreezeEvery;
            if(Closed){if(Beckon(true))StartCoroutine(CoverPuff());return;}
            for(int k=1;k>=0;k--)
            {
                bool forward=k==1;if(!Beckon(forward)||!Ready(forward))continue;
                Begin(forward,Spread,Spread+(forward?1:-1));flip.breeze=true;flip.began=now;flip.turn.Hold(BreezeLift);return;
            }
        }
        IEnumerator CoverPuff()
        {
            for(float t=0;t<.9f&&Closed&&coverGrabber<0&&!coverOpening;t+=Time.unscaledDeltaTime)
            {var at=placed;at.opening=CoverBreeze*Mathf.Sin(Mathf.PI*t/.9f);Apply(at);yield return null;}
            if(Closed&&coverGrabber<0&&!coverOpening){var at=placed;at.opening=0;Apply(at);}
        }

        // Each edge shows (and can be pressed) only while it may turn its page: aqua, Idle at rest, toward Hover as it
        // breathes while it beckons, Hover while a pinch or a press would turn it (Grab while pressed), its whole page
        // brightening with it.
        void Corners()
        {
            var plain=Tone(PaperTone);var lit=Tone(PageLit);
            for(int k=0;k<2;k++)
            {
                // The page being drawn keeps its edge live until it is let go.
                var ear=corners[k];bool held=flip!=null&&!flip.breeze&&(flip.direct||flip.indirect)&&(flip.forward?1:0)==k,open=Ready(k==1)||held,beckon=open&&Beckon!=null&&Beckon(k==1);
                ear.root.gameObject.SetActive(open);
                // The paper itself lifts while it turns; a breath of wind leaves the band showing (it is the cue).
                bool shown=open&&(flip==null||flip.breeze);
                bool on=open&&(ear.press.Hover&&!(handAt[0]||handAt[1])||ear.press.Down||sideNear[k]||GazeSide==k);
                // The whole page brightens with its band: fully while a pinch or a press would turn it, a little with its
                // beckoning breath; taken up, it fades back to plain (on the turning sheet, below).
                float glow=shown?(on?1:beckon?.15f+.1f*Mathf.Sin(Time.unscaledTime*3):0):0;ear.glow=Mathf.MoveTowards(ear.glow,glow,Time.unscaledDeltaTime*4);
                var tint=Color.Lerp(plain,lit,ear.glow);
                if(tint!=ear.tint){ear.tint=tint;MemoryUi.Tint(paper[k],tint);}
                if(!open)continue;
                ear.band.enabled=ear.rim.enabled=shown;
                float target=on?1:beckon?.45f+.35f*Mathf.Sin(Time.unscaledTime*3):.22f;ear.lift=Mathf.MoveTowards(ear.lift,target,Time.unscaledDeltaTime*4);
                MemoryUi.Tint(ear.band,Lit(Mathf.InverseLerp(.22f,1,ear.lift),ear.press.Down));
                // Lifting a little, but always behind what is printed on the page (Front), never level with it.
                ear.band.transform.localPosition=new Vector3(ear.band.transform.localPosition.x,0,-.0015f*ear.lift);
            }
            // The turning sheet is the page that was lit: it carries that page's glow (a breath of wind, or the moment it is
            // taken up), so the page does not drop to plain under the hand.
            var sheetTint=flip!=null?Color.Lerp(plain,lit,corners[flip.forward?1:0].glow):plain;
            if(sheetTint!=this.sheetTint){this.sheetTint=sheetTint;MemoryUi.Tint(sheet.Renderer,sheetTint);}
            // Open, the cover's outside lies face down under the left page: its press goes, or a hand reaching under that
            // page's edge to turn it pokes the cover (the 2026-11-07 log: "book/cover poke select ... open already").
            bool coverLive=Closed&&!Sealed&&flight==null&&At!=Where.Walls;coverBand.enabled=coverRim.enabled=coverLive;
            if(coverPress.gameObject.activeSelf!=(Closed&&At!=Where.Walls))coverPress.gameObject.SetActive(Closed&&At!=Where.Walls);
            if(coverLive)
            {
                // Not the press's hover: a fingertip over the cover would light it, though only a pinch at the edge takes it.
                bool on=coverNear||coverGrabber>=0||GazeCover;
                coverLift=Mathf.MoveTowards(coverLift,on?1:.35f+.3f*Mathf.Sin(Time.unscaledTime*3),Time.unscaledDeltaTime*4);
                MemoryUi.Tint(coverBand,Lit(Mathf.InverseLerp(.35f,1,coverLift),coverGrabber>=0||coverPress.Down));
            }
        }
        void Step()
        {
            var f=flip;float dt=Time.unscaledDeltaTime;
            f.turn.Advance(dt*f.rate);Pose();Sound(f,dt);
            if(f.turn.Landed)Land();
        }
        // Paper rustles as fast as the page moves; its edge slaps down when it lands.
        void Sound(Flip f,float dt)
        {
            float speed=Mathf.Clamp01(Mathf.Max(Mathf.Abs(f.turn.RootSpeed),Mathf.Abs(f.turn.TipSpeed))/4)*(f.breeze?.4f:1);
            rustle.volume=Mathf.MoveTowards(rustle.volume,speed*.22f,dt*1.5f);rustle.pitch=.85f+.3f*speed;
            if(f.turn.Impact>.5f&&!f.breeze)Say(sounds?.Flap,.9f+.2f*Mathf.Clamp01(f.turn.Impact/4),.1f+.25f*Mathf.Clamp01(f.turn.Impact/4));
        }
        void Say(AudioClip clip,float pitch,float volume){if(!clip)return;voice.pitch=pitch;voice.PlayOneShot(clip,volume);}
    }

    // The open book's geometry in its own space: the spine's foot (Corner), the right page's direction from the spine
    // (Along) and the way it faces (Normal, into the open book), the way up its pages (Up, along the spine), and how far
    // it opens (radians): the left page lies that far round from the right, past its facing.
    public sealed class BookShape
    {
        public readonly Vector3 Corner,Along,Normal,Up;public readonly float Opening,Lift;
        public BookShape(Vector3 corner,Vector3 along,Vector3 normal,Vector3 up,float openingDegrees,float lift)
        {Corner=corner;Along=along.normalized;Normal=normal.normalized;Up=up.normalized;Opening=openingDegrees*Mathf.Deg2Rad;Lift=lift;}
        // Lying open flat (the book's own space: pages along x, up the page +z, the pages facing +y).
        public static BookShape Flat=>new BookShape(Vector3.zero,Vector3.right,Vector3.up,Vector3.forward,180,0);
        // A page's direction from the spine, and the way it faces.
        public Vector3 Direction(bool right)=>right?Along:Mathf.Cos(Opening)*Along+Mathf.Sin(Opening)*Normal;
        public Vector3 Facing(bool right)=>right?Normal:Mathf.Sin(Opening)*Along-Mathf.Cos(Opening)*Normal;
        // A page's frame: origin at its bottom corner on the reader's left, x to the reader's right, y up the page, -z
        // toward the reader. The right page starts at the spine, the left page at its outer edge.
        public Pose Page(bool right)=>new Pose(right?Corner:Corner+Direction(false)*MemoryBook.Width,Quaternion.LookRotation(-Facing(right),Up));
    }
}
