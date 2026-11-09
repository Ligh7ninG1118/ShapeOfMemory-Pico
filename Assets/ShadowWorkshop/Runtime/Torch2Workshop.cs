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
        public const float RightSeconds=1.4f;
        float nextWall,nextScore,hintUntil,snapTime,snapLength,rightTime,rightLength;
        Quaternion snapFrom,snapTo,rightFrom;
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
                Rotation=Quaternion.Slerp(snapFrom,snapTo,1-(1-t)*(1-t)*(1-t));
                if(t>=1){snapLength=0;if(solveOnLanding){solveOnLanding=false;Solve();}}
            }
            if(Righting)
            {
                rightTime+=Time.unscaledDeltaTime;float t=Mathf.Clamp01(rightTime/rightLength);
                Rotation=Quaternion.Slerp(rightFrom,Projection.Solution,t*t*(3-2*t));if(t>=1)rightLength=0;
            }
            if(world.Animating&&Time.unscaledTime>=nextWall){nextWall=Time.unscaledTime+1/30f;world.RefreshWalls();}
            if(Time.unscaledTime>=nextScore){nextScore=Time.unscaledTime+.1f;Refresh();}
        }
        void Refresh(){Projection.Render(Rotation,Position);Projection.Evaluate();Evaluate(.1f);world.Refresh();}
        public void Evaluate(float dt)
        {
            if(Completed)return;
            StableTime=Projection.Score.Ready&&!Busy&&!Snapping?StableTime+dt:0;
            if(StableTime>=SettleSeconds)Solve();
            else UpdateMessage();
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
        public bool BeginHold(int owner){if(Completed||solveOnLanding||owner<0||(Owner>=0&&Owner!=owner))return false;Owner=owner;StableTime=0;snapLength=0;return true;}
        // The owner can lose the orb here (SnapWhileHeld): a holder checks Owner after each Hold.
        public void Hold(int owner,Vector3 position,Quaternion rotation)
        {
            if(Owner!=owner)return;var before=Rotation;SetPose(position,rotation);
            if(SnapWhileHeld)HeldSnap(before);
        }
        public void EndHold(int owner){if(Owner!=owner)return;Owner=-1;TrySnap();}
        // Let go within snapAngle of an answer, passing or at snapWarmth, and the orb eases into that exact answer (only the
        // rotation; the score ignores where the pile sits). Never while held, and never from a lookalike far away.
        public bool TrySnap()
        {
            if(Completed||Busy)return false;
            Projection.Render(Rotation,Position);Projection.Evaluate();
            var s=Projection.Score;float angle=Projection.AngleToAnswer(Rotation,out int answer);
            if(angle<.5f||angle>level.snapAngle||!(s.Ready||s.warmth>=level.snapWarmth))return false;
            SnapTo(Projection.Answers[answer],false);return true;
        }
        void SnapTo(Quaternion answer,bool solve)
        {
            float angle=Quaternion.Angle(Rotation,answer);snapFrom=Rotation;snapTo=answer;snapTime=0;
            snapLength=Mathf.Lerp(.25f,.5f,angle/Mathf.Max(1,level.snapAngle));solveOnLanding=solve;
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
            for(int i=0;i<=steps;i++){float d=Projection.AngleToAnswer(Quaternion.Slerp(a,b,i/(float)steps),out int k);if(d<best){best=d;answer=Projection.Answers[k];}}
            return best;
        }
        public void Hint(){if(Completed||bare)return;hintUntil=Time.unscaledTime+12;UpdateMessage();world.Refresh();}
        public void ResetChallenge()
        {
            if(Busy)return;Completed=false;StableTime=0;hintUntil=0;snapLength=0;rightLength=0;solveOnLanding=false;SetPose(Torch2Projection.Home,Projection.Start);world.ResetReveal();Refresh();
        }
        public void Height(float amount){if(!Busy)transform.position+=Vector3.up*amount;}
    }
}
