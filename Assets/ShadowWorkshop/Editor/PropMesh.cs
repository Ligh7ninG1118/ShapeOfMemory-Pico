using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace ShadowWorkshopIntegration.Editor
{
    // Procedural mesh, material and texture helpers shared by the hand-built prop looks (TravelCameraBuilder,
    // SunglassesBuilder). Those props are drawn only; their original simple meshes stay the shadow (shadowProxy /
    // shadowMesh). Triangles are wound from their vertices' normals, so shapes only have to get normals right.
    public sealed class PropMesh
    {
        public const float UvPerMetre=25;   // planar UVs: one texture tile every 4 cm
        public readonly List<Vector3> v=new List<Vector3>(),n=new List<Vector3>();
        public readonly List<Vector2> uv=new List<Vector2>();
        readonly List<int>[] tris;
        public PropMesh(int submeshes){tris=new List<int>[submeshes];for(int i=0;i<submeshes;i++)tris[i]=new List<int>();}

        public int Add(Vector3 p,Vector3 normal,Vector2 t){v.Add(p);n.Add(normal);uv.Add(t);return v.Count-1;}
        // Wound to face along its vertices' normals (Unity: clockwise seen from the front); degenerate ones are dropped.
        public void Tri(int m,int a,int b,int c)
        {
            var face=Vector3.Cross(v[b]-v[a],v[c]-v[a]);if(face.sqrMagnitude<1e-22f)return;
            if(Vector3.Dot(face,n[a]+n[b]+n[c])<0){var s=b;b=c;c=s;}
            tris[m].Add(a);tris[m].Add(b);tris[m].Add(c);
        }
        public void Quad(int m,int a,int b,int c,int d){Tri(m,a,b,c);Tri(m,a,c,d);}

        // Saves in place when the asset exists, so references to it survive a rebuild.
        public Mesh Save(string path,string name)
        {
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(!mesh){mesh=new Mesh();AssetDatabase.CreateAsset(mesh,path);}else mesh.Clear();
            mesh.name=name;mesh.indexFormat=v.Count>65535?UnityEngine.Rendering.IndexFormat.UInt32:UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(v);mesh.SetNormals(n);mesh.SetUVs(0,uv);mesh.subMeshCount=tris.Length;
            for(int i=0;i<tris.Length;i++)mesh.SetTriangles(tris[i],i);
            mesh.RecalculateBounds();mesh.RecalculateTangents();EditorUtility.SetDirty(mesh);AssetDatabase.SaveAssets();
            return mesh;
        }

        // --- Shapes ------------------------------------------------------------------------------------------------
        public struct Pt{public float r,h;public int m;}
        // Profile point for Lathe; m is the submesh of the segment that starts here.
        public static Pt P(float r,float h,int m)=>new Pt{r=r,h=h,m=m};
        static void Frame(Vector3 axis,out Vector3 e1,out Vector3 e2)
        {
            e1=Vector3.Cross(axis,Mathf.Abs(axis.y)<.9f?Vector3.up:Vector3.right).normalized;e2=Vector3.Cross(axis,e1);
        }
        // Surface of revolution: profile (radius, height along axis) swept round the axis. Normals are flat along the
        // profile (crisp ring steps) and smooth around it; walk the profile with the outside on the side (dh,-dr) points to.
        public void Lathe(Vector3 center,Vector3 axis,int sides,params Pt[] profile)=>Lathe(center,axis,sides,new Vector2(UvPerMetre,0),false,profile);
        // Same, with u once round (0..1) and v = h * vMap.x + vMap.y, e.g. to fit a printed label to a height band.
        // mirrorU runs u the other way round, so print reads left to right from outside (needed with an upward axis).
        public void Lathe(Vector3 center,Vector3 axis,int sides,Vector2 vMap,bool mirrorU,params Pt[] profile)
        {
            axis.Normalize();Frame(axis,out var e1,out var e2);
            for(int i=0;i+1<profile.Length;i++)
            {
                var a=profile[i];var c=profile[i+1];var nrh=new Vector2(c.h-a.h,-(c.r-a.r)).normalized;int start=v.Count;
                for(int k=0;k<=sides;k++)
                {
                    float t=k*2*Mathf.PI/sides,u=mirrorU?1-(float)k/sides:(float)k/sides;var radial=e1*Mathf.Cos(t)+e2*Mathf.Sin(t);var nn=radial*nrh.x+axis*nrh.y;
                    Add(center+axis*a.h+radial*a.r,nn,new Vector2(u,a.h*vMap.x+vMap.y));
                    Add(center+axis*c.h+radial*c.r,nn,new Vector2(u,c.h*vMap.x+vMap.y));
                }
                for(int k=0;k<sides;k++){int q=start+k*2;Quad(a.m,q,q+1,q+3,q+2);}
            }
        }
        // A flat shape: a closed outline (x, y; either winding) extruded along z with edges rounded to radius r.
        // half(p) is the half thickness and mid(p) the z of the middle surface at p (tapers, bends); edgeMat(i) picks the
        // submesh of the edge from outline point i to i+1. Caps are ear-clipped. UVs: planar (x, y) on the caps,
        // (length along the outline, z) round the edge, both in uvPerMetre.
        public void Slab(int capMat,Func<int,int> edgeMat,Vector2[] outline,float r,Func<Vector2,float> half,Func<Vector2,float> mid,int arcSteps,float uvPerMetre)
        {
            int count=outline.Length;float area=0;
            for(int i=0;i<count;i++){var a=outline[i];var c=outline[(i+1)%count];area+=a.x*c.y-c.x*a.y;}
            var normal=new Vector2[count];var inner=new Vector2[count];
            for(int i=0;i<count;i++)
            {
                var t=(outline[(i+1)%count]-outline[(i+count-1)%count]).normalized;normal[i]=new Vector2(t.y,-t.x)*(area>0?1:-1);
                inner[i]=outline[i]-normal[i]*r;
            }
            // Edge: per outline point, a quarter round from the top cap down to the wall, the wall, and a quarter round to the bottom cap.
            int ring=2*(arcSteps+1),start=v.Count;float length=0;
            for(int i=0;i<=count;i++)
            {
                int j=i%count;if(i>0)length+=(outline[j]-outline[i-1]).magnitude;
                var p=outline[j];float h=half(p),zc=mid(p),rr=Mathf.Min(r,h);var n3=new Vector3(normal[j].x,normal[j].y,0);
                for(int s=0;s<ring;s++)
                {
                    bool top=s<=arcSteps;float phi=top?90f*(1-(float)s/arcSteps):-90f*(s-arcSteps-1)/arcSteps;phi*=Mathf.Deg2Rad;
                    float zArc=top?zc+h-rr:zc-h+rr;var nn=n3*Mathf.Cos(phi)+Vector3.forward*Mathf.Sin(phi);
                    var pos=new Vector3(inner[j].x,inner[j].y,zArc)+nn*rr;
                    Add(pos,nn,new Vector2(length*uvPerMetre,pos.z*uvPerMetre));
                }
            }
            for(int i=0;i<count;i++)for(int s=0;s+1<ring;s++){int q=start+i*ring+s;Quad(edgeMat(i),q,q+ring,q+ring+1,q+1);}
            // Caps, normals tilted with the surface (taper and bend).
            var tris=Triangulate(inner);const float e=.0005f;
            foreach(var sign in new[]{1f,-1f})
            {
                float Z(Vector2 p)=>mid(p)+sign*half(p);int capStart=v.Count;
                foreach(var p in inner)
                {
                    float zx=(Z(p+Vector2.right*e)-Z(p-Vector2.right*e))/(2*e),zy=(Z(p+Vector2.up*e)-Z(p-Vector2.up*e))/(2*e);
                    Add(new Vector3(p.x,p.y,Z(p)),(new Vector3(-zx,-zy,1)*sign).normalized,p*uvPerMetre);
                }
                for(int k=0;k<tris.Count;k+=3)Tri(capMat,capStart+tris[k],capStart+tris[k+1],capStart+tris[k+2]);
            }
        }
        // Ear clipping for a simple polygon (either winding). Collinear points are clipped as empty ears.
        public static List<int> Triangulate(Vector2[] poly)
        {
            var left=new List<int>();for(int i=0;i<poly.Length;i++)left.Add(i);
            float area=0;for(int i=0;i<poly.Length;i++){var a=poly[i];var b=poly[(i+1)%poly.Length];area+=a.x*b.y-b.x*a.y;}
            float Cross(Vector2 a,Vector2 b,Vector2 c)=>(b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x);
            var result=new List<int>();
            while(left.Count>3)
            {
                bool clipped=false;
                for(int k=0;k<left.Count&&!clipped;k++)
                {
                    int i0=left[(k+left.Count-1)%left.Count],i1=left[k],i2=left[(k+1)%left.Count];var a=poly[i0];var b=poly[i1];var c=poly[i2];
                    if(Cross(a,b,c)*Mathf.Sign(area)<0)continue;   // reflex
                    bool inside=false;
                    foreach(var j in left)
                    {
                        if(j==i0||j==i1||j==i2)continue;var p=poly[j];
                        float d1=Cross(a,b,p),d2=Cross(b,c,p),d3=Cross(c,a,p);
                        if((d1>0&&d2>0&&d3>0)||(d1<0&&d2<0&&d3<0)){inside=true;break;}
                    }
                    if(inside)continue;
                    result.Add(i0);result.Add(i1);result.Add(i2);left.RemoveAt(k);clipped=true;
                }
                if(!clipped){Debug.LogWarning("PropMesh.Triangulate: polygon not simple; cap left open.");break;}
            }
            if(left.Count==3){result.Add(left[0]);result.Add(left[1]);result.Add(left[2]);}
            return result;
        }
        // Ridged band round the axis between heights h0 and h1: V-grooves between rIn and rOut. End caps are optional:
        // when the ridges stand barely proud of what they wrap, the caps are invisible and only cost triangles.
        public void Knurl(int m,Vector3 center,Vector3 axis,float h0,float h1,float rIn,float rOut,int teeth,bool caps=true)
        {
            axis.Normalize();Frame(axis,out var e1,out var e2);int count=teeth*2;
            Vector3 Dir(int k){float t=k*2*Mathf.PI/count;return e1*Mathf.Cos(t)+e2*Mathf.Sin(t);}
            Vector3 At(int k)=>Dir(k)*(k%2==0?rOut:rIn);
            for(int k=0;k<count;k++)
            {
                var p0=At(k);var p1=At(k+1);var nn=Vector3.Cross(axis,p1-p0).normalized;if(Vector3.Dot(nn,p0+p1)<0)nn=-nn;
                Quad(m,Add(center+axis*h0+p0,nn,Vector2.zero),Add(center+axis*h1+p0,nn,Vector2.up),Add(center+axis*h1+p1,nn,Vector2.one),Add(center+axis*h0+p1,nn,Vector2.right));
                if(caps)foreach(var (h,cap) in new[]{(h0,-axis),(h1,axis)})
                    Quad(m,Add(center+axis*h+Dir(k)*rIn*.96f,cap,Vector2.zero),Add(center+axis*h+p0,cap,Vector2.up),Add(center+axis*h+p1,cap,Vector2.one),Add(center+axis*h+Dir(k+1)*rIn*.96f,cap,Vector2.right));
            }
        }
        public void Box(int m,Vector3 min,Vector3 max)
        {
            var c=(min+max)/2;var h=(max-min)/2;
            for(int a=0;a<3;a++)for(int s=-1;s<=1;s+=2)
            {
                int u=(a+1)%3,w=(a+2)%3;var nn=Vector3.zero;nn[a]=s;var q=new int[4];
                for(int i=0;i<4;i++){var p=Vector3.zero;p[a]=s*h[a];p[u]=(i==1||i==2?1:-1)*h[u];p[w]=(i>=2?1:-1)*h[w];q[i]=Add(c+p,nn,new Vector2(p[u],p[w])*UvPerMetre);}
                Quad(m,q[0],q[1],q[2],q[3]);
            }
        }
        // Box with every edge rounded by radius r: each face is a grid whose outer rows sit on the round (seg steps per
        // face, so two faces make a quarter circle), pushed out from the inner box along the normal.
        public void RoundedBox(int m,Vector3 min,Vector3 max,float r,int seg=3)
        {
            var c=(min+max)/2;var h=(max-min)/2;r=Mathf.Min(r,Mathf.Min(h.x,Mathf.Min(h.y,h.z))*.999f);
            float[] Grid(float half)
            {
                var g=new List<float>();
                for(int k=seg;k>=0;k--)g.Add(-half+r-r*Mathf.Tan(Mathf.PI/4*k/seg));
                for(int k=0;k<=seg;k++)g.Add(half-r+r*Mathf.Tan(Mathf.PI/4*k/seg));
                return g.ToArray();
            }
            for(int a=0;a<3;a++)for(int s=-1;s<=1;s+=2)
            {
                int u=(a+1)%3,w=(a+2)%3;var gu=Grid(h[u]);var gw=Grid(h[w]);int start=v.Count;
                for(int i=0;i<gu.Length;i++)for(int j=0;j<gw.Length;j++)
                {
                    var p=Vector3.zero;p[a]=s*h[a];p[u]=gu[i];p[w]=gw[j];
                    var inner=new Vector3(Mathf.Clamp(p.x,-h.x+r,h.x-r),Mathf.Clamp(p.y,-h.y+r,h.y-r),Mathf.Clamp(p.z,-h.z+r,h.z-r));
                    var nn=(p-inner).normalized;Add(c+inner+nn*r,nn,new Vector2(p[u],p[w])*UvPerMetre);
                }
                for(int i=0;i+1<gu.Length;i++)for(int j=0;j+1<gw.Length;j++){int q=start+i*gw.Length+j;Quad(m,q,q+gw.Length,q+gw.Length+1,q+1);}
            }
        }
        // Box with edges bevelled by r and shaded round (the cheapest rounded box: 44 triangles): a flat quad per face, a
        // quad per edge whose two sides take their faces' normals, a triangle per corner taking all three. top=false
        // leaves out the +y face with its edges and corners, for a box whose top is hidden (under a lid).
        public void BevelBox(int m,Vector3 min,Vector3 max,float r,bool top=true)
        {
            var lo=min+Vector3.one*r;var hi=max-Vector3.one*r;
            // The point on face (axis a, side s) nearest the inner box's corner/edge at signs (sx, sy, sz).
            int At(int a,int s,Vector3Int sign)
            {
                var p=Vector3.zero;var nn=Vector3.zero;nn[a]=s;
                for(int i=0;i<3;i++)p[i]=i==a?(s>0?max[i]:min[i]):(sign[i]>0?hi[i]:lo[i]);
                int u=(a+1)%3,w=(a+2)%3;return Add(p,nn,new Vector2(p[u],p[w])*UvPerMetre);
            }
            bool Shown(int a,int s)=>top||a!=1||s<0;
            for(int a=0;a<3;a++)for(int s=-1;s<=1;s+=2)
            {
                if(!Shown(a,s))continue;int u=(a+1)%3,w=(a+2)%3;var q=new int[4];
                for(int i=0;i<4;i++){var sign=Vector3Int.zero;sign[u]=i==1||i==2?1:-1;sign[w]=i>=2?1:-1;q[i]=At(a,s,sign);}
                Quad(m,q[0],q[1],q[2],q[3]);
            }
            // Edges: faces (a, sa) and (b, sb) meet along the third axis c.
            for(int a=0;a<3;a++)for(int b=a+1;b<3;b++)for(int sa=-1;sa<=1;sa+=2)for(int sb=-1;sb<=1;sb+=2)
            {
                if(!Shown(a,sa)||!Shown(b,sb))continue;int c=3-a-b;var lowEnd=Vector3Int.zero;lowEnd[a]=sa;lowEnd[b]=sb;lowEnd[c]=-1;var highEnd=lowEnd;highEnd[c]=1;
                Quad(m,At(a,sa,lowEnd),At(a,sa,highEnd),At(b,sb,highEnd),At(b,sb,lowEnd));
            }
            for(int sx=-1;sx<=1;sx+=2)for(int sy=-1;sy<=1;sy+=2)for(int sz=-1;sz<=1;sz+=2)
            {
                if(!top&&sy>0)continue;var sign=new Vector3Int(sx,sy,sz);
                Tri(m,At(0,sx,sign),At(1,sy,sign),At(2,sz,sign));
            }
        }
        // A closed cross-section (offsets and normals in a side/up frame) carried along a path. Each path point has its
        // own frame and section (same point count), so the section can change along the way. Smooth both ways; u runs
        // along the path (uRepeat tiles end to end; keep it whole on a closed path so the texture meets), v round the section.
        public void Sweep(int m,Vector3[] path,Vector3[] side,Vector3[] up,Func<int,(Vector2[] at,Vector2[] normal)> section,bool closed,float uRepeat)
        {
            int count=path.Length,columns=closed?count+1:count;int start=v.Count,ring=0;
            for(int i=0;i<columns;i++)
            {
                int j=i%count;var (at,normal)=section(j);ring=at.Length+1;
                for(int k=0;k<=at.Length;k++)
                {
                    int s=k%at.Length;
                    Add(path[j]+side[j]*at[s].x+up[j]*at[s].y,(side[j]*normal[s].x+up[j]*normal[s].y).normalized,new Vector2(uRepeat*i/(columns-1f),(float)k/at.Length));
                }
            }
            for(int i=0;i+1<columns;i++)for(int k=0;k+1<ring;k++){int q=start+i*ring+k;Quad(m,q,q+ring,q+ring+1,q+1);}
        }
        // Rounded rectangle as a Sweep section: spans a..b on the side axis and c..d on the up axis, corners of radius r.
        // The loop closes across the -up face, so the texture seam sits there.
        public static (Vector2[] at,Vector2[] normal) RoundedRect(float a,float b,float c,float d,float r,int steps=4)
        {
            var at=new List<Vector2>();var normal=new List<Vector2>();
            var corners=new[]{(new Vector2(b-r,c+r),270f),(new Vector2(b-r,d-r),0f),(new Vector2(a+r,d-r),90f),(new Vector2(a+r,c+r),180f)};
            foreach(var (center,from) in corners)for(int j=0;j<=steps;j++)
            {
                float t=(from+90f*j/steps)*Mathf.Deg2Rad;var dir=new Vector2(Mathf.Cos(t),Mathf.Sin(t));
                at.Add(center+dir*r);normal.Add(dir);
            }
            return (at.ToArray(),normal.ToArray());
        }
        public static (Vector2[] at,Vector2[] normal) Ellipse(float rx,float ry,int steps)
        {
            var at=new Vector2[steps];var normal=new Vector2[steps];
            for(int k=0;k<steps;k++){float t=k*2*Mathf.PI/steps;at[k]=new Vector2(Mathf.Cos(t)*rx,Mathf.Sin(t)*ry);normal[k]=new Vector2(Mathf.Cos(t)/rx,Mathf.Sin(t)/ry).normalized;}
            return (at,normal);
        }

        // Surface of revolution like Lathe, but with normals smooth along the profile as well as round it, so bends
        // shade round on few rows (VR budget). Profile indices in creases stay crisp; points on the axis close it.
        // u runs round, v along the profile.
        public void SmoothLathe(int m,Vector3 center,Vector3 axis,int sides,IList<Vector2> profile,params int[] creases)
        {
            axis.Normalize();Frame(axis,out var e1,out var e2);int count=profile.Count;
            var seg=new Vector2[count-1];for(int i=0;i+1<count;i++){var d=profile[i+1]-profile[i];seg[i]=new Vector2(d.y,-d.x).normalized;}
            int Ring(Vector2 p,Vector2 nrh,float arc)
            {
                bool pole=p.x<1e-6f;if(pole)nrh=new Vector2(0,Mathf.Sign(nrh.y));int start=v.Count;
                for(int k=0;k<=sides;k++)
                {
                    float u=(k+(pole?.5f:0))/sides,t=u*2*Mathf.PI;var radial=e1*Mathf.Cos(t)+e2*Mathf.Sin(t);
                    Add(center+axis*p.y+radial*p.x,(radial*nrh.x+axis*nrh.y).normalized,new Vector2(u,arc*UvPerMetre));
                }
                return start;
            }
            var inRing=new int[count];var outRing=new int[count];float along=0;
            for(int i=0;i<count;i++)
            {
                if(i>0)along+=Vector2.Distance(profile[i-1],profile[i]);
                if(i==0)inRing[i]=outRing[i]=Ring(profile[i],seg[0],along);
                else if(i==count-1)inRing[i]=outRing[i]=Ring(profile[i],seg[i-1],along);
                else if(Array.IndexOf(creases,i)>=0){inRing[i]=Ring(profile[i],seg[i-1],along);outRing[i]=Ring(profile[i],seg[i],along);}
                else inRing[i]=outRing[i]=Ring(profile[i],(seg[i-1]+seg[i]).normalized,along);
            }
            for(int i=0;i+1<count;i++)for(int k=0;k<sides;k++){int a=outRing[i]+k,b=inRing[i+1]+k;Quad(m,a,b,b+1,a+1);}
        }
        // Surface of revolution round the y axis (raised by y0): profile (radius, height) walked with the outside on the
        // side (dh, -dr) points to. Normals are smooth along the profile (each point averages its two segments) unless
        // given; a point repeated is a crease. Points on the axis close the surface and keep their segment's slope, so a
        // cone's tip shades like the cone (SmoothLathe turns them along the axis, which suits flat caps only).
        public void Revolve(int m,int sides,IList<Vector2> profile,IList<Vector2> normals=null,float y0=0)
        {
            int count=profile.Count;
            bool Crease(int i)=>(profile[i+1]-profile[i]).sqrMagnitude<1e-12f;
            var seg=new Vector2[count-1];
            for(int i=0;i+1<count;i++){var d=(profile[i+1]-profile[i])*1000;seg[i]=Crease(i)?Vector2.zero:new Vector2(d.y,-d.x).normalized;}
            Vector2 Normal(int i)
            {
                if(normals!=null)return normals[i];
                bool before=i>0&&!Crease(i-1),after=i+1<count&&!Crease(i);
                return before&&after?(seg[i-1]+seg[i]).normalized:after?seg[i]:seg[i-1];
            }
            var ring=new int[count];
            for(int i=0;i<count;i++)
            {
                var p=profile[i];var nn=Normal(i);bool pole=p.x<1e-6f;ring[i]=v.Count;
                for(int k=0;k<=sides;k++)
                {
                    // A pole's vertex sits half a step round, under the one triangle it closes.
                    float u=(k+(pole?.5f:0))/sides,t=u*2*Mathf.PI;var radial=new Vector3(Mathf.Cos(t),0,Mathf.Sin(t));
                    Add(Vector3.up*(p.y+y0)+radial*Mathf.Max(0,p.x),(radial*nn.x+Vector3.up*nn.y).normalized,new Vector2(u,p.y*UvPerMetre));
                }
            }
            for(int i=0;i+1<count;i++)if(!Crease(i))for(int k=0;k<sides;k++){int a=ring[i]+k,c=ring[i+1]+k;Quad(m,a,c,c+1,a+1);}
        }
        // Revolve profile of a spheroid (radius a, half height c) round height y, pole to pole, with its true normals.
        public static (Vector2[] at,Vector2[] normal) Spheroid(float a,float c,float y,int rings)
        {
            var at=new Vector2[rings+1];var normal=new Vector2[rings+1];
            for(int i=0;i<=rings;i++)
            {
                float p=i*Mathf.PI/rings,s=Mathf.Max(0,Mathf.Sin(p)),co=Mathf.Cos(p);
                at[i]=new Vector2(a*s,y-c*co);normal[i]=new Vector2(s/a,-co/c).normalized;
            }
            return (at,normal);
        }
        // A handle-like bar along a path: section(t) (t 0..1 along the path; the same point count everywhere, unit
        // outward normals) carried in a frame whose side axis stays as near sideHint as the path allows (up = tangent x
        // side). Either end can be closed by a rounded cap that long, the section shrinking like a quarter ellipse to a
        // point; zero leaves the end open, for one buried in something. Smooth all over; u along the path, v round.
        public void Bar(int m,IList<Vector3> path,Vector3 sideHint,Func<float,(Vector2[] at,Vector2[] normal)> section,float startCap,float endCap,int capSteps=2)
        {
            int count=path.Count;var along=new float[count];for(int i=1;i<count;i++)along[i]=along[i-1]+Vector3.Distance(path[i-1],path[i]);
            Vector3 Tangent(int i)=>(path[Mathf.Min(i+1,count-1)]-path[Mathf.Max(i-1,0)]).normalized;
            var rings=new List<(Vector3 at,Vector3 tangent,float t,float scale,float tilt)>();
            if(startCap>0)for(int k=capSteps;k>=1;k--){float a=k*Mathf.PI/2/capSteps;var tg=Tangent(0);rings.Add((path[0]-tg*startCap*Mathf.Sin(a),tg,0,Mathf.Cos(a),-Mathf.Sin(a)));}
            for(int i=0;i<count;i++)rings.Add((path[i],Tangent(i),along[i]/along[count-1],1,0));
            if(endCap>0)for(int k=1;k<=capSteps;k++){float a=k*Mathf.PI/2/capSteps;var tg=Tangent(count-1);rings.Add((path[count-1]+tg*endCap*Mathf.Sin(a),tg,1,Mathf.Cos(a),Mathf.Sin(a)));}
            int start=v.Count,columns=0;
            foreach(var (at,tangent,t,scale,tilt) in rings)
            {
                var side=(sideHint-tangent*Vector3.Dot(sideHint,tangent)).normalized;var up=Vector3.Cross(tangent,side);
                var (points,normals)=section(t);columns=points.Length+1;
                for(int k=0;k<=points.Length;k++)
                {
                    int s=k%points.Length;var nn=(side*normals[s].x+up*normals[s].y)*Mathf.Sqrt(1-tilt*tilt)+tangent*tilt;
                    Add(at+(side*points[s].x+up*points[s].y)*scale,nn.normalized,new Vector2(t,(float)k/points.Length));
                }
            }
            for(int r=0;r+1<rings.Count;r++)for(int k=0;k+1<columns;k++){int q=start+r*columns+k;Quad(m,q,q+columns,q+columns+1,q+1);}
        }
        // A polyline (or closed polygon) with its corners rounded: each point with a radius and the arc's step count
        // (radius 0 keeps the corner). A radius shrinks where its edges are too short for it; open ends stay put.
        public static List<Vector2> RoundCorners(IList<(Vector2 p,float r,int steps)> corners,bool closed)
        {
            int count=corners.Count;var list=new List<Vector2>();
            void Put(Vector2 p){if(list.Count==0||(list[list.Count-1]-p).sqrMagnitude>1e-12f)list.Add(p);}
            for(int i=0;i<count;i++)
            {
                var (p,r,steps)=corners[i];
                if(r<=0||steps<1||(!closed&&(i==0||i==count-1))){Put(p);continue;}
                Vector2 prev=corners[(i+count-1)%count].p,next=corners[(i+1)%count].p,dIn=(p-prev).normalized,dOut=(next-p).normalized;
                float turn=Vector2.Angle(dIn,dOut)*Mathf.Deg2Rad;if(turn<1e-3f){Put(p);continue;}
                float d=r*Mathf.Tan(turn/2),limit=Mathf.Min(Vector2.Distance(prev,p),Vector2.Distance(p,next))*.5f;
                if(d>limit){r*=limit/d;d=limit;}
                Vector2 a=p-dIn*d,b=p+dOut*d;
                var toCenter=dIn.x*dOut.y-dIn.y*dOut.x>0?new Vector2(-dIn.y,dIn.x):new Vector2(dIn.y,-dIn.x);var center=a+toCenter*r;
                float a0=Mathf.Atan2(a.y-center.y,a.x-center.x),a1=Mathf.Atan2(b.y-center.y,b.x-center.x);
                float delta=Mathf.DeltaAngle(a0*Mathf.Rad2Deg,a1*Mathf.Rad2Deg)*Mathf.Deg2Rad;
                for(int k=0;k<=steps;k++){float t=a0+delta*k/steps;Put(center+new Vector2(Mathf.Cos(t),Mathf.Sin(t))*r);}
            }
            if(closed&&list.Count>1&&(list[0]-list[list.Count-1]).sqrMagnitude<=1e-12f)list.RemoveAt(list.Count-1);
            return list;
        }
        // Points along a centripetal Catmull-Rom curve through points, steps per span (its ends carried on straight).
        public static List<Vector3> Curve(IList<Vector3> points,int steps)
        {
            var list=new List<Vector3>();int count=points.Count;
            for(int i=0;i+1<count;i++)
            {
                Vector3 p1=points[i],p2=points[i+1],p0=i>0?points[i-1]:2*p1-p2,p3=i+2<count?points[i+2]:2*p2-p1;
                float t0=0,t1=t0+Mathf.Sqrt(Vector3.Distance(p0,p1)),t2=t1+Mathf.Sqrt(Vector3.Distance(p1,p2)),t3=t2+Mathf.Sqrt(Vector3.Distance(p2,p3));
                for(int k=0;k<steps;k++)
                {
                    float u=Mathf.Lerp(t1,t2,(float)k/steps);
                    var a1=(t1-u)/(t1-t0)*p0+(u-t0)/(t1-t0)*p1;var a2=(t2-u)/(t2-t1)*p1+(u-t1)/(t2-t1)*p2;var a3=(t3-u)/(t3-t2)*p2+(u-t2)/(t3-t2)*p3;
                    var b1=(t2-u)/(t2-t0)*a1+(u-t0)/(t2-t0)*a2;var b2=(t3-u)/(t3-t1)*a2+(u-t1)/(t3-t1)*a3;
                    list.Add((t2-u)/(t2-t1)*b1+(u-t1)/(t2-t1)*b2);
                }
            }
            list.Add(points[count-1]);
            return list;
        }

        // --- Materials and textures ----------------------------------------------------------------------------------
        public sealed class Look
        {
            public string name;public Color color;public float metallic,smoothness,bumpScale=1;
            public Func<Texture2D> baseMap,normalMap;
            public Look(string name,Color color,float metallic,float smoothness){this.name=name;this.color=color;this.metallic=metallic;this.smoothness=smoothness;}
        }
        // One material per look, in submesh order. Only created when missing, so tweaks made in Unity survive a rebuild.
        public static Material[] Materials(string folder,Look[] looks)
        {
            var shader=AssetDatabase.LoadAssetAtPath<Material>("Assets/ShadowWorkshop/Materials/Graybox surfaces.mat").shader;var list=new Material[looks.Length];
            for(int i=0;i<looks.Length;i++)
            {
                var look=looks[i];string path=folder+look.name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(!m)
                {
                    m=new Material(shader);m.SetColor("_BaseColor",look.color);m.SetFloat("_Metallic",look.metallic);m.SetFloat("_Smoothness",look.smoothness);
                    if(look.baseMap!=null)m.SetTexture("_BaseMap",look.baseMap());
                    if(look.normalMap!=null){m.SetTexture("_BumpMap",look.normalMap());m.SetFloat("_BumpScale",look.bumpScale);m.EnableKeyword("_NORMALMAP");}
                    AssetDatabase.CreateAsset(m,path);
                }
                list[i]=m;
            }
            AssetDatabase.SaveAssets();return list;
        }
        public static Texture2D SaveTexture(string path,Texture2D tex,bool normalMap,TextureWrapMode wrap=TextureWrapMode.Repeat)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllBytes(path,tex.EncodeToPNG());Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=normalMap?TextureImporterType.NormalMap:TextureImporterType.Default;importer.wrapMode=wrap;importer.anisoLevel=4;importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        // Tileable value noise: the lattice wraps every period cells, so sampling u,v in 0..1 tiles seamlessly.
        public static float Fbm(float u,float v,int periodU,int periodV,int octaves,int seed)
        {
            float sum=0,amp=.5f,norm=0;
            for(int o=0;o<octaves;o++){sum+=amp*Noise(u*periodU,v*periodV,periodU,periodV,seed+o*31);norm+=amp;amp*=.5f;periodU*=2;periodV*=2;}
            return sum/norm;
        }
        static float Noise(float x,float y,int px,int py,int seed)
        {
            int x0=Mathf.FloorToInt(x),y0=Mathf.FloorToInt(y);float fx=x-x0,fy=y-y0,sx=fx*fx*(3-2*fx),sy=fy*fy*(3-2*fy);
            float H(int ix,int iy)
            {
                ix=(ix%px+px)%px;iy=(iy%py+py)%py;
                unchecked{int h=ix*374761393+iy*668265263+seed*144665;h=(h^(h>>13))*1274126177;return ((h^(h>>16))&0xffff)/65535f;}
            }
            return Mathf.Lerp(Mathf.Lerp(H(x0,y0),H(x0+1,y0),sx),Mathf.Lerp(H(x0,y0+1),H(x0+1,y0+1),sx),sy);
        }
    }
}
