using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static ShadowWorkshopIntegration.Editor.PropMesh;

namespace ShadowWorkshopIntegration.Editor
{
    // Two inflatables for the Summer level (2026-11-17, see SummerBuilder), built like the other props: procedural, real
    // size, smooth normals doing the rounding, detail in textures drawn here, a light closed hull casting the shadow.
    // SWIM RING: 500 mm across, its tube 120 mm, lying flat: eight equal panels of glossy vinyl, cream and turquoise, welded
    // seams between them and round the outer and inner edge (all in the texture). 288 triangles; shadow 192.
    // FLAMINGO FLOAT: a pink inflatable drink holder: a puffy ring 220 mm across and 54 mm tall round an 80 mm cup hole, and
    // from its front-left a neck 45 mm thick rising in an S to 250 mm, curling forward over a head whose beak turns down,
    // pale at its root and black at the tip. Printed on it: folded wings outlined in dark red with scalloped feathers on
    // both sides of the ring, blue eyes ringed in white on the head, and the welded seams. One texture holds the ring
    // (lower half) and the neck (upper half). About 500 triangles; shadow 238 (ring, neck with head, beak).
    // Each in its own frame: metres, +y up, -z the front, lying on y = 0.
    public static class SummerInflatablesBuilder
    {
        const string SwimRingPath=SummerBuilder.Meshes+"Summer swim ring.asset",SwimRingShadowPath=SummerBuilder.Meshes+"Summer swim ring shadow.asset";
        const string FlamingoPath=SummerBuilder.Meshes+"Summer flamingo float.asset",FlamingoShadowPath=SummerBuilder.Meshes+"Summer flamingo float shadow.asset";
        const string SwimRingTexPath=SummerBuilder.Textures+"summer swim ring.png",FlamingoTexPath=SummerBuilder.Textures+"summer flamingo vinyl.png",BeakTexPath=SummerBuilder.Textures+"summer flamingo beak.png";

        static Texture2D Load(string path)=>AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        static readonly Look SwimVinyl=new Look("summer swim ring vinyl",Color.white,0f,.72f){baseMap=()=>Load(SwimRingTexPath)??SwimRingTexture()};
        static readonly Look FlamingoVinyl=new Look("summer flamingo vinyl",Color.white,0f,.70f){baseMap=()=>Load(FlamingoTexPath)??FlamingoTexture()};
        static readonly Look Beak=new Look("summer flamingo beak",Color.white,0f,.60f){baseMap=()=>Load(BeakTexPath)??BeakTexture()};

        [MenuItem("MetaHackthone/Shadow Workshop/Summer/Build inflatables")]
        public static void Build()
        {
            SwimRingTexture();FlamingoTexture();BeakTexture();
            var built=new[]{
                SummerBuilder.Stage("SWIM RING",SwimRing(),SwimRingShadow(),PropMesh.Materials(SummerBuilder.MaterialFolder,new[]{SwimVinyl})),
                SummerBuilder.Stage("FLAMINGO FLOAT",Flamingo(),FlamingoShadow(),PropMesh.Materials(SummerBuilder.MaterialFolder,new[]{FlamingoVinyl,Beak}))};
            Debug.Log("SUMMER_INFLATABLES_OK "+string.Join(", ",built.Select(g=>{var m=g.GetComponent<MeshFilter>().sharedMesh;var s=g.GetComponent<CloudGarden.ShadowPuzzle.ShadowPiece>().shadowProxy;
                return $"{m.name} {m.triangles.Length/3} (shadow {(s?s.triangles.Length/3:m.triangles.Length/3)}), {m.bounds.size.x*1000:0} x {m.bounds.size.y*1000:0} x {m.bounds.size.z*1000:0} mm";})));
        }

        // --- Torus ---------------------------------------------------------------------------------------------------------
        // Lying round the y axis: the tube's centre circle radius R at height y0, its section an ellipse of radii a (out) and
        // c (up). Round the ring u runs 0..1 from the back (+z): .25 left (-x), .5 front, .75 right; the section angle starts
        // at the outer equator and runs up over the top, v = angle / 2pi. straddle (shadow hulls) pushes the polygons out
        // so they straddle the round shapes, their corners as far outside as their flats are inside.
        static void Torus(PropMesh b,int m,int major,int minor,float R,float a,float c,float y0,bool straddle)
        {
            float g=straddle?Grow(major):1,s=straddle?Grow(minor):1;int start=b.v.Count;
            for(int k=0;k<=major;k++)
            {
                float th=Mathf.PI/2+k*2*Mathf.PI/major;var radial=new Vector3(Mathf.Cos(th),0,Mathf.Sin(th));
                for(int j=0;j<=minor;j++)
                {
                    float ph=j*2*Mathf.PI/minor,cs=Mathf.Cos(ph),sn=Mathf.Sin(ph);
                    b.Add(radial*(R+a*s*cs)*g+Vector3.up*(y0+c*s*sn),(radial*cs/a+Vector3.up*sn/c).normalized,new Vector2((float)k/major,(float)j/minor));
                }
            }
            for(int k=0;k<major;k++)for(int j=0;j<minor;j++){int q=start+k*(minor+1)+j;b.Quad(m,q,q+minor+1,q+minor+2,q+1);}
        }
        static float Grow(int sides)=>2/(1+Mathf.Cos(Mathf.PI/sides));

        // --- Swim ring -----------------------------------------------------------------------------------------------------
        const float SwimR=.19f,SwimTube=.06f;
        static Mesh SwimRing()
        {
            var b=new PropMesh(1);Torus(b,0,18,8,SwimR,SwimTube,SwimTube,SwimTube,false);
            return b.Save(SwimRingPath,"Summer swim ring");
        }
        static Mesh SwimRingShadow()
        {
            var b=new PropMesh(1);Torus(b,0,16,6,SwimR,SwimTube,SwimTube,SwimTube,true);
            return b.Save(SwimRingShadowPath,"Summer swim ring shadow");
        }

        // --- Flamingo float --------------------------------------------------------------------------------------------------
        const float FR=.075f,FA=.035f,FC=.027f;   // ring: tube centre radius, section radii out and up (it sits on y = 0)
        const float NeckU=.403f;                    // the neck's root round the ring (u, as Torus): front-left
        static Vector3 NeckRoot{get{float th=Mathf.PI/2+NeckU*2*Mathf.PI;return new Vector3(Mathf.Cos(th)*FR,0,Mathf.Sin(th)*FR);}}
        // Centre lines in the neck's upright plane, (forward, up) from the root: the neck from inside the ring, up with a
        // forward lean, back, then curling forward over the crown to the head; the beak from inside the head, forward and down.
        static readonly Vector2[] NeckLine={new Vector2(0,.030f),new Vector2(.004f,.068f),new Vector2(.011f,.105f),new Vector2(.010f,.138f),new Vector2(0,.165f),
            new Vector2(-.007f,.190f),new Vector2(-.002f,.212f),new Vector2(.014f,.223f),new Vector2(.034f,.225f),new Vector2(.050f,.214f)};
        static readonly Vector2[] BeakLine={new Vector2(.048f,.212f),new Vector2(.068f,.198f),new Vector2(.080f,.185f),new Vector2(.086f,.170f),new Vector2(.084f,.158f)};
        const float HeadR=.026f,EyeBack=.014f;      // the head's radius; the eyes' centres this far back from the head's end
        static Vector3 InPlane(Vector2 p)=>NeckRoot+new Vector3(0,p.y,-p.x);
        // The curve through the points, as count points evenly spaced along it.
        static List<Vector3> Even(Vector2[] points,int count)
        {
            var dense=Curve(points.Select(InPlane).ToList(),8);var along=Along(dense);var list=new List<Vector3>();int j=0;
            for(int k=0;k<count;k++)
            {
                float s=along[dense.Count-1]*k/(count-1);while(j+2<dense.Count&&along[j+1]<s)j++;
                list.Add(Vector3.Lerp(dense[j],dense[j+1],Mathf.InverseLerp(along[j],along[j+1],s)));
            }
            return list;
        }
        static float[] Along(IList<Vector3> path){var a=new float[path.Count];for(int i=1;i<path.Count;i++)a[i]=a[i-1]+Vector3.Distance(path[i-1],path[i]);return a;}
        // Neck radius at t (0..1 along it): 22.5 mm, flaring into the ring, swelling to the head over its last 50 mm.
        static float NeckRadius(float t,float length)
        {
            float s=t*length,fromEnd=length-s;
            return .0225f+(HeadR-.0225f)*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.050f,.022f,fromEnd))+.008f*Mathf.Pow(Mathf.Clamp01(1-s/.07f),2);
        }
        static float BeakRadius(float t)=>Mathf.Lerp(.0125f,.0045f,Mathf.Pow(t,1.1f));
        const int NeckPoints=11,NeckSides=8;

        // Texture layout (FW x FH): the ring in rows 0..RH-1 (row r is section angle r/(RH-1) round), the neck in rows RH..FH-1
        // (row RH+r is angle r/(RH-1) round its section, u along it). UVs hit row centres, so the halves never blend.
        const int FW=1024,FH=512,RH=FH/2;
        static void ToRows(PropMesh b,int from,bool neck){for(int i=from;i<b.uv.Count;i++)b.uv[i]=new Vector2(b.uv[i].x,((neck?RH:0)+.5f+b.uv[i].y*(RH-1))/FH);}

        // Submeshes: vinyl (ring, neck, head), beak.
        static Mesh Flamingo()
        {
            var b=new PropMesh(2);
            Torus(b,0,16,8,FR,FA,FC,FC,false);ToRows(b,0,false);
            var neck=Even(NeckLine,NeckPoints);float length=Along(neck)[NeckPoints-1];int from=b.v.Count;
            b.Bar(0,neck,Vector3.right,t=>{float r=NeckRadius(t,length);return Ellipse(r,r,NeckSides);},0,HeadR,2);ToRows(b,from,true);
            b.Bar(1,Even(BeakLine,5),Vector3.right,t=>{float r=BeakRadius(t);return Ellipse(r*.85f,r,6);},0,.0045f,2);
            return b.Save(FlamingoPath,"Summer flamingo float");
        }
        // Its shadow: the ring as a 12 x 6 torus, the neck and head a pentagonal tube (pointed caps), the beak a diamond one.
        static Mesh FlamingoShadow()
        {
            var b=new PropMesh(1);
            Torus(b,0,12,6,FR,FA,FC,FC,true);
            var neck=Even(NeckLine,7);float length=Along(neck)[6],g5=Grow(5),g4=Grow(4);
            b.Bar(0,neck,Vector3.right,t=>{float r=NeckRadius(t,length)*g5;return Ellipse(r,r,5);},.015f,HeadR,1);
            b.Bar(0,Even(BeakLine,4),Vector3.right,t=>{float r=BeakRadius(t)*g4;return Ellipse(r*.85f,r,4);},.006f,.0045f,1);
            return b.Save(FlamingoShadowPath,"Summer flamingo float shadow");
        }

        // --- Textures (detail colours kept at about 0.6 or below: the gym's light lifts lit faces about 1.5x) ----------------
        // Eight panels, cream and turquoise, a soft welded seam between them and round the outer and inner edge.
        static Texture2D SwimRingTexture()
        {
            const int W=512,H=128;var tex=new Texture2D(W,H,TextureFormat.RGB24,false,false);
            var cream=new Color(.60f,.57f,.45f);var teal=new Color(.12f,.47f,.52f);
            for(int y=0;y<H;y++)for(int x=0;x<W;x++)
            {
                float u=(x+.5f)/W,v=(y+.5f)/H,f=u*8,edge=Mathf.Abs(f-Mathf.Round(f))*W/8;   // pixels to the nearest panel edge
                var c=Mathf.FloorToInt(f)%2==0?cream:teal;if(edge<.5f)c=(cream+teal)/2;
                c*=.96f+.08f*Fbm(u,v,16,4,3,211);
                c*=Mathf.Lerp(.84f,1,Mathf.Clamp01(edge-.5f));
                float rim=Mathf.Min(Mathf.Abs(v-Mathf.Round(v*2)/2),1)*H;c*=Mathf.Lerp(.88f,1,Mathf.Clamp01(rim-.5f));
                c.a=1;tex.SetPixel(x,y,c);
            }
            return SaveTexture(SwimRingTexPath,tex,false);
        }

        // Pink vinyl. Ring: a seam round the outer and inner edge, and a folded wing on each side, drawn in millimetres on the
        // surface (x back from the wing's front, y up the section from 28 degrees above the outer edge). Neck: a seam down its
        // front and back, and the eyes.
        static Texture2D FlamingoTexture()
        {
            var tex=new Texture2D(FW,FH,TextureFormat.RGB24,false,false);
            var pink=new Color(.60f,.30f,.43f);var fill=new Color(.62f,.37f,.49f);var ink=new Color(.40f,.05f,.10f);
            // Arc length (mm) round the ring's section from the outer edge, upward.
            float Arc(float ph){const int N=48;float sum=0;for(int i=0;i<N;i++){float p=(i+.5f)*ph/N;sum+=Mathf.Sqrt(FA*FA*Mathf.Sin(p)*Mathf.Sin(p)+FC*FC*Mathf.Cos(p)*Mathf.Cos(p));}return sum*ph/N*1000;}
            float whole=Arc(2*Mathf.PI),wingY=Arc(28*Mathf.Deg2Rad);
            for(int y=0;y<RH;y++)
            {
                float ph=y*2*Mathf.PI/(RH-1),wy=Arc(ph);if(wy>whole/2)wy-=whole;wy-=wingY;
                float seam=Mathf.Min(Mathf.Min(ph,Mathf.Abs(ph-Mathf.PI)),2*Mathf.PI-ph)*FC*1000,circ=2*Mathf.PI*(FR+FA*Mathf.Cos(ph))*1000;
                for(int x=0;x<FW;x++)
                {
                    float u=(x+.5f)/FW;var c=pink*(.96f+.07f*Fbm(u,(float)y/(RH-1),24,6,3,301));
                    c*=Mathf.Lerp(.84f,1,Mathf.Clamp01((seam-.5f)/.6f));
                    foreach(var (front,dir) in new[]{(.32f,1f),(.68f,-1f)})
                    {
                        var p=new Vector2((front-u)*dir*circ,wy);if(p.x<-20||p.x>140||Mathf.Abs(p.y)>40)continue;
                        Wing(p,out bool inside,out float line);
                        if(inside)c=fill*(.97f+.05f*Fbm(u,(float)y/(RH-1),24,6,3,307));
                        c=Color.Lerp(c,ink,line);
                    }
                    c.a=1;tex.SetPixel(x,y,c);
                }
            }
            // Neck: where the eyes fall, from the drawn neck's own path and frames.
            var neck=Even(NeckLine,NeckPoints);var along=Along(neck);float length=along[NeckPoints-1],eyeS=length-EyeBack;
            int seg=0;while(seg+2<NeckPoints&&along[seg+1]<eyeS)seg++;
            var tangent=(neck[seg+1]-neck[seg]).normalized;var side=(Vector3.right-tangent*Vector3.Dot(Vector3.right,tangent)).normalized;var up=Vector3.Cross(tangent,side);
            var eyes=new[]{-1f,1f}.Select(sx=>{var d=side*sx*Mathf.Cos(20*Mathf.Deg2Rad)+Vector3.up*Mathf.Sin(20*Mathf.Deg2Rad);d-=tangent*Vector3.Dot(d,tangent);
                return Mathf.Atan2(Vector3.Dot(d,up),Vector3.Dot(d,side));}).ToArray();
            float eyeT=eyeS/length,eyeR=NeckRadius(eyeT,length)*1000;
            var white=new Color(.60f,.60f,.58f);var blue=new Color(.13f,.36f,.62f);var pupil=new Color(.04f,.04f,.07f);var rim=new Color(.30f,.07f,.12f);
            Color Ring(Color under,Color over,float d,float edge)=>Color.Lerp(over,under,Mathf.Clamp01((d-edge)/.45f+.5f));
            for(int y=RH;y<FH;y++)
            {
                float th=(y-RH)*2*Mathf.PI/(RH-1);
                for(int x=0;x<FW;x++)
                {
                    float t=(x+.5f)/FW,r=NeckRadius(t,length)*1000;var c=pink*(.96f+.07f*Fbm(t,(float)(y-RH)/(RH-1),12,6,3,311));
                    float seam=Mathf.Min(Mathf.Abs(th-Mathf.PI/2),Mathf.Abs(th-1.5f*Mathf.PI))*r;c*=Mathf.Lerp(.84f,1,Mathf.Clamp01((seam-.5f)/.6f));
                    foreach(var e in eyes)
                    {
                        float du=(t-eyeT)*length*1000,dv=Mathf.DeltaAngle(e*Mathf.Rad2Deg,th*Mathf.Rad2Deg)*Mathf.Deg2Rad*eyeR,d=Mathf.Sqrt(du*du+dv*dv);
                        if(d>8.5f)continue;
                        c=Ring(c,rim,d,7.6f);c=Ring(c,white,d,6.8f);c=Ring(c,blue,d,4.3f);c=Ring(c,pupil,d,2.0f);
                    }
                    c.a=1;tex.SetPixel(x,y,c);
                }
            }
            return SaveTexture(FlamingoTexPath,tex,false);
        }
        // The wing, in millimetres (x back from its rounded front, y up), as layers from the bottom up: three long primaries
        // trailing back, a row of round secondaries along the lower edge, a row of smaller coverts above them, the body on top.
        // Each layer's outline is drawn where no layer above covers it, so the rows read as scalloped feathers.
        static float Seg(Vector2 p,Vector2 a,Vector2 b){var d=b-a;float t=Mathf.Clamp01(Vector2.Dot(p-a,d)/d.sqrMagnitude);return Vector2.Distance(p,a+d*t);}
        static float Oval(Vector2 p,Vector2 c,float ax,float ay)
        {
            var q=p-c;float k=new Vector2(q.x/ax,q.y/ay).magnitude;if(k<1e-4f)return -Mathf.Min(ax,ay);
            return (k-1)*k/new Vector2(q.x/(ax*ax),q.y/(ay*ay)).magnitude;
        }
        static readonly System.Func<Vector2,float>[] WingLayers=new System.Func<Vector2,float>[]{
            p=>Seg(p,new Vector2(60,14),new Vector2(108,9))-6f,p=>Seg(p,new Vector2(58,4),new Vector2(116,-1))-6.5f,p=>Seg(p,new Vector2(54,-6),new Vector2(105,-12))-6.5f}
            .Concat(new[]{(13f,-10f),(24f,-12.5f),(36f,-13.5f),(48f,-13f),(60f,-11f)}.Select(o=>(System.Func<Vector2,float>)(p=>Vector2.Distance(p,new Vector2(o.Item1,o.Item2))-8f)))
            .Concat(new[]{(10f,-3f),(21f,-5f),(32f,-6f),(43f,-6f),(54f,-5f),(64f,-2f)}.Select(o=>(System.Func<Vector2,float>)(p=>Vector2.Distance(p,new Vector2(o.Item1,o.Item2))-7f)))
            .Concat(new System.Func<Vector2,float>[]{p=>Oval(p,new Vector2(34,9),35,15)}).ToArray();
        static void Wing(Vector2 p,out bool inside,out float line)
        {
            const float Half=1.1f;int n=WingLayers.Length;var d=new float[n];for(int i=0;i<n;i++)d[i]=WingLayers[i](p);
            inside=d.Any(x=>x<0);line=0;
            for(int i=0;i<n;i++)
            {
                float own=Mathf.Clamp01((Half-Mathf.Abs(d[i]))/.5f+.5f);if(own<=0)continue;
                for(int j=i+1;j<n&&own>0;j++)own*=Mathf.Clamp01((d[j]-Half)/.5f+.5f);
                line=Mathf.Max(line,own);
            }
        }
        // Along the beak: pinkish white at its root, black from the bend to the tip.
        static Texture2D BeakTexture()
        {
            const int W=64,H=4;var tex=new Texture2D(W,H,TextureFormat.RGB24,false,false);
            var pale=new Color(.60f,.50f,.52f);var black=new Color(.05f,.045f,.05f);
            for(int y=0;y<H;y++)for(int x=0;x<W;x++){var c=Color.Lerp(pale,black,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.50f,.58f,(x+.5f)/W)));c.a=1;tex.SetPixel(x,y,c);}
            return SaveTexture(BeakTexPath,tex,false,TextureWrapMode.Clamp);
        }
    }
}
