using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static ShadowWorkshopIntegration.Editor.PropMesh;

namespace ShadowWorkshopIntegration.Editor
{
    // Two summer things for the Summer level (2026-11-17), built like the other props (procedural, real size, smooth normals
    // doing the rounding, detail in textures generated here, mobile VR budget), staged in ShadowGym by SummerBuilder.Stage.
    // Beach ball: an inflatable ball 300 mm across, six gores pole to pole (red, white, green, white, blue, white), small
    // white caps at both poles, a valve plug on a white gore, glossy vinyl. Drawn 24 x 12 (528 triangles); its shadow is a
    // 16 x 8 sphere straddling it (224).
    // Popsicle: an orange ice lolly, the bar 95 mm tall, 50 mm wide and 18 mm thick with a rounded top and softly rounded
    // edges, two vertical grooves pressed into its front (a normal map), on a flat light-wood stick 10 x 2 mm showing 50 mm
    // below. Its shadow: the bar's outline with chamfered edges and a box for the stick.
    // Each in its own frame: metres, +y up, -z the front, standing on y = 0.
    public static class SummerBallPopsicleBuilder
    {
        const string Meshes=SummerBuilder.Meshes,Textures=SummerBuilder.Textures,MaterialFolder=SummerBuilder.MaterialFolder;
        public const string BallPath=Meshes+"Summer beach ball.asset",BallShadowPath=Meshes+"Summer beach ball shadow.asset";
        public const string PopsiclePath=Meshes+"Summer popsicle.asset",PopsicleShadowPath=Meshes+"Summer popsicle shadow.asset";
        const string GoresPath=Textures+"summer beach ball gores.png",IcePath=Textures+"summer popsicle ice.png",IceNormalPath=Textures+"summer popsicle ice normal.png";
        const string StickPath=Textures+"summer popsicle stick.png";

        static Texture2D Load(string path)=>AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        static readonly Look Vinyl=new Look("summer beach ball vinyl",Color.white,0f,.80f){baseMap=()=>Load(GoresPath)??GoresTexture()};
        static readonly Look Ice=new Look("summer popsicle ice",Color.white,0f,.55f){baseMap=()=>Load(IcePath)??IceTextures(),
            normalMap=()=>{if(!Load(IceNormalPath))IceTextures();return Load(IceNormalPath);},bumpScale=1f};
        static readonly Look Wood=new Look("summer popsicle stick",Color.white,0f,.15f){baseMap=()=>Load(StickPath)??StickTexture()};
        public static Material[] BallMaterials()=>PropMesh.Materials(MaterialFolder,new[]{Vinyl});
        // Popsicle submeshes: ice, stick.
        public static Material[] PopsicleMaterials()=>PropMesh.Materials(MaterialFolder,new[]{Ice,Wood});

        [MenuItem("MetaHackthone/Shadow Workshop/Summer/Build beach ball and popsicle")]
        public static void Build()
        {
            GoresTexture();IceTextures();StickTexture();
            var ball=Ball();var ballShadow=BallShadow();var popsicle=Popsicle();var popsicleShadow=PopsicleShadow();
            SummerBuilder.Stage("BEACH BALL",ball,ballShadow,BallMaterials());
            SummerBuilder.Stage("POPSICLE",popsicle,popsicleShadow,PopsicleMaterials());
            Debug.Log("SUMMER_BALL_POPSICLE_OK "+string.Join(", ",new[]{(ball,ballShadow),(popsicle,popsicleShadow)}.Select(m=>
                $"{m.Item1.name} {m.Item1.triangles.Length/3} (shadow {m.Item2.triangles.Length/3}), {m.Item1.bounds.size.x*1000:0} x {m.Item1.bounds.size.y*1000:0} x {m.Item1.bounds.size.z*1000:0} mm")));
        }

        // --- Beach ball ---------------------------------------------------------------------------------------------------
        const float R=.150f;
        // 24 sides (a gore every four, so gore edges run along vertices) by 12 rings; u once round, v pole to pole.
        static Mesh Ball()
        {
            var b=new PropMesh(1);var (at,normal)=Spheroid(R,R,R,12);
            b.Revolve(0,24,at,normal);
            for(int i=0;i<b.v.Count;i++)b.uv[i]=new Vector2(b.uv[i].x,Mathf.Acos(Mathf.Clamp((R-b.v[i].y)/R,-1,1))/Mathf.PI);
            return b.Save(BallPath,"Summer beach ball");
        }
        // A coarser sphere pushed out to straddle the round one (corners as far out as flats are in). 224 triangles.
        static Mesh BallShadow()
        {
            var b=new PropMesh(1);float grow=2/(1+Mathf.Cos(Mathf.PI/16));var (at,normal)=Spheroid(R*grow,R*grow,R,8);
            b.Revolve(0,16,at,normal);
            return b.Save(BallShadowPath,"Summer beach ball shadow");
        }

        // --- Popsicle -----------------------------------------------------------------------------------------------------
        const float BarW=.050f,BarH=.095f,BarT=.018f,StickOut=.050f,StickW=.010f,StickT=.002f,StickIn=.012f;
        // The bar's outline (x, y): small round corners at the bottom, a big round top with a short flat between.
        static Vector2[] BarOutline(int bottomSteps,int topSteps)=>RoundCorners(new[]{
            (new Vector2(-BarW/2,StickOut),.0065f,bottomSteps),(new Vector2(BarW/2,StickOut),.0065f,bottomSteps),
            (new Vector2(BarW/2,StickOut+BarH),.022f,topSteps),(new Vector2(-BarW/2,StickOut+BarH),.022f,topSteps)},true).ToArray();
        // Ice UVs: planar (x, y); the front face in the texture's left half (with the grooves), the rest in its plain right half.
        const float IceSpanX=.060f,IceSpanY=.100f,IceBelow=.0025f;
        static Mesh Popsicle()
        {
            var b=new PropMesh(2);const int IceM=0,WoodM=1;
            var bar=BarOutline(2,4);int start=b.v.Count;
            b.Slab(IceM,i=>IceM,bar,.006f,p=>BarT/2,p=>0,2,UvPerMetre);
            int front=b.v.Count-bar.Length;   // Slab adds the back cap, then the front cap last
            for(int i=start;i<b.v.Count;i++)
            {
                var p=b.v[i];float u=(p.x+IceSpanX/2)/IceSpanX*.5f,v=(p.y-StickOut+IceBelow)/IceSpanY;
                b.uv[i]=new Vector2(i>=front?u:u+.5f,v);
            }
            // Stick: rounded bottom end, its top buried 12 mm in the bar.
            var stick=RoundCorners(new[]{(new Vector2(-StickW/2,0),StickW/2,3),(new Vector2(StickW/2,0),StickW/2,3),
                (new Vector2(StickW/2,StickOut+StickIn),0f,0),(new Vector2(-StickW/2,StickOut+StickIn),0f,0)},true).ToArray();
            b.Slab(WoodM,i=>WoodM,stick,StickT/2,p=>StickT/2,p=>0,1,UvPerMetre);
            return b.Save(PopsiclePath,"Summer popsicle");
        }
        // The bar's outline coarser with chamfered edges, and a plain box for the stick. 104 triangles.
        static Mesh PopsicleShadow()
        {
            var b=new PropMesh(1);
            b.Slab(0,i=>0,BarOutline(1,3),.004f,p=>BarT/2,p=>0,1,UvPerMetre);
            b.Box(0,new Vector3(-StickW/2,0,-StickT/2),new Vector3(StickW/2,StickOut+.004f,StickT/2));
            return b.Save(PopsicleShadowPath,"Summer popsicle shadow");
        }

        // --- Textures (detail colours kept at about 0.6 or below: the gym's light lifts lit faces about 1.5x) ------------------
        static Vector3 OnBall(float u,float v){float lon=u*2*Mathf.PI,lat=(v-.5f)*Mathf.PI;return new Vector3(Mathf.Cos(lat)*Mathf.Cos(lon),Mathf.Sin(lat),Mathf.Cos(lat)*Mathf.Sin(lon));}
        // Gores round u (the red one's right edge straight in front, at u 0.75), thin darker seams, white caps 11 degrees
        // round each pole and a valve plug on the white gore right of the red.
        static Texture2D GoresTexture()
        {
            const int W=512,H=256;const float Seam=.0008f,Cap=11f*Mathf.Deg2Rad;var tex=new Texture2D(W,H,TextureFormat.RGB24,false,false);
            var white=new Color(.60f,.60f,.58f);
            var gores=new[]{new Color(.60f,.06f,.05f),white,new Color(.08f,.42f,.13f),white,new Color(.05f,.20f,.55f),white};
            var valve=OnBall(10f/12f,.5f+28f/180f);
            for(int y=0;y<H;y++)for(int x=0;x<W;x++)
            {
                float u=(x+.5f)/W,v=(y+.5f)/H,lat=(v-.5f)*Mathf.PI,f=Mathf.Repeat(u-7f/12f,1f)*6;int g=Mathf.Min(5,(int)f);
                float polar=Mathf.Min(v,1-v)*Mathf.PI;Color c;
                if(polar<Cap)c=white;
                else
                {
                    c=gores[g];float edge=Mathf.Min(f-Mathf.Floor(f),Mathf.Ceil(f)-f)/6*2*Mathf.PI*R*Mathf.Cos(lat);
                    c*=Mathf.Lerp(.80f,1f,Mathf.Clamp01(edge/Seam-.5f));
                }
                c*=Mathf.Lerp(.80f,1f,Mathf.Clamp01(Mathf.Abs(polar-Cap)*R/Seam-.5f));
                float d=Vector3.Angle(OnBall(u,v),valve)*Mathf.Deg2Rad*R;
                if(d<.0045f)c=new Color(.52f,.52f,.50f);else if(d<.0065f)c=new Color(.40f,.40f,.39f);
                c.a=1;tex.SetPixel(x,y,c);
            }
            return SaveTexture(GoresPath,tex,false);
        }
        // Orange ice (brighter than the 0.6 guide: a flat colour clipping on lit faces only gets more orange; the grooves stay
        // under it), faintly mottled with pale frost specks; the left half (the front face) with two grooves 3.2 mm wide and
        // 1 mm deep, darker inside, and their normal map.
        static float Groove(float x,float y)
        {
            const float Half=.0016f,Depth=.0010f,Y0=.030f,Y1=.070f;float h=0;
            foreach(var gx in new[]{-.0065f,.0065f})
            {
                float d=new Vector2(x-gx,y-Mathf.Clamp(y,Y0,Y1)).magnitude;
                if(d<Half)h-=Depth*(1+Mathf.Cos(d/Half*Mathf.PI))/2;
            }
            return h;
        }
        static Texture2D IceTextures()
        {
            const int W=256,H=256;const float E=.0002f;
            var albedo=new Texture2D(W,H,TextureFormat.RGB24,false,false);var normal=new Texture2D(W,H,TextureFormat.RGB24,false,true);
            for(int y=0;y<H;y++)for(int x=0;x<W;x++)
            {
                float u=(x+.5f)/W,v=(y+.5f)/H;bool front=u<.5f;float lu=front?u*2:(u-.5f)*2;
                float px=lu*IceSpanX-IceSpanX/2,py=v*IceSpanY-IceBelow;
                float n=Fbm(lu,v,5,8,4,301),speck=Fbm(lu,v,24,40,2,307);
                var c=Color.Lerp(new Color(.74f,.40f,.05f),new Color(.79f,.45f,.07f),n);
                c=Color.Lerp(c,new Color(.80f,.54f,.22f),Mathf.Clamp01((speck-.72f)*12)*.30f);
                var nn=Vector3.forward;
                if(front)
                {
                    float h=Groove(px,py);c*=1-.32f*Mathf.Clamp01(-h/.0010f);
                    nn=new Vector3(-(Groove(px+E,py)-Groove(px-E,py))/(2*E),-(Groove(px,py+E)-Groove(px,py-E))/(2*E),1).normalized;
                }
                c.a=1;albedo.SetPixel(x,y,c);normal.SetPixel(x,y,new Color(nn.x*.5f+.5f,nn.y*.5f+.5f,nn.z*.5f+.5f));
            }
            SaveTexture(IceNormalPath,normal,true,TextureWrapMode.Clamp);
            return SaveTexture(IcePath,albedo,false,TextureWrapMode.Clamp);
        }
        // Pale birch, grain running up the stick (v).
        static Texture2D StickTexture()
        {
            const int S=128;var tex=new Texture2D(S,S,TextureFormat.RGB24,false,false);
            for(int y=0;y<S;y++)for(int x=0;x<S;x++)
            {
                float u=(x+.5f)/S,v=(y+.5f)/S,grain=Fbm(u,v,24,3,4,311);
                var c=Color.Lerp(new Color(.50f,.38f,.23f),new Color(.58f,.46f,.30f),grain);
                c=Color.Lerp(c,new Color(.44f,.31f,.18f),Mathf.Clamp01((grain-.62f)*10)*.5f);
                c.a=1;tex.SetPixel(x,y,c);
            }
            return SaveTexture(StickPath,tex,false);
        }
    }
}
