using System;
using System.Collections.Generic;
using UnityEngine;

namespace CloudGarden.ShadowPuzzle
{
    public sealed class MoonWorkshop : MonoBehaviour
    {
        public Camera viewCamera;
        public ShadowPlayerContext xr;
        public Material surfaceMaterial, screenMaterial, glowMaterial;
        public MoonWorld world;
        public MoonInput input;
        public MoonProjection Projection { get; private set; }
        public readonly ShadowPose[] poses=new ShadowPose[MoonProjection.Count];
        public readonly Transform[] pieces=new Transform[MoonProjection.Count];
        public readonly int[] owners=new int[MoonProjection.Count];
        readonly ShadowPose[] starts=new ShadowPose[MoonProjection.Count];
        readonly Stack<Edit> history=new Stack<Edit>();
        struct Edit {public int part;public ShadowPose pose;}
        public int Stage {get;private set;}
        public int Focus {get;private set;}
        public bool Completed {get;private set;}
        public bool Snap=true, Spinning=true;
        public float StableTime {get;private set;}
        public string Message {get;private set;}
        public int UndoCount=>history.Count;
        public bool Busy {get{foreach(int owner in owners)if(owner>=0)return true;return false;}}
        float nextSample,hintUntil,suppressCompletionUntil;
        public bool ValidationMode;
        void Awake()
        {
            Projection=new MoonProjection();
            for(int i=0;i<poses.Length;i++){poses[i]=MoonProjection.Tray[i];owners[i]=-1;}
            world=gameObject.AddComponent<MoonWorld>();world.game=this;world.Build();
            input=gameObject.AddComponent<MoonInput>();input.game=this;
            Message="Nine blocks. One hidden explorer. Start with the front outline. Pinch to pick up; open your fingers to release.";
        }
        void Update()
        {
            if(Time.unscaledTime>=nextSample)
            {
                float dt=.1f;nextSample=Time.unscaledTime+dt;Projection.Render(poses);Evaluate(dt);world.Refresh();
            }
        }
        public void Evaluate(float dt)
        {
            if(Completed)return;
            bool ready=Time.unscaledTime>=suppressCompletionUntil&&(Stage<2?Projection.scores[Stage].Ready:Projection.AllReady(poses));
            StableTime=ready&&!Busy?StableTime+dt:0;
            if(StableTime>=1)
            {
                StableTime=0;
                if(Stage<2){Stage++;Focus=Stage;world.Chime();}
                else{Completed=true;Message="MOON COURIER\nYour nine blocks became a little explorer. Walk around it, pause the turntable, or EDIT to keep building.";world.Reveal();Debug.Log("MOON_COMPLETE: all three views and connected nine-block assembly");}
            }
            if(!Completed&&Time.unscaledTime>hintUntil)
            {
                Message=Stage==0?"01 / FRONT\nFind the long ears, wide head and two boots. Keep the blocks inside the lit base.":Stage==1?"02 / LEFT 45\nNow work in depth. The boots come forward; the pack sits behind the body. All blocks stay movable.":"03 / RIGHT 45\nCheck both arms and the backpack. All THREE views must be green together. Use and connect all nine blocks.";
                if(Stage==2&&Projection.scores[0].Ready&&Projection.scores[1].Ready&&Projection.scores[2].Ready&&!MoonProjection.Connected(poses))Message="The outlines match. Bring all nine blocks onto the base and close the gaps between them.";
            }
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
            if(part<0||owners[part]!=owner)return;owners[part]=-1;
            var p=poses[part];
            if(!trackingLost)
            {
                if(!MoonProjection.InWorkspace(p))p=MoonProjection.Tray[part];
                else if(Snap)
                {
                    // Universal 5 mm / 15 degree workbench grid, never attraction to an answer pose.
                    var snapped=PhysicalAssembly.SnapGeometry.Grid(new Pose(p.position,p.rotation),.005f,15);
                    p.position=snapped.position;p.rotation=snapped.rotation;
                }
            }
            poses[part]=p;ApplyPose(part);
            if(Vector3.Distance(starts[part].position,p.position)>.0001f||Quaternion.Angle(starts[part].rotation,p.rotation)>.1f)history.Push(new Edit{part=part,pose=starts[part]});
        }
        public void ApplyPose(int part){pieces[part].localPosition=poses[part].position;pieces[part].localRotation=poses[part].rotation;}
        public void Undo(){if(Busy||Completed||history.Count==0)return;var edit=history.Pop();poses[edit.part]=edit.pose;ApplyPose(edit.part);StableTime=0;}
        public void SetFocus(int view){Focus=view;world.Refresh();}
        public void Hint()
        {
            hintUntil=Time.unscaledTime+14;
            Message=Focus==0?"FRONT HINT\nWide block = head. Two thin bars = ears. The two long boots sit underneath the body.":Focus==1?"LEFT HINT\nWatch the same piece on all panels. Slide it toward / away from you to change the diagonal view while keeping its front outline.":"RIGHT HINT\nThe small pack touches the back of the body. The two arms sit at different depths. Amber = extra shadow; pale stripes = missing shadow.";
        }
        public void ResetChallenge()
        {
            if(Busy)return;Completed=false;Stage=Focus=0;StableTime=0;suppressCompletionUntil=0;history.Clear();world.ResetReveal();
            for(int i=0;i<poses.Length;i++){owners[i]=-1;poses[i]=MoonProjection.Tray[i];ApplyPose(i);}hintUntil=0;
        }
        public void EditAssembly(){if(Busy)return;Completed=false;StableTime=0;suppressCompletionUntil=Time.unscaledTime+5;world.ResetReveal();hintUntil=Time.unscaledTime+3;Message="Keep shaping your explorer. All three outlines remain live.";}
        public void Height(float amount){if(!Busy)transform.position+=Vector3.up*amount;}
    }
}
