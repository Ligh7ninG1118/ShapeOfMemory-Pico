using System.Collections;
using CloudGarden.ShadowPuzzle;
using PhysicalAssembly.MetaXR;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CloudGarden.Integration
{
    public sealed class ShadowDemo : MonoBehaviour, IShadowHost
    {
        public string InitialLevel = "ThreeWishes";
        public GameObject StandalonePlayer;
        public Material Surface, Screen, Glow;
        public MetaPlayerRig Player { get; private set; }
        public string Level { get; private set; }
        public GameObject Content { get; private set; }
        public WishesWorkshop Wishes { get; private set; }
        public MoonWorkshop Moon { get; private set; }
        public ShadowWorkshop Boat { get; private set; }
        public SilhouetteWorkshop Silhouette { get; private set; }
        public TorchWorkshop Torch { get; private set; }
        public Torch2Workshop Torch2 { get; private set; }
        public bool Busy => (Wishes && Wishes.Busy) || (Moon && Moon.Busy) || (Boat && Boat.Busy) || (Silhouette && Silhouette.Busy) || (Torch && Torch.Busy) || (Torch2 && Torch2.Busy);
        [Tooltip("Optional: PREV / NEXT cycle only these level ids. Empty = every shadow level.")] public string[] Playlist;
        // Set by editor tools (the gym's Play this level) before Play: open only this level instead of InitialLevel.
        public static string PendingLevel;
        // PREV / NEXT order. Everyday-object levels are SilhouetteLevel assets in Resources/ShadowArt/Levels,
        // flashlight levels are TorchLevel assets in Resources/ShadowArt/Torch, posable-pile levels Torch2Level assets in
        // Resources/ShadowArt/Torch2.
        public static readonly string[] Levels = {"ThreeWishes","MoonCourier","ShadowWorkshop","ALife","Italia","Nippon","Encore","BrightIdea","GoodBoy","Birthday","BonVoyage","GoodMorning","Home"};
        static readonly string[] LevelNames = {"THREE WISHES","MOON","BOAT","A LIFE","ITALIA","NIPPON","ENCORE","BRIGHT IDEA","GOOD BOY","BIRTHDAY","BON VOYAGE","GOOD MORNING","HOME"};
        bool switching;
        Pose? desktopView;

        IEnumerator Start()
        {
            SceneManager.SetActiveScene(gameObject.scene);
            var root = Instantiate(StandalonePlayer);
            Player = root.GetComponentInChildren<MetaPlayerRig>(true);
            Player.Initialize(Player.XRRig, Player.DesktopCamera);
            if (Player.IsXR)
            {
                float deadline = Time.realtimeSinceStartup + 3;
                while (Player.ViewCamera.transform.position.y - Player.XRRig.position.y < .4f && Time.realtimeSinceStartup < deadline) yield return null;
            }
            else
            {
                Player.ViewCamera.transform.position = new Vector3(.1f, 1.60f, -1.85f);
                Player.ViewCamera.transform.LookAt(new Vector3(0, 1.05f, .05f));
                Player.ViewCamera.fieldOfView = 48;
                Player.ViewCamera.backgroundColor = new Color(.045f, .075f, .09f);
                desktopView = new Pose(Player.ViewCamera.transform.position, Player.ViewCamera.transform.rotation);
            }
            if (!string.IsNullOrEmpty(PendingLevel)) { Playlist = new[]{PendingLevel}; LoadLevel(PendingLevel); PendingLevel = null; }
            else LoadLevel(InitialLevel);
        }
        public void LoadLevel(string level)
        {
            bool original = level == "ThreeWishes" || level == "MoonCourier" || level == "ShadowWorkshop";
            var silhouette = original ? null : SilhouetteLevel.Load(level);
            var torch = original || silhouette ? null : TorchLevel.Load(level);
            var torch2 = original || silhouette || torch ? null : Torch2Level.Load(level);
            if (Busy || switching || !Player || (!original && !silhouette && !torch && !torch2)) return;
            switching = true;
            var pose = Content ? new Pose(Content.transform.position, Content.transform.rotation) : new Pose(new Vector3(0,.72f,0), Quaternion.identity);
            bool first = !Content;
            if (Content) { Content.SetActive(false); Destroy(Content); }
            Wishes = null; Moon = null; Boat = null; Silhouette = null; Torch = null; Torch2 = null;
            Content = new GameObject(level + " gameplay"); Content.SetActive(false); Content.transform.SetParent(transform, false);
            Content.transform.SetPositionAndRotation(pose.position, pose.rotation);
            var context = Content.AddComponent<MetaShadowPlayer>(); context.Player = Player; context.Host = this;
            if (level == "ThreeWishes")
            {
                Wishes = Content.AddComponent<WishesWorkshop>(); Wishes.viewCamera = Player.ViewCamera; Wishes.xr = context;
                Wishes.surfaceMaterial = Surface; Wishes.screenMaterial = Screen; Wishes.glowMaterial = Glow;
            }
            else if (level == "MoonCourier")
            {
                Moon = Content.AddComponent<MoonWorkshop>(); Moon.viewCamera = Player.ViewCamera; Moon.xr = context;
                Moon.surfaceMaterial = Surface; Moon.screenMaterial = Screen; Moon.glowMaterial = Glow;
            }
            else if (torch2)
            {
                Torch2 = Content.AddComponent<Torch2Workshop>(); Torch2.level = torch2; Torch2.viewCamera = Player.ViewCamera; Torch2.xr = context;
                Torch2.surfaceMaterial = Surface; Torch2.screenMaterial = Screen; Torch2.glowMaterial = Glow;
            }
            else if (torch)
            {
                Torch = Content.AddComponent<TorchWorkshop>(); Torch.level = torch; Torch.viewCamera = Player.ViewCamera; Torch.xr = context;
                Torch.surfaceMaterial = Surface; Torch.screenMaterial = Screen; Torch.glowMaterial = Glow;
            }
            else if (silhouette)
            {
                Silhouette = Content.AddComponent<SilhouetteWorkshop>(); Silhouette.level = silhouette; Silhouette.viewCamera = Player.ViewCamera; Silhouette.xr = context;
                Silhouette.surfaceMaterial = Surface; Silhouette.screenMaterial = Screen; Silhouette.glowMaterial = Glow;
            }
            else
            {
                Boat = Content.AddComponent<ShadowWorkshop>(); Boat.viewCamera = Player.ViewCamera; Boat.xr = context;
                Boat.surfaceMaterial = Surface; Boat.screenMaterial = Screen; Boat.glowMaterial = Glow;
            }
            Level = level; Content.SetActive(true);
            AddLevelButtons();
            foreach (var control in Content.GetComponentsInChildren<ShadowControl>(true))
            {
                // Native ray/poke menus are gated while either hand owns a puzzle piece.
                var label = control.GetComponentInChildren<TMP_Text>();
                var color = label ? label.color : Color.white;
                control.gameObject.AddComponent<MetaMenuButton>().Initialize(control.Press,
                    () => control.Available && context.HeadReady && !Busy && !HandNearPiece(context),
                    (hover, selected) => { if (label) label.color = selected ? Color.yellow : hover ? new Color(.65f,1,.85f) : color; },
                    new Vector3(0,.51f,0), Quaternion.Euler(90,0,0));
            }
            if (first && (Player.IsXR || PhysicalAssembly.PlayAreaPose.IsSet)) Recenter();
            if (!Player.IsXR && desktopView.HasValue)
            {
                // Flashlight levels need the wall, the pile and the light in one desktop view.
                var view = Player.ViewCamera.transform;
                if (Torch) { view.position = Content.transform.TransformPoint(new Vector3(.25f,.98f,-1.80f)); view.LookAt(Content.transform.TransformPoint(new Vector3(0,.42f,.15f))); }
                // The corner room: both walls, both lamps and the pile.
                else if (Torch2) { view.position = Content.transform.TransformPoint(new Vector3(0,1.15f,-2.0f)); view.LookAt(Content.transform.TransformPoint(new Vector3(0,.40f,.30f))); }
                else view.SetPositionAndRotation(desktopView.Value.position, desktopView.Value.rotation);
            }
            switching = false;
        }
        bool HandNearPiece(MetaShadowPlayer context)
        {
            if(!Player.IsXR)return false;
            // Match the workshop priority: nearby object handling wins over a distant ray button.
            for(int hand=0;hand<2;hand++)
            {
                if(!context.ReadHand(hand==0,out _,out var tip,out _))continue;
                if(Torch){if(hand==Torch.TurnHand&&TorchInput.Near(Torch.transform.InverseTransformPoint(tip)))return true;continue;}
                if(Torch2){if(Torch2.input.Near(tip))return true;continue;}
                var pieces=Wishes?Wishes.pieces:Moon?Moon.pieces:Silhouette?Silhouette.pieces:Boat.pieces;
                for(int i=0;i<pieces.Length;i++)
                {
                    bool available=Wishes?Wishes.CanGrab(i):Moon?Moon.CanGrab(i):Silhouette?Silhouette.CanGrab(i):Boat.CanGrab(i);
                    if(!available)continue;
                    if(Wishes)
                    {
                        if(Vector3.Distance(tip,Wishes.ClosestPoint(i,tip))<.045f)return true;
                    }
                    else if(Silhouette)
                    {
                        if(Vector3.Distance(tip,Silhouette.ClosestPoint(i,tip))<.045f)return true;
                    }
                    else
                    {
                        var collider=pieces[i].GetComponent<Collider>();
                        if(collider && Vector3.Distance(tip,collider.ClosestPoint(tip))<.045f)return true;
                    }
                }
            }
            return false;
        }
        public void Recenter()
        {
            if (!Content || Busy) return;
            if(PhysicalAssembly.PlayAreaPose.IsSet)
            {
                var pose=PhysicalAssembly.PlayAreaPose.AtHeight(.72f); Content.transform.SetPositionAndRotation(pose.position,pose.rotation);
                if(!Player.IsXR)
                {
                    var view=Player.ViewCamera.transform;
                    view.position=Content.transform.TransformPoint(new Vector3(.1f,.88f,-1.85f)); view.LookAt(Content.transform.TransformPoint(new Vector3(0,.33f,.05f)));
                    desktopView=new Pose(view.position,view.rotation);
                }
                return;
            }
            if (!Player.IsXR) { Content.transform.SetPositionAndRotation(new Vector3(0,.72f,0),Quaternion.identity); return; }
            var head = Player.ViewCamera.transform;
            var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .1f) forward = Vector3.forward;
            float floor = Player.FloorHeight;
            Content.transform.SetPositionAndRotation(new Vector3(head.position.x, floor + .72f, head.position.z) + forward * .65f, Quaternion.LookRotation(forward));
        }
        // Every shadow level gets the same PREV / NEXT pair beside the corner buttons.
        void AddLevelButtons()
        {
            var list = Playlist != null && Playlist.Length > 0 ? Playlist : Levels;
            int index = System.Array.IndexOf(list, Level); if (index < 0) return;
            string previous = list[(index + list.Length - 1) % list.Length], next = list[(index + 1) % list.Length];
            AddControl("PREV LEVEL", "< " + Name(previous), -.325f, .16f, () => LoadLevel(previous), () => !Busy);
            AddControl("NEXT LEVEL", Name(next) + " >", .325f, .16f, () => LoadLevel(next), () => !Busy);
        }
        static string Name(string level)
        {
            int index = System.Array.IndexOf(Levels, level); return index >= 0 ? LevelNames[index] : level.ToUpperInvariant();
        }
        void AddControl(string name, string caption, float x, float width, System.Action action, System.Func<bool> enabled)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
            go.transform.SetParent(Content.transform,false); go.transform.localPosition = new Vector3(x,.025f,-.55f);
            go.transform.localScale = new Vector3(width,.03f,.065f); go.transform.localRotation = Quaternion.Euler(-20,0,0);
            go.GetComponent<Renderer>().sharedMaterial = Surface;
            var control = go.AddComponent<ShadowControl>(); control.action = action; control.enabledWhen = enabled;
            var label = new GameObject(name + " label").AddComponent<TextMeshPro>(); label.transform.SetParent(Content.transform,false);
            label.transform.localPosition = new Vector3(x,.044f,-.556f); label.transform.localRotation = Quaternion.Euler(70,0,0);
            label.text = caption; label.fontSize = .12f; label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.sizeDelta = new Vector2(width - .02f,.04f); label.transform.SetParent(go.transform,true);
            if (Wishes) Wishes.world.controls.Add(control); else if (Moon) Moon.world.controls.Add(control); else if (Silhouette) Silhouette.world.controls.Add(control);
            else if (Torch) Torch.world.controls.Add(control); else if (Torch2) Torch2.world.controls.Add(control); else Boat.world.controls.Add(control);
        }
    }
}
