using System.Collections.Generic;
using UnityEngine;

namespace CloudGarden.ShadowPuzzle
{
    public sealed class WishesWorkshop : MonoBehaviour
    {
        public Camera viewCamera;
        public ShadowPlayerContext xr;
        public Material surfaceMaterial,screenMaterial,glowMaterial;
        public WishesWorld world;
        public WishesInput input;
        public WishesProjection Projection {get;private set;}
        public readonly ShadowPose[] poses=new ShadowPose[WishesProjection.Count];
        public readonly Transform[] pieces=new Transform[WishesProjection.Count];
        public readonly Collider[][] pickSurfaces=new Collider[WishesProjection.Count][];
        public readonly Renderer[] renderers=new Renderer[WishesProjection.Count];
        public readonly int[] owners=new int[WishesProjection.Count];
        readonly ShadowPose[] starts=new ShadowPose[WishesProjection.Count],checkpoint=new ShadowPose[WishesProjection.Count];
        readonly Stack<Edit> history=new Stack<Edit>();
        struct Edit {public int part;public ShadowPose pose;}
        public int Stage {get;private set;}
        public int Focus {get;private set;}
        public bool Completed {get;private set;}
        public bool Snap=true,Spinning=true;
        public float StableTime {get;private set;}
        public string Message {get;private set;}
        public int UndoCount=>history.Count;
        public bool Busy {get{foreach(int owner in owners)if(owner>=0)return true;return false;}}
        public bool CanReveal=>!Completed&&!Busy&&StableTime>=1&&ConditionsMet;
        public bool ConditionsMet=>Projection.ReadyThrough(Stage)&&(Stage<2||Projection.Connected(poses));
        float nextSample,hintUntil;
        public bool ValidationMode;
        public bool ViewUnlocked(int v)=>v>=0&&v<=Stage;
        void Awake()
        {
            Projection=new WishesProjection();
            for(int i=0;i<poses.Length;i++){poses[i]=WishesProjection.Tray[i];owners[i]=-1;}
            world=gameObject.AddComponent<WishesWorld>();world.game=this;world.Build();
            input=gameObject.AddComponent<WishesInput>();input.game=this;UpdateMessage();
        }
        void Update()
        {
            if(Time.unscaledTime<nextSample)return;
            nextSample=Time.unscaledTime+.1f;Projection.Render(poses);Evaluate(.1f);world.Refresh();
        }
        public void Evaluate(float dt)
        {
            if(Completed)return;StableTime=ConditionsMet&&!Busy?Mathf.Min(1.2f,StableTime+dt):0;UpdateMessage();
        }
        void UpdateMessage()
        {
            if(Completed||Time.unscaledTime<hintUntil)return;
            if(CanReveal){Message=Stage==2?"All three wishes agree. Release your hands, then press REVEAL SCULPTURE.":"All open views agree. Press REVEAL to discover the next wish.";return;}
            Message=Stage==0?"HOME / A light to return to.\nBuild the lighthouse and leave its lantern window open. Only this view matters for now.":Stage==1?"GROWTH / A place to take root.\nKeep the lighthouse AND form the pine. Both open views must agree before the third wish appears.":"JOURNEY / Courage to leave.\nKeep the lighthouse and pine; form the rocket with an open porthole. All three views and all 12 blocks must agree.";
        }
        public bool RevealNext()
        {
            // Refresh at the action boundary: no stale green screen can unlock a damaged assembly.
            Projection.Render(poses);
            if(!CanReveal)return false;
            StableTime=0;hintUntil=0;
            if(Stage<2)
            {
                for(int i=0;i<poses.Length;i++)checkpoint[i]=poses[i];
                Stage++;Focus=Stage;world.Unlock(Stage);world.Chime();UpdateMessage();
            }
            else
            {
                Completed=true;Message="THREE WISHES\nA light to return to. A place to grow. The courage to leave.\nOne sculpture, shaped by your hands.";
                world.Reveal();Debug.Log("WISHES_COMPLETE: 0 / left45 / right45 simultaneous, windows clear, 12 pieces connected");
            }
            world.Refresh();return true;
        }
        public bool CanGrab(int part)=>part>=0&&part<poses.Length&&!Completed;
        public bool BeginGrab(int part,int owner)
        {
            if(!CanGrab(part)||owner<0)return false;
            // One continuous grab owns one block. Explicit handoff of that block is still allowed.
            for(int i=0;i<owners.Length;i++)if(i!=part&&owners[i]==owner)return false;
            if(owners[part]<0)starts[part]=poses[part];owners[part]=owner;StableTime=0;return true;
        }
        public void MoveGrab(int part,int owner,ShadowPose pose){if(owners[part]==owner){poses[part]=pose;ApplyPose(part);}}
        public void EndGrab(int part,int owner,bool trackingLost=false)
        {
            if(part<0||owners[part]!=owner)return;owners[part]=-1;var pose=poses[part];
            if(!trackingLost)
            {
                if(!WishesProjection.InWorkspace(pose))pose=WishesProjection.Tray[part];
                else if(Snap)
                {
                    var snapped=PhysicalAssembly.SnapGeometry.Grid(new Pose(pose.position,pose.rotation),.005f,15);
                    pose.position=snapped.position;pose.rotation=snapped.rotation;
                }
            }
            poses[part]=pose;ApplyPose(part);
            if(Vector3.Distance(starts[part].position,pose.position)>.0001f||Quaternion.Angle(starts[part].rotation,pose.rotation)>.1f)history.Push(new Edit{part=part,pose=starts[part]});
        }
        public void ApplyPose(int part){pieces[part].localPosition=poses[part].position;pieces[part].localRotation=poses[part].rotation;}
        public Vector3 ClosestPoint(int part,Vector3 point)
        {
            var bounds=renderers[part].bounds;var closest=bounds.ClosestPoint(point);if((point-closest).sqrMagnitude>.045f*.045f)return closest;
            float distance=float.PositiveInfinity;
            foreach(var collider in pickSurfaces[part]){var p=collider.ClosestPoint(point);float d=(p-point).sqrMagnitude;if(d<distance){distance=d;closest=p;}}
            return closest;
        }
        public void Undo(){if(Busy||Completed||history.Count==0)return;var edit=history.Pop();poses[edit.part]=edit.pose;ApplyPose(edit.part);StableTime=0;}
        public void Restore(){if(Busy||Completed||Stage==0)return;for(int i=0;i<poses.Length;i++){poses[i]=checkpoint[i];ApplyPose(i);}history.Clear();StableTime=0;}
        public void SetFocus(int view){if(!ViewUnlocked(view)||Completed)return;Focus=view;world.Refresh();}
        public void Hint()
        {
            hintUntil=Time.unscaledTime+15;
            Message=Focus==0?"HOME HINT\nWarm blocks form the base, teal the middle, pale blocks the crown. Turn the cut faces inward. Keep light passing through the lantern.":Focus==1?"GROWTH HINT\nSlide pieces in depth while watching BOTH open panels. The branch steps come from angled cuts. A green lighthouse can turn amber again.":"JOURNEY HINT\nThe round porthole must remain open. Compare the middle pieces in all three panels. RESTORE returns to the last reveal checkpoint.";
        }
        public void ResetChallenge()
        {
            if(Busy)return;Completed=false;Stage=Focus=0;StableTime=0;hintUntil=0;history.Clear();
            for(int i=0;i<poses.Length;i++){owners[i]=-1;poses[i]=WishesProjection.Tray[i];ApplyPose(i);}world.ResetReveal();UpdateMessage();
        }
        public void EditAssembly(){if(Busy)return;Completed=false;StableTime=0;world.ResetReveal();UpdateMessage();}
        public void Height(float amount){if(!Busy)transform.position+=Vector3.up*amount;}
        void OnDestroy(){Projection?.Dispose();}
    }
}
