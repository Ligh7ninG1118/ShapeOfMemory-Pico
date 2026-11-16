using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static ShadowWorkshopIntegration.Editor.PropMesh;

namespace ShadowWorkshopIntegration.Editor
{
    // Detailed looks for the GoodMorning kitchen pieces: a Swiss cheese wedge, a steel fork, a salt jar (in place of the
    // plain glass tumbler) and a printed sugar-cube box (in place of the plain box). Each keeps its original's pivot and
    // outline and is only drawn: the original mesh stays the piece's shadow (ShadowPiece.shadowProxy in the gym,
    // Torch2Piece.shadowMesh in levels), so the puzzle's picture and scoring are unchanged. Kept to a mobile VR budget
    // (a few hundred triangles each); the detail is in the textures. Printed textures come from
    // Tools/ShadowArt/build_kitchen_labels.py; the cheese and wood textures are generated here.
    public static class KitchenPropsBuilder
    {
        const string Meshes="Assets/ShadowWorkshop/Art/Torch2/Meshes/",Textures="Assets/ShadowWorkshop/Art/Torch2/Textures/";
        const string MaterialFolder="Assets/ShadowWorkshop/Art/Torch2/Materials/";
        const string HolesPath=Textures+"cheese holes.png",HolesNormalPath=Textures+"cheese holes normal.png",WoodPath=Textures+"wood grain.png";
        const string LabelPath=Textures+"salt jar label.png",SugarPrintPath=Textures+"sugar box.png";

        public sealed class Prop{public string label,mesh,shadow;public Look[] looks;}
        // label: the gym object's new name (null keeps it). shadow: the original mesh asset, which stays the shadow.
        public static readonly Prop[] Props={
            new Prop{mesh=Meshes+"Cheese wedge detailed.asset",shadow=Meshes+"Cheese wedge.asset",looks=new[]{
                new Look("cheese paste",Color.white,0f,.30f){baseMap=()=>Load(HolesPath),normalMap=()=>Load(HolesNormalPath),bumpScale=1f},
                new Look("cheese rind",new Color(.80f,.58f,.26f),0f,.28f)}},
            new Prop{mesh=Meshes+"Fork detailed.asset",shadow=Meshes+"Fork.asset",looks=new[]{
                new Look("kitchen steel",new Color(.86f,.87f,.89f),.75f,.86f)}},
            new Prop{label="SALT JAR",mesh=Meshes+"Salt jar.asset",shadow=Meshes+"glass_tumbler_hipoly_empty.obj",looks=new[]{
                new Look("salt jar glaze",Color.white,0f,.78f){baseMap=()=>Load(LabelPath)},
                new Look("salt jar wood",Color.white,0f,.32f){baseMap=()=>Load(WoodPath)}}},
            new Prop{label="SUGAR BOX",mesh=Meshes+"Sugar box.asset",shadow=Meshes+"sugar_box_closed.obj",looks=new[]{
                new Look("sugar box print",Color.white,0f,.30f){baseMap=()=>Load(SugarPrintPath)}}}};
        static Texture2D Load(string path)=>AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        public static Mesh ShadowOf(Prop p)=>AssetDatabase.LoadAllAssetsAtPath(p.shadow) is var all&&all.Length>0?System.Array.Find(all,o=>o is Mesh) as Mesh:null;
        public static Material[] Materials(Prop p)=>PropMesh.Materials(MaterialFolder,p.looks);

        [MenuItem("MetaHackthone/Shadow Workshop/Build kitchen props")]
        public static void Build()
        {
            CheeseTextures();WoodGrain();Prints();
            var meshes=new[]{Cheese(),Fork(),SaltJar(),SugarBox()};
            foreach(var p in Props)Materials(p);
            Debug.Log("KITCHEN_PROPS_OK "+string.Join(", ",System.Array.ConvertAll(meshes,m=>$"{m.name} {m.triangles.Length/3} tris")));
        }

        // --- Cheese: right-triangle wedge (point at -x, rind face at +x), 90 x 45 mm, 40 mm thick along z -------------------
        static Mesh Cheese()
        {
            var outline=new List<Vector2>();
            var A=new Vector2(-.045f,-.0225f);var B=new Vector2(.0435f,-.0225f);var C=new Vector2(.0435f,.0225f);
            // The point's radius stays small: a sharp corner gives up r / sin(half angle) of its length to the round.
            Fillet(outline,C,A,B,.0004f,2);Fillet(outline,A,B,C,.0020f,3);
            // Rind face: a shallow arc (the wheel's curve) between the fillets at B and C.
            var from=outline[outline.Count-1];var to=FilletStart(B,C,A,.0016f);
            for(int k=1;k<6;k++){float t=k/6f;var p=Vector2.Lerp(from,to,t);p.x+=.0015f*4*t*(1-t);outline.Add(p);}
            Fillet(outline,B,C,A,.0016f,3);
            var b=new PropMesh(2);var pts=outline.ToArray();
            b.Slab(0,i=>pts[i].x>.040f&&pts[(i+1)%pts.Length].x>.040f?1:0,pts,.0015f,p=>.020f,p=>0,2,1/.06f);
            return b.Save(Props[0].mesh,"Cheese wedge detailed");
        }
        // Rounds the corner at v (coming from prev, going to next) with radius r: adds steps+1 points along the arc.
        static void Fillet(List<Vector2> into,Vector2 prev,Vector2 v,Vector2 next,float r,int steps)
        {
            var d1=(v-prev).normalized;var d2=(next-v).normalized;float turn=Vector2.Angle(-d1,d2)*Mathf.Deg2Rad;
            float t=r/Mathf.Tan(turn/2);var center=v+(d2-d1).normalized*(r/Mathf.Sin(turn/2));
            var a=v-d1*t;var c=v+d2*t;float a0=Mathf.Atan2(a.y-center.y,a.x-center.x),a1=Mathf.Atan2(c.y-center.y,c.x-center.x);
            float sweep=Mathf.DeltaAngle(a0*Mathf.Rad2Deg,a1*Mathf.Rad2Deg)*Mathf.Deg2Rad;
            for(int k=0;k<=steps;k++){float ang=a0+sweep*k/steps;into.Add(center+new Vector2(Mathf.Cos(ang),Mathf.Sin(ang))*r);}
        }
        static Vector2 FilletStart(Vector2 prev,Vector2 v,Vector2 next,float r)
        {
            var d1=(v-prev).normalized;var d2=(next-v).normalized;float turn=Vector2.Angle(-d1,d2)*Mathf.Deg2Rad;return v-d1*(r/Mathf.Tan(turn/2));
        }

        // --- Fork: tines at -y, handle end at +y, 26 x 163 mm, within the original's 4 mm depth ---------------------------
        static Mesh Fork()
        {
            var pts=new List<Vector2>();
            const float endR=.006f,endY=.0813f-endR;
            var width=new[]{(.0753f,.0060f),(.060f,.0059f),(.045f,.0055f),(.030f,.0050f),(.015f,.0045f),(0f,.0040f),(-.012f,.0036f),
                (-.020f,.0035f),(-.027f,.0042f),(-.033f,.0060f),(-.038f,.0085f),(-.042f,.0108f),(-.046f,.0122f)};
            for(int k=0;k<4;k++){float a=(90+k*22.5f)*Mathf.Deg2Rad;pts.Add(new Vector2(endR*Mathf.Cos(a),endY+endR*Mathf.Sin(a)));}
            foreach(var (y,w) in width)pts.Add(new Vector2(-w,y));
            const float gap=.0018f,baseY=-.050f,tipY=-.0813f,wb=.00475f,wt=.0040f;float tipC=tipY+wt/2;
            float Cb(int t)=>-.0122f+wb/2+t*(wb+gap);float Ct(int t)=>-.0107f+wt/2+t*(wt+gap);
            pts.Add(new Vector2(-.0122f,baseY));
            for(int t=0;t<4;t++)
            {
                for(int k=0;k<=4;k++){float a=(180+k*45)*Mathf.Deg2Rad;pts.Add(new Vector2(Ct(t)+wt/2*Mathf.Cos(a),tipC+wt/2*Mathf.Sin(a)));}
                if(t==3)break;
                float slot=(Cb(t)+wb/2+Cb(t+1)-wb/2)/2;
                for(int k=0;k<=2;k++){float a=(180-k*90)*Mathf.Deg2Rad;pts.Add(new Vector2(slot+gap/2*Mathf.Cos(a),baseY+gap/2*Mathf.Sin(a)));}
            }
            pts.Add(new Vector2(.0122f,baseY));
            for(int i=width.Length-1;i>=0;i--)pts.Add(new Vector2(width[i].Item2,width[i].Item1));
            for(int k=1;k<4;k++){float a=k*22.5f*Mathf.Deg2Rad;pts.Add(new Vector2(endR*Mathf.Cos(a),endY+endR*Mathf.Sin(a)));}
            // Thinner towards the tines; both ends bend a little towards -z, the neck is the low point.
            float Half(Vector2 p)=>p.y>0?.0013f:p.y>-.02f?Mathf.Lerp(.0010f,.0013f,(p.y+.02f)/.02f):Mathf.Lerp(.0007f,.0010f,Mathf.Clamp01((p.y+.05f)/.03f));
            float Mid(Vector2 p){float s=p.y<-.02f?(p.y+.02f)/.06f:(p.y+.02f)/.10f;return(p.y<-.02f?-.0008f:-.0004f)*s*s;}
            var b=new PropMesh(1);b.Slab(0,i=>0,pts.ToArray(),.0005f,Half,Mid,1,25);
            return b.Save(Props[1].mesh,"Fork detailed");
        }

        // --- Salt jar: speckled glazed crock with a wooden lid, the tumbler's taper (r 32.5 -> 39 mm over 110 mm) ----------
        const int JarSides=32;
        static Mesh SaltJar()
        {
            var b=new PropMesh(2);var up=Vector3.up;const int Glaze=0,Wood=1;
            // Label band from y 6 mm to 95 mm; the foot and rim clamp to its plain edge rows.
            const float v0=.006f,v1=.095f;var label=new Vector2(1/(v1-v0),-v0/(v1-v0));
            b.Lathe(Vector3.zero,up,JarSides,label,true,P(0,.0006f,Glaze),P(.0298f,.0006f,Glaze),P(.0320f,.0022f,Glaze),P(.0327f,.0065f,Glaze),P(.0381f,.0955f,Glaze),P(.0372f,.0968f,Glaze));
            b.Lathe(Vector3.zero,up,JarSides,P(.0370f,.0962f,Wood),P(.0388f,.0962f,Wood),P(.0391f,.0974f,Wood),P(.0391f,.1088f,Wood),P(.0383f,.1101f,Wood));
            // Lid top: a flat fan with planar UVs so the grain runs straight across.
            int center=b.Add(new Vector3(0,.1101f,0),up,new Vector2(.5f,.5f));
            for(int k=0;k<=JarSides;k++){float a=k*2*Mathf.PI/JarSides;var p=new Vector3(Mathf.Cos(a)*.0383f,.1101f,Mathf.Sin(a)*.0383f);b.Add(p,up,new Vector2(p.x/.08f+.5f,p.z/.08f+.5f));}
            for(int k=0;k<JarSides;k++)b.Tri(Wood,center,center+1+k,center+2+k);
            return b.Save(Props[2].mesh,"Salt jar");
        }

        // --- Sugar box: the original's 110 x 64 x 64 mm box and its slightly larger lid, printed from one atlas -----------
        // Regions in atlas px (x, y, w, h from the top left); must match REGIONS in build_kitchen_labels.py.
        static readonly RectInt Front=new RectInt(8,8,500,291),Back=new RectInt(516,8,500,291),Top=new RectInt(8,307,500,291),
            Bottom=new RectInt(516,307,500,291),Left=new RectInt(8,606,291,291),Right=new RectInt(307,606,291,291);
        static Mesh SugarBox()
        {
            var b=new PropMesh(1);
            const float X=.055f,Y=.0641f,Z=.032f,LX=.0553f,LY=.065f,LZ=.0323f;
            // Box sides (its top is under the lid) ...
            Face(b,new Vector3(-X,0,-Z),new Vector3(2*X,0,0),new Vector3(0,Y,0),Front,0,1);
            Face(b,new Vector3(X,0,Z),new Vector3(-2*X,0,0),new Vector3(0,Y,0),Back,0,1);
            Face(b,new Vector3(X,0,-Z),new Vector3(0,0,2*Z),new Vector3(0,Y,0),Right,0,1);
            Face(b,new Vector3(-X,0,Z),new Vector3(0,0,-2*Z),new Vector3(0,Y,0),Left,0,1);
            Face(b,new Vector3(-X,0,Z),new Vector3(2*X,0,0),new Vector3(0,0,-2*Z),Bottom,0,1);
            // ... and the lid: its top, and its thin rim reusing the top rows of each side's print.
            Face(b,new Vector3(-LX,LY,-LZ),new Vector3(2*LX,0,0),new Vector3(0,0,2*LZ),Top,0,1);
            const float rim=.985f;
            Face(b,new Vector3(-LX,Y,-LZ),new Vector3(2*LX,0,0),new Vector3(0,LY-Y,0),Front,rim,1);
            Face(b,new Vector3(LX,Y,LZ),new Vector3(-2*LX,0,0),new Vector3(0,LY-Y,0),Back,rim,1);
            Face(b,new Vector3(LX,Y,-LZ),new Vector3(0,0,2*LZ),new Vector3(0,LY-Y,0),Right,rim,1);
            Face(b,new Vector3(-LX,Y,LZ),new Vector3(0,0,-2*LZ),new Vector3(0,LY-Y,0),Left,rim,1);
            return b.Save(Props[3].mesh,"Sugar box");
        }
        // One flat face from origin spanning u (left to right as seen from outside) and v (bottom to top), printed with
        // the atlas region; t0..t1 picks a horizontal band of the region (the lid rim uses its top rows).
        static void Face(PropMesh b,Vector3 origin,Vector3 u,Vector3 v,RectInt region,float t0,float t1)
        {
            var n=Vector3.Cross(v,u).normalized;var q=new int[4];
            for(int i=0;i<4;i++)
            {
                float s=i==1||i==2?1:0,t=i>=2?1:0;float tt=Mathf.Lerp(t0,t1,t);
                q[i]=b.Add(origin+u*s+v*t,n,new Vector2((region.x+s*region.width)/1024f,1-(region.y+(1-tt)*region.height)/1024f));
            }
            b.Quad(0,q[0],q[1],q[2],q[3]);
        }

        // --- Textures ---------------------------------------------------------------------------------------------------
        // Swiss cheese: a 60 mm tile of spherical eyes and small pores; albedo darkens inside them, a normal map shapes them.
        static void CheeseTextures()
        {
            const int size=512;const float mm=size/60f;var rnd=new System.Random(11);var holes=new List<Vector3>();
            for(int i=0;i<9;i++)holes.Add(new Vector3(rnd.Next(size),rnd.Next(size),Mathf.Lerp(2.5f,5.5f,(float)rnd.NextDouble())*mm));
            for(int i=0;i<30;i++)holes.Add(new Vector3(rnd.Next(size),rnd.Next(size),Mathf.Lerp(.5f,1.4f,(float)rnd.NextDouble())*mm));
            var height=new float[size,size];var depth=new float[size,size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float h=0,d01=0;
                foreach(var hole in holes)
                {
                    float dx=Mathf.Abs(x-hole.x),dy=Mathf.Abs(y-hole.y);dx=Mathf.Min(dx,size-dx);dy=Mathf.Min(dy,size-dy);
                    float q=(dx*dx+dy*dy)/(hole.z*hole.z);if(q>=1)continue;float dent=Mathf.Sqrt(1-q);
                    if(-dent*hole.z*.6f<h){h=-dent*hole.z*.6f;d01=dent;}
                }
                height[y,x]=h;depth[y,x]=d01;
            }
            var albedo=new Texture2D(size,size,TextureFormat.RGB24,false,false);var normal=new Texture2D(size,size,TextureFormat.RGB24,false,true);
            var paste=new Color(.98f,.84f,.46f);
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float u=x/(float)size,v=y/(float)size;float tint=.95f+.07f*Fbm(u,v,6,6,3,3);
                var c=paste*tint*(1-.22f*depth[y,x]);c.a=1;albedo.SetPixel(x,y,c);
                float hx=(height[y,(x+1)%size]-height[y,(x+size-1)%size])*.5f,hy=(height[(y+1)%size,x]-height[(y+size-1)%size,x])*.5f;
                var n=new Vector3(-hx,-hy,1).normalized;normal.SetPixel(x,y,new Color(n.x*.5f+.5f,n.y*.5f+.5f,n.z*.5f+.5f));
            }
            SaveTexture(HolesPath,albedo,false);SaveTexture(HolesNormalPath,normal,true);
        }
        // Straight wood grain along u, tileable: warped rings in v.
        static void WoodGrain()
        {
            const int size=256;var tex=new Texture2D(size,size,TextureFormat.RGB24,false,false);
            var light=new Color(.74f,.54f,.33f);var dark=new Color(.48f,.30f,.16f);
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float u=x/(float)size,v=y/(float)size;float t=v*9+.6f*Fbm(u,v,2,3,3,1);float ring=Mathf.Pow(.5f+.5f*Mathf.Sin(t*2*Mathf.PI),3);
                var c=Color.Lerp(light,dark,ring*.75f+.25f*Fbm(u,v,16,4,2,8));c.a=1;tex.SetPixel(x,y,c);
            }
            SaveTexture(WoodPath,tex,false);
        }
        // The printed PNGs from build_kitchen_labels.py: clamp (the label wraps once; atlas regions are padded).
        static void Prints()
        {
            foreach(var path in new[]{LabelPath,SugarPrintPath})
            {
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);if(!importer){Debug.LogWarning("Missing "+path+"; run Tools/ShadowArt/build_kitchen_labels.py");continue;}
                importer.wrapMode=TextureWrapMode.Clamp;importer.anisoLevel=4;importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.SaveAndReimport();
            }
        }
    }
}
