using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using static ShadowWorkshopIntegration.Editor.PropMesh;

namespace ShadowWorkshopIntegration.Editor
{
    // A plain, quiet map for the folded travel map (Art/TravelMap, BonVoyage_NEW): low-saturation flat colours, no outlines,
    // no words. A sea in the lower left fed by a winding river, a few sage parks, white roads edged in grey (three main,
    // three minor) with bridges over the river, and a dashed terracotta route from a small ring to a pin. Painted into
    // Folded Travel Map simple.png, which keeps the original's layout (the front's four panels in the top 5/8 of a 1024
    // square; the plain back and the edge strip copied from Folded Travel Map.png), and the map's material pointed at it,
    // so the gym and every level using the map show it. The mesh and its shadow are unchanged. Laid out in mm on the
    // sheet as placed (the gym's and BonVoyage_NEW's 1.2415 x scale), each fold panel's width measured there, so nothing
    // is stretched across the folds.
    public static class TravelMapBuilder
    {
        const string Folder="Assets/ShadowWorkshop/Art/TravelMap/";
        const string OriginalPath=Folder+"Folded Travel Map.png",PrintPath=Folder+"Folded Travel Map simple.png",MaterialPath=Folder+"Folded Travel Map.mat";
        // The front's panels: their u ranges in the texture (v from FrontV up) and widths on the sheet (mm).
        static readonly float[] PanelU={0,.3125f,.5371094f,.7558594f,1};
        static readonly float[] PanelMm={34.258f,21.767f,17.890f,21.397f};
        const float FrontV=.375f,MapH=72.951f,P=16;   // sheet height (mm); canvas pixels per mm
        const int Size=1024;
        static float MapW=>PanelMm[0]+PanelMm[1]+PanelMm[2]+PanelMm[3];
        static float Fold(int k){float s=0;for(int i=0;i<k;i++)s+=PanelMm[i];return s;}

        [MenuItem("MetaHackthone/Shadow Workshop/Build simple travel map")]
        public static void Build()
        {
            var tex=Print();var mat=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            mat.SetTexture("_BaseMap",tex);if(mat.HasProperty("_MainTex"))mat.SetTexture("_MainTex",tex);
            EditorUtility.SetDirty(mat);AssetDatabase.SaveAssetIfDirty(mat);
            Debug.Log($"TRAVEL_MAP_OK {MapW:F1} x {MapH:F1} mm, {mat.name} -> {tex.name}");
        }

        // --- Palette -------------------------------------------------------------------------------------------------
        // Mid tones, and roads edged in grey, so the map still reads on the folds the light pushes towards white.
        static readonly Color Paper=new Color(.88f,.86f,.80f),Land=new Color(.82f,.79f,.72f),Sand=new Color(.77f,.73f,.64f),
            Water=new Color(.52f,.62f,.68f),WaterDeep=new Color(.47f,.58f,.64f),Shore=new Color(.60f,.69f,.74f),
            Park=new Color(.60f,.67f,.54f),Road=Color.white,Street=new Color(.96f,.95f,.92f),Casing=new Color(.56f,.54f,.50f),Route=new Color(.66f,.34f,.27f),
            Shadow=new Color(.25f,.25f,.25f);

        // --- Layout (mm on the sheet, x from the left edge, y up from the bottom) ---------------------------------------
        static readonly Vector2[] Coast=V(-6,33, 3,32.2f, 9,30.4f, 14,27.2f, 17.5f,23.2f, 20.4f,18.6f, 23.6f,14.2f, 27.6f,10.6f, 31,6.4f, 33.2f,1.5f, 34.5f,-6);
        static readonly Vector2[] River=V(63,76, 61.5f,66, 64.5f,57, 60,49, 51,45, 43,40.5f, 38,33, 31.5f,27.5f, 24.5f,22.5f, 18,19);
        static readonly (Vector2[] path,float half)[] Roads={
            (V(-2,47, 12,46, 26,44.5f, 40,46, 54,52, 68,53.5f, 82,50, 98,51),.55f),
            (V(28,-2, 36,14, 44,26, 52,37, 62,46, 74,60, 84,76),.55f),
            (V(70,-2, 71,15, 69.5f,30, 72,44, 70.5f,62, 72,76),.45f),
            (V(40,-2, 46,10, 58,16, 72,17.5f, 98,20),.26f),
            (V(22,76, 26,58, 26,44.5f),.26f),
            (V(88,-2, 86,20, 82,35, 84,50.4f),.26f)};
        // Parks as unions of ellipses (centre, radii, turn).
        static readonly (float x,float y,float rx,float ry,float turn)[][] Parks={
            new[]{(10f,60f,8f,5.5f,10f),(17f,64f,6f,4.5f,-15f),(5f,66f,5f,4f,0f)},
            new[]{(84f,58f,6.5f,4.5f,20f),(89f,62f,4.5f,3.5f,0f)},
            new[]{(46f,21f,4.2f,3f,-10f)},
            new[]{(78f,22f,5f,3.4f,15f)}};
        static readonly Vector2[] Trip=V(16.5f,41.5f, 24,43.6f, 35,42.6f, 45,47.5f, 55,54.6f, 64,56, 71.4f,55.2f);
        static readonly Vector2 Start=new Vector2(16.5f,41.5f),Goal=new Vector2(73.4f,54.6f);
        static Vector2[] V(params float[] xy){var a=new Vector2[xy.Length/2];for(int i=0;i<a.Length;i++)a[i]=new Vector2(xy[2*i],xy[2*i+1]);return a;}

        // --- The print -------------------------------------------------------------------------------------------------
        static Texture2D Print()
        {
            CW=Mathf.CeilToInt(MapW*P);CH=Mathf.CeilToInt(MapH*P);C=new Color[CW*CH];
            LandAndSea();
            foreach(var park in Parks)
            {
                var r=Rect.zero;foreach(var e in park){var b=Around(new Vector2(e.x,e.y),Mathf.Max(e.rx,e.ry)+.5f);r=r.width==0?b:Rect.MinMaxRect(Mathf.Min(r.xMin,b.xMin),Mathf.Min(r.yMin,b.yMin),Mathf.Max(r.xMax,b.xMax),Mathf.Max(r.yMax,b.yMax));}
                Fill(r,p=>{float d=1e9f;foreach(var e in park)d=Mathf.Min(d,Ell(p,new Vector2(e.x,e.y),e.rx,e.ry,e.turn));return d;},Park);
            }
            var river=Smooth(River,8);int n=river.Count;
            Fill(Bounds(river,2),p=>{float d=1e9f;for(int i=0;i+1<n;i++)d=Mathf.Min(d,Seg(p,river[i],river[i+1])-Mathf.Lerp(.55f,1.1f,(float)i/n));return d;},Water);
            foreach(var (path,half) in Roads){var r=Smooth(path,6);Fill(Bounds(r,1),p=>Lines(p,r)-half-(half>.4f?.2f:.14f),Casing);}
            foreach(var (path,half) in Roads){var r=Smooth(path,6);Fill(Bounds(r,1),p=>Lines(p,r)-half,half>.4f?Road:Street);}
            Dashed(Smooth(Trip,8),1.1f,.8f,.22f,Route);
            Fill(Around(Start,1.2f),p=>Mathf.Abs(Circle(p,Start,.7f))-.22f,Route);
            Pin(Goal);
            Finish();
            return Bake();
        }

        // Sea and land from one signed distance to the coast (negative on land): a pale shore band fading to deeper
        // water offshore, a thin sandy edge, flat land.
        static void LandAndSea()
        {
            var land=Smooth(Coast,6);land.Add(new Vector2(MapW+6,-6));land.Add(new Vector2(MapW+6,MapH+6));land.Add(new Vector2(-6,MapH+6));
            for(int y=0;y<CH;y++)for(int x=0;x<CW;x++)
            {
                var p=new Vector2((x+.5f)/P,(y+.5f)/P);float d=Poly(p,land);
                var sea=Color.Lerp(Water,WaterDeep,Mathf.SmoothStep(0,1,(d-1)/10));
                sea=Color.Lerp(sea,Shore,Mathf.Clamp01(.5f-(d-.9f)*P));
                var ground=Color.Lerp(Sand,Land,Mathf.Clamp01(.5f-(d+.5f)*P));
                C[y*CW+x]=Color.Lerp(sea,ground,Mathf.Clamp01(.5f-d*P));
            }
        }
        static void Pin(Vector2 tip)
        {
            var c=tip+new Vector2(0,3.1f);var tri=new[]{tip,c+new Vector2(-1.55f,-1),c+new Vector2(1.55f,-1)};
            Fill(Around(tip,1.5f),p=>Ell(p,tip,1.05f,.3f),Shadow,.15f);
            Fill(Around(c,4),p=>Mathf.Min(Circle(p,c,1.9f),Poly(p,tri)),Route);
            Fill(Around(c,1.2f),p=>Circle(p,c,.7f),Paper);
        }
        // Soft creases at the folds, a faint paper grain, and a plain paper margin.
        static void Finish()
        {
            float W=MapW;var folds=new[]{Fold(1),Fold(2),Fold(3)};const float M=1.3f;
            for(int y=0;y<CH;y++)for(int x=0;x<CW;x++)
            {
                float s=(x+.5f)/P,t=(y+.5f)/P;int i=y*CW+x;
                float edge=Mathf.Min(Mathf.Min(s,W-s),Mathf.Min(t,MapH-t));var c=Color.Lerp(C[i],Paper,Mathf.Clamp01(.5f-(edge-M)*P));
                float crease=0;foreach(var f in folds)crease=Mathf.Max(crease,Mathf.Exp(-Mathf.Abs(s-f)/.45f));
                c*=(.985f+.025f*(Fbm(s/W,t/MapH,16,12,3,11)-.5f))*(1-.06f*crease);c.a=1;C[i]=c;
            }
        }

        // Resample the canvas into the texture's front (each panel to its u range), keeping the original's back and edges.
        static Texture2D Bake()
        {
            var orig=new Texture2D(2,2);orig.LoadImage(File.ReadAllBytes(OriginalPath));
            var px=orig.width==Size&&orig.height==Size?orig.GetPixels():new Color[Size*Size];UnityEngine.Object.DestroyImmediate(orig);
            int y0=Mathf.RoundToInt(FrontV*Size);
            for(int y=y0;y<Size;y++)for(int x=0;x<Size;x++)
            {
                float ua=(float)x/Size,ub=(float)(x+1)/Size;int k=0;while(k<3&&ua>=PanelU[k+1]-1e-6f)k++;
                float s0=Fold(k)+(ua-PanelU[k])/(PanelU[k+1]-PanelU[k])*PanelMm[k],s1=Fold(k)+(ub-PanelU[k])/(PanelU[k+1]-PanelU[k])*PanelMm[k];
                float t0=((float)y/Size-FrontV)/(1-FrontV)*MapH,t1=((float)(y+1)/Size-FrontV)/(1-FrontV)*MapH;
                var sum=Color.clear;for(int j=0;j<3;j++)for(int i=0;i<3;i++)sum+=Sample(Mathf.Lerp(s0,s1,(i+.5f)/3),Mathf.Lerp(t0,t1,(j+.5f)/3));
                var c=sum/9;c.a=1;px[y*Size+x]=c;
            }
            var tex=new Texture2D(Size,Size,TextureFormat.RGB24,false);tex.SetPixels(px);
            return SaveTexture(PrintPath,tex,false,TextureWrapMode.Clamp);
        }
        static Color Sample(float s,float t)
        {
            float x=Mathf.Clamp(s*P-.5f,0,CW-1.001f),y=Mathf.Clamp(t*P-.5f,0,CH-1.001f);int x0=(int)x,y0=(int)y;float fx=x-x0,fy=y-y0;
            int i=y0*CW+x0;return Color.Lerp(Color.Lerp(C[i],C[i+1],fx),Color.Lerp(C[i+CW],C[i+CW+1],fx),fy);
        }

        // --- Canvas: shapes as signed distances in mm, painted over their bounds with a pixel of anti-aliasing ---------
        static int CW,CH;static Color[] C;
        static void Fill(Rect r,Func<Vector2,float> sd,Color col,float alpha=1)
        {
            int x0=Mathf.Max(0,Mathf.FloorToInt(r.xMin*P)-2),x1=Mathf.Min(CW,Mathf.CeilToInt(r.xMax*P)+2);
            int y0=Mathf.Max(0,Mathf.FloorToInt(r.yMin*P)-2),y1=Mathf.Min(CH,Mathf.CeilToInt(r.yMax*P)+2);
            for(int y=y0;y<y1;y++)for(int x=x0;x<x1;x++)
            {
                float a=Mathf.Clamp01(.5f-sd(new Vector2((x+.5f)/P,(y+.5f)/P))*P)*alpha;
                if(a>0){int i=y*CW+x;C[i]=Color.Lerp(C[i],col,a);}
            }
        }
        static Rect Around(Vector2 c,float rad)=>new Rect(c.x-rad,c.y-rad,2*rad,2*rad);
        static Rect Bounds(IList<Vector2> pts,float m=0)
        {
            float x0=1e9f,y0=1e9f,x1=-1e9f,y1=-1e9f;foreach(var p in pts){x0=Mathf.Min(x0,p.x);y0=Mathf.Min(y0,p.y);x1=Mathf.Max(x1,p.x);y1=Mathf.Max(y1,p.y);}
            return new Rect(x0-m,y0-m,x1-x0+2*m,y1-y0+2*m);
        }
        static Vector2 Rot(Vector2 v,float deg){float c=Mathf.Cos(deg*Mathf.Deg2Rad),s=Mathf.Sin(deg*Mathf.Deg2Rad);return new Vector2(c*v.x-s*v.y,s*v.x+c*v.y);}
        static float Circle(Vector2 p,Vector2 c,float r)=>Vector2.Distance(p,c)-r;
        static float Seg(Vector2 p,Vector2 a,Vector2 b){var ab=b-a;float t=Mathf.Clamp01(Vector2.Dot(p-a,ab)/Mathf.Max(ab.sqrMagnitude,1e-9f));return Vector2.Distance(p,a+ab*t);}
        static float Lines(Vector2 p,IList<Vector2> pts){float d=1e9f;for(int i=0;i+1<pts.Count;i++)d=Mathf.Min(d,Seg(p,pts[i],pts[i+1]));return d;}
        static float Ell(Vector2 p,Vector2 c,float rx,float ry,float turn=0){var q=Rot(p-c,-turn);return (new Vector2(q.x/rx,q.y/ry).magnitude-1)*Mathf.Min(rx,ry);}
        // Signed distance to a closed polygon (negative inside).
        static float Poly(Vector2 p,IList<Vector2> v)
        {
            int n=v.Count;float d=(p-v[0]).sqrMagnitude,s=1;
            for(int i=0,j=n-1;i<n;j=i,i++)
            {
                var e=v[j]-v[i];var w=p-v[i];var b=w-e*Mathf.Clamp01(Vector2.Dot(w,e)/e.sqrMagnitude);d=Mathf.Min(d,b.sqrMagnitude);
                bool c1=p.y>=v[i].y,c2=p.y<v[j].y,c3=e.x*w.y>e.y*w.x;if(c1&&c2&&c3||!c1&&!c2&&!c3)s=-s;
            }
            return s*Mathf.Sqrt(d);
        }
        static List<Vector2> Smooth(IList<Vector2> pts,int steps)
        {
            var list=new List<Vector2>();int n=pts.Count;Vector2 At(int i)=>pts[Mathf.Clamp(i,0,n-1)];
            for(int i=0;i+1<n;i++)for(int k=0;k<steps;k++)
            {
                float t=(float)k/steps;Vector2 p0=At(i-1),p1=At(i),p2=At(i+1),p3=At(i+2);
                list.Add(.5f*(2*p1+(p2-p0)*t+(2*p0-5*p1+4*p2-p3)*t*t+(3*p1-p0-3*p2+p3)*t*t*t));
            }
            list.Add(pts[n-1]);return list;
        }
        // Round-capped dashes along a path.
        static void Dashed(List<Vector2> path,float dash,float gap,float half,Color col)
        {
            var dashes=new List<List<Vector2>>();bool on=true;float left=dash;var cur=new List<Vector2>{path[0]};
            for(int i=0;i+1<path.Count;i++)
            {
                var a=path[i];var b=path[i+1];float len=Vector2.Distance(a,b),t=0;
                while(len-t>left)
                {
                    t+=left;var q=Vector2.Lerp(a,b,t/len);
                    if(on){cur.Add(q);dashes.Add(cur);}else cur=new List<Vector2>{q};
                    on=!on;left=on?dash:gap;
                }
                left-=len-t;if(on)cur.Add(b);
            }
            if(on&&cur.Count>1)dashes.Add(cur);
            foreach(var d in dashes)Fill(Bounds(d,half+.4f),p=>Lines(p,d)-half,col);
        }
    }
}
