using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static ShadowWorkshopIntegration.Editor.PropCanvas;
using static ShadowWorkshopIntegration.Editor.PropMesh;

namespace ShadowWorkshopIntegration.Editor
{
    // Detailed look for the Torch2 'Luggage tag' prop: a leather tag with rounded edges and saddle stitching, a nickel
    // eyelet round a real hole, an address card behind a window on the front, and a cord knotted behind the eyelet.
    // Drawn only: the original flat 'Luggage tag' mesh stays the piece's shadow, so the puzzle's picture and scoring are
    // unchanged. Same frame and outline as the original: a 52 x 91 mm pentagon pointing up, 4 mm thick, front facing -Z,
    // the cord rising from the hole to a bend on the right and on up to the left.
    // Kept light for mobile VR (about 600 triangles): the stitching is painted, not modelled. Both leather faces carry
    // it in the card's texture (one atlas: card, front, back), so they use the card's material; the rounded edge and the
    // window's cut edge keep the plain leather material. Same four materials in the same order as before.
    public static class LuggageTagBuilder
    {
        public const string MeshPath="Assets/ShadowWorkshop/Art/Torch2/Meshes/Luggage tag detailed.asset";
        public const string ShadowPath="Assets/ShadowWorkshop/Art/Torch2/Meshes/Luggage tag.asset";
        const string MaterialFolder="Assets/ShadowWorkshop/Art/Torch2/Materials/";
        const string CardPath="Assets/ShadowWorkshop/Art/Torch2/Textures/luggage tag card.png";
        // Submeshes, in the order of Materials(): the original tag and string materials first, so the colours stay.
        const int Leather=0,Cord=1,Metal=2,Card=3;

        // The original pentagon, counter-clockwise from bottom left, and how round each corner becomes.
        static readonly Vector2[] Corners={new Vector2(-.026f,-.06072f),new Vector2(.026f,-.06072f),new Vector2(.026f,.01428f),new Vector2(0,.03028f),new Vector2(-.026f,.01428f)};
        static readonly float[] CornerRadius={.0045f,.0045f,.0040f,.0035f,.0040f};
        const float Half=.002f,Edge=.0012f;                            // half thickness, edge round
        const float ArcStep=45f,SplitStep=.010f;                       // mesh outline: a point every 45 degrees round the corners, none along the straight edges
        static readonly Vector2 Eyelet=new Vector2(0,.0200f);
        const float BarrelR=.0023f,HoleR=.0028f,FlangeR=.0046f;const int EyeletSides=10;
        static readonly Vector2 WindowMin=new Vector2(-.0185f,-.0512f),WindowMax=new Vector2(.0185f,.0080f);
        const float WindowR=.0035f,WindowDepth=.0006f,Split=.0110f;    // the faces are filled in two parts, above and below Split (on the straight sides)
        const float StitchInset=.0033f,WindowStitch=.0017f;
        const float StitchPitch=.0024f,StitchLength=.0015f,StitchWidth=.00055f,StitchSlant=15f;
        const float CordR=.0012f,CordStep=.004f;const int CordSides=6;
        // Cord centre line: from inside the knot behind the eyelet, through the hole, up the front to the bend, on up to
        // the top knot (the original string ran (1.6, 23.5) -> (11.7, 44.8) -> (-8.9, 60.7) mm).
        static readonly Vector3[] CordPoints={
            new Vector3(0,.0200f,.0047f),new Vector3(0,.0200f,.0012f),new Vector3(0,.0200f,-.0024f),new Vector3(.0004f,.0216f,-.0044f),
            new Vector3(.0020f,.0262f,-.0048f),new Vector3(.0052f,.0345f,-.0034f),new Vector3(.0092f,.0415f,-.0014f),new Vector3(.0112f,.0450f,-.0004f),
            new Vector3(.0082f,.0490f,.0002f),new Vector3(.0022f,.0543f,.0004f),new Vector3(-.0048f,.0586f,.0002f),new Vector3(-.0082f,.0602f,0)};

        // Atlas, 1024 x 512 px: the card's 320 x 512 px print on the left, then the front and the back faces at 5 px/mm
        // (260 x 455 px each, the outline's bounding box from its bottom-left corner), leather with the stitching on.
        const int AtlasW=1024,AtlasH=512,CardW=320,FrontX=336,BackX=612,FaceY=16;const float FacePx=5000;

        // The atlas is rewritten in place (same GUID) on every build, so editing Atlas takes effect.
        [MenuItem("MetaHackthone/Shadow Workshop/Build luggage tag")]
        public static void Build()
        {
            Atlas();var mesh=BuildMesh();var materials=Materials();
            var counts=new List<string>();for(int i=0;i<mesh.subMeshCount;i++)counts.Add((mesh.GetIndexCount(i)/3).ToString());
            Debug.Log($"LUGGAGE_TAG_OK verts={mesh.vertexCount} tris={string.Join("+",counts)} materials={materials.Length}");
        }
        public static Material[] Materials()
        {
            Material Existing(string name)=>AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder+name+".mat");
            var leather=Existing("tag");
            var card=PropMesh.Materials(MaterialFolder,new[]{new Look("tag card",Color.white,0f,.25f){baseMap=()=>AssetDatabase.LoadAssetAtPath<Texture2D>(CardPath)??Atlas()}})[0];
            // The faces are on the card's material, so it shines like the leather round them.
            if(leather&&!Mathf.Approximately(card.GetFloat("_Smoothness"),leather.GetFloat("_Smoothness")))
            {
                card.SetFloat("_Smoothness",leather.GetFloat("_Smoothness"));EditorUtility.SetDirty(card);AssetDatabase.SaveAssetIfDirty(card);
            }
            return new[]{leather,Existing("string"),Existing("camera chrome"),card};
        }

        public static Mesh BuildMesh()
        {
            var b=new PropMesh(4);
            var outline=Outline(0,ArcStep,1f,Split);
            Rim(b,outline);

            // Faces, Edge in from the outline, split at Split: above it a ring round the hole on both faces; below it a fan
            // on the back and a ring round the window on the front.
            var face=new List<Vector2>();foreach(var (p,n) in outline)face.Add(p-n*Edge);
            var hole=Circle(Eyelet,HoleR,EyeletSides);
            int right=face.FindIndex(p=>Mathf.Abs(p.y-Split)<1e-6f&&p.x>0),left=face.FindIndex(p=>Mathf.Abs(p.y-Split)<1e-6f&&p.x<0);
            var upper=Run(face,right,left);var lower=Run(face,left,right);
            float splitX=face[right].x;int splitSteps=Mathf.CeilToInt(2*splitX/SplitStep);
            for(int k=1;k<splitSteps;k++){float t=(float)k/splitSteps;upper.Add(new Vector2(Mathf.Lerp(-splitX,splitX,t),Split));lower.Add(new Vector2(Mathf.Lerp(splitX,-splitX,t),Split));}
            Vector2 FrontUv(Vector2 p)=>FaceUv(p,FrontX);Vector2 BackUv(Vector2 p)=>FaceUv(p,BackX);
            var lowerCenter=new Vector2(0,(Split+Corners[0].y)/2);
            Ring(b,Card,Eyelet,upper,hole,Half,Vector3.forward,BackUv);
            Ring(b,Card,lowerCenter,lower,new List<Vector2>{lowerCenter},Half,Vector3.forward,BackUv);
            var window=RoundRect(WindowMin,WindowMax,WindowR,2,1f);var windowCenter=(WindowMin+WindowMax)/2;
            Ring(b,Card,Eyelet,upper,hole,-Half,Vector3.back,FrontUv);
            Ring(b,Card,windowCenter,lower,window.ConvertAll(e=>e.p),-Half,Vector3.back,FrontUv);

            // Window: the cut edge of the leather, then the address card at the bottom of the recess.
            float floor=-Half+WindowDepth;int count=window.Count,start=b.v.Count;
            for(int k=0;k<=count;k++)
            {
                var (p,n)=window[k%count];var inward=new Vector3(-n.x,-n.y,0);
                b.Add(new Vector3(p.x,p.y,-Half),inward,Vector2.zero);b.Add(new Vector3(p.x,p.y,floor),inward,Vector2.zero);
            }
            for(int k=0;k<count;k++){int q=start+k*2;b.Quad(Leather,q,q+1,q+3,q+2);}
            var size=WindowMax-WindowMin;Vector2 CardUv(Vector2 p)=>new Vector2((p.x-WindowMin.x)/size.x*CardW/AtlasW,(p.y-WindowMin.y)/size.y);
            int hub=b.Add(new Vector3(windowCenter.x,windowCenter.y,floor),Vector3.back,CardUv(windowCenter));start=b.v.Count;
            foreach(var (p,_) in window)b.Add(new Vector3(p.x,p.y,floor),Vector3.back,CardUv(p));
            for(int k=0;k<count;k++)b.Tri(Card,hub,start+k,start+(k+1)%count);

            // Eyelet: a barrel through the hole, rolled over into a flange on each face.
            b.Lathe(new Vector3(Eyelet.x,Eyelet.y,0),Vector3.forward,EyeletSides,
                P(FlangeR,Half,Metal),P(BarrelR,Half+.0009f,Metal),P(BarrelR,-Half-.0009f,Metal),P(FlangeR,-Half,Metal));

            CordAndKnots(b);
            return b.Save(MeshPath,"Luggage tag detailed");
        }
        // Where p (tag x, y) falls on a face's part of the atlas, in pixels and as a UV.
        static Vector2 FacePixel(Vector2 p,int x0)=>new Vector2(x0+(p.x-Corners[0].x)*FacePx,FaceY+(p.y-Corners[0].y)*FacePx);
        static Vector2 FaceUv(Vector2 p,int x0){var q=FacePixel(p,x0);return new Vector2(q.x/AtlasW,q.y/AtlasH);}

        // --- Leather ---------------------------------------------------------------------------------------------------
        // The outline pulled in by inset (corners stay concentric, at least 0.4 mm round), counter-clockwise, as points
        // with outward normals: corner arcs every arcStep degrees, straight edges every step and exactly where they cross a splitY.
        static List<(Vector2 p,Vector2 n)> Outline(float inset,float arcStep,float step,params float[] splitY)
        {
            int count=Corners.Length;var arcs=new (Vector2 c,float r,float a0,float a1)[count];
            for(int i=0;i<count;i++)
            {
                Vector2 at=Corners[i],dIn=(at-Corners[(i+count-1)%count]).normalized,dOut=(Corners[(i+1)%count]-at).normalized;
                Vector2 nIn=new Vector2(dIn.y,-dIn.x),nOut=new Vector2(dOut.y,-dOut.x);
                float halfInterior=Vector2.Angle(-dIn,dOut)*.5f*Mathf.Deg2Rad,r=Mathf.Max(CornerRadius[i]-inset,.0004f);
                var c=at-(nIn+nOut).normalized*((inset+r)/Mathf.Sin(halfInterior));
                float a0=Mathf.Atan2(nIn.y,nIn.x),a1=Mathf.Atan2(nOut.y,nOut.x);if(a1<a0)a1+=2*Mathf.PI;
                arcs[i]=(c,r,a0,a1);
            }
            var list=new List<(Vector2,Vector2)>();
            for(int i=0;i<count;i++)
            {
                var (c,r,a0,a1)=arcs[i];int steps=Mathf.Max(2,Mathf.CeilToInt((a1-a0)/(arcStep*Mathf.Deg2Rad)-.001f));
                for(int k=0;k<=steps;k++){float a=Mathf.Lerp(a0,a1,(float)k/steps);var n=new Vector2(Mathf.Cos(a),Mathf.Sin(a));list.Add((c+n*r,n));}
                var next=arcs[(i+1)%count];var normal=new Vector2(Mathf.Cos(a1),Mathf.Sin(a1));
                var from=c+normal*r;var to=next.c+new Vector2(Mathf.Cos(next.a0),Mathf.Sin(next.a0))*next.r;float length=Vector2.Distance(from,to);
                var ts=new List<float>();int m=Mathf.CeilToInt(length/step);for(int k=1;k<m;k++)ts.Add((float)k/m);
                foreach(var y in splitY){float t=(y-from.y)/(to.y-from.y);if(t>0&&t<1){ts.RemoveAll(u=>Mathf.Abs(u-t)*length<step*.3f);ts.Add(t);}}
                ts.Sort();
                foreach(var t in ts){var p=Vector2.Lerp(from,to,t);foreach(var y in splitY)if(Mathf.Abs(p.y-y)<1e-6f)p.y=y;list.Add((p,normal));}
            }
            return list;
        }
        // Rounded edge all round: a quarter round from each face (Edge in from the outline) out to a flat band on it. One
        // step a quarter, so it is a bevel whose normals turn from the face to the side and shade it round.
        static void Rim(PropMesh b,List<(Vector2 p,Vector2 n)> outline)
        {
            const int steps=1;var profile=new List<(float o,float z,float c,float s)>();   // outward offset, z, normal (outward, z)
            for(int k=0;k<=steps;k++){float a=Mathf.Lerp(-90,0,(float)k/steps)*Mathf.Deg2Rad;profile.Add((Edge*(Mathf.Cos(a)-1),-(Half-Edge)+Edge*Mathf.Sin(a),Mathf.Cos(a),Mathf.Sin(a)));}
            for(int k=0;k<=steps;k++){float a=Mathf.Lerp(0,90,(float)k/steps)*Mathf.Deg2Rad;profile.Add((Edge*(Mathf.Cos(a)-1),(Half-Edge)+Edge*Mathf.Sin(a),Mathf.Cos(a),Mathf.Sin(a)));}
            int count=outline.Count,rows=profile.Count,start=b.v.Count;float along=0;
            for(int i=0;i<=count;i++)
            {
                var (p,n)=outline[i%count];if(i>0)along+=Vector2.Distance(p,outline[i-1].p);
                foreach(var (o,z,c,s) in profile)b.Add(new Vector3(p.x+n.x*o,p.y+n.y*o,z),new Vector3(n.x*c,n.y*c,s),new Vector2(along,z)*UvPerMetre);
            }
            for(int i=0;i<count;i++)for(int j=0;j+1<rows;j++){int q=start+i*rows+j;b.Quad(Leather,q,q+rows,q+rows+1,q+1);}
        }
        // A flat face at z between an outer and an inner loop, both counter-clockwise and star-shaped round center,
        // zipped together in order of angle, except that a step whose triangle would turn the wrong way (over the hole, as
        // where a sparse outer edge meets a rounded corner of the hole) takes the other loop's next point instead. An
        // inner loop of one point makes a fan.
        static void Ring(PropMesh b,int m,Vector2 center,List<Vector2> outer,List<Vector2> inner,float z,Vector3 normal,Func<Vector2,Vector2> uv)
        {
            float Angle(Vector2 p){var d=p-center;float a=Mathf.Atan2(d.y,d.x);return a<0?a+2*Mathf.PI:a;}
            (int[] index,Vector2[] at,float[] angle) Loop(List<Vector2> loop)
            {
                int first=0;for(int i=1;i<loop.Count;i++)if(Angle(loop[i])<Angle(loop[first]))first=i;
                var index=new int[loop.Count+1];var at=new Vector2[loop.Count+1];var angle=new float[loop.Count+1];
                for(int k=0;k<=loop.Count;k++)
                {
                    var p=loop[(first+k)%loop.Count];at[k]=p;angle[k]=Angle(p)+(k==loop.Count?2*Mathf.PI:0);
                    index[k]=k==loop.Count?index[0]:b.Add(new Vector3(p.x,p.y,z),normal,uv(p));
                }
                return (index,at,angle);
            }
            float Turn(Vector2 a,Vector2 c,Vector2 d)=>(c.x-a.x)*(d.y-a.y)-(c.y-a.y)*(d.x-a.x);
            var (outerIndex,oa,outerAngle)=Loop(outer);var (innerIndex,ia,innerAngle)=Loop(inner);
            int no=outer.Count,ni=inner.Count,po=0,pi=0;
            while(po<no||pi<ni)
            {
                bool next;   // take the next outer point
                if(pi>=ni)next=true;else if(po>=no)next=false;
                else
                {
                    bool outerOk=Turn(oa[po],oa[po+1],ia[pi])>0,innerOk=Turn(oa[po],ia[pi+1],ia[pi])>0;
                    next=outerAngle[po+1]<=innerAngle[pi+1]?outerOk||!innerOk:outerOk&&!innerOk;
                }
                if(next){b.Tri(m,outerIndex[po],outerIndex[po+1],innerIndex[pi]);po++;}
                else{b.Tri(m,outerIndex[po],innerIndex[pi+1],innerIndex[pi]);pi++;}
            }
        }
        // The loop's points from index from to index to (inclusive), going round.
        static List<Vector2> Run(List<Vector2> loop,int from,int to)
        {
            var run=new List<Vector2>();for(int k=from;;k=(k+1)%loop.Count){run.Add(loop[k]);if(k==to)break;}
            return run;
        }
        static List<Vector2> Circle(Vector2 center,float r,int steps)
        {
            var list=new List<Vector2>();for(int k=0;k<steps;k++){float a=k*2*Mathf.PI/steps;list.Add(center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*r);}
            return list;
        }
        // Rounded rectangle, counter-clockwise from the bottom-right corner, with outward normals: steps per corner,
        // straight edges every step.
        static List<(Vector2 p,Vector2 n)> RoundRect(Vector2 min,Vector2 max,float r,int steps,float step)
        {
            var list=new List<(Vector2,Vector2)>();
            var centers=new[]{new Vector2(max.x-r,min.y+r),new Vector2(max.x-r,max.y-r),new Vector2(min.x+r,max.y-r),new Vector2(min.x+r,min.y+r)};
            for(int i=0;i<4;i++)
            {
                float a0=(i-1)*Mathf.PI/2;
                for(int k=0;k<=steps;k++){float a=a0+Mathf.PI/2*k/steps;var n=new Vector2(Mathf.Cos(a),Mathf.Sin(a));list.Add((centers[i]+n*r,n));}
                var normal=new Vector2(Mathf.Cos(a0+Mathf.PI/2),Mathf.Sin(a0+Mathf.PI/2));var from=centers[i]+normal*r;var to=centers[(i+1)%4]+normal*r;
                int m=Mathf.CeilToInt(Vector2.Distance(from,to)/step);for(int k=1;k<m;k++)list.Add((Vector2.Lerp(from,to,(float)k/m),normal));
            }
            return list;
        }

        // --- Stitching -------------------------------------------------------------------------------------------------
        // Evenly spaced slanted stitches along a closed loop (tag x, y): the segment each stitch covers.
        static List<(Vector2 a,Vector2 b)> Stitches(List<(Vector2 p,Vector2 n)> loop)
        {
            int count=loop.Count;var along=new float[count+1];var list=new List<(Vector2,Vector2)>();
            for(int i=1;i<=count;i++)along[i]=along[i-1]+Vector2.Distance(loop[i-1].p,loop[i%count].p);
            int stitches=Mathf.RoundToInt(along[count]/StitchPitch),seg=0;float slant=StitchSlant*Mathf.Deg2Rad;
            for(int s=0;s<stitches;s++)
            {
                float d=(s+.5f)*along[count]/stitches;while(seg<count-1&&along[seg+1]<d)seg++;
                Vector2 a=loop[seg].p,c=loop[(seg+1)%count].p,p=Vector2.Lerp(a,c,(d-along[seg])/(along[seg+1]-along[seg])),t=(c-a).normalized;
                var dir=new Vector2(t.x*Mathf.Cos(slant)-t.y*Mathf.Sin(slant),t.x*Mathf.Sin(slant)+t.y*Mathf.Cos(slant));
                list.Add((p-dir*(StitchLength*.5f),p+dir*(StitchLength*.5f)));
            }
            return list;
        }

        // --- Cord ------------------------------------------------------------------------------------------------------
        // A round cord along a smooth curve through CordPoints, its ends buried in overhand knots.
        static void CordAndKnots(PropMesh b)
        {
            var path=new List<Vector3>();
            for(int i=0;i+1<CordPoints.Length;i++)
            {
                Vector3 p1=CordPoints[i],p2=CordPoints[i+1];
                Vector3 p0=i>0?CordPoints[i-1]:2*p1-p2,p3=i+2<CordPoints.Length?CordPoints[i+2]:2*p2-p1;
                int steps=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(p1,p2)/CordStep-.001f));
                for(int k=0;k<steps;k++)path.Add(CatmullRom(p0,p1,p2,p3,(float)k/steps));
            }
            path.Add(CordPoints[CordPoints.Length-1]);
            int count=path.Count;var side=new Vector3[count];var up=new Vector3[count];
            for(int i=0;i<count;i++)
            {
                var tangent=(path[Mathf.Min(i+1,count-1)]-path[Mathf.Max(i-1,0)]).normalized;
                var prev=i>0?up[i-1]:Vector3.Cross(tangent,Vector3.right);   // carried along so the cord does not spin
                up[i]=(prev-tangent*Vector3.Dot(prev,tangent)).normalized;side[i]=Vector3.Cross(up[i],tangent);
            }
            var section=Ellipse(CordR,CordR,CordSides);
            b.Sweep(Cord,path.ToArray(),side,up,j=>section,false,1);
            Knot(b,path[0],path[0]-path[1]);
            Knot(b,path[count-1],path[count-1]-path[count-2]);
        }
        // Centripetal Catmull-Rom between p1 and p2.
        static Vector3 CatmullRom(Vector3 p0,Vector3 p1,Vector3 p2,Vector3 p3,float t)
        {
            float t0=0,t1=t0+Mathf.Sqrt(Vector3.Distance(p0,p1)),t2=t1+Mathf.Sqrt(Vector3.Distance(p1,p2)),t3=t2+Mathf.Sqrt(Vector3.Distance(p2,p3));
            float u=Mathf.Lerp(t1,t2,t);
            var a1=(t1-u)/(t1-t0)*p0+(u-t0)/(t1-t0)*p1;var a2=(t2-u)/(t2-t1)*p1+(u-t1)/(t2-t1)*p2;var a3=(t3-u)/(t3-t2)*p2+(u-t2)/(t3-t2)*p3;
            var b1=(t2-u)/(t2-t0)*a1+(u-t0)/(t2-t0)*a2;var b2=(t3-u)/(t3-t1)*a2+(u-t1)/(t3-t1)*a3;
            return (t2-u)/(t2-t1)*b1+(u-t1)/(t2-t1)*b2;
        }
        // An overhand knot's lump: two strands crossing round the cord's end, each a long ellipsoid.
        static void Knot(PropMesh b,Vector3 at,Vector3 axis)
        {
            axis.Normalize();var e1=Vector3.Cross(axis,Mathf.Abs(axis.y)<.9f?Vector3.up:Vector3.right).normalized;var e2=Vector3.Cross(axis,e1);
            for(int i=0;i<2;i++)
            {
                float a=i*Mathf.PI+.4f;var radial=e1*Mathf.Cos(a)+e2*Mathf.Sin(a);
                var main=(Vector3.Cross(axis,radial)+axis*.55f).normalized;var other=Vector3.Cross(main,radial).normalized;
                Ellipsoid(b,Cord,at+radial*.0009f,radial*.0012f,other*.0012f,main*.0021f);
            }
        }
        // Ellipsoid with orthogonal semi-axes x, y, z, its poles on z: lat - 1 rings of lon points and a fan at each pole;
        // smooth normals.
        static void Ellipsoid(PropMesh b,int m,Vector3 c,Vector3 x,Vector3 y,Vector3 z,int lat=3,int lon=5)
        {
            int south=b.Add(c-z,-z.normalized,new Vector2(.5f,0)),start=b.v.Count;
            for(int i=1;i<lat;i++)for(int k=0;k<lon;k++)
            {
                float phi=Mathf.PI*(i/(float)lat-.5f),th=2*Mathf.PI*k/lon,cp=Mathf.Cos(phi),sp=Mathf.Sin(phi),ct=Mathf.Cos(th),st=Mathf.Sin(th);
                var n=(x/x.sqrMagnitude*cp*ct+y/y.sqrMagnitude*cp*st+z/z.sqrMagnitude*sp).normalized;
                b.Add(c+x*cp*ct+y*cp*st+z*sp,n,new Vector2((float)k/lon,(float)i/lat));
            }
            int north=b.Add(c+z,z.normalized,new Vector2(.5f,1)),top=start+(lat-2)*lon;
            for(int k=0;k<lon;k++)
            {
                int k1=(k+1)%lon;
                b.Tri(m,south,start+k,start+k1);b.Tri(m,north,top+k,top+k1);
                for(int i=0;i+2<lat;i++){int q=start+i*lon;b.Quad(m,q+k,q+lon+k,q+lon+k1,q+k1);}
            }
        }

        // --- Atlas -----------------------------------------------------------------------------------------------------
        // The card: 320 x 512 px over the 37 x 59 mm window (u to the right seen from the front, v up): a blue BON VOYAGE
        // header with a plane, NAME / ADDRESS / PHONE rows filled in by hand in blue ink, and a barcode. Then the two
        // faces in the tag material's leather colour, stitched in the string's colour round the edge (and the window).
        static Texture2D Atlas()
        {
            const int w=AtlasW,h=AtlasH;var px=new Color[w*h];
            Color paper=new Color(.96f,.94f,.87f),header=new Color(.10f,.24f,.50f),label=new Color(.28f,.30f,.35f),rule=new Color(.58f,.60f,.64f),
                ink=new Color(.12f,.20f,.58f),bars=new Color(.13f,.14f,.18f);
            Color MaterialColor(string name,Color fallback){var mat=AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder+name+".mat");return mat?mat.GetColor("_BaseColor"):fallback;}
            Color leather=MaterialColor("tag",new Color(.9f,.7f,.3f)),thread=MaterialColor("string",new Color(.9f,.9f,.9f));
            var groove=Color.Lerp(leather,Color.black,.3f);leather.a=thread.a=groove.a=1;
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)px[y*w+x]=x<CardW+8?paper:leather;

            Fill(px,w,p=>RoundRectDistance(p,new Vector2(160,438),new Vector2(140,50),12)<0,header,18,386,302,490);
            var plane=new Vector2(66,438);float tilt=-20*Mathf.Deg2Rad;
            Fill(px,w,p=>
            {
                var d=p-plane;var q=new Vector2(d.x*Mathf.Cos(tilt)-d.y*Mathf.Sin(tilt),d.x*Mathf.Sin(tilt)+d.y*Mathf.Cos(tilt))/60f;
                if(SegmentDistance(q,new Vector2(-.46f,0),new Vector2(.40f,0))<.07f)return true;
                q.y=Mathf.Abs(q.y);
                return Convex(q,new Vector2(.10f,0),new Vector2(-.14f,.48f),new Vector2(-.24f,.48f),new Vector2(-.10f,0))||
                       Convex(q,new Vector2(-.30f,0),new Vector2(-.44f,.20f),new Vector2(-.50f,.20f),new Vector2(-.42f,0));
            },paper,26,398,106,478);
            Text(px,w,"BON",124,448,4,paper);Text(px,w,"VOYAGE",124,402,4,paper);

            foreach(var (word,y) in new[]{("NAME",340),("ADDRESS",290),("PHONE",190)})Text(px,w,word,26,y,2,label);
            foreach(var y in new[]{332,282,237,182})Stroke(px,w,h,new List<Vector2>{new Vector2(26,y),new Vector2(294,y)},1.4f,rule);
            foreach(var (x0,x1,y,seed) in new[]{(86f,252f,336f,3),(116f,284f,286f,5),(30f,226f,241f,8),(96f,240f,186f,13)})
                foreach(var stroke in Scribble(x0,x1,y,seed))Stroke(px,w,h,stroke,1.7f,ink);

            var rnd=new System.Random(11);bool bar=true;
            for(int x=40;x<280;bar=!bar)
            {
                int width=2*(1+rnd.Next(3));
                if(bar)for(int y=58;y<122;y++)for(int k=x;k<Mathf.Min(x+width,280);k++)px[y*w+k]=bars;
                x+=width;
            }

            // Stitching: each stitch's hole a shade darker under the thread.
            void Seam(List<(Vector2 p,Vector2 n)> loop,int x0)
            {
                var segments=new List<(Vector2,Vector2)>();foreach(var (a,c) in Stitches(loop))segments.Add((FacePixel(a,x0),FacePixel(c,x0)));
                Segments(px,w,h,segments,(StitchWidth+.0003f)*FacePx,groove);Segments(px,w,h,segments,StitchWidth*FacePx,thread);
            }
            var seam=Outline(StitchInset,10f,.002f);Seam(seam,FrontX);Seam(seam,BackX);
            var margin=Vector2.one*WindowStitch;Seam(RoundRect(WindowMin-margin,WindowMax+margin,WindowR+WindowStitch,6,.002f),FrontX);

            var tex=new Texture2D(w,h,TextureFormat.RGB24,false,false);tex.SetPixels(px);tex.Apply();
            SaveTexture(CardPath,tex,false,TextureWrapMode.Clamp);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(CardPath);
        }
        // Joined-up handwriting along a line: loops of a trochoid, letter heights varying, pen lifted between words.
        static List<List<Vector2>> Scribble(float x0,float x1,float baseline,int seed)
        {
            var rnd=new System.Random(seed);var strokes=new List<List<Vector2>>();const float advance=1.45f,loop=2.1f,slant=.28f;
            float x=x0;
            while(x<x1-12)
            {
                int letters=3+rnd.Next(5);var stroke=new List<Vector2>();var heights=new float[letters];
                for(int k=0;k<letters;k++)heights[k]=rnd.NextDouble()<.25?11:5.5f+(float)rnd.NextDouble()*2;
                for(float t=0;t<=letters*2*Mathf.PI;t+=.15f)
                {
                    float hgt=heights[Mathf.Min(letters-1,(int)(t/(2*Mathf.PI)))],y=hgt*(1-Mathf.Cos(t))*.5f;
                    var p=new Vector2(x+advance*t+loop*Mathf.Sin(t)+slant*y,baseline+y);if(p.x>x1)break;stroke.Add(p);
                }
                strokes.Add(stroke);x+=advance*letters*2*Mathf.PI+7;
            }
            return strokes;
        }
    }
}
