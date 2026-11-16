using System.Collections.Generic;
using System.Linq;
using CloudGarden.ShadowPuzzle;
using UnityEditor;
using UnityEngine;

namespace ShadowWorkshopIntegration.Editor
{
    // The CozyShops medium jar, smashed where other pieces pass through it (Home's frisbee): the jar's glass shell
    // rebuilt 24-sided in its own frame, with a jagged hole cut wherever its wall comes within Clearance (plus up to Jag
    // more, so the edge breaks into shards) of those pieces, and a 2 mm band down the broken edge so it reads as glass.
    // The hole is cut for the pile as placed and, where the gym gathers its pile in the game (Torch2Gym.drawGather),
    // as drawn there too. Drawn only: the whole jar stays the piece's shadow (ShadowPiece.shadowProxy in the gym,
    // Torch2Piece.shadowMesh in levels), so the puzzle is unchanged. About 750 triangles, the jar's own glass material.
    public static class BrokenJarBuilder
    {
        public const string MeshPath="Assets/ShadowWorkshop/Art/Torch2/Meshes/Broken jar.asset";
        const int Sides=24;
        const float Clearance=.006f,Jag=.007f,Thick=.002f;   // metres in the orb
        const int Cracks=7;
        // The jar's outside, in its mesh units (mm / 1000): the base out to its rounded foot, up the wall, over the
        // shoulder to the neck the lid sits on.
        static readonly Vector2[] Base={new Vector2(0,0),new Vector2(.0415f,0)};
        static readonly Vector2[] Side=MakeSide();
        static Vector2[] MakeSide()
        {
            var s=new List<Vector2>{new Vector2(.0415f,0),new Vector2(.0450f,.0012f),new Vector2(.0470f,.0036f)};
            for(int i=0;i<=10;i++)s.Add(new Vector2(.0475f,Mathf.Lerp(.006f,.0976f,i/10f)));
            s.Add(new Vector2(.0467f,.1016f));s.Add(new Vector2(.0451f,.1053f));s.Add(new Vector2(.0384f,.1150f));
            return s.ToArray();
        }

        [MenuItem("MetaHackthone/Shadow Workshop/Break selected jar where the others pass through")]
        static void BreakSelected()
        {
            var jar=Selection.activeTransform;var others=Selection.transforms.Where(t=>t!=jar).ToArray();
            var mesh=Break(jar,others);Debug.Log($"BROKEN_JAR_OK {mesh.triangles.Length/3} tris");
        }
        [MenuItem("MetaHackthone/Shadow Workshop/Break selected jar where the others pass through",true)]
        static bool CanBreak()=>Selection.activeTransform&&Selection.activeTransform.GetComponent<MeshFilter>()&&Selection.transforms.Length>1;

        // Builds the broken jar for jar and the pieces passing through it, and puts it on jar (undoable), its old mesh
        // (or old shadow) kept as its shadow.
        public static Mesh Break(Transform jar,Transform[] others)
        {
            var mesh=BuildMesh(jar,others);
            var filter=jar.GetComponent<MeshFilter>();var tag=jar.GetComponent<ShadowPiece>();var shadow=tag&&tag.shadowProxy?tag.shadowProxy:filter.sharedMesh;
            Undo.RecordObjects(new Object[]{jar.gameObject,filter},"Break jar");
            jar.name="Broken jar";filter.sharedMesh=mesh;
            if(!tag)tag=Undo.AddComponent<ShadowPiece>(jar.gameObject);else Undo.RecordObject(tag,"Break jar");
            tag.role=ShadowPiece.Role.Picture;tag.shadowProxy=shadow;
            EditorUtility.SetDirty(jar.gameObject);
            return mesh;
        }

        // The pile's layouts to clear: as placed, and as the game draws it when the gym gathers it.
        static List<Matrix4x4[]> Layouts(Transform jar,Transform[] others)
        {
            var all=new[]{jar}.Concat(others).ToArray();var list=new List<Matrix4x4[]>{all.Select(t=>t.parent?t.parent.worldToLocalMatrix*t.localToWorldMatrix:t.localToWorldMatrix).ToArray()};
            var gym=Object.FindFirstObjectByType<Torch2Gym>();
            if(gym&&gym.drawGather<1&&all.All(t=>t.parent==jar.parent))
                list.Add(all.Select(t=>Matrix4x4.TRS(Torch2Level.Gathered(t.localPosition,gym.drawGather,gym.drawGatherFrom),t.localRotation,t.localScale)).ToArray());
            return list;
        }

        public static Mesh BuildMesh(Transform jar,Transform[] others)
        {
            // The shell's rings: the base (a fan from its middle) and the side, creased at the foot.
            var pos=new List<Vector3>();var nrm=new List<Vector3>();var rings=new List<int>();
            void Ring(Vector2 p,Vector2 n){rings.Add(pos.Count);for(int k=0;k<Sides;k++){float a=k*2*Mathf.PI/Sides;var radial=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));pos.Add(radial*p.x+Vector3.up*p.y);nrm.Add((radial*n.x+Vector3.up*n.y).normalized);}}
            int pole=pos.Count;pos.Add(Vector3.zero);nrm.Add(Vector3.down);
            Ring(Base[1],Vector2.down);int baseRing=0;
            for(int i=0;i<Side.Length;i++)
            {
                Vector2 Seg(int a)=>new Vector2(Side[a+1].y-Side[a].y,-(Side[a+1].x-Side[a].x)).normalized;
                var n=i==0?Seg(0):i==Side.Length-1?Seg(i-1):(Seg(i-1)+Seg(i)).normalized;Ring(Side[i],n);
            }

            // How far each shell point is inside the break: its distance to the nearest passing piece (negative inside
            // it) in any layout, less the clearance and a jagged extra.
            var layouts=Layouts(jar,others);var f=new float[pos.Count];for(int i=0;i<f.Length;i++)f[i]=float.MaxValue;
            for(int o=0;o<others.Length;o++)
            {
                var m=others[o].GetComponent<MeshFilter>().sharedMesh;var ov=m.vertices;var ot=m.triangles;
                foreach(var layout in layouts)
                {
                    var w=ov.Select(v=>layout[o+1].MultiplyPoint3x4(v)).ToArray();var toLayout=layout[0];
                    for(int i=0;i<pos.Count;i++){var p=toLayout.MultiplyPoint3x4(pos[i]);f[i]=Mathf.Min(f[i],SignedDistance(p,w,ot));}
                }
            }
            for(int i=0;i<f.Length;i++){int ring=i==pole?0:(i-1)/Sides,k=i==pole?0:(i-1)%Sides;f[i]-=Clearance+Jag*Noise(ring,k);}

            // Marching triangles: keep each triangle's part outside the break, cutting edges where f crosses zero.
            var outPos=new List<Vector3>();var outNrm=new List<Vector3>();var map=new Dictionary<long,int>();
            int Grid(int i){long key=i;if(!map.TryGetValue(key,out int x)){x=outPos.Count;outPos.Add(pos[i]);outNrm.Add(nrm[i]);map[key]=x;}return x;}
            int Cut(int a,int b)
            {
                long key=((long)Mathf.Min(a,b)+1)<<32|(uint)Mathf.Max(a,b);
                if(!map.TryGetValue(key,out int x)){float t=f[a]/(f[a]-f[b]);x=outPos.Count;outPos.Add(Vector3.Lerp(pos[a],pos[b],t));outNrm.Add(Vector3.Lerp(nrm[a],nrm[b],t).normalized);map[key]=x;}
                return x;
            }
            var tris=new List<int[]>();var edges=new List<(int a,int b,Vector3 into,int tri)>();
            void Clip(int a,int b,int c)
            {
                var v=new[]{a,b,c};int kept=v.Count(i=>f[i]>=0);if(kept==0)return;
                if(kept==3){tris.Add(new[]{Grid(a),Grid(b),Grid(c)});return;}
                var poly=new List<int>();var cuts=new List<int>();var gone=Vector3.zero;
                for(int e=0;e<3;e++)
                {
                    int p=v[e],q=v[(e+1)%3];if(f[p]>=0)poly.Add(Grid(p));else gone+=pos[p];
                    if((f[p]>=0)!=(f[q]>=0)){int x=Cut(p,q);poly.Add(x);cuts.Add(x);}
                }
                int first=tris.Count;for(int i=1;i+1<poly.Count;i++)tris.Add(new[]{poly[0],poly[i],poly[i+1]});
                gone/=3-kept;var mid=(outPos[cuts[0]]+outPos[cuts[1]])/2;var along=outPos[cuts[1]]-outPos[cuts[0]];var surf=(outNrm[cuts[0]]+outNrm[cuts[1]]).normalized;
                var into=Vector3.Cross(along,surf).normalized;if(Vector3.Dot(into,gone-mid)<0)into=-into;
                edges.Add((cuts[0],cuts[1],into,first));
            }
            for(int k=0;k<Sides;k++)Clip(pole,1+baseRing*Sides+(k+1)%Sides,1+baseRing*Sides+k);
            for(int r=1;r+1<rings.Count;r++)for(int k=0;k<Sides;k++)
            {
                int a=rings[r]+k,b=rings[r]+(k+1)%Sides,c=rings[r+1]+(k+1)%Sides,d=rings[r+1]+k;Clip(a,b,c);Clip(a,c,d);
            }

            // Only the jar itself: shards the cut left floating are dropped.
            var parent=Enumerable.Range(0,outPos.Count).ToArray();
            int Find(int x){while(parent[x]!=x)x=parent[x]=parent[parent[x]];return x;}
            foreach(var t in tris){parent[Find(t[1])]=Find(t[0]);parent[Find(t[2])]=Find(t[0]);}
            var size=new Dictionary<int,int>();foreach(var t in tris){int root=Find(t[0]);size[root]=size.TryGetValue(root,out int s)?s+1:1;}
            int main=size.OrderByDescending(kv=>kv.Value).First().Key;

            var pm=new PropMesh(1);var used=new Dictionary<int,int>();
            int Use(int i){if(!used.TryGetValue(i,out int x)){x=pm.Add(outPos[i],outNrm[i],Vector2.zero);used[i]=x;}return x;}
            foreach(var t in tris)if(Find(t[0])==main)pm.Tri(0,Use(t[0]),Use(t[1]),Use(t[2]));
            // The broken edge: a band Thick deep, square to the glass, facing into the hole.
            float thick=Thick/jar.lossyScale.magnitude*Mathf.Sqrt(3);
            foreach(var (p,q,into,tri) in edges)
            {
                if(Find(p)!=main)continue;
                pm.Quad(0,pm.Add(outPos[p],into,Vector2.zero),pm.Add(outPos[q],into,Vector2.zero),
                    pm.Add(outPos[q]-outNrm[q]*thick,into,Vector2.zero),pm.Add(outPos[p]-outNrm[p]*thick,into,Vector2.zero));
            }
            // Cracks running out from the break across the wall: thin fins of glass, like the edge, a few bends each,
            // stopping short of the hole.
            var passing=new List<(Vector3[] w,int[] t,Matrix4x4 jar)>();
            for(int o=0;o<others.Length;o++)
            {
                var m=others[o].GetComponent<MeshFilter>().sharedMesh;
                foreach(var layout in layouts)passing.Add((m.vertices.Select(v=>layout[o+1].MultiplyPoint3x4(v)).ToArray(),m.triangles,layout[0]));
            }
            float Gap(Vector3 p)=>passing.Min(x=>SignedDistance(x.jar.MultiplyPoint3x4(p),x.w,x.t))-Clearance;
            var rims=edges.Where(r=>Find(r.a)==main).ToList();
            for(int c=0;c<Cracks&&rims.Count>0;c++)
            {
                var e=rims[(c*rims.Count/Cracks+c*7)%rims.Count];var at=(outPos[e.a]+outPos[e.b])/2;var dir=-e.into;var n=(outNrm[e.a]+outNrm[e.b]).normalized;
                for(int s=0;s<4;s++)
                {
                    float turn=(Noise(c+40,s)-.5f)*60,len=Mathf.Lerp(.006f,.011f,Noise(c+80,s));
                    dir=Quaternion.AngleAxis(turn,n)*dir;var next=at+dir*len;
                    if(next.y<.008f||next.y>.095f)break;
                    var flat=new Vector3(next.x,0,next.z).normalized;next=flat*RadiusAt(next.y)+Vector3.up*next.y;var nn=flat;
                    if(Gap(next)<.002f)break;
                    var side=Vector3.Cross(next-at,n).normalized;
                    pm.Quad(0,pm.Add(at,side,Vector2.zero),pm.Add(next,side,Vector2.zero),pm.Add(next-nn*thick*.75f,side,Vector2.zero),pm.Add(at-n*thick*.75f,side,Vector2.zero));
                    dir=Vector3.ProjectOnPlane(next-at,nn).normalized;at=next;n=nn;
                }
            }
            return pm.Save(MeshPath,"Broken jar");
        }

        // The side's radius at height y (mesh units).
        static float RadiusAt(float y)
        {
            for(int i=0;i+1<Side.Length;i++)if(y<=Side[i+1].y)return Mathf.Lerp(Side[i].x,Side[i+1].x,Mathf.InverseLerp(Side[i].y,Side[i+1].y,y));
            return Side[Side.Length-1].x;
        }
        static float SignedDistance(Vector3 p,Vector3[] w,int[] t)
        {
            float best=float.MaxValue;int crossings=0;var ray=new Vector3(.5377f,.6124f,.5793f);
            for(int i=0;i<t.Length;i+=3)
            {
                Vector3 a=w[t[i]],b=w[t[i+1]],c=w[t[i+2]];best=Mathf.Min(best,(ClosestOnTriangle(p,a,b,c)-p).sqrMagnitude);
                if(RayHits(p,ray,a,b,c))crossings++;
            }
            return Mathf.Sqrt(best)*(crossings%2==1?-1:1);
        }
        static bool RayHits(Vector3 o,Vector3 d,Vector3 a,Vector3 b,Vector3 c)
        {
            var e1=b-a;var e2=c-a;var h=Vector3.Cross(d,e2);float det=Vector3.Dot(e1,h);if(Mathf.Abs(det)<1e-14f)return false;
            float inv=1/det;var s=o-a;float u=inv*Vector3.Dot(s,h);if(u<0||u>1)return false;var q=Vector3.Cross(s,e1);float v=inv*Vector3.Dot(d,q);if(v<0||u+v>1)return false;
            return inv*Vector3.Dot(e2,q)>0;
        }
        static Vector3 ClosestOnTriangle(Vector3 p,Vector3 a,Vector3 b,Vector3 c)
        {
            var ab=b-a;var ac=c-a;var ap=p-a;float d1=Vector3.Dot(ab,ap),d2=Vector3.Dot(ac,ap);if(d1<=0&&d2<=0)return a;
            var bp=p-b;float d3=Vector3.Dot(ab,bp),d4=Vector3.Dot(ac,bp);if(d3>=0&&d4<=d3)return b;
            float vc=d1*d4-d3*d2;if(vc<=0&&d1>=0&&d3<=0)return a+ab*(d1/(d1-d3));
            var cp=p-c;float d5=Vector3.Dot(ab,cp),d6=Vector3.Dot(ac,cp);if(d6>=0&&d5<=d6)return c;
            float vb=d5*d2-d1*d6;if(vb<=0&&d2>=0&&d6<=0)return a+ac*(d2/(d2-d6));
            float va=d3*d6-d5*d4;if(va<=0&&d4-d3>=0&&d5-d6>=0)return b+(c-b)*((d4-d3)/(d4-d3+d5-d6));
            float den=1/(va+vb+vc);return a+ab*(vb*den)+ac*(vc*den);
        }
        // A fixed 0..1 value per shell point, so the break is the same every build.
        static float Noise(int ring,int k){unchecked{uint h=(uint)(ring*73856093)^(uint)(k*19349663)^0x9e3779b9u;h^=h>>13;h*=0x5bd1e995;h^=h>>15;return (h&0xffff)/65535f;}}
    }
}
