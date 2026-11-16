using System.Collections.Generic;
using CloudGarden.Integration;
using Oculus.Interaction.Input;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShapeOfMemory
{
    // The player's hands as a skinned mesh in a hand look (MemoryHandLook: Ink, Lantern), in place of the joint-drawn
    // bones (MemoryHands). The mesh is ours (Art/Hands/MemoryHand_R/_L, built by Tools/ShadowArt/build_hand.py).
    // The bones are placed from the tracked joint POSITIONS alone, never the runtime's joint rotations or the mesh's own
    // bone lengths: each joint's bone sits on the joint, points at its child joint and stretches to reach it, turned
    // about that line by the hand's own frame. So the mesh takes the tracked hand's proportions on any runtime (the
    // SDK's skinned hand, fitted to Meta's skeleton, sat off the real hands on the Pico), and its fingertips are where
    // MemoryTouch presses.
    // Bones are the 26 OpenXR joints in HandJointId order (Palm 0, Wrist 1, Thumb 2-5, Index 6-10, Middle 11-15,
    // Ring 16-20, Little 21-25), kept flat (no hierarchy) so each takes its own stretch.
    public sealed class MemoryHandSkin : MonoBehaviour
    {
        public const int Count=26,Palm=0,Wrist=1,ThumbTip=5,IndexProximal=7,IndexTip=10,MiddleProximal=12,LittleProximal=22;
        public static readonly HandJointId[] Joints=
        {
            HandJointId.HandPalm,HandJointId.HandWristRoot,
            HandJointId.HandThumb1,HandJointId.HandThumb2,HandJointId.HandThumb3,HandJointId.HandThumbTip,
            HandJointId.HandIndex0,HandJointId.HandIndex1,HandJointId.HandIndex2,HandJointId.HandIndex3,HandJointId.HandIndexTip,
            HandJointId.HandMiddle0,HandJointId.HandMiddle1,HandJointId.HandMiddle2,HandJointId.HandMiddle3,HandJointId.HandMiddleTip,
            HandJointId.HandRing0,HandJointId.HandRing1,HandJointId.HandRing2,HandJointId.HandRing3,HandJointId.HandRingTip,
            HandJointId.HandPinky0,HandJointId.HandPinky1,HandJointId.HandPinky2,HandJointId.HandPinky3,HandJointId.HandPinkyTip,
        };
        public static readonly string[] Names=
        {
            "Palm","Wrist","ThumbMetacarpal","ThumbProximal","ThumbDistal","ThumbTip",
            "IndexMetacarpal","IndexProximal","IndexIntermediate","IndexDistal","IndexTip",
            "MiddleMetacarpal","MiddleProximal","MiddleIntermediate","MiddleDistal","MiddleTip",
            "RingMetacarpal","RingProximal","RingIntermediate","RingDistal","RingTip",
            "LittleMetacarpal","LittleProximal","LittleIntermediate","LittleDistal","LittleTip",
        };
        // Each joint's child, whose way its bone points (-1: a tip, which carries on its distal bone's line). The palm
        // and the wrist point at the middle knuckle.
        public static readonly int[] Child={MiddleProximal,MiddleProximal,3,4,5,-1,7,8,9,10,-1,12,13,14,15,-1,17,18,19,20,-1,22,23,24,25,-1};
        // Pinch closeness for the tips' colour, as MemoryHands: from 6 cm apart to 2.8 cm (where a pinch registers).
        public const float PinchOpen=.06f,PinchClosed=.028f;
        public static readonly int CloseId=Shader.PropertyToID("_Close");

        // ---- The bone placement (shared by the importer's bind poses, the game and the editor preview)

        // A rotation whose forward is along and whose right is side (made square to along).
        public static Quaternion Frame(Vector3 along,Vector3 side)
        {
            var z=along.sqrMagnitude>1e-12f?along.normalized:Vector3.forward;
            var x=side-z*Vector3.Dot(side,z);
            if(x.sqrMagnitude<1e-10f)x=Vector3.Cross(Mathf.Abs(z.y)<.9f?Vector3.up:Vector3.right,z);
            x.Normalize();
            return Quaternion.LookRotation(z,Vector3.Cross(z,x));
        }
        // The hand's frame: forward from the wrist to the middle knuckle, right across the knuckles from the index to the
        // little finger.
        public static Quaternion HandFrame(IReadOnlyList<Vector3> j)=>Frame(j[MiddleProximal]-j[Wrist],j[LittleProximal]-j[IndexProximal]);
        // Joint i's bone line: to its child, or on along the distal bone for a tip.
        public static Vector3 Along(IReadOnlyList<Vector3> j,int i)=>Child[i]>=0?j[Child[i]]-j[i]:j[i]-j[i-1];
        // The rest pose's bone frames as bind poses: each bone turned about its line so that its right is the line's own
        // bend axis (across the line and the palm's normal).
        public static Matrix4x4[] Bindposes(IReadOnlyList<Vector3> rest)
        {
            var hand=HandFrame(rest);var normal=hand*Vector3.up;
            var b=new Matrix4x4[Count];
            for(int i=0;i<Count;i++)
            {
                var along=Along(rest,i);var side=Vector3.Cross(along,normal);
                if(side.sqrMagnitude<1e-10f)side=hand*Vector3.right;
                b[i]=Matrix4x4.TRS(rest[i],Frame(along,side),Vector3.one).inverse;
            }
            return b;
        }

        // What the bones need from a hand mesh: its rest joints, bone lengths, and each bone's turn in the hand's frame.
        // Two kinds of mesh:
        // - ours (no bone names): one bind pose per joint in Joints order, each bone's +z along its bone, so a bone
        //   stretches along z to reach its child;
        // - anyone else's rig, as it is (2026-11-15: the Unity XR Hands sample hand, which its licence lets us use but not
        //   modify): boneNames gives the joint of each of the mesh's bones ("R_IndexProximal" -> IndexProximal). Each
        //   bone keeps its own axes: placed on its joint, turned as the joint's frame (bone line + the hand's side) turns
        //   from rest, and scaled with the hand's size (wrist to middle knuckle against the mesh's).
        public sealed class Rig
        {
            public readonly Vector3[] Rest=new Vector3[Count];
            // The rest joints in our own mesh's layout (wrist at 0, fingers +y, back of the hand +z, the right thumb +x, the
            // left's -x), whatever way the model was authored: what MemoryHandPose's made-up poses start from.
            public readonly Vector3[] Canonical=new Vector3[Count];
            public readonly float[] Length=new float[Count];
            readonly Vector3[] side=new Vector3[Count];
            // An external rig: the joint of each mesh bone, its turn from the joint's frame, its rest scale.
            readonly int[] jointOf;
            readonly Quaternion[] offset;
            readonly Vector3[] restScale;
            readonly float restSpan;
            public int BoneCount=>jointOf!=null?jointOf.Length:Count;
            // An external rig's mesh units in metres (1: metres, .01: centimetres); our meshes are in metres.
            public float Unit{get;private set;}=1;
            public bool External=>jointOf!=null;
            public Rig(Mesh mesh,IReadOnlyList<string> boneNames=null)
            {
                var bind=mesh.bindposes;
                if(boneNames==null)
                {
                    if(bind.Length!=Count)throw new System.ArgumentException($"{mesh.name} has {bind.Length} bind poses, not the {Count} hand joints");
                    var turn=new Quaternion[Count];
                    for(int i=0;i<Count;i++){var m=bind[i].inverse;Rest[i]=m.GetPosition();turn[i]=m.rotation;}
                    var hand=Quaternion.Inverse(HandFrame(Rest));
                    for(int i=0;i<Count;i++){Length[i]=Along(Rest,i).magnitude;side[i]=hand*(turn[i]*Vector3.right);}
                    System.Array.Copy(Rest,Canonical,Count);
                    return;
                }
                if(boneNames.Count!=bind.Length)throw new System.ArgumentException($"{mesh.name}: {boneNames.Count} bone names for {bind.Length} bind poses");
                jointOf=new int[bind.Length];offset=new Quaternion[bind.Length];restScale=new Vector3[bind.Length];
                var found=new bool[Count];
                for(int k=0;k<bind.Length;k++)
                {
                    int j=JointOf(boneNames[k]);
                    if(j<0)throw new System.ArgumentException($"{mesh.name}: bone {boneNames[k]} isn't one of the 26 hand joints");
                    jointOf[k]=j;var m=bind[k].inverse;Rest[j]=m.GetPosition();restScale[k]=m.lossyScale;found[j]=true;
                }
                for(int j=0;j<Count;j++)if(!found[j])throw new System.ArgumentException($"{mesh.name} has no bone for {Names[j]}");
                // Units: a model may come in centimetres (the Unity sample's FBX does). A hand is about 8.5 cm from the wrist
                // to the middle knuckle, so take the power of ten that puts it near that, and keep Rest in metres.
                float span=(Rest[MiddleProximal]-Rest[Wrist]).magnitude;
                Unit=span>1e-9f?Mathf.Pow(10,Mathf.Round(Mathf.Log10(.085f/span))):1;
                for(int j=0;j<Count;j++)Rest[j]*=Unit;
                for(int k=0;k<bind.Length;k++)restScale[k]*=Unit;
                var restHand=HandFrame(Rest);
                for(int k=0;k<bind.Length;k++)
                {
                    int j=jointOf[k];var m=bind[k].inverse;
                    var frame=Frame(Along(Rest,j),restHand*Vector3.right);
                    offset[k]=Quaternion.Inverse(frame)*m.rotation;
                }
                for(int j=0;j<Count;j++)Length[j]=Along(Rest,j).magnitude;
                restSpan=Mathf.Max(1e-6f,(Rest[MiddleProximal]-Rest[Wrist]).magnitude);
                bool leftHand=false;foreach(var b in boneNames)if(b!=null&&b.StartsWith("L_")){leftHand=true;break;}
                var toLocal=HandToLocal(Rest,leftHand);
                for(int j=0;j<Count;j++){var c=toLocal.MultiplyPoint3x4(Rest[j]);if(leftHand)c.x=-c.x;Canonical[j]=c;}
            }
            // "R_IndexProximal", "L_Wrist", "IndexProximal": the joint index, or -1.
            public static int JointOf(string bone)
            {
                if(string.IsNullOrEmpty(bone))return -1;
                int cut=bone.IndexOf('_');var name=cut>=0&&cut<=2?bone.Substring(cut+1):bone;
                return System.Array.IndexOf(Names,name);
            }
            // Places the bones on joints (world). parentScale: the bones' parent's uniform world scale.
            public void Pose(IReadOnlyList<Vector3> joints,Transform[] bones,float parentScale=1)
            {
                var hand=HandFrame(joints);
                if(jointOf!=null)
                {
                    var right=hand*Vector3.right;float size=(joints[MiddleProximal]-joints[Wrist]).magnitude/restSpan;
                    for(int k=0;k<jointOf.Length;k++)
                    {
                        int j=jointOf[k];
                        bones[k].SetPositionAndRotation(joints[j],Frame(Along(joints,j),right)*offset[k]);
                        bones[k].localScale=restScale[k]*(size/parentScale);
                    }
                    return;
                }
                for(int i=0;i<Count;i++)
                {
                    var along=Along(joints,i);
                    float stretch=Child[i]>=0&&Length[i]>1e-6f?along.magnitude/Length[i]:1;
                    bones[i].SetPositionAndRotation(joints[i],Frame(along,hand*side[i]));
                    bones[i].localScale=new Vector3(1,1,stretch)/parentScale;
                }
            }
        }

        // One skinned hand: its renderer, flat bones and (for a look with motes) its particles.
        public sealed class SkinnedHand
        {
            public readonly GameObject Root;
            public readonly SkinnedMeshRenderer Skin;
            // The bones in the mesh's own bone order (ours: one a joint, in joint order).
            public readonly Transform[] Bones;
            public readonly Rig Rig;
            public readonly Vector3[] Joints=new Vector3[Count];
            readonly MemoryHandLook look;
            readonly MaterialPropertyBlock block=new MaterialPropertyBlock();
            readonly ParticleSystem motes;
            readonly bool left;
            float due;
            // boneNames: a model's bones in its mesh's order (MemoryHandLook.BonesFor); null for our own mesh. left: which hand,
            // for the hand's frame the shaders draw their patterns in.
            public SkinnedHand(Transform parent,string name,Mesh mesh,MemoryHandLook look,IReadOnlyList<string> boneNames=null,bool left=false)
            {
                this.look=look;this.left=left;Rig=new Rig(mesh,boneNames);
                Root=new GameObject(name);Root.transform.SetParent(parent,false);
                var bones=new GameObject("Bones").transform;bones.SetParent(Root.transform,false);
                Bones=new Transform[Rig.BoneCount];int root=Wrist;
                for(int i=0;i<Bones.Length;i++)
                {
                    Bones[i]=new GameObject(boneNames!=null?boneNames[i]:Names[i]).transform;Bones[i].SetParent(bones,false);
                    if(boneNames!=null&&Rig.JointOf(boneNames[i])==Wrist)root=i;
                }
                var go=new GameObject("Skin");go.transform.SetParent(Root.transform,false);
                Skin=go.AddComponent<SkinnedMeshRenderer>();
                Skin.sharedMesh=mesh;Skin.bones=Bones;Skin.rootBone=Bones[root];Skin.sharedMaterials=look.Materials;
                Skin.shadowCastingMode=ShadowCastingMode.Off;Skin.receiveShadows=false;
                Skin.lightProbeUsage=LightProbeUsage.Off;Skin.reflectionProbeUsage=ReflectionProbeUsage.Off;
                Skin.updateWhenOffscreen=false;Skin.localBounds=new Bounds(new Vector3(0,0,.08f),Vector3.one*.42f);
                Skin.skinnedMotionVectors=false;
                Rig.Pose(Rig.Rest,Bones);
                if(look.Motes&&look.MoteMaterial)motes=Motes(Root.transform,look);
            }
            public bool Shown=>Skin.enabled;
            public void Show(bool on)
            {
                if(Skin.enabled==on)return;
                Skin.enabled=on;
                if(motes&&!on)motes.Clear();
            }
            // Joints (world) in, bones placed, the tips' colour set from how closed the pinch is.
            public void Pose(IReadOnlyList<Vector3> joints)
            {
                if(!ReferenceEquals(joints,Joints))for(int i=0;i<Count;i++)Joints[i]=joints[i];
                float scale=Root.transform.lossyScale.x;
                Rig.Pose(joints,Bones,scale>1e-6f?scale:1);
                block.SetFloat(CloseId,Mathf.InverseLerp(PinchOpen,PinchClosed,Vector3.Distance(joints[IndexTip],joints[ThumbTip])));
                SetHandSpace(block,joints,left,look.WristFade);
                Skin.SetPropertyBlock(block);
            }
            public void Pose()=>Pose(Joints);
            // The lantern's specks: drift up off the fingers, more off the tips.
            static readonly int[] MoteFrom={IndexTip,ThumbTip,15,20,25,9,14,4,19,8,13};
            public void Emit(float dt)
            {
                if(!motes||!Skin.enabled)return;
                due+=look.MoteRate*dt;
                var p=new ParticleSystem.EmitParams();
                while(due>=1)
                {
                    due-=1;
                    var at=Joints[MoteFrom[Random.Range(0,MoteFrom.Length)]]+Random.insideUnitSphere*.006f;
                    p.position=at;
                    p.velocity=Vector3.up*Random.Range(look.MoteRise.x,look.MoteRise.y)+Random.insideUnitSphere*.006f;
                    p.startSize=Random.Range(look.MoteSize.x,look.MoteSize.y);
                    p.startLifetime=look.MoteLife*Random.Range(.6f,1.1f);
                    p.startColor=look.MoteColor;
                    motes.Emit(p,1);
                }
            }
            static ParticleSystem Motes(Transform parent,MemoryHandLook look)
            {
                var go=new GameObject("Motes");go.transform.SetParent(parent,false);
                var ps=go.AddComponent<ParticleSystem>();
                ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main=ps.main;main.loop=true;main.playOnAwake=false;main.simulationSpace=ParticleSystemSimulationSpace.World;
                main.startSpeed=0;main.gravityModifier=0;main.maxParticles=256;main.startLifetime=look.MoteLife;
                var emission=ps.emission;emission.enabled=false;
                var shape=ps.shape;shape.enabled=false;
                var fade=ps.colorOverLifetime;fade.enabled=true;
                var g=new Gradient();
                g.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
                    new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.2f),new GradientAlphaKey(.6f,.6f),new GradientAlphaKey(0,1)});
                fade.color=g;
                var drag=ps.limitVelocityOverLifetime;drag.enabled=true;drag.drag=.6f;
                var r=go.GetComponent<ParticleSystemRenderer>();
                r.sharedMaterial=look.MoteMaterial;r.renderMode=ParticleSystemRenderMode.Billboard;
                r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;r.lightProbeUsage=LightProbeUsage.Off;
                ps.Play();
                return ps;
            }
        }

        // What the hand shaders read per hand (Shaders/Hands/HandSpace.hlsl) in place of extra mesh channels: the hand's
        // frame (wrist at 0, x toward the thumb, y toward the fingers, z out of the back), the index and thumb pads, the
        // wrist and the way the hand points.
        public const float TipNear=.006f;
        static readonly int HandToLocalId=Shader.PropertyToID("_HandToLocal"),IndexPadId=Shader.PropertyToID("_IndexPad"),
            ThumbPadId=Shader.PropertyToID("_ThumbPad"),WristId=Shader.PropertyToID("_WristWS"),ForwardId=Shader.PropertyToID("_HandFwdWS"),FadeId=Shader.PropertyToID("_WristFade");
        public static Matrix4x4 HandToLocal(IReadOnlyList<Vector3> j,bool left)
        {
            var w=j[Wrist];var forward=(j[MiddleProximal]-w).normalized;
            var thumbward=Vector3.ProjectOnPlane(j[IndexProximal]-j[LittleProximal],forward).normalized;
            var back=Vector3.Cross(thumbward,forward)*(left?-1:1);
            var m=Matrix4x4.identity;
            m.SetRow(0,new Vector4(thumbward.x,thumbward.y,thumbward.z,-Vector3.Dot(thumbward,w)));
            m.SetRow(1,new Vector4(forward.x,forward.y,forward.z,-Vector3.Dot(forward,w)));
            m.SetRow(2,new Vector4(back.x,back.y,back.z,-Vector3.Dot(back,w)));
            return m;
        }
        public static Vector3 Pad(IReadOnlyList<Vector3> j,int tip)=>j[tip]-(j[tip]-j[tip-1]).normalized*TipNear;
        public static void SetHandSpace(MaterialPropertyBlock block,IReadOnlyList<Vector3> j,bool left,Vector2 wristFade)
        {
            block.SetVector(FadeId,new Vector4(wristFade.x,wristFade.y,0,0));
            block.SetMatrix(HandToLocalId,HandToLocal(j,left));
            block.SetVector(IndexPadId,Pad(j,IndexTip));block.SetVector(ThumbPadId,Pad(j,ThumbTip));
            block.SetVector(WristId,j[Wrist]);block.SetVector(ForwardId,(j[MiddleProximal]-j[Wrist]).normalized);
        }

        // ---- The game's hands
        MetaShadowPlayer player;
        readonly SkinnedHand[] hands=new SkinnedHand[2];
        public MemoryHandLook Look{get;private set;}
        public bool Visible(bool left)=>hands[left?0:1].Shown;
        public SkinnedHand Hand(bool left)=>hands[left?0:1];

        public static MemoryHandSkin Add(GameObject host,MetaShadowPlayer player,MemoryHandLook look)
        {
            var it=host.AddComponent<MemoryHandSkin>();it.player=player;it.Look=look;
            var root=new GameObject("Memory hands ("+look.name+")").transform;root.SetParent(host.transform,false);
            it.hands[0]=new SkinnedHand(root,"Left hand",look.MeshFor(true),look,look.BonesFor(true),true);
            it.hands[1]=new SkinnedHand(root,"Right hand",look.MeshFor(false),look,look.BonesFor(false),false);
            foreach(var h in it.hands)h.Show(false);
            return it;
        }
        // The ghost hand (set by the host): the drawn hand stops on solid things and a faint copy shows the tracked hand
        // (MemoryGhostHand). GhostMaterial: see-through (URP Unlit transparent), tinted GhostColor at the ghost's alpha.
        public MemoryGhostHand Ghost;
        public Material GhostMaterial;
        public Color GhostColor=new Color(.93f,.91f,1f,.40f);
        // Shaders/Hands/HandGhost.shader's material (Resources, so the build carries it).
        public const string GhostMaterialPath="ShapeOfMemory/Hand ghost";
        readonly SkinnedHand[] ghosts=new SkinnedHand[2];
        readonly Material[] ghostMaterials=new Material[2];
        readonly Vector3[] raw=new Vector3[Count];
        public SkinnedHand GhostOf(bool left)=>ghosts[left?0:1];

        // Whether a hand is in something deep enough that only its ghost shows (2026-11-15, the user: "the ink hand should
        // disappear if the player's hand is inside something and the ghost hand is activated"). Hysteresis: hidden past
        // half the ghost's fade-in, back below a quarter, so it doesn't flicker at the edge.
        public const float HideFrom=.5f,ShowBelow=.25f;
        readonly bool[] ghosted=new bool[2];
        public bool Ghosted(bool left)=>ghosted[left?0:1];

        // After the hand data has updated for this frame.
        void LateUpdate()
        {
            for(int h=0;h<2;h++)
            {
                var view=hands[h];var hand=player?player.TrackedHand(h==0):null;
                bool shown=hand&&Read(hand,view.Joints);
                if(!shown){view.Show(false);ghosted[h]=false;Ghost?.Lost(h);ShowGhost(h,null,0);continue;}
                if(Ghost!=null)Settle(h,view.Joints);
                view.Show(!ghosted[h]);if(ghosted[h])continue;
                view.Pose();view.Emit(Time.deltaTime);
            }
        }
        // Moves the drawn joints out of solid things and shows the ghost at the tracked ones (tests call it). Returns how
        // much the ghost shows (0..1); past HideFrom the drawn hand goes and only the ghost is left.
        public float Settle(int h,Vector3[] joints)
        {
            for(int i=0;i<Count;i++)raw[i]=joints[i];
            var offset=Ghost.Solve(h,raw);
            if(offset!=Vector3.zero)for(int i=0;i<Count;i++)joints[i]=raw[i]+offset;
            float shown=MemoryGhostHand.Shown(offset);
            if(!ghosted[h]&&shown>HideFrom)ghosted[h]=true;
            else if(ghosted[h]&&shown<ShowBelow)ghosted[h]=false;
            // Alone, the ghost shows in full; beside the drawn hand it fades in with the depth.
            ShowGhost(h,raw,ghosted[h]?1:shown);
            return shown;
        }
        void ShowGhost(int h,Vector3[] at,float alpha)
        {
            var ghost=ghosts[h];
            if(alpha<=0||at==null){if(ghost!=null)ghost.Show(false);return;}
            if(ghost==null)
            {
                if(!GhostMaterial)return;
                ghost=ghosts[h]=new SkinnedHand(hands[h].Root.transform.parent,(h==0?"Left":"Right")+" hand ghost",Look.MeshFor(h==0),Look,Look.BonesFor(h==0),h==0);
                ghostMaterials[h]=new Material(GhostMaterial){name="Ghost hand",renderQueue=3060};
                ghost.Skin.sharedMaterials=new[]{ghostMaterials[h]};
            }
            var c=GhostColor;c.a*=alpha;ghostMaterials[h].SetColor("_BaseColor",c);
            ghost.Pose(at);ghost.Show(true);
        }
        void OnDestroy(){foreach(var m in ghostMaterials)if(m)Destroy(m);}
        // The tracked joints (world) of one hand, in Joints order; false while that hand isn't tracked.
        public static bool ReadJoints(MetaShadowPlayer player,bool left,Vector3[] joints)
        {
            var hand=player?player.TrackedHand(left):null;
            return hand&&Read(hand,joints);
        }
        static bool Read(Hand hand,Vector3[] joints)
        {
            for(int i=0;i<Count;i++){if(!hand.GetJointPose(Joints[i],out var pose))return false;joints[i]=pose.position;}
            return true;
        }
    }
}
