using UnityEngine;

namespace CloudGarden.ShadowPuzzle
{
    // Iteration 2: the lamps are fixed; all freedom is in the pile, which sits in an orb the player rolls freely
    // and carries by its hub. A level is solved when every lamp it uses shows its picture at the same time.
    public sealed class Torch2Workshop : MonoBehaviour
    {
        public const float SettleSeconds=.5f;
        public static readonly Vector3 MinPosition=new Vector3(-.30f,.30f,-.25f),MaxPosition=new Vector3(.30f,.70f,.35f);
        public Camera viewCamera;
        public ShadowPlayerContext xr;
        public Material surfaceMaterial,screenMaterial,glowMaterial;
        public Torch2Level level;
        public Torch2World world;
        public Torch2Input input;
        // Set before the first enable: no debug labels, buttons or prompts; the host game (Shape of Memory) draws its own.
        public bool bare;
        // A bare host's second hint: the target picture cards stand by the lamps.
        public bool CardsShown;
        // Set before the first enable: the host restyles the room as soon as it is built, before the walls are first lit.
        [System.NonSerialized] public System.Action<Torch2Room> roomStyle;
        // Set before the first enable: the host shows the target pictures itself, so no picture cards stand by the lamps.
        [System.NonSerialized] public bool hostShowsPictures;
        // Set before the first enable: pieces are drawn where level.Drawn puts them (gathered toward the hub); the
        // projection keeps their real places, so the shadow is unchanged.
        [System.NonSerialized] public bool drawGathered;
        public Vector3 Drawn(Vector3 position)=>drawGathered?level.Drawn(position):position;
        public Vector3 Drawn(Torch2Piece piece)=>drawGathered?level.Drawn(piece):piece.position;
        public Torch2Projection Projection {get;private set;}
        public Quaternion Rotation {get;private set;}
        public Vector3 Position {get;private set;}
        public int Owner {get;private set;}=-1;
        public bool Busy=>Owner>=0;
        public bool Completed {get;private set;}
        public float StableTime {get;private set;}
        public string Message {get;private set;}
        public bool HintActive=>Time.unscaledTime<hintUntil;
        public bool TwoLamps=>level.lamps.Length>1;
        // The orb turning itself into the answer after a close release (TrySnap), or out of the hand (SnapWhileHeld).
        public bool Snapping=>snapLength>0;
        // Found: the orb easing into the exact solution, so the picture shows the right way up even when it was found
        // upside down or mirrored (a flip the level accepts).
        public bool Righting=>rightLength>0;
        // Set by a host (Shape of Memory): held within level.heldSnapAngle of an answer, or swept through it between two
        // samples, the orb leaves the hand, turns into that answer and the level is solved when it lands.
        [System.NonSerialized] public bool SnapWhileHeld;
        // The snap's glide, in seconds: x for an orb already at the answer, up to y at the widest angle a snap starts from
        // (heldSnapAngle out of the hand, snapAngle on a release). It sets off at its average speed and eases to rest
        // (Glide); the old .25-.5 s ease-out set off at three times that, which read as a jerk at the end of a level.
        [System.NonSerialized] public Vector2 SnapSeconds=new Vector2(.7f,1.3f);
        // How far through its glide a snap is (0..1), its length, and whether it solves the level as it lands (a held snap).
        public float SnapProgress=>snapLength>0?Mathf.Clamp01(snapTime/snapLength):0;
        public float SnapLength=>snapLength;
        public bool SnapSolves=>Snapping&&solveOnLanding;
        // The glide's share of the way at t (0..1): starts at speed 1 (the average), ends at rest, never overshoots.
        public static float Glide(float t){t=Mathf.Clamp01(t);return 1-(1-t)*(1-t)*(1+t);}
        public const float RightSeconds=1.4f;
        // Set by a host before play (Shape of Memory, 2026-11-15: "nerf the snap"): no snaps, so the orb never moves by
        // itself while a hand might want it. Left untouched within reach of an answer (InReach) for LockSeconds, the memory
        // locks: the lamp light brightens over BrightenSeconds (Torch2World.Brighten), then the orb glides slowly into the
        // exact design pose (a flipped find is righted on the way, so no Righting follows), and the level is solved as it
        // lands. A locked orb can't be taken back; a host's pause cancels the lock (CancelLock) and the wait starts again.
        [System.NonSerialized] public bool HoldToSolve;
        [System.NonSerialized] public float LockSeconds=1,BrightenSeconds=.6f;
        // The glide after the light: angle/GlideSpeed seconds (degrees per second), kept within GlideSeconds.
        [System.NonSerialized] public float GlideSpeed=70;
        [System.NonSerialized] public Vector2 GlideSeconds=new Vector2(1.2f,2.6f);
        public bool Locking=>lockLength>0;
        // Seconds from the lock to the landing, and how many of them are left.
        public float LockLength=>lockLength;
        public float LockRemaining=>lockLength>0?Mathf.Max(0,lockLength-lockTime):0;
        // Close enough to lock (HoldToSolve): passing, or as warm as a release snap needed within the angle it reached
        // from (snapWarmth within snapAngle): the same tolerance the snaps gave. Never further than the level's lockAngle
        // from an answer, where it has one.
        public bool InReach
        {
            get
            {
                var s=Projection.Score;float angle=Projection.AngleToAnswer(Rotation);
                if(level.lockAngle>0&&angle>level.lockAngle)return false;
                return s.Ready||s.warmth>=level.snapWarmth&&angle<=level.snapAngle;
            }
        }
        float nextWall,nextScore,hintUntil,snapTime,snapLength,rightTime,rightLength,lockTime,lockLength;
        Quaternion snapFrom,snapTo,rightFrom,lockFrom;
        bool solveOnLanding;
        void Awake()
        {
            Projection=new Torch2Projection(level);SetPose(Torch2Projection.Home,Projection.Start);
            world=gameObject.AddComponent<Torch2World>();world.game=this;world.Build();
            input=gameObject.AddComponent<Torch2Input>();input.game=this;Refresh();
        }
        void Update()
        {
            // The shadow meshes follow the pile every frame (Torch2World); the scoring raster runs at 10 Hz and the
            // wall light only changes while the reveal glows.
            if(Snapping)
            {
                snapTime+=Time.unscaledDeltaTime;float t=Mathf.Clamp01(snapTime/snapLength);
                Rotation=Quaternion.Slerp(snapFrom,snapTo,Glide(t));
                if(t>=1){snapLength=0;if(solveOnLanding){solveOnLanding=false;Solve();}}
            }
            if(Righting)
            {
                rightTime+=Time.unscaledDeltaTime;float t=Mathf.Clamp01(rightTime/rightLength);
                Rotation=Quaternion.Slerp(rightFrom,Projection.Solution,t*t*(3-2*t));if(t>=1)rightLength=0;
            }
            if(Locking)
            {
                // The light first, then the glide: from rest to rest (the orb sat still for LockSeconds).
                lockTime+=Time.unscaledDeltaTime;float b=Mathf.Clamp01(lockTime/BrightenSeconds);world.Brighten=b*b*(3-2*b);
                float t=Mathf.Clamp01((lockTime-BrightenSeconds)/Mathf.Max(.01f,lockLength-BrightenSeconds));
                Rotation=Quaternion.Slerp(lockFrom,Projection.Solution,t*t*(3-2*t));
                if(lockTime>=lockLength){lockLength=0;Rotation=Quaternion.Normalize(Projection.Solution);Solve();Refresh();}
            }
            if(world.Animating&&Time.unscaledTime>=nextWall){nextWall=Time.unscaledTime+1/30f;world.RefreshWalls();}
            if(Time.unscaledTime>=nextScore){nextScore=Time.unscaledTime+.1f;Refresh();}
        }
        void Refresh(){Projection.Render(Rotation,Position);Projection.Evaluate();Evaluate(.1f);world.Refresh();}
        public void Evaluate(float dt)
        {
            if(Completed||Locking)return;
            // Left alone counts only while hands could take it (a host's intro, pause or outro switches the input off).
            StableTime=(HoldToSolve?InReach&&input&&input.enabled:Projection.Score.Ready)&&!Busy&&!Snapping?StableTime+dt:0;
            if(HoldToSolve&&StableTime>=LockSeconds)Lock();
            else if(!HoldToSolve&&StableTime>=SettleSeconds)Solve();
            else UpdateMessage();
        }
        // Left in reach for LockSeconds (HoldToSolve): the light comes up, then the orb glides into the design pose.
        void Lock()
        {
            var s=Projection.Score;float off=Projection.AngleToAnswer(Rotation),design=Quaternion.Angle(Rotation,Projection.Solution);
            StableTime=0;hintUntil=0;lockFrom=Rotation;lockTime=0;
            lockLength=BrightenSeconds+Mathf.Clamp(design/Mathf.Max(1,GlideSpeed),GlideSeconds.x,GlideSeconds.y);
            Debug.Log($"TORCH2_LOCK: {level.name} left alone {LockSeconds:0.0} s in reach ({(s.Ready?"passing":$"warmth {s.warmth:0.00}")}, {off:0.0} degrees from an answer); gliding {design:0} degrees into the design pose, landing in {lockLength:0.0} s");
        }
        // A host's pause (Shape of Memory's MENU): the lock comes undone where the glide got to; left alone in reach again,
        // it locks again.
        public void CancelLock()
        {
            if(!Locking)return;
            lockLength=0;StableTime=0;world.Brighten=0;Debug.Log($"TORCH2_LOCK: {level.name} cancelled {lockTime:0.0} s in");
        }
        // Found where the orb stands; it then eases into the exact solution (Righting).
        void Solve()
        {
            Completed=true;StableTime=0;hintUntil=0;Message=level.complete;
            float off=Projection.AngleToAnswer(Rotation,out int answer),design=Quaternion.Angle(Rotation,Projection.Solution);
            if(design>.01f){rightFrom=Rotation;rightTime=0;rightLength=Mathf.Lerp(.3f,RightSeconds,design/180);}
            world.Reveal();Debug.Log($"TORCH2_COMPLETE: {level.name} {design:0} degrees from the design pose"+(answer>0?$", found flipped (answer {answer}, {off:0} degrees off it)":""));
        }
        void UpdateMessage()
        {
            if(HintActive){Message=level.hint;return;}
            var s=Projection.Score;bool hands=xr&&xr.Active;
            string how=hands?"Pinch the orb's rings and drag to roll it any way. Pinch the hub in the middle to carry it.":"Left-drag rolls the orb. Right-drag moves it, the wheel brings it nearer or further.";
            if(s.Ready)Message="There it is. Let go and hold still...";
            else if(s.warmth>.75f)Message=TwoLamps?"Very warm. Both walls are close - small turns now.":"Very warm. Small turns now.";
            else if(s.warmth>.45f)Message=TwoLamps?"Warmer. Watch both walls: the weaker one decides.":"Warmer. Keep turning, and try tipping it too.";
            else if(!s.visible)Message="Keep the pile inside the light so its shadow reaches the wall.\n"+how;
            else Message=level.riddle+"\n"+how;
        }
        public void SetPose(Vector3 position,Quaternion rotation)
        {
            Position=Vector3.Max(MinPosition,Vector3.Min(MaxPosition,position));Rotation=Quaternion.Normalize(rotation);
        }
        // Grabbing stops a release snap where it is: the hand wins. A snap out of the hand (SnapWhileHeld) is already a
        // solve, so nothing takes it back.
        public bool CanHold(int owner)=>!(Completed||solveOnLanding||Locking||owner<0||(Owner>=0&&Owner!=owner));
        public bool BeginHold(int owner){if(!CanHold(owner))return false;Owner=owner;StableTime=0;snapLength=0;return true;}
        // A hold handed from one holder to another without letting go (Torch2Input: of two hands holding, the first lets go).
        public void PassHold(int from,int to){if(Owner==from&&to>=0)Owner=to;}
        // The owner can lose the orb here (SnapWhileHeld): a holder checks Owner after each Hold.
        public void Hold(int owner,Vector3 position,Quaternion rotation)
        {
            if(Owner!=owner)return;var before=Rotation;SetPose(position,rotation);
            if(SnapWhileHeld&&!HoldToSolve)HeldSnap(before);
        }
        public void EndHold(int owner){if(Owner!=owner)return;Owner=-1;TrySnap();}
        // Let go within snapAngle of an answer, passing or at snapWarmth, and the orb eases into that exact answer (only the
        // rotation; the score ignores where the pile sits). Never while held, and never from a lookalike far away.
        public bool TrySnap()
        {
            if(Completed||Busy||HoldToSolve)return false;
            Projection.Render(Rotation,Position);Projection.Evaluate();
            // The nearest answer pose: with any turn counting (Torch2Projection.AnyTurn), the answer turned to where the
            // pile already stands, so a snap only rights the tip and never spins the picture round.
            var s=Projection.Score;var target=Projection.NearestAnswer(Rotation,out float angle);
            if(angle<.5f||angle>level.snapAngle||!(s.Ready||s.warmth>=level.snapWarmth))return false;
            SnapTo(target,false);return true;
        }
        void SnapTo(Quaternion answer,bool solve)
        {
            float angle=Quaternion.Angle(Rotation,answer);snapFrom=Rotation;snapTo=answer;snapTime=0;
            snapLength=Mathf.Lerp(SnapSeconds.x,SnapSeconds.y,angle/Mathf.Max(1,solve?level.heldSnapAngle:level.snapAngle));solveOnLanding=solve;
        }
        // Still held, within heldSnapAngle of an answer now or anywhere on the turn since the last sample (a quick turn can
        // pass straight through the answer between samples, and a release snap only sees where the hand let go): the orb
        // leaves the hand and turns into the answer, solved when it lands.
        void HeldSnap(Quaternion before)
        {
            float closest=Closest(before,Rotation,out var answer);
            if(closest>level.heldSnapAngle)return;
            Owner=-1;StableTime=0;Debug.Log($"TORCH2_HELD_SNAP: {level.name} passed {closest:0.0} degrees from an answer, {Quaternion.Angle(Rotation,answer):0.0} off it now");
            if(Quaternion.Angle(Rotation,answer)<.5f){Rotation=answer;Solve();return;}
            SnapTo(answer,true);
        }
        // The least angle to any answer along the turn from a to b (every 2 degrees), and that answer.
        float Closest(Quaternion a,Quaternion b,out Quaternion answer)
        {
            int steps=Mathf.Clamp(Mathf.CeilToInt(Quaternion.Angle(a,b)/2),1,45);float best=float.MaxValue;answer=Projection.Solution;
            for(int i=0;i<=steps;i++){var pose=Projection.NearestAnswer(Quaternion.Slerp(a,b,i/(float)steps),out float d);if(d<best){best=d;answer=pose;}}
            return best;
        }
        public void Hint(){if(Completed||bare)return;hintUntil=Time.unscaledTime+12;UpdateMessage();world.Refresh();}
        public void ResetChallenge()
        {
            if(Busy)return;Completed=false;StableTime=0;hintUntil=0;snapLength=0;rightLength=0;lockLength=0;solveOnLanding=false;SetPose(Torch2Projection.Home,Projection.Start);world.ResetReveal();Refresh();
        }
        public void Height(float amount){if(!Busy)transform.position+=Vector3.up*amount;}
    }
}
