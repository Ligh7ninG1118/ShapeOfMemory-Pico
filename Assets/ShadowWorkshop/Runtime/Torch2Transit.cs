using System;
using System.Collections.Generic;
using UnityEngine;

namespace CloudGarden.ShadowPuzzle
{
    // How a level's things travel between the table and their places in the orb, one at a time, so that none passes
    // through another: each lifts straight up off the table, swings round the part of the pile already built, and comes
    // into its place from outside along a line that is clear. Things nearest the hub go first. A thing made of several
    // pieces (one table group) moves as one. The game's intro plays it forwards (MemoryIntro) and its outro backwards; the
    // designers' gym previews it in Play mode. Planning uses each mover's mesh box, a little shrunk, and treats pairs that
    // already touch at rest as touching; the gym's Check level tests the real shapes along the way (Torch2GymTools).
    public sealed class Torch2Transit
    {
        // Lift: least rise off the table. Clear: gap kept round boxes. Rise: least upward slope of the line into a place.
        public const float Lift=.06f,Clear=.025f,Rise=.35f,Shrink=.9f;
        // Where loose things lie when a level has no table layout (x, z in level space): inside the table (x +-.7,
        // z -.46 to .54), clear of the lamps at the front corners and of the game's riddle card and console at the front.
        public static readonly Rect Area=Rect.MinMaxRect(-.58f,-.15f,.58f,.48f);
        // The table box's top in level space (Torch2Room: centre -.035, height .06).
        public const float TableTop=-.005f;

        // Per piece, level space: where it lies on the table and where it sits in the orb.
        public readonly Pose[] table,slot;
        // Movers in the order they fly; each piece's mover.
        public readonly Mover[] movers;
        public readonly int[] moverOf;
        public float Length{get;}
        public readonly Vector3 hub;

        public sealed class Mover
        {
            public int[] members;
            public float start,end;
            // The line into its place (out from the place) and whether no clear one was found.
            public Vector3 approach;
            public bool blocked;
            internal Pose[] fromOffsets,toOffsets;
            internal Quaternion fromRotation,toRotation;
            internal Bounds box;
            internal Vector3[] points;
            internal float[] turns,along;
            internal Vector3 TableCentre,SlotCentre;
            internal float radius;
            // The box's centre and how far the frame has turned (0 table, 1 place) at fraction e of the path.
            internal void At(float e,out Vector3 centre,out float turn)
            {
                float target=e*along[along.Length-1];int hi=Array.BinarySearch(along,target);if(hi<0)hi=~hi;
                hi=Mathf.Clamp(hi,1,along.Length-1);int lo=hi-1;float span=along[hi]-along[lo],f=span>1e-6f?(target-along[lo])/span:0;
                centre=Vector3.Lerp(points[lo],points[hi],f);turn=Mathf.Lerp(turns[lo],turns[hi],f);
            }
        }

        // drawn: where a piece's place in the pile is drawn (Torch2Workshop.Drawn; null = its own place).
        public Torch2Transit(Torch2Level level,Quaternion pileRotation,Vector3 pilePosition,Func<Vector3,Vector3> drawn=null):this(level,pileRotation,pilePosition,level.transitFly,level.transitOverlap,drawn){}
        // fly: seconds per mover; overlap: how much of one flight the next may share (0 = strictly one at a time).
        public Torch2Transit(Torch2Level level,Quaternion pileRotation,Vector3 pilePosition,float fly,float overlap,Func<Vector3,Vector3> drawn=null)
        {
            int n=level.pieces.Length;hub=pilePosition;table=TablePoses(level,out var groups);slot=new Pose[n];moverOf=new int[n];
            for(int i=0;i<n;i++){var at=level.pieces[i].position;if(drawn!=null)at=drawn(at);slot[i]=new Pose(pilePosition+pileRotation*at,pileRotation*Quaternion.Euler(level.pieces[i].euler));}
            // One mover per group, framed on its first member.
            var byGroup=new SortedDictionary<int,List<int>>();for(int i=0;i<n;i++){if(!byGroup.TryGetValue(groups[i],out var list))byGroup[groups[i]]=list=new List<int>();list.Add(i);}
            var all=new List<Mover>();
            foreach(var members in byGroup.Values)
            {
                var m=new Mover{members=members.ToArray()};int first=m.members[0];
                var fromFrame=table[first];var toFrame=slot[first];m.fromRotation=fromFrame.rotation;m.toRotation=toFrame.rotation;
                m.fromOffsets=new Pose[m.members.Length];m.toOffsets=new Pose[m.members.Length];bool any=false;var box=new Bounds();
                for(int k=0;k<m.members.Length;k++)
                {
                    int i=m.members[k];m.fromOffsets[k]=Mul(Inverse(fromFrame),table[i]);m.toOffsets[k]=Mul(Inverse(toFrame),slot[i]);
                    var piece=level.pieces[i];var bounds=Bounds(piece);
                    for(int c=0;c<8;c++)
                    {
                        var corner=m.fromOffsets[k].position+m.fromOffsets[k].rotation*Vector3.Scale(Corner(bounds,c),piece.Scale);
                        if(!any){box=new Bounds(corner,Vector3.zero);any=true;}else box.Encapsulate(corner);
                    }
                }
                m.box=box;m.radius=box.extents.magnitude;
                m.TableCentre=fromFrame.position+fromFrame.rotation*box.center;m.SlotCentre=toFrame.position+toFrame.rotation*box.center;
                foreach(int i in m.members)moverOf[i]=all.Count;all.Add(m);
            }
            Plan(all);movers=all.ToArray();
            for(int k=0;k<movers.Length;k++)foreach(int i in movers[k].members)moverOf[i]=k;
            float step=fly*(1-Mathf.Clamp(overlap,0,.9f));
            for(int k=0;k<movers.Length;k++){movers[k].start=k*step;movers[k].end=movers[k].start+fly;}
            Length=movers.Length==0?0:movers[movers.Length-1].end;
        }

        // A piece's pose (level space) at time t: on the table before its mover starts, in its place after it lands.
        public Pose Evaluate(int piece,float t)
        {
            var m=movers[moverOf[piece]];int k=Array.IndexOf(m.members,piece);
            float u=Mathf.Clamp01((t-m.start)/Mathf.Max(1e-4f,m.end-m.start)),e=u*u*u*(u*(u*6-15)+10);
            m.At(e,out var centre,out float turn);var rotation=Quaternion.Slerp(m.fromRotation,m.toRotation,turn);
            var frame=new Pose(centre-rotation*m.box.center,rotation);
            var offset=e<=0?m.fromOffsets[k]:e>=1?m.toOffsets[k]:new Pose(Vector3.Lerp(m.fromOffsets[k].position,m.toOffsets[k].position,e),Quaternion.Slerp(m.fromOffsets[k].rotation,m.toOffsets[k].rotation,e));
            return Mul(frame,offset);
        }
        public bool Flying(int piece,float t){var m=movers[moverOf[piece]];return t>m.start&&t<m.end;}

        // ---- Planning
        struct Box{public Vector3 c,e;public Quaternion q;}
        Box TableBox(Mover m,Vector3 lift=default)=>new Box{c=m.TableCentre+lift,e=m.box.extents*Shrink,q=m.fromRotation};
        Box SlotBox(Mover m,Vector3 shift=default)=>new Box{c=m.SlotCentre+shift,e=m.box.extents*Shrink,q=m.toRotation};
        // Planned backwards, by taking the finished pile apart: the outermost mover that can leave along a clear line, and
        // lift back off the table clear of what has gone back there already, comes out next; flying goes in the reverse
        // order. So a base goes in before what rests on it, and the pile builds from the inside out. Boxes that already
        // overlap at rest, on the table or in the orb, may overlap that much on the way, no more.
        void Plan(List<Mover> all)
        {
            int n=all.Count;var restTable=new float[n,n];var restSlot=new float[n,n];
            for(int a=0;a<n;a++)for(int b=0;b<n;b++)if(a!=b){restTable[a,b]=Depth(TableBox(all[a]),TableBox(all[b]));restSlot[a,b]=Depth(SlotBox(all[a]),SlotBox(all[b]));}
            float top=TableTop;foreach(var m in all){var b=TableBox(m);top=Mathf.Max(top,b.c.y+Reach(b,Vector3.up));}
            // In the orb when the one taken out flies in; back on the table (still waiting) when it flies.
            var inOrb=new List<int>();for(int i=0;i<n;i++)inOrb.Add(i);
            inOrb.Sort((a,b)=>(all[b].SlotCentre-hub).sqrMagnitude.CompareTo((all[a].SlotCentre-hub).sqrMagnitude));
            var onTable=new List<int>();var removed=new List<Mover>();var others=new List<int>();
            while(inOrb.Count>0)
            {
                // First choice: the whole way clear. Then: at least a clear line in. Else the outermost, marked blocked.
                int pick=-1;
                for(int pass=0;pass<2&&pick<0;pass++)foreach(int i in inOrb)
                {
                    var m=all[i];others.Clear();foreach(int j in inOrb)if(j!=i)others.Add(j);float radius=PileRadius(all,others),liftTo=LiftHeight(m,top);
                    foreach(var d in Lines(m))
                    {
                        if(!LineClear(all,i,d,others,onTable,restSlot,restTable,radius,out float exit))continue;
                        Path(m,liftTo,Entry(m,d,exit,liftTo),radius+m.radius*Shrink+Clear);
                        if(pass==1||PathClear(all,i,others,onTable,restSlot,restTable)){pick=i;m.approach=d;break;}
                    }
                    if(pick>=0)break;
                }
                if(pick<0)
                {
                    pick=inOrb[0];var m=all[pick];m.blocked=true;others.Clear();foreach(int j in inOrb)if(j!=pick)others.Add(j);float radius=PileRadius(all,others),liftTo=LiftHeight(m,top);
                    m.approach=Lines(m)[0];Path(m,liftTo,Entry(m,m.approach,Mathf.Max(.05f,radius+m.radius-(m.SlotCentre-hub).magnitude)+Clear,liftTo),radius+m.radius*Shrink+Clear);
                }
                inOrb.Remove(pick);onTable.Add(pick);removed.Add(all[pick]);
            }
            removed.Reverse();all.Clear();all.AddRange(removed);
        }
        // Where the line into the place starts: out of the pile, and no lower than the lift, so the swing stays above the table.
        Vector3 Entry(Mover m,Vector3 d,float exit,float liftTo)=>m.SlotCentre+d*Mathf.Max(exit,(liftTo-m.SlotCentre.y)/Mathf.Max(d.y,.05f));
        // The whole path, every 1.5 cm, against what still lies on the table and what is in the orb.
        bool PathClear(List<Mover> all,int i,List<int> pile,List<int> waiting,float[,] restSlot,float[,] restTable)
        {
            var m=all[i];float length=m.along[m.along.Length-1];
            for(float s=0;s<=length;s+=.015f)
            {
                m.At(s/length,out var centre,out float turn);var box=new Box{c=centre,e=m.box.extents*Shrink,q=Quaternion.Slerp(m.fromRotation,m.toRotation,turn)};
                foreach(int j in waiting)if(Depth(box,TableBox(all[j]))>Mathf.Max(0,restTable[i,j])+Slack)return false;
                foreach(int j in pile)if(Depth(box,SlotBox(all[j]))>Mathf.Max(0,restSlot[i,j])+Slack)return false;
            }
            return true;
        }
        float PileRadius(List<Mover> all,List<int> pile){float r=0;foreach(int p in pile)r=Mathf.Max(r,(all[p].SlotCentre-hub).magnitude+all[p].radius*Shrink);return r;}
        float LiftHeight(Mover m,float top){var b=TableBox(m);return Mathf.Max(m.TableCentre.y+Lift,top+Reach(b,Vector3.down)+Clear);}
        const float Slack=.003f;
        // Clear when the box slides out along d without cutting into what is placed or still on the table (deeper than
        // at rest), until it is outside the pile so far.
        bool LineClear(List<Mover> all,int i,Vector3 d,List<int> placed,List<int> waiting,float[,] restSlot,float[,] restTable,float placedRadius,out float exit)
        {
            var m=all[i];exit=Clear;float outside=placedRadius+m.radius*Shrink+Clear;
            for(float s=.01f;s<=.9f;s+=.01f)
            {
                var box=SlotBox(m,d*s);
                foreach(int j in placed)if(Depth(box,SlotBox(all[j]))>Mathf.Max(0,restSlot[i,j])+Slack)return false;
                foreach(int j in waiting)if(Depth(box,TableBox(all[j]))>Mathf.Max(0,restTable[i,j])+Slack)return false;
                if((box.c-hub).magnitude>=outside){exit=s+Clear;return true;}
            }
            exit=.9f;return true;
        }
        // Lines into a place, best first: out from the hub, straight down from above, and turned either way; never
        // steeper from below than Rise, so nothing comes up through the table.
        Vector3[] Lines(Mover m)
        {
            var radial=m.SlotCentre-hub;if(radial.sqrMagnitude<4e-4f)radial=Vector3.up;
            var away=Vector3.ProjectOnPlane(m.TableCentre-hub,Vector3.up);
            Vector3 Up(Vector3 v)
            {
                v.Normalize();if(v.y>=Rise)return v;
                var h=new Vector3(v.x,0,v.z);if(h.sqrMagnitude<1e-4f)h=away;if(h.sqrMagnitude<1e-4f)h=Vector3.back;
                return (h.normalized*Mathf.Sqrt(1-Rise*Rise)+Vector3.up*Rise).normalized;
            }
            return new[]{Up(radial),Vector3.up,Up(Quaternion.Euler(0,40,0)*radial),Up(Quaternion.Euler(0,-40,0)*radial),Up(away),Up(Vector3.back)};
        }
        // Lift straight up, swing round the hub outside the pile so far (turning on the way), then in along the line.
        void Path(Mover m,float liftTo,Vector3 entry,float keepOut)
        {
            var points=new List<Vector3>();var turns=new List<float>();
            var lifted=new Vector3(m.TableCentre.x,liftTo,m.TableCentre.z);
            for(int i=0;i<=6;i++){points.Add(Vector3.Lerp(m.TableCentre,lifted,i/6f));turns.Add(0);}
            Vector3 d1=lifted-hub,d2=entry-hub;float r1=d1.magnitude,r2=d2.magnitude;d1/=Mathf.Max(r1,1e-5f);d2/=Mathf.Max(r2,1e-5f);
            bool overTop=Vector3.Dot(d1,d2)<-.85f;
            for(int i=1;i<=24;i++)
            {
                float u=i/24f;var dir=overTop?(u<.5f?Vector3.Slerp(d1,Vector3.up,u*2):Vector3.Slerp(Vector3.up,d2,u*2-1)):Vector3.Slerp(d1,d2,u);
                // On the way round: no lower than the lift (above the table's things), no nearer the hub than keepOut.
                var p=hub+dir*Mathf.Max(Mathf.Lerp(r1,r2,u),keepOut);
                if(p.y<liftTo)
                {
                    p.y=liftTo;var flat=new Vector3(p.x-hub.x,0,p.z-hub.z);float need=keepOut*keepOut-(liftTo-hub.y)*(liftTo-hub.y);
                    if(need>flat.sqrMagnitude&&flat.sqrMagnitude>1e-6f)flat=flat.normalized*Mathf.Sqrt(need);p=new Vector3(hub.x+flat.x,liftTo,hub.z+flat.z);
                }
                points.Add(p);turns.Add(u*u*(3-2*u));
            }
            for(int i=1;i<=10;i++){points.Add(Vector3.Lerp(entry,m.SlotCentre,i/10f));turns.Add(1);}
            // Round the corners (Chaikin, ends kept).
            for(int pass=0;pass<2;pass++)
            {
                var p=new List<Vector3>{points[0]};var t=new List<float>{turns[0]};
                for(int i=0;i<points.Count-1;i++)
                {
                    p.Add(Vector3.Lerp(points[i],points[i+1],.25f));t.Add(Mathf.Lerp(turns[i],turns[i+1],.25f));
                    p.Add(Vector3.Lerp(points[i],points[i+1],.75f));t.Add(Mathf.Lerp(turns[i],turns[i+1],.75f));
                }
                p.Add(points[points.Count-1]);t.Add(turns[turns.Count-1]);points=p;turns=t;
            }
            m.points=points.ToArray();m.turns=turns.ToArray();m.along=new float[m.points.Length];
            for(int i=1;i<m.points.Length;i++)m.along[i]=m.along[i-1]+Vector3.Distance(m.points[i-1],m.points[i]);
            if(m.along[m.along.Length-1]<1e-5f)m.along[m.along.Length-1]=1e-5f;
        }
        // How far a box reaches along a direction from its centre.
        static float Reach(Box b,Vector3 d){float r=0;for(int k=0;k<3;k++)r+=b.e[k]*Mathf.Abs(Vector3.Dot(b.q*Axis(k),d));return r;}
        static Vector3 Axis(int k)=>k==0?Vector3.right:k==1?Vector3.up:Vector3.forward;
        // How deep two oriented boxes overlap (separating axes: the least overlap over the 15 axes; 0 or less = apart).
        static float Depth(Box a,Box b)
        {
            var t=b.c-a.c;float depth=float.PositiveInfinity;
            void Try(Vector3 axis){if(axis.sqrMagnitude<1e-6f)return;axis.Normalize();depth=Mathf.Min(depth,Reach(a,axis)+Reach(b,axis)-Mathf.Abs(Vector3.Dot(t,axis)));}
            for(int i=0;i<3;i++){Try(a.q*Axis(i));Try(b.q*Axis(i));}
            for(int i=0;i<3;i++)for(int j=0;j<3;j++)Try(Vector3.Cross(a.q*Axis(i),b.q*Axis(j)));
            return depth;
        }

        // ---- Table
        // Where each piece lies on the table: the level's table layout when every piece has one, otherwise a scatter.
        // groups: pieces sharing a value move as one.
        public static Pose[] TablePoses(Torch2Level level,out int[] groups)
        {
            int n=level.pieces.Length;groups=new int[n];var poses=new Pose[n];
            if(HasTable(level)){for(int i=0;i<n;i++){var p=level.pieces[i];poses[i]=new Pose(p.tablePosition,Quaternion.Euler(p.tableEuler));groups[i]=p.tableGroup;}return poses;}
            for(int i=0;i<n;i++)groups[i]=i;return Scatter(level);
        }
        public static bool HasTable(Torch2Level level)=>level.pieces.Length>0&&Array.TrueForAll(level.pieces,p=>p.onTable);
        // Rest pose of each piece on the table (level space): lying on its thinnest side, turned close to a right angle so
        // its footprint box stays tight, the largest placed first, each where its box overlaps the others' least (seeded
        // per level, so a level always scatters the same way).
        public static Pose[] Scatter(Torch2Level level)
        {
            int n=level.pieces.Length;var poses=new Pose[n];var boxes=new Rect[n];var flats=new Quaternion[n];var areas=new float[n];
            var random=new System.Random(Seed(level.name));
            for(int i=0;i<n;i++)
            {
                var piece=level.pieces[i];var size=Vector3.Scale(Bounds(piece).size,piece.Scale);
                flats[i]=size.y<=size.x&&size.y<=size.z?Quaternion.identity:size.x<=size.z?Quaternion.Euler(0,0,90):Quaternion.Euler(90,0,0);
                Footprint(piece,flats[i],out var min,out var max);areas[i]=(max.x-min.x)*(max.z-min.z);
            }
            var order=new int[n];for(int i=0;i<n;i++)order[i]=i;Array.Sort(order,(a,b)=>areas[b].CompareTo(areas[a]));
            var placed=new List<int>();
            foreach(int i in order)
            {
                var piece=level.pieces[i];float bestOverlap=float.PositiveInfinity;
                for(int attempt=0;attempt<300&&bestOverlap>0;attempt++)
                {
                    var rotation=Quaternion.Euler(0,90*random.Next(4)+((float)random.NextDouble()*2-1)*12,0)*flats[i];
                    Footprint(piece,rotation,out var min,out var max);
                    float w=Mathf.Min(max.x-min.x,Area.width),d=Mathf.Min(max.z-min.z,Area.height);
                    var c=new Vector2(Mathf.Lerp(Area.xMin+w*.5f,Area.xMax-w*.5f,(float)random.NextDouble()),Mathf.Lerp(Area.yMin+d*.5f,Area.yMax-d*.5f,(float)random.NextDouble()));
                    var box=new Rect(c.x-(max.x-min.x)*.5f,c.y-(max.z-min.z)*.5f,max.x-min.x,max.z-min.z);
                    float overlap=0;foreach(int j in placed)overlap+=Overlap(box,boxes[j],Gap);
                    if(overlap>=bestOverlap)continue;
                    bestOverlap=overlap;boxes[i]=box;
                    // Pivot such that the bottom touches the table top and the footprint's centre sits at c.
                    poses[i]=new Pose(new Vector3(c.x-(min.x+max.x)*.5f,TableTop-min.y+.0005f,c.y-(min.z+max.z)*.5f),rotation);
                }
                placed.Add(i);
            }
            return poses;
        }
        const float Gap=.012f;
        public static Bounds Bounds(Torch2Piece piece)=>piece.mesh?piece.mesh.bounds:new Bounds(Vector3.zero,Vector3.one*.05f);
        static Vector3 Corner(Bounds b,int c)=>b.center+Vector3.Scale(b.extents,new Vector3((c&1)==0?-1:1,(c&2)==0?-1:1,(c&4)==0?-1:1));
        // The piece's box at this rotation, about its pivot (scale applied).
        static void Footprint(Torch2Piece piece,Quaternion rotation,out Vector3 min,out Vector3 max)
        {
            var bounds=Bounds(piece);min=Vector3.positiveInfinity;max=Vector3.negativeInfinity;
            for(int c=0;c<8;c++){var p=rotation*Vector3.Scale(Corner(bounds,c),piece.Scale);min=Vector3.Min(min,p);max=Vector3.Max(max,p);}
        }
        // Overlapping area of two boxes, each grown by half the gap.
        static float Overlap(Rect a,Rect b,float gap)
        {
            float dx=Mathf.Min(a.xMax,b.xMax)-Mathf.Max(a.xMin,b.xMin)+gap,dz=Mathf.Min(a.yMax,b.yMax)-Mathf.Max(a.yMin,b.yMin)+gap;
            return dx>0&&dz>0?dx*dz:0;
        }
        static int Seed(string name){int h=17;foreach(char c in name)h=h*31+c;return h;}
        public static Pose Mul(Pose a,Pose b)=>new Pose(a.position+a.rotation*b.position,a.rotation*b.rotation);
        public static Pose Inverse(Pose a){var q=Quaternion.Inverse(a.rotation);return new Pose(-(q*a.position),q);}
    }
}
