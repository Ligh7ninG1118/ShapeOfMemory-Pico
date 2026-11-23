using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShapeOfMemory
{
    // Bookmarks: silk ribbons that come out from between the pages at the book's near edge, up over the stand's front
    // rail, and hang down in front of it, one per place worth jumping to (Settings and Begin at the front, the credits and
    // the back endpaper at the back), in book order: two groups either side of a gap over the drawer's knob (hanging in
    // front of it they hid it), clear of the pages' turning edges (MemoryBook.ReachIn). The open spread's ribbon hangs
    // longer.
    // A ribbon is pinched, not pressed (the user, 2026-11-15: a poke was unintuitive). PinchPicks (the user, 2026-11-16:
    // lifting it was hard to work): a pinch on its hanging silk is the choice; it lifts itself over the rail (as the
    // desktop's click lifts it) and the book turns to its spread (Jump), let go or not; a hand rising with it only lifts it
    // sooner. Without PinchPicks (2026-11-15) it swings up after the hand and turns the book only lifted LiftOn degrees,
    // falling back if let go short. A look and a pinch takes it too (GazeGrab). Fingertips never press it. Aqua, as
    // everything pinched and moved: its border lights while a pinch would take it, and while it is held. A hand coming
    // within TemptRadius stirs it as a breeze does the beckoning pages (it lifts and flutters), so it asks to be taken.
    // Shown, they grow out of the book one after another (sliding out from between the pages, over the rail and down);
    // hidden, they draw back in.
    // Each ribbon is one strip of silk (a mesh rebuilt as it moves, 2026-11-16): from the pages over the rail it lies flat,
    // rolls over the rail's front edge and hangs; a lift, the sway and the stir bend it along its length and twist it more
    // toward its foot, so the silk never parts at the rail (it was a hanging quad hinged to the span over the rail: stirred,
    // its twist opened a gap either side of its middle). Woven (Silk: fine ribs, a sheen, a stitched hem, a crease where it
    // rolls over the rail, a swallowtail end), tinted per ribbon, its mark embroidered in a deeper shade of its own colour,
    // and it sways a little, never while a hand or a look is on it. They show while the book lies open on its stand (Shown,
    // from the host). They hang from the book's stand pose, in the stage's space (the book never moves while they show).
    // Device log: SHAPE_OF_MEMORY_RIBBON.
    public sealed class MemoryBookmarks : MonoBehaviour
    {
        // Metres: width, pitch, how far the hanging part drops (the open spread's further by Longer); the sway (degrees,
        // seconds a swing). Spaced for a pinch (the user, 2026-11-16: further apart): 3.4 cm of air between neighbours (was
        // 1.4), 11 cm over the knob (was 9); the outer ones hang 14 cm out, their reach (GrabRadius) still 6 cm short of
        // the pages' turning edges (MemoryBook.ReachIn, 25 cm out).
        public const float Width=.036f,Pitch=.07f,Drop=.06f,Longer=.012f,KnobGap=.11f,Sway=3f,SwaySeconds=4.6f;
        // Taking: a pinch within GrabRadius (metres) of a ribbon's silk takes it, if it closed at most GrabWindow seconds
        // before (as the book's page edges allow: hands close on the way in). Lifted LiftOn degrees from hanging it turns
        // the book. Taken by a look, IndirectRise metres of the hand's rise lift it all the way (90 degrees). Picked (a
        // pinch, PinchPicks, or the desktop's click), it lifts itself 75 degrees over ClickSeconds.
        public const float GrabRadius=.045f,GrabWindow=.4f,LiftOn=60,IndirectRise=.12f,ClickSeconds=.3f;
        // A pinch on the silk is the choice (the user, 2026-11-16). Off: the 2026-11-15 lift, LiftOn degrees by the hand.
        public static bool PinchPicks=true;
        // Growing out over GrowSeconds, each GrowStagger after the one before; drawing back in over ShrinkSeconds. The
        // stir: a hand within TemptRadius (metres) lifts it TemptLift degrees and flutters it TemptFlutter either way,
        // twisting it up to TemptTwist at its foot.
        public const float GrowSeconds=.5f,GrowStagger=.08f,ShrinkSeconds=.25f,TemptRadius=.09f,TemptLift=12,TemptFlutter=6,TemptTwist=10;
        // The silk's roll over the rail's front edge: how fast (metres of silk) it turns from lying across the rail to
        // hanging (an exponential, so it never kinks); the hanging part is cut into Sections, closer at the top (Spacing).
        public const float Roll=.003f;
        const int Sections=18;const float Spacing=1.6f;
        // The stand's front rail, from the Book Rest (metres; y out of the board, z up it): it runs from 1.5 to 4.5 cm in
        // front of the book's near edge and stands 3.6 to 4.1 cm off the board (raycast on the scene's stand, 2026-11-15).
        // Each ribbon comes up from between the pages to Crest, over the rail's back edge, across its top to Lip, just past
        // its front, and hangs from there. (Measured from the book, the lip sank into the rail when our thinner book came
        // back: it lies 2 cm lower than the artist's did.)
        static readonly Vector3 Crest=new Vector3(0,.047f,-.012f),Lip=new Vector3(0,.0455f,-.055f);
        // Metres from the Book Rest up to the book's root (its underside), so the rail's points can be found from the
        // book's pose (MemoryGame: the book's Underside, scaled). Unset: the environment artist's book's.
        public Func<float> BookHeight;
        public sealed class Mark
        {
            public string Id;public int Spread;public Color Color;public Texture2D Icon;
            // Hang: the rail's lip, turned to the ribbon's lift (its press box hangs from it); Silk: the whole ribbon's mesh;
            // Outline: the aqua border behind its hanging part; Badge: its embroidered mark.
            public Transform Root,Hang,Badge;public Pressable Press;public Renderer Silk,Outline;public float Phase,Swing;
            // How far it is lifted (degrees from hanging, toward the reader) and how fast it is falling back; who holds it
            // (-1 nobody, 0 the left hand, 1 the right, Click the desktop's click or a hand's pick let go), whether by a look
            // (Sweep, from Start); a hand near enough to take it; the silk's length while a hand holds it; how far it has
            // lifted itself since it was picked.
            public float Raised,Speed,Length=Drop,Picked;public int Holder=-1;public bool Sweep,Near;public Vector3 Start;
            // How far it has grown out of the book (0..1) and the wait before it starts; how stirred it is by a hand near
            // (0..1), and whether one is this frame. Its silk from the pages over the rail: the ends of the two flat spans.
            public float Grow,Wait,Tempt;public bool Lure;public int Index;
            public Vector3 OverFrom,OverTo,AcrossTo,Side;
            // The silk as built this frame (the Root's space): the hanging part's middle line, from the lip down, and how
            // long it is (0: not hanging yet).
            public readonly Vector3[] Line=new Vector3[Sections+1];public float Hanging;
            internal Mesh SilkMesh,OutlineMesh;internal Vector3[] SilkVertices,OutlineVertices;
        }
        public const int Click=2;
        public readonly List<Mark> Marks=new List<Mark>();
        public Func<bool> Shown;
        public Func<int> Current;
        public Action<int> Jump;
        public Func<bool> HandsAllowed;
        // The tracked hands (MemoryGame: the player's): whether they are, and a hand's pinch point and pinch (thumb to index
        // tip, metres).
        public delegate bool HandReader(int hand,out Vector3 tip,out float pinch);
        public Func<bool> Tracking;
        public HandReader ReadHand;
        MemoryKit kit;
        Func<Pose> bookPose;
        static Texture2D silk;
        Material silkMaterial;
        readonly bool[] pinching=new bool[2],seen=new bool[2],near=new bool[2];
        readonly float[] closedAt={float.NegativeInfinity,float.NegativeInfinity};
        // Each section's share of the hanging part, from the lip (0) to the foot (1): closer at the top, where it rolls.
        static readonly float[] along=Along();
        static float[] Along(){var a=new float[Sections+1];for(int k=0;k<=Sections;k++)a[k]=Mathf.Pow(k/(float)Sections,Spacing);return a;}

        public static MemoryBookmarks Add(Transform stage,MemoryKit kit,Func<Pose> bookPose)
        {
            var go=new GameObject("Bookmarks");go.transform.SetParent(stage,false);
            var b=go.AddComponent<MemoryBookmarks>();b.kit=kit;b.bookPose=bookPose;return b;
        }
        // Lays the ribbons out again: (id, spread, colour, icon) in book order.
        public void Set(IList<(string id,int spread,Color color,Texture2D icon)> marks)
        {
            foreach(var m in Marks){if(m.Root)Destroy(m.Root.gameObject);if(m.SilkMesh)Destroy(m.SilkMesh);if(m.OutlineMesh)Destroy(m.OutlineMesh);}
            Marks.Clear();
            // The first half (rounded up) left of the gap, the rest right of it, in order.
            int left=(marks.Count+1)/2;float edge=KnobGap*.5f+Width*.5f;
            for(int i=0;i<marks.Count;i++)Marks.Add(Make(marks[i],i<left?-edge-(left-1-i)*Pitch:edge+(i-left)*Pitch,i));
        }
        Mark Make((string id,int spread,Color color,Texture2D icon) spec,float x,int index)
        {
            if(!silk)silk=Silk();
            if(!silkMaterial){silkMaterial=new Material(kit.Icon){name="Ribbon silk"};silkMaterial.SetTexture("_BaseMap",silk);silkMaterial.SetColor("_BaseColor",Color.white);kit.Owned.Add(silkMaterial);}
            var mark=new Mark{Id=spec.id,Spread=spec.spread,Color=spec.color,Icon=spec.icon,Phase=index*1.9f,Index=index};
            var pose=bookPose();float s=MemoryBook.FlatScale,lift=BookHeight?.Invoke()??MemoryBook.ArtHalf*MemoryBook.ArtScale*s;
            Vector3 Book(Vector3 local)=>pose.position+pose.rotation*(local*s);
            Vector3 Rail(Vector3 p)=>pose.position+pose.rotation*new Vector3(x,p.y-lift,p.z);
            var root=new GameObject("Ribbon "+spec.id).transform;root.SetParent(transform,false);mark.Root=root;
            // From between the pages at the near edge, up over the rail and across it, then down in front of the stand.
            mark.OverFrom=Book(new Vector3(x/s,.002f,0));mark.OverTo=Rail(Crest);mark.AcrossTo=Rail(Lip);mark.Side=pose.rotation*Vector3.right;
            // The lip, turned to the lift: a look (MemoryGaze finds its press box, GazeGrab takes it) and the desktop's click
            // find it hanging from here; hands never press it.
            var hang=new GameObject("Hang").transform;hang.SetParent(root,false);hang.localPosition=mark.AcrossTo;hang.localRotation=Quaternion.identity;mark.Hang=hang;
            mark.Silk=Strip("Silk",root,silkMaterial,spec.color,Silk(mark),out mark.SilkMesh,out mark.SilkVertices);
            mark.Outline=Strip("Outline",root,kit.Screen,MemoryPalette.AquaHover,Outline(),out mark.OutlineMesh,out mark.OutlineVertices);mark.Outline.enabled=false;
            // Its mark embroidered in a deeper shade of its own colour, above the swallowtail.
            if(spec.icon)mark.Badge=MemoryUi.Picture("Mark",root,mark.AcrossTo,Quaternion.identity,Vector2.one*Width*.72f,kit.Icon,spec.icon,Color.Lerp(spec.color,MemoryPalette.Ink800,.62f),kit.Owned).transform;
            var hit=new GameObject("Press",typeof(BoxCollider));hit.transform.SetParent(hang,false);hit.transform.localPosition=new Vector3(0,-Drop*.5f,0);hit.transform.localScale=new Vector3(Width,Drop,.008f);
            mark.Press=Pressable.Add(hit,()=>Clicked(mark),()=>Shown==null||Shown(),()=>false,new Vector3(0,0,-.51f),Quaternion.identity);
            mark.Press.Label="bookmark/"+spec.id;mark.Press.Why=()=>Shown!=null&&!Shown()?"bookmarks hidden now":PinchPicks?"pinched, not pressed":"lifted, not pressed";
            Build(mark,0,Drop,0,0);
            return mark;
        }
        // A mesh renderer under root with this topology (vertices filled in by Build each frame).
        Renderer Strip(string name,Transform root,Material material,Color color,(Vector2[] uv,int[] triangles,int count) shape,out Mesh mesh,out Vector3[] vertices)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root,false);
            vertices=new Vector3[shape.count];mesh=new Mesh{name="Ribbon "+name};mesh.MarkDynamic();
            mesh.vertices=vertices;mesh.uv=shape.uv;mesh.triangles=shape.triangles;
            go.GetComponent<MeshFilter>().sharedMesh=mesh;
            var r=go.GetComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;r.receiveShadows=false;
            MemoryUi.Tint(r,color);return r;
        }
        // The silk's sections: the pages' edge, the rail's back edge (Crest), its front (Lip), then the hanging part's.
        // v runs from 1 at the pages to .5 at the lip and 0 at the foot (the swallowtail): Silk's texture is laid out so.
        static (Vector2[],int[],int) Silk(Mark m)
        {
            float over=Vector3.Distance(m.OverFrom,m.OverTo),across=Vector3.Distance(m.OverTo,m.AcrossTo);
            int sections=3+Sections;var v=new float[sections];v[0]=1;v[1]=.5f+.5f*across/Mathf.Max(1e-4f,over+across);v[2]=.5f;
            for(int k=1;k<=Sections;k++)v[2+k]=.5f*(1-along[k]);
            var uv=new Vector2[sections*2];for(int i=0;i<sections;i++){uv[i*2]=new Vector2(0,v[i]);uv[i*2+1]=new Vector2(1,v[i]);}
            return (uv,Ladder(sections,false),sections*2);
        }
        // The border: the hanging part only, a little wider and longer, both faces (the screen material culls).
        static (Vector2[],int[],int) Outline(){int sections=Sections+2;return (new Vector2[sections*2],Ladder(sections,true),sections*2);}
        // Two triangles between each pair of sections (left, right vertices), facing the reader; both: and the back too.
        static int[] Ladder(int sections,bool both)
        {
            var t=new List<int>();
            for(int i=0;i+1<sections;i++)
            {
                int aL=i*2,aR=aL+1,bL=aL+2,bR=aL+3;t.AddRange(new[]{aL,aR,bR,aL,bR,bL});
                if(both)t.AddRange(new[]{aL,bR,aR,aL,bL,bR});
            }
            return t.ToArray();
        }
        public Mark Find(string id)=>Marks.Find(m=>m.Id==id);
        // Whether a hand is near enough a ribbon to take it, or holds one (its pinch is the ribbon's, not the look's).
        public bool HandAt(int hand)=>near[hand]||Held(hand)!=null;
        public bool Holding=>Marks.Exists(m=>m.Holder>=0);
        public Mark Held(int hand)=>Marks.Find(m=>m.Holder==hand);
        bool Allowed()=>(Shown==null||Shown())&&(HandsAllowed==null||HandsAllowed());
        static string Side(int hand)=>hand==0?"left":hand==1?"right":"click";

        // One hand this frame (Update, from the tracked hands; tests drive it): where its pinch is, whether it pinches,
        // and whether that pinch is fresh enough to take a ribbon. Held: the ribbon follows (and turns the book past
        // LiftOn); let go, it falls back, or picked (PinchPicks) lifts on by itself. Free: the nearest ribbon within
        // GrabRadius lights, and a fresh pinch takes it.
        public void Hand(int hand,Vector3 tip,bool pinch,bool fresh)
        {
            near[hand]=false;
            var held=Held(hand);
            if(held!=null)
            {
                if(!pinch){if(PinchPicks){held.Holder=Click;held.Sweep=false;Debug.Log($"SHAPE_OF_MEMORY_RIBBON {held.Id} let go, lifting on");}else LetGo(held,"let go");return;}
                float by=held.Sweep?Mathf.Clamp01(Rise(held,tip)/IndirectRise)*90:Toward(held,tip,out held.Length);
                held.Raised=PinchPicks?Mathf.Max(by,held.Picked):by;
                if(held.Raised>=LiftOn)Lifted(held);
                return;
            }
            if(!Allowed())return;
            Mark best=null;float closest=GrabRadius;
            foreach(var m in Marks)
            {
                if(!m.Root||m.Holder>=0||m.Grow<1)continue;float d=Distance(m,tip);
                if(d<TemptRadius)m.Lure=true;
                if(d<closest){closest=d;best=m;}
            }
            if(best==null)return;
            near[hand]=true;best.Near=true;
            if(pinch&&fresh){Take(best,hand,false,tip);Debug.Log($"SHAPE_OF_MEMORY_RIBBON {best.Id} taken hand={Side(hand)}");}
        }
        void Take(Mark m,int holder,bool sweep,Vector3 tip){m.Holder=holder;m.Sweep=sweep;m.Start=tip;m.Speed=0;m.Picked=m.Raised;if(holder<2)closedAt[holder]=float.NegativeInfinity;}
        // A look at a ribbon and a pinch (MemoryGaze): it lifts as the hand rises. Returns whether it took the ribbon.
        public bool GazeGrab(Pressable on,int hand,Vector3 tip)
        {
            var m=Marks.Find(k=>k.Press==on);
            if(m==null||m.Holder>=0||m.Grow<1||Held(hand)!=null||!Allowed())return false;
            Take(m,hand,true,tip);
            Debug.Log($"SHAPE_OF_MEMORY_RIBBON {m.Id} taken by a look hand={Side(hand)}");return true;
        }
        void Clicked(Mark m){if(m.Holder>=0||m.Grow<1)return;Take(m,Click,false,default);Debug.Log($"SHAPE_OF_MEMORY_RIBBON {m.Id} clicked");}
        void Lifted(Mark m)
        {
            Debug.Log($"SHAPE_OF_MEMORY_RIBBON {m.Id} lifted {m.Raised:0} deg -> spread {m.Spread}");
            m.Holder=-1;m.Sweep=false;m.Speed=0;m.Picked=0;Jump?.Invoke(m.Spread);
        }
        void LetGo(Mark m,string why){Debug.Log($"SHAPE_OF_MEMORY_RIBBON {m.Id} {why} at {m.Raised:0} deg");m.Holder=-1;m.Sweep=false;m.Picked=0;}
        // Stage space (the ribbons' own): the way they hang is -y, toward the reader -z.
        Vector3 Local(Vector3 world)=>transform.InverseTransformPoint(world);
        // The angle a hand at tip holds the ribbon at, the silk pointing at it (degrees from hanging, toward the reader),
        // and the silk's length to reach it.
        float Toward(Mark m,Vector3 tip,out float length)
        {
            var d=Local(tip)-Local(m.Hang.position);d.x=0;
            length=Mathf.Clamp(d.magnitude+.012f,.03f,Drop+Longer);
            return Mathf.Clamp(Mathf.Atan2(-d.z,-d.y)*Mathf.Rad2Deg,-15,150);
        }
        // A look's hand: its rise since the pinch, and half its draw toward the reader.
        float Rise(Mark m,Vector3 tip){var d=Local(tip)-Local(m.Start);return d.y+Mathf.Max(0,-d.z)*.5f;}
        // From a point to the ribbon's hanging silk (its middle line, as built this frame).
        float Distance(Mark m,Vector3 point)
        {
            var p=m.Root.InverseTransformPoint(point);float best=float.MaxValue;
            if(m.Hanging<=0)return Vector3.Distance(p,m.AcrossTo);
            for(int k=0;k<Sections;k++)
            {
                Vector3 a=m.Line[k],ab=m.Line[k+1]-a;float t=Mathf.Clamp01(Vector3.Dot(p-a,ab)/Mathf.Max(1e-8f,ab.sqrMagnitude));
                best=Mathf.Min(best,Vector3.Distance(p,a+ab*t));
            }
            return best;
        }
        void Update()
        {
            bool shown=Shown==null||Shown();int open=Current?.Invoke()??-1;float now=Time.unscaledTime,dt=Time.unscaledDeltaTime;
            foreach(var m in Marks){m.Near=m.Lure=false;if(!shown&&m.Holder>=0)LetGo(m,"hidden");}
            if(shown&&ReadHand!=null&&(Tracking==null||Tracking()))
                for(int hand=0;hand<2;hand++)
                {
                    if(!ReadHand(hand,out var tip,out float pinch))
                    {
                        var held=Held(hand);if(held!=null){if(PinchPicks){held.Holder=Click;held.Sweep=false;}else LetGo(held,"hand lost");}
                        pinching[hand]=seen[hand]=near[hand]=false;continue;
                    }
                    bool was=pinching[hand];pinching[hand]=pinch<(was?MemoryBook.PinchOff:MemoryBook.PinchOn);
                    if(pinching[hand]&&!was)closedAt[hand]=seen[hand]?now:float.NegativeInfinity;
                    seen[hand]=true;
                    Hand(hand,tip,pinching[hand],pinching[hand]&&now-closedAt[hand]<=GrabWindow);
                }
            foreach(var m in Marks)
            {
                if(!m.Root)continue;
                // Growing out (one after another) or drawing back in.
                if(shown)
                {
                    if(m.Grow<=0&&m.Wait<=0)m.Wait=m.Index*GrowStagger+.001f;
                    if(m.Wait>0)m.Wait-=dt;
                    if(m.Wait<=0)m.Grow=Mathf.Min(1,m.Grow+dt/GrowSeconds);
                }
                else{m.Wait=0;m.Grow=Mathf.Max(0,m.Grow-dt/ShrinkSeconds);}
                bool active=m.Grow>0;
                if(m.Root.gameObject.activeSelf!=active)m.Root.gameObject.SetActive(active);
                if(!active)continue;
                // Picked (the desktop's click, or a hand's pinch with PinchPicks): it lifts itself; a hand still holding it
                // can only lift it sooner (Hand).
                bool picked=m.Holder==Click||m.Holder>=0&&PinchPicks;
                if(picked)m.Picked=Mathf.MoveTowards(m.Picked,75,dt*75/ClickSeconds);
                if(m.Holder==Click){m.Raised=Mathf.Max(m.Raised,m.Picked);if(m.Raised>=LiftOn)Lifted(m);}
                else if(m.Holder>=0&&PinchPicks&&m.Picked>m.Raised){m.Raised=m.Picked;if(m.Raised>=LiftOn)Lifted(m);}
                bool held=m.Holder>=0,here=m.Spread==open,hover=held||m.Near||m.Press.GazeOn||m.Press.Hover;
                // Let go: it falls back to hanging, a little past and back.
                if(!held&&m.Raised!=0)
                {
                    m.Speed+=(-m.Raised*60-m.Speed*9)*dt;m.Raised+=m.Speed*dt;
                    if(Mathf.Abs(m.Raised)<.2f&&Mathf.Abs(m.Speed)<2){m.Raised=0;m.Speed=0;}
                }
                // The open spread's ribbon hangs further: stretched, its mark moved down with its foot. A hand lifting it
                // (not picking it: PinchPicks) gathers the silk to its fingers. Growing, only as much silk as has come out:
                // over the rail, then down.
                float length=held&&m.Holder!=Click&&!m.Sweep&&!PinchPicks?m.Length:Drop+(here?Longer:0);
                float overLength=Vector3.Distance(m.OverFrom,m.OverTo),acrossLength=Vector3.Distance(m.OverTo,m.AcrossTo);
                float silk=Mathf.SmoothStep(0,1,m.Grow)*(overLength+acrossLength+length);
                // A slow swing about the rail; still while a hand or a look is on it, or it is lifted. Stirred by a hand near:
                // lifted a little and fluttering, twisting as silk does in a draught, more toward its foot.
                if(!hover&&m.Raised==0)m.Swing=Sway*Mathf.Sin(now*Mathf.PI*2/SwaySeconds+m.Phase);
                m.Tempt=Mathf.MoveTowards(m.Tempt,m.Lure&&!held&&m.Grow>=1?1:0,dt/.25f);
                float lift=(m.Raised==0?m.Swing:0)+m.Raised;
                m.Hang.localRotation=Quaternion.Euler(lift,0,0);
                Build(m,silk,length,lift,now);
                m.Outline.enabled=hover&&m.Hanging>.001f;if(m.Outline.enabled)MemoryUi.Tint(m.Outline,held?MemoryPalette.AquaGrab:MemoryPalette.AquaHover);
            }
        }
        // The ribbon as far as silk metres of it have come out (its hanging part at most length long), the hanging part
        // lifted lift degrees (plus the stir).
        void Build(Mark m,float silk,float length,float lift,float now)
        {
            float overLength=Vector3.Distance(m.OverFrom,m.OverTo),acrossLength=Vector3.Distance(m.OverTo,m.AcrossTo);
            float hanging=Mathf.Clamp(silk-overLength-acrossLength,0,length);m.Hanging=hanging;
            // The flat spans, revealed from the pages.
            Vector3 OnRail(float s)=>s<=overLength?Vector3.Lerp(m.OverFrom,m.OverTo,s/Mathf.Max(1e-5f,overLength)):Vector3.Lerp(m.OverTo,m.AcrossTo,Mathf.Clamp01((s-overLength)/Mathf.Max(1e-5f,acrossLength)));
            float shown=Mathf.Min(silk,overLength+acrossLength);
            var side=m.Side.normalized;
            var v=m.SilkVertices;float half=Width*.5f;
            void Put(Vector3[] into,int section,Vector3 at,Vector3 across,float w){into[section*2]=at-across*w;into[section*2+1]=at+across*w;}
            Put(v,0,m.OverFrom,side,half);Put(v,1,OnRail(Mathf.Min(shown,overLength)),side,half);Put(v,2,OnRail(shown),side,half);
            // The hanging part: from lying across the rail it rolls over its front edge (Roll) to the lift's angle, which the
            // stir raises more toward the foot and sets fluttering down its length; it twists about its own line, not at
            // all at the lip and most at the foot.
            var across=m.AcrossTo-m.OverTo;float lying=Mathf.Atan2(-across.z,-across.y)*Mathf.Rad2Deg;
            float tempt=m.Tempt;
            float At(float t,float s)
            {
                float flutter=tempt*TemptFlutter*(.7f*Mathf.Sin(now*Mathf.PI*2*2.4f+m.Phase-2.2f*t)+.3f*Mathf.Sin(now*Mathf.PI*2*5.3f+m.Phase*2-3.1f*t))*(.3f+.7f*t);
                float hang=lift+tempt*TemptLift*(.35f+.65f*t)+flutter;
                return hang+(lying-hang)*Mathf.Exp(-s/Roll);
            }
            Vector3 Down(float degrees){float a=degrees*Mathf.Deg2Rad;return new Vector3(0,-Mathf.Cos(a),-Mathf.Sin(a));}
            var p=OnRail(shown);m.Line[0]=p;var o=m.OutlineVertices;
            Vector3 lastTangent=Down(At(0,0));
            for(int k=0;k<=Sections;k++)
            {
                if(k>0)
                {
                    float s0=along[k-1]*hanging,s1=along[k]*hanging;
                    var dir=Down(At((along[k-1]+along[k])*.5f,(s0+s1)*.5f));p+=dir*(s1-s0);m.Line[k]=p;
                    var next=k<Sections?Down(At((along[k]+along[k+1])*.5f,(along[k]+along[k+1])*.5f*hanging)):dir;
                    lastTangent=(dir+next).normalized;
                }
                float t=along[k],twist=tempt*TemptTwist*Mathf.Sin(now*Mathf.PI*2*1.6f+m.Phase-1.5f*t)*t;
                var flat=Vector3.ProjectOnPlane(side,lastTangent).normalized;var turned=Quaternion.AngleAxis(twist,lastTangent)*flat;
                Put(v,2+k,p,turned,half);
                // The border, just behind the silk (away from the reader), a little wider; one more section past the foot.
                var toward=Vector3.Cross(turned,lastTangent);
                Put(o,k,p-toward*.0015f,turned,half+.003f);
                if(k==Sections)Put(o,k+1,p+lastTangent*.002f-toward*.0015f,turned,half+.003f);
                // Its mark, 22 mm up from the foot, on the silk's face toward the reader.
                if(m.Badge&&k>0&&along[k-1]*hanging<=hanging-.022f&&along[k]*hanging>=hanging-.022f)
                    m.Badge.SetLocalPositionAndRotation(Vector3.Lerp(m.Line[k-1],p,Mathf.InverseLerp(along[k-1]*hanging,along[k]*hanging,hanging-.022f))+toward*.0008f,Quaternion.LookRotation(-toward,-lastTangent));
            }
            if(m.Badge&&m.Badge.gameObject.activeSelf!=hanging>.03f)m.Badge.gameObject.SetActive(hanging>.03f);
            m.SilkMesh.vertices=v;m.SilkMesh.RecalculateBounds();
            m.OutlineMesh.vertices=o;m.OutlineMesh.RecalculateBounds();
            // The press box hangs as long as the silk does.
            var box=m.Press.transform;float drop=Mathf.Max(.001f,hanging);
            if(!Mathf.Approximately(box.localScale.y,drop)){box.localScale=new Vector3(Width,drop,.008f);box.localPosition=new Vector3(0,-drop*.5f,0);}
        }
        void OnDestroy(){foreach(var m in Marks){if(m.SilkMesh)Destroy(m.SilkMesh);if(m.OutlineMesh)Destroy(m.OutlineMesh);}}
        // Woven silk, white (each ribbon tints it), laid out along the whole ribbon: v from 1 at the pages to .5 at the
        // rail's lip and 0 at the foot. Fine ribs across, a sheen down one side, a stitched hem along both edges, a crease
        // where it rolls over the lip, a swallowtail cut into its foot (alpha, for the icon material's clip).
        static Texture2D Silk()
        {
            const int W=64,H=512;var t=new Texture2D(W,H,TextureFormat.RGBA32,true){name="Ribbon silk",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Trilinear,anisoLevel=4};
            var pixels=new Color32[W*H];var random=new System.Random(31);
            for(int y=0;y<H;y++)for(int x=0;x<W;x++)
            {
                float u=(x+.5f)/W,v=(y+.5f)/H,edge=Mathf.Min(u,1-u);
                float rib=.94f+.06f*Mathf.Cos(y*Mathf.PI*2/3f);
                float sheen=1+.12f*Mathf.Exp(-Mathf.Pow((u-.32f)/.11f,2))-.08f*Mathf.Exp(-Mathf.Pow((u-.82f)/.14f,2));
                float stitch=edge>.075f&&edge<.105f&&(y/5)%2==0?.74f:1;
                float border=edge<.045f?.86f:1;
                float crease=1-.2f*Mathf.Exp(-Mathf.Pow((v-.505f)/.012f,2));
                float grain=1+((float)random.NextDouble()-.5f)*.05f;
                float c=Mathf.Clamp01(rib*sheen*stitch*border*crease*grain);
                bool cut=v<.075f*(1-Mathf.Abs(u-.5f)*2);
                pixels[y*W+x]=new Color(c,c,c,cut?0:1);
            }
            t.SetPixels32(pixels);t.Apply(true,true);return t;
        }
    }
}
