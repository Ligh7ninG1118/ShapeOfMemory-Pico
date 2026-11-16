using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShapeOfMemory
{
    // The ghost hand (2026-11-15): when a tracked hand goes where the world is solid (into the table top, the open book,
    // the button panel, a lamp already pushed as far as it gives), the drawn hand stops on the surface and a faint copy of
    // the tracked hand shows where the real one is. Only the drawing moves: pinches, presses and grabs all keep reading
    // the tracked joints (MemoryTouch presses 6 mm in front of a face, before the hand would reach it).
    // The drawn hand moves as one piece (every joint by the same offset): bending fingers apart reads as broken.
    // A thin solid (the table top is 4 cm) would push a deep hand out through its far side, so each solid remembers the
    // face a hand came in through and keeps pushing out of that one; past GiveUp the hand is let go (the player has
    // clearly moved on, or tracking jumped).
    public sealed class MemoryGhostHand
    {
        public struct Solid
        {
            public int Id;
            public Vector3 Centre;
            public Quaternion Turn;
            public Vector3 Half;      // a box's half sizes along Turn's axes
            public float Radius;      // > 0: a sphere of this radius instead
        }
        // Metres. Joint radii: about the skin's distance from the joint.
        public const float FingerRadius=.0085f,PalmRadius=.02f,GiveUp=.12f,ShowFrom=.012f,ShowFull=.03f;
        public readonly List<Solid> Solids=new List<Solid>();
        readonly Func<List<Solid>,int> collect;
        readonly Dictionary<int,Vector2Int>[] faces={new Dictionary<int,Vector2Int>(),new Dictionary<int,Vector2Int>()};
        readonly Vector3[][] drawn={new Vector3[MemoryHandSkin.Count],new Vector3[MemoryHandSkin.Count]};
        readonly bool[] had=new bool[2];
        readonly HashSet<int> touched=new HashSet<int>();
        public Vector3 Offset(int hand)=>offsets[hand];
        readonly Vector3[] offsets=new Vector3[2];

        // collect: fills the list with this frame's solids (the host knows what is solid now).
        public MemoryGhostHand(Func<List<Solid>,int> collect){this.collect=collect;}

        public static float JointRadius(int joint)=>joint<=MemoryHandSkin.Wrist?PalmRadius:FingerRadius;
        // How much the ghost shows for an offset (0 hidden .. 1).
        public static float Shown(Vector3 offset)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(ShowFrom,ShowFull,offset.magnitude));

        // The offset that keeps hand's joints (world, tracked) out of every solid; the host draws joints + offset.
        public Vector3 Solve(int hand,IReadOnlyList<Vector3> joints)
        {
            Solids.Clear();collect?.Invoke(Solids);
            var offset=Solve(Solids,joints,faces[hand],had[hand]?drawn[hand]:null,touched);
            if(offset.magnitude>GiveUp){offset=Vector3.zero;faces[hand].Clear();}
            for(int i=0;i<joints.Count&&i<drawn[hand].Length;i++)drawn[hand][i]=joints[i]+offset;
            had[hand]=true;offsets[hand]=offset;
            return offset;
        }
        // Tracking lost: start afresh when it comes back.
        public void Lost(int hand){had[hand]=false;faces[hand].Clear();offsets[hand]=Vector3.zero;}

        // The solver itself (tests call it): pushes the whole hand out of each solid it is in, out of the face it came in
        // through (remembered in faces by solid id: x the axis, y the side), judged from where it was drawn last frame.
        public static Vector3 Solve(List<Solid> solids,IReadOnlyList<Vector3> joints,Dictionary<int,Vector2Int> faces,IReadOnlyList<Vector3> before,HashSet<int> touched)
        {
            var offset=Vector3.zero;touched.Clear();
            for(int pass=0;pass<3;pass++)
            {
                bool moved=false;
                foreach(var s in solids)
                    for(int j=0;j<joints.Count;j++)
                    {
                        float r=JointRadius(j);var p=joints[j]+offset;
                        if(s.Radius>0)
                        {
                            var d=p-s.Centre;float dist=d.magnitude,pen=s.Radius+r-dist;
                            if(pen<=0)continue;
                            offset+=(dist>1e-5f?d/dist:Vector3.up)*pen;touched.Add(s.Id);moved=true;continue;
                        }
                        var inv=Quaternion.Inverse(s.Turn);var l=inv*(p-s.Centre);
                        Vector2Int face;
                        if(faces.TryGetValue(s.Id,out face))
                        {
                            // Came in through this face: held out of it however deep (even past the far side), as long as
                            // the joint is over the face itself.
                            bool over=true;
                            for(int k=0;k<3;k++)if(k!=face.x&&Mathf.Abs(l[k])>s.Half[k]+r)over=false;
                            if(!over)continue;
                        }
                        else
                        {
                            // clear of the box grown by the joint
                            if(Mathf.Abs(l.x)>s.Half.x+r||Mathf.Abs(l.y)>s.Half.y+r||Mathf.Abs(l.z)>s.Half.z+r)continue;
                            var from=before!=null&&j<before.Count?inv*(before[j]-s.Centre):l;
                            face=Face(from,s.Half);faces[s.Id]=face;
                        }
                        int a=face.x;float side=face.y;
                        float depth=s.Half[a]+r-side*l[a];
                        if(depth<=0)continue;
                        var normal=Vector3.zero;normal[a]=side;
                        offset+=s.Turn*normal*depth;touched.Add(s.Id);moved=true;
                    }
                if(!moved)break;
            }
            // A solid no joint is in any more forgets its face.
            if(faces.Count>0)
            {
                var gone=new List<int>();
                foreach(var id in faces.Keys)if(!touched.Contains(id))gone.Add(id);
                foreach(var id in gone)faces.Remove(id);
            }
            return offset;
        }
        // The face a point is out past furthest (inside: the nearest face).
        static Vector2Int Face(Vector3 local,Vector3 half)
        {
            int best=0;float most=float.MinValue;
            for(int k=0;k<3;k++){float e=Mathf.Abs(local[k])-half[k];if(e>most){most=e;best=k;}}
            return new Vector2Int(best,local[best]>=0?1:-1);
        }

        // ---- Solids from the scene's things
        // A box round a transform's meshes, in its own frame (unit scale): the box's local centre and half sizes.
        // keep: which meshes count (null: all; the scene's table carries env art's ivy, which rises above its top).
        public static bool LocalBox(Transform root,out Bounds box,Func<MeshFilter,bool> keep=null)
        {
            box=default;bool any=false;
            if(!root)return false;
            var toRoot=root.worldToLocalMatrix;
            foreach(var f in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if(!f.sharedMesh||(keep!=null&&!keep(f)))continue;
                var m=toRoot*f.transform.localToWorldMatrix;var b=f.sharedMesh.bounds;
                for(int c=0;c<8;c++)
                {
                    var corner=b.center+Vector3.Scale(b.extents,new Vector3((c&1)!=0?1:-1,(c&2)!=0?1:-1,(c&4)!=0?1:-1));
                    var p=m.MultiplyPoint3x4(corner);
                    if(!any){box=new Bounds(p,Vector3.zero);any=true;}else box.Encapsulate(p);
                }
            }
            return any;
        }
        // A world solid from a box in a transform's frame (the transform's scale applied).
        public static Solid Box(int id,Transform t,Vector3 localCentre,Vector3 localHalf)
        {
            var s=t.lossyScale;
            return new Solid{Id=id,Centre=t.TransformPoint(localCentre),Turn=t.rotation,Half=new Vector3(Mathf.Abs(localHalf.x*s.x),Mathf.Abs(localHalf.y*s.y),Mathf.Abs(localHalf.z*s.z))};
        }
    }
}
