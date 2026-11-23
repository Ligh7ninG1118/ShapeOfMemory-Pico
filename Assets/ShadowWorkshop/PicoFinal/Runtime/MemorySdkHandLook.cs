using System.Collections.Generic;
using Oculus.Interaction;
using Oculus.Interaction.Input;
using UnityEngine;

namespace ShapeOfMemory
{
    // The Quest (OVR rig) hands in a hand look (MemoryGame.HandLook: Lantern). It keeps Meta's own hand exactly as the
    // Interaction SDK's HandVisual poses it: fitted to Meta's skeleton, so it sits on the real hand. Only the materials
    // change to the look's, and the look's motes are added. 2026-11-16, the user asked to use Meta's hand mesh, change the
    // shader and add the VFX.
    // The mesh itself is never touched (the Oculus SDK licence allows no derivative works). The Pico rig never comes
    // here: MemoryGame calls this on the OVR rig only, and draws MemoryHandSkin on the Pico rig.
    // After each HandVisual poses its skeleton (WhenHandVisualUpdated), this sets the hand-space values the hand shaders
    // read (MemoryHandSkin.SetHandSpace) and the pinch's _Close. They go into the block of the renderer's
    // MaterialPropertyBlockEditor: that editor re-applies its block every frame and would wipe a separate one.
    // The ghost hand, as on the Pico (MemoryGhostHand; 2026-11-17, the user asked for it on the Quest too):
    // - The drawn hand stops on solid things. The visual's root is moved by the solver's offset right after the SDK
    //   poses it. The SDK sets that root to the tracked pose on every update (HandVisual updateRootPose), so the offset
    //   never adds up. Nothing else reads the visual's transforms (checked on MetaHackathonPlayer.prefab), and input stays
    //   on the tracked joints.
    // - A faint copy of Meta's hand (the root's subtree, instantiated once) shows the tracked pose, in the ghost material.
    // - A hand deep in something hides its drawn copy. MemoryGame.HideLostSdkHands does that through ForceOffVisibility
    //   (Ghosted), the one switch the SDK doesn't undo.
    public sealed class MemorySdkHandLook : MonoBehaviour
    {
        // Meta's hand mesh, measured 2026-11-16 on OpenXRLeft/RightHand.fbx and OVRHand_L/R.fbx
        // (com.meta.xr.sdk.interaction), the same on all four. Metres at hand scale 1, scaled with Hand.Scale.
        // - The forearm stub is closed by a slanted cap: its back-facing surface runs from 2.19 cm behind the wrist joint
        //   (StubCut) to 0.98 cm (CapFront). A fade that is still partly lit at CapFront draws the cap's edge as a line
        //   across the back of the hand. So the fade is 0 by 0.88 cm behind the wrist and full 0.4 cm past it.
        public static readonly Vector2 WristFade=new Vector2(-.0088f,.0040f);
        public const float StubCut=-.0219f,CapFront=-.0098f;
        // - The tip joints sit halfway along the last phalanx, not at the fingertip (index: tip joint 11.6 mm past the
        //   distal joint, then 11.1 mm more mesh). So the aqua pads go by the mesh: from the distal joint, toward the tip
        //   joint, this far (to the fingertip), less MemoryHandSkin.TipNear, as on our own mesh.
        public const float IndexReach=.0227f,ThumbReach=.0248f;

        sealed class View
        {
            public HandVisual Visual;          // null for a hand built by a test (AddFor)
            public Transform Root;             // what the SDK poses at the tracked root each update
            public SkinnedMeshRenderer Skin;
            public Material[] Original;
            public MaterialPropertyBlock Block;
            public bool Left;
            public readonly Vector3[] Tracked=new Vector3[MemoryHandSkin.Count];
            public readonly Vector3[] Joints=new Vector3[MemoryHandSkin.Count];   // drawn: tracked + the ghost's offset
            public bool Posed,Ghosted;
            public ParticleSystem Motes;
            public float Due;
            public System.Action OnUpdated;
            // The ghost: a copy of Root's subtree (Bones[k] copies Source[k]), its renderer, block and material.
            public Transform[] Source,Bones;
            public SkinnedMeshRenderer GhostSkin;
            public MaterialPropertyBlock GhostBlock;
            public Material GhostMaterial;
        }
        readonly List<View> views=new List<View>();
        public MemoryHandLook Look{get;private set;}
        // How many SDK hands are drawn in the look (2 on the Quest rig: the reticle hands keep their wire look).
        public int Count=>views.Count;

        // Set by the host, as MemoryHandSkin's. Ghost: the solids the drawn hands stop on (null: they go through).
        // GhostMaterial: Shaders/Hands/HandGhost (MemoryHandSkin.GhostMaterialPath), tinted GhostColor at the ghost's
        // alpha. Seen: whether a hand is in view at all (MetaShadowPlayer.Seen; null: whenever its data is valid).
        public MemoryGhostHand Ghost;
        public Material GhostMaterial;
        public Color GhostColor=new Color(.93f,.91f,1f,.40f);
        public System.Func<bool,bool> Seen;

        public static MemorySdkHandLook Add(GameObject host,IEnumerable<HandVisual> visuals,MemoryHandLook look)
        {
            var it=host.AddComponent<MemorySdkHandLook>();it.Look=look;
            foreach(var visual in visuals)
            {
                if(!visual||visual.Hand==null||Reticle(visual.transform))continue;
                // The renderer HandVisual drives: its Awake left only one of its two models (Meta's own or the OpenXR
                // skeleton's) active.
                foreach(var skin in visual.GetComponentsInChildren<SkinnedMeshRenderer>(false))
                {
                    var view=it.Restyle(skin,visual.Root,visual.Hand.Handedness==Handedness.Left);
                    view.Visual=visual;view.OnUpdated=()=>it.Pose(view);
                    visual.WhenHandVisualUpdated+=view.OnUpdated;
                }
            }
            return it;
        }
        // A hand without a HandVisual (tests): skin under root, posed by the caller, stepped with Step.
        public static MemorySdkHandLook AddFor(GameObject host,MemoryHandLook look,SkinnedMeshRenderer skin,Transform root,bool left)
        {
            var it=host.AddComponent<MemorySdkHandLook>();it.Look=look;
            it.Restyle(skin,root,left);
            return it;
        }
        // The distance-grab reticle hands (…/Visuals/LeftHandReticle/…): ghosts of where a far grab would land. They
        // keep their wire look and get no motes.
        static bool Reticle(Transform t)
        {
            for(;t;t=t.parent)if(t.name.Contains("Reticle"))return true;
            return false;
        }
        View Restyle(SkinnedMeshRenderer skin,Transform root,bool left)
        {
            var editor=skin.GetComponent<MaterialPropertyBlockEditor>();
            var view=new View{Root=root?root:skin.transform.parent,Skin=skin,Original=skin.sharedMaterials,Left=left,
                Block=editor?editor.MaterialPropertyBlock:new MaterialPropertyBlock()};
            skin.sharedMaterials=Look.Materials;
            if(Look.Motes&&Look.MoteMaterial)
            {
                var holder=new GameObject((left?"Left":"Right")+" SDK hand motes").transform;holder.SetParent(transform,false);
                view.Motes=MemoryHandSkin.SkinnedHand.Motes(holder,Look);
            }
            views.Add(view);
            return view;
        }

        // ---- Each update of a hand
        void Pose(View view)
        {
            var hand=view.Visual.Hand;
            bool seen=Seen==null||Seen(view.Left);
            bool tracked=seen&&Read(hand,view.Tracked);
            Step(view,tracked?view.Tracked:null,Mathf.Max(.1f,hand.Scale));
        }
        // One update, right after the hand's root and joints were posed at the tracked hand (tests call it for AddFor's
        // hand). tracked: its joints (world, MemoryHandSkin.Joints order); null: not in view.
        public void Step(int index,IReadOnlyList<Vector3> tracked,float scale=1)=>Step(views[index],tracked,scale);
        void Step(View view,IReadOnlyList<Vector3> tracked,float scale)
        {
            int h=view.Left?0:1;
            if(tracked==null){view.Posed=false;view.Ghosted=false;Ghost?.Lost(h);ShowGhost(view,0,null,scale);return;}
            var offset=Vector3.zero;float shown=0;
            if(Ghost!=null)
            {
                offset=Ghost.Solve(h,tracked);
                shown=MemoryGhostHand.Shown(offset);
                // As MemoryHandSkin.Settle: hidden past half the ghost's fade-in, back below a quarter.
                if(!view.Ghosted&&shown>MemoryHandSkin.HideFrom)view.Ghosted=true;
                else if(view.Ghosted&&shown<MemoryHandSkin.ShowBelow)view.Ghosted=false;
            }
            // The ghost copies the tracked pose before the drawn hand moves; alone, it shows in full.
            ShowGhost(view,view.Ghosted?1:shown,tracked,scale);
            for(int i=0;i<MemoryHandSkin.Count;i++)view.Joints[i]=tracked[i]+offset;
            if(offset!=Vector3.zero&&view.Root)view.Root.position+=offset;
            Apply(view.Skin,view.Block,view.Joints,view.Left,scale);
            view.Posed=true;
        }
        void ShowGhost(View view,float alpha,IReadOnlyList<Vector3> tracked,float scale)
        {
            if(alpha<=0||tracked==null||!GhostMaterial||!view.Root){if(view.GhostSkin)view.GhostSkin.enabled=false;return;}
            if(!view.GhostSkin&&!MakeGhost(view))return;
            for(int k=0;k<view.Source.Length;k++)
            {
                var from=view.Source[k];if(!from)continue;
                view.Bones[k].SetLocalPositionAndRotation(from.localPosition,from.localRotation);
                view.Bones[k].localScale=from.localScale;
            }
            var c=GhostColor;c.a*=alpha;view.GhostMaterial.SetColor(BaseColorId,c);
            Apply(view.GhostSkin,view.GhostBlock,tracked,view.Left,scale);
            view.GhostSkin.enabled=true;
        }
        static readonly int BaseColorId=Shader.PropertyToID("_BaseColor");
        bool MakeGhost(View view)
        {
            var copy=Instantiate(view.Root.gameObject,view.Root.parent,false);
            copy.name=(view.Left?"Left":"Right")+" hand ghost";
            // The copy's SDK block editors would set their own blocks on it every frame.
            foreach(var editor in copy.GetComponentsInChildren<MaterialPropertyBlockEditor>(true)){editor.enabled=false;Kill(editor);}
            view.Source=view.Root.GetComponentsInChildren<Transform>(true);
            view.Bones=copy.GetComponentsInChildren<Transform>(true);
            if(view.Bones.Length!=view.Source.Length){Kill(copy);view.Source=view.Bones=null;return false;}
            // The copy's renderer is the one at the skin's place in the subtree.
            int at=System.Array.IndexOf(view.Source,view.Skin.transform);
            view.GhostSkin=at>=0?view.Bones[at].GetComponent<SkinnedMeshRenderer>():null;
            if(!view.GhostSkin){Kill(copy);view.Source=view.Bones=null;return false;}
            foreach(var other in copy.GetComponentsInChildren<Renderer>(true))if(other!=view.GhostSkin)other.enabled=false;
            view.GhostMaterial=new Material(GhostMaterial){name="Ghost hand",renderQueue=3060};
            view.GhostSkin.sharedMaterials=new[]{view.GhostMaterial};
            view.GhostBlock=new MaterialPropertyBlock();
            return true;
        }
        static void Kill(Object o){if(!o)return;if(Application.isPlaying)Destroy(o);else DestroyImmediate(o);}
        // Makes the ghost copies up front (hidden), so the first touch of something solid doesn't instantiate mid-play.
        public void Prewarm()
        {
            if(!GhostMaterial)return;
            foreach(var view in views)if(!view.GhostSkin&&view.Root&&MakeGhost(view))view.GhostSkin.enabled=false;
        }

        // Whether visual's hand is deep enough in something that only its ghost shows (MemoryGame hides the drawn one).
        public bool Ghosted(HandVisual visual)
        {
            foreach(var view in views)if(view.Visual==visual&&view.Ghosted)return true;
            return false;
        }
        public bool Ghosted(int index)=>views[index].Ghosted;
        public bool GhostShown(int index)=>views[index].GhostSkin&&views[index].GhostSkin.enabled;
        public Transform GhostRoot(int index)=>views[index].GhostSkin?views[index].Bones[0]:null;

        // The shaders' per-hand values into block, and the block onto the renderer (tests call it). scale: Hand.Scale,
        // which the SDK sizes Meta's mesh by.
        public static void Apply(Renderer skin,MaterialPropertyBlock block,IReadOnlyList<Vector3> joints,bool left,float scale=1)
        {
            block.SetFloat(MemoryHandSkin.CloseId,Mathf.InverseLerp(MemoryHandSkin.PinchOpen,MemoryHandSkin.PinchClosed,
                Vector3.Distance(joints[MemoryHandSkin.IndexTip],joints[MemoryHandSkin.ThumbTip])));
            MemoryHandSkin.SetHandSpace(block,joints,left,WristFade*scale,
                Pad(joints,MemoryHandSkin.IndexTip,IndexReach*scale),Pad(joints,MemoryHandSkin.ThumbTip,ThumbReach*scale));
            skin.SetPropertyBlock(block);
        }
        // A pad on Meta's mesh: reach from the distal joint toward the tip joint is the fingertip; TipNear short of it.
        public static Vector3 Pad(IReadOnlyList<Vector3> j,int tip,float reach)
        {
            var distal=j[tip-1];var along=j[tip]-distal;
            return distal+(along.sqrMagnitude>1e-12f?along.normalized:Vector3.zero)*(reach-MemoryHandSkin.TipNear);
        }
        // The hand's joints (world) in MemoryHandSkin.Joints order: the same Hand the visual is posed from (Meta's
        // SyntheticHand, which a hand grab may snap), so the values match the drawn mesh.
        static bool Read(IHand hand,Vector3[] joints)
        {
            if(hand==null||!hand.IsTrackedDataValid)return false;
            for(int i=0;i<MemoryHandSkin.Count;i++){if(!hand.GetJointPose(MemoryHandSkin.Joints[i],out var pose))return false;joints[i]=pose.position;}
            return true;
        }
        void LateUpdate()
        {
            foreach(var view in views)
            {
                if(!view.Motes)continue;
                bool shown=view.Skin&&view.Skin.enabled&&view.Posed;
                if(!shown){if(view.Motes.particleCount>0)view.Motes.Clear();view.Due=0;continue;}
                MemoryHandSkin.SkinnedHand.EmitMotes(view.Motes,Look,view.Joints,ref view.Due,Time.deltaTime);
            }
        }
        // Back to the SDK's own look (switching it off in play, or the game going away).
        void OnDestroy()
        {
            foreach(var view in views)
            {
                if(view.Visual)view.Visual.WhenHandVisualUpdated-=view.OnUpdated;
                if(view.Skin)view.Skin.sharedMaterials=view.Original;
                if(view.Motes)Kill(view.Motes.transform.parent.gameObject);
                if(view.GhostSkin)Kill(view.Bones[0].gameObject);
                if(view.GhostMaterial)Kill(view.GhostMaterial);
            }
            views.Clear();
        }
    }
}
