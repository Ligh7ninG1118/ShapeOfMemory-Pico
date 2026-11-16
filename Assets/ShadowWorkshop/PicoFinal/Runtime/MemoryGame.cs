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
    // The UI's rule (2026-11-15): the book chooses, the drawer starts, the toolbar by the hand is always in reach.
    // - Closed at first (Cover): the title on its cover. At every launch the title comes up alone in the dark, the book
    //   comes up round it in the air, then comes down to the stand as the dark lifts;
    //   only a hand opens it, carrying its cover's edge over (or a look, a pinch and a sweep; the desktop clicks it). It
    //   opens onto Begin, whatever has been found.
    // - The spreads: Settings (the front endpaper: table height, language, music, start over), Begin (how the book works,
    //   "Turn the page to begin", the table height), then one per memory (Levels), the ending, the credits (Credits; their
    //   words and names are MemoryCredits' asset) and the back endpaper, Close the book? (Quit: KEEP READING or QUIT). You
    //   cannot turn past a memory not yet found. Ribbons hanging from the book (MemoryBookmarks) turn straight to Settings,
    //   Begin, the credits and the back endpaper, whenever the book lies open on its stand (a memory out is left).
    // - A chapter's page has PLAY under its riddle (PLAY AGAIN once found): the stand's drawer slides open and the memory
    //   starts (MemoryDrawer: only that; 2026-11-15 evening, pulling it to start was too hard to notice and to work).
    // - Rising, the book flies up and opens onto the lamps' walls (MemoryBook.Walls); the level's things grow on the table
    //   and fly into the orb (MemoryIntro); Playing, the shadow falls on its pages, the riddle and hints on the page the
    //   lamp leaves dark.
    // - While a memory plays, the play strip under the orb: HINT, HOLD TO RESET, PAUSE and HOLD TO QUIT. Off the walls,
    //   QUIT is the back endpaper's ribbon (the user, 2026-11-15: the plaque that stood by the stand for it sat awkwardly).
    // - PAUSE folds the orb away and brings the book down to the stand (Paused): RESUME, the table height, or turn back to
    //   leave the memory. The headset's own menu (focus lost) pauses too.
    // - Solved: the found moment holds FoundHold, the things go back to the table and stay, the book comes down open at the
    //   memory, its picture printed (Found); its page beckons, and turning it moves on (or PLAY AGAIN plays it again).
    // A level swaps in place, so the player rig and passthrough are never reloaded. The scene owns its player.
    public sealed class MemoryGame : MonoBehaviour, IShadowHost
    {
        public enum Phase{Cover,Title,Levels,Rising,Intro,Playing,Lowering,Paused,Solved,Found,Leaving,Ending,Credits,Settings,Quit}
        // Seconds: the found moment holds FoundHold before its things go back to the table (3 s was too short to take in
        // the found shadow). The book flies up RiseSeconds to start a memory and down LowerSeconds after one; a pause flies
        // PauseSeconds each way while the orb folds FoldSeconds.
        public const float FoundHold=6,RiseSeconds=1.5f,LowerSeconds=1.2f,PauseSeconds=.8f,FoldSeconds=.4f;
        // The title, at every launch (Cover): TitleDark of dark, the title comes up in it over TitleFade and holds TitleHold;
        // the closed book comes up round it over Emerge (the title is its cover's), waits EmergeHold, then comes down onto
        // the stand over DescendSeconds while the dark lifts off the room over LightsUp.
        public const float TitleDark=.6f,TitleFade=2,TitleHold=3,Emerge=1.8f,EmergeHold=.8f,DescendSeconds=2.2f,LightsUp=2;
        public static float TitleSeconds=>TitleDark+TitleFade+TitleHold+Emerge+EmergeHold+Mathf.Max(DescendSeconds,LightsUp);
        // Lift: one LOWER / RAISE step.
        public const float HoldToReset=1f,Lift=.025f,RowPitch=.115f,StandShare=.16f;
        // Holds: anything that throws something away is a hold with a ring (RESET above, QUIT, START OVER).
        public const float HoldToQuit=1.5f,HoldToForget=2f;
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
        // The buttons while playing (stage space): a strip lying across the table's front under the orb, about 30 cm ahead of
        // and 52 cm under the eyes, tilted to them. (The 2026-11-15 device round: the upright post by the right hand stood
        // in front of the right lamp, and a post on either side would: the lamps sit about 57 cm out, level with the head.)
        // Under the orb in view, it hides no lamp, ring or shadow; it is about 28 cm from the lamps' beams and outside the
        // orb's reach; a seated player reaches it with the forearm. It rises out of the table while a memory plays.
        public static Vector3 StripAt=>new Vector3(0,StageBelow-.52f,.30f-StageAhead);
        public static Quaternion StripFacing=>Facing(StripAt);
        // Turned to the eyes: +y toward them, +z up the panel.
        public static Quaternion Facing(Vector3 at)
        {
            var eye=new Vector3(0,StageBelow,-StageAhead);var toward=(eye-at).normalized;
            return Quaternion.LookRotation(Vector3.ProjectOnPlane(Vector3.up,toward).normalized,toward);
        }
        [Tooltip("Player prefab. The Pico build swaps in its own (PicoBuild.OnProcessScene looks for this field name).")]
        public GameObject StandalonePlayer;
        public Material Surface,Screen,Glow,IconMaterial;
        [Tooltip("URP Unlit, transparent (alpha blended): the orb's rings, the pages' turning edges and the dark before the title. Empty: those are opaque and there is no dark.")]
        public Material SeeThrough;
        [Tooltip("Everything round the table once the sky is up: sky, table-top lighting, clouds, ruins, the environment scene (MemoryLook; presets in PicoFinal/Looks, switched by MetaHackthone > Pico Final > Look). Empty: the room stays, lit as the scene is.")]
        public MemoryLook Look;
        [Tooltip("White-on-transparent button icons, found by file name (Tools/ShadowArt/build_icons.py).")]
        public Texture2D[] Icons=Array.Empty<Texture2D>();
        [Tooltip("Pico rig: draw the tracked joints as bones (MemoryHands) instead of the SDK's see-through hands. On: after the 2026-11-08 playtest the see-through hands came back for one round, then the user asked for the bones again.")]
        public bool JointHands=true;
        [Tooltip("With JointHands: the hands as our skinned mesh in this look (Looks/Hands: Ink, Lantern; MemoryHandSkin), placed from the tracked joints. Empty: the joint-drawn bones (MemoryHands). Switch with MetaHackthone > Pico Final > Hands.")]
        public MemoryHandLook HandLook;
        [Tooltip("How far past the drawn rings a pinch still takes the orb (metres; Torch2Input.ReachMargin). The gym keeps 0.06; raised here after the 2026-11-08 playtest found the smaller rings hard to catch.")]
        public float RingReach=.10f;
        // ---- The play area as the scene has it (2026-11-13). Each one empty: built from primitives at runtime, as before.
        [Header("Play area (the scene's own objects; empty: built at runtime)")]
        [Tooltip("The play area: everything on the table is its child. In a headset the game places it from the head (the eyes StageAhead behind and StageBelow over its origin, the table top); on the desktop it stays where the scene has it.")]
        public Transform PlayArea;
        [Tooltip("The table. The memories' rooms keep their own table box, hidden.")]
        public Transform Table;
        [Tooltip("The reading stand, its pivot on the table top: it sinks into the table (its height scaled down toward the pivot) as the book rises off it.")]
        public Transform ReadingStand;
        [Tooltip("Where the book lies on the stand: on the board's surface under the spine's near end, at the board's tilt (up: out of the board; forward: up the board, along the spine). Moving it moves the book.")]
        public Transform BookRest;
        [Tooltip("The book's model (the environment artist's SM_Book_01): the game opens it and prints its pages on it (MemoryBook.Dress). Where it sits in the scene is only a preview: the game puts it on the Book Rest.")]
        public Transform BookModel;
        [Tooltip("Dress the book in Book Model. Off: our own book (its covers, page blocks and tooled cover; the user's pick, 2026-11-15 evening) and Book Model is hidden at runtime.")]
        public bool ArtBook;
        [Tooltip("The play strip under the orb (HINT / HOLD TO RESET / PAUSE while a memory plays; it rises out of the table): its +y out of the strip toward the eyes, its +z up the strip; a post goes down to the table. Empty: built at StripAt.")]
        public Transform ButtonPanel;
        [Tooltip("The book's pages while a memory plays where no lamp reaches, and so its shadows (MemoryBook.Dark). On a pale tone the page's words print in ink, on a dark one in moonlight (PaleWalls).")]
        // Dusk plum #5E4E70 (2026-11-15: Ink700 read too dark; picked from a sheet of seven, the shadows still crisp).
        public Color WallTone=new Color(94/255f,78/255f,112/255f);
        // The book comes down from the title in a higher arc onto the scene's stand (the environment artist's: its crest rises
        // above the board, and the book flying down from upright cut through it at MemoryBook.FlyArc).
        public const float TitleArc=.2f;
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
        // The spreads in order: Settings (the front endpaper), Begin (the cover opens onto it), one per chapter, the ending,
        // the credits, and the back endpaper (Quit: close the book?), the last.
        public const int SettingsSpread=0,BeginSpread=1;
        public static int SpreadOf(int level)=>level+2;
        public static int ChapterAt(int spread)=>spread-2;
        public int EndingSpread=>Progress.Count+2;
        public int CreditsSpread=>Progress.Count+3;
        public int QuitSpread=>Progress.Count+4;
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
        Transform post;
        (Vector3 position,Quaternion rotation) stripHome;
        // The spread a ribbon to the back endpaper came from: KEEP READING turns back to it.
        int returnSpread=-1;
        // 0 the play strip down in the table .. 1 up.
        float stripUp;
        // The toolbar shows from the moment the title's dark lifts (or at once on a launch into a chapter).
        bool toolbarUp=true;
        public bool ToolbarUp=>toolbarUp;
        // The stand's drawer (null: a stand without one, and memories start the old way) and the ribbons.
        public MemoryDrawer Drawer{get;private set;}
        public MemoryBookmarks Bookmarks{get;private set;}
        readonly Dictionary<string,GameObject> sets=new Dictionary<string,GameObject>();
        readonly Dictionary<string,MemoryButton> buttons=new Dictionary<string,MemoryButton>();
        readonly Dictionary<string,string> captions=new Dictionary<string,string>();
        readonly Dictionary<string,MemoryTile> tiles=new Dictionary<string,MemoryTile>();
        AudioSource audioSource;
        AudioClip click;
        float solvedAt,nextReport,foldFrom=1,foldTo=1,foldTime,foldSeconds;
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
            // caption: its key in MemoryText.Ui.
            public readonly string id,caption,icon;public readonly Action action;public readonly Func<bool> enabled;public readonly float hold;
            public Spec(string id,string caption,string icon,Action action,Func<bool> enabled=null,float hold=0){this.id=id;this.caption=caption;this.icon=icon;this.action=action;this.enabled=enabled;this.hold=hold;}
        }

        IEnumerator Start()
        {
            MemoryLog.Begin();
            // The language (the headset's) and its font, glyphs made, before anything prints (MemoryText).
            Debug.Log($"SHAPE_OF_MEMORY_TEXT language={MemoryText.Code} font={(MemoryText.Face(MemoryText.Role.Text)?MemoryText.Face(MemoryText.Role.Text).name:"default")}");
            MemoryText.Changed+=Relabel;
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
            // The language picked in Settings, else the headset's own (MemoryText), before anything prints.
            if(Progress.Language!=""&&Progress.Language!=MemoryText.Code)MemoryText.Use(Progress.Language);
            foreach(var k in new[]{kit,roomKit}){k.Surface=Surface;k.Screen=Screen;k.Icon=IconMaterial;k.SeeThrough=SeeThrough;}
            if(!SeeThrough)Debug.LogWarning("SHAPE_OF_MEMORY no SeeThrough material: rings and edges opaque, no dark before the title");
            pages=new MemoryPages(kit);
            var root=Instantiate(StandalonePlayer);Player=root.GetComponentInChildren<MetaPlayerRig>(true);Player.Initialize(Player.XRRig,Player.DesktopCamera);
            xr=gameObject.AddComponent<MetaShadowPlayer>();xr.Player=Player;xr.Host=this;
            audioSource=gameObject.AddComponent<AudioSource>();audioSource.spatialBlend=0;audioSource.volume=.35f;click=MemoryUi.Click(kit.Owned);
            MemoryTile.HoverTick=()=>{if(audioSource)audioSource.PlayOneShot(click,.12f);};
            sounds=new MemorySounds(kit.Owned);
            var room=gameObject.AddComponent<AudioSource>();room.clip=sounds.Room;room.loop=true;room.spatialBlend=0;room.volume=.07f;room.Play();
            Music=MemoryMusic.Add(transform,MemorySoundtrack.Load());Music.Muted=!Progress.Music;
            if(Player.IsXR&&xr.UnityXRRig)SilenceMrukTicker();
            if(PlayArea){Stage=PlayArea;desktopPose=new Pose(PlayArea.position,PlayArea.rotation);}
            else{Stage=new GameObject("Stage").transform;Stage.SetParent(transform,false);}
            foreach(var authored in new[]{Table,ReadingStand,BookModel})NoColliders(authored);
            MemoryBook.Dark=WallTone;
            BuildStand();BuildStrip();BuildBook();KeepTableClear();BuildDrawer();BuildBookmarks();BuildPrompt();IdleRoom();
            var look=Look;if(!look){look=ScriptableObject.CreateInstance<MemoryLook>();look.Sky=null;kit.Owned.Add(look);}
            Debug.Log("SHAPE_OF_MEMORY_LOOK "+(Look?look.ToString():"none: the room stays"));
            Sky=MemorySky.Add(gameObject,look.Sky,SeeThrough,look.GlowAzimuth,look.GlowFacing,Player.ViewCamera,Stage,kit.Owned);
            if(Look)Lighting=MemoryLighting.Add(gameObject,look.Lighting,null,Sky,Stage);
            Dream=MemoryDream.Add(Stage,look.DreamPieces,look.DreamMaterial,Sky,kit.Owned);
            Environment=MemoryEnvironment.Add(gameObject,look.EnvironmentScene,look.EnvironmentHide,Sky,Stage,()=>Player.FloorHeight,Player.ViewCamera,look.EnvironmentGiants,look.EnvironmentSeaMaterial,look.Fog);
            Clouds=MemoryClouds.Add(Stage,look,Sky,()=>Player.FloorHeight,look.HasEnvironment?()=>Environment.Root:(Func<Transform>)null,kit.Owned);
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
                Debug.Log("SHAPE_OF_MEMORY_HAND_VISUALS "+(xr.UnityXRRig&&JointHands?Skin?"skin ("+Skin.Look.name+", MemoryHandSkin)":"joints (MemoryHands)":"SDK see-through hands"));
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
        // On the Pico rig the SDK's hand mesh, fitted to Meta's skeleton, sits off the real hands. With JointHands (the
        // default), hide it and draw hands placed from the tracked joints themselves: our skinned hand in HandLook
        // (MemoryHandSkin), or without a look the joints as bones (MemoryHands).
        void ShowRealHands()
        {
            foreach(var visual in Player.XRRig.GetComponentsInChildren<HandVisual>(true))visual.ForceOffVisibility=true;
            if(HandLook&&HandLook.Valid)
            {
                Skin=MemoryHandSkin.Add(gameObject,xr,HandLook);
                // The drawn hands stop on the table, the open book, the button panel and a lamp pushed as far as it goes;
                // a faint ghost shows the tracked hand.
                Skin.Ghost=new MemoryGhostHand(Solids);
                var ghost=Resources.Load<Material>(MemoryHandSkin.GhostMaterialPath);Skin.GhostMaterial=ghost?ghost:SeeThrough;
            }
            else Hands=MemoryHands.Add(gameObject,xr,Surface,Glow,kit.Owned);
        }
        // ---- What a hand can't go into (MemoryGhostHand), this frame
        Bounds tableBox;bool tableMeasured;
        const int TableSolid=1,BookSolid=2,PanelSolid=3,LampSolid=10;
        // The table's top is solid 4 cm down (under it is open: a seated player's hands rest there).
        public const float TableTop=.04f;
        int Solids(List<MemoryGhostHand.Solid> into)
        {
            if(Table)
            {
                // The table model itself (SM_Table_01), not the ivy env art hangs on it.
                if(!tableMeasured){tableMeasured=true;if(!MemoryGhostHand.LocalBox(Table,out tableBox,f=>f.sharedMesh.name.StartsWith("SM_Table"))&&!MemoryGhostHand.LocalBox(Table,out tableBox))tableBox=default;}
                if(tableBox.size.y>0)
                {
                    float top=tableBox.max.y,deep=TableTop/Mathf.Max(1e-4f,Mathf.Abs(Table.lossyScale.y));
                    into.Add(MemoryGhostHand.Box(TableSolid,Table,new Vector3(tableBox.center.x,top-deep*.5f,tableBox.center.z),
                        new Vector3(tableBox.extents.x,deep*.5f,tableBox.extents.z)));
                }
            }
            // The open book lying on its stand: both pages and the block under them.
            if(Book&&Book.OnStand&&!Book.Closed)
            {
                float w=MemoryBook.Width,h=MemoryBook.Height,o=MemoryBook.Overhang,t=MemoryBook.Thickness;
                into.Add(MemoryGhostHand.Box(BookSolid,Book.transform,new Vector3(0,-t*.5f,h*.5f),new Vector3(w+o,t*.5f,h*.5f+o)));
            }
            if(slab&&slab.activeInHierarchy)
            {
                var s=slab.transform;
                into.Add(new MemoryGhostHand.Solid{Id=PanelSolid,Centre=s.position,Turn=s.rotation,Half=s.lossyScale*.5f});
            }
            int k=0;
            foreach(var orb in MemoryLampOrb.All)
            {
                k++;
                if(orb&&orb.Offset.magnitude>=MemoryLampOrb.MaxPush*.97f)
                    into.Add(new MemoryGhostHand.Solid{Id=LampSolid+k,Centre=orb.Centre,Turn=Quaternion.identity,Radius=MemoryLampOrb.Radius});
            }
            return into.Count;
        }
        public MemoryHands Hands{get;private set;}
        public MemoryHandSkin Skin{get;private set;}
        // Passthrough until the book is first opened, then the magic-hour sky.
        public MemorySky Sky{get;private set;}
        // Architecture drifting beyond the book once the sky is up.
        public MemoryDream Dream{get;private set;}
        // The clouds and ruins round the player once the sky is up.
        public MemoryClouds Clouds{get;private set;}
        // The environment artist's scene round the player once the sky is up (Look.EnvironmentScene).
        public MemoryEnvironment Environment{get;private set;}
        // The table-top's key light and ambient (Look.Lighting); null without a look.
        public MemoryLighting Lighting{get;private set;}
        // The soundtrack (Resources/ShapeOfMemory/Soundtrack.asset): the book's music, from the first time it lies open.
        public MemoryMusic Music{get;private set;}
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
            Current=Phase.Cover;Book.Show(BeginSpread);Show(null);toolbarUp=false;
            Book.Sealed=true;Book.Place(Presented,MemoryBook.Where.Presented);Stage.gameObject.SetActive(true);
            var title=new MemoryShade().Add(Book.CoverFace);
            var book=new MemoryShade().Add(Book.transform,t=>t.IsChildOf(Book.CoverFace));
            var room=new MemoryShade().Add(stand).Add(idleRoom).Add(Table);
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
            if(Titling&&Book.At==MemoryBook.Where.Presented&&!Book.Flying)Book.Fly(Book.Stand(0),MemoryBook.Where.Stand,DescendSeconds,null,ReadingStand?TitleArc:MemoryBook.FlyArc);
            // The toolbar (HOLD TO QUIT) is there from here on.
            toolbarUp=true;
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
        // The cover opens onto the endpaper and nothing more. (It used to riffle on to the next memory not yet found, which
        // then started by itself; the user, 2026-11-11: the cover does nothing extra.)
        void CoverOpened(){Book.Refill();Sky.Show();SetPhaseFor(BeginSpread);}
        // The phase for a spread the book stands open at with no memory out.
        void SetPhaseFor(int spread)
        {
            var phase=spread==SettingsSpread?Phase.Settings:spread==BeginSpread?Phase.Title:spread==EndingSpread?Phase.Ending:spread==CreditsSpread?Phase.Credits:spread==QuitSpread?Phase.Quit:Phase.Levels;
            // The arpeggio plays arriving at the end, not turning back to it from the credits.
            if(phase==Phase.Ending&&Current!=Phase.Ending&&Current!=Phase.Credits){audioSource.PlayOneShot(sounds.Ending,.6f);Debug.Log("SHAPE_OF_MEMORY_ENDING");}
            Current=phase;
        }

        // ---- A memory
        // From a chapter's page (PLAY or PLAY AGAIN, through PlayChapter): the memory starts.
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
            // No snaps (2026-11-15, the user: "nerf the snap"; the held snap of 2026-11-08 took the orb out of the hand):
            // left alone within reach of the answer for a second, the light on the pages brightens, the orb glides slowly
            // into the answer and the found moment plays as it lands (Torch2Workshop.HoldToSolve).
            game.HoldToSolve=true;
            // The glide in swells, and the chapter's own found moment plays on its pages (MemorySolveCue).
            MemorySolveMoment.Add(game,MemorySolveCue.For(asset),sounds.Swell);
            // Drawn PileScale smaller (the shadow keeps the pile's own size), before the intro takes the parts' sizes.
            game.world.Pile.localScale=Vector3.one*PileScale;game.input.OrbScale=PileScale;game.input.ReachMargin=RingReach;
            // No wires to a hub and no hub: the things float free inside the rings (playtest 2026-11-08). The hub's
            // transform stays as the orb's middle (MemoryFound).
            game.world.RemoveWires();game.world.Hub.enabled=false;
            Rings(game.world);
            // Held, the things keep their own colours; the pinched ring and a dot where each hand holds show the grip
            // (playtest 2026-11-11).
            game.world.LightHeldPieces=false;MemoryGrip.Add(game,kit);
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
            // A lock coming in comes undone (it would land, and the found moment play, with the book down).
            Level.CancelLock();
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
        // ---- Quitting
        // Leaving: QUIT on the back endpaper (its ribbon, off the walls: the page is the question) or HOLD TO QUIT on the play
        // strip (while a memory plays: the hold is the question). Progress is saved as it changes, so leaving saves nothing
        // more: Application.Quit on a device, the end of Play mode in the editor. Tests set QuitHandler.
        public Action QuitHandler;
        void Quit()
        {
            Debug.Log("SHAPE_OF_MEMORY_QUIT leaving");MemoryLog.Flush();
            if(QuitHandler!=null){QuitHandler();return;}
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying=false;
#else
            Application.Quit();
#endif
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
            if(Current==Phase.Intro&&Level&&!Intro.Running)
            {
                Current=Phase.Playing;Progress.SawIntro(LevelIndex);Show("play");
                Guide=MemoryGuide.Add(Level,LevelIndex==0&&!OwnBook&&!Progress.Taught,InputMode,Glow,()=>{Progress.Taught=true;Book.Refill();});
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
            // The soundtrack starts the first time the book lies open on its stand: the cover opened, or after a launch into
            // a chapter, the book down after it. It makes way for the closeness cue while the book is up on the walls.
            if(!Music.Started&&Book.OnStand&&!Book.Closed)Music.Begin();
            Music.Up=Book.Up;
            // The stand sinks into the table as the book rises off it, and comes up under it as it lands: it moves only
            // while the book is within StandShare of the stand, both ways. Measured 2026-11-08 along the flight (walls <->
            // stand, arc .12): the book's underside sweeps through the stand's space from about 30% of the way out, so a
            // stand up by half way down (the earlier schedule) cut through it; at StandShare .16 no point of the book enters it.
            float up=Mathf.Clamp01(Book.Up/StandShare);
            stand.gameObject.SetActive(up<.999f);stand.localScale=new Vector3(standScale.x,standScale.y*Mathf.Max(.001f,1-up),standScale.z);
            coverPrompt.gameObject.SetActive(Current==Phase.Cover&&Book.OnStand&&Book.Closed);
            // The play strip rises out of the table while a memory is played, and sinks again.
            float stripTarget=ShownSet=="play"?1:0;
            if(!Mathf.Approximately(stripUp,stripTarget)||strip.gameObject.activeSelf!=stripUp>0)
            {
                stripUp=Mathf.MoveTowards(stripUp,stripTarget,dt/StripSeconds);strip.gameObject.SetActive(stripUp>0);
                strip.localPosition=stripHome.position+Vector3.down*(StripRise*(1-MemoryIntro.Smooth(stripUp)));
            }
            if(coverPrompt.gameObject.activeSelf){coverPrompt.text=CoverWords;if(promptFitted!=coverPrompt.text)FitPromptCard();}
            if(Player.IsXR){FollowSystemRecenter();WatchHands();}
            if(Player.IsXR&&Time.unscaledTime>=nextReport){nextReport=Time.unscaledTime+10;Debug.Log($"SHAPE_OF_MEMORY_HANDS phase={Current} spread={Book.Spread} set={ShownSet??"none"} book={Book.At}{(Book.Flying?"(flying)":"")} gaze={(Gaze&&Gaze.Available?"on":"off")} {xr.Report()} {(Trace?Trace.Summary():"")}");}
            if(relabel&&Book&&!Book.Turning&&!Book.Flying)RelabelNow();
            DesktopClick();
            MemoryLog.Tick();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DevelopmentKeys();
#endif
        }
        string CoverWords
        {
            get
            {
                var mode=InputMode;string key=mode+"/"+MemoryText.Code;if(coverWordsFor==key)return coverWords;coverWordsFor=key;
                return coverWords=mode==MemoryGuide.Input.Gaze?Lines(T("cover/prompt/gaze"),T("cover/prompt/gaze/more")):
                    mode==MemoryGuide.Input.Hands?Lines(MemoryText.Get("cover/prompt/hands",MemoryPalette.AquaHover),T("cover/prompt/hands/more")):T("cover/prompt/desktop");
            }
        }
        string coverWords,coverWordsFor;
        // A main line and a smaller second one under it.
        static string Lines(string main,string more)=>main+"\n<size=70%>"+more+"</size>";
        // A word from the game's own table (MemoryText.Ui).
        static string T(string key,params object[] args)=>MemoryText.Get(key,args);
        // The language changed (MemoryText.Changed): print again what is printed (pages, cover, buttons, guide).
        bool relabel;
        void Relabel()=>relabel=true;
        void RelabelNow()
        {
            relabel=false;coverWordsFor=null;
            CoverTitle(Book.CoverFace);Book.Refill();
            foreach(var pair in buttons)if(captions.TryGetValue(pair.Key,out var key))pair.Value.Caption=T(key);
            if(Guide)Guide.Relabel();
            Debug.Log("SHAPE_OF_MEMORY_TEXT reprinted in "+MemoryText.Code);
        }

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
            Book=MemoryBook.Build(Stage,kit,sounds,xr,HandsFree);
            if(ArtBook)Book.Dress(BookModel);else if(BookModel)BookModel.gameObject.SetActive(false);
            Book.StandPose=BookOnStand;Book.Count=QuitSpread+1;Book.Fill=Fill;
            Book.Open=CornerOpen;Book.Beckon=Beckons;Book.Turned=Turned;Book.Opened=CoverOpened;Book.HandsWhy=HandsBlocked;
            Book.Place(Book.Stand(180),MemoryBook.Where.Stand);CoverTitle(Book.CoverFace);
        }
        // The book's root (its spine's near end, under the paper) on the stand, in the stage's space: on the scene's Book
        // Rest, lifted by the book's underside, or else the built stand's (StandPose).
        Pose BookOnStand
        {
            get
            {
                if(!BookRest)return StandPose;
                var rotation=Quaternion.Inverse(Stage.rotation)*BookRest.rotation;
                return new Pose(Stage.InverseTransformPoint(BookRest.position)+rotation*Vector3.up*(Book.Underside*MemoryBook.FlatScale),rotation);
            }
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
        // not yet found, and not while the drawer runs out to start a memory.
        bool CornerOpen(bool forward)
        {
            if(Drawer&&Drawer.Opening)return false;
            switch(Current)
            {
                case Phase.Settings:return forward;
                case Phase.Title:return true;
                case Phase.Levels:{int c=ChapterAt(Book.Spread);return !forward||c>=0&&c<Progress.Count&&Progress.Solved(c);}
                case Phase.Paused:return !forward;
                case Phase.Found:return true;
                // Back from the ending only onto a chapter that is open (the credits' ribbon reaches it before the end).
                case Phase.Ending:return forward||Progress.Unlocked(Progress.Count-1);
                // The credits on to the back endpaper; that one back only.
                case Phase.Credits:return true;
                case Phase.Quit:return !forward;
                default:return false;
            }
        }
        // The page that moves the player on breathes: the cover, the endpaper's, a found memory's, the ending's (to the
        // credits); a paused memory's back.
        bool Beckons(bool forward)=>Current==Phase.Cover||(Current==Phase.Title||Current==Phase.Found||Current==Phase.Ending||Current==Phase.Settings)&&forward||Current==Phase.Paused&&!forward;
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
            int index=ChapterAt(spread);
            if(spread==SettingsSpread)SettingsPage(face,right,live);
            else if(spread==BeginSpread)Endpaper(face,right,live);
            else if(index<Progress.Count&&Level&&LevelIndex==index){if(Current==Phase.Paused||Current==Phase.Found||Current==Phase.Lowering||Current==Phase.Leaving)DownPage(face,right,live);else StagePage(face,right);}
            // While a memory is still out (going), the next chapter prints without buttons.
            else if(index<Progress.Count)ChapterPage(index,face,right,live&&!Level);
            else if(spread==EndingSpread)EndingPage(face,right);
            else if(spread==CreditsSpread)CreditsPage(face,right);
            else ClosePage(face,right,live);
        }
        // The cover: the title stamped in gold on the leather.
        void CoverTitle(Transform cover)
        {
            var old=cover.Find("Title");if(old){old.name="Title (old)";Destroy(old.gameObject);}
            var face=new GameObject("Title").transform;face.SetParent(cover,false);
            float m=MemoryPages.Middle;var gilt=MemoryPalette.Gold;
            // The title alone, a little above the middle (the "A BOOK OF SHADOWS" kicker went 2026-11-14). English stacks
            // SHAPE / of / MEMORY; an upright script (Chinese: 浮影集) sets the whole name on one line, larger, with the rule
            // drawn up under it.
            bool upright=MemoryText.Upright;
            if(upright)pages.Text(face,T("title"),m,.58f,.2f,1f,.25f,gilt,FontStyles.Bold,28,role:MemoryText.Role.Title);
            else
            {
                pages.Text(face,T("cover/title/top"),m,.69f,.14f,1f,.17f,gilt,FontStyles.Bold,5,role:MemoryText.Role.Title);
                pages.Text(face,T("cover/title/of"),m,.57f,.055f,.3f,.08f,gilt,FontStyles.Italic);
                pages.Text(face,T("cover/title/bottom"),m,.45f,.14f,1f,.17f,gilt,FontStyles.Bold,5,role:MemoryText.Role.Title);
            }
            pages.Rule(face,m,upright?.40f:.32f,.46f,gilt);
        }
        // Begin, the endpaper: how this book works (left); the way on, the table's height and where Settings are (right).
        void Endpaper(Transform face,bool right,bool live)
        {
            float m=MemoryPages.Middle;var mode=InputMode;
            if(!right)
            {
                Kicker(face,T("how/title"),.90f,.026f,.9f,MemoryPages.Gilt,26);
                // The words that name what to pinch are aqua (Deep, on paper), as the edge they name is (<em> in the table).
                var edge=MemoryPalette.AquaDeep;
                var how=new List<(string,string)>();bool desktop=mode==MemoryGuide.Input.Desktop;
                how.Add((T("how/turn"),desktop?MemoryText.Get("how/turn/desktop",edge):MemoryText.Get("how/turn/hands",edge)));
                how.Add((T("how/press"),desktop?T("how/press/desktop"):T("how/press/hands")));
                // The ribbons are lifted, not pressed (2026-11-15).
                how.Add((T("how/ribbon"),desktop?T("how/ribbon/desktop"):T("how/ribbon/hands")));
                how.Add((T("how/objects"),desktop?T("how/objects/desktop"):T("how/objects/hands")));
                float step=how.Count>3?.17f:.2f,top=how.Count>3?.75f:.72f;
                for(int i=0;i<how.Count;i++)
                {
                    float v=top-i*step;
                    pages.Text(face,how[i].Item1,m,v,.04f,.9f,.06f,MemoryPages.Ink,FontStyles.Bold,6);
                    pages.Text(face,how[i].Item2,m,v-.065f,.03f,.9f,.06f,MemoryPages.Faded,FontStyles.Italic);
                }
                if(mode==MemoryGuide.Input.Gaze)pages.Text(face,T("how/gaze"),m,how.Count>3?.10f:.12f,.028f,.9f,.06f,MemoryPages.Gilt,FontStyles.Italic);
                return;
            }
            pages.Text(face,ProgressLine(),m,.86f,.032f,.86f,.1f,MemoryPages.Faded,FontStyles.Italic);
            pages.Text(face,T("begin"),m,.69f,.06f,.95f,.1f,MemoryPages.Ink,FontStyles.Italic);
            HeightTiles(face,live,"title",.48f);
            pages.Text(face,T("begin/settings"),m,.20f,.028f,.8f,.06f,MemoryPages.Faded,FontStyles.Italic);
        }
        // A page's small tracked heading (CHAPTER I, TO THE READER, SETTINGS), centred at v. Chinese is set KickerCjk times
        // larger with less tracking: at the English size, four upright characters read as a footnote, not a heading.
        public const float KickerCjk=1.6f,KickerCjkSpacing=12;
        TMP_Text Kicker(Transform face,string text,float v,float size,float width,Color color,float spacing)
        {
            bool cjk=MemoryText.Upright;float k=cjk?KickerCjk:1;
            return pages.Text(face,text,MemoryPages.Middle,v,size*k,width,.05f*k,color,FontStyles.Bold,cjk?KickerCjkSpacing:spacing);
        }
        // The two-wall chapter's riddle in the right page's top margin (StagePage): centre MarginRiddleTop under the top
        // edge, a box tall enough for four lines of verse at MarginRiddleSize.
        public const float MarginRiddleTop=.105f,MarginRiddleSize=.028f,MarginRiddleWidth=.72f,MarginRiddleHeight=.17f;
        // LOWER and RAISE tiles, "Table height" between them (a row of its own above them pushed the tiles off the foot of
        // the pause page).
        void HeightTiles(Transform face,bool live,string set,float v)
        {
            float m=MemoryPages.Middle;
            Tile(face,live,set+"/lower",T("table/lower"),"down",m-.17f,v,()=>Raise(-Lift),()=>!Busy&&Book.OnStand);
            Tile(face,live,set+"/raise",T("table/raise"),"up",m+.17f,v,()=>Raise(Lift),()=>!Busy&&Book.OnStand);
            pages.Text(face,T("table/height"),m,v,.026f,.15f,.08f,MemoryPages.Faded,FontStyles.Italic);
        }
        // Settings, the front endpaper (turn back from Begin, or its ribbon): the table's height and the language (left);
        // the music and starting over (right). The option in use has a gilt frame. (Which hand the toolbar was by went with
        // the QUIT plaque it moved, 2026-11-15.)
        void SettingsPage(Transform face,bool right,bool live)
        {
            float m=MemoryPages.Middle;Func<bool> still=()=>!Busy&&Book.OnStand&&!Book.Turning;
            if(!right)
            {
                Kicker(face,T("settings/title"),.90f,.026f,.9f,MemoryPages.Gilt,26);
                HeightTiles(face,live,"settings",.66f);
                // Each language by its own name, in its own face.
                var codes=MemoryProgress.Languages;
                for(int i=0;i<codes.Length;i++)
                {
                    string code=codes[i];
                    var tile=Tile(face,live,"settings/language/"+code,MemoryText.NameOf(code),MemoryText.IsUpright(code)?"lang-zh":"lang-en",m+(i==0?-.17f:.17f),.36f,()=>UseLanguage(code),still,0,MemoryText.Code==code);
                    var own=MemoryText.Face(MemoryText.Role.Text,MemoryText.IsUpright(code));if(tile.Label&&own)tile.Label.font=own;
                }
                pages.Text(face,T("settings/language"),m,.36f,.026f,.15f,.08f,MemoryPages.Faded,FontStyles.Italic);
                return;
            }
            bool music=Progress.Music;
            Tile(face,live,"settings/music",music?T("settings/music/on"):T("settings/music/off"),music?"music":"mute",m,.70f,ToggleMusic,still);
            Tile(face,live,"settings/forget",T("settings/forget"),"replay",m,.34f,Forget,still,HoldToForget);
        }
        void UseLanguage(string code){Progress.Language=code;Debug.Log("SHAPE_OF_MEMORY_SETTINGS language "+code);if(MemoryText.Code!=code)MemoryText.Use(code);else Book.Refill();}
        void ToggleMusic(){Progress.Music=!Progress.Music;Music.Muted=!Progress.Music;Debug.Log("SHAPE_OF_MEMORY_SETTINGS music "+(Progress.Music?"on":"off"));Book.Refill();}
        // Start over: every memory waits again; the book turns to Begin.
        void Forget()
        {
            Progress.Forget();Debug.Log("SHAPE_OF_MEMORY_SETTINGS start over");
            Book.Refill();Book.TurnTo(BeginSpread,()=>SetPhaseFor(BeginSpread));
        }
        // A chapter as the contents show it: number, name and picture on the left; the riddle on the right and PLAY under it
        // (PLAY AGAIN once found).
        void ChapterPage(int index,Transform face,bool right,bool live)
        {
            var level=Chapter(index);if(!level)return;
            bool solved=Progress.Solved(index),locked=!Progress.Unlocked(index);float m=MemoryPages.Middle;var accent=MemoryPalette.Deep(index);
            if(!right)
            {
                Kicker(face,MemoryText.Chapter(index+1),.93f,.026f,.8f,MemoryPages.Gilt,24);
                pages.Text(face,locked?T("chapter/locked"):MemoryText.Name(level),m,.84f,.07f,1f,.1f,locked?MemoryPages.Faded:accent,FontStyles.Bold,6,role:MemoryText.Role.Title);
                MemoryPlate(face,m,.47f,.46f,level,solved,locked?null:"?",accent);
                if(locked)pages.Icon(face,m,.47f,.14f,Icon("lock"),MemoryPages.Faded);
                pages.Text(face,solved?T("chapter/found"):locked?T("chapter/locked/why"):T("chapter/not-found"),m,.16f,.03f,.8f,.06f,MemoryPages.Faded,FontStyles.Italic);
                return;
            }
            if(locked){pages.Text(face,T("chapter/blank"),m,.6f,.034f,.8f,.1f,MemoryPages.Faded,FontStyles.Italic);return;}
            pages.Text(face,MemoryText.Riddle(level),m,.74f,.034f,.86f,.3f,MemoryPages.Ink,FontStyles.Italic);
            Tile(face,live,"chapter/play",solved?T("play-again"):T("play"),solved?"replay":"play",m,.36f,()=>PlayChapter(index),
                ()=>Current==Phase.Levels&&!Level&&!Busy&&!Book.Turning&&Book.OnStand&&!(Drawer&&Drawer.Opening));
        }
        // A memory being played, on the walls: the shadow falls on the page its lamp lights; the page the lamps leave dark
        // carries the chapter, the riddle, the hints and (after the second hint) the picture; with both lit they go in the
        // margins. On the first memory, until the orb has been rolled, the goal comes first.
        void StagePage(Transform face,bool right)
        {
            var level=Level.level;int wall=right?1:0;bool lit=Array.IndexOf(level.lamps,wall)>=0,both=level.lamps.Length>1;float m=MemoryPages.Middle,w=MemoryBook.Width,h=MemoryBook.Height;
            // On the dark page: the chapter's number, rule and hints gold, its name in its own Light accent, the words
            // Moonlight and upright (the synthetic italic was the thinnest text in the game), the action aqua. On a pale
            // wall tone (PaleWalls), the paper's: gilt, the Deep accent, ink, deep aqua.
            string chapter=MemoryText.Chapter(LevelIndex+1),name=MemoryText.Name(level);bool pale=PaleWalls;
            var accent=pale?MemoryPalette.Deep(LevelIndex):MemoryPalette.Light(LevelIndex);
            Color gold=pale?MemoryPages.Gilt:MemoryPalette.Gold,words=pale?MemoryPages.Ink:MemoryPalette.Moonlight,act=pale?MemoryPalette.AquaDeep:MemoryPalette.AquaHover,quiet=pale?MemoryPages.Faded:MemoryPalette.Haze;
            bool found=Current==Phase.Solved||Current==Phase.Leaving,goal=LevelIndex==0&&!Progress.Taught&&!found;
            if(!lit)
            {
                Kicker(face,chapter,.93f,.024f,.8f,gold,24);
                pages.Text(face,name,m,.85f,.06f,1f,.09f,accent,FontStyles.Bold,6,role:MemoryText.Role.Title);
                pages.Rule(face,m,.79f,.36f,gold);
                if(found)
                {
                    pages.Text(face,T("found"),m,.72f,.04f,.8f,.06f,gold,FontStyles.Bold,14);
                    pages.Text(face,MemoryText.Complete(level),m,.62f,.04f,.86f,.14f,words);
                    pages.Plate(face,m,.32f,.34f,pages.Picture(level),null,accent);
                    return;
                }
                if(goal)pages.Text(face,MemoryText.Get("goal",act),m,.71f,.034f,.86f,.1f,words);
                pages.Text(face,MemoryText.Riddle(level),m,goal?.55f:.62f,.031f,.86f,.2f,words);
                if(HintStage>=1)pages.Text(face,MemoryText.Hint(level),m,.37f,.028f,.86f,.18f,gold);
                if(HintStage>=2)
                {
                    if(both)return;
                    pages.Plate(face,m,.15f,.20f,pages.Picture(level),null,accent);
                    pages.Text(face,T("picture"),m+.2f,.15f,.022f,.2f,.05f,quiet,FontStyles.Italic);
                }
                return;
            }
            // A lit page: only its margins (the spot leaves the corners and the top edge dark).
            if(!both)return;
            if(!right)
            {
                pages.Text(face,chapter+"   "+name+(found?"   "+T("found"):""),m,h-.045f,.026f,.9f,.05f,accent,FontStyles.Bold,8);
                if(HintStage>=1)pages.Text(face,MemoryText.Hint(level),m,.06f,.022f,.9f,.09f,gold);
            }
            // The riddle (four lines of verse) or the found words in the right page's top margin, between the page's top edge
            // and the lamp's spot: taller than one margin line, and narrow enough to stay clear of the answer plate that the
            // second hint puts in the corner (w-.12 on the right).
            else pages.Text(face,found?MemoryText.Complete(level):MemoryText.Riddle(level),m,h-MarginRiddleTop,MarginRiddleSize,MarginRiddleWidth,MarginRiddleHeight,words);
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
                Kicker(face,MemoryText.Chapter(LevelIndex+1),.93f,.026f,.8f,MemoryPages.Gilt,24);
                pages.Text(face,MemoryText.Name(level),m,.84f,.07f,1f,.1f,accent,FontStyles.Bold,6,role:MemoryText.Role.Title);
                pages.Rule(face,m,.77f,.36f,MemoryPages.Gilt);
                MemoryPlate(face,m,.45f,.44f,level,found,"?",accent);
                if(!found)pages.Text(face,T("leave"),m,.10f,.03f,.8f,.05f,MemoryPalette.AquaDeep,FontStyles.Italic);
                return;
            }
            if(found)
            {
                pages.Text(face,T("found"),m,.90f,.04f,.8f,.06f,MemoryPages.Gilt,FontStyles.Bold,14);
                pages.Text(face,MemoryText.Complete(level),m,.72f,.034f,.86f,.22f,MemoryPages.Ink,FontStyles.Italic);
                Tile(face,live,"found/replay",T("play-again"),"replay",m,.38f,()=>PlayChapter(LevelIndex),()=>Current==Phase.Found&&!Book.Turning&&Book.OnStand&&!(Drawer&&Drawer.Opening));
                // Ending short of the turning edge's band (the outer EdgeBand), which lights over anything printed there.
                pages.Text(face,T("turn-on"),w-MemoryBook.EdgeBand-.03f-.25f,.09f,.03f,.5f,.05f,MemoryPalette.AquaDeep,FontStyles.Italic,0,TextAlignmentOptions.Right);
                return;
            }
            // Top to bottom, clear of the foot's margin rule: the riddle and hint, RESUME, LOWER / RAISE. (RESTART went: HOLD TO
            // RESET on the toolbar puts the pile back; QUIT went to the toolbar.)
            pages.Text(face,T("paused"),m,.92f,.04f,.8f,.06f,MemoryPages.Gilt,FontStyles.Bold,14);
            pages.Text(face,MemoryText.Riddle(level),m,.80f,.03f,.86f,.13f,MemoryPages.Ink,FontStyles.Italic);
            // Room for a three-line hint clear of the tiles (Merriweather sets wider than the old face; in a .09 box it
            // shrank to 40%).
            if(HintStage>=1)pages.Text(face,MemoryText.Hint(level),m,.675f,.025f,.86f,.11f,MemoryPages.Gilt);
            Tile(face,live,"pause/resume",T("resume"),"play",m,.52f,Resume,()=>Current==Phase.Paused&&!Book.Turning&&Book.OnStand);
            HeightTiles(face,live,"pause",.24f);
        }
        void EndingPage(Transform face,bool right)
        {
            float m=MemoryPages.Middle;
            if(!right)
            {
                if(!Finished){pages.Text(face,T("ending/unfinished"),m,.55f,.034f,.8f,.12f,MemoryPages.Faded,FontStyles.Italic);return;}
                pages.Text(face,T("ending/thanks"),m,.62f,.1f,1f,.14f,MemoryPages.Ink,FontStyles.Bold,5,role:MemoryText.Role.Title);
                pages.Text(face,T("ending/for-playing"),m,.50f,.045f,.6f,.07f,MemoryPages.Gilt,FontStyles.Italic);
                pages.Rule(face,m,.40f,.4f,MemoryPages.Gilt);
                pages.Text(face,T("title"),m,.30f,.03f,.8f,.05f,MemoryPages.Faded,FontStyles.Bold,12);
                return;
            }
            for(int i=0;i<Progress.Count;i++)
            {
                var level=Chapter(i);if(!level)continue;float u=m+(i%2==0?-.22f:.22f),v=i<2?.70f:.30f;
                MemoryPlate(face,u,v,.3f,level,Progress.Solved(i),"?",MemoryPalette.Deep(i),.62f);
                pages.Text(face,MemoryText.Name(level),u,v-.19f,.022f,.4f,.04f,MemoryPages.Ink,FontStyles.Bold,6);
            }
        }
        // The credits (MemoryCredits' asset, read each time the spread is printed): who made the game (left); what others
        // made that it uses, by section (right). Both pages open the same way, the page's line in gilt italic over a gilt
        // rule, as the ending's "for playing" is set.
        MemoryCredits credits;
        public MemoryCredits Credits
        {
            get
            {
                if(credits)return credits;credits=MemoryCredits.Load();if(credits)return credits;
                Debug.LogError("SHAPE_OF_MEMORY_CREDITS missing: Resources/"+MemoryCredits.ResourcePath+".asset (MetaHackthone > Pico Final > Credits makes it); printing the defaults");
                credits=ScriptableObject.CreateInstance<MemoryCredits>();kit.Owned.Add(credits);return credits;
            }
        }
        void CreditsPage(Transform face,bool right)
        {
            var c=Credits;float m=MemoryPages.Middle;
            pages.Text(face,right?MemoryText.Asset(MemoryText.ThanksToKey,c.thanksTo):MemoryText.Asset(MemoryText.MadeByKey,c.madeBy),m,.88f,.045f,.8f,.07f,MemoryPages.Gilt,FontStyles.Italic);
            pages.Rule(face,m,.81f,.3f,MemoryPages.Gilt);
            if(!right){NameColumns(face,c.Names.ToArray(),m,.58f);return;}
            // One block, so a long list shrinks to fit as a whole: each heading in gilt capitals, its lines under it, a half
            // line between sections.
            string Printed(MemoryCredits.Section s)
            {
                string title=string.IsNullOrWhiteSpace(s.heading)?"":MemoryText.Asset(MemoryText.SectionKey(s.heading),s.heading);
                string heading=MemoryPalette.Colored("<b><cspace=0.12em>"+title.ToUpperInvariant()+"</cspace></b>",MemoryPages.Gilt);
                string lines=s.Lines.Any()?string.Join("\n",s.Lines):MemoryPalette.Colored(MemoryText.Italic(T("credits/to-come")),MemoryPages.Faded);
                return "<size=80%>"+heading+"</size>\n<size=25%> </size>\n"+lines;
            }
            // From under the rule to near the page's foot.
            pages.Text(face,string.Join("\n<size=90%> </size>\n",c.Sections.Select(Printed)),m,.43f,.034f,.86f,.62f,MemoryPages.Ink,FontStyles.Normal,0,TextAlignmentOptions.Top);
        }
        // The names in two columns NameColumnGap apart, read down the left one first (it takes the odd one out). Rows stay
        // level across the columns (a short right column is padded with a blank line), and both are set at one size, the
        // smaller of the two (each label alone would shrink only itself to fit a long name).
        public const float NameColumnGap=.4f;
        void NameColumns(Transform face,string[] names,float u,float v)
        {
            int rows=(names.Length+1)/2,rest=names.Length-rows;
            var columns=new List<TMP_Text>();
            void Column(float at,IEnumerable<string> lines,bool pad)=>columns.Add(pages.Text(face,string.Join("\n",lines)+(pad?"\n\u00A0":""),at,v,.045f,.38f,.34f,MemoryPages.Ink,FontStyles.Normal,2));
            Column(u-NameColumnGap*.5f,names.Take(rows),false);
            if(rest>0)Column(u+NameColumnGap*.5f,names.Skip(rows),rest<rows);
            foreach(var column in columns)column.ForceMeshUpdate();
            float size=columns.Min(c=>c.fontSize);
            foreach(var column in columns){column.enableAutoSizing=false;column.fontSize=size;}
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
        // hold: seconds the press must be held (with a ring of dots); chosen: the option now in use (a gilt frame).
        MemoryTile Tile(Transform face,bool live,string id,string caption,string icon,float u,float v,Action action,Func<bool> enabled,float hold=0,bool chosen=false)
        {
            var tile=MemoryTile.Create(pages,face,id,caption,Icon(icon),u,v,.17f,kit,()=>{Debug.Log("SHAPE_OF_MEMORY_PRESS "+id);audioSource.PlayOneShot(click);action();},enabled,HandsFree,live,hold,chosen);
            if(live){tiles[id]=tile;tile.Press.Label=id;tile.Press.Why=()=>enabled()?HandsBlocked():$"not now ({Current}{(Book.Turning?", turning":"")}{(Book.OnStand?"":", book away")})";}
            return tile;
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
            if(!Player.IsXR){Stage.SetPositionAndRotation(desktopPose.position+Vector3.up*lift,desktopPose.rotation);Frame();return;}
            var head=Player.ViewCamera.transform;var forward=Vector3.ProjectOnPlane(head.forward,Vector3.up).normalized;
            if(forward.sqrMagnitude<.1f)forward=Vector3.forward;
            float floor=Player.FloorHeight,y=Mathf.Clamp(head.position.y-StageBelow+lift,floor+MinTable,floor+MaxTable);
            Stage.SetPositionAndRotation(new Vector3(head.position.x,y,head.position.z)+forward*StageAhead,Quaternion.LookRotation(forward));
            Debug.Log($"SHAPE_OF_MEMORY_PLACE table {y-floor:0.00} m over the floor, eyes {head.position.y-floor:0.00} m");
        }
        // On the desktop the table stays put: where the scene's play area is, or 72 cm over the floor.
        Pose desktopPose=new Pose(new Vector3(0,.72f,0),Quaternion.identity);
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
            if(ReadingStand){stand=ReadingStand;standScale=stand.localScale;return;}
            stand=new GameObject("Reading stand").transform;stand.SetParent(Stage,false);standScale=Vector3.one;
            var pose=StandPose;float s=MemoryBook.FlatScale,w=MemoryBook.Width,h=MemoryBook.Height,o=MemoryBook.Overhang;
            var board=pose.position+pose.rotation*new Vector3(0,-MemoryBook.Thickness*s-.007f,h*.5f*s);
            MemoryUi.Shape(PrimitiveType.Cube,"Stand board",stand,board,pose.rotation,new Vector3((2*w+2*o)*s+.03f,.012f,(h+2*o)*s+.03f),Surface,Wood);
            var lip=pose.position+pose.rotation*new Vector3(0,-MemoryBook.Thickness*s+.004f,-o*s-.012f);
            MemoryUi.Shape(PrimitiveType.Cube,"Stand lip",stand,lip,pose.rotation,new Vector3((2*w+2*o)*s+.03f,.03f,.012f),Surface,Wood);
            MemoryUi.Shape(PrimitiveType.Cylinder,"Stand post",stand,new Vector3(0,board.y*.5f,board.z),Quaternion.identity,new Vector3(.035f,board.y*.5f,.035f),Surface,Wood*.8f);
            MemoryUi.Shape(PrimitiveType.Cylinder,"Stand foot",stand,new Vector3(0,.005f,board.z),Quaternion.identity,new Vector3(.18f,.005f,.18f),Surface,Wood*.8f);
        }
        Vector3 standScale=Vector3.one;
        // Where a memory's things lie on the table (Torch2Transit.Scatter) keeps clear of the stand, the book on it and the
        // drawer's front (KeepOut, stage space = the levels' space): the stand comes up out of the table as the book comes
        // down after a memory is found, while the things stay, and it came up through the ones lying there (2026-11-15).
        public const float KeepOutMargin=.02f;
        void KeepTableClear()
        {
            var min=new Vector2(float.MaxValue,float.MaxValue);var max=-min;
            foreach(var r in stand.GetComponentsInChildren<Renderer>(true))
            {
                if(BookModel&&r.transform.IsChildOf(BookModel))continue;
                var b=r.bounds;
                for(int c=0;c<8;c++){var p=Stage.InverseTransformPoint(b.center+Vector3.Scale(b.extents,new Vector3((c&1)==0?-1:1,(c&2)==0?-1:1,(c&4)==0?-1:1)));min=Vector2.Min(min,new Vector2(p.x,p.z));max=Vector2.Max(max,new Vector2(p.x,p.z));}
            }
            if(min.x>max.x){Torch2Transit.KeepOut=Array.Empty<Rect>();return;}
            Torch2Transit.KeepOut=new[]{Rect.MinMaxRect(min.x-KeepOutMargin,min.y-KeepOutMargin,max.x+KeepOutMargin,max.y+KeepOutMargin)};
            // With the front middle kept clear, the things may lie back to the walls' corner (the scene's table reaches far
            // past it): the walls' WallClear keeps them in front of the pages.
            Torch2Transit.HostArea=Rect.MinMaxRect(-.72f,-.15f,.72f,.74f);
            Debug.Log($"SHAPE_OF_MEMORY_TABLE things keep out of x {min.x-KeepOutMargin:0.00}..{max.x+KeepOutMargin:0.00}, z {min.y-KeepOutMargin:0.00}..{max.y+KeepOutMargin:0.00} (the stand)");
        }
        // The scene's own things are only seen: no collider of theirs may catch the hands' rays or a look (ones on Ignore
        // Raycast, as the stand drawer's knob, are handles and stay).
        static void NoColliders(Transform root)
        {
            if(!root)return;
            foreach(var c in root.GetComponentsInChildren<Collider>(true))if(c.gameObject.layer!=2)Destroy(c);
        }
        // The cover's prompt: over the book's far edge on the stand, where the eyes already are, turned up to them.
        void BuildPrompt()
        {
            var pose=Book.StandPose;float s=MemoryBook.FlatScale,h=MemoryBook.Height;
            var top=pose.position+pose.rotation*new Vector3(0,0,h*s);
            coverPrompt=MemoryUi.Label("",Stage,top+new Vector3(0,.07f,.02f),Quaternion.Euler(18,0,0),.02f,new Vector2(.56f,.08f),MemoryPalette.Moonlight);
            // Its first line is capitals: a little air between them (the card is fitted to the words, so it widens too).
            coverPrompt.characterSpacing=4;
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
            // The words may be in a new language: its font first.
            MemoryText.Apply(coverPrompt);promptFitted=coverPrompt.text;coverPrompt.ForceMeshUpdate();var b=coverPrompt.textBounds;
            promptCard.localPosition=new Vector3(b.center.x,b.center.y,.003f);promptCard.localScale=new Vector3(b.size.x+.04f,b.size.y+.028f,1);
            promptEdge.localPosition=new Vector3(b.center.x,b.center.y,.005f);promptEdge.localScale=new Vector3(b.size.x+.046f,b.size.y+.034f,1);
        }
        // The play strip (StripAt, or the scene's Button panel): HINT, HOLD TO RESET, PAUSE and HOLD TO QUIT in a row, names
        // under the caps, while a memory is played; it rises out of the table (StripRise over StripSeconds) and sinks again.
        // An unlit Ink800 plate on a post down to the table.
        public const float StripWidth=.53f,StripDepth=.15f,ButtonPitch=.125f,StripRise=.16f,StripSeconds=.35f;
        void BuildStrip()
        {
            if(ButtonPanel)strip=ButtonPanel;
            else{strip=new GameObject("Play strip").transform;strip.SetParent(Stage,false);strip.localPosition=StripAt;strip.localRotation=StripFacing;}
            stripHome=(strip.localPosition,strip.localRotation);
            slab=MemoryUi.Shape(PrimitiveType.Cube,"Strip slab",strip,new Vector3(0,.007f,0),Quaternion.identity,new Vector3(StripWidth,.014f,StripDepth),Screen,MemoryPalette.Ink800);
            var under=strip.localPosition;
            post=MemoryUi.Shape(PrimitiveType.Cylinder,"Strip post",Stage,new Vector3(under.x,under.y*.5f,under.z),Quaternion.identity,new Vector3(.02f,Mathf.Max(.001f,under.y*.5f),.02f),Surface,MemoryPalette.Ink900).transform;
            post.SetParent(strip,true);
            Set("play",new Spec("hint","strip/hint","hint",Hint,()=>Current==Phase.Playing&&HintStage<MemoryProgress.MaxHints),
                new Spec("reset","strip/reset","reset",ResetOrb,()=>Current==Phase.Playing&&Level&&!Level.Completed,HoldToReset),
                new Spec("pause","strip/pause","pause",Pause,()=>Current==Phase.Playing),
                new Spec("quit","strip/quit","quit",Quit,()=>Current==Phase.Playing,HoldToQuit));
            buttons["play/hint"].Pips(MemoryProgress.MaxHints);
            strip.gameObject.SetActive(false);stripUp=0;strip.localPosition=stripHome.position+Vector3.down*StripRise;
            Show(null);
        }
        // A row of buttons across the strip, the first on the left, their names under them.
        void Set(string name,params Spec[] specs)
        {
            var set=new GameObject("Buttons "+name);set.transform.SetParent(strip,false);set.SetActive(false);sets[name]=set;
            for(int i=0;i<specs.Length;i++)
            {
                var spec=specs[i];var position=new Vector3((i-(specs.Length-1)*.5f)*ButtonPitch,.014f,.015f);string id=name+"/"+spec.id;
                captions[id]=spec.caption;
                var button=MemoryButton.Create(set.transform,spec.id,T(spec.caption),Icon(spec.icon),position,kit,()=>{Debug.Log("SHAPE_OF_MEMORY_PRESS "+id);audioSource.PlayOneShot(click);spec.action();},
                    ()=>!Busy&&!Book.Turning&&(spec.enabled==null||spec.enabled()),HandsFree,spec.hold,true);
                button.Press.Label=id;button.Press.Why=()=>Busy?"orb held":Book.Turning?"book turning":spec.enabled!=null&&!spec.enabled()?$"not now ({Current})":HandsBlocked();
                buttons[id]=button;
            }
        }
        // The set on the play strip ("play" while playing; null: the strip sinks into the table).
        void Show(string name)
        {
            if(name!=ShownSet)Debug.Log($"SHAPE_OF_MEMORY_SET {ShownSet??"none"}>{name??"none"}");
            foreach(var pair in sets)pair.Value.SetActive(pair.Key==name);ShownSet=name;
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
        // L: the next language.
        void DevelopmentKeys()
        {
            var keyboard=Keyboard.current;if(keyboard==null)return;
            if(keyboard.lKey.wasPressedThisFrame)MemoryText.Next();
            if(keyboard.nKey.wasPressedThisFrame&&!Book.Turning&&!Book.Flying)Load(LevelIndex<0?0:(LevelIndex+1)%Progress.Count,true);
            if(keyboard.kKey.wasPressedThisFrame&&Current==Phase.Cover)FinishTitle();
            else if(keyboard.kKey.wasPressedThisFrame&&(Current==Phase.Intro||Current==Phase.Rising))FinishIntro();
            else if(keyboard.kKey.wasPressedThisFrame&&Current==Phase.Playing&&Level.BeginHold(7)){Level.Hold(7,Level.Position,Level.Projection.Solution);Level.EndHold(7);}
        }
#endif

        // ---- The drawer (the stand's own: the environment artist's BookStandDrawer) slides open as a memory starts and shuts
        // again as the stand sinks (MemoryDrawer). A stand without one: the memory just starts.
        void BuildDrawer()
        {
            var mechanism=stand?stand.GetComponentInChildren<ArtDressing.BookStandDrawer>(true):null;
            if(!mechanism){Debug.Log("SHAPE_OF_MEMORY_DRAWER none on the stand");return;}
            Drawer=MemoryDrawer.Add(gameObject,mechanism,kit,()=>Player.ViewCamera);
        }
        // PLAY (a chapter's page) or PLAY AGAIN (a chapter found, or the found page): the drawer runs out, then the memory
        // starts (over, if one is out: Replay), gold motes flying from the drawer to where its things come up.
        void PlayChapter(int index)
        {
            if(Busy||Book.Turning||!Book.OnStand||Drawer&&Drawer.Opening)return;
            Debug.Log($"SHAPE_OF_MEMORY_PLAY chapter {index+1}"+(Progress.Solved(index)?" again":""));
            void Go(){if(Level)Replay(index);else Begin(index);if(Drawer)StartCoroutine(Motes());}
            if(Drawer)Drawer.Open(Go);else Go();
        }
        // Gold specks from the open drawer to where the memory's things will come up on the table, as the book rises.
        IEnumerator Motes()
        {
            float deadline=Time.unscaledTime+1.5f;
            while(!(Level&&Current==Phase.Rising)&&Time.unscaledTime<deadline)yield return null;
            if(Level&&Current==Phase.Rising&&Drawer)Drawer.Burst(Level.world.Pieces.Where(p=>p).Select(p=>p.position).ToArray());
        }
        // ---- Bookmarks: a ribbon for Settings, Begin and the credits (MemoryBookmarks; the 2026-11-15 device round: no ribbon
        // per chapter).
        void BuildBookmarks()
        {
            Bookmarks=MemoryBookmarks.Add(Stage,kit,()=>Book.StandPose);Bookmarks.BookHeight=()=>Book.Underside*MemoryBook.FlatScale;
            // Lifted by the player's hands (a pinch on the silk), as the book's pages and cover are carried.
            Bookmarks.Tracking=()=>xr&&xr.Active;Bookmarks.ReadHand=(int hand,out Vector3 tip,out float pinch)=>xr.ReadHand(hand==0,out _,out tip,out pinch);
            // Whenever the book lies open on its stand: browsing, or a memory paused or found (a ribbon then leaves it).
            Bookmarks.Shown=()=>toolbarUp&&Book.OnStand&&!Book.Closed&&!(Drawer&&Drawer.Opening)&&(Level?Current==Phase.Paused||Current==Phase.Found:
                Current==Phase.Title||Current==Phase.Levels||Current==Phase.Ending||Current==Phase.Credits||Current==Phase.Settings||Current==Phase.Quit);
            Bookmarks.Current=()=>Book.Spread;Bookmarks.Jump=JumpTo;Bookmarks.HandsAllowed=HandsFree;
            SetBookmarks();
        }
        void SetBookmarks()
        {
            if(!Bookmarks)return;
            // Silk: Settings haze, Begin gold, the credits sage, the back endpaper (QUIT) ember; two each side of the knob.
            Bookmarks.Set(new List<(string,int,Color,Texture2D)>{("settings",SettingsSpread,MemoryPalette.Haze,Icon("gear")),("begin",BeginSpread,MemoryPalette.Gold,Icon("book")),
                ("credits",CreditsSpread,MemoryPalette.Light(4),Icon("heart")),("quit",QuitSpread,MemoryPalette.Ember,Icon("quit"))});
        }
        // A ribbon: one page turns straight to its spread; from a paused or found memory, the memory is left as a page turned away
        // from it leaves it (Turned).
        void JumpTo(int spread)
        {
            if(Busy||Book.Turning||!Book.OnStand||Level&&Current!=Phase.Paused&&Current!=Phase.Found)return;
            if(spread==Book.Spread){audioSource.PlayOneShot(click,.4f);return;}
            Debug.Log($"SHAPE_OF_MEMORY_PAGE jump {Book.Spread}>{spread}");
            if(spread==QuitSpread)returnSpread=Level?SpreadOf(LevelIndex):Book.Spread;
            Book.TurnStraight(spread,()=>Turned(spread));
        }
        // The back endpaper: close the book? Its question on the left, KEEP READING (back to where the ribbon was touched,
        // or the credits) and QUIT on the right; the page is the question, so QUIT is a press.
        void ClosePage(Transform face,bool right,bool live)
        {
            float m=MemoryPages.Middle;
            if(!right)
            {
                Kicker(face,T("close/title"),.90f,.026f,.9f,MemoryPages.Gilt,26);
                pages.Text(face,T("close/kept"),m,.64f,.042f,.8f,.18f,MemoryPages.Ink,FontStyles.Italic);
                // How many are found, once any are (with none, Begin's line is about the table, not the book).
                if(Progress.SolvedCount>0)pages.Text(face,ProgressLine(),m,.44f,.03f,.8f,.06f,MemoryPages.Faded,FontStyles.Italic);
                return;
            }
            Func<bool> still=()=>Current==Phase.Quit&&Book.OnStand&&!Book.Turning;
            Tile(face,live,"close/stay",T("close/stay"),"book",m-.17f,.52f,KeepReading,still);
            Tile(face,live,"close/quit",T("close/quit"),"quit",m+.17f,.52f,Quit,still);
        }
        // How many memories are found (Begin, the back endpaper).
        string ProgressLine(){int found=Progress.SolvedCount;return found==0?T("progress/none"):Progress.Continue<0?T("progress/all"):T("progress/some",found,Progress.Count);}
        void KeepReading()
        {
            int to=returnSpread>=0&&returnSpread<QuitSpread?returnSpread:CreditsSpread;
            returnSpread=-1;Debug.Log($"SHAPE_OF_MEMORY_PAGE keep reading >{to}");
            Book.TurnStraight(to,()=>Turned(to));
        }

        // ---- The wall tone (WallTone): the pages' colour on the walls where no lamp reaches, and so the shadows'.
        public static bool PaleWalls=>MemoryBook.Dark.grayscale>.38f;
        // Another tone now (the samples, a look check): the room's walls, the book's paper and its words.
        public void UseWallTone(Color tone)
        {
            MemoryBook.Dark=tone;if(stageRoom!=null)stageRoom.Base=tone;
            Book.SetBrightness(Book.Brightness);Book.Refill();if(Level)Level.world.RefreshWalls();
            Debug.Log($"SHAPE_OF_MEMORY_WALLS tone #{ColorUtility.ToHtmlStringRGB(tone)} ({(PaleWalls?"pale: ink words":"dark: moonlight words")})");
        }

        // ---- Rooms
        // The level's room: a table deep and wide enough for everything, each lit lamp a floating flashlight (no stands:
        // they read as microphones), and the walls drawn on the book's pages (no frames; their dark is the dimmed paper),
        // shown once the book is up.
        void StyleStage(Torch2Room room,Torch2Workshop game)
        {
            stageRoom=room;Style(room);foreach(var frame in room.frames)if(frame)frame.gameObject.SetActive(false);room.Base=MemoryBook.Dark;
            for(int k=0;k<2;k++)if(room.Lit(k)&&room.lampHeads[k])MemoryFlashlight.Add(room.lampHeads[k],kit,Glow,()=>game&&game.world?game.world.LampLevel:0);
            // Every lamp can be pushed and springs back (the light never moves); a lit one sends out its rays while it's on.
            for(int k=0;k<2;k++)MemoryLampOrb.Add(room.lampHeads[k],xr,room.Lit(k)?(Func<float>)(()=>game&&game.world?game.world.LampLevel:0):null);
        }
        void Style(Torch2Room room)
        {
            room.RemoveLampModels();if(room.RestMarker)room.RestMarker.SetActive(false);
            // From just in front of the player to past the walls' corner. With the scene's own table, the room's stays hidden.
            room.Table.transform.localPosition=new Vector3(0,-.035f,.29f);room.Table.transform.localScale=new Vector3(1.78f,.06f,1.18f);
            MemoryUi.Tint(room.Table.GetComponent<Renderer>(),MemoryPalette.Table);
            if(Table)room.Table.SetActive(false);
        }
        // Between memories: just the table (and the book on its stand).
        void IdleRoom()
        {
            if(idleRoom)return;
            idleRoom=new GameObject("Book room").transform;idleRoom.SetParent(Stage,false);
            idle=new Torch2Room(idleRoom,Surface,Screen,Glow,Array.Empty<int>(),roomKit.Owned);Style(idle);
            idle.ShowWalls(false);idle.Lamps(0);if(idle.RestMarker)idle.RestMarker.SetActive(false);
            for(int k=0;k<2;k++)MemoryLampOrb.Add(idle.lampHeads[k],xr,null);
        }
        void DestroyIdleRoom(){if(idleRoom){Destroy(idleRoom.gameObject);idleRoom=null;idle=null;}roomKit.Release();}
        void ClearLevel(){if(Level){Destroy(Level.gameObject);Level=null;}stageRoom=null;Intro=null;Guide=null;Found=null;LevelIndex=-1;HintStage=0;settling=false;}
        void IShadowHost.LoadLevel(string level)=>Load(Progress.IndexOf(level));
        void OnDestroy(){Torch2Transit.KeepOut=Array.Empty<Rect>();Torch2Transit.HostArea=null;MemoryText.Changed-=Relabel;foreach(var input in inputs)input.trackingOriginUpdated-=OnTrackingOriginUpdated;kit.Release();roomKit.Release();MemoryTile.HoverTick=null;MemoryLog.Flush();}
        // The headset sleeping (taken off) or the app leaving the front: write out what the session log holds.
        void OnApplicationPause(bool paused){Debug.Log("SHAPE_OF_MEMORY_APP "+(paused?"paused":"resumed"));if(paused){MemoryLog.Flush();if(Player&&Player.IsXR)PauseForSystem();}}
        void OnApplicationFocus(bool focused){Debug.Log("SHAPE_OF_MEMORY_APP "+(focused?"focused":"unfocused"));if(!focused){MemoryLog.Flush();if(Player&&Player.IsXR)PauseForSystem();}}
        // The headset's menu over the game (focus lost): a memory being played pauses, as if PAUSE were pressed (the hands
        // already let go: MetaShadowPlayer ignores them while unfocused). Back in focus, the book is down at its chapter.
        // Headsets only: in the editor a click outside the Game view takes its focus.
        public void PauseForSystem()
        {
            if(!Ready||Current!=Phase.Playing||!Level)return;
            Level.input.ReleaseAll();
            Debug.Log("SHAPE_OF_MEMORY_APP pause for the system");Pause();
        }
    }
}
