using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CloudGarden.Integration;
using CloudGarden.ShadowPuzzle;
using PhysicalAssembly.MetaXR;
using Oculus.Interaction;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace ShapeOfMemory
{
    // Shape of Memory: the four posable-pile levels (Torch2Level) as one game in one scene, played in a book (MemoryBook).
    // The book lies on a reading stand in front of the player; its pages are the only way through the game:
    // - Closed at first (Cover): the title on its cover. At every launch the title comes up alone in the dark, the book
    //   comes up round it in the air, then comes down to the stand as the dark lifts;
    //   pinching its cover's edge (or pressing it, or looking at it and pinching) opens it.
    // - Spread 0 is the endpaper (Title): how the hands work, the table height, "Turn the page to begin". Then one spread
    //   per memory (Levels): turned to the next memory not yet found, the book waits Dwell, then rises; turning on cancels.
    //   A memory already found has PLAY AGAIN. You cannot turn past a memory not yet found. Then the ending.
    // - Rising, the book flies up and opens onto the lamps' walls (MemoryBook.Walls); the level's things grow on the table
    //   and fly into the orb (MemoryIntro); Playing, the shadow falls on its pages, the riddle and hints on the page the
    //   lamp leaves dark. Three buttons by the right hand: HINT, HOLD TO RESET, MENU.
    // - MENU folds the orb away and brings the book down to the stand (Paused): RESUME, the table height, or turn back to
    //   leave the memory.
    // - Solved: the found moment holds FoundHold, the things go back to the table and stay, the book comes down open at the
    //   memory, its picture printed (Found); its page beckons, and turning it moves on (or PLAY AGAIN).
    // A level swaps in place, so the player rig and passthrough are never reloaded. The scene owns its player.
    public sealed class MemoryGame : MonoBehaviour, IShadowHost
    {
        public enum Phase{Cover,Title,Levels,Rising,Intro,Playing,Lowering,Paused,Solved,Found,Leaving,Ending}
        public const string GameTitle="SHAPE OF MEMORY";
        // Seconds: the found moment holds FoundHold before its things go back to the table (3 s was too short to take in
        // the found shadow). The book flies up RiseSeconds to start a memory and down LowerSeconds after one; a pause flies
        // PauseSeconds each way while the orb folds FoldSeconds. A memory waiting its turn starts Dwell after its page lands.
        public const float FoundHold=6,RiseSeconds=1.5f,LowerSeconds=1.2f,PauseSeconds=.8f,FoldSeconds=.4f,Dwell=1.2f;
        // The title, at every launch (Cover): TitleDark of dark, the title comes up in it over TitleFade and holds TitleHold;
        // the closed book comes up round it over Emerge (the title is its cover's), waits EmergeHold, then comes down onto
        // the stand over DescendSeconds while the dark lifts off the room over LightsUp.
        public const float TitleDark=.6f,TitleFade=2,TitleHold=3,Emerge=1.8f,EmergeHold=.8f,DescendSeconds=2.2f,LightsUp=2;
        public static float TitleSeconds=>TitleDark+TitleFade+TitleHold+Emerge+EmergeHold+Mathf.Max(DescendSeconds,LightsUp);
        // Lift: one LOWER / RAISE step.
        public const float HoldToReset=1f,Lift=.025f,RowPitch=.115f,StandShare=.16f;
        // The objects and their rings are drawn this much smaller than the pile their shadow is cast from (playtest
        // 2026-11-08: "smaller cluster, same shadow"). Only the drawing: Torch2Projection keeps the pile's own size, so the
        // shadows, the score and every level stay as they were. The rings' reach shrinks with them (Torch2Input.OrbScale).
        public const float PileScale=.7f;
        // Placement, from the head at start (and after the headset's recenter): the orb's middle OrbAhead in front of the
        // eyes and OrbBelow under them (its rings then pass about 30 cm from the eyes, and the hand rolls it with the elbow
        // bent), everything else with it; the book's middle on its stand ReadAhead in front and ReadBelow under the eyes,
        // tilted ReadTilt toward the reader. The table top stays MinTable..MaxTable over the floor.
        public const float OrbAhead=.60f,OrbBelow=.30f,ReadAhead=.40f,ReadBelow=.33f,ReadTilt=30,MinTable=.40f,MaxTable=1.10f;
        public static float StageAhead=>OrbAhead-Torch2Projection.Home.z;
        public static float StageBelow=>OrbBelow+Torch2Projection.Home.y;
        // The buttons while playing (stage space): an upright panel at the book's right, about 28 cm ahead of and 44 cm right
        // of the eyes (57 degrees round), its middle 25 cm under them, turned to them; clear of the objects' rings, and from
        // the eyes clear of the dark page's text and the first level's teaching label (both end by about 50 degrees).
        public static Vector3 StripAt=>new Vector3(.44f,StageBelow-.25f,.28f-StageAhead);
        public static Quaternion StripFacing
        {
            get
            {
                var eye=new Vector3(0,StageBelow,-StageAhead);var toward=(eye-StripAt).normalized;
                return Quaternion.LookRotation(Vector3.ProjectOnPlane(Vector3.up,toward).normalized,toward);
            }
        }
        [Tooltip("Player prefab. The Pico build swaps in its own (PicoBuild.OnProcessScene looks for this field name).")]
        public GameObject StandalonePlayer;
        public Material Surface,Screen,Glow,IconMaterial;
        [Tooltip("URP Unlit, transparent (alpha blended): the orb's rings, the pages' turning edges and the dark before the title. Empty: those are opaque and there is no dark.")]
        public Material SeeThrough;
        [Tooltip("The sky that fades in once the book is first opened (MemorySky); empty: the room stays.")]
        public Material SkyMaterial;
        [Tooltip("Where that sky's pink glow is in its own cubemap (degrees round from +Z toward +X, at _Rotation 0): WindwardEveningSky 50, MagicHourSky 120. The dome is turned so the glow sits beyond the book.")]
        public float SkyGlowAzimuth=50;
        [Tooltip("White-on-transparent button icons, found by file name (Tools/ShadowArt/build_icons.py).")]
        public Texture2D[] Icons=Array.Empty<Texture2D>();
        [Tooltip("Pico rig: draw the tracked joints as bones (MemoryHands) instead of the SDK's see-through hands. On: after the 2026-11-08 playtest the see-through hands came back for one round, then the user asked for the bones again.")]
        public bool JointHands=true;
        [Tooltip("How far past the drawn rings a pinch still takes the orb (metres; Torch2Input.ReachMargin). The gym keeps 0.06; raised here after the 2026-11-08 playtest found the smaller rings hard to catch.")]
        public float RingReach=.10f;
        // Tests keep progress in memory instead of PlayerPrefs. Cleared on entering Play, since domain reload is off.
        public static MemoryProgress.IStore StoreOverride;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetOverride(){StoreOverride=null;}

        public MetaPlayerRig Player{get;private set;}
        public MemoryProgress Progress{get;private set;}
        public Phase Current{get=>current;private set{if(current!=value)Debug.Log($"SHAPE_OF_MEMORY_PHASE {current}>{value}");current=value;}}
        Phase current;
        public int LevelIndex{get;private set;}=-1;
        public Torch2Workshop Level{get;private set;}
        public int HintStage{get;private set;}
        public Transform Stage{get;private set;}
        public bool Ready{get;private set;}
        public string ShownSet{get;private set;}
        public bool Busy=>Level&&Level.Busy;
        public MemoryBook Book{get;private set;}
        // The book's levels in order (null for an empty slot), and whether a launch made it a book of one level.
        public Torch2Level Chapter(int index)=>index>=0&&index<chapters.Length?chapters[index]:null;
        public bool OwnBook{get;private set;}
        Torch2Level[] chapters=Array.Empty<Torch2Level>();
        // A level's spread, and the ending's.
        public static int SpreadOf(int level)=>level+1;
        public int EndingSpread=>Progress.Count+1;
        // The open pages' words (tests, logs).
        public string PageText(bool right)=>string.Join("\n",(right?Book.RightFace:Book.LeftFace).GetComponentsInChildren<TMP_Text>().Select(t=>t.text));
        public MemoryTile Tile(string id)=>tiles.TryGetValue(id,out var tile)&&tile&&tile.Press!=null?tile:null;
        // How the player is playing, for the words that say what to do.
        public MemoryGuide.Input InputMode=>!Player||!Player.IsXR?MemoryGuide.Input.Desktop:Gaze&&Gaze.Available?MemoryGuide.Input.Gaze:MemoryGuide.Input.Hands;
        MetaShadowPlayer xr;
        readonly MemoryKit kit=new MemoryKit(),roomKit=new MemoryKit();
        MemoryPages pages;
        Transform strip,stand,idleRoom;
        TMP_Text coverPrompt;
        Torch2Room idle,stageRoom;
        GameObject slab;
        readonly Dictionary<string,GameObject> sets=new Dictionary<string,GameObject>();
        readonly Dictionary<string,MemoryButton> buttons=new Dictionary<string,MemoryButton>();
        readonly Dictionary<string,MemoryTile> tiles=new Dictionary<string,MemoryTile>();
        AudioSource audioSource;
        AudioClip click;
        float solvedAt,nextReport,waited,foldFrom=1,foldTo=1,foldTime,foldSeconds;
        bool settling;
        MemorySounds sounds;
        public MemoryIntro Intro{get;private set;}
        public MemoryGuide Guide{get;private set;}
        // The memory-found moment over the orb (null once it has played).
        public MemoryFound Found{get;private set;}
        public MemoryGaze Gaze{get;private set;}
        // Fingertip presses (XR only).
        public MemoryTouch Touch{get;private set;}
        // System recenter: the frame to re-place the table on (-1 = none), and the head pose last frame.
        int recenterFrame=-1;
        Pose lastHead;
        bool headSeen;
        readonly List<XRInputSubsystem> inputs=new List<XRInputSubsystem>();

        sealed class Spec
        {
            public readonly string id,caption,icon;public readonly Action action;public readonly Func<bool> enabled;public readonly float hold;
            public Spec(string id,string caption,string icon,Action action,Func<bool> enabled=null,float hold=0){this.id=id;this.caption=caption;this.icon=icon;this.action=action;this.enabled=enabled;this.hold=hold;}
        }

        IEnumerator Start()
        {
            MemoryLog.Begin();
            // The chapters (MemoryChapters). Build and Run can start at a level (launch argument "level"): one of the book's
            // opens straight into its chapter; any other Torch2Level, such as a designer's fresh from the gym, plays as a
            // book of one chapter.
            string launch=PhysicalAssembly.LaunchArguments.Get("level");
            var book=MemoryChapters.Load();chapters=book?book.chapters.ToArray():Array.Empty<Torch2Level>();
            if(!book)Debug.LogError("SHAPE_OF_MEMORY_CHAPTERS missing: Resources/"+MemoryChapters.ResourcePath+".asset");
            var own=!string.IsNullOrEmpty(launch)&&!chapters.Any(c=>c&&c.name==launch)?Torch2Level.Load(launch):null;
            OwnBook=own;if(own)chapters=new[]{own};
            else if(book)foreach(var problem in book.Check())if(problem.Error)Debug.LogError("SHAPE_OF_MEMORY_CHAPTERS "+problem);else Debug.LogWarning("SHAPE_OF_MEMORY_CHAPTERS "+problem);
            Progress=new MemoryProgress(StoreOverride??new MemoryProgress.Prefs(),chapters.Select(c=>c?c.name:"").ToArray());
            foreach(var k in new[]{kit,roomKit}){k.Surface=Surface;k.Screen=Screen;k.Icon=IconMaterial;k.SeeThrough=SeeThrough;}
            if(!SeeThrough)Debug.LogWarning("SHAPE_OF_MEMORY no SeeThrough material: rings and edges opaque, no dark before the title");
            pages=new MemoryPages(kit);
            var root=Instantiate(StandalonePlayer);Player=root.GetComponentInChildren<MetaPlayerRig>(true);Player.Initialize(Player.XRRig,Player.DesktopCamera);
            xr=gameObject.AddComponent<MetaShadowPlayer>();xr.Player=Player;xr.Host=this;
            audioSource=gameObject.AddComponent<AudioSource>();audioSource.spatialBlend=0;audioSource.volume=.35f;click=MemoryUi.Click(kit.Owned);
            MemoryTile.HoverTick=()=>{if(audioSource)audioSource.PlayOneShot(click,.12f);};
            sounds=new MemorySounds(kit.Owned);
            var room=gameObject.AddComponent<AudioSource>();room.clip=sounds.Room;room.loop=true;room.spatialBlend=0;room.volume=.07f;room.Play();
            if(Player.IsXR&&xr.UnityXRRig)SilenceMrukTicker();
            Stage=new GameObject("Stage").transform;Stage.SetParent(transform,false);
            BuildStand();BuildStrip();BuildBook();IdleRoom();
            Sky=MemorySky.Add(gameObject,SkyMaterial,SeeThrough,SkyGlowAzimuth,Player.ViewCamera,Stage,kit.Owned);
            int start=string.IsNullOrEmpty(launch)?-1:Progress.IndexOf(launch);
            // The title (Cover) starts in the dark: nothing of the table shows while the headset settles.
            if(start<0){Sky.Dark=1;Stage.gameObject.SetActive(false);}
            if(Player.IsXR)
            {
                // Hands only, no rays, and fingertips press through MemoryTouch rather than the rig's pokes.
                Debug.Log("SHAPE_OF_MEMORY_HANDS_ONLY trackers rewired="+MetaShadowPlayer.HandsOnly(Player.XRRig)+" hand rays off="+MetaShadowPlayer.NoHandRays(Player.XRRig)+" hand pokes off="+MetaShadowPlayer.NoHandPokes(Player.XRRig));
                Touch=MemoryTouch.Add(gameObject,xr);
                float deadline=Time.realtimeSinceStartup+3;
                while(Player.ViewCamera.transform.position.y-Player.XRRig.position.y<.4f&&Time.realtimeSinceStartup<deadline)yield return null;
                SubsystemManager.GetSubsystems(inputs);foreach(var input in inputs)input.trackingOriginUpdated+=OnTrackingOriginUpdated;
                if(xr.UnityXRRig&&JointHands)ShowRealHands();
                Debug.Log("SHAPE_OF_MEMORY_HAND_VISUALS "+(xr.UnityXRRig&&JointHands?"joints (MemoryHands)":"SDK see-through hands"));
                Trace=MemoryTrace.Add(gameObject,Player.XRRig);
                Gaze=MemoryGaze.Add(this,xr);
            }
            else{Player.ViewCamera.fieldOfView=48;Player.ViewCamera.backgroundColor=new Color(.045f,.075f,.09f);}
            Recenter();
            if(start>=0){Book.Place(Book.Stand(180),MemoryBook.Where.Stand);Book.Show(SpreadOf(start));Sky.Show(true);Load(start,true);}
            else StartCoroutine(Cover());
            StartCoroutine(Prewarm());
            Ready=true;
            Debug.Log($"SHAPE_OF_MEMORY_READY xr={Player.IsXR} rig={(Player.IsXR?xr.UnityXRRig?"unityxr":"ovr":"desktop")} inputs={inputs.Count} solved={Progress.SolvedCount}");
            if(!string.IsNullOrEmpty(launch))Debug.Log("SHAPE_OF_MEMORY_LAUNCH level="+launch+" "+(start<0?"unknown (no such Torch2Level in Resources): title":$"chapter={start+1}/{Progress.Count} own={own}"));
        }
        // On the Pico rig the SDK's hand mesh, fitted to Meta's skeleton, sits off the real hands that passthrough already
        // shows. With JointHands (the default), hide it and draw the tracked joints themselves (MemoryHands).
        void ShowRealHands()
        {
            foreach(var visual in Player.XRRig.GetComponentsInChildren<HandVisual>(true))visual.ForceOffVisibility=true;
            Hands=MemoryHands.Add(gameObject,xr,Surface,Glow,kit.Owned);
        }
        public MemoryHands Hands{get;private set;}
        // Passthrough until the book is first opened, then the magic-hour sky.
        public MemorySky Sky{get;private set;}
        // The rig's interactors in the device log (XR only).
        public MemoryTrace Trace{get;private set;}

        // ---- The cover
        // Every launch opens on the title (TitleSeconds; the user's call, 2026-11-08: not only the first time, as it was):
        // in the dark (MemorySky.Dark: the room hidden), the gold title
        // comes up where the closed book hangs (Presented) and holds; the book comes up out of the dark round it, the title
        // now its cover's; then it comes down onto the stand as the dark lifts off the room and the table and the stand come
        // up. Things come up from black (MemoryShade), which against the dark is unseen. The cover cannot be opened until
        // the book has landed (MemoryBook.Sealed). A launch into a chapter skips it. Opening the cover (MemoryBook.Opened)
        // goes on in CoverOpened.
        // The title is still on: FinishTitle (tests, the development K key) or a level loaded over it cuts it short.
        bool Titling=>Current==Phase.Cover&&!titleCut;
        bool titleCut;
        public void FinishTitle(){if(Current==Phase.Cover&&Book.Sealed)titleCut=true;}
        IEnumerator Cover()
        {
            Current=Phase.Cover;Book.Show(0);Show(null);
            Book.Sealed=true;Book.Place(Presented,MemoryBook.Where.Presented);Stage.gameObject.SetActive(true);
            var title=new MemoryShade().Add(Book.CoverFace);
            var book=new MemoryShade().Add(Book.transform,t=>t.IsChildOf(Book.CoverFace));
            var room=new MemoryShade().Add(stand).Add(idleRoom);
            // A black lit surface still shows the key light's specular and the sky's reflection (about 4% linear: a grey in
            // sRGB, measured 2026-11-08), so both stay off until the room comes up; the book comes up in the ambient alone.
            var keys=FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l=>l.type==LightType.Directional).Select(l=>(light:l,intensity:l.intensity)).ToArray();
            float reflections=RenderSettings.reflectionIntensity;
            void Lights(float k){foreach(var key in keys)if(key.light)key.light.intensity=key.intensity*k;RenderSettings.reflectionIntensity=reflections*k;}
            title.Set(0);book.Set(0);room.Set(0);Sky.Dark=1;Lights(0);Debug.Log("SHAPE_OF_MEMORY_TITLE");
            yield return Ramp(TitleDark,null);
            yield return Ramp(TitleFade,title.Set);
            yield return Ramp(TitleHold,null);
            yield return Ramp(Emerge,book.Set);
            yield return Ramp(EmergeHold,null);
            if(Titling&&Book.At==MemoryBook.Where.Presented&&!Book.Flying)Book.Fly(Book.Stand(0),MemoryBook.Where.Stand,DescendSeconds,null,.05f);
            yield return Ramp(LightsUp,k=>{room.Set(k);Sky.Dark=1-k;Lights(k);});
            while(Titling&&Book.Flying)yield return null;
            if(Current==Phase.Cover&&!Book.OnStand)Book.Place(Book.Stand(0),MemoryBook.Where.Stand);
            title.Set(1);book.Set(1);room.Set(1);Sky.Dark=0;Lights(1);Book.SetBrightness(Book.Brightness);Book.Sealed=false;titleCut=false;
        }
        // From 0 to 1 over seconds, eased (at once if the title has been cut short); null set just waits.
        IEnumerator Ramp(float seconds,Action<float> set)
        {
            for(float t=0;t<seconds&&Titling;t+=Time.unscaledDeltaTime){set?.Invoke(MemoryIntro.Smooth(t/seconds));yield return null;}
            set?.Invoke(1);
        }
        // Where the closed book hangs in the title: upright, its cover to the player, about a metre ahead at eye height.
        public static MemoryBook.Placement Presented
        {
            get
            {
                const float scale=.45f;var rotation=Quaternion.Euler(-90,0,0);
                var centre=new Vector3(0,StageBelow,1f-StageAhead);
                return new MemoryBook.Placement(centre-rotation*new Vector3(MemoryBook.Width*.5f*scale,0,MemoryBook.Height*.5f*scale),rotation,scale,0);
            }
        }
        // The found pictures are drawn once each (MemoryPages.Picture, tens of milliseconds): do it while the cover waits,
        // one a frame, so turning to a found memory or the ending never stalls.
        IEnumerator Prewarm()
        {
            for(int i=0;i<Progress.Count;i++)
            {
                yield return null;var level=Chapter(i);if(!level)continue;
                var clock=System.Diagnostics.Stopwatch.StartNew();pages.Picture(level);long ms=clock.ElapsedMilliseconds;
                if(level.lamps.Length>1)for(int slot=0;slot<level.lamps.Length;slot++){yield return null;pages.Picture(level,slot);}
                Debug.Log($"SHAPE_OF_MEMORY_PICTURE {level.name} {MemoryPages.PictureSize} px in {ms} ms");
            }
        }
        void CoverOpened()
        {
            Book.Refill();Sky.Show();
            int mark=Progress.SolvedCount>0&&Progress.Continue>=0?SpreadOf(Progress.Continue):0;
            if(mark==0){SetPhaseFor(0);return;}
            Current=Phase.Levels;Book.TurnTo(mark,()=>SetPhaseFor(Book.Spread));
        }
        // The phase for a spread the book stands open at with no memory out.
        void SetPhaseFor(int spread)
        {
            waited=0;Book.Anticipate=0;
            var phase=spread==0?Phase.Title:spread==EndingSpread?Phase.Ending:Phase.Levels;
            if(phase==Phase.Ending&&Current!=Phase.Ending){audioSource.PlayOneShot(sounds.Ending,.6f);Debug.Log("SHAPE_OF_MEMORY_ENDING");}
            Current=phase;
        }

        // ---- A memory
        // From a chapter's page (its wait, or PLAY AGAIN): the memory starts.
        void Begin(int index)
        {
            if(Busy||Book.Turning||!Book.OnStand)return;
            if(Book.Spread!=SpreadOf(index)){Book.TurnTo(SpreadOf(index),()=>Load(index));return;}
            Load(index);
        }
        // force: open a locked level (development keys and tests). The book rises onto the walls, then the intro plays.
        public void Load(int index,bool force=false)
        {
            if(Busy||index<0||index>=Progress.Count||!(force||Progress.Unlocked(index)))return;
            var asset=Chapter(index);if(!asset)return;
            ClearLevel();DestroyIdleRoom();
            var content=new GameObject(asset.name+" memory");content.SetActive(false);content.transform.SetParent(Stage,false);
            var game=content.AddComponent<Torch2Workshop>();game.bare=true;game.level=asset;game.viewCamera=Player.ViewCamera;game.xr=xr;game.roomStyle=room=>StyleStage(room,game);game.hostShowsPictures=true;
            game.surfaceMaterial=Surface;game.screenMaterial=Screen;game.glowMaterial=Glow;
            // Far pieces drawn nearer the middle where the level asks (Torch2Level.drawGather; Home's two pictures spread its pile).
            game.drawGathered=true;
            content.SetActive(true);game.input.FixedInPlace=true;
            // Turned close enough while still held, the orb snaps in and the memory is found (a turn that overshot the
            // answer never counted before).
            game.SnapWhileHeld=true;
            // Drawn PileScale smaller (the shadow keeps the pile's own size), before the intro takes the parts' sizes.
            game.world.Pile.localScale=Vector3.one*PileScale;game.input.OrbScale=PileScale;game.input.ReachMargin=RingReach;
            // No wires to a hub and no hub: the things float free inside the rings (playtest 2026-11-08). The hub's
            // transform stays as the orb's middle (MemoryFound).
            game.world.RemoveWires();game.world.Hub.enabled=false;
            Rings(game.world);
            Level=game;LevelIndex=index;HintStage=0;Guide=null;Found=null;settling=false;foldFrom=foldTo=1;
            stageRoom?.ShowWalls(false);Current=Phase.Rising;Show(null);
            Intro=MemoryIntro.Play(game,Progress.SeenIntro(index),sounds);Intro.Frozen=true;Intro.Seek(0);
            if(!Book.OnStand)Book.Place(Book.Stand(180),MemoryBook.Where.Stand);
            if(Book.Spread!=SpreadOf(index))Book.Show(SpreadOf(index));else Book.Refill();
            Book.Fly(MemoryBook.Walls,MemoryBook.Where.Walls,RiseSeconds,()=>{if(Level!=game)return;Book.Refill();stageRoom?.ShowWalls(true);if(Intro)Intro.Frozen=false;Current=Phase.Intro;});
        }
        // The orb's rings are light strokes: unlit and see-through (MemoryPalette.StrokeRest; opaque they were too loud),
        // aqua by state (Idle, Hover with a hand near enough to roll them, Grab while rolling), firmer as a hand comes, each
        // with an Ink800 rim: an inverted hull, the ring's tube RimWidth fatter, drawn from its back faces only (MemoryKit.Hull).
        Mesh ringRim;
        void Rings(Torch2World world)
        {
            foreach(var part in world.OrbParts)
            {
                if(!part||part==world.Hub.transform)continue;var filter=part.GetComponent<MeshFilter>();if(!filter)continue;
                part.GetComponent<Renderer>().sharedMaterial=kit.Stroke;
                if(!ringRim)ringRim=Fatter(filter.sharedMesh,MemoryPalette.RimWidth);
                var rim=new GameObject("Ring rim",typeof(MeshFilter),typeof(MeshRenderer));rim.transform.SetParent(part,false);rim.GetComponent<MeshFilter>().sharedMesh=ringRim;
                var r=rim.GetComponent<MeshRenderer>();r.sharedMaterial=kit.Hull;MemoryUi.Tint(r,MemoryPalette.Alpha(MemoryPalette.Ink800,MemoryPalette.RimAlpha));
                r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;r.receiveShadows=false;
            }
            world.RingColors(MemoryPalette.Alpha(MemoryPalette.AquaIdle,MemoryPalette.StrokeRest),MemoryPalette.Alpha(MemoryPalette.AquaHover,MemoryPalette.StrokeLit),MemoryPalette.Alpha(MemoryPalette.AquaGrab,MemoryPalette.StrokeHeld));
        }
        // A copy of the mesh with every vertex pushed out along its normal (a ring's tube that much fatter).
        Mesh Fatter(Mesh mesh,float by)
        {
            var vertices=mesh.vertices;var normals=mesh.normals;for(int i=0;i<vertices.Length;i++)vertices[i]+=normals[i]*by;
            var fat=new Mesh{name=mesh.name+" rim"};fat.SetVertices(vertices);fat.SetNormals(normals);fat.SetTriangles(mesh.triangles,0);fat.RecalculateBounds();kit.Owned.Add(fat);return fat;
        }
        void Hint()
        {
            if(Current!=Phase.Playing||HintStage>=MemoryProgress.MaxHints)return;
            HintStage++;Progress.UsedHints(LevelIndex,HintStage);Book.Refill();buttons["play/hint"].PipsLit=MemoryProgress.MaxHints-HintStage;
        }
        void ResetOrb(){if(Level&&!Level.Busy&&!Level.Completed)Level.ResetChallenge();}
        // Jump to the end of the running intro (tests and the development K key; players skip with a pinch once seen).
        public void FinishIntro(){if(Intro&&Intro.Running){Intro.Frozen=false;Intro.Finish();}}
        // Meta's MR Utility Kit ticks a hidden global context every frame through OVRPlugin, which the Pico build strips:
        // a DllNotFoundException every frame. Nothing here uses MRUK, so switch the ticker off (destroying it makes MRUK
        // recreate it).
        static void SilenceMrukTicker()
        {
            var ticker=GameObject.Find("MRUKGlobalContext");
            if(ticker)foreach(var behaviour in ticker.GetComponents<MonoBehaviour>())behaviour.enabled=false;
            Debug.Log("SHAPE_OF_MEMORY_MRUK silenced="+(ticker!=null));
        }
        // MENU: the lamps go out, the orb folds away and the book comes down to the stand, open at the memory.
        void Pause()
        {
            if(Current!=Phase.Playing||Busy)return;
            Current=Phase.Lowering;Level.input.enabled=false;Level.world.Hover=Torch2Input.Grip.None;Show(null);
            Fold(0);Level.world.LampLevel=0;stageRoom?.ShowWalls(false);Book.Refill();
            Book.Fly(Book.Stand(180),MemoryBook.Where.Stand,PauseSeconds,()=>{Current=Phase.Paused;Book.Refill();});
        }
        // RESUME: the book goes back up onto the walls, the orb unfolds and the lamps come on.
        void Resume()
        {
            if(Current!=Phase.Paused||!Book.OnStand)return;
            Current=Phase.Rising;Book.Refill();var game=Level;
            Book.Fly(MemoryBook.Walls,MemoryBook.Where.Walls,PauseSeconds,()=>
            {
                if(Level!=game)return;
                stageRoom?.ShowWalls(true);Level.world.LampLevel=1;Fold(1);Level.input.enabled=true;Current=Phase.Playing;Show("play");Book.Refill();
            });
        }
        // After a solve, once its things are back on the table: the book comes down open at the found memory.
        void Lower()
        {
            Current=Phase.Lowering;stageRoom?.ShowWalls(false);Book.Refill();
            Book.Fly(Book.Stand(180),MemoryBook.Where.Stand,LowerSeconds,()=>{Current=Phase.Found;Book.Refill();});
        }
        // A page turned away from a paused or found memory: what is left of it shrinks away; on to the spread it landed on.
        void LeaveTo(int spread)
        {
            Current=Phase.Leaving;Show(null);
            MemoryOutro.Shrink(Level,.45f,()=>{ClearLevel();IdleRoom();SetPhaseFor(spread);Book.Refill();});
        }
        // PLAY AGAIN: a found memory's things shrink away and it starts over; from a chapter's page it just starts.
        void Replay(int index)
        {
            if(!Level){Begin(index);return;}
            if(Busy||!Book.OnStand)return;
            Current=Phase.Leaving;Show(null);
            MemoryOutro.Shrink(Level,.35f,()=>{ClearLevel();Load(index,true);});
        }
        // The orb folds into its hub (0) or opens out again (1).
        void Fold(float to){if(!Level)return;foldFrom=Level.world.Pile.localScale.x/PileScale;foldTo=to;foldTime=0;foldSeconds=FoldSeconds;}
        void Update()
        {
            if(!Ready)return;
            float dt=Time.unscaledDeltaTime;
            // A memory waiting its turn: the book lifts a little, then rises (turning the page on cancels).
            if(Current==Phase.Levels&&!Level&&Book.OnStand&&!Book.Turning&&!Book.Dragging)
            {
                int c=Book.Spread-1;
                if(c>=0&&c<Progress.Count&&c==Progress.Continue&&Progress.Unlocked(c)){waited+=dt;Book.Anticipate=waited/Dwell;if(waited>=Dwell){waited=0;Book.Anticipate=0;Begin(c);}}
                else{waited=0;Book.Anticipate=0;}
            }
            else if(Current==Phase.Levels){waited=0;Book.Anticipate=0;}
            if(Current==Phase.Intro&&Level&&!Intro.Running)
            {
                Current=Phase.Playing;Progress.SawIntro(LevelIndex);Show("play");
                Guide=MemoryGuide.Add(Level,LevelIndex==0&&!OwnBook&&!Progress.Taught,InputMode,Glow,()=>{Progress.Taught=true;Book.Refill();},()=>HintStage>0);
            }
            if(Current==Phase.Playing&&Level&&Level.Completed)
            {
                Current=Phase.Solved;solvedAt=Time.unscaledTime;Progress.Solve(LevelIndex);Show(null);Book.Refill();
                Found=MemoryFound.Play(Level,Player.ViewCamera.transform,kit,Glow,sounds.Found);
            }
            // The found moment held, and no hand still on the pile: its things go back to the table, then the book comes down.
            if(Current==Phase.Solved&&!settling&&!Busy&&Time.unscaledTime>=solvedAt+FoundHold){settling=true;var game=Level;MemoryOutro.Play(Level,sounds,()=>{if(Level==game)Lower();},true);}
            if(Level&&foldTime<foldSeconds)
            {
                foldTime+=dt;float t=Mathf.Clamp01(foldTime/foldSeconds),e=t*t*(3-2*t);
                Level.world.Pile.localScale=Vector3.one*PileScale*Mathf.Max(.001f,Mathf.Lerp(foldFrom,foldTo,e));
            }
            // The paper darkens to the walls' unlit colour (MemoryBook.Dark) as the book rises onto them, and back as it comes down.
            float shade=1-Book.Up;if(!Mathf.Approximately(Book.Brightness,shade))Book.SetBrightness(shade);
            // The stand sinks into the table as the book rises off it, and comes up under it as it lands: it moves only
            // while the book is within StandShare of the stand, both ways. Measured 2026-11-08 along the flight (walls <->
            // stand, arc .12): the book's underside sweeps through the stand's space from about 30% of the way out, so a
            // stand up by half way down (the earlier schedule) cut through it; at StandShare .16 no point of the book enters it.
            float up=Mathf.Clamp01(Book.Up/StandShare);
            stand.gameObject.SetActive(up<.999f);stand.localScale=new Vector3(1,Mathf.Max(.001f,1-up),1);
            coverPrompt.gameObject.SetActive(Current==Phase.Cover&&Book.OnStand&&Book.Closed);
            if(coverPrompt.gameObject.activeSelf){coverPrompt.text=CoverWords;if(promptFitted!=coverPrompt.text)FitPromptCard();}
            if(Player.IsXR){FollowSystemRecenter();WatchHands();}
            if(Player.IsXR&&Time.unscaledTime>=nextReport){nextReport=Time.unscaledTime+10;Debug.Log($"SHAPE_OF_MEMORY_HANDS phase={Current} spread={Book.Spread} set={ShownSet??"none"} book={Book.At}{(Book.Flying?"(flying)":"")} gaze={(Gaze&&Gaze.Available?"on":"off")} {xr.Report()} {(Trace?Trace.Summary():"")}");}
            DesktopClick();
            MemoryLog.Tick();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DevelopmentKeys();
#endif
        }
        string CoverWords=>InputMode==MemoryGuide.Input.Gaze?"LOOK AT THE BOOK AND PINCH\n<size=70%>or pinch its cover's edge and open it</size>":
            InputMode==MemoryGuide.Input.Hands?"PINCH THE COVER'S "+MemoryPalette.Colored("GLOWING EDGE",MemoryPalette.AquaHover)+"\n<size=70%>and open the book</size>":"CLICK THE BOOK TO OPEN IT";

        // Device log: where a hand was when tracking lost it (degrees below and right of the view, metres from the eyes)
        // and how long it stayed lost. Low, close work at the book can sit at the edge of the headset's hand tracking.
        readonly bool[] handSeen=new bool[2];
        readonly float[] handLostAt=new float[2];
        readonly Vector3[] lastTip=new Vector3[2];
        void WatchHands()
        {
            var head=Player.ViewCamera.transform;float now=Time.unscaledTime;
            for(int hand=0;hand<2;hand++)
            {
                string side=hand==0?"left":"right";
                if(xr.ReadHand(hand==0,out _,out var tip,out _))
                {
                    if(!handSeen[hand]&&handLostAt[hand]>0)Debug.Log($"SHAPE_OF_MEMORY_HAND {side} back after {now-handLostAt[hand]:0.00}s");
                    lastTip[hand]=tip;handSeen[hand]=true;continue;
                }
                if(!handSeen[hand])continue;
                handSeen[hand]=false;handLostAt[hand]=now;
                var local=Quaternion.Inverse(head.rotation)*(lastTip[hand]-head.position);
                Debug.Log($"SHAPE_OF_MEMORY_HAND {side} lost ({xr.HandProblem(hand==0)}), last at {Mathf.Atan2(-local.y,local.z)*Mathf.Rad2Deg:0} deg below and {Mathf.Atan2(local.x,local.z)*Mathf.Rad2Deg:0} deg right of the view, {local.magnitude:0.00} m from the eyes");
            }
        }

        // ---- The book
        void BuildBook()
        {
            Book=MemoryBook.Build(Stage,kit,sounds,xr,HandsFree);Book.StandPose=StandPose;Book.Count=Progress.Count+2;Book.Fill=Fill;
            Book.Open=CornerOpen;Book.Beckon=Beckons;Book.Turned=Turned;Book.Opened=CoverOpened;Book.HandsWhy=HandsBlocked;
            Book.Place(Book.Stand(180),MemoryBook.Where.Stand);CoverTitle(Book.CoverFace);
        }
        // The stand's pose for the book (its spine's near end), in the stage's space: the book's middle ReadAhead in front
        // of and ReadBelow under the eyes at placement, tilted ReadTilt toward the reader.
        public static Pose StandPose
        {
            get
            {
                var rotation=Quaternion.Euler(-ReadTilt,0,0);
                var centre=new Vector3(0,StageBelow-ReadBelow,ReadAhead-StageAhead);
                return new Pose(centre-rotation*new Vector3(0,0,MemoryBook.Height*MemoryBook.FlatScale*.5f),rotation);
            }
        }
        // Edges turn pages while browsing, back out of a paused memory, either way from a found one. Never past a memory
        // not yet found.
        bool CornerOpen(bool forward)
        {
            switch(Current)
            {
                case Phase.Title:return forward;
                case Phase.Levels:{int c=Book.Spread-1;return !forward||c>=0&&c<Progress.Count&&Progress.Solved(c);}
                case Phase.Paused:return !forward;
                case Phase.Found:return true;
                case Phase.Ending:return !forward;
                default:return false;
            }
        }
        // The page that moves the player on breathes: the cover, the endpaper's, a found memory's; a paused memory's back.
        bool Beckons(bool forward)=>Current==Phase.Cover||Current==Phase.Title&&forward||Current==Phase.Found&&forward||Current==Phase.Paused&&!forward;
        bool Finished=>Progress.Solved(Progress.Count-1);
        void Turned(int spread)
        {
            Debug.Log($"SHAPE_OF_MEMORY_PAGE {spread}");
            if(Level&&(Current==Phase.Paused||Current==Phase.Found)){LeaveTo(spread);return;}
            if(Level)return;
            SetPhaseFor(spread);
        }
        void Fill(int spread,bool right,Transform face,bool live)
        {
            int index=spread-1;
            if(spread==0)Endpaper(face,right,live);
            else if(index<Progress.Count&&Level&&LevelIndex==index){if(Current==Phase.Paused||Current==Phase.Found||Current==Phase.Lowering||Current==Phase.Leaving)DownPage(face,right,live);else StagePage(face,right);}
            // While a memory is still out (going), the next chapter prints without buttons.
            else if(index<Progress.Count)ChapterPage(index,face,right,live&&!Level);
            else EndingPage(face,right);
        }
        // The cover: the title stamped in gold on the leather.
        void CoverTitle(Transform face)
        {
            float m=MemoryPages.Middle;var gilt=MemoryPalette.Gold;
            pages.Text(face,"A  BOOK  OF  SHADOWS",m,.86f,.026f,.8f,.05f,gilt,FontStyles.Normal,14);
            pages.Text(face,"SHAPE",m,.66f,.14f,1f,.17f,gilt,FontStyles.Bold,10);
            pages.Text(face,"of",m,.54f,.055f,.3f,.08f,gilt,FontStyles.Italic);
            pages.Text(face,"MEMORY",m,.42f,.14f,1f,.17f,gilt,FontStyles.Bold,10);
            pages.Rule(face,m,.29f,.46f,gilt);
        }
        // The endpaper: how this book works (left); the table's height and the way on (right).
        void Endpaper(Transform face,bool right,bool live)
        {
            float m=MemoryPages.Middle;var mode=InputMode;
            if(!right)
            {
                pages.Text(face,"HOW  THIS  BOOK  WORKS",m,.90f,.026f,.9f,.05f,MemoryPages.Gilt,FontStyles.Bold,10);
                // The words that name what to pinch are aqua (Deep, on paper), as the edge they name is.
                string edge=MemoryPalette.Colored("glowing edge",MemoryPalette.AquaDeep);
                var how=mode==MemoryGuide.Input.Desktop?new[]{("TURN A PAGE","Click its "+edge+"."),("PRESS","Click a tile or a button."),("TURN THE OBJECTS","Drag the rings around them, any way.")}:
                    new[]{("TURN A PAGE","Pinch its "+edge+" and carry it over."),("PRESS","Touch a tile or a button with a fingertip."),("TURN THE OBJECTS","Pinch a ring around them and drag, any way.")};
                for(int i=0;i<how.Length;i++)
                {
                    float v=.72f-i*.2f;
                    pages.Text(face,how[i].Item1,m,v,.04f,.9f,.06f,MemoryPages.Ink,FontStyles.Bold,6);
                    pages.Text(face,how[i].Item2,m,v-.065f,.03f,.9f,.06f,MemoryPages.Faded,FontStyles.Italic);
                }
                if(mode==MemoryGuide.Input.Gaze)pages.Text(face,"Or look at any of them and pinch, wherever your hand is.",m,.12f,.028f,.9f,.06f,MemoryPages.Gilt,FontStyles.Italic);
                return;
            }
            int found=Progress.SolvedCount;
            pages.Text(face,found==0?"Every pile on this table hides a picture in its shadow.":Progress.Continue<0?"Every memory is found.":$"{found} of {Progress.Count} memories found.",m,.86f,.032f,.86f,.1f,MemoryPages.Faded,FontStyles.Italic);
            pages.Text(face,"Turn the page to begin",m,.62f,.06f,.95f,.1f,MemoryPages.Ink,FontStyles.Italic);
            HeightTiles(face,live,"title",.26f);
        }
        // LOWER and RAISE tiles, "Table height" between them (a row of its own above them pushed the tiles off the foot of
        // the pause page).
        void HeightTiles(Transform face,bool live,string set,float v)
        {
            float m=MemoryPages.Middle;
            Tile(face,live,set+"/lower","LOWER","down",m-.17f,v,()=>Raise(-Lift),()=>!Busy&&Book.OnStand);
            Tile(face,live,set+"/raise","RAISE","up",m+.17f,v,()=>Raise(Lift),()=>!Busy&&Book.OnStand);
            pages.Text(face,"Table\nheight",m,v,.026f,.15f,.08f,MemoryPages.Faded,FontStyles.Italic);
        }
        // A chapter as the contents show it: number, name and picture on the left; the riddle on the right, and PLAY
        // AGAIN once found. The next memory to find starts by itself.
        void ChapterPage(int index,Transform face,bool right,bool live)
        {
            var level=Chapter(index);if(!level)return;
            bool solved=Progress.Solved(index),locked=!Progress.Unlocked(index);float m=MemoryPages.Middle;var accent=MemoryPalette.Deep(index);
            if(!right)
            {
                pages.Text(face,"CHAPTER  "+MemoryPages.Roman(index+1),m,.93f,.026f,.8f,.05f,MemoryPages.Gilt,FontStyles.Bold,12);
                pages.Text(face,locked?"LOCKED":level.button,m,.84f,.07f,1f,.1f,locked?MemoryPages.Faded:accent,FontStyles.Bold,6);
                MemoryPlate(face,m,.47f,.46f,level,solved,locked?null:"?",accent);
                if(locked)pages.Icon(face,m,.47f,.14f,Icon("lock"),MemoryPages.Faded);
                pages.Text(face,solved?"Found.":locked?"Find the memory before it first.":"Not found yet.",m,.16f,.03f,.8f,.06f,MemoryPages.Faded,FontStyles.Italic);
                return;
            }
            if(locked){pages.Text(face,"This page is still blank.",m,.6f,.034f,.8f,.1f,MemoryPages.Faded,FontStyles.Italic);return;}
            pages.Text(face,level.riddle,m,.74f,.034f,.86f,.3f,MemoryPages.Ink,FontStyles.Italic);
            if(solved)Tile(face,live,"chapter/play","PLAY AGAIN","replay",m,.36f,()=>Replay(index),()=>!Level&&!Book.Turning&&Book.OnStand);
            else pages.Text(face,"Hold still: the memory opens.",m,.36f,.034f,.86f,.08f,MemoryPalette.AquaDeep,FontStyles.Italic);
        }
        // A memory being played, on the walls: the shadow falls on the page its lamp lights; the page the lamps leave dark
        // carries the chapter, the riddle, the hints and (after the second hint) the picture; with both lit they go in the
        // margins. On the first memory, until the orb has been rolled, the goal comes first.
        void StagePage(Transform face,bool right)
        {
            var level=Level.level;int wall=right?1:0;bool lit=Array.IndexOf(level.lamps,wall)>=0,both=level.lamps.Length>1;float m=MemoryPages.Middle,w=MemoryBook.Width,h=MemoryBook.Height;
            // On the dark page: the chapter's number, rule and hints gold, its name in its own Light accent, the words
            // Moonlight and upright (the synthetic italic was the thinnest text in the game), the action aqua.
            string chapter="CHAPTER  "+MemoryPages.Roman(LevelIndex+1);var accent=MemoryPalette.Light(LevelIndex);
            bool found=Current==Phase.Solved||Current==Phase.Leaving,goal=LevelIndex==0&&!Progress.Taught&&!found;
            if(!lit)
            {
                pages.Text(face,chapter,m,.93f,.024f,.8f,.05f,MemoryPalette.Gold,FontStyles.Bold,12);
                pages.Text(face,level.button,m,.85f,.06f,1f,.09f,accent,FontStyles.Bold,6);
                pages.Rule(face,m,.79f,.36f,MemoryPalette.Gold);
                if(found)
                {
                    pages.Text(face,"FOUND",m,.72f,.04f,.8f,.06f,MemoryPalette.Gold,FontStyles.Bold,14);
                    pages.Text(face,level.complete,m,.62f,.04f,.86f,.14f,MemoryPalette.Moonlight);
                    pages.Plate(face,m,.32f,.34f,pages.Picture(level),null,accent);
                    return;
                }
                if(goal)pages.Text(face,"The objects in the rings cast a shadow on the lit page. "+MemoryPalette.Colored("Turn them",MemoryPalette.AquaHover)+" until it shows a picture.",m,.71f,.034f,.86f,.1f,MemoryPalette.Moonlight);
                pages.Text(face,level.riddle,m,goal?.55f:.62f,.031f,.86f,.2f,MemoryPalette.Moonlight);
                if(HintStage>=1)pages.Text(face,level.hint,m,.37f,.028f,.86f,.18f,MemoryPalette.Gold);
                if(HintStage>=2)
                {
                    if(both)return;
                    pages.Plate(face,m,.15f,.20f,pages.Picture(level),null,accent);
                    pages.Text(face,"The picture",m+.2f,.15f,.022f,.2f,.05f,MemoryPalette.Haze,FontStyles.Italic);
                }
                return;
            }
            // A lit page: only its margins (the spot leaves the corners and the top edge dark).
            if(!both)return;
            if(!right)
            {
                pages.Text(face,chapter+"   "+level.button+(found?"   FOUND":""),m,h-.045f,.026f,.9f,.05f,accent,FontStyles.Bold,8);
                if(HintStage>=1)pages.Text(face,level.hint,m,.06f,.022f,.9f,.09f,MemoryPalette.Gold);
            }
            else pages.Text(face,found?level.complete:level.riddle,m,h-.06f,.022f,.9f,.09f,MemoryPalette.Moonlight);
            if(HintStage>=2)pages.Plate(face,right?w-.12f:.12f,h-.22f,.16f,pages.Picture(level,Array.IndexOf(level.lamps,wall)),null,accent);
        }
        // A memory out while the book is on the stand: paused (RESUME, the table's height, turn back to leave) or found
        // (its picture, PLAY AGAIN, turn the page to go on).
        void DownPage(Transform face,bool right,bool live)
        {
            var level=Level.level;float m=MemoryPages.Middle,w=MemoryBook.Width;var accent=MemoryPalette.Deep(LevelIndex);
            bool found=Level.Completed;
            if(!right)
            {
                pages.Text(face,"CHAPTER  "+MemoryPages.Roman(LevelIndex+1),m,.93f,.026f,.8f,.05f,MemoryPages.Gilt,FontStyles.Bold,12);
                pages.Text(face,level.button,m,.84f,.07f,1f,.1f,accent,FontStyles.Bold,6);
                pages.Rule(face,m,.77f,.36f,MemoryPages.Gilt);
                MemoryPlate(face,m,.45f,.44f,level,found,"?",accent);
                if(!found)pages.Text(face,"Turn this page back to leave the memory",m,.10f,.03f,.8f,.05f,MemoryPalette.AquaDeep,FontStyles.Italic);
                return;
            }
            if(found)
            {
                pages.Text(face,"FOUND",m,.90f,.04f,.8f,.06f,MemoryPages.Gilt,FontStyles.Bold,14);
                pages.Text(face,level.complete,m,.72f,.034f,.86f,.22f,MemoryPages.Ink,FontStyles.Italic);
                Tile(face,live,"found/replay","PLAY AGAIN","replay",m,.38f,()=>Replay(LevelIndex),()=>Current==Phase.Found&&!Book.Turning&&Book.OnStand);
                // Ending short of the turning edge's band (the outer EdgeBand), which lights over anything printed there.
                pages.Text(face,"Turn the page",w-MemoryBook.EdgeBand-.03f-.25f,.09f,.03f,.5f,.05f,MemoryPalette.AquaDeep,FontStyles.Italic,0,TextAlignmentOptions.Right);
                return;
            }
            // Top to bottom, clear of the foot's margin rule: the riddle and hint, RESUME / RESTART, LOWER / RAISE.
            pages.Text(face,"PAUSED",m,.92f,.04f,.8f,.06f,MemoryPages.Gilt,FontStyles.Bold,14);
            pages.Text(face,level.riddle,m,.785f,.03f,.86f,.13f,MemoryPages.Ink,FontStyles.Italic);
            if(HintStage>=1)pages.Text(face,level.hint,m,.655f,.026f,.86f,.09f,MemoryPages.Gilt);
            Tile(face,live,"pause/resume","RESUME","play",m-.17f,.52f,Resume,()=>Current==Phase.Paused&&!Book.Turning&&Book.OnStand);
            Tile(face,live,"pause/restart","RESTART","replay",m+.17f,.52f,()=>Replay(LevelIndex),()=>Current==Phase.Paused&&!Book.Turning&&Book.OnStand);
            HeightTiles(face,live,"pause",.24f);
        }
        void EndingPage(Transform face,bool right)
        {
            float m=MemoryPages.Middle;
            if(!right)
            {
                if(!Finished){pages.Text(face,"Find the last memory to finish the book.",m,.55f,.034f,.8f,.12f,MemoryPages.Faded,FontStyles.Italic);return;}
                pages.Text(face,"THANK YOU",m,.62f,.1f,1f,.14f,MemoryPages.Ink,FontStyles.Bold,10);
                pages.Text(face,"for playing",m,.50f,.045f,.6f,.07f,MemoryPages.Gilt,FontStyles.Italic);
                pages.Rule(face,m,.40f,.4f,MemoryPages.Gilt);
                pages.Text(face,GameTitle,m,.30f,.03f,.8f,.05f,MemoryPages.Faded,FontStyles.Bold,12);
                return;
            }
            for(int i=0;i<Progress.Count;i++)
            {
                var level=Chapter(i);if(!level)continue;float u=m+(i%2==0?-.22f:.22f),v=i<2?.70f:.30f;
                MemoryPlate(face,u,v,.3f,level,Progress.Solved(i),"?",MemoryPalette.Deep(i),.62f);
                pages.Text(face,level.button,u,v-.19f,.022f,.4f,.04f,MemoryPages.Ink,FontStyles.Bold,6);
            }
        }
        // A memory's picture plate: one, or for a two-wall level one per wall side by side (the left wall's on the left),
        // each pair times the single plate's size (2026-11-08: Home's two pictures squeezed onto one plate read as one muddle).
        void MemoryPlate(Transform face,float u,float v,float size,Torch2Level level,bool found,string mark,Color accent,float pair=.74f)
        {
            if(level.lamps.Length<2){pages.Plate(face,u,v,size,found?pages.Picture(level):null,mark,accent);return;}
            float each=size*pair,step=each*.5f+.024f;
            for(int side=0;side<2;side++)
            {
                int slot=Array.IndexOf(level.lamps,side);if(slot<0)continue;
                pages.Plate(face,u+(side==0?-step:step),v,each,found?pages.Picture(level,slot):null,mark,accent);
            }
        }
        void Tile(Transform face,bool live,string id,string caption,string icon,float u,float v,Action action,Func<bool> enabled)
        {
            var tile=MemoryTile.Create(pages,face,id,caption,Icon(icon),u,v,.17f,kit,()=>{Debug.Log("SHAPE_OF_MEMORY_PRESS "+id);audioSource.PlayOneShot(click);action();},enabled,HandsFree,live);
            if(live){tiles[id]=tile;tile.Press.Label=id;tile.Press.Why=()=>enabled()?HandsBlocked():$"not now ({Current}{(Book.Turning?", turning":"")}{(Book.OnStand?"":", book away")})";}
        }

        // ---- Placement
        // The table is placed from the head at start and again after the headset's own recenter, which moves the tracking
        // origin; there is no in-game recenter (one made the table chase wherever the head had turned to press it). The
        // runtime reports a recenter through trackingOriginUpdated; a head pose that jumps further in one frame than a head
        // can move counts too, in case a runtime does not report it. Re-placed a couple of frames later, once the head pose
        // is the new one, and never while a hand holds the orb.
        void OnTrackingOriginUpdated(XRInputSubsystem input){recenterFrame=Time.frameCount+2;Debug.Log("SHAPE_OF_MEMORY_RECENTER event");}
        void FollowSystemRecenter()
        {
            var head=Player.ViewCamera.transform;var pose=new Pose(head.position,head.rotation);
            if(headSeen&&xr.HeadReady&&recenterFrame<0)
            {
                float yaw=Vector3.Angle(Vector3.ProjectOnPlane(lastHead.forward,Vector3.up),Vector3.ProjectOnPlane(pose.forward,Vector3.up));
                if(yaw>45||Vector3.Distance(lastHead.position,pose.position)>.5f){recenterFrame=Time.frameCount+2;Debug.Log($"SHAPE_OF_MEMORY_RECENTER jump yaw={yaw:0} move={Vector3.Distance(lastHead.position,pose.position):0.00}");}
            }
            lastHead=pose;headSeen=xr.HeadReady;
            if(recenterFrame>=0&&Time.frameCount>=recenterFrame&&!Busy){recenterFrame=-1;Recenter();}
        }
        // From the head: the orb OrbAhead in front of and OrbBelow under the eyes (plus the player's own LOWER / RAISE).
        public void Recenter()
        {
            if(Busy||!Player)return;
            float lift=Progress.Lift;
            if(!Player.IsXR){Stage.SetPositionAndRotation(new Vector3(0,.72f+lift,0),Quaternion.identity);Frame();return;}
            var head=Player.ViewCamera.transform;var forward=Vector3.ProjectOnPlane(head.forward,Vector3.up).normalized;
            if(forward.sqrMagnitude<.1f)forward=Vector3.forward;
            float floor=Player.FloorHeight,y=Mathf.Clamp(head.position.y-StageBelow+lift,floor+MinTable,floor+MaxTable);
            Stage.SetPositionAndRotation(new Vector3(head.position.x,y,head.position.z)+forward*StageAhead,Quaternion.LookRotation(forward));
            Debug.Log($"SHAPE_OF_MEMORY_PLACE table {y-floor:0.00} m over the floor, eyes {head.position.y-floor:0.00} m");
        }
        void Raise(float amount){float before=Progress.Lift;Progress.Lift=before+amount;Stage.position+=Stage.up*(Progress.Lift-before);if(!Player.IsXR)Frame();}
        // Desktop: the book on its stand and the walls in one view.
        void Frame()
        {
            var view=Player.ViewCamera.transform;
            view.position=Stage.TransformPoint(new Vector3(0,.95f,-1.05f));view.LookAt(Stage.TransformPoint(new Vector3(0,.36f,.15f)));
        }

        // ---- The stand and the buttons by the hand
        static readonly Color Wood=new Color(.24f,.16f,.11f);
        // The reading stand: a board under the book, a lip at its near edge and a post down to the table. It sinks into the
        // table as the book rises (the orb takes its place).
        void BuildStand()
        {
            stand=new GameObject("Reading stand").transform;stand.SetParent(Stage,false);
            var pose=StandPose;float s=MemoryBook.FlatScale,w=MemoryBook.Width,h=MemoryBook.Height,o=MemoryBook.Overhang;
            var board=pose.position+pose.rotation*new Vector3(0,-MemoryBook.Thickness*s-.007f,h*.5f*s);
            MemoryUi.Shape(PrimitiveType.Cube,"Stand board",stand,board,pose.rotation,new Vector3((2*w+2*o)*s+.03f,.012f,(h+2*o)*s+.03f),Surface,Wood);
            var lip=pose.position+pose.rotation*new Vector3(0,-MemoryBook.Thickness*s+.004f,-o*s-.012f);
            MemoryUi.Shape(PrimitiveType.Cube,"Stand lip",stand,lip,pose.rotation,new Vector3((2*w+2*o)*s+.03f,.03f,.012f),Surface,Wood);
            MemoryUi.Shape(PrimitiveType.Cylinder,"Stand post",stand,new Vector3(0,board.y*.5f,board.z),Quaternion.identity,new Vector3(.035f,board.y*.5f,.035f),Surface,Wood*.8f);
            MemoryUi.Shape(PrimitiveType.Cylinder,"Stand foot",stand,new Vector3(0,.005f,board.z),Quaternion.identity,new Vector3(.18f,.005f,.18f),Surface,Wood*.8f);
            // Over the book's far edge, where the eyes already are, turned up to them.
            var top=pose.position+pose.rotation*new Vector3(0,0,h*s);
            coverPrompt=MemoryUi.Label("",Stage,top+new Vector3(0,.07f,.02f),Quaternion.Euler(18,0,0),.02f,new Vector2(.56f,.08f),MemoryPalette.Moonlight);
            // On an opaque Ink800 card with a Faint edge (FitPromptCard): floating over the room, the words had nothing steady
            // behind them.
            promptCard=MemoryUi.Shape(PrimitiveType.Quad,"Prompt card",coverPrompt.transform,new Vector3(0,0,.003f),Quaternion.identity,Vector3.one,Screen,MemoryPalette.Ink800).transform;
            promptEdge=MemoryUi.Shape(PrimitiveType.Quad,"Prompt card edge",coverPrompt.transform,new Vector3(0,0,.005f),Quaternion.identity,Vector3.one,Screen,MemoryPalette.Faint).transform;
        }
        Transform promptCard,promptEdge;
        string promptFitted;
        // The card hugs the words (they differ by input: hands, look-and-pinch, mouse).
        void FitPromptCard()
        {
            promptFitted=coverPrompt.text;coverPrompt.ForceMeshUpdate();var b=coverPrompt.textBounds;
            promptCard.localPosition=new Vector3(b.center.x,b.center.y,.003f);promptCard.localScale=new Vector3(b.size.x+.04f,b.size.y+.028f,1);
            promptEdge.localPosition=new Vector3(b.center.x,b.center.y,.005f);promptEdge.localScale=new Vector3(b.size.x+.046f,b.size.y+.034f,1);
        }
        // HINT, HOLD TO RESET and MENU, top to bottom, on a slim upright panel at the book's right (StripAt), turned to the
        // eyes and on a post from the table: within the right hand's reach, and well clear of the objects and their
        // shadow. Only while playing. (The book's own edge, on the walls, is about a metre away: out of reach.)
        void BuildStrip()
        {
            strip=new GameObject("Strip").transform;strip.SetParent(Stage,false);strip.localPosition=StripAt;strip.localRotation=StripFacing;
            float tall=3*RowPitch+.03f;
            // An unlit Ink800 plate (it faces the eyes, so its colour is the colour seen); the post a lit prop.
            slab=MemoryUi.Shape(PrimitiveType.Cube,"Strip slab",strip,new Vector3(0,.007f,0),Quaternion.identity,new Vector3(.115f,.014f,tall),Screen,MemoryPalette.Ink800);
            var foot=strip.TransformPoint(new Vector3(0,0,-tall*.5f));var onStage=Stage.InverseTransformPoint(foot);
            var post=MemoryUi.Shape(PrimitiveType.Cylinder,"Strip post",Stage,new Vector3(onStage.x,onStage.y*.5f,onStage.z),Quaternion.identity,new Vector3(.02f,onStage.y*.5f,.02f),Surface,MemoryPalette.Ink900);
            post.transform.SetParent(strip,true);
            Set("play",new Spec("hint","HINT","hint",Hint,()=>Current==Phase.Playing&&HintStage<MemoryProgress.MaxHints),
                new Spec("reset","HOLD TO RESET","reset",ResetOrb,()=>Current==Phase.Playing&&Level&&!Level.Completed,HoldToReset),
                new Spec("menu","MENU","menu",Pause,()=>Current==Phase.Playing));
            buttons["play/hint"].Pips(MemoryProgress.MaxHints);
            Show(null);
        }
        // A column of buttons, the first at the top (the strip's +z is up the panel).
        void Set(string name,params Spec[] specs)
        {
            var set=new GameObject("Buttons "+name);set.transform.SetParent(strip,false);set.SetActive(false);sets[name]=set;
            for(int i=0;i<specs.Length;i++)
            {
                var spec=specs[i];var position=new Vector3(0,.014f,((specs.Length-1)*.5f-i)*RowPitch+.012f);string id=name+"/"+spec.id;
                var button=MemoryButton.Create(set.transform,spec.id,spec.caption,Icon(spec.icon),position,kit,()=>{Debug.Log("SHAPE_OF_MEMORY_PRESS "+id);audioSource.PlayOneShot(click);spec.action();},
                    ()=>!Busy&&!Book.Turning&&(spec.enabled==null||spec.enabled()),HandsFree,spec.hold);
                button.Press.Label=id;button.Press.Why=()=>Busy?"orb held":Book.Turning?"book turning":spec.enabled!=null&&!spec.enabled()?$"not now ({Current})":HandsBlocked();
                buttons[id]=button;
            }
        }
        // null hides the strip (everything but playing).
        void Show(string name)
        {
            if(name!=ShownSet)Debug.Log($"SHAPE_OF_MEMORY_SET {ShownSet??"none"}>{name??"none"}");
            foreach(var pair in sets)pair.Value.SetActive(pair.Key==name);ShownSet=name;if(strip)strip.gameObject.SetActive(name!=null);
            if(name=="play")buttons["play/hint"].PipsLit=MemoryProgress.MaxHints-HintStage;
        }
        public MemoryButton Button(string set,string id)=>buttons.TryGetValue(set+"/"+id,out var button)?button:null;
        Texture2D Icon(string name){foreach(var icon in Icons)if(icon&&icon.name==name)return icon;return null;}
        // Hands may press a button unless one of them is pinching at the orb: that pinch belongs to the pile.
        bool HandsFree()=>HandsBlocked()==null;
        // Why hands may not press now (null: they may), for the device log.
        string HandsBlocked()
        {
            if(!xr.HeadReady)return "head "+xr.HeadState();
            if(!Level||Current!=Phase.Playing)return null;
            for(int hand=0;hand<2;hand++)if(xr.ReadHand(hand==0,out _,out var tip,out float pinch)&&pinch<.03f&&Level.input.Near(tip))return hand==0?"left hand pinching at the orb":"right hand pinching at the orb";
            return null;
        }
        // During play Torch2Input clicks buttons on the desktop; elsewhere (the stand, a pause) this does.
        void DesktopClick()
        {
            if(Player.IsXR||(Level&&Level.input.enabled))return;
            var mouse=Mouse.current;if(mouse==null||!mouse.leftButton.wasPressedThisFrame)return;
            if(Physics.Raycast(Player.ViewCamera.ScreenPointToRay(mouse.position.ReadValue()),out var hit,10)&&hit.collider.GetComponent<ShadowControl>() is ShadowControl control)control.Press();
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // N: next level, locked or not. K: finish the title or the intro, or solve the current level where it stands.
        void DevelopmentKeys()
        {
            var keyboard=Keyboard.current;if(keyboard==null)return;
            if(keyboard.nKey.wasPressedThisFrame&&!Book.Turning&&!Book.Flying)Load(LevelIndex<0?0:(LevelIndex+1)%Progress.Count,true);
            if(keyboard.kKey.wasPressedThisFrame&&Current==Phase.Cover)FinishTitle();
            else if(keyboard.kKey.wasPressedThisFrame&&(Current==Phase.Intro||Current==Phase.Rising))FinishIntro();
            else if(keyboard.kKey.wasPressedThisFrame&&Current==Phase.Playing&&Level.BeginHold(7)){Level.Hold(7,Level.Position,Level.Projection.Solution);Level.EndHold(7);}
        }
#endif

        // ---- Rooms
        // The level's room: a table deep and wide enough for everything, each lit lamp a floating flashlight (no stands:
        // they read as microphones), and the walls drawn on the book's pages (no frames; their dark is the dimmed paper),
        // shown once the book is up.
        void StyleStage(Torch2Room room,Torch2Workshop game)
        {
            stageRoom=room;Style(room);foreach(var frame in room.frames)if(frame)frame.gameObject.SetActive(false);room.Base=MemoryBook.Dark;
            for(int k=0;k<2;k++)if(room.Lit(k)&&room.lampHeads[k])MemoryFlashlight.Add(room.lampHeads[k],kit,Glow,()=>game&&game.world?game.world.LampLevel:0);
        }
        void Style(Torch2Room room)
        {
            room.RemoveLampModels();if(room.RestMarker)room.RestMarker.SetActive(false);
            // From just in front of the player to past the walls' corner.
            room.Table.transform.localPosition=new Vector3(0,-.035f,.29f);room.Table.transform.localScale=new Vector3(1.78f,.06f,1.18f);
            MemoryUi.Tint(room.Table.GetComponent<Renderer>(),MemoryPalette.Table);
        }
        // Between memories: just the table (and the book on its stand).
        void IdleRoom()
        {
            if(idleRoom)return;
            idleRoom=new GameObject("Book room").transform;idleRoom.SetParent(Stage,false);
            idle=new Torch2Room(idleRoom,Surface,Screen,Glow,Array.Empty<int>(),roomKit.Owned);Style(idle);
            idle.ShowWalls(false);idle.Lamps(0);if(idle.RestMarker)idle.RestMarker.SetActive(false);
        }
        void DestroyIdleRoom(){if(idleRoom){Destroy(idleRoom.gameObject);idleRoom=null;idle=null;}roomKit.Release();}
        void ClearLevel(){if(Level){Destroy(Level.gameObject);Level=null;}stageRoom=null;Intro=null;Guide=null;Found=null;LevelIndex=-1;HintStage=0;settling=false;}
        void IShadowHost.LoadLevel(string level)=>Load(Progress.IndexOf(level));
        void OnDestroy(){foreach(var input in inputs)input.trackingOriginUpdated-=OnTrackingOriginUpdated;kit.Release();roomKit.Release();MemoryTile.HoverTick=null;MemoryLog.Flush();}
        // The headset sleeping (taken off) or the app leaving the front: write out what the session log holds.
        void OnApplicationPause(bool paused){Debug.Log("SHAPE_OF_MEMORY_APP "+(paused?"paused":"resumed"));if(paused)MemoryLog.Flush();}
        void OnApplicationFocus(bool focused){Debug.Log("SHAPE_OF_MEMORY_APP "+(focused?"focused":"unfocused"));if(!focused)MemoryLog.Flush();}
    }
}
