using UnityEditor;
using UnityEngine;
using static ShadowWorkshopIntegration.Editor.PropMesh;

namespace ShadowWorkshopIntegration.Editor
{
    // A picture postcard for the WildWest level, wordless: a flat-colour landscape (peach sky, a pale sun, five ridges
    // fading from sky blue to night navy, dark pine silhouettes) over a plain white strip; on the back a blank landscape
    // postcard layout (divider, address lines, a dashed stamp box). 100 x 148 mm, 0.5 mm card, 12 triangles; the card casts its
    // own shadow. One 1024 atlas drawn here: the front, the back and a white patch for the edges.
    public static class WesternPostcardBuilder
    {
        const string Art="Assets/ShadowWorkshop/Art/Torch2/";
        public const string MeshPath=Art+"Meshes/Postcard.asset",PrintPath=Art+"Textures/postcard print.png";
        const string MaterialFolder=Art+"Materials/";
        const float W=.100f,H=.148f,T=.0005f;const int Size=1024;
        // Atlas regions in pixels (x, y from the bottom left, w, h).
        static readonly RectInt Front=new RectInt(4,4,690,1016),Back=new RectInt(702,4,318,470),Edge=new RectInt(702,486,16,16);

        public static readonly WildWestBuilder.Prop Prop=new WildWestBuilder.Prop{label="POSTCARD",file="Postcard",build=BuildMesh,materials=Materials};
        public static Material[] Materials()=>PropMesh.Materials(MaterialFolder,new[]{
            new Look("postcard print",Color.white,0f,.18f){baseMap=()=>AssetDatabase.LoadAssetAtPath<Texture2D>(PrintPath)??Print()}});

        [MenuItem("MetaHackthone/Shadow Workshop/Build postcard")]
        public static void Build()
        {
            Print();var mesh=BuildMesh();Materials();
            Debug.Log($"POSTCARD_OK {mesh.triangles.Length/3} tris");
        }

        // The card: front (-z) and back (+z) quads on their atlas regions, four thin white edges.
        public static Mesh BuildMesh()
        {
            var b=new PropMesh(1);float x=W/2,y=H/2,z=T/2;
            Vector2 F(float px,float py)=>new Vector2((Front.x+(px+x)/W*Front.width)/Size,(Front.y+(py+y)/H*Front.height)/Size);
            Vector2 B(float px,float py)=>new Vector2((Back.x+(x-px)/W*Back.width)/Size,(Back.y+(py+y)/H*Back.height)/Size);
            var e=new Vector2((Edge.x+Edge.width/2f)/Size,(Edge.y+Edge.height/2f)/Size);
            b.Quad(0,b.Add(new Vector3(-x,-y,-z),Vector3.back,F(-x,-y)),b.Add(new Vector3(-x,y,-z),Vector3.back,F(-x,y)),b.Add(new Vector3(x,y,-z),Vector3.back,F(x,y)),b.Add(new Vector3(x,-y,-z),Vector3.back,F(x,-y)));
            b.Quad(0,b.Add(new Vector3(x,-y,z),Vector3.forward,B(x,-y)),b.Add(new Vector3(x,y,z),Vector3.forward,B(x,y)),b.Add(new Vector3(-x,y,z),Vector3.forward,B(-x,y)),b.Add(new Vector3(-x,-y,z),Vector3.forward,B(-x,-y)));
            void Side(Vector3 a,Vector3 c,Vector3 n){var d=new Vector3(0,0,T);b.Quad(0,b.Add(a-d/2,n,e),b.Add(a+d/2,n,e),b.Add(c+d/2,n,e),b.Add(c-d/2,n,e));}
            Side(new Vector3(-x,-y,0),new Vector3(-x,y,0),Vector3.left);Side(new Vector3(-x,y,0),new Vector3(x,y,0),Vector3.up);
            Side(new Vector3(x,y,0),new Vector3(x,-y,0),Vector3.right);Side(new Vector3(x,-y,0),new Vector3(-x,-y,0),Vector3.down);
            return b.Save(MeshPath,"Postcard");
        }

        // --- The print -----------------------------------------------------------------------------------------------
        static readonly Color SkyTop=new Color(.98f,.73f,.60f),SkyLow=new Color(.99f,.81f,.67f),Sun=new Color(1f,.98f,.94f),
            Pine=new Color(.11f,.20f,.26f),Strip=new Color(.97f,.96f,.94f),Paper=new Color(.96f,.94f,.90f),Ink=new Color(.52f,.51f,.49f);   // ink dark enough to survive the gym's lit faces
        static readonly Color[] Ridges={new Color(.56f,.70f,.97f),new Color(.42f,.61f,.91f),new Color(.31f,.50f,.80f),new Color(.22f,.38f,.62f),new Color(.15f,.27f,.41f)};
        static Texture2D Print()
        {
            var px=new Color[Size*Size];for(int i=0;i<px.Length;i++)px[i]=Color.white;
            Paint(px,Front,FrontArt);Paint(px,Back,BackArt);
            var tex=new Texture2D(Size,Size,TextureFormat.RGB24,false,false);tex.SetPixels(px);
            return SaveTexture(PrintPath,tex,false,TextureWrapMode.Clamp);
        }
        // Each pixel of a region from art(u, v) in mm from its bottom left (2 x 2 samples), a few pixels of bleed round it.
        static void Paint(Color[] px,RectInt r,System.Func<float,float,Color> art)
        {
            float sx=W*1000/r.width,sy=H*1000/r.height;
            for(int y=r.y-3;y<r.yMax+3;y++)for(int x=r.x-3;x<r.xMax+3;x++)
            {
                if(x<0||y<0||x>=Size||y>=Size)continue;var c=Color.clear;
                for(int j=0;j<2;j++)for(int i=0;i<2;i++)c+=art(Mathf.Clamp((x-r.x+(i+.5f)/2)*sx,0,W*1000),Mathf.Clamp((y-r.y+(j+.5f)/2)*sy,0,H*1000));
                c/=4;c.a=1;px[y*Size+x]=c;
            }
        }
        // Landscape over a 26 mm white strip, inside a 3 mm white margin; u, v in mm.
        static Color FrontArt(float u,float v)
        {
            const float M=3,StripTop=26;float w=W*1000,h=H*1000;
            if(u<M||u>w-M||v>h-M||v<StripTop)return Strip;
            float x=(u-M)/(w-2*M),y=(v-StripTop)/(h-M-StripTop);   // 0..1 over the picture
            var c=Color.Lerp(SkyLow,SkyTop,Mathf.SmoothStep(0,1,(y-.35f)/.65f));
            if(new Vector2((x-.66f)*(w-2*M),(y-.76f)*(h-M-StripTop)).magnitude<13)c=Sun;
            for(int k=0;k<Ridges.Length;k++)if(y<Ridge(k,x))c=Ridges[k];
            if(InPine(x,y,.07f,-.02f,.95f,.15f,9,3)||InPine(x,y,.93f,.08f,.62f,.13f,7,5)||InPine(x,y,.84f,.03f,.42f,.10f,6,7)
               ||InPine(x,y,.36f,.22f,.40f,.055f,8,11)||InPine(x,y,.44f,.18f,.22f,.06f,5,13))c=Pine;
            if(y<.07f+.035f*Fbm(x,0,14,1,3,17))c=Pine;   // a dark band of treetops along the bottom
            return c;
        }
        // Ridge k's height (0..1 of the picture) across x: further ridges higher and paler, all gently rolling.
        static float Ridge(int k,float x)
        {
            float[] baseY={.58f,.49f,.39f,.28f,.17f},amp={.035f,.05f,.06f,.07f,.06f};
            float s=Mathf.Sin(x*(5+k)*1.3f+k*1.7f)*.55f+Mathf.Sin(x*(11+2*k)+k*.9f)*.25f+(Fbm(x,k*.13f,8,1,3,23+k)-.5f)*.8f;
            return baseY[k]+amp[k]*s;
        }
        // A pine silhouette: base (bx, by), height h, widest half-width hw, its tiers, a seed for its ragged edge.
        static bool InPine(float x,float y,float bx,float by,float h,float hw,int tiers,int seed)
        {
            float t=(y-by)/h;if(t<0||t>1)return false;float dx=Mathf.Abs(x-bx);
            if(t<.08f)return dx<hw*.08f;   // trunk
            float tier=Mathf.Repeat((t-.08f)/.92f*tiers,1),taper=1-(t-.08f)/.92f;
            float half=hw*taper*Mathf.Lerp(1f,.35f,tier)*(.8f+.4f*Fbm(t*3,x*.1f,24,1,2,seed));
            return dx<Mathf.Max(half,hw*.12f*taper);   // the thinnest part narrows to a point at the top
        }
        // A blank postcard back, laid out landscape like a real one (turn the card a quarter clockwise to read it: the
        // picture's top edge becomes the back's right edge): a centre divider, three address lines, a dashed stamp box
        // top right. u, v in mm on the portrait region; s, t in mm across and up the landscape back.
        static Color BackArt(float u,float v)
        {
            float w=H*1000,h=W*1000,s=v,t=h-u;var c=Paper;
            if(Mathf.Abs(s-w/2)<.35f&&t>12&&t<h-12)c=Ink;
            foreach(var ly in new[]{.22f,.35f,.48f})if(Mathf.Abs(t-h*ly)<.3f&&s>w/2+7&&s<w-8)c=Ink;
            float sx0=w-7-20,sx1=w-7,sy0=h-7-24,sy1=h-7;bool onBox=(Mathf.Abs(s-sx0)<.35f||Mathf.Abs(s-sx1)<.35f)&&t>sy0&&t<sy1||(Mathf.Abs(t-sy0)<.35f||Mathf.Abs(t-sy1)<.35f)&&s>sx0&&s<sx1;
            if(onBox&&Mathf.Repeat((s+t)*.5f,1)<.6f)c=Ink;
            return c;
        }
    }
}
