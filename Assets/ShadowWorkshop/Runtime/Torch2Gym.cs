using System;
using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace CloudGarden.ShadowPuzzle
{
    // The designers' gym for torch-2 levels. Everything under the cluster is the pile, and the pieces as placed are the
    // answer. The cluster itself is the orb: it stays at the rest position, and its rotation only previews the orb
    // (identity is the answer). The game's own room (Torch2Room) shows the pile's shadow live in edit mode, so blocking
    // out needs no Play mode. ShadowPiece marks objects as visual only or gives them a simpler shadow mesh. The table
    // layout holds a twin of each piece where it lies on the table before the level; Play mode shows them lifting into the
    // orb (Torch2Transit), and export stores it. Checks, export, open-in-gym and play live in the editor (Torch2GymEditor,
    // MetaHackthone > Shadow Workshop > Gym).
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
        [Header("Room")]
        public Material surfaceMaterial,screenMaterial,glowMaterial;
        [HideInInspector] public string report="";

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
            DropRoom();
            for(int k=0;k<2;k++){Torch2Room.Kill(placeholders[k]);placeholders[k]=null;placeholderKeys[k]=null;}
            foreach(var copy in readable.Values)Torch2Room.Kill(copy);readable.Clear();
            if(Preview)Torch2Room.Kill(Preview);Preview=null;Projection=null;built=false;
        }
        void Start()
        {
            if(!Application.isPlaying)return;
            if(tableLayout)PlayTransit();
            else Debug.Log("TORCH2_GYM_TRANSIT nothing to play: set Table Layout on the gym to the object holding the table copies of the cluster's pieces.",this);
        }
        void Update(){if(transit!=null)StepTransit();else Refresh();}

        // ---- Play mode: the table to the orb, as the game's intro does it
        const float TransitLead=.6f;
        Torch2Transit transit;
        float transitTime;
        Transform[] transitPieces;
        Vector3[] restPositions;
        Quaternion[] restRotations;
        public bool Transiting=>transit!=null;
        // The editor's start picker, the one Check level uses (Torch2GymTools). While Auto Start is on, Play mode picks
        // the start with it, so the preview never opens on a start that was never checked.
        public static Func<Torch2Gym,Quaternion> StartPicker;
        public float TransitLength=>transit!=null?TransitLead+transit.Length:0;
        // The table layout hides and its twins under the cluster fly from its places into the orb, with the lamps off;
        // then the lamps come on over the start pose. Play mode only, so the scene comes back as it was afterwards.
        public void PlayTransit()
        {
            if(!Application.isPlaying)return;
            if(transit!=null)EndTransit();
            Refresh(true);if(Preview==null||!Torch2Transit.HasTable(Preview)){Debug.LogWarning("TORCH2_GYM_TRANSIT needs a table layout with a twin of every piece: "+Notes.Trim());return;}
            if(!transitToAnswer&&autoStart&&StartPicker!=null)start=StartPicker(this);
            var rotation=transitToAnswer?Quaternion.identity:start;cluster.localRotation=rotation;
            transit=new Torch2Transit(Preview,rotation,Torch2Projection.Home,transitFly,transitOverlap);
            int n=Pieces.Count;transitPieces=new Transform[n];restPositions=new Vector3[n];restRotations=new Quaternion[n];
            for(int i=0;i<n;i++){var t=Pieces[i].source.transform;transitPieces[i]=t;restPositions[i]=t.localPosition;restRotations[i]=t.localRotation;}
            tableLayout.gameObject.SetActive(false);orb.gameObject.SetActive(false);room.Lamps(0);if(Projection!=null)room.Light(Projection,Torch2Room.Dark);
            transitTime=0;PoseTransit();
            Debug.Log($"TORCH2_GYM_TRANSIT {n} pieces in {transit.movers.Length} movers, {TransitLength:0.0} s{(Array.Exists(transit.movers,m=>m.blocked)?", some without a clear line (Check level)":"")}");
        }
        void StepTransit(){transitTime+=Time.deltaTime;PoseTransit();if(transitTime>=TransitLength)EndTransit();}
        void PoseTransit()
        {
            // Parents before children, so a moved parent never carries a child off its pose.
            var order=new int[transitPieces.Length];for(int i=0;i<order.Length;i++)order[i]=i;
            Array.Sort(order,(a,b)=>Depth(transitPieces[a]).CompareTo(Depth(transitPieces[b])));
            foreach(int i in order){var pose=transit.Evaluate(i,transitTime-TransitLead);transitPieces[i].SetPositionAndRotation(transform.TransformPoint(pose.position),transform.rotation*pose.rotation);}
        }
        static int Depth(Transform t){int d=0;for(;t;t=t.parent)d++;return d;}
        void EndTransit()
        {
            for(int i=0;i<transitPieces.Length;i++)if(transitPieces[i])transitPieces[i].SetLocalPositionAndRotation(restPositions[i],restRotations[i]);
            transit=null;orb.gameObject.SetActive(true);room.Lamps(1);Refresh(true);
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
