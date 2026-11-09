using UnityEngine;
using UnityEngine.InputSystem;

namespace CloudGarden.ShadowPuzzle
{
    // The pile sits in an orb. Quest: pinch the orb's shell and drag to roll it like a ball; the point you pinched
    // stays under your fingers, so the turn comes from the arm, not the wrist, and any axis works. Pinch the hub in the
    // middle to carry it without turning. Either hand; the other hand can take over. A sample that jumps further than
    // any hand can (a tracking flip) is skipped, and so is the jump back. Desktop: left-drag rolls the orb under the
    // cursor, right-drag moves it in the view plane, the wheel moves it in depth.
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
        int holding=-1;
        Grip grip;
        // A hold taken from afar (RemoteGrab): the point looked at on the shell, and where the hand was then.
        bool remote;
        Vector3 remoteAnchor,remoteStart;
        public bool HoldingRemote=>holding>=0&&remote;
        // A look and a pinch from afar (Shape of Memory): the point looked at on the orb's shell is held as if the fingers
        // were on it, and the hand's movement since moves it, so the orb rolls as it would under the fingers. Released
        // when the pinch opens past the book's release distance.
        public const float RemoteRelease=.04f;
        public bool RemoteGrab(int h,Vector3 worldPoint,Vector3 worldTip)
        {
            if(holding>=0||!game.BeginHold(h))return false;
            holding=h;grip=Grip.Roll;remote=true;
            remoteAnchor=transform.InverseTransformPoint(worldPoint);remoteStart=transform.InverseTransformPoint(worldTip);
            lastPinch=remoteAnchor;lastSpoke=lastPinch-game.Position;return true;
        }
        // Pinch point fixed in the hand at the grab; last pinch and last orb centre -> pinch, in workbench space.
        Vector3 pinchInHand,lastPinch,lastSpoke;
        readonly RaycastHit[] hits=new RaycastHit[32];
        public Grip Grabbed=>holding>=0?grip:Grip.None;
        void Update()
        {
            game.world.Hover=Grip.None;
            if(game.xr&&game.xr.Active){if(!game.xr.HeadReady){ReleaseAll();return;}for(int h=0;h<2;h++)Hand(h);}else Desktop();
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
        // Live hands and regression playback share this path.
        public void SampleHand(int h,bool tracked,Pose palm,Vector3 tip,float pinchDistance,float deltaTime)
        {
            var gesture=gestures[h];gesture.Sample(tracked,pinchDistance,deltaTime);
            // The orb left the hand by itself (Torch2Workshop.SnapWhileHeld): let go of it here too; a new pinch takes it.
            if(holding==h&&game.Owner!=h)Drop();
            // Tracking loss leaves the orb where it was.
            if(!tracked){if(holding==h)Release();return;}
            if(holding==h)
            {
                if(remote?pinchDistance>=RemoteRelease:!gesture.Closed){Release();return;}
                var pinch=remote?remoteAnchor+(transform.InverseTransformPoint(tip)-remoteStart):transform.InverseTransformPoint(palm.position+palm.rotation*pinchInHand);
                var move=pinch-lastPinch;lastPinch=pinch;
                if(grip==Grip.Carry){if(move.magnitude<=MaxMovePerSample)game.Hold(h,game.Position+move,game.Rotation);return;}
                var spoke=pinch-game.Position;var from=lastSpoke;lastSpoke=spoke;
                // Too near the centre to tell which way it rolls, or a tracking jump.
                float centre=.05f*OrbScale;
                if(spoke.magnitude<centre||from.magnitude<centre||move.magnitude>MaxMovePerSample||Vector3.Angle(from,spoke)>MaxTurnPerSample)return;
                game.Hold(h,game.Position,Quaternion.FromToRotation(from,spoke)*game.Rotation);
                if(game.Owner!=h)Drop();
                return;
            }
            var zone=Zone(tip);if(zone!=Grip.None)game.world.Hover=zone;
            ShadowControl control=null;float controlDistance=.035f;
            if(!game.xr||!game.xr.UsesNativeMenu)foreach(var button in game.world.controls)if(button.Available){float d=Vector3.Distance(tip,button.GetComponent<Collider>().ClosestPoint(tip));if(d<controlDistance){control=button;controlDistance=d;}}
            if(!gesture.Pressed)return;
            if(control!=null)control.Press();
            else if(zone!=Grip.None&&game.BeginHold(h))
            {
                holding=h;grip=zone;pinchInHand=Quaternion.Inverse(palm.rotation)*(tip-palm.position);
                lastPinch=transform.InverseTransformPoint(tip);lastSpoke=lastPinch-game.Position;
            }
        }
        void Desktop()
        {
            var mouse=Mouse.current;var keyboard=Keyboard.current;if(mouse==null||!game.viewCamera)return;
            if(holding==2&&game.Owner!=2)Drop();
            var ray=game.viewCamera.ScreenPointToRay(mouse.position.ReadValue());ShadowControl button=null;float nearest=10;
            int count=Physics.RaycastNonAlloc(ray,hits,10);
            for(int i=0;i<count;i++){var control=hits[i].collider.GetComponent<ShadowControl>();if(control!=null&&hits[i].distance<nearest){nearest=hits[i].distance;button=control;}}
            if(mouse.leftButton.wasPressedThisFrame&&button!=null){button.Press();return;}
            var local=new Ray(transform.InverseTransformPoint(ray.origin),transform.InverseTransformDirection(ray.direction).normalized);
            var delta=mouse.delta.ReadValue();float dt=Time.unscaledDeltaTime;float scroll=Mathf.Clamp(mouse.scroll.ReadValue().y/120,-3,3);
            bool drag=mouse.leftButton.isPressed||mouse.rightButton.isPressed;
            if(mouse.leftButton.wasPressedThisFrame&&game.BeginHold(2)){holding=2;grip=Grip.Roll;lastSpoke=OrbPoint(local)-game.Position;}
            else if(!FixedInPlace&&mouse.rightButton.wasPressedThisFrame&&game.BeginHold(2)){holding=2;grip=Grip.Carry;}
            if(!drag&&holding<0&&Vector3.Cross(local.direction,game.Position-local.origin).magnitude<=Radius)game.world.Hover=Grip.Roll;
            var rotation=game.Rotation;var position=game.Position;
            var right=transform.InverseTransformDirection(game.viewCamera.transform.right);var up=transform.InverseTransformDirection(game.viewCamera.transform.up);var forward=transform.InverseTransformDirection(game.viewCamera.transform.forward);
            if(holding==2&&mouse.leftButton.isPressed){var spoke=OrbPoint(local)-game.Position;rotation=Quaternion.FromToRotation(lastSpoke,spoke)*rotation;lastSpoke=spoke;}
            if(!FixedInPlace&&mouse.rightButton.isPressed)position+=(right*delta.x+up*delta.y)*.0012f;
            if(!FixedInPlace)position+=forward*scroll*.02f;
            if(keyboard!=null)
            {
                // A/D turn about up, W/S tip toward or away, Z/C roll about the view direction.
                float yaw=(keyboard.aKey.isPressed?60:0)-(keyboard.dKey.isPressed?60:0),tip=(keyboard.wKey.isPressed?45:0)-(keyboard.sKey.isPressed?45:0),roll=(keyboard.zKey.isPressed?45:0)-(keyboard.cKey.isPressed?45:0);
                rotation=Quaternion.AngleAxis(yaw*dt,Vector3.up)*Quaternion.AngleAxis(tip*dt,right)*Quaternion.AngleAxis(roll*dt,forward)*rotation;
                if(!FixedInPlace)
                {
                    if(keyboard.leftArrowKey.isPressed)position-=right*.15f*dt;if(keyboard.rightArrowKey.isPressed)position+=right*.15f*dt;
                    if(keyboard.upArrowKey.isPressed)position+=Vector3.up*.15f*dt;if(keyboard.downArrowKey.isPressed)position-=Vector3.up*.15f*dt;
                    if(keyboard.qKey.isPressed)position+=forward*.15f*dt;if(keyboard.eKey.isPressed)position-=forward*.15f*dt;
                }
                if(keyboard.hKey.wasPressedThisFrame)game.Hint();
            }
            bool changed=!rotation.Equals(game.Rotation)||position!=game.Position;
            if(changed&&(holding==2||game.BeginHold(2))){holding=2;game.Hold(2,position,rotation);}
            if(holding==2&&!drag)Release();
        }
        // Where the cursor ray meets the orb; past its outline, the nearest point on the outline.
        Vector3 OrbPoint(Ray ray)
        {
            var centre=game.Position;var closest=ray.origin+ray.direction*Vector3.Dot(centre-ray.origin,ray.direction);
            var off=closest-centre;float d=off.magnitude,r=Radius;
            if(d>=r)return centre+off*(r/Mathf.Max(d,1e-6f));
            return closest-ray.direction*Mathf.Sqrt(r*r-d*d);
        }
        void Release(){if(holding>=0)game.EndHold(holding);Drop();}
        void Drop(){holding=-1;grip=Grip.None;remote=false;}
        public void ReleaseAll()
        {
            Release();
            foreach(var g in gestures)g.Reset();
        }
        void OnApplicationFocus(bool focused){if(!focused)ReleaseAll();}
        void OnApplicationPause(bool paused){if(paused)ReleaseAll();}
        void OnDisable(){if(game)ReleaseAll();}
    }
}
