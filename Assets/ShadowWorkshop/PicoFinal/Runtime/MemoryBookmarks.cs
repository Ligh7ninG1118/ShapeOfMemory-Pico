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
    // A ribbon is lifted, not pressed (the user, 2026-11-15: a poke was unintuitive). A pinch on its hanging silk takes
    // it and it swings up round the rail after the hand; lifted LiftOn degrees (toward the reader and up), the book
    // riffles to its spread (Jump) and the ribbon drops back. Let go short and it falls back. A look and a pinch takes it
    // too, the hand's rise lifting it (GazeGrab); the desktop's click lifts it by itself. Fingertips never press it. Aqua,
    // as everything pinched and moved: its border lights while a pinch would take it, and while it is held. A hand coming
    // within TemptRadius stirs it as a breeze does the beckoning pages (it lifts and flutters), so it asks to be taken.
    // Shown, they grow out of the book one after another (sliding out from between the pages, over the rail and down);
    // hidden, they draw back in.
    // The silk is woven (Silk: fine ribs, a sheen, a stitched hem, a crease where it bends over the rail, a swallowtail
    // end), tinted per ribbon, its mark embroidered in a deeper shade of its own colour, and it sways a little, never
    // while a hand or a look is on it. They show while the book lies open on its stand (Shown, from the host). They hang
    // from the book's stand pose, in the stage's space (the book never moves while they show). Device log:
    // SHAPE_OF_MEMORY_RIBBON.
    public sealed class MemoryBookmarks : MonoBehaviour
    {
        // Metres: width, pitch, how far the hanging part drops (the open spread's further by Longer); the sway (degrees,
        // seconds a swing).
        public const float Width=.036f,Pitch=.05f,Drop=.06f,Longer=.012f,KnobGap=.09f,Sway=3f,SwaySeconds=4.6f;
        // Lifting: a pinch within GrabRadius (metres) of a ribbon's silk takes it, if it closed at most GrabWindow seconds
        // before (as the book's page edges allow: hands close on the way in); lifted LiftOn degrees from hanging it turns
        // the book. Taken by a look, IndirectRise metres of the hand's rise lift it all the way (90 degrees). The desktop's
        // click lifts it over ClickSeconds.
        public const float GrabRadius=.035f,GrabWindow=.4f,LiftOn=60,IndirectRise=.12f,ClickSeconds=.3f;
        // Growing out over GrowSeconds, each GrowStagger after the one before; drawing back in over ShrinkSeconds. The
        // stir: a hand within TemptRadius (metres) lifts it TemptLift degrees and flutters it TemptFlutter either way.
        public const float GrowSeconds=.5f,GrowStagger=.08f,ShrinkSeconds=.25f,TemptRadius=.09f,TemptLift=12,TemptFlutter=6;
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
            public Transform Root,Hang,Cloth,Badge;public Pressable Press;public Renderer Outline;public float Phase,Swing;
            // How far it is lifted (degrees from hanging, toward the reader) and how fast it is falling back; who holds it
            // (-1 nobody, 0 the left hand, 1 the right, Click the desktop's click), whether by a look (Sweep, from Start);
            // a hand near enough to take it; the silk's length while a hand holds it.
            public float Raised,Speed,Length=Drop;public int Holder=-1;public bool Sweep,Near;public Vector3 Start;
            // How far it has grown out of the book (0..1) and the wait before it starts; how stirred it is by a hand near
            // (0..1), and whether one is this frame. Its silk from the pages over the rail: the spans and their ends.
            public float Grow,Wait,Tempt;public bool Lure;public int Index;
            public Transform Over,Across;public Vector3 OverFrom,OverTo,AcrossTo;
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
        static Texture2D body,tail;
        readonly bool[] pinching=new bool[2],seen=new bool[2],near=new bool[2];
        readonly float[] closedAt={float.NegativeInfinity,float.NegativeInfinity};

        public static MemoryBookmarks Add(Transform stage,MemoryKit kit,Func<Pose> bookPose)
        {
            var go=new GameObject("Bookmarks");go.transform.SetParent(stage,false);
            var b=go.AddComponent<MemoryBookmarks>();b.kit=kit;b.bookPose=bookPose;return b;
        }
        // Lays the ribbons out again: (id, spread, colour, icon) in book order.
        public void Set(IList<(string id,int spread,Color color,Texture2D icon)> marks)
        {
            foreach(var m in Marks)if(m.Root)Destroy(m.Root.gameObject);
            Marks.Clear();
            // The first half (rounded up) left of the gap, the rest right of it, in order.
            int left=(marks.Count+1)/2;float edge=KnobGap*.5f+Width*.5f;
            for(int i=0;i<marks.Count;i++)Marks.Add(Make(marks[i],i<left?-edge-(left-1-i)*Pitch:edge+(i-left)*Pitch,i));
        }
        Mark Make((string id,int spread,Color color,Texture2D icon) spec,float x,int index)
        {
            if(!body){body=Silk(false);tail=Silk(true);}
            var mark=new Mark{Id=spec.id,Spread=spec.spread,Color=spec.color,Icon=spec.icon,Phase=index*1.9f,Index=index};
            var pose=bookPose();float s=MemoryBook.FlatScale,lift=BookHeight?.Invoke()??MemoryBook.ArtHalf*MemoryBook.ArtScale*s;
            Vector3 Book(Vector3 local)=>pose.position+pose.rotation*(local*s);
            Vector3 Rail(Vector3 p)=>pose.position+pose.rotation*new Vector3(x,p.y-lift,p.z);
            var root=new GameObject("Ribbon "+spec.id).transform;root.SetParent(transform,false);mark.Root=root;
            // From between the pages at the near edge, up over the rail and across it (only the top of the silk: no tail).
            var from=Book(new Vector3(x/s,.002f,0));var crest=Rail(Crest);var lip=Rail(Lip);
            Transform Span(string name,Vector3 a,Vector3 b,float v0,float v1)
            {
                var along=b-a;
                var r=MemoryUi.Picture(name,root,(a+b)*.5f,Quaternion.LookRotation(Vector3.Cross(pose.rotation*Vector3.right,along.normalized),along),new Vector2(Width,along.magnitude),kit.Icon,body,spec.color,kit.Owned);
                r.sharedMaterial.SetTextureScale("_BaseMap",new Vector2(1,v1-v0));r.sharedMaterial.SetTextureOffset("_BaseMap",new Vector2(0,v0));
                return r.transform;
            }
            mark.Over=Span("Over",from,crest,.62f,.75f);mark.Across=Span("Across",crest,lip,.75f,1);mark.OverFrom=from;mark.OverTo=crest;mark.AcrossTo=lip;
            // Then down in front of the stand, its face to the reader (-z: the stage's forward is away from them), swinging
            // (and lifted) about the lip.
            var hang=new GameObject("Hang").transform;hang.SetParent(root,false);hang.localPosition=lip;hang.localRotation=Quaternion.identity;mark.Hang=hang;
            mark.Cloth=MemoryUi.Picture("Ribbon",hang,new Vector3(0,-Drop*.5f,0),Quaternion.identity,new Vector2(Width,Drop),kit.Icon,tail,spec.color,kit.Owned).transform;
            mark.Outline=MemoryUi.Shape(PrimitiveType.Quad,"Outline",hang,new Vector3(0,-Drop*.5f,.0015f),Quaternion.identity,new Vector3(Width+.006f,Drop+.004f,1),kit.Screen,MemoryPalette.AquaHover).GetComponent<Renderer>();mark.Outline.enabled=false;
            // Its mark embroidered in a deeper shade of its own colour, above the swallowtail.
            if(spec.icon)mark.Badge=MemoryUi.Picture("Mark",hang,new Vector3(0,-Drop+.022f,-.0008f),Quaternion.identity,Vector2.one*Width*.72f,kit.Icon,spec.icon,Color.Lerp(spec.color,MemoryPalette.Ink800,.62f),kit.Owned).transform;
            // For a look (MemoryGaze finds it, GazeGrab takes it) and the desktop's click; hands never press it.
            var hit=new GameObject("Press",typeof(BoxCollider));hit.transform.SetParent(hang,false);hit.transform.localPosition=new Vector3(0,-Drop*.5f,0);hit.transform.localScale=new Vector3(Width,Drop,.008f);
            mark.Press=Pressable.Add(hit,()=>Clicked(mark),()=>Shown==null||Shown(),()=>false,new Vector3(0,0,-.51f),Quaternion.identity);
            mark.Press.Label="bookmark/"+spec.id;mark.Press.Why=()=>Shown!=null&&!Shown()?"bookmarks hidden now":"lifted, not pressed";
            return mark;
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
        // LiftOn); let go, it falls back. Free: the nearest ribbon within GrabRadius lights, and a fresh pinch takes it.
        public void Hand(int hand,Vector3 tip,bool pinch,bool fresh)
        {
            near[hand]=false;
            var held=Held(hand);
            if(held!=null)
            {
                if(!pinch){LetGo(held,"let go");return;}
                held.Raised=held.Sweep?Mathf.Clamp01(Rise(held,tip)/IndirectRise)*90:Toward(held,tip,out held.Length);
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
            if(pinch&&fresh){best.Holder=hand;best.Sweep=false;best.Start=tip;best.Speed=0;closedAt[hand]=float.NegativeInfinity;Debug.Log($"SHAPE_OF_MEMORY_RIBBON {best.Id} taken hand={Side(hand)}");}
        }
        // A look at a ribbon and a pinch (MemoryGaze): it lifts as the hand rises. Returns whether it took the ribbon.
        public bool GazeGrab(Pressable on,int hand,Vector3 tip)
        {
            var m=Marks.Find(k=>k.Press==on);
            if(m==null||m.Holder>=0||m.Grow<1||Held(hand)!=null||!Allowed())return false;
            m.Holder=hand;m.Sweep=true;m.Start=tip;m.Speed=0;closedAt[hand]=float.NegativeInfinity;
            Debug.Log($"SHAPE_OF_MEMORY_RIBBON {m.Id} taken by a look hand={Side(hand)}");return true;
        }
        void Clicked(Mark m){if(m.Holder>=0||m.Grow<1)return;m.Holder=Click;m.Sweep=false;Debug.Log($"SHAPE_OF_MEMORY_RIBBON {m.Id} clicked");}
        void Lifted(Mark m)
        {
            Debug.Log($"SHAPE_OF_MEMORY_RIBBON {m.Id} lifted {m.Raised:0} deg -> spread {m.Spread}");
            m.Holder=-1;m.Sweep=false;m.Speed=0;Jump?.Invoke(m.Spread);
        }
        void LetGo(Mark m,string why){Debug.Log($"SHAPE_OF_MEMORY_RIBBON {m.Id} {why} at {m.Raised:0} deg");m.Holder=-1;m.Sweep=false;}
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
        // From a point to the ribbon's hanging silk (its middle line, as it hangs now).
        float Distance(Mark m,Vector3 point)
        {
            var a=m.Hang.position;var b=m.Hang.TransformPoint(new Vector3(0,-m.Cloth.localScale.y,0));
            var ab=b-a;float t=Mathf.Clamp01(Vector3.Dot(point-a,ab)/Mathf.Max(1e-6f,ab.sqrMagnitude));
            return Vector3.Distance(point,a+ab*t);
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
                        var held=Held(hand);if(held!=null)LetGo(held,"hand lost");
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
                // The desktop's click: it lifts itself.
                if(m.Holder==Click){m.Raised=Mathf.MoveTowards(m.Raised,75,dt*75/ClickSeconds);if(m.Raised>=LiftOn)Lifted(m);}
                bool held=m.Holder>=0,here=m.Spread==open,hover=held||m.Near||m.Press.GazeOn||m.Press.Hover;
                // Let go: it falls back to hanging, a little past and back.
                if(!held&&m.Raised!=0)
                {
                    m.Speed+=(-m.Raised*60-m.Speed*9)*dt;m.Raised+=m.Speed*dt;
                    if(Mathf.Abs(m.Raised)<.2f&&Mathf.Abs(m.Speed)<2){m.Raised=0;m.Speed=0;}
                }
                // The open spread's ribbon hangs further: stretched, its mark moved down with its foot. A hand holding it
                // gathers the silk to its fingers. Growing, only as much silk as has come out: over the rail, then down.
                float length=held&&m.Holder!=Click&&!m.Sweep?m.Length:Drop+(here?Longer:0);
                float overLength=Vector3.Distance(m.OverFrom,m.OverTo),acrossLength=Vector3.Distance(m.OverTo,m.AcrossTo);
                float silk=Mathf.SmoothStep(0,1,m.Grow)*(overLength+acrossLength+length);
                Reveal(m.Over,m.OverFrom,m.OverTo,silk);Reveal(m.Across,m.OverTo,m.AcrossTo,silk-overLength);
                length=Mathf.Clamp(silk-overLength-acrossLength,0,length);
                bool hanging=length>.001f;
                if(m.Cloth.gameObject.activeSelf!=hanging){m.Cloth.gameObject.SetActive(hanging);m.Outline.gameObject.SetActive(hanging);}
                if(m.Badge&&m.Badge.gameObject.activeSelf!=length>.03f)m.Badge.gameObject.SetActive(length>.03f);
                if(!hanging)continue;
                if(!Mathf.Approximately(m.Cloth.localScale.y,length))
                {
                    m.Cloth.localScale=new Vector3(Width,length,1);m.Cloth.localPosition=new Vector3(0,-length*.5f,0);
                    m.Outline.transform.localScale=new Vector3(Width+.006f,length+.004f,1);m.Outline.transform.localPosition=new Vector3(0,-length*.5f,.0015f);
                    if(m.Badge)m.Badge.localPosition=new Vector3(0,-length+.022f,-.0008f);
                }
                // A slow swing about the rail; still while a hand or a look is on it, or it is lifted. Stirred by a hand near:
                // lifted a little and fluttering, twisting as silk does in a draught.
                if(!hover&&m.Raised==0)m.Swing=Sway*Mathf.Sin(now*Mathf.PI*2/SwaySeconds+m.Phase);
                m.Tempt=Mathf.MoveTowards(m.Tempt,m.Lure&&!held&&m.Grow>=1?1:0,dt/.25f);
                float stir=m.Tempt*(TemptLift+TemptFlutter*(.7f*Mathf.Sin(now*Mathf.PI*2*2.4f+m.Phase)+.3f*Mathf.Sin(now*Mathf.PI*2*5.3f+m.Phase*2)));
                float twist=m.Tempt*7*Mathf.Sin(now*Mathf.PI*2*1.6f+m.Phase);
                m.Hang.localRotation=Quaternion.Euler((m.Raised==0?m.Swing:0)+m.Raised+stir,twist,0);
                m.Outline.enabled=hover;if(hover)MemoryUi.Tint(m.Outline,held?MemoryPalette.AquaGrab:MemoryPalette.AquaHover);
            }
        }
        // A span of the silk shown from its start as far as reach (metres) along it.
        static void Reveal(Transform span,Vector3 from,Vector3 to,float reach)
        {
            float full=Vector3.Distance(from,to),shown=Mathf.Clamp(reach,0,full);bool on=shown>.0005f;
            if(span.gameObject.activeSelf!=on)span.gameObject.SetActive(on);
            if(!on)return;
            span.localPosition=from+(to-from).normalized*(shown*.5f);span.localScale=new Vector3(Width,shown,1);
        }
        // Woven silk, white (each ribbon tints it): fine ribs across, a sheen down one side, a stitched hem along both
        // edges, a crease at the top where it bends over the rail; with tail, a swallowtail cut into its foot (alpha, for the
        // icon material's clip).
        static Texture2D Silk(bool withTail)
        {
            const int W=64,H=256;var t=new Texture2D(W,H,TextureFormat.RGBA32,true){name=withTail?"Ribbon silk tail":"Ribbon silk",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Trilinear,anisoLevel=4};
            var pixels=new Color32[W*H];var random=new System.Random(withTail?31:32);
            for(int y=0;y<H;y++)for(int x=0;x<W;x++)
            {
                float u=(x+.5f)/W,v=(y+.5f)/H,edge=Mathf.Min(u,1-u);
                float rib=.94f+.06f*Mathf.Cos(y*Mathf.PI*2/3f);
                float sheen=1+.12f*Mathf.Exp(-Mathf.Pow((u-.32f)/.11f,2))-.08f*Mathf.Exp(-Mathf.Pow((u-.82f)/.14f,2));
                float stitch=edge>.075f&&edge<.105f&&(y/5)%2==0?.74f:1;
                float border=edge<.045f?.86f:1;
                float crease=v>.93f?Mathf.Lerp(1,.78f,(v-.93f)/.07f):1;
                float grain=1+((float)random.NextDouble()-.5f)*.05f;
                float c=Mathf.Clamp01(rib*sheen*stitch*border*crease*grain);
                bool cut=withTail&&v<.15f*(1-Mathf.Abs(u-.5f)*2);
                pixels[y*W+x]=new Color(c,c,c,cut?0:1);
            }
            t.SetPixels32(pixels);t.Apply(true,true);return t;
        }
    }
}
