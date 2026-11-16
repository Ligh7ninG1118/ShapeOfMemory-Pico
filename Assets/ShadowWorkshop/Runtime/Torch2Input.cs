using UnityEngine;
using UnityEngine.InputSystem;

namespace CloudGarden.ShadowPuzzle
{
    // The pile sits in an orb. Quest: pinch the orb's shell and drag to roll it like a ball; the point you pinched
    // stays under your fingers, so the turn comes from the arm, not the wrist, and any axis works. Pinch the hub in the
    // middle to carry it without turning. Either hand, or both: a second hand pinching the shell while the first rolls
    // it takes hold too, and the orb then turns the way that best carries both grips (TwoHandTurn): a hand held still is
    // a pivot the other turns it about, hands on opposite sides turn it like a wheel, hands moving together roll it as
    // one. Let go with either and the other carries on. A sample that jumps further than any hand can (a tracking flip)
    // is skipped, and so is the jump back. Desktop: left-drag rolls the orb under the cursor, right-drag moves it in the
    // view plane, the wheel moves it in depth.
    public sealed class Torch2Input : MonoBehaviour
    {
        public enum Grip{None,Roll,Carry}
        public Torch2Workshop game;
        // The pile stays where the level put it: no carrying by the hub (a pinch there takes nothing), no desktop moves.
        // Only rolling. (Shape of Memory; the gym still carries.)
        public bool FixedInPlace;
        public const float OrbRadius=.30f,HubRadius=.10f,GrabReach=.36f,MaxTurnPerSample=30,MaxMovePerSample=.12f;
        // The orb as drawn, against the pile its shadow comes from (Shape of Memory draws it smaller, MemoryGame.PileScale):
        // the rings, the hub zone and the centre guard scale with it; the reach is ReachMargin past the drawn rings.
        public float OrbScale=1;
        public float ReachMargin=GrabReach-OrbRadius;
        public float Radius=>OrbRadius*OrbScale;
        public float Reach=>Radius+ReachMargin;
        readonly ShadowPinchGesture[] gestures={new ShadowPinchGesture(),new ShadowPinchGesture()};
        // One hand's hold, in workbench space: the pinch point fixed in the hand at the grab, where that point is now and
        // was at the last turn, and the orb centre -> pinch then. A hold taken from afar (RemoteGrab) instead keeps the
        // point looked at on the shell (anchor) and where the hand was then (start). Not holding: its fingertip, and
        // whether a pinch there would take hold (hover).
        sealed class Grasp
        {
            public bool on,remote,hover;
            public Vector3 pinchInHand,now,last,lastSpoke,anchor,start,tip;
        }
        readonly Grasp[] grasps={new Grasp(),new Grasp()};
        // The holder the game knows (Torch2Workshop.Owner): a holding hand (0 left, 1 right) or the desktop (2); -1 none.
        // With both hands on, the owner is one of them.
        int owner=-1;
        Grip grip;
        public bool HoldingRemote=>owner>=0&&owner<2&&grasps[owner].remote;
        // A look and a pinch from afar (Shape of Memory): the point looked at on the orb's shell is held as if the fingers
        // were on it, and the hand's movement since moves it, so the orb rolls as it would under the fingers. Released
        // when the pinch opens past the book's release distance. One hand only: no second hand joins it.
        public const float RemoteRelease=.04f;
        public bool RemoteGrab(int h,Vector3 worldPoint,Vector3 worldTip)
        {
            if(owner>=0||!game.BeginHold(h))return false;
            owner=h;grip=Grip.Roll;var g=grasps[h];g.on=g.remote=true;
            g.anchor=transform.InverseTransformPoint(worldPoint);g.start=transform.InverseTransformPoint(worldTip);
            g.now=g.last=g.anchor;g.lastSpoke=g.anchor-game.Position;Log(h,"takes it from afar");return true;
        }
        readonly RaycastHit[] hits=new RaycastHit[32];
        public Grip Grabbed=>owner>=0?grip:Grip.None;
        // Where hand h holds the orb (its pinch point, workbench space) and whether from afar (RemoteGrab).
        public bool Holding(int h,out Vector3 at,out bool fromAfar){var g=grasps[h];at=g.now;fromAfar=g.remote;return owner>=0&&g.on;}
        // Where a pinch by hand h would take hold of the orb now (its fingertip, workbench space).
        public bool Hovering(int h,out Vector3 at){var g=grasps[h];at=g.tip;return g.hover&&!g.on;}
        void Update()
        {
            game.world.Hover=Grip.None;
            // The orb left the hands by itself (Torch2Workshop.SnapWhileHeld): let go of it here too; a new pinch takes it.
            if(owner>=0&&game.Owner!=owner)Drop();
            if(game.xr&&game.xr.Active){if(!game.xr.HeadReady){ReleaseAll();return;}for(int h=0;h<2;h++)Hand(h);Turn();}else Desktop();
        }
        void Hand(int h)
        {
            bool tracked=game.xr.ReadHand(h==0,out var palm,out var tip,out var pinch);
            SampleHand(h,tracked,palm,tip,pinch,Time.unscaledDeltaTime);
        }
        // What a pinch at this point would grab: the hub carries, the shell rolls.
        public Grip Zone(Vector3 worldPoint)
        {
            float d=Vector3.Distance(transform.InverseTransformPoint(worldPoint),game.Position);
            return d<=HubRadius*OrbScale?(FixedInPlace?Grip.None:Grip.Carry):d<=Reach?Grip.Roll:Grip.None;
        }
        public bool Near(Vector3 worldPoint)=>Zone(worldPoint)!=Grip.None;
        // Live hands and regression playback share this path: sample each hand, then Turn once.
        public void SampleHand(int h,bool tracked,Pose palm,Vector3 tip,float pinchDistance,float deltaTime)
        {
            var gesture=gestures[h];gesture.Sample(tracked,pinchDistance,deltaTime);var g=grasps[h];g.hover=false;
            if(g.on)
            {
                // Tracking loss leaves the orb where it was.
                if(!tracked||(g.remote?pinchDistance>=RemoteRelease:!gesture.Closed)){Let(h);return;}
                g.now=g.remote?g.anchor+(transform.InverseTransformPoint(tip)-g.start):transform.InverseTransformPoint(palm.position+palm.rotation*g.pinchInHand);
                return;
            }
            if(!tracked)return;
            var zone=Zone(tip);if(zone!=Grip.None)game.world.Hover=zone;
            ShadowControl control=null;float controlDistance=.035f;
            if(!game.xr||!game.xr.UsesNativeMenu)foreach(var button in game.world.controls)if(button.Available){float d=Vector3.Distance(tip,button.GetComponent<Collider>().ClosestPoint(tip));if(d<controlDistance){control=button;controlDistance=d;}}
            g.tip=transform.InverseTransformPoint(tip);g.hover=control==null&&zone!=Grip.None&&CanTake(h,zone);
            if(!gesture.Pressed)return;
            if(control!=null)control.Press();
            else if(zone!=Grip.None&&Take(h,zone))
            {
                g.on=true;g.remote=false;g.hover=false;g.pinchInHand=Quaternion.Inverse(palm.rotation)*(tip-palm.position);
                g.now=g.last=g.tip;g.lastSpoke=g.now-game.Position;
            }
        }
        // A free orb: the game must let this hand hold it. Held: a second hand joins a roll by hand (never a carry, and
        // never a hold from afar).
        bool CanTake(int h,Grip zone)=>owner<0?game.CanHold(h):owner<2&&owner!=h&&grip==Grip.Roll&&zone==Grip.Roll&&!grasps[owner].remote;
        bool Take(int h,Grip zone)
        {
            if(!CanTake(h,zone))return false;
            if(owner>=0){Log(h,"joins");return true;}
            if(!game.BeginHold(h))return false;
            owner=h;grip=zone;Log(h,"takes it");return true;
        }
        // Hand h lets go: the other hand, still holding, carries on from where it is.
        void Let(int h)
        {
            var g=grasps[h];g.on=g.remote=false;int other=1-h;
            if(owner>=0&&owner<2&&grasps[other].on)
            {
                if(owner==h){game.PassHold(h,other);owner=other;}
                Log(h,"lets go, the other hand keeps it");return;
            }
            Release();
        }
        // Turns (or carries) the orb by the hands holding it, after both hands have been sampled.
        public void Turn()
        {
            if(owner<0||owner>1)return;
            int a=grasps[0].on?0:1,b=grasps[0].on&&grasps[1].on?1:-1;
            if(!grasps[a].on)return;
            if(grip==Grip.Carry)
            {
                var g=grasps[a];var move=g.now-g.last;g.last=g.now;
                if(move.magnitude<=MaxMovePerSample)game.Hold(owner,game.Position+move,game.Rotation);
            }
            else
            {
                bool ok=Step(grasps[a],out var a0,out var a1);Vector3 b0=default,b1=default;
                if(b>=0)ok&=Step(grasps[b],out b0,out b1);
                if(!ok)return;
                game.Hold(owner,game.Position,(b<0?Quaternion.FromToRotation(a0,a1):TwoHandTurn(a0,b0,a1,b1))*game.Rotation);
            }
            if(game.Owner!=owner)Drop();
        }
        // One grip's spoke (orb centre -> pinch) at the last turn and now. False when it is too near the centre to tell
        // which way it rolls, or jumped further than a hand can (a tracking flip): that sample is skipped.
        bool Step(Grasp g,out Vector3 from,out Vector3 to)
        {
            var move=g.now-g.last;g.last=g.now;from=g.lastSpoke;to=g.now-game.Position;g.lastSpoke=to;float centre=.05f*OrbScale;
            return to.magnitude>=centre&&from.magnitude>=centre&&move.magnitude<=MaxMovePerSample&&Vector3.Angle(from,to)<=MaxTurnPerSample;
        }
        // Two grips on the orb, each a spoke from its centre before (a0, b0) and now (a1, b1): the turn that best carries
        // both, the centre fixed. Taken from the grips' directions, whose chord (a-b) and sum (a+b) are at right angles:
        // the swing that carries the longer of the two onto its new self, then the twist about it that carries the other,
        // faded out as the other vanishes. Grips straight across the orb can't say how it spins about the line between
        // them (no twist: a ball pinched at two poles); grips side by side act as one hand at their middle. The twist fades
        // in from 6 to 23 degrees apart and out from 157 to 174 (a short chord would turn tracking jitter into spin). When
        // both grips move as one rigid turn, that turn comes back exactly; one grip held still, the orb turns about its spoke.
        public static Quaternion TwoHandTurn(Vector3 a0,Vector3 b0,Vector3 a1,Vector3 b1)
        {
            a0.Normalize();b0.Normalize();a1.Normalize();b1.Normalize();
            Vector3 d0=a0-b0,d1=a1-b1,s0=a0+b0,s1=a1+b1;
            bool chord=d0.sqrMagnitude+d1.sqrMagnitude>=s0.sqrMagnitude+s1.sqrMagnitude;
            Vector3 p0=chord?d0:s0,p1=chord?d1:s1,q0=chord?s0:d0,q1=chord?s1:d1;
            if(p0.sqrMagnitude<1e-8f||p1.sqrMagnitude<1e-8f)return Quaternion.identity;
            var swing=Quaternion.FromToRotation(p0,p1);var axis=p1.normalized;
            var from=Vector3.ProjectOnPlane(swing*q0,axis);var to=Vector3.ProjectOnPlane(q1,axis);
            float sure=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.1f,.4f,Mathf.Min(from.magnitude,to.magnitude)));
            if(sure<=0)return swing;
            return Quaternion.AngleAxis(Vector3.SignedAngle(from,to,axis)*sure,axis)*swing;
        }
        void Desktop()
        {
            var mouse=Mouse.current;var keyboard=Keyboard.current;if(mouse==null||!game.viewCamera)return;
            var ray=game.viewCamera.ScreenPointToRay(mouse.position.ReadValue());ShadowControl button=null;float nearest=10;
            int count=Physics.RaycastNonAlloc(ray,hits,10);
            for(int i=0;i<count;i++){var control=hits[i].collider.GetComponent<ShadowControl>();if(control!=null&&hits[i].distance<nearest){nearest=hits[i].distance;button=control;}}
            if(mouse.leftButton.wasPressedThisFrame&&button!=null){button.Press();return;}
            var local=new Ray(transform.InverseTransformPoint(ray.origin),transform.InverseTransformDirection(ray.direction).normalized);
            var delta=mouse.delta.ReadValue();float dt=Time.unscaledDeltaTime;float scroll=Mathf.Clamp(mouse.scroll.ReadValue().y/120,-3,3);
            bool drag=mouse.leftButton.isPressed||mouse.rightButton.isPressed;
            if(mouse.leftButton.wasPressedThisFrame&&game.BeginHold(2)){owner=2;grip=Grip.Roll;desktopSpoke=OrbPoint(local)-game.Position;}
            else if(!FixedInPlace&&mouse.rightButton.wasPressedThisFrame&&game.BeginHold(2)){owner=2;grip=Grip.Carry;}
            if(!drag&&owner<0&&Vector3.Cross(local.direction,game.Position-local.origin).magnitude<=Radius)game.world.Hover=Grip.Roll;
            var rotation=game.Rotation;var position=game.Position;
            var right=transform.InverseTransformDirection(game.viewCamera.transform.right);var up=transform.InverseTransformDirection(game.viewCamera.transform.up);var forward=transform.InverseTransformDirection(game.viewCamera.transform.forward);
            if(owner==2&&mouse.leftButton.isPressed){var spoke=OrbPoint(local)-game.Position;rotation=Quaternion.FromToRotation(desktopSpoke,spoke)*rotation;desktopSpoke=spoke;}
            if(!FixedInPlace&&mouse.rightButton.isPressed)position+=(right*delta.x+up*delta.y)*.0012f;
            if(!FixedInPlace)position+=forward*scroll*.02f;
            if(keyboard!=null)
            {
                rotation=KeyTurn(keyboard,dt,right,forward)*rotation;
                if(!FixedInPlace)
                {
                    if(keyboard.leftArrowKey.isPressed)position-=right*.15f*dt;if(keyboard.rightArrowKey.isPressed)position+=right*.15f*dt;
                    if(keyboard.upArrowKey.isPressed)position+=Vector3.up*.15f*dt;if(keyboard.downArrowKey.isPressed)position-=Vector3.up*.15f*dt;
                    if(keyboard.qKey.isPressed)position+=forward*.15f*dt;if(keyboard.eKey.isPressed)position-=forward*.15f*dt;
                }
                if(keyboard.hKey.wasPressedThisFrame)game.Hint();
            }
            bool changed=!rotation.Equals(game.Rotation)||position!=game.Position;
            if(changed&&(owner==2||game.BeginHold(2))){owner=2;game.Hold(2,position,rotation);}
            if(owner==2&&!drag)Release();
        }
        Vector3 desktopSpoke;
        // A/D turn about up, W/S tip toward or away, Z/C roll about the view direction (right, forward: the view's, in the
        // orb's space). The turn for this frame, to put in front of the orb's rotation.
        public static Quaternion KeyTurn(Keyboard keyboard,float dt,Vector3 right,Vector3 forward)
        {
            float yaw=(keyboard.aKey.isPressed?60:0)-(keyboard.dKey.isPressed?60:0),tip=(keyboard.wKey.isPressed?45:0)-(keyboard.sKey.isPressed?45:0),roll=(keyboard.zKey.isPressed?45:0)-(keyboard.cKey.isPressed?45:0);
            return Quaternion.AngleAxis(yaw*dt,Vector3.up)*Quaternion.AngleAxis(tip*dt,right)*Quaternion.AngleAxis(roll*dt,forward);
        }
        // Where the cursor ray meets the orb; past its outline, the nearest point on the outline.
        Vector3 OrbPoint(Ray ray)=>OrbPoint(ray,game.Position,Radius);
        // The same for any orb (the gym's Play mode rolls its cluster with it). ray.direction must be normalised.
        public static Vector3 OrbPoint(Ray ray,Vector3 centre,float r)
        {
            var closest=ray.origin+ray.direction*Vector3.Dot(centre-ray.origin,ray.direction);
            var off=closest-centre;float d=off.magnitude;
            if(d>=r)return centre+off*(r/Mathf.Max(d,1e-6f));
            return closest-ray.direction*Mathf.Sqrt(r*r-d*d);
        }
        void Release(){if(owner>=0)game.EndHold(owner);Drop();}
        void Drop(){owner=-1;grip=Grip.None;foreach(var g in grasps)g.on=g.remote=false;}
        public void ReleaseAll()
        {
            Release();
            foreach(var g in gestures)g.Reset();
            foreach(var g in grasps)g.hover=false;
        }
        static void Log(int h,string what)=>Debug.Log($"TORCH2_GRIP {(h==0?"left":"right")} {what}");
        void OnApplicationFocus(bool focused){if(!focused)ReleaseAll();}
        void OnApplicationPause(bool paused){if(paused)ReleaseAll();}
        void OnDisable(){if(game)ReleaseAll();}
    }
}
