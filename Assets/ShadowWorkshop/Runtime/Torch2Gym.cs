using System;
using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace CloudGarden.ShadowPuzzle
{
    // The designers' gym for torch-2 levels. Everything under the cluster is the pile, and the pieces as placed are the
    // answer. The cluster itself is the orb: it stays at the rest position, and its rotation only previews the orb
    // (identity is the answer). The game's own room (Torch2Room) shows the pile's shadow live in edit mode, so blocking
    // out needs no Play mode. ShadowPiece marks objects as visual only or gives them a simpler shadow mesh. The table
    // layout holds a twin of each piece where it lies on the table before the level (without one they scatter, as in the
    // game); Play mode shows them lifting into the orb (Torch2Transit), then the rings close and the lamps come on, and
    // then the orb rolls under the mouse as on the game's desktop (the wheel and right-drag move the camera). Saved views
    // are orb rotations to come back to: V in Play mode or the inspector saves one, and they outlast Play mode. Checks,
    // export, open-in-gym and play live in the editor (Torch2GymEditor, MetaHackthone > Shadow Workshop > Gym).
    [ExecuteAlways]
    public sealed class Torch2Gym : MonoBehaviour
    {
        public enum Walls{Left,Right,Both}
        public const int ShadowTriangleBudget=4000;
        // Furthest a piece's mesh may reach from the hub: the orb's grab reach (shipped levels reach 17-36 cm).
        public const float MaxPieceRadius=Torch2Input.GrabReach;
        [Header("Level")]
        [Tooltip("Asset name under Resources/ShadowArt/Torch2: a letter, then letters, digits or _.")] public string id="NewLevel";
        // A new gym's words, until the designer writes their own (Shape of Memory's chapter check flags them).
        public const string PlaceholderButton="NEW LEVEL",PlaceholderRiddle="What do these things make together?";
        public string title="N E W   L E V E L",button=PlaceholderButton;
        [TextArea(2,4)] public string riddle=PlaceholderRiddle,hint="",complete="";
        public Color accent=new Color(.95f,.62f,.30f);
        public Walls walls=Walls.Left;
        [Tooltip("The orb. Its children are the pieces as they are at the answer. It stays at the rest position; rotate it to preview the orb (no rotation = the answer).")]
        public Transform cluster;
        [Header("Start and match (Check level fills these while auto is ticked)")]
        public bool autoStart=true;
        [Tooltip("Orb rotation the level opens with.")] public Quaternion start=Quaternion.Euler(35,140,20);
        public bool autoThreshold=true;
        [Range(.5f,.98f)] public float iouMin=.62f;
        [Tooltip("Shadows from both faces of every triangle, so single-sided and mirrored meshes still cast. Leave on.")]
        public bool bothFaces=true;
        [Tooltip("The picture also counts upside down (exact) and, if ticked, mirrored (only close: the lamp's perspective swaps near and far).")]
        public Torch2Flips flips=Torch2Flips.UpsideDown;
        [Header("Closeness cue (empty slides or layers use generated placeholders)")]
        [Tooltip("Picture the left lamp projects, blurred and grey until the pile nears the answer. Any size, mipmaps on.")]
        public Texture2D leftSlide;
        [Tooltip("Picture the right lamp projects.")] public Texture2D rightSlide;
        [Tooltip("Looping layers of one length: the first always plays, the others join one per tier. Heard in Play this level.")]
        public AudioClip[] layers=new AudioClip[0];
        [Range(0,1)] public float cueVolume=.5f;
        [Tooltip("Warmth that reaches tiers 1-3 (stirring, warm, hot); passing is tier 4. Check level reports how often random poses reach each.")]
        public Vector3 tiers=new Vector3(.35f,.60f,.80f);
        [Tooltip("Further than this from the answer (degrees) the cue stays at tier 1.")] public float lookalikeAngle=50;
        [Tooltip("The music only warms within this many degrees of an answer.")] public float cueAngle=30;
        [Tooltip("Let go within snapAngle degrees of the answer, passing or at snapWarmth, and the orb turns itself into the answer.")]
        public float snapWarmth=.90f,snapAngle=20;
        [Tooltip("Shape of Memory: still held within this many degrees of an answer, the orb leaves the hand and snaps in, solved.")] public float heldSnapAngle=8;
        // Held here so opening a level and exporting it keep them (export makes a new level: anything the gym does not
        // hold goes back to its default).
        [Tooltip("One-lamp levels that accept upside down: the picture counts turned any way at all on the wall (Torch2Level.anyTurn). Two-lamp levels never.")]
        public bool anyTurn=true;
        [Tooltip("Above 0: a host's lock (hold to solve) only takes the orb within this many degrees of an answer (Torch2Level.lockAngle; Birthday 10, GoodMorning 6).")]
        public float lockAngle=0;
        [Tooltip("Shape of Memory only: pieces further than Draw Gather From are drawn with that extra distance times this (1 = as placed); shadows unchanged. The gym always shows the real places.")]
        [Range(.2f,1)] public float drawGather=1;
        public float drawGatherFrom=.12f;
        [Tooltip("Gym only: shows the slides as they look at this tier (0 cold ... 4 found).")] [Range(0,4)] public float previewTier;
        [Header("Table (where the things lie before the level)")]
        [Tooltip("A twin of every piece, named like it or like one of its parents under the cluster, where it lies on the table before the level. The game lifts them from here into the orb one at a time (Play mode shows it). Empty scatters them automatically.")]
        public Transform tableLayout;
        [Tooltip("Seconds each thing takes from the table to its place.")] [Range(.3f,2)] public float transitFly=.8f;
        [Tooltip("How much of one flight the next may share (0 = strictly one at a time). Check level reports any clash.")] [Range(0,.8f)] public float transitOverlap=.3f;
        [Tooltip("Play mode only: the things fly into the orb at the answer instead of the level's start rotation.")] public bool transitToAnswer;
        [Tooltip("Play mode opens with the things flying in from the table, then the rings and lamps. Untick to start with the orb up and the lamps on (Skip intro in the Game view or Space does it once).")] public bool playIntro=true;
        [Header("Room")]
        public Material surfaceMaterial,screenMaterial,glowMaterial;
        [HideInInspector] public string report="";

        // An orb rotation to come back to (the cluster's local rotation; identity is the answer).
        [Serializable] public struct View{public string name;public Quaternion rotation;}
        // Saved views, drawn by the inspector. Never part of the level. Changes made in Play mode are kept when it ends
        // (Torch2GymSession copies the list back onto the edit-mode gym).
        [HideInInspector] public List<View> views=new List<View>();
        // Edit mode: a saved view turned the orb, and the rotation it had before the first one did (BackToOriginal). Saved
        // with the scene, so the way back survives reloads and restarts.
        [HideInInspector] public bool showingView;
        [HideInInspector] public Quaternion beforeViews=Quaternion.identity;

        public struct Piece
        {
            public string label;
            public GameObject source;
            public Mesh mesh,shadow;
            public Material[] materials;
            public bool visualOnly;
            public Vector3 position,scale;
            public Quaternion rotation;
        }
        // The cluster as last gathered, its in-memory level and the projection the walls show.
        public List<Piece> Pieces{get;private set;}=new List<Piece>();
        public Torch2Level Preview{get;private set;}
        public Torch2Projection Projection{get;private set;}
        public string Notes{get;private set;}="";
        public int[] Lamps=>walls==Walls.Left?new[]{0}:walls==Walls.Right?new[]{1}:new[]{0,1};
        // The editor uses this to make the generated room unpickable.
        public static event Action<GameObject> RoomBuilt;
        GameObject roomObject;
        Torch2Room room;
        Transform orb;
        Walls roomWalls;
        readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();
        readonly Dictionary<Mesh,Mesh> readable=new Dictionary<Mesh,Mesh>();
        int signature,cueShown;
        bool built,drawn;
        Quaternion shown;
        readonly Texture2D[] placeholders=new Texture2D[2];
        readonly string[] placeholderKeys=new string[2];

        void OnEnable()
        {
            built=false;
#if UNITY_EDITOR
            // Edit mode only updates on scene changes: draw the room once the editor settles (after loads and reloads).
            if(!Application.isPlaying)UnityEditor.EditorApplication.delayCall+=()=>{if(this&&isActiveAndEnabled)Refresh();};
#endif
        }
        void OnDisable()
        {
            // Mid-flight: the pieces go back to their places (the room with its orb goes next).
            if(transit!=null){for(int i=0;i<transitPieces.Length;i++)if(transitPieces[i])transitPieces[i].SetLocalPositionAndRotation(restPositions[i],restRotations[i]);transit=null;}
            DropRoom();
            for(int k=0;k<2;k++){Torch2Room.Kill(placeholders[k]);placeholders[k]=null;placeholderKeys[k]=null;}
            foreach(var copy in readable.Values)Torch2Room.Kill(copy);readable.Clear();
            if(Preview)Torch2Room.Kill(Preview);Preview=null;Projection=null;built=false;
        }
        void Start(){if(!Application.isPlaying)return;PlayTransit();if(!playIntro)SkipTransit();}
        void Update()
        {
            if(Application.isPlaying)CameraInput();
            if(transit!=null){StepTransit();return;}
            if(Application.isPlaying&&cluster)PlayInput();
            Refresh();
        }

        // ---- Play mode: the table to the orb, then the rings close and the lamps come on, as the game's intro does it
        // (MemoryIntro's timings); only then does the orb roll.
        const float TransitLead=.6f,RingsAt=.1f,CloseRings=.4f,LampsAt=.4f,Flicker=.4f,Rest=.3f;
        Torch2Transit transit;
        float transitTime,lampShown=-1;
        Transform[] transitPieces;
        Vector3[] restPositions;
        Quaternion[] restRotations;
        public bool Transiting=>transit!=null;
        // The editor's start picker, the one Check level uses (Torch2GymTools). While Auto Start is on, Play mode picks
        // the start with it, so the preview never opens on a start that was never checked.
        public static Func<Torch2Gym,Quaternion> StartPicker;
        float Landed=>TransitLead+transit.Length;
        public float TransitLength=>transit!=null?Landed+LampsAt+Flicker+Rest:0;
        // The table layout hides and the pieces under the cluster fly from its twins' places (or, without a full table
        // layout, from where the game scatters them) into the orb, with the lamps off; then the rings close round the pile
        // and the lamps come on over the start pose. Play mode only, so the scene comes back as it was afterwards.
        public void PlayTransit()
        {
            if(!Application.isPlaying)return;
            if(transit!=null)EndTransit();
            Refresh(true);if(Preview==null||Preview.pieces.Length==0||!orb){Debug.LogWarning("TORCH2_GYM_TRANSIT nothing under the cluster to fly in. "+Notes.Trim(),this);return;}
            bool table=Torch2Transit.HasTable(Preview);
            if(tableLayout&&!table)Debug.LogWarning("TORCH2_GYM_TRANSIT the table layout has no twin for some pieces, so all of them scatter, as the game would do: "+Notes.Trim(),this);
            if(!transitToAnswer&&autoStart&&StartPicker!=null)start=StartPicker(this);
            var rotation=transitToAnswer?Quaternion.identity:start;cluster.localRotation=rotation;Refresh();
            transit=new Torch2Transit(Preview,rotation,Torch2Projection.Home,transitFly,transitOverlap);
            int n=Pieces.Count;transitPieces=new Transform[n];restPositions=new Vector3[n];restRotations=new Quaternion[n];
            for(int i=0;i<n;i++){var t=Pieces[i].source.transform;transitPieces[i]=t;restPositions[i]=t.localPosition;restRotations[i]=t.localRotation;}
            if(tableLayout)tableLayout.gameObject.SetActive(false);
            orb.gameObject.SetActive(false);lampShown=-1;Lamp(0);rolling=false;
            transitTime=0;PoseTransit();
            Debug.Log($"TORCH2_GYM_TRANSIT {n} pieces in {transit.movers.Length} movers from {(table?"the table layout":"a scatter")}, {TransitLength:0.0} s{(Array.Exists(transit.movers,m=>m.blocked)?", some without a clear line (Check level)":"")}");
        }
        // Space skips to the end (as do the Game view's Skip intro and the inspector's Skip).
        void StepTransit()
        {
            var keyboard=Keyboard.current;
            if(keyboard!=null&&keyboard.spaceKey.wasPressedThisFrame){SkipTransit();return;}
            transitTime+=Time.deltaTime;PoseTransit();if(transitTime>=TransitLength)EndTransit();
        }
        // Straight to the end of the intro: everything in its place, the rings up, the lamps on.
        public void SkipTransit(){if(transit==null)return;transitTime=TransitLength;PoseTransit();EndTransit();}
        void PoseTransit()
        {
            // Parents before children, so a moved parent never carries a child off its pose.
            var order=new int[transitPieces.Length];for(int i=0;i<order.Length;i++)order[i]=i;
            Array.Sort(order,(a,b)=>Depth(transitPieces[a]).CompareTo(Depth(transitPieces[b])));
            foreach(int i in order){var pose=transit.Evaluate(i,transitTime-TransitLead);transitPieces[i].SetPositionAndRotation(transform.TransformPoint(pose.position),transform.rotation*pose.rotation);}
            // Once everything has landed: the rings close round it, and the lamps catch, dip and come on.
            float after=transitTime-Landed,ring=Mathf.Clamp01((after-RingsAt)/CloseRings),f=after-LampsAt;
            orb.gameObject.SetActive(ring>0);orb.localScale=Vector3.one*Back(ring);
            Lamp(f<0?0:f<.06f?.7f:f<.16f?.15f:Mathf.Clamp01(.15f+(f-.16f)/(Flicker-.16f)*.85f));
        }
        static float Back(float t){const float s=1.7f;t-=1;return t*t*((s+1)*t+s)+1;}
        static int Depth(Transform t){int d=0;for(;t;t=t.parent)d++;return d;}
        // 0 = lamps off and the walls dark, 1 = on.
        void Lamp(float level)
        {
            if(level==lampShown)return;lampShown=level;
            room.Lamps(level);if(Projection!=null)room.Light(Projection,Color.Lerp(Torch2Room.Dark,Torch2Room.Warm,level));
        }
        void EndTransit()
        {
            for(int i=0;i<transitPieces.Length;i++)if(transitPieces[i])transitPieces[i].SetLocalPositionAndRotation(restPositions[i],restRotations[i]);
            transit=null;orb.gameObject.SetActive(true);orb.localScale=Vector3.one;Lamp(1);Refresh(true);
        }

        // ---- Play mode, once the orb is up: it rolls as on the game's desktop (Torch2Input): left-drag rolls it under the
        // cursor, A/D W/S Z/C turn it. Always in place, as in Shape of Memory. V saves the view.
        bool rolling,viewMoved;
        Vector3 lastSpoke;
        float savedUntil;
        string savedName;
        GUIStyle hintStyle;
        // The Game view's camera, any time in Play mode (the flight in too): the wheel takes it nearer or further along its
        // view, right-drag pans it, the orb staying under the cursor. Play mode puts the camera back when it ends.
        public const float ViewNearest=.45f,ViewFurthest=6,ViewNotch=.12f;
        void CameraInput()
        {
            var mouse=Mouse.current;var view=Camera.main;viewMoved=false;if(mouse==null||!view)return;
            var t=view.transform;var centre=transform.TransformPoint(Torch2Projection.Home);
            float scroll=mouse.scroll.ReadValue().y;
            if(InputSystem.settings.scrollDeltaBehavior==InputSettings.ScrollDeltaBehavior.KeepPlatformSpecificInputRange&&
                (Application.platform==RuntimePlatform.WindowsEditor||Application.platform==RuntimePlatform.WindowsPlayer))scroll/=120f;
            scroll=Mathf.Clamp(scroll,-3,3);
            if(scroll!=0)
            {
                if(view.orthographic)view.orthographicSize=Mathf.Clamp(view.orthographicSize*Mathf.Exp(-scroll*ViewNotch),.05f,ViewFurthest);
                else t.position=Dolly(t.position,t.forward,centre,scroll);
                viewMoved=true;
            }
            var delta=mouse.delta.ReadValue();
            if(mouse.rightButton.isPressed&&!mouse.rightButton.wasPressedThisFrame&&delta!=Vector2.zero)
            {
                float height=view.orthographic?2*view.orthographicSize:2*Mathf.Max(Vector3.Dot(centre-t.position,t.forward),.05f)*Mathf.Tan(view.fieldOfView*.5f*Mathf.Deg2Rad);
                t.position=Pan(t.position,t.right,t.up,delta,height/Mathf.Max(1,view.pixelHeight));viewMoved=true;
            }
        }
        // The camera after notches of the wheel (up = nearer): the orb's depth in front of it changes by ViewNotch a notch,
        // within ViewNearest (clear of the rings) and ViewFurthest. The camera moves along its own view.
        public static Vector3 Dolly(Vector3 position,Vector3 forward,Vector3 centre,float notches)
        {
            float depth=Vector3.Dot(centre-position,forward);
            float target=Mathf.Clamp(depth*Mathf.Exp(-notches*ViewNotch),ViewNearest,ViewFurthest);
            return position+forward*(depth-target);
        }
        // The camera after a drag of delta pixels: what lies at the orb's depth moves with the cursor (perPixel: metres a
        // pixel there).
        public static Vector3 Pan(Vector3 position,Vector3 right,Vector3 up,Vector2 delta,float perPixel)=>position-(right*delta.x+up*delta.y)*perPixel;
        void PlayInput()
        {
            var mouse=Mouse.current;var keyboard=Keyboard.current;var view=Camera.main;if(!view)return;
            var rotation=cluster.localRotation;
            if(mouse!=null)
            {
                var ray=view.ScreenPointToRay(mouse.position.ReadValue());
                var local=new Ray(transform.InverseTransformPoint(ray.origin),transform.InverseTransformDirection(ray.direction).normalized);
                var spoke=Torch2Input.OrbPoint(local,Torch2Projection.Home,Torch2Input.OrbRadius)-Torch2Projection.Home;
                if(mouse.leftButton.wasPressedThisFrame)rolling=true;
                // A camera move shifts the cursor's point on the orb without the hand moving: no turn from it.
                else if(rolling&&mouse.leftButton.isPressed&&!viewMoved)rotation=Quaternion.FromToRotation(lastSpoke,spoke)*rotation;
                if(!mouse.leftButton.isPressed)rolling=false;
                lastSpoke=spoke;
            }
            if(keyboard!=null)rotation=Torch2Input.KeyTurn(keyboard,Time.unscaledDeltaTime,transform.InverseTransformDirection(view.transform.right),transform.InverseTransformDirection(view.transform.forward))*rotation;
            if(!rotation.Equals(cluster.localRotation))cluster.localRotation=Quaternion.Normalize(rotation);
            if(keyboard!=null&&keyboard.vKey.wasPressedThisFrame)
            {
                int i=SaveView();savedName=views[i].name;savedUntil=Time.unscaledTime+2;
                Debug.Log($"TORCH2_GYM_VIEW saved {savedName}, {AngleFromAnswer(views[i].rotation):0} degrees from the answer ({views.Count} saved; kept after Play mode)",this);
            }
        }
        // What the Game view can do, along its bottom edge.
        void OnGUI()
        {
            if(!Application.isPlaying||!cluster)return;
            if(hintStyle==null)hintStyle=new GUIStyle(GUI.skin.box){alignment=TextAnchor.MiddleLeft,fontSize=13,wordWrap=false,padding=new RectOffset(10,10,6,6)};
            bool intro=Transiting;
            string text=intro?"The things fly in from the table, then the rings close and the lamps come on. Wheel: nearer / further, right-drag: pan.":
                $"Drag rolls the orb, A/D W/S Z/C turn it. Wheel: nearer / further, right-drag: pan. V saves this view ({views.Count} saved, kept after Play). {AngleFromAnswer(cluster.localRotation):0} degrees from the answer.";
            if(!intro&&Time.unscaledTime<savedUntil)text=$"Saved {savedName}.   "+text;
            const string skip="Skip intro (Space)";var button=intro?GUI.skin.button.CalcSize(new GUIContent(skip))+new Vector2(16,8):Vector2.zero;
            var size=hintStyle.CalcSize(new GUIContent(text));float room=Screen.width-20-(intro?button.x+8:0);
            GUI.Box(new Rect(10,Screen.height-size.y-10,Mathf.Min(size.x,room),size.y),text,hintStyle);
            // The button: once the flight is under way, the intro can be cut short (the click does not turn the orb).
            if(intro&&GUI.Button(new Rect(10+Mathf.Min(size.x,room)+8,Screen.height-Mathf.Max(size.y,button.y)-10,button.x,Mathf.Max(size.y,button.y)),skip))SkipTransit();
        }

        // ---- Saved views (no undo here: the editor records it, Torch2GymTools)
        // Degrees to the nearest answer, flips included.
        public float AngleFromAnswer(Quaternion rotation)=>Projection!=null?Projection.AngleToAnswer(rotation):Quaternion.Angle(rotation,Quaternion.identity);
        public string NextViewName()
        {
            int n=0;foreach(var v in views)if(v.name!=null&&v.name.StartsWith("View ")&&int.TryParse(v.name.Substring(5),out int k))n=Mathf.Max(n,k);
            return "View "+(n+1);
        }
        // Saves the orb as it is now; returns its index.
        public int SaveView(){views.Add(new View{name=NextViewName(),rotation=Rotation(cluster.localRotation)});return views.Count-1;}
        // Turns the orb to a saved view. In edit mode the first view shown remembers where the orb was, for BackToOriginal;
        // later ones keep that. Only the cluster turns: the pieces under it, which are the answer, never move.
        public void ShowView(int i)
        {
            if(!cluster||i<0||i>=views.Count||Transiting)return;
            if(!Application.isPlaying&&!showingView){beforeViews=Rotation(cluster.localRotation);showingView=true;}
            cluster.localRotation=Rotation(views[i].rotation);Refresh();
        }
        // The orb back to the rotation it had before the first view was shown.
        public void BackToOriginal(){if(!cluster||!showingView)return;cluster.localRotation=Rotation(beforeViews);showingView=false;Refresh();}
        // The saved view the orb is at (within a tenth of a degree), or -1.
        public int ShownView()
        {
            if(!cluster)return -1;var now=cluster.localRotation;
            for(int i=0;i<views.Count;i++)if(Quaternion.Angle(now,Rotation(views[i].rotation))<.1f)return i;
            return -1;
        }
        // A usable rotation, bit for bit when it already is one: a zeroed one (a default list entry) is the answer.
        public static Quaternion Rotation(Quaternion q)
        {
            float m=q.x*q.x+q.y*q.y+q.z*q.z+q.w*q.w;
            return m<1e-8f?Quaternion.identity:Mathf.Abs(m-1)<1e-5f?q:Quaternion.Normalize(q);
        }
        // Re-gathers when anything under the cluster changed (or force), redraws the shadows when the orb turned.
        public void Refresh(bool force=false)
        {
            if(!cluster||!surfaceMaterial||!screenMaterial||!glowMaterial)return;
            if(cluster.parent!=transform){Notes="The cluster must be a direct child of the gym.";return;}
            // The orb rests where the game puts it: move the pieces, not the cluster.
            if(cluster.localPosition!=Torch2Projection.Home)cluster.localPosition=Torch2Projection.Home;
            if(cluster.localScale!=Vector3.one)cluster.localScale=Vector3.one;
            if(!roomObject||roomWalls!=walls)BuildRoom();
            int s=Signature();
            if(force||!built||s!=signature){signature=s;built=true;Rebuild();drawn=false;}
            if(Projection==null)return;
            var rotation=cluster.localRotation;
            if(!drawn||!rotation.Equals(shown)){drawn=true;shown=rotation;room.Shadows(Projection,rotation,Torch2Projection.Home);orb.localRotation=rotation;}
            int cue=CueSignature();if(cue!=cueShown){cueShown=cue;ShowCue();}
        }
        // The slides at previewTier, as Torch2Cue shows them in the game.
        void ShowCue()
        {
            Torch2Cue.Look(previewTier,out float blur,out float saturation,out float mix);
            foreach(int k in Lamps)
            {
                Texture2D slide=k==0?leftSlide:rightSlide;
                if(!slide)
                {
                    string key=id+"/"+k+"/"+accent;
                    if(!placeholders[k]||placeholderKeys[k]!=key){Torch2Room.Kill(placeholders[k]);placeholders[k]=Torch2Placeholders.Slide(id+"/"+k,accent);placeholderKeys[k]=key;}
                    slide=placeholders[k];
                }
                room.Slide(k,slide);room.Focus(k,blur,saturation,mix);
            }
        }
        int CueSignature()
        {
            unchecked
            {
                int h=roomObject.GetInstanceID();h=h*31+previewTier.GetHashCode();h=h*31+(leftSlide?leftSlide.GetInstanceID():0);h=h*31+(rightSlide?rightSlide.GetInstanceID():0);
                h=h*31+(id??"").GetHashCode();return h*31+accent.GetHashCode();
            }
        }
        void Rebuild()
        {
            var notes=new StringBuilder();Pieces=Gather(cluster,notes);if(tableLayout)PairTable(notes);
            if(Preview)Torch2Room.Kill(Preview);
            Preview=MakeLevel(Pieces,Readable);Preview.hideFlags=HideFlags.DontSave;
            try{Projection=new Torch2Projection(Preview,false);room.Light(Projection,Torch2Room.Warm);}
            catch(Exception e){Projection=null;notes.AppendLine("Preview failed: "+e.Message);}
            Notes=notes.ToString();
        }
        void BuildRoom()
        {
            DropRoom();roomWalls=walls;
            roomObject=new GameObject("Gym room (generated, not saved)");roomObject.transform.SetParent(transform,false);
            room=new Torch2Room(roomObject.transform,surfaceMaterial,screenMaterial,glowMaterial,Lamps,owned);
            orb=new GameObject("Orb").transform;orb.SetParent(roomObject.transform,false);orb.localPosition=Torch2Projection.Home;
            var rings=room.Orb(orb,out var hub);foreach(var ring in rings)room.Tint(ring,new Color(.28f,.44f,.48f,Torch2Room.RingAlpha));room.Tint(hub,new Color(.16f,.16f,.17f));
            foreach(var t in roomObject.GetComponentsInChildren<Transform>(true))t.gameObject.hideFlags=HideFlags.DontSave|HideFlags.NotEditable;
            built=false;cueShown=0;RoomBuilt?.Invoke(roomObject);
        }
        void DropRoom()
        {
            if(roomObject)Torch2Room.Kill(roomObject);roomObject=null;room=null;
            foreach(var item in owned)Torch2Room.Kill(item);owned.Clear();
        }
        Mesh Readable(Mesh mesh)
        {
            if(mesh.isReadable)return mesh;
            if(!readable.TryGetValue(mesh,out var copy)||!copy){copy=ReadableCopy(mesh);copy.hideFlags=HideFlags.DontSave;readable[mesh]=copy;}
            return copy;
        }
        int Signature()
        {
            unchecked
            {
                int h=(int)walls*31+(bothFaces?7:0)+(int)flips*131;
                var frame=cluster.worldToLocalMatrix;
                foreach(var r in cluster.GetComponentsInChildren<Renderer>())
                {
                    h=h*31+r.GetInstanceID();h=h*31+(r.enabled?1:0);
                    var filter=r.GetComponent<MeshFilter>();if(filter&&filter.sharedMesh)h=h*31+filter.sharedMesh.GetInstanceID();
                    var m=frame*r.transform.localToWorldMatrix;for(int i=0;i<16;i++)h=h*31+m[i].GetHashCode();
                    var tag=r.GetComponentInParent<ShadowPiece>();if(tag){h=h*31+(int)tag.role;h=h*31+(tag.shadowProxy?tag.shadowProxy.GetInstanceID():0);}
                }
                // The table layout's names and objects only (where things lie matters to export and Play, not the preview).
                if(tableLayout)foreach(var t in tableLayout.GetComponentsInChildren<Transform>(true)){h=h*31+t.GetInstanceID();h=h*31+t.name.GetHashCode();}
                return h;
            }
        }

        // Every enabled mesh under the cluster, posed in the cluster's frame. LOD groups count their first level only.
        public static List<Piece> Gather(Transform cluster,StringBuilder notes)
        {
            var pieces=new List<Piece>();var skipped=new HashSet<Renderer>();
            foreach(var lod in cluster.GetComponentsInChildren<LODGroup>()){var levels=lod.GetLODs();for(int i=1;i<levels.Length;i++)foreach(var r in levels[i].renderers)if(r)skipped.Add(r);}
            foreach(var skinned in cluster.GetComponentsInChildren<SkinnedMeshRenderer>())notes?.AppendLine($"{skinned.name}: skinned meshes are skipped; bake it to a static mesh.");
            var frame=cluster.worldToLocalMatrix;
            foreach(var renderer in cluster.GetComponentsInChildren<MeshRenderer>())
            {
                if(!renderer.enabled||skipped.Contains(renderer))continue;
                var filter=renderer.GetComponent<MeshFilter>();if(!filter||!filter.sharedMesh){notes?.AppendLine($"{renderer.name}: no mesh.");continue;}
                var tag=renderer.GetComponentInParent<ShadowPiece>();if(tag&&!tag.transform.IsChildOf(cluster))tag=null;
                var t=renderer.transform;
                var piece=new Piece{label=renderer.name,source=renderer.gameObject,mesh=filter.sharedMesh,materials=renderer.sharedMaterials,
                    visualOnly=tag&&tag.role==ShadowPiece.Role.VisualOnly,shadow=tag&&tag.gameObject==renderer.gameObject&&tag.shadowProxy?tag.shadowProxy:filter.sharedMesh,
                    position=cluster.InverseTransformPoint(t.position),rotation=Quaternion.Inverse(cluster.rotation)*t.rotation,scale=t.lossyScale};
                // A rotated parent with non-uniform scale skews its children, which position, rotation and scale cannot hold.
                var expected=frame*t.localToWorldMatrix;var made=Matrix4x4.TRS(piece.position,piece.rotation,piece.scale);
                for(int i=0;i<16;i++)if(Mathf.Abs(expected[i]-made[i])>1e-3f){notes?.AppendLine($"{renderer.name}: skewed by a parent's non-uniform scale, so it is stored slightly off. Scale the object itself instead.");break;}
                pieces.Add(piece);
            }
            return pieces;
        }
        // The cluster as a level: the answer is no rotation, the pieces as placed. shadowOf gives the readable mesh that
        // casts each piece's shadow (the live preview keeps copies in memory, export saves them).
        public Torch2Level MakeLevel(List<Piece> pieces,Func<Mesh,Mesh> shadowOf)
        {
            var level=ScriptableObject.CreateInstance<Torch2Level>();level.name=id;
            level.title=title;level.button=button;level.riddle=riddle;level.hint=hint;level.complete=complete;level.accent=accent;level.lamps=Lamps;
            level.solution=Quaternion.identity;level.start=start;level.iouMin=iouMin;level.bothFaces=bothFaces;
            level.leftSlide=leftSlide;level.rightSlide=rightSlide;level.layers=layers!=null?(AudioClip[])layers.Clone():new AudioClip[0];level.cueVolume=cueVolume;
            level.tiers=tiers;level.lookalikeAngle=lookalikeAngle;level.snapWarmth=snapWarmth;level.snapAngle=snapAngle;
            level.flips=flips;level.cueAngle=cueAngle;level.heldSnapAngle=heldSnapAngle;level.drawGather=drawGather;level.drawGatherFrom=drawGatherFrom;
            level.anyTurn=anyTurn;level.lockAngle=lockAngle;
            level.pieces=pieces.ConvertAll(p=>new Torch2Piece{label=p.label,mesh=p.mesh,materials=p.materials,position=p.position,euler=p.rotation.eulerAngles,scale=p.scale,
                shadowMesh=p.visualOnly?null:shadowOf(p.shadow),visualOnly=p.visualOnly}).ToArray();
            level.transitFly=transitFly;level.transitOverlap=transitOverlap;
            if(tableLayout&&PairTable(null,out var poses,out var groups))
                for(int i=0;i<level.pieces.Length;i++){level.pieces[i].onTable=true;level.pieces[i].tablePosition=poses[i].position;level.pieces[i].tableEuler=poses[i].rotation.eulerAngles;level.pieces[i].tableGroup=groups[i];}
            return level;
        }
        bool PairTable(StringBuilder notes)=>PairTable(notes,out _,out _);
        // Each gathered piece's twin in the table layout: the layout object named like the piece or like one of its
        // parents under the cluster (the outermost one that has a twin; repeated names pair in order). Pieces under one
        // twin travel as one thing. Poses are in the gym's space (the level's).
        public bool PairTable(StringBuilder notes,out Pose[] poses,out int[] groups)
        {
            int n=Pieces.Count;poses=new Pose[n];groups=new int[n];bool ok=n>0;
            var twins=new List<Transform>(tableLayout.GetComponentsInChildren<Transform>(true));twins.Remove(tableLayout);
            var paired=new Dictionary<Transform,Transform>();var taken=new HashSet<Transform>();var groupOf=new Dictionary<Transform,int>();var chain=new List<Transform>();
            for(int i=0;i<n;i++)
            {
                var t=Pieces[i].source.transform;chain.Clear();for(var a=t;a&&a!=cluster;a=a.parent)chain.Insert(0,a);
                Transform anchor=null,twin=null;
                foreach(var a in chain)
                {
                    if(paired.TryGetValue(a,out twin)){anchor=a;break;}
                    twin=twins.Find(x=>x.name==a.name&&!taken.Contains(x));if(twin){anchor=a;paired[a]=twin;taken.Add(twin);break;}
                }
                if(!anchor){notes?.AppendLine($"{t.name}: the table layout has nothing named like it or a parent under the cluster.");ok=false;continue;}
                if(!groupOf.TryGetValue(anchor,out int g))groupOf[anchor]=g=groupOf.Count;groups[i]=g;
                var m=transform.worldToLocalMatrix*twin.localToWorldMatrix*anchor.worldToLocalMatrix*t.localToWorldMatrix;poses[i]=new Pose(m.GetColumn(3),m.rotation);
                if((twin.lossyScale-anchor.lossyScale).sqrMagnitude>1e-6f)notes?.AppendLine($"{twin.name}: scaled unlike its twin under the cluster; the game moves the cluster's one.");
            }
            foreach(var twin in twins)if(!taken.Contains(twin)&&twin.GetComponent<MeshRenderer>())
            {
                bool under=false;for(var a=twin.parent;a&&a!=tableLayout;a=a.parent)if(taken.Contains(a))under=true;
                if(!under)notes?.AppendLine($"{twin.name}: in the table layout with no twin under the cluster; it stays out of the game.");
            }
            return ok;
        }
        // Positions and triangles of any mesh, readable. In the editor this also reads store meshes with Read/Write off.
        public static Mesh ReadableCopy(Mesh source)
        {
            using(var all=Mesh.AcquireReadOnlyMeshData(source))
            {
                var data=all[0];var vertices=new NativeArray<Vector3>(data.vertexCount,Allocator.Temp);data.GetVertices(vertices);
                var triangles=new List<int>();
                for(int s=0;s<data.subMeshCount;s++)
                {
                    var sub=data.GetSubMesh(s);if(sub.topology!=MeshTopology.Triangles)continue;
                    var indices=new NativeArray<int>(sub.indexCount,Allocator.Temp);data.GetIndices(indices,s);triangles.AddRange(indices);indices.Dispose();
                }
                var copy=new Mesh{name=source.name+" shadow",indexFormat=data.vertexCount>65535?IndexFormat.UInt32:IndexFormat.UInt16};
                copy.SetVertices(vertices);copy.SetTriangles(triangles,0);copy.RecalculateBounds();vertices.Dispose();return copy;
            }
        }
        // Orb, the safe radius for pieces and each lit lamp's cone onto its wall.
        void OnDrawGizmos()
        {
            if(!cluster)return;
            Gizmos.matrix=transform.localToWorldMatrix;
            Gizmos.color=new Color(.55f,.92f,1f,.35f);Gizmos.DrawWireSphere(Torch2Projection.Home,Torch2Input.OrbRadius);
            foreach(int k in Lamps)
            {
                var lamp=Torch2Projection.Lamps[k];var aim=(Torch2Projection.Home-lamp).normalized;var side=Vector3.Cross(aim,Vector3.up).normalized;var up=Vector3.Cross(side,aim);
                var n=Torch2Projection.WallNormal[k];float reach=Vector3.Dot(Torch2Projection.WallOrigin[k]-lamp,n);Gizmos.color=new Color(1f,.85f,.5f,.4f);
                for(int i=0;i<16;i++)
                {
                    float a=i*Mathf.PI/8,h=Torch2Projection.HalfAngle*Mathf.Deg2Rad;
                    var dir=aim*Mathf.Cos(h)+(side*Mathf.Cos(a)+up*Mathf.Sin(a))*Mathf.Sin(h);
                    Gizmos.DrawLine(lamp,lamp+dir*(reach/Vector3.Dot(dir,n)));
                }
            }
        }
    }
}
