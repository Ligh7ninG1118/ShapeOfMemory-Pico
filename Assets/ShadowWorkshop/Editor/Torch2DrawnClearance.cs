using System.Collections.Generic;
using System.Linq;
using System.Text;
using CloudGarden.ShadowPuzzle;
using UnityEditor;
using UnityEngine;

namespace ShadowWorkshopIntegration.Editor
{
    // Keeps a gathered pile's drawn pieces out of each other. A level with drawGather below 1 is drawn with its far pieces
    // pulled toward the hub (Torch2Level.Gathered), each at full size, so pieces that clear each other as placed can be
    // drawn into one another (Home_NEW at 0.4: 19 pairs, 2026-11-15; the gym shows the real places, so it never showed).
    // This finds those pairs (a triangle edge of one crossing a triangle of the other) and gathers the outer piece of each
    // less (Torch2Piece.drawGather, Step at a time toward 1) until none remain. Pieces that touch as placed (at one
    // position, like a jar, its lid and the biscuits in it, or crossing, like a cup sitting in its saucer) keep one gather
    // so they stay together; gathering less cannot part those anyway. The shadow keeps the real places, so the puzzle is
    // unchanged. The gym's export runs it; Assets > Shadow Workshop > Keep drawn pieces apart runs it on a level.
    public static class Torch2DrawnClearance
    {
        const float Step=.05f,Cell=5;   // gather step; grid cell in mm
        const int MaxRounds=40;

        [MenuItem("Assets/Shadow Workshop/Keep drawn pieces apart")]
        static void OnSelected()
        {
            foreach(var path in Selection.objects.OfType<Torch2Level>().Select(AssetDatabase.GetAssetPath).ToArray())
            {
                // Unity's copy can be older than the file (gym exports, Plastic updates): read the file first.
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);var level=AssetDatabase.LoadAssetAtPath<Torch2Level>(path);
                var notes=new StringBuilder();var left=Solve(level,notes);EditorUtility.SetDirty(level);
                Debug.Log($"TORCH2_DRAWN_CLEARANCE {level.name}: {notes}{(left.Count>0?" Still crossing as placed: "+string.Join("; ",left):"")}");
            }
            AssetDatabase.SaveAssets();
        }
        [MenuItem("Assets/Shadow Workshop/Keep drawn pieces apart",true)] static bool CanSolve()=>Selection.objects.OfType<Torch2Level>().Any();

        sealed class Body{public Vector3[] local;public int[] tris;public Matrix4x4 turn;public Vector3 position;public int unit;}

        // Sets every piece's drawGather (0 where the level's already keeps it clear) and returns the pairs that still
        // cross when drawn: those that cross as placed too.
        public static List<string> Solve(Torch2Level level,StringBuilder notes=null)
        {
            var pieces=level.pieces;int n=pieces.Length;
            for(int i=0;i<n;i++)pieces[i].drawGather=0;
            if(level.drawGather>=1){notes?.Append("drawn as placed (drawGather 1), nothing to keep apart.");return new List<string>();}
            var bodies=new Body[n];
            for(int i=0;i<n;i++)
            {
                var p=pieces[i];if(!p.mesh)continue;
                var mesh=p.mesh.isReadable?p.mesh:Torch2Gym.ReadableCopy(p.mesh);
                bodies[i]=new Body{local=mesh.vertices,tris=mesh.triangles,turn=Matrix4x4.TRS(Vector3.zero,Quaternion.Euler(p.euler),p.Scale),position=p.position,unit=i};
                if(mesh!=p.mesh)Object.DestroyImmediate(mesh);
            }
            Vector3[] Placed(int i,float g)
            {
                var b=bodies[i];var at=Torch2Level.Gathered(b.position,g,level.drawGatherFrom);var w=new Vector3[b.local.Length];
                for(int k=0;k<w.Length;k++)w[k]=(b.turn.MultiplyPoint3x4(b.local[k])+at)*1000;
                return w;
            }
            // Pieces touching as placed (one position, or crossing) form one unit with one gather.
            var asPlaced=Enumerable.Range(0,n).Select(i=>bodies[i]!=null?Placed(i,1):null).ToArray();
            var allowed=new HashSet<(int,int)>();
            int Root(int i){while(bodies[i].unit!=i)i=bodies[i].unit=bodies[bodies[i].unit].unit;return i;}
            for(int i=0;i<n;i++)for(int j=i+1;j<n;j++)
            {
                if(bodies[i]==null||bodies[j]==null)continue;
                bool together=(bodies[i].position-bodies[j].position).sqrMagnitude<1e-10f;
                if(!together&&Cross(asPlaced[i],bodies[i].tris,asPlaced[j],bodies[j].tris)){together=true;allowed.Add((i,j));}
                if(together){int a=Root(i),b=Root(j);if(a!=b)bodies[a].unit=b;}
            }
            for(int i=0;i<n;i++)if(bodies[i]!=null)bodies[i].unit=Root(i);
            var gather=Enumerable.Repeat(level.drawGather,n).ToArray();
            int rounds=0;var crossing=new List<(int,int)>();
            for(;rounds<MaxRounds;rounds++)
            {
                var drawn=Enumerable.Range(0,n).Select(i=>bodies[i]!=null?Placed(i,gather[bodies[i].unit]):null).ToArray();
                crossing=Pairs(bodies).Where(pair=>!allowed.Contains(pair)&&Cross(drawn[pair.Item1],bodies[pair.Item1].tris,drawn[pair.Item2],bodies[pair.Item2].tris)).ToList();
                if(crossing.Count==0)break;
                // Gather the outer one of each pair less (the inner one if the outer is already as placed).
                var ease=new HashSet<int>();
                foreach(var (i,j) in crossing)
                {
                    int outer=bodies[i].position.magnitude>=bodies[j].position.magnitude?i:j,inner=outer==i?j:i;
                    int u=gather[bodies[outer].unit]<1?bodies[outer].unit:bodies[inner].unit;if(gather[u]<1)ease.Add(u);
                }
                if(ease.Count==0)break;
                foreach(int u in ease)gather[u]=Mathf.Min(1,gather[u]+Step);
            }
            for(int i=0;i<n;i++)
            {
                if(bodies[i]==null)continue;float g=gather[bodies[i].unit];
                bool moves=bodies[i].position.magnitude>level.drawGatherFrom;
                pieces[i].drawGather=moves&&g>level.drawGather+1e-4f?Mathf.Round(g*100)/100:0;
            }
            level.pieces=pieces;
            var eased=Enumerable.Range(0,n).Where(i=>pieces[i].drawGather>0).Select(i=>$"{pieces[i].label} {pieces[i].drawGather:0.00}").ToList();
            notes?.Append(crossing.Count==0
                ?$"{(eased.Count==0?"no piece needed easing":"eased "+string.Join(", ",eased))} (level {level.drawGather:0.00}); nothing drawn crosses anything after {rounds} rounds."
                :$"still crossing after {rounds} rounds: {string.Join("; ",crossing.Select(c=>pieces[c.Item1].label+" x "+pieces[c.Item2].label))}.");
            return allowed.Select(a=>pieces[a.Item1].label+" x "+pieces[a.Item2].label).ToList();
        }

        // Every pair of different pieces not yet in one unit.
        static IEnumerable<(int,int)> Pairs(Body[] bodies)
        {
            for(int i=0;i<bodies.Length;i++)for(int j=i+1;j<bodies.Length;j++)
                if(bodies[i]!=null&&bodies[j]!=null&&bodies[i].unit!=bodies[j].unit)yield return (i,j);
        }

        // Whether any triangle edge of either mesh (mm, pile space) crosses a triangle of the other.
        static bool Cross(Vector3[] va,int[] ta,Vector3[] vb,int[] tb)
        {
            Bounds(va,out var aLo,out var aHi);Bounds(vb,out var bLo,out var bHi);
            var lo=Vector3.Max(aLo,bLo);var hi=Vector3.Min(aHi,bHi);
            if(lo.x>hi.x||lo.y>hi.y||lo.z>hi.z)return false;
            return EdgesCross(va,ta,vb,tb,lo,hi)||EdgesCross(vb,tb,va,ta,lo,hi);
        }
        static void Bounds(Vector3[] v,out Vector3 lo,out Vector3 hi){lo=Vector3.one*float.MaxValue;hi=-lo;foreach(var p in v){lo=Vector3.Min(lo,p);hi=Vector3.Max(hi,p);}}
        static long Key(int x,int y,int z)=>((long)(x+1000)<<22)|((long)(y+1000)<<11)|(long)(z+1000);
        static bool EdgesCross(Vector3[] va,int[] ta,Vector3[] vb,int[] tb,Vector3 lo,Vector3 hi)
        {
            bool Outside(Vector3 mn,Vector3 mx)=>mx.x<lo.x||mx.y<lo.y||mx.z<lo.z||mn.x>hi.x||mn.y>hi.y||mn.z>hi.z;
            var grid=new Dictionary<long,List<int>>();
            for(int k=0;k<tb.Length;k+=3)
            {
                var mn=Vector3.Min(vb[tb[k]],Vector3.Min(vb[tb[k+1]],vb[tb[k+2]]));var mx=Vector3.Max(vb[tb[k]],Vector3.Max(vb[tb[k+1]],vb[tb[k+2]]));
                if(Outside(mn,mx))continue;
                for(int x=Mathf.FloorToInt(mn.x/Cell);x<=Mathf.FloorToInt(mx.x/Cell);x++)for(int y=Mathf.FloorToInt(mn.y/Cell);y<=Mathf.FloorToInt(mx.y/Cell);y++)for(int z=Mathf.FloorToInt(mn.z/Cell);z<=Mathf.FloorToInt(mx.z/Cell);z++)
                {var key=Key(x,y,z);if(!grid.TryGetValue(key,out var list))grid[key]=list=new List<int>();list.Add(k);}
            }
            if(grid.Count==0)return false;
            for(int k=0;k<ta.Length;k+=3)for(int e=0;e<3;e++)
            {
                var p=va[ta[k+e]];var q=va[ta[k+(e+1)%3]];var mn=Vector3.Min(p,q);var mx=Vector3.Max(p,q);
                if(Outside(mn,mx))continue;
                for(int x=Mathf.FloorToInt(mn.x/Cell);x<=Mathf.FloorToInt(mx.x/Cell);x++)for(int y=Mathf.FloorToInt(mn.y/Cell);y<=Mathf.FloorToInt(mx.y/Cell);y++)for(int z=Mathf.FloorToInt(mn.z/Cell);z<=Mathf.FloorToInt(mx.z/Cell);z++)
                    if(grid.TryGetValue(Key(x,y,z),out var list))foreach(int t in list)if(Segment(p,q,vb[tb[t]],vb[tb[t+1]],vb[tb[t+2]]))return true;
            }
            return false;
        }
        // Segment pq against triangle abc (Moller-Trumbore, t in 0..1).
        static bool Segment(Vector3 p,Vector3 q,Vector3 a,Vector3 b,Vector3 c)
        {
            var d=q-p;var e1=b-a;var e2=c-a;var h=Vector3.Cross(d,e2);float det=Vector3.Dot(e1,h);if(Mathf.Abs(det)<1e-9f)return false;
            float f=1/det;var s=p-a;float u=f*Vector3.Dot(s,h);if(u<0||u>1)return false;
            var qv=Vector3.Cross(s,e1);float v=f*Vector3.Dot(d,qv);if(v<0||u+v>1)return false;
            float t=f*Vector3.Dot(e2,qv);return t>=0&&t<=1;
        }
    }
}
