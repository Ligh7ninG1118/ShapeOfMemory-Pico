using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace CloudGarden.ShadowPuzzle
{
    public sealed class ShadowWorkshop : MonoBehaviour
    {
        public Camera viewCamera;
        public ShadowPlayerContext xr;
        public Material surfaceMaterial, screenMaterial, glowMaterial;
        public ShadowProjection Projection { get; private set; }
        public ShadowScore Score { get; private set; }
        public readonly ShadowPose[] poses = new ShadowPose[3];
        public readonly bool[] available = {true,false,false};
        public readonly int[] owners = {-1,-1,-1};
        public readonly Transform[] pieces = new Transform[3];
        public bool HullConfirmed { get; private set; }
        public bool HullLocked { get; private set; }
        public bool Completed { get; private set; }
        public bool Busy => owners[0]>=0||owners[1]>=0||owners[2]>=0;
        public bool CanKeep => !HullConfirmed&&!Busy&&Score.HullReady;
        public int UndoCount => history.Count;
        public string Message { get; private set; }="Pick up the lit block. Turn it, then move it toward the lamp to enlarge its shadow.";
        public float StableTime { get; private set; }
        public bool ValidationMode;
        public ShadowInput input;
        public ShadowWorld world;
        public string SavePath => Path.Combine(Application.persistentDataPath,"shadow-workshop-v1.json");
        ShadowPose checkpoint;
        readonly ShadowPose[] grabStart = new ShadowPose[3];
        readonly Stack<Edit> history=new Stack<Edit>();
        struct Edit { public int part;public ShadowPose pose; public bool locked; }
        [Serializable] class Save { public int version=1;public ShadowPose hull;public ShadowPose[] poses; }
        float nextScore,lastAttempt,lastImprovement,best,helpUntil;
        readonly bool[] lit=new bool[3];
        static readonly float[] thresholds={.82f,.80f,.70f};
        public bool RegionLit(int i)=>lit[i];

        void Awake()
        {
            Projection=new ShadowProjection();
            for(int i=0;i<3;i++)poses[i]=ShadowProjection.Tray[i];
            world=gameObject.AddComponent<ShadowWorld>();world.game=this;world.Build();
            input=gameObject.AddComponent<ShadowInput>();input.game=this;
            lastAttempt=lastImprovement=Time.unscaledTime;
        }
        void Update()
        {
            Projection.Render(poses,available);
            if(Time.unscaledTime>=nextScore){nextScore=Time.unscaledTime+.1f;Evaluate(.1f);}
            world.Refresh();
            if(!Completed&&Time.unscaledTime>helpUntil)
            {
                if(Time.unscaledTime-lastAttempt>20)Message="Reach for a glowing piece and pinch. Release to leave it floating.";
                else if(Time.unscaledTime-lastImprovement>30)Message=Score.hull<.82f?"The lower hull is missing. Try turning the wide block toward the screen.":Score.sail<.8f?"Fill the triangular sail. Moving toward the lamp makes its shadow larger.":"Check the thin mast on the left. The L bracket can overlap the sail.";
            }
        }
        public void Evaluate(float dt)
        {
            Score=Projection.Score();
            float progress=HullConfirmed?Score.iou:Score.hull-Mathf.Min(1,Score.hullSpill);
            if(progress>best+.025f){best=progress;lastImprovement=Time.unscaledTime;}
            for(int i=0;i<3;i++)
            {
                float value=i==0?Score.hull:i==1?Score.sail:Score.mast;
                bool valid=available[i]&&value>=(lit[i]?thresholds[i]-.04f:thresholds[i])&&(HullConfirmed?Score.spill<=.18f:Score.hullSpill<=.20f);
                if(valid&&!lit[i])world.Chime(440+i*160);lit[i]=valid;
            }
            if(Completed)return;
            StableTime=HullConfirmed&&!Busy&&Score.Complete?StableTime+dt:0;
            if(StableTime>=1f){Completed=true;Message="A boat made of shadows. Keep exploring, or restart the challenge.";world.Chime(880);world.Reveal();}
        }
        public bool CanGrab(int i)=>i>=0&&i<3&&available[i]&&!(i==0&&HullLocked);
        public bool BeginGrab(int part,int owner)
        {
            if(!CanGrab(part)||owner<0)return false;
            // One continuous grab owns one block. Explicit handoff of that block is still allowed.
            for(int i=0;i<owners.Length;i++)if(i!=part&&owners[i]==owner)return false;
            // A handoff continues the original transaction; it never scales the object.
            if(owners[part]<0)grabStart[part]=poses[part];
            owners[part]=owner;StableTime=0;lastAttempt=Time.unscaledTime;return true;
        }
        public void MoveGrab(int part,int owner,ShadowPose pose)
        {
            if(owners[part]!=owner)return;
            poses[part]=pose;ApplyPose(part);lastAttempt=Time.unscaledTime;
        }
        public void EndGrab(int part,int owner,bool trackingLost=false)
        {
            if(owners[part]!=owner)return;
            owners[part]=-1;
            if(!trackingLost&&!ShadowProjection.InWorkspace(poses[part]))poses[part]=ShadowProjection.Tray[part];
            if(Vector3.Distance(grabStart[part].position,poses[part].position)>.0001f||Quaternion.Angle(grabStart[part].rotation,poses[part].rotation)>.1f)
                history.Push(new Edit{part=part,pose=grabStart[part],locked=false});
            ApplyPose(part);lastAttempt=Time.unscaledTime;
        }
        public void ApplyPose(int part){if(pieces[part]){pieces[part].localPosition=poses[part].position;pieces[part].localRotation=poses[part].rotation;}}
        public void KeepHull()
        {
            Projection.Render(poses,available);Score=Projection.Score();if(!CanKeep)return;
            checkpoint=poses[0];HullConfirmed=HullLocked=true;available[1]=available[2]=true;history.Clear();best=0;lastImprovement=Time.unscaledTime;
            Message="Hull saved and locked. Now turn the wedge and L bracket to fill the sail and mast.";helpUntil=Time.unscaledTime+12;
            if(!ValidationMode)try{File.WriteAllText(SavePath,JsonUtility.ToJson(new Save{hull=checkpoint,poses=(ShadowPose[])poses.Clone()},true));}catch(Exception e){Debug.LogWarning("Shadow checkpoint could not be saved: "+e.Message);}
            world.Chime(660);
        }
        public void ToggleHullLock()
        {
            if(!HullConfirmed||Busy)return;
            history.Push(new Edit{part=0,pose=poses[0],locked=HullLocked});HullLocked=!HullLocked;
            Message=HullLocked?"Hull locked at its current pose.":"Hull unlocked. All three shadows still need to match.";helpUntil=Time.unscaledTime+8;
        }
        public void RestoreHull()
        {
            if(!HullConfirmed||Busy)return;
            history.Push(new Edit{part=0,pose=poses[0],locked=HullLocked});poses[0]=checkpoint;HullLocked=true;ApplyPose(0);StableTime=0;
            Message="Hull checkpoint restored. Sail and mast stayed where you placed them.";helpUntil=Time.unscaledTime+8;
        }
        public void Undo()
        {
            if(Busy||history.Count==0)return;var edit=history.Pop();poses[edit.part]=edit.pose;if(edit.part==0)HullLocked=edit.locked;ApplyPose(edit.part);StableTime=0;
        }
        public void ResetChallenge()
        {
            input.ReleaseAll();HullConfirmed=HullLocked=Completed=false;StableTime=0;history.Clear();best=0;
            for(int i=0;i<3;i++){poses[i]=ShadowProjection.Tray[i];available[i]=i==0;lit[i]=false;ApplyPose(i);}
            lastAttempt=lastImprovement=Time.unscaledTime;Message="Start with the hull block. Turn it and explore depth.";helpUntil=Time.unscaledTime+10;world.ResetReveal();
        }
        public void ResumeCheckpoint()
        {
            if(Busy||!File.Exists(SavePath))return;
            try
            {
                var save=JsonUtility.FromJson<Save>(File.ReadAllText(SavePath));if(save==null||save.version!=1||save.poses==null||save.poses.Length!=3||!Valid(save.hull))return;
                foreach(var pose in save.poses)if(!Valid(pose))return;
                ResetChallenge();checkpoint=save.hull;for(int i=0;i<3;i++){poses[i]=save.poses[i];available[i]=true;ApplyPose(i);}HullConfirmed=HullLocked=true;
                Message="Saved hull restored. Finish the sail and mast.";
            }catch(Exception e){Debug.LogWarning("Cannot restore shadow checkpoint: "+e.Message);}
        }
        static bool Valid(ShadowPose p)=>ShadowProjection.InWorkspace(p)&&!float.IsNaN(p.rotation.x)&&!float.IsNaN(p.rotation.y)&&!float.IsNaN(p.rotation.z)&&!float.IsNaN(p.rotation.w)&&Mathf.Abs(Quaternion.Dot(p.rotation,p.rotation)-1)<.02f;
        public void Help()
        {
            Message=!HullConfirmed?"TURN: rotate the wide face toward the screen. DEPTH: closer to the lamp = a larger shadow.":"Try different depths: the hull is nearer the lamp than the sail. Their shadows can overlap.";
            helpUntil=Time.unscaledTime+14;lastAttempt=Time.unscaledTime;world.ShowDemo();
        }
        public void Height(float delta){if(Busy)return;transform.position+=Vector3.up*delta;}
        void OnDestroy(){Projection?.Dispose();}
    }
}
