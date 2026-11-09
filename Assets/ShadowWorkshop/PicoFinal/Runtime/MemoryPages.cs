using System;
using System.Collections.Generic;
using CloudGarden.ShadowPuzzle;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShapeOfMemory
{
    // What gets printed on the book's pages: text, rules and ornaments, picture plates and the button tiles. Places are
    // in page units (u from the reader's left, v up, metres) on a page's content root (MemoryBook.Fill). Shares one
    // material per texture and one picture per level, so pages can be rebuilt freely.
    public sealed class MemoryPages
    {
        // Printed on the paper: body text, secondary lines, and gold-role words (chapter numbers, rules, FOUND, hints).
        public static readonly Color Ink=MemoryPalette.Sepia,Faded=MemoryPalette.SepiaSoft,Gilt=MemoryPalette.GoldDeep;
        // In front of the lamps' walls and their shadows (Torch2Room draws them up to 3 mm off the page). Layer: the gap
        // between things stacked on a page (a mark on a plate, a plate on its frame), wide enough to keep them apart in
        // the depth buffer even at the flat book's third of the size.
        public const float Front=-.006f,Layer=.002f;
        readonly MemoryKit kit;
        readonly Dictionary<(Material,Texture),Material> materials=new Dictionary<(Material,Texture),Material>();
        readonly Dictionary<string,Texture2D> pictures=new Dictionary<string,Texture2D>();
        public MemoryPages(MemoryKit kit){this.kit=kit;}

        public static Vector3 At(float u,float v,float z=Front)=>new Vector3(u-MemoryBook.Width*.5f,v-MemoryBook.Height*.5f,z);
        public static float Middle=>MemoryBook.Width*.5f;
        public TMP_Text Text(Transform face,string text,float u,float v,float size,float width,float height,Color color,FontStyles style=FontStyles.Normal,float spacing=0,TextAlignmentOptions align=TextAlignmentOptions.Center)
        {
            var label=MemoryUi.Label(text,face,At(u,v),Quaternion.identity,size,new Vector2(width,height),color,align);
            label.fontStyle=style;label.characterSpacing=spacing;label.enableAutoSizing=true;label.fontSizeMin=size*4;label.fontSizeMax=size*10;return label;
        }
        public Renderer Quad(Transform face,string name,float u,float v,Vector2 size,Material material,Texture texture,Color color,float z=Front,float turn=0)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Quad);go.name=name;MemoryUi.Kill(go.GetComponent<Collider>());go.transform.SetParent(face,false);
            go.transform.localPosition=At(u,v,z);go.transform.localRotation=Quaternion.Euler(0,0,turn);go.transform.localScale=new Vector3(size.x,size.y,1);
            var r=go.GetComponent<Renderer>();r.sharedMaterial=Material(material,texture);r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;MemoryUi.Tint(r,color);return r;
        }
        Material Material(Material material,Texture texture)
        {
            if(materials.TryGetValue((material,texture),out var m)&&m)return m;
            m=new Material(material){name=material.name+" "+(texture?texture.name:"plain")};if(texture)m.SetTexture("_BaseMap",texture);m.SetColor("_BaseColor",Color.white);
            kit.Owned.Add(m);return materials[(material,texture)]=m;
        }
        // A gilt rule with a diamond in its middle.
        public void Rule(Transform face,float u,float v,float width,Color color)
        {
            Quad(face,"Rule",u,v,new Vector2(width,.004f),kit.Screen,null,color);
            Quad(face,"Diamond",u,v,Vector2.one*.022f,kit.Screen,null,color,Front-Layer,45);
        }
        // A framed picture plate: a thin Sepia frame, the picture (on Paper) or a mark on PaperShade inside it.
        public void Plate(Transform face,float u,float v,float size,Texture picture,string mark,Color markColor)
        {
            Quad(face,"Plate frame",u,v,Vector2.one*(size+.016f),kit.Screen,null,Ink,Front+Layer);
            Quad(face,"Plate",u,v,Vector2.one*size,kit.Screen,picture,picture?Color.white:MemoryPalette.PaperShade);
            if(!picture&&mark!=null)Text(face,mark,u,v,size*.45f,size,size,markColor,FontStyles.Bold).transform.localPosition+=new Vector3(0,0,-Layer);
        }
        // Over whatever it is printed on (a plate's lock).
        public void Icon(Transform face,float u,float v,float size,Texture2D icon,Color color){if(icon)Quad(face,"Icon",u,v,Vector2.one*size,kit.Icon,icon,color,Front-Layer);}
        // The level's found picture: the pile's shadow at the answer, drawn from its exact outline (Torch2Projection.Outline,
        // what the walls show) at PictureSize with 2x2 samples a pixel, not the score's 64-cell raster. The chapter's Deep on paper,
        // framed on the shadow's bounds with a margin; two-wall levels put the two side by side. Kept for the session.
        public const int PictureSize=512;
        public const float PictureMargin=1.14f;
        static readonly Color PictureColour=MemoryPalette.Paper;
        public Texture2D Picture(Torch2Level level)
        {
            if(pictures.TryGetValue(level.name,out var cached)&&cached)return cached;
            var projection=new Torch2Projection(level,false);int walls=projection.walls.Length;const int S=PictureSize;
            var pixels=Blank(S);
            if(walls==1)Draw(projection,0,level,pixels,S,0,0,S);
            else{int half=S/2,top=(S-half)/2;for(int slot=0;slot<walls&&slot<2;slot++)Draw(projection,slot,level,pixels,S,slot*half,top,half);}
            return pictures[level.name]=Make("Memory "+level.name,pixels);
        }
        // One wall's picture only (two-wall levels print each on its own page).
        public Texture2D Picture(Torch2Level level,int slot)
        {
            string key=level.name+"/"+slot;if(pictures.TryGetValue(key,out var cached)&&cached)return cached;
            var projection=new Torch2Projection(level,false);var pixels=Blank(PictureSize);
            Draw(projection,slot,level,pixels,PictureSize,0,0,PictureSize);
            return pictures[key]=Make("Memory "+key,pixels);
        }
        static Color32[] Blank(int size){var pixels=new Color32[size*size];Color32 paper=PictureColour;for(int i=0;i<pixels.Length;i++)pixels[i]=paper;return pixels;}
        Texture2D Make(string name,Color32[] pixels)
        {
            var texture=new Texture2D(PictureSize,PictureSize,TextureFormat.RGBA32,true){name=name,filterMode=FilterMode.Trilinear,anisoLevel=4,wrapMode=TextureWrapMode.Clamp};kit.Owned.Add(texture);
            texture.SetPixels32(pixels);texture.Apply(true,!Readable);return texture;
        }
        // Keep the pictures' pixels on the CPU too (tests); otherwise only the GPU copy stays.
        public bool Readable;
        readonly List<Vector2> outline=new List<Vector2>();
        // One wall's shadow at the answer into a size x size square at (x0, y0) of a stride-wide image.
        void Draw(Torch2Projection projection,int slot,Torch2Level level,Color32[] pixels,int stride,int x0,int y0,int size)
        {
            projection.Outline(slot,level.solution,Torch2Projection.Home,outline);if(outline.Count<3)return;
            var min=outline[0];var max=outline[0];foreach(var q in outline){min=Vector2.Min(min,q);max=Vector2.Max(max,q);}
            var centre=(min+max)*.5f;float extent=Mathf.Max(max.x-min.x,max.y-min.y)*.5f*PictureMargin;if(extent<1e-5f)return;
            int n=size*2;var samples=new bool[n*n];var corner=centre-Vector2.one*extent;float scale=n/(2*extent);
            for(int i=0;i+2<outline.Count;i+=3)Fill(samples,n,(outline[i]-corner)*scale,(outline[i+1]-corner)*scale,(outline[i+2]-corner)*scale);
            Color paper=PictureColour,ink=MemoryPalette.Deep(MemoryPalette.ChapterOf(level));
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                int s=(2*y)*n+2*x,count=(samples[s]?1:0)+(samples[s+1]?1:0)+(samples[s+n]?1:0)+(samples[s+n+1]?1:0);
                if(count>0)pixels[(y0+y)*stride+x0+x]=Color.Lerp(paper,ink,count*.25f);
            }
        }
        // Fills a triangle's sample centres, row by row (sample units: one per sample, y up).
        static void Fill(bool[] samples,int n,Vector2 a,Vector2 b,Vector2 c)
        {
            int y0=Mathf.Max(0,Mathf.CeilToInt(Mathf.Min(a.y,Mathf.Min(b.y,c.y))-.5f)),y1=Mathf.Min(n-1,Mathf.FloorToInt(Mathf.Max(a.y,Mathf.Max(b.y,c.y))-.5f));
            for(int y=y0;y<=y1;y++)
            {
                float yc=y+.5f,left=float.PositiveInfinity,right=float.NegativeInfinity;
                Cross(a,b,yc,ref left,ref right);Cross(b,c,yc,ref left,ref right);Cross(c,a,yc,ref left,ref right);
                if(left>right)continue;
                int x0=Mathf.Max(0,Mathf.CeilToInt(left-.5f)),x1=Mathf.Min(n-1,Mathf.FloorToInt(right-.5f));
                for(int x=x0;x<=x1;x++)samples[y*n+x]=true;
            }
        }
        static void Cross(Vector2 p,Vector2 q,float y,ref float left,ref float right)
        {
            if((p.y>y)==(q.y>y)&&p.y!=y&&q.y!=y)return;
            if(Mathf.Approximately(p.y,q.y)){left=Mathf.Min(left,Mathf.Min(p.x,q.x));right=Mathf.Max(right,Mathf.Max(p.x,q.x));return;}
            float x=p.x+(q.x-p.x)*(y-p.y)/(q.y-p.y);left=Mathf.Min(left,x);right=Mathf.Max(right,x);
        }
        // The title, set like a book's title page: SHAPE, a small italic "of", MEMORY, a gilt rule, the progress line.
        public void TitlePage(Transform face,string progress)
        {
            float m=Middle;
            Text(face,"A  BOOK  OF  SHADOWS",m,.90f,.024f,.8f,.05f,Faded,FontStyles.Normal,14);
            foreach(var (word,v) in new[]{("SHAPE",.70f),("MEMORY",.44f)})
            {
                // A gilt shadow a little down and right, as if stamped into the paper; drawn before the ink whatever the
                // view (on a flat page it can be the nearer).
                var stamp=Text(face,word,m+.005f,v-.005f,.135f,1f,.17f,Gilt,FontStyles.Bold,10);stamp.transform.localPosition+=new Vector3(0,0,.0008f);stamp.GetComponent<Renderer>().sortingOrder=-1;
                Text(face,word,m,v,.135f,1f,.17f,Ink,FontStyles.Bold,10);
            }
            Text(face,"of",m,.575f,.055f,.3f,.08f,Gilt,FontStyles.Italic);
            Rule(face,m,.31f,.46f,Gilt);
            Text(face,progress,m,.20f,.03f,.8f,.06f,Faded,FontStyles.Italic);
        }
        public static string Roman(int n)=>n switch{1=>"I",2=>"II",3=>"III",4=>"IV",5=>"V",6=>"VI",7=>"VII",8=>"VIII",9=>"IX",10=>"X",_=>n.ToString()};
    }

    // A button printed as a raised tile on a page: a leather square with a gold icon, its name beneath. It rises toward
    // the hand on hover and sinks into the page when pressed; leather and gold whatever its state. On a page that is turning or about to be uncovered it is
    // built without Press and pressed nearly flat (Flat), so the turning page can pass over it; a live one pops up out of
    // the page (PopSeconds) when its page lands.
    public sealed class MemoryTile : MonoBehaviour
    {
        // Rise: how far it comes up toward a hovering finger (page units; about 5 mm on the stand), ringed (a dark Ink800 rim
        // with an aqua outline outside it: Hover, Grab while pressed) and with a soft tick (HoverTick) as it does. One that
        // can't be pressed now (the book flying or turning) just doesn't ring.
        public const float Depth=.016f,Flat=.15f,PopSeconds=.25f,Rise=.016f,Sink=.01f;
        public static readonly Color Leather=MemoryPalette.Leather;
        public static Action HoverTick;
        public Pressable Press{get;private set;}
        public string Caption{get;private set;}
        Transform body;
        Renderer rim,outline;
        float rise,pop;
        bool hovered;

        public static MemoryTile Create(MemoryPages pages,Transform face,string name,string caption,Texture2D iconTexture,float u,float v,float size,MemoryKit kit,
            Action action,Func<bool> enabled,Func<bool> handsAllowed,bool live)
        {
            var root=new GameObject("Tile "+name).transform;root.SetParent(face,false);root.localPosition=MemoryPages.At(u,v,0);
            var tile=root.gameObject.AddComponent<MemoryTile>();tile.Caption=caption;
            tile.body=new GameObject("Body").transform;tile.body.SetParent(root,false);
            MemoryUi.Shape(PrimitiveType.Cube,"Leather",tile.body,new Vector3(0,0,-Depth*.5f-.002f),Quaternion.identity,new Vector3(size,size,Depth),kit.Surface,Leather);
            if(iconTexture)pages.Quad(tile.body,"Icon",MemoryPages.Middle,MemoryBook.Height*.5f,Vector2.one*size*.55f,kit.Icon,iconTexture,MemoryPalette.Gold,-Depth-.0035f);
            pages.Text(face,caption,u,v-size*.5f-.045f,.032f,size+.16f,.06f,MemoryPages.Ink,FontStyles.Bold,6);
            tile.body.localScale=new Vector3(1,1,Flat);
            if(live)
            {
                var hit=new GameObject("Press",typeof(BoxCollider));hit.transform.SetParent(root,false);hit.transform.localPosition=new Vector3(0,0,-Depth*.5f-.002f);hit.transform.localScale=new Vector3(size,size,Depth);
                tile.Press=Pressable.Add(hit,action,enabled,handsAllowed,new Vector3(0,0,-.51f),Quaternion.identity);
                tile.rim=pages.Quad(face,"Tile rim",u,v,Vector2.one*(size*1.2f),kit.Screen,null,MemoryPalette.Ink800,MemoryPages.Front+MemoryPages.Layer);tile.rim.enabled=false;
                tile.outline=pages.Quad(face,"Tile outline",u,v,Vector2.one*(size*1.32f),kit.Screen,null,MemoryPalette.AquaHover,MemoryPages.Front+MemoryPages.Layer*2);tile.outline.enabled=false;
            }
            return tile;
        }
        void Update()
        {
            if(Press==null)return;
            bool available=Press.Available,down=available&&Press.Down,hover=available&&Press.Hover;
            if(hover&&!hovered)HoverTick?.Invoke();hovered=hover;
            if(pop<1){pop=Mathf.Min(1,pop+Time.unscaledDeltaTime/PopSeconds);float eased=1-(1-pop)*(1-pop)*(1-pop);body.localScale=new Vector3(1,1,Mathf.LerpUnclamped(Flat,1,eased));}
            rise=Mathf.MoveTowards(rise,down?-Sink:hover?Rise:0,Time.unscaledDeltaTime*.2f);body.localPosition=new Vector3(0,0,-rise);
            bool ring=available&&(hover||down);
            if(rim)rim.enabled=ring;
            if(outline){outline.enabled=ring;if(ring)MemoryUi.Tint(outline,down?MemoryPalette.AquaGrab:MemoryPalette.AquaHover);}
        }
        void OnDestroy(){if(rim)Destroy(rim.gameObject);if(outline)Destroy(outline.gameObject);}
    }
}
