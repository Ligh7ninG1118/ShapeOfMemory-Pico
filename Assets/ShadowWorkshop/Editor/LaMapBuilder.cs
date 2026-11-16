using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using static ShadowWorkshopIntegration.Editor.PropMesh;

namespace ShadowWorkshopIntegration.Editor
{
    // A cartoon Los Angeles for the folded travel map (Art/TravelMap, BonVoyage_NEW): the Pacific on the left with the
    // title, a whale and a compass; Malibu's and Hollywood's green hills along the top with the HOLLYWOOD sign; the pier and
    // its Ferris wheel; a plane leaving LAX on a dashed red route up the coast, over the hills to a pin on downtown's towers;
    // yellow freeways (10, 405, 101, 110, 5) with their shields; the San Gabriels and a sun in sunglasses; a beach, a ship
    // and a sailboat; an inset of California with LA starred. Painted into Folded LA Map.png, which keeps the original's
    // layout (the front's four panels in the top 5/8 of a 1024 square; the plain back and the edge strip copied from
    // Folded Travel Map.png), and the map's material pointed at it, so the gym and every level using the map show it. The
    // mesh and its shadow are unchanged. Laid out in mm on the sheet as placed (the gym's and BonVoyage_NEW's 1.2415 x
    // scale), each fold panel's width measured there, so nothing is stretched across the folds.
    public static class LaMapBuilder
    {
        const string Folder="Assets/ShadowWorkshop/Art/TravelMap/";
        const string OriginalPath=Folder+"Folded Travel Map.png",PrintPath=Folder+"Folded LA Map.png",MaterialPath=Folder+"Folded Travel Map.mat";
        const string FontPath="Assets/TextMesh Pro/Fonts/LiberationSans.ttf";
        // The front's panels: their u ranges in the texture (v from FrontV up) and widths on the sheet (mm).
        static readonly float[] PanelU={0,.3125f,.5371094f,.7558594f,1};
        static readonly float[] PanelMm={34.258f,21.767f,17.890f,21.397f};
        const float FrontV=.375f,MapH=72.951f,P=16;   // sheet height (mm); canvas pixels per mm
        const int Size=1024;
        static float MapW=>PanelMm[0]+PanelMm[1]+PanelMm[2]+PanelMm[3];
        static float Fold(int k){float s=0;for(int i=0;i<k;i++)s+=PanelMm[i];return s;}

        [MenuItem("MetaHackthone/Shadow Workshop/Build LA travel map")]
        public static void Build()
        {
            var tex=Print();var mat=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            mat.SetTexture("_BaseMap",tex);if(mat.HasProperty("_MainTex"))mat.SetTexture("_MainTex",tex);
            EditorUtility.SetDirty(mat);AssetDatabase.SaveAssetIfDirty(mat);
            Debug.Log($"LA_MAP_OK {MapW:F1} x {MapH:F1} mm, {mat.name} -> {tex.name}");
        }

        // --- Palette -------------------------------------------------------------------------------------------------
        static readonly Color Ink=new Color(.17f,.19f,.26f),Paper=new Color(.97f,.94f,.85f),LandCream=new Color(.97f,.91f,.76f),
            Sand=new Color(.97f,.82f,.55f),Grid=new Color(.88f,.79f,.61f),Shallow=new Color(.55f,.87f,.93f),Deep=new Color(.33f,.69f,.86f),
            OceanText=new Color(.15f,.44f,.62f),RoadYellow=new Color(1f,.82f,.33f),RoadLine=new Color(.96f,.60f,.22f),
            Hill=new Color(.53f,.79f,.40f),HillDark=new Color(.42f,.70f,.34f),HillLight=new Color(.70f,.88f,.52f),Tree=new Color(.27f,.58f,.30f),
            ParkGreen=new Color(.62f,.84f,.45f),Rock=new Color(.80f,.60f,.44f),RockDark=new Color(.65f,.46f,.34f),Snow=new Color(.98f,.98f,1f),
            Foothill=new Color(.93f,.87f,.66f),Trunk=new Color(.62f,.42f,.26f),TrunkDark=new Color(.45f,.29f,.17f),Frond=new Color(.30f,.70f,.36f),
            FrondDark=new Color(.20f,.52f,.28f),Coconut=new Color(.45f,.30f,.18f),Red=new Color(.93f,.31f,.24f),Yellow=new Color(1f,.80f,.24f),
            Teal=new Color(.20f,.70f,.70f),Pink=new Color(.98f,.55f,.66f),Steel=new Color(.92f,.92f,.95f),Plank=new Color(.70f,.50f,.32f),
            SunYellow=new Color(1f,.85f,.25f),SunRay=new Color(1f,.64f,.20f),Cheek=new Color(1f,.50f,.42f),Card=new Color(1f,.98f,.93f),
            StateGold=new Color(.99f,.76f,.35f),ShieldBlue=new Color(.15f,.35f,.70f),Whale=new Color(.38f,.52f,.70f),
            TitleTop=new Color(.99f,.48f,.36f),TitleBottom=new Color(1f,.73f,.30f),Window=new Color(1f,.97f,.80f),
            Board=new Color(.30f,.72f,.86f),Hull=new Color(.80f,.24f,.22f);
        static readonly Color[] Towers={new Color(.93f,.78f,.55f),new Color(.55f,.73f,.90f),new Color(.36f,.67f,.78f),new Color(.74f,.70f,.86f),new Color(.95f,.86f,.64f)};
        const float Line=.22f;

        // --- Geography (mm on the sheet, x east from the left edge, y north from the bottom) -----------------------------
        static readonly Vector2[] Coast=V(-6,59.8f, 2,59.4f, 8,58.6f, 13,57.4f, 18,55.6f, 22,53.6f, 24.6f,51.4f, 26,49, 27,45.5f, 27.8f,41.5f,
            28.6f,37.5f, 29.4f,34, 29.4f,30.8f, 28.4f,27.8f, 26.4f,26, 24.6f,24.6f, 23.8f,22.6f, 24.6f,20.8f, 26.8f,19.8f, 29.6f,19.2f, 31.8f,18.4f,
            33.4f,16.8f, 35.6f,15.8f, 38.6f,15.5f, 42,15.3f, 45.5f,14.8f, 50,13.9f, 55,12.8f, 60,11.6f, 66,10.4f, 72,9.2f, 78,7.9f, 84,6.6f, 90,5.3f, 101,3.4f);
        static readonly Vector2[][] Freeways={
            V(25.8f,48.8f, 31,48.2f, 38,47.2f, 46,46, 54,44.8f, 60,44.2f, 66,44.5f, 74,45.5f, 84,46.2f, 98,46.8f),            // 10
            V(37.6f,75, 37.4f,64, 36.6f,56, 35.8f,48, 35.6f,40, 36.4f,32, 38.4f,25, 41.5f,19, 44.5f,15.6f),                   // 405
            V(58,46.6f, 54.2f,51.6f, 50.6f,56, 47.4f,59.6f, 42,64.5f, 38.6f,69, 36,75),                                       // 101
            V(59.4f,44.2f, 58.4f,36, 57.6f,26, 56.3f,18, 55.6f,13.4f),                                                         // 110
            V(68,44.6f, 74,50, 80,55, 86,59.5f, 98,64)};                                                                       // 5
        static readonly Vector2[] Route=V(31,34.4f, 30.2f,38.6f, 28.9f,44.2f, 28.2f,48.9f, 30.6f,53.4f, 35.6f,57.6f, 40.6f,61, 45.4f,62.4f,
            50.4f,60.4f, 55.2f,57.6f, 60.2f,57.2f, 65,59);
        // California's outline (longitude, latitude), for the inset.
        static readonly Vector2[] California=V(-124.21f,42, -120,42, -120,39, -114.63f,35, -114.13f,34.27f, -114.43f,34.08f, -114.52f,33.03f,
            -114.72f,32.72f, -117.12f,32.53f, -117.28f,33.05f, -117.6f,33.39f, -118.08f,33.73f, -118.41f,33.8f, -118.52f,34.03f, -118.8f,34.02f,
            -119.22f,34.15f, -119.6f,34.4f, -120.47f,34.45f, -120.64f,34.58f, -120.63f,35, -120.86f,35.36f, -121.29f,35.66f, -121.89f,36.3f,
            -121.93f,36.62f, -121.8f,36.8f, -122.02f,36.96f, -122.4f,37.2f, -122.51f,37.78f, -122.97f,38, -123.03f,38.3f, -123.72f,38.92f,
            -123.83f,39.8f, -124.36f,40.44f, -124.1f,40.85f, -124.16f,41.45f);
        static Vector2[] V(params float[] xy){var a=new Vector2[xy.Length/2];for(int i=0;i<a.Length;i++)a[i]=new Vector2(xy[2*i],xy[2*i+1]);return a;}

        // --- The print -------------------------------------------------------------------------------------------------
        static Texture2D Print()
        {
            CW=Mathf.CeilToInt(MapW*P);CH=Mathf.CeilToInt(MapH*P);C=new Color[CW*CH];
            LoadFont("LOSANGELESPACIFICOCEANLAXDOWNTOWNHOLLYWOODCALIFORNIAN0123456789");
            LandAndSea();
            Park(new Vector2(52.6f,30),2.6f,1.8f);Park(new Vector2(45.6f,39.5f),2.2f,1.6f);Park(new Vector2(69.5f,31),2.5f,1.7f);
            Roads();Mountains();Hills();
            Stamp(Text("HOLLYWOOD",new Vector2(45.6f,64.9f),2.3f,.5f,.78f),Color.white,.05f,Ink,.1f,.15f);
            Skyline();Pier();
            Palm(new Vector2(24.9f,54.2f),5.2f,-.8f);Palm(new Vector2(28.9f,41),5.6f,.9f);Palm(new Vector2(27.8f,23.6f),4.4f,.6f);
            Palm(new Vector2(42.6f,50.5f),5.8f,-.6f);Palm(new Vector2(46.2f,51.6f),5,.7f);Palm(new Vector2(43.5f,26),5.2f,.5f);
            Palm(new Vector2(52.8f,36.2f),5,-.5f);Palm(new Vector2(63.8f,24),4.8f,.6f);Palm(new Vector2(91.5f,11.6f),5.4f,.7f);
            foreach(var w in V(4,41.5f, 20.6f,44.2f, 21.6f,33.6f, 4.5f,25.5f, 17,22.6f, 14.4f,53, 39.2f,11.2f, 54.6f,7.6f, 58.8f,7.6f, 72.4f,4.6f, 81.6f,3.8f))Wave(w);
            WhaleTail(new Vector2(11.5f,30));Compass(new Vector2(6.6f,50.5f),3.3f);Ship();Sailboat();Umbrella(new Vector2(78.5f,9.8f));Surfboard(new Vector2(86.6f,11.4f));
            Sun(new Vector2(86.6f,64.6f),3.8f);Inset();
            Interstate(new Vector2(49,45.6f),"10");Interstate(new Vector2(37.6f,28),"405");Interstate(new Vector2(58.1f,33),"110");
            Interstate(new Vector2(77.2f,52.7f),"5");UsRoute(new Vector2(51.6f,54.2f),"101");
            Dashed(Smooth(Route,8),1.25f,.85f,.26f,Red,Color.white);
            Plane(new Vector2(31,34.3f),118,1);Stamp(Text("LAX",new Vector2(31.4f,30.4f),1.5f),Ink,.05f,Color.white,.22f);
            Pin(new Vector2(65,59));Title();
            Stamp(Text("PACIFIC",new Vector2(14.4f,41),1.9f,.5f,1,.2f,.25f),OceanText,.04f,Color.clear,0);
            Stamp(Text("OCEAN",new Vector2(14.4f,38.1f),1.9f,.5f,1,.2f,.25f),OceanText,.04f,Color.clear,0);
            Stamp(Text("DOWNTOWN",new Vector2(66.4f,41),1.35f),Ink,.05f,Color.white,.22f);
            Finish();
            return Bake();
        }

        // Sea and land from one signed distance to the coast (negative on land): deeper blue offshore with two foam lines,
        // a beach, then cream land with a faint street grid; the coast inked.
        static void LandAndSea()
        {
            var land=Smooth(Coast,5);land.Add(new Vector2(MapW+6,MapH+6));land.Add(new Vector2(-6,MapH+6));
            for(int y=0;y<CH;y++)for(int x=0;x<CW;x++)
            {
                var p=new Vector2((x+.5f)/P,(y+.5f)/P);float d=Poly(p,land);
                var sea=Color.Lerp(Shallow,Deep,Mathf.SmoothStep(0,1,d/10));
                sea=Color.Lerp(sea,Color.white,Mathf.Clamp01(.5f-(Mathf.Abs(d-.75f)-.1f)*P)*.55f);
                sea=Color.Lerp(sea,Color.white,Mathf.Clamp01(.5f-(Mathf.Abs(d-1.55f)-.08f)*P)*.3f);
                var q=Rot(p,-12);float street=Mathf.Min(Mathf.Abs(Mathf.Repeat(q.x,2.2f)-1.1f),Mathf.Abs(Mathf.Repeat(q.y,2.2f)-1.1f));
                var ground=Color.Lerp(LandCream,Grid,Mathf.Clamp01(.5f-(street-.06f)*P)*.6f*Mathf.Clamp01(-(d+1.6f)/1.5f));
                ground=Color.Lerp(Sand,ground,Mathf.Clamp01(.5f-(d+1.1f)*P));
                var c=Color.Lerp(sea,ground,Mathf.Clamp01(.5f-d*P));
                C[y*CW+x]=Color.Lerp(c,Ink,Mathf.Clamp01(.5f-(Mathf.Abs(d)-.15f)*P));
            }
        }
        static void Park(Vector2 c,float rx,float ry)
        {
            Inked(Around(c,Mathf.Max(rx,ry)+.6f),p=>Ell(p,c,rx,ry),ParkGreen,.18f);
            foreach(var o in V(-.35f,.25f, .3f,-.1f, .1f,.45f)){var t=c+new Vector2(o.x*rx,o.y*ry);Inked(Around(t,.8f),p=>Circle(p,t,.42f),Tree,.13f);}
        }
        static void Roads()
        {
            var roads=new List<List<Vector2>>();foreach(var r in Freeways)roads.Add(Smooth(r,6));
            foreach(var r in roads)Fill(Bounds(r,1),p=>Lines(p,r)-.72f,Ink);
            foreach(var r in roads)Fill(Bounds(r,1),p=>Lines(p,r)-.52f,RoadYellow);
            foreach(var r in roads)Fill(Bounds(r,1),p=>Lines(p,r)-.06f,RoadLine,.6f);
        }
        // The San Gabriels: tall peaks behind, lower ones in front, shaded and snow-capped, behind a band of foothills.
        static void Mountains()
        {
            const float Base=55;
            foreach(var (x,top,hw) in new[]{(66f,71.2f,6.8f),(80.4f,70.8f,7f),(95.2f,71.4f,6.2f),(59.4f,68.4f,6f),(73.2f,68.2f,6.4f),(87.8f,69f,6.6f)})
            {
                float h=top-Base;var apex=new Vector2(x,top);var tri=new[]{apex,new Vector2(x+hw,Base),new Vector2(x-hw,Base)};
                Inked(Bounds(tri,.5f),p=>Poly(p,tri),Rock);
                var shade=new[]{apex,new Vector2(x+hw,Base),new Vector2(x+hw*.12f,Base)};Fill(Bounds(shade),p=>Poly(p,shade),RockDark);
                var cap=new[]{apex,new Vector2(x+hw*.3f,top-h*.3f),new Vector2(x+hw*.1f,top-h*.21f),new Vector2(x,top-h*.31f),new Vector2(x-hw*.12f,top-h*.2f),new Vector2(x-hw*.3f,top-h*.3f)};
                Fill(Bounds(cap),p=>Poly(p,cap),Snow);
            }
            var ridge=Smooth(V(52,57.4f, 56,58.2f, 60,57.4f, 64,58.4f, 68,57.6f, 72,58.6f, 76,57.8f, 80,58.8f, 84,57.9f, 88,58.7f, 92,57.8f, 99,58.4f),5);
            var band=new List<Vector2>(ridge){new Vector2(99,51),new Vector2(52,51)};
            FillBy(Bounds(band),p=>Poly(p,band),p=>Foothill,p=>Mathf.Clamp01((p.y-51.5f)/3));
            Fill(Bounds(ridge,.5f),p=>Lines(p,ridge)-.11f,Ink);
        }
        // Malibu's and Hollywood's hills: a green band of rounded, inked mounds with trees; the HOLLYWOOD sign's on the big one.
        static void Hills()
        {
            var region=Smooth(V(-6,61.6f, 3,61.2f, 9,60.6f, 15,59.8f, 20,58.9f, 25,58, 29.5f,57.4f, 33.5f,58, 37.5f,59.6f, 42,60.4f, 47,60.6f,
                51.5f,59.9f, 55,58.6f, 58.2f,59.2f, 60.6f,61, 61.4f,64, 61.4f,75),5);
            region.Add(new Vector2(-6,75));Inked(Bounds(region,.5f),p=>Poly(p,region),Hill);
            var mounds=new[]{(2.5f,68.8f,6.8f,5.2f),(14f,68f,6.8f,5f),(25.5f,67.2f,6.5f,4.8f),(35.5f,67.8f,5.5f,4.4f),(55.5f,67.6f,5.6f,4.6f),
                (6.5f,64.4f,6f,3.5f),(18f,62.8f,5.8f,3.4f),(28.5f,61.2f,5.2f,3.1f),(45.6f,64f,9.6f,3.8f),(56.4f,62.6f,4.6f,3f)};
            for(int i=0;i<mounds.Length;i++)
            {
                var (x,y,rx,ry)=mounds[i];var c=new Vector2(x,y);
                Inked(Around(c,Mathf.Max(rx,ry)+.5f),p=>Ell(p,c,rx,ry),i%2==0?Hill:HillDark,.2f);
                var hc=c+new Vector2(-rx*.28f,ry*.3f);Fill(Around(hc,rx),p=>Mathf.Max(Ell(p,hc,rx*.42f,ry*.32f),Ell(p,c,rx,ry)),HillLight,.55f);
            }
            foreach(var t in V(4.5f,66.3f, 10.8f,65.2f, 15.8f,66.6f, 21.2f,64, 24,66.6f, 31,63.2f, 33.5f,65.6f, 53.6f,63.4f, 57.6f,64.8f, 9.6f,70.2f, 19.8f,70, 29.8f,69.8f, 40.6f,70.6f, 58.6f,69.4f))
            {
                var c=t;Inked(Around(c,1),p=>Circle(p,c,.5f),Tree,.15f);Fill(Around(c,1),p=>Circle(p,c+new Vector2(-.15f,.17f),.2f),HillLight,.6f);
            }
        }
        // Downtown: five towers on the 10, windows lit, roofs flat, slanted, spired and domed.
        static void Skyline()
        {
            const float Base=46.4f;
            foreach(var (x,w,h,col,roof) in new[]{(59.3f,2.5f,6.2f,0,0),(70.3f,2.4f,5.4f,4,0),(61.9f,2.5f,8.8f,1,1),(67.6f,2.6f,8.2f,3,2),(64.7f,3.1f,10.6f,2,3)})
            {
                var c=new Vector2(x,Base+h/2);float x0=x-w/2,y1=Base+h;
                Func<Vector2,float> body=p=>RBox(p,c,new Vector2(w/2,h/2),.12f),sd=body;
                if(roof==0)sd=p=>Mathf.Min(body(p),RBox(p,new Vector2(x-w*.15f,y1+.35f),new Vector2(w*.22f,.35f),.05f));
                if(roof==1)sd=p=>Mathf.Max(body(p),p.y-(y1-1.4f+1.4f*(p.x-x0)/w));
                if(roof==2)sd=p=>Mathf.Min(Mathf.Min(body(p),Poly(p,new[]{new Vector2(x-.75f,y1-.1f),new Vector2(x+.75f,y1-.1f),new Vector2(x,y1+2)})),Seg(p,new Vector2(x,y1+1.5f),new Vector2(x,y1+3))-.07f);
                if(roof==3)sd=p=>Mathf.Min(body(p),Circle(p,new Vector2(x,y1),w/2));
                Inked(new Rect(x0-.5f,Base-.5f,w+1,h+4),sd,Towers[col],.2f);
                for(float wy=Base+.8f;wy<y1-1.3f;wy+=.95f)for(float wx=x0+.55f;wx<x0+w-.3f;wx+=.7f)
                {
                    var wc=new Vector2(wx,wy);Fill(Around(wc,.5f),p=>RBox(p,wc,new Vector2(.17f,.25f),.04f),Window,.85f);
                }
            }
        }
        // The pier on its posts and the Ferris wheel at its end.
        static void Pier()
        {
            for(float x=19.9f;x<26.6f;x+=1.3f){var a=new Vector2(x,47.7f);Fill(Around(a,1),p=>Seg(p,a,a+new Vector2(0,-.75f))-.1f,Ink);}
            var deck=new Vector2(22.9f,48.05f);Inked(Around(deck,4.2f),p=>RBox(p,deck,new Vector2(3.5f,.42f),.1f),Plank,.18f);
            var c=new Vector2(21.4f,51.3f);const float R=2.45f;var l=new Vector2(20.3f,48.4f);var r=new Vector2(22.5f,48.4f);
            Fill(Around(c,3.6f),p=>Mathf.Min(Seg(p,c,l),Seg(p,c,r))-.17f,Ink);Fill(Around(c,3.6f),p=>Mathf.Min(Seg(p,c,l),Seg(p,c,r))-.08f,Steel);
            for(int k=0;k<8;k++){var e=c+Dir(k*45+10)*R;Fill(Around(c,R+.5f),p=>Seg(p,c,e)-.06f,Ink);}
            Fill(Around(c,R+.5f),p=>Mathf.Abs(Circle(p,c,R))-.21f,Ink);Fill(Around(c,R+.5f),p=>Mathf.Abs(Circle(p,c,R))-.11f,Red);
            var cabins=new[]{Red,Yellow,Teal,Pink};
            for(int k=0;k<8;k++){var q=c+Dir(k*45+10)*R;Inked(Around(q,.8f),p=>Circle(p,q,.42f),cabins[k%4],.13f);}
            Inked(Around(c,.6f),p=>Circle(p,c,.3f),Yellow,.12f);
        }
        // A palm: a curved ringed trunk, seven drooping fronds with dark midribs, coconuts.
        static void Palm(Vector2 b,float h,float lean)
        {
            var top=b+new Vector2(lean,h);var mid=b+new Vector2(lean*.25f+.35f*Mathf.Sign(lean),h*.5f);var trunk=new List<Vector2>();
            for(int i=0;i<=8;i++){float t=i/8f;trunk.Add((1-t)*(1-t)*b+2*(1-t)*t*mid+t*t*top);}
            Func<Vector2,float> tr=p=>{float d=1e9f;for(int i=0;i+1<trunk.Count;i++)d=Mathf.Min(d,Seg(p,trunk[i],trunk[i+1])-Mathf.Lerp(.36f,.24f,(i+.5f)/8));return d;};
            Inked(Bounds(trunk,1),tr,Trunk,.18f);
            for(int i=1;i<8;i++)
            {
                var a=trunk[i];var dir=(trunk[i+1]-trunk[i-1]).normalized;var n=new Vector2(-dir.y,dir.x)*Mathf.Lerp(.36f,.24f,i/8f)*.85f;
                Fill(Around(a,.6f),p=>Seg(p,a-n,a+n)-.05f,TrunkDark);
            }
            float L=h*.42f;var angles=new[]{160f,125f,85f,50f,15f,-25f,200f};var leaf=new Vector2[angles.Length];
            for(int k=0;k<angles.Length;k++){var dir=Dir(angles[k]);leaf[k]=top+dir*L*.5f+new Vector2(0,-L*.12f*Mathf.Abs(dir.x));}
            Func<Vector2,float> fronds=p=>{float d=1e9f;for(int k=0;k<angles.Length;k++)d=Mathf.Min(d,Ell(p,leaf[k],L*.55f,L*.16f,angles[k]));return d;};
            Inked(Around(top,L*1.3f),fronds,Frond,.18f);
            for(int k=0;k<angles.Length;k++){var end=leaf[k]+Dir(angles[k])*L*.32f;Fill(Around(top,L*1.3f),p=>Seg(p,top,end)-.06f,FrondDark,.8f);}
            Inked(Around(top,1),p=>Mathf.Min(Circle(p,top+new Vector2(-.3f,-.25f),.28f),Circle(p,top+new Vector2(.28f,-.3f),.28f)),Coconut,.12f);
        }
        static void Wave(Vector2 c)=>Fill(Around(c,1.4f),p=>Mathf.Min(Arc(p,c+new Vector2(-.5f,0),.5f,0,180),Arc(p,c+new Vector2(.5f,0),.5f,180,360))-.11f,Color.white,.8f);
        static void WhaleTail(Vector2 c)
        {
            Fill(Around(c,3.6f),p=>Mathf.Abs(Ell(p,c,2.7f,.62f))-.12f,Color.white,.85f);
            Func<Vector2,float> tail=p=>Mathf.Min(Mathf.Min(Ell(p,c+new Vector2(-1.45f,2.75f),1.75f,.68f,-22),Ell(p,c+new Vector2(1.45f,2.75f),1.75f,.68f,22)),
                RBox(p,c+new Vector2(0,1.05f),new Vector2(.55f,1.25f),.45f));
            Inked(Around(c,4.5f),tail,Whale,.2f);
            foreach(var o in V(-3.3f,3.7f, 3.4f,4, -3.9f,2.5f, 3.9f,2.8f)){var q=c+o;Inked(Around(q,.6f),p=>Circle(p,q,.24f),Color.white,.1f);}
        }
        static void Compass(Vector2 c,float r)
        {
            Fill(Around(c,r),p=>Mathf.Abs(Circle(p,c,r*.62f))-.07f,Ink,.8f);
            var star=new Vector2[8];for(int k=0;k<8;k++)star[k]=c+Dir(k*45)*(k%2==0?r:r*.26f);
            Inked(Around(c,r+.5f),p=>Poly(p,star),Paper,.18f);
            for(int k=0;k<4;k++)
            {
                var tip=star[2*k];var flank=star[(2*k+7)%8];var half=new[]{c,tip,flank};
                Fill(Around(c,r+.2f),p=>Poly(p,half),k==1?Red:new Color(.55f,.6f,.7f));
                if(k==1){var other=new[]{c,tip,star[3]};Fill(Around(c,r+.2f),p=>Poly(p,other),new Color(.98f,.6f,.5f));}
            }
            Stamp(Text("N",c+new Vector2(0,r+.4f),1.4f),Ink,.05f,Color.white,.2f);
        }
        static void Ship()
        {
            var hull=V(42.4f,7.4f, 51.2f,7.4f, 52.4f,9.3f, 41.4f,9.3f);Inked(Bounds(hull,.5f),p=>Poly(p,hull),Hull,.18f);
            Fill(Bounds(hull),p=>Mathf.Max(Poly(p,hull)+.05f,Mathf.Abs(p.y-8.05f)-.12f),Color.white);
            var boxes=new[]{Teal,Yellow,ShieldBlue,new Color(.98f,.55f,.25f),Teal};
            for(int i=0;i<5;i++){var c=new Vector2(43.05f+i*1.3f,9.9f);Inked(Around(c,1.2f),p=>RBox(p,c,new Vector2(.6f,.58f),.05f),boxes[i],.12f);}
            var bridge=new Vector2(50.6f,10.6f);Inked(Around(bridge,2),p=>RBox(p,bridge,new Vector2(.7f,1.3f),.08f),Color.white,.12f);
            var glass=new Vector2(50.6f,11.3f);Fill(Around(glass,1),p=>RBox(p,glass,new Vector2(.5f,.17f),.04f),ShieldBlue);
            var funnel=new Vector2(50.6f,12.3f);Inked(Around(funnel,1),p=>RBox(p,funnel,new Vector2(.3f,.4f),.05f),Red,.12f);
        }
        static void Sailboat()
        {
            var hull=V(62.6f,3, 67.6f,3, 68.6f,4.4f, 61.6f,4.4f);Inked(Bounds(hull,.5f),p=>Poly(p,hull),Plank,.16f);
            Fill(new Rect(64.6f,4,1.2f,6.5f),p=>Seg(p,new Vector2(65.2f,4.4f),new Vector2(65.2f,9.8f))-.07f,Ink);
            var main=V(65.45f,4.8f, 65.45f,9.4f, 68.2f,4.8f);Inked(Bounds(main,.5f),p=>Poly(p,main),Color.white,.14f);
            var jib=V(64.95f,4.8f, 64.95f,8.6f, 62.6f,4.8f);Inked(Bounds(jib,.5f),p=>Poly(p,jib),Red,.14f);
            var flag=V(65.25f,9.85f, 66.3f,9.55f, 65.25f,9.25f);Fill(Bounds(flag,.2f),p=>Poly(p,flag),Yellow);
        }
        static void Umbrella(Vector2 b)
        {
            var top=b+new Vector2(0,3.4f);Fill(new Rect(b.x-.3f,b.y-.3f,.6f,4),p=>Seg(p,b,top)-.09f,Ink);
            Func<Vector2,float> canopy=p=>Mathf.Max(Circle(p,top,2),top.y-p.y);
            Fill(Around(top,2.6f),p=>canopy(p)-.18f,Ink);
            FillBy(Around(top,2.4f),canopy,p=>Mathf.Repeat(Mathf.Atan2(p.y-top.y,p.x-top.x)*Mathf.Rad2Deg,60)<30?Red:Color.white);
        }
        static void Surfboard(Vector2 c)
        {
            Inked(Around(c,2.8f),p=>Ell(p,c,.62f,2.3f,-12),Board,.16f);
            Fill(Around(c,2.6f),p=>Ell(p,c,.12f,2f,-12),Color.white,.9f);
        }
        static void Sun(Vector2 c,float r)
        {
            Func<Vector2,float> rays=p=>{float d=1e9f;for(int k=0;k<12;k++){float a=k*30+15;d=Mathf.Min(d,Seg(p,c+Dir(a)*(r+.5f),c+Dir(a)*(r+1.9f))-.42f);}return d;};
            Inked(Around(c,r+2.8f),rays,SunRay,.2f);Inked(Around(c,r+.5f),p=>Circle(p,c,r),SunYellow,.22f);
            var gl=c+new Vector2(-1.25f,.55f);var gr=c+new Vector2(1.25f,.55f);
            Fill(Around(c,r),p=>Mathf.Min(Mathf.Min(RBox(p,gl,new Vector2(1.05f,.7f),.45f),RBox(p,gr,new Vector2(1.05f,.7f),.45f)),Seg(p,gl,gr)-.14f),Ink);
            Fill(Around(c,r),p=>Mathf.Min(Seg(p,gl+new Vector2(-.55f,.2f),gl+new Vector2(-.2f,.42f)),Seg(p,gr+new Vector2(-.55f,.2f),gr+new Vector2(-.2f,.42f)))-.08f,Color.white,.9f);
            Fill(Around(c,r),p=>Arc(p,c+new Vector2(0,-.2f),1.5f,215,325)-.13f,Ink);
            Fill(Around(c,r),p=>Mathf.Min(Ell(p,c+new Vector2(-2.3f,-.9f),.6f,.4f),Ell(p,c+new Vector2(2.3f,-.9f),.6f,.4f)),Cheek,.6f);
        }
        // An inset card: California in gold with LA starred, its name below.
        static void Inset()
        {
            var c=new Vector2(84.6f,35.5f);var half=new Vector2(8.4f,12.2f);
            Fill(Around(c,15),p=>RBox(p,c+new Vector2(.45f,-.45f),half,1.2f),Ink,.25f);Inked(Around(c,15),p=>RBox(p,c,half,1.2f),Card,.24f);
            Vector2 At(float lon,float lat)=>new Vector2(c.x+(lon+119.27f)*.8f*1.85f,36.9f+(lat-37.27f)*1.85f);
            var state=new Vector2[California.Length];for(int i=0;i<state.Length;i++)state[i]=At(California[i].x,California[i].y);
            Inked(Bounds(state,.5f),p=>Poly(p,state),StateGold,.2f);
            var la=At(-118.24f,34.05f);var star=Star(la,1.15f,.47f);Inked(Around(la,1.6f),p=>Poly(p,star),Red,.14f);
            Stamp(Text("CALIFORNIA",new Vector2(c.x,24.7f),1.55f),Ink,.04f,Color.clear,0);
        }
        static void Interstate(Vector2 c,string number)
        {
            const float s=1.45f;var shape=V(-1,.95f, -.55f,1.05f, 0,.92f, .55f,1.05f, 1,.95f, 1.02f,.15f, .72f,-.62f, 0,-1.08f, -.72f,-.62f, -1.02f,.15f);
            for(int i=0;i<shape.Length;i++)shape[i]=c+shape[i]*s;var r=Around(c,s*1.4f);
            Inked(r,p=>Poly(p,shape),Color.white,.16f);Fill(r,p=>Poly(p,shape)+.16f,ShieldBlue);Fill(r,p=>Mathf.Max(Poly(p,shape)+.16f,c.y+.42f*s-p.y),Red);
            Stamp(Text(number,c+new Vector2(0,-.62f),1,.5f,number.Length>2?.8f:1),Color.white,.03f,Color.clear,0);
        }
        static void UsRoute(Vector2 c,string number)
        {
            var r=Around(c,2);Inked(r,p=>RBox(p,c,new Vector2(1.3f,1.25f),.45f),Color.white,.16f);Fill(r,p=>Mathf.Abs(RBox(p,c,new Vector2(1.3f,1.25f),.45f)+.22f)-.06f,Ink);
            Stamp(Text(number,c+new Vector2(0,-.5f),1,.5f,.8f),Ink,.03f,Color.clear,0);
        }
        static void Plane(Vector2 c,float turn,float s)
        {
            var wing=V(.5f,.3f, -.6f,2.5f, -1.2f,2.5f, -.7f,.3f);var tail=V(-1.6f,.25f, -2.2f,1.25f, -2.6f,1.25f, -2.3f,.25f);
            Vector2[] Mirror(Vector2[] a){var m=new Vector2[a.Length];for(int i=0;i<a.Length;i++)m[i]=new Vector2(a[i].x,-a[i].y);return m;}
            var wing2=Mirror(wing);var tail2=Mirror(tail);
            Func<Vector2,float> body=p=>{var q=Rot(p-c,-turn)/s;return Mathf.Min(Mathf.Min(Seg(q,new Vector2(-2.2f,0),new Vector2(2.1f,0))-.42f,Mathf.Min(Poly(q,wing),Poly(q,wing2))),Mathf.Min(Poly(q,tail),Poly(q,tail2)))*s;};
            Inked(Around(c,3.2f*s),body,Color.white,.2f);
            Fill(Around(c,3.2f*s),p=>{var q=Rot(p-c,-turn)/s;return Mathf.Min(Poly(q,tail),Poly(q,tail2))*s;},Red);
            Fill(Around(c,3.2f*s),p=>{var q=Rot(p-c,-turn)/s;return Mathf.Min(Seg(q,new Vector2(-1.3f,0),new Vector2(1.1f,0))-.09f,Circle(q,new Vector2(1.65f,0),.17f))*s;},ShieldBlue,.85f);
        }
        static void Pin(Vector2 tip)
        {
            var c=tip+new Vector2(0,3.3f);var tri=new[]{tip,c+new Vector2(-1.65f,-1),c+new Vector2(1.65f,-1)};
            Fill(Around(tip,1.4f),p=>Ell(p,tip,1,.3f),Ink,.3f);
            Inked(Around(c,4),p=>Mathf.Min(Circle(p,c,2),Poly(p,tri)),Red,.24f);
            Inked(Around(c,1.2f),p=>Circle(p,c+new Vector2(0,.15f),.75f),Color.white,.14f);
            Fill(Around(c,2.2f),p=>Arc(p,c,1.45f,110,160)-.13f,Color.white,.6f);
        }
        static void Title()
        {
            var c=new Vector2(16.8f,10.6f);var half=new Vector2(14.2f,7.4f);var r=Around(c,15);
            Fill(r,p=>RBox(p,c+new Vector2(.5f,-.5f),half,2),Ink,.3f);Fill(r,p=>RBox(p,c,half,2)-.28f,Ink);
            FillBy(r,p=>RBox(p,c,half,2),p=>Color.Lerp(TitleBottom,TitleTop,(p.y-c.y+half.y)/(2*half.y)));
            Fill(r,p=>Mathf.Abs(RBox(p,c,half,2)+.75f)-.09f,Paper,.9f);
            foreach(var x in new[]{8.6f,25f}){var s=Star(new Vector2(x,13.5f),1.05f,.45f);Inked(Bounds(s,.5f),p=>Poly(p,s),Yellow,.16f);}
            Stamp(Text("LOS",new Vector2(c.x,11.7f),3.8f),Color.white,.16f,Ink,.36f,.35f);
            Stamp(Text("ANGELES",new Vector2(c.x,5.7f),3.8f),Color.white,.16f,Ink,.36f,.35f);
        }
        // Creases at the folds, a paper grain, and a cream margin inside an inked frame.
        static void Finish()
        {
            float W=MapW;var folds=new[]{Fold(1),Fold(2),Fold(3)};const float M=1.35f;
            for(int y=0;y<CH;y++)for(int x=0;x<CW;x++)
            {
                float s=(x+.5f)/P,t=(y+.5f)/P;int i=y*CW+x;
                float edge=Mathf.Min(Mathf.Min(s,W-s),Mathf.Min(t,MapH-t));var c=Color.Lerp(C[i],Paper,Mathf.Clamp01(.5f-(edge-M)*P));
                c=Color.Lerp(c,Ink,Mathf.Clamp01(.5f-(Mathf.Abs(edge-M)-.1f)*P)*.9f);
                float crease=0;foreach(var f in folds)crease=Mathf.Max(crease,Mathf.Exp(-Mathf.Abs(s-f)/.45f));
                float paper=.975f+.04f*(Fbm(s/W,t/MapH,16,12,3,11)-.5f);
                c*=paper*(1-.1f*crease);c.a=1;C[i]=c;
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
        static void Fill(Rect r,Func<Vector2,float> sd,Color col,float alpha=1)=>FillBy(r,sd,p=>col,p=>alpha);
        static void FillBy(Rect r,Func<Vector2,float> sd,Func<Vector2,Color> col,Func<Vector2,float> alpha=null)
        {
            int x0=Mathf.Max(0,Mathf.FloorToInt(r.xMin*P)-2),x1=Mathf.Min(CW,Mathf.CeilToInt(r.xMax*P)+2);
            int y0=Mathf.Max(0,Mathf.FloorToInt(r.yMin*P)-2),y1=Mathf.Min(CH,Mathf.CeilToInt(r.yMax*P)+2);
            for(int y=y0;y<y1;y++)for(int x=x0;x<x1;x++)
            {
                var p=new Vector2((x+.5f)/P,(y+.5f)/P);float a=Mathf.Clamp01(.5f-sd(p)*P);if(a<=0)continue;
                if(alpha!=null)a*=alpha(p);int i=y*CW+x;C[i]=Color.Lerp(C[i],col(p),a);
            }
        }
        // A shape with the cartoon ink line round it.
        static void Inked(Rect r,Func<Vector2,float> sd,Color col,float line=Line){Fill(Grow(r,line),p=>sd(p)-line,Ink);Fill(r,sd,col);}
        static Rect Grow(Rect r,float m)=>new Rect(r.x-m,r.y-m,r.width+2*m,r.height+2*m);
        static Rect Around(Vector2 c,float rad)=>new Rect(c.x-rad,c.y-rad,2*rad,2*rad);
        static Rect Bounds(IList<Vector2> pts,float m=0)
        {
            float x0=1e9f,y0=1e9f,x1=-1e9f,y1=-1e9f;foreach(var p in pts){x0=Mathf.Min(x0,p.x);y0=Mathf.Min(y0,p.y);x1=Mathf.Max(x1,p.x);y1=Mathf.Max(y1,p.y);}
            return new Rect(x0-m,y0-m,x1-x0+2*m,y1-y0+2*m);
        }
        static Vector2 Dir(float deg)=>new Vector2(Mathf.Cos(deg*Mathf.Deg2Rad),Mathf.Sin(deg*Mathf.Deg2Rad));
        static Vector2 Rot(Vector2 v,float deg){float c=Mathf.Cos(deg*Mathf.Deg2Rad),s=Mathf.Sin(deg*Mathf.Deg2Rad);return new Vector2(c*v.x-s*v.y,s*v.x+c*v.y);}
        static float Circle(Vector2 p,Vector2 c,float r)=>Vector2.Distance(p,c)-r;
        static float Seg(Vector2 p,Vector2 a,Vector2 b){var ab=b-a;float t=Mathf.Clamp01(Vector2.Dot(p-a,ab)/Mathf.Max(ab.sqrMagnitude,1e-9f));return Vector2.Distance(p,a+ab*t);}
        static float Lines(Vector2 p,IList<Vector2> pts){float d=1e9f;for(int i=0;i+1<pts.Count;i++)d=Mathf.Min(d,Seg(p,pts[i],pts[i+1]));return d;}
        static float Ell(Vector2 p,Vector2 c,float rx,float ry,float turn=0){var q=Rot(p-c,-turn);return (new Vector2(q.x/rx,q.y/ry).magnitude-1)*Mathf.Min(rx,ry);}
        static float RBox(Vector2 p,Vector2 c,Vector2 half,float round)
        {
            var q=new Vector2(Mathf.Abs(p.x-c.x)-half.x+round,Mathf.Abs(p.y-c.y)-half.y+round);
            return new Vector2(Mathf.Max(q.x,0),Mathf.Max(q.y,0)).magnitude+Mathf.Min(Mathf.Max(q.x,q.y),0)-round;
        }
        // Distance to the arc of a circle from a0 counter-clockwise to a1 (degrees).
        static float Arc(Vector2 p,Vector2 c,float r,float a0,float a1)
        {
            var q=p-c;float rel=Mathf.Repeat(Mathf.Atan2(q.y,q.x)*Mathf.Rad2Deg-a0,360);
            return rel<=Mathf.Repeat(a1-a0,360)?Mathf.Abs(q.magnitude-r):Mathf.Min(Vector2.Distance(p,c+Dir(a0)*r),Vector2.Distance(p,c+Dir(a1)*r));
        }
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
        static Vector2[] Star(Vector2 c,float r,float inner){var a=new Vector2[10];for(int k=0;k<10;k++)a[k]=c+Dir(90+36*k)*(k%2==0?r:inner);return a;}
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
        static void Dashed(List<Vector2> path,float dash,float gap,float half,Color col,Color halo)
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
            foreach(var d in dashes)Fill(Bounds(d,half+.4f),p=>Lines(p,d)-half-.13f,halo);
            foreach(var d in dashes)Fill(Bounds(d,half+.4f),p=>Lines(p,d)-half,col);
        }

        // --- Lettering: LiberationSans in bold, its coverage read from the font's atlas and turned into a signed distance,
        // so the letters can be thickened into rounded cartoon capitals, inked and given a drop shadow. ------------------
        const string FontFile=FontPath;const int FontPx=96;
        static Font font;static float[] atlasA;static int aw,ah;
        sealed class Label{public int x0,y0,w,h;public float[] sd;}
        static void LoadFont(string chars)
        {
            font=AssetDatabase.LoadAssetAtPath<Font>(FontFile);font.RequestCharactersInTexture(chars,FontPx,FontStyle.Bold);
            var src=font.material.mainTexture;aw=src.width;ah=src.height;var rt=RenderTexture.GetTemporary(aw,ah,0,RenderTextureFormat.ARGB32);
            Graphics.Blit(src,rt);var prev=RenderTexture.active;RenderTexture.active=rt;
            var read=new Texture2D(aw,ah,TextureFormat.RGBA32,false);read.ReadPixels(new Rect(0,0,aw,ah),0,0);read.Apply();
            RenderTexture.active=prev;RenderTexture.ReleaseTemporary(rt);
            var px=read.GetPixels();atlasA=new float[px.Length];for(int i=0;i<px.Length;i++)atlasA[i]=px[i].a;UnityEngine.Object.DestroyImmediate(read);
        }
        static float AtlasAlpha(Vector2 uv)
        {
            float x=uv.x*aw-.5f,y=uv.y*ah-.5f;int x0=Mathf.FloorToInt(x),y0=Mathf.FloorToInt(y);float fx=x-x0,fy=y-y0;
            float A(int i,int j)=>atlasA[Mathf.Clamp(j,0,ah-1)*aw+Mathf.Clamp(i,0,aw-1)];
            return Mathf.Lerp(Mathf.Lerp(A(x0,y0),A(x0+1,y0),fx),Mathf.Lerp(A(x0,y0+1),A(x0+1,y0+1),fx),fy);
        }
        // A line of text with its baseline at 'at' (align 0 left, .5 centred), capitals 'cap' mm high.
        static Label Text(string s,Vector2 at,float cap,float align=.5f,float squeeze=1,float shear=0,float tracking=0)
        {
            font.GetCharacterInfo('H',out CharacterInfo hci,FontPx,FontStyle.Bold);float k=cap/hci.maxY;
            var glyphs=new List<(CharacterInfo ci,float x)>();float pen=0;
            foreach(var ch in s){if(!font.GetCharacterInfo(ch,out CharacterInfo ci,FontPx,FontStyle.Bold))continue;glyphs.Add((ci,pen));pen+=ci.advance*k*squeeze+tracking;}
            float width=pen-tracking,left=at.x-align*width;const float Pad=1.3f;
            var l=new Label{x0=Mathf.FloorToInt((left-Pad)*P),y0=Mathf.FloorToInt((at.y-cap*.4f-Pad)*P)};
            l.w=Mathf.CeilToInt((left+width+Pad+cap*shear)*P)-l.x0;l.h=Mathf.CeilToInt((at.y+cap*1.15f+Pad)*P)-l.y0;
            var cov=new float[l.w*l.h];const int SS=4;
            foreach(var (ci,gx) in glyphs)
            {
                if(ci.maxX<=ci.minX||ci.maxY<=ci.minY)continue;
                float gx0=left+gx+ci.minX*k*squeeze,gx1=left+gx+ci.maxX*k*squeeze,gy0=at.y+ci.minY*k,gy1=at.y+ci.maxY*k;
                int px0=Mathf.Max(0,Mathf.FloorToInt((gx0+shear*Mathf.Min(0,gy0-at.y))*P)-l.x0-1),px1=Mathf.Min(l.w,Mathf.CeilToInt((gx1+shear*Mathf.Max(0,gy1-at.y))*P)-l.x0+1);
                int py0=Mathf.Max(0,Mathf.FloorToInt(gy0*P)-l.y0-1),py1=Mathf.Min(l.h,Mathf.CeilToInt(gy1*P)-l.y0+1);
                for(int y=py0;y<py1;y++)for(int x=px0;x<px1;x++)
                {
                    float sum=0;
                    for(int j=0;j<SS;j++)for(int i=0;i<SS;i++)
                    {
                        float Y=(l.y0+y+(j+.5f)/SS)/P,X=(l.x0+x+(i+.5f)/SS)/P-shear*(Y-at.y);
                        float fx=(X-gx0)/(gx1-gx0),fy=(Y-gy0)/(gy1-gy0);if(fx<0||fx>1||fy<0||fy>1)continue;
                        sum+=AtlasAlpha(Vector2.Lerp(Vector2.Lerp(ci.uvBottomLeft,ci.uvBottomRight,fx),Vector2.Lerp(ci.uvTopLeft,ci.uvTopRight,fx),fy));
                    }
                    int idx=y*l.w+x;cov[idx]=Mathf.Max(cov[idx],sum/(SS*SS));
                }
            }
            int n=l.w*l.h;var inside=new bool[n];var outside=new bool[n];for(int i=0;i<n;i++){inside[i]=cov[i]>=.5f;outside[i]=!inside[i];}
            var dOut=Edt(inside,l.w,l.h);var dIn=Edt(outside,l.w,l.h);l.sd=new float[n];
            for(int i=0;i<n;i++)
            {
                float d=inside[i]?-(Mathf.Sqrt(dIn[i])-.5f):Mathf.Sqrt(dOut[i])-.5f;
                if(cov[i]>0&&cov[i]<1&&Mathf.Abs(d)<=.5f)d=.5f-cov[i];
                l.sd[i]=d/P;
            }
            return l;
        }
        // Paints a label: an optional drop shadow, an ink line round it, the letters thickened by 'bold' (mm).
        static void Stamp(Label l,Color fill,float bold,Color ink,float line,float shadow=0)
        {
            int sh=Mathf.RoundToInt(shadow*P);
            for(int y=0;y<l.h;y++)for(int x=0;x<l.w;x++)
            {
                int cx=l.x0+x,cy=l.y0+y;if(cx<0||cy<0||cx>=CW||cy>=CH)continue;int i=cy*CW+cx;float d=l.sd[y*l.w+x];
                if(sh>0&&x-sh>=0&&y+sh<l.h)C[i]=Color.Lerp(C[i],Ink,Mathf.Clamp01(.5f-(l.sd[(y+sh)*l.w+x-sh]-bold-line)*P)*.35f);
                if(line>0)C[i]=Color.Lerp(C[i],ink,Mathf.Clamp01(.5f-(d-bold-line)*P));
                C[i]=Color.Lerp(C[i],fill,Mathf.Clamp01(.5f-(d-bold)*P));
            }
        }
        // Squared Euclidean distance to the nearest set cell (Felzenszwalb and Huttenlocher's separable transform).
        static float[] Edt(bool[] set,int w,int h)
        {
            const float Inf=1e20f;var g=new float[w*h];int n=Mathf.Max(w,h);var f=new float[n];var d=new float[n];var v=new int[n];var z=new float[n+1];
            for(int i=0;i<w*h;i++)g[i]=set[i]?0:Inf;
            for(int x=0;x<w;x++){for(int y=0;y<h;y++)f[y]=g[y*w+x];Edt1(f,h,d,v,z);for(int y=0;y<h;y++)g[y*w+x]=d[y];}
            for(int y=0;y<h;y++){for(int x=0;x<w;x++)f[x]=g[y*w+x];Edt1(f,w,d,v,z);for(int x=0;x<w;x++)g[y*w+x]=d[x];}
            return g;
        }
        static void Edt1(float[] f,int n,float[] d,int[] v,float[] z)
        {
            int k=0;v[0]=0;z[0]=-1e20f;z[1]=1e20f;
            for(int q=1;q<n;q++)
            {
                float s=(f[q]+q*q-(f[v[k]]+v[k]*v[k]))/(2*q-2*v[k]);
                while(s<=z[k]){k--;s=(f[q]+q*q-(f[v[k]]+v[k]*v[k]))/(2*q-2*v[k]);}
                k++;v[k]=q;z[k]=s;z[k+1]=1e20f;
            }
            k=0;for(int q=0;q<n;q++){while(z[k+1]<q)k++;d[q]=(q-v[k])*(q-v[k])+f[v[k]];}
        }
    }
}
