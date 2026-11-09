using UnityEngine;
using UnityEngine.InputSystem;

namespace CloudGarden.ShadowPuzzle
{
    // Quest: the flashlight is bound to one hand and aims along that hand's pointer ray; the other hand pinches the
    // turntable and turns it like a lazy Susan. Desktop: left-drag turns the table, right-drag orbits the light.
    public sealed class TorchInput : MonoBehaviour
    {
        public TorchWorkshop game;
        public const float GrabRadius=.30f,LensAhead=.09f;
        readonly ShadowPinchGesture[] gestures={new ShadowPinchGesture(),new ShadowPinchGesture()};
        int turning=-1;
        float grabAngle,grabYaw;
        readonly RaycastHit[] hits=new RaycastHit[32];
        void Update()
        {
            game.world.HoverTurntable=false;
            if(game.xr&&game.xr.Active){if(!game.xr.HeadReady){ReleaseAll();return;}Torch();Turntable();}else Desktop();
        }
        void Torch()
        {
            int h=game.TorchHand;
            // Tracking loss leaves the flashlight where it was, like setting it down.
            if(!game.xr.ReadHand(h==0,out var palm,out _,out _))return;
            Vector3 direction=game.xr.ReadAim(h==0,out var aim)?aim.rotation*Vector3.forward:(palm.position-game.viewCamera.transform.position).normalized;
            var lens=palm.position+direction*LensAhead;
            game.SetLight(transform.InverseTransformPoint(lens),transform.InverseTransformDirection(direction));
        }
        void Turntable()
        {
            int h=game.TurnHand;bool tracked=game.xr.ReadHand(h==0,out var palm,out var tip,out var pinch);
            SampleTurn(h,tracked,tip,pinch,Time.unscaledDeltaTime);
        }
        // Live hands and regression playback share this path. Angle is measured about the turntable axis.
        public void SampleTurn(int h,bool tracked,Vector3 tip,float pinchDistance,float deltaTime)
        {
            var gesture=gestures[h];gesture.Sample(tracked,pinchDistance,deltaTime);
            if(!tracked){if(turning==h){game.EndTurn(h);turning=-1;}return;}
            var local=transform.InverseTransformPoint(tip);
            if(turning==h)
            {
                if(!gesture.Closed){game.EndTurn(h);turning=-1;}else game.Turn(h,grabYaw+Mathf.DeltaAngle(grabAngle,Angle(local)));
                return;
            }
            bool near=Near(local);game.world.HoverTurntable=near;
            ShadowControl control=null;float controlDistance=.035f;
            if(!game.xr||!game.xr.UsesNativeMenu)foreach(var button in game.world.controls)if(button.Available){float d=Vector3.Distance(tip,button.GetComponent<Collider>().ClosestPoint(tip));if(d<controlDistance){control=button;controlDistance=d;}}
            if(!gesture.Pressed)return;
            if(control!=null)control.Press();
            else if(near&&game.BeginTurn(h)){turning=h;grabAngle=Angle(local);grabYaw=game.Yaw;}
        }
        public static bool Near(Vector3 local)
        {
            var d=new Vector2(local.x-TorchProjection.Axis.x,local.z-TorchProjection.Axis.z);return d.magnitude<=GrabRadius&&local.y>=.02f&&local.y<=.62f;
        }
        static float Angle(Vector3 local)=>Mathf.Atan2(local.x-TorchProjection.Axis.x,local.z-TorchProjection.Axis.z)*Mathf.Rad2Deg;
        void Desktop()
        {
            var mouse=Mouse.current;var keyboard=Keyboard.current;if(mouse==null||!game.viewCamera)return;
            var ray=game.viewCamera.ScreenPointToRay(mouse.position.ReadValue());ShadowControl button=null;float nearest=10;
            int count=Physics.RaycastNonAlloc(ray,hits,10);
            for(int i=0;i<count;i++){var control=hits[i].collider.GetComponent<ShadowControl>();if(control!=null&&hits[i].distance<nearest){nearest=hits[i].distance;button=control;}}
            if(mouse.leftButton.wasPressedThisFrame&&button!=null){button.Press();return;}
            var delta=mouse.delta.ReadValue();float dt=Time.unscaledDeltaTime;
            if(mouse.leftButton.wasPressedThisFrame&&game.BeginTurn(2))turning=2;
            if(turning==2){if(mouse.leftButton.isPressed)game.Turn(2,game.Yaw-delta.x*.35f);else{game.EndTurn(2);turning=-1;}}
            float az=game.Azimuth,el=game.Elevation,distance=game.Distance;
            if(mouse.rightButton.isPressed){az+=delta.x*.25f;el+=delta.y*.25f;}
            distance*=1-Mathf.Clamp(mouse.scroll.ReadValue().y/120,-3,3)*.06f;
            if(keyboard!=null)
            {
                if(turning<0&&(keyboard.aKey.isPressed||keyboard.dKey.isPressed)&&game.BeginTurn(2)){game.Turn(2,game.Yaw+(keyboard.aKey.isPressed?60:-60)*dt);game.EndTurn(2);}
                if(keyboard.wKey.isPressed)el+=30*dt;if(keyboard.sKey.isPressed)el-=30*dt;
                if(keyboard.qKey.isPressed)az-=40*dt;if(keyboard.eKey.isPressed)az+=40*dt;
                if(keyboard.hKey.wasPressedThisFrame)game.Hint();
            }
            if(az!=game.Azimuth||el!=game.Elevation||distance!=game.Distance)game.Orbit(az,el,distance);
        }
        public void ReleaseAll()
        {
            if(turning>=0)game.EndTurn(turning);turning=-1;
            foreach(var g in gestures)g.Reset();
        }
        void OnApplicationFocus(bool focused){if(!focused)ReleaseAll();}
        void OnApplicationPause(bool paused){if(paused)ReleaseAll();}
        void OnDisable(){if(game)ReleaseAll();}
    }
}
