using System.Collections.Generic;
using UnityEngine;

namespace CloudGarden.ShadowPuzzle
{
    // Everyday-object silhouette level: one front light, one screen, all pieces free.
    public sealed class SilhouetteWorkshop : MonoBehaviour
    {
        public const float SettleSeconds=.8f;
        public Camera viewCamera;
        public ShadowPlayerContext xr;
        public Material surfaceMaterial,screenMaterial,glowMaterial;
        public SilhouetteLevel level;
        public SilhouetteWorld world;
        public SilhouetteInput input;
        public SilhouetteProjection Projection {get;private set;}
        public ShadowPose[] poses;
        public Transform[] pieces;
        public BoxCollider[] pickSurfaces;
        public Renderer[] renderers;
        public int[] owners;
        ShadowPose[] starts;
        readonly Stack<Edit> history=new Stack<Edit>();
        struct Edit {public int part;public ShadowPose pose;}
        public int Count=>poses.Length;
        public bool Completed {get;private set;}
        public bool Snap=true;
        public float StableTime {get;private set;}
        public string Message {get;private set;}
        public int UndoCount=>history.Count;
        public bool Busy {get{foreach(int owner in owners)if(owner>=0)return true;return false;}}
        public bool HintActive=>Time.unscaledTime<hintUntil;
        public bool ValidationMode;
        float nextSample,hintUntil,suppressCompletionUntil;
        void Awake()
        {
            Projection=new SilhouetteProjection(level);int n=Projection.Count;
            poses=new ShadowPose[n];pieces=new Transform[n];pickSurfaces=new BoxCollider[n];renderers=new Renderer[n];owners=new int[n];starts=new ShadowPose[n];
            for(int i=0;i<n;i++){poses[i]=level.pieces[i].Tray;owners[i]=-1;}
            world=gameObject.AddComponent<SilhouetteWorld>();world.game=this;world.Build();
            input=gameObject.AddComponent<SilhouetteInput>();input.game=this;UpdateMessage();
        }
        void Update()
        {
            if(Time.unscaledTime<nextSample)return;
            nextSample=Time.unscaledTime+.1f;Projection.Render(poses);Evaluate(.1f);world.Refresh();
        }
        public void Evaluate(float dt)
        {
            if(Completed)return;
            bool ready=Projection.Score.Ready&&!Busy&&Time.unscaledTime>=suppressCompletionUntil;
            StableTime=ready?StableTime+dt:0;
            if(StableTime>=SettleSeconds)Complete();else UpdateMessage();
        }
        void Complete()
        {
            Completed=true;StableTime=0;hintUntil=0;Message=level.complete;world.Reveal();
            Debug.Log("SILHOUETTE_COMPLETE: "+level.name+" cover="+Projection.Score.cover.ToString("0.00")+" spill="+Projection.Score.spill.ToString("0.00"));
        }
        void UpdateMessage()
        {
            if(Completed)return;
            if(HintActive){Message=level.hint;return;}
            var s=Projection.Score;int placed=0;for(int i=0;i<Count;i++)if(SilhouetteProjection.InWorkspace(poses[i]))placed++;
            if(placed==0)Message=level.intro+"\nPinch an object, carry it over the base and watch the screen.";
            else if(s.Ready)Message="That's it. Let go and hold still...";
            else if(s.Match(level)>=1&&!s.placed)Message="Almost. Every object belongs in the picture - bring the last ones over the base.";
            else if(s.Match(level)>=1&&s.pieceSpill>level.pieceSpillMax&&s.spill<=level.spillMax)Message="So close. One object is turned the wrong way - find the amber edge and rotate it.";
            else if(s.Match(level)>=1)Message="The outline is filled. Trim the amber shadow that falls outside it.";
            else if(s.spill>level.spillMax||s.pieceSpill>level.pieceSpillMax)Message="Amber means shadow outside the outline. Pale stripes still need shadow.";
            else Message=level.intro+"\n"+placed+" / "+Count+" objects in the light.";
        }
        public bool CanGrab(int part)=>part>=0&&part<Count&&!Completed;
        public bool BeginGrab(int part,int owner)
        {
            if(!CanGrab(part)||owner<0)return false;
            // One continuous grab owns one object. Explicit handoff of that object is still allowed.
            for(int i=0;i<owners.Length;i++)if(i!=part&&owners[i]==owner)return false;
            if(owners[part]<0)starts[part]=poses[part];owners[part]=owner;StableTime=0;return true;
        }
        public void MoveGrab(int part,int owner,ShadowPose pose){if(owners[part]==owner){poses[part]=pose;ApplyPose(part);}}
        public void EndGrab(int part,int owner,bool trackingLost=false)
        {
            if(part<0||owners[part]!=owner)return;owners[part]=-1;var pose=poses[part];
            if(!trackingLost)
            {
                if(!SilhouetteProjection.InWorkspace(pose))pose=level.pieces[part].Tray;
                else if(Snap)
                {
                    // Same 5 mm / 15 degree workbench grid as the other shadow levels; never attracts to the answer.
                    var snapped=PhysicalAssembly.SnapGeometry.Grid(new Pose(pose.position,pose.rotation),.005f,15);
                    pose.position=snapped.position;pose.rotation=snapped.rotation;
                }
            }
            poses[part]=pose;ApplyPose(part);
            if(Vector3.Distance(starts[part].position,pose.position)>.0001f||Quaternion.Angle(starts[part].rotation,pose.rotation)>.1f)history.Push(new Edit{part=part,pose=starts[part]});
        }
        public void ApplyPose(int part){pieces[part].localPosition=poses[part].position;pieces[part].localRotation=poses[part].rotation;}
        public Vector3 ClosestPoint(int part,Vector3 point)=>pickSurfaces[part].ClosestPoint(point);
        public void Undo(){if(Busy||Completed||history.Count==0)return;var edit=history.Pop();poses[edit.part]=edit.pose;ApplyPose(edit.part);StableTime=0;}
        public void Hint(){if(Completed)return;hintUntil=Time.unscaledTime+12;UpdateMessage();}
        public void ResetChallenge()
        {
            if(Busy)return;Completed=false;StableTime=0;hintUntil=0;suppressCompletionUntil=0;history.Clear();
            for(int i=0;i<Count;i++){owners[i]=-1;poses[i]=level.pieces[i].Tray;ApplyPose(i);}world.ResetReveal();UpdateMessage();
        }
        public void EditAssembly(){if(Busy||!Completed)return;Completed=false;StableTime=0;suppressCompletionUntil=Time.unscaledTime+4;world.ResetReveal();UpdateMessage();}
        public void Height(float amount){if(!Busy)transform.position+=Vector3.up*amount;}
    }
}
