using UnityEngine;
using UnityEngine.InputSystem;

namespace CloudGarden.ShadowPuzzle
{
    public sealed class SilhouetteInput : MonoBehaviour
    {
        public SilhouetteWorkshop game;
        readonly int[] held={-1,-1};
        readonly ShadowPinchGesture[] gestures={new ShadowPinchGesture(),new ShadowPinchGesture()};
        readonly Vector3[] offsets=new Vector3[2];
        readonly Quaternion[] rotations=new Quaternion[2];
        int selected,desktopPart=-1;
        Vector3 dragOffset;
        float editUntil;
        readonly RaycastHit[] hits=new RaycastHit[64];
        public int Selected=>selected;
        void Update()
        {
            game.world.HoverPart=-1;
            if(game.xr&&game.xr.Active){if(!game.xr.HeadReady){ReleaseAll();return;}Hand(0);Hand(1);}else Desktop();
        }
        void Hand(int h)
        {
            bool tracked=game.xr.ReadHand(h==0,out var palm,out var index,out var distance);
            SampleHand(h,tracked,palm,index,distance,Time.unscaledDeltaTime);
        }
        // Live hands and deterministic regression playback use the same interaction path.
        public void SampleHand(int h,bool tracked,Pose palm,Vector3 tip,float pinchDistance,float deltaTime)
        {
            var gesture=gestures[h];gesture.Sample(tracked,pinchDistance,deltaTime);
            if(!tracked){if(held[h]>=0)game.EndGrab(held[h],h,true);held[h]=-1;return;}
            if(held[h]>=0&&game.owners[held[h]]!=h){held[h]=-1;gesture.Reset();return;}
            if(held[h]>=0)
            {
                if(!gesture.Closed){game.EndGrab(held[h],h);held[h]=-1;}
                else
                {
                    Vector3 position=tip+palm.rotation*offsets[h];Quaternion rotation=palm.rotation*rotations[h];
                    game.MoveGrab(held[h],h,new ShadowPose(transform.InverseTransformPoint(position),Quaternion.Inverse(transform.rotation)*rotation));
                }
            }
            else
            {
                ShadowControl control=null;float controlDistance=.035f;
                foreach(var button in game.world.controls)if((!game.xr || !game.xr.UsesNativeMenu) && button.Available){float d=Vector3.Distance(tip,button.GetComponent<Collider>().ClosestPoint(tip));if(d<controlDistance){control=button;controlDistance=d;}}
                int nearest=-1;float distance=.045f;
                for(int i=0;i<game.Count;i++)if(game.CanGrab(i)){float d=Vector3.Distance(tip,game.ClosestPoint(i,tip));if(d<distance){distance=d;nearest=i;}}
                if(nearest>=0)game.world.HoverPart=nearest;
                if(gesture.Pressed)
                {
                    if(control!=null)control.Press();
                    else if(nearest>=0&&game.BeginGrab(nearest,h))
                    {
                        held[h]=nearest;offsets[h]=Quaternion.Inverse(palm.rotation)*(game.pieces[nearest].position-tip);rotations[h]=Quaternion.Inverse(palm.rotation)*game.pieces[nearest].rotation;
                    }
                }
            }
        }
        void Desktop()
        {
            var mouse=Mouse.current;var keyboard=Keyboard.current;if(mouse==null || !game.viewCamera)return;
            var ray=game.viewCamera.ScreenPointToRay(mouse.position.ReadValue());int hovered=-1;ShadowControl button=null;
            float nearestDistance=10;int count=Physics.RaycastNonAlloc(ray,hits,10);
            for(int h=0;h<count;h++)
            {
                var hit=hits[h];if(hit.distance>=nearestDistance)continue;
                var control=hit.collider.GetComponent<ShadowControl>();int piece=-1;
                for(int i=0;i<game.Count;i++)if(hit.transform.IsChildOf(game.pieces[i])&&game.CanGrab(i))piece=i;
                if(control!=null||piece>=0){nearestDistance=hit.distance;button=control;hovered=piece;}
            }
            if(hovered>=0)game.world.HoverPart=hovered;
            bool down=mouse.leftButton.wasPressedThisFrame||mouse.rightButton.wasPressedThisFrame;
            if(down)
            {
                if(button!=null&&mouse.leftButton.wasPressedThisFrame){FinishDesktop();button.Press();return;}
                if(hovered>=0)
                {
                    FinishDesktop();selected=hovered;StartDesktop();
                    // Rests lie outside the light's depth range; drag from them on the base's mid plane.
                    if(desktopPart>=0&&!SilhouetteProjection.InWorkspace(game.poses[selected])){var pose=game.poses[selected];pose.position.z=0;game.MoveGrab(selected,2,pose);}
                    dragOffset=game.poses[selected].position-PlanePoint(ray,game.poses[selected].position.z);
                }
            }
            if(keyboard!=null)
            {
                for(int i=0;i<Mathf.Min(9,game.Count);i++)if(keyboard[(Key)((int)Key.Digit1+i)].wasPressedThisFrame&&game.CanGrab(i)){FinishDesktop();selected=i;}
                if(keyboard.zKey.wasPressedThisFrame||keyboard.backspaceKey.wasPressedThisFrame){FinishDesktop();game.Undo();}
                if(keyboard.hKey.wasPressedThisFrame)game.Hint();
                if(keyboard.lKey.wasPressedThisFrame){FinishDesktop();game.Snap=!game.Snap;}
                // Exact 15 degree turns of the selected object: arrows roll/pitch, [ ] yaw.
                var step=keyboard.leftArrowKey.wasPressedThisFrame?Quaternion.AngleAxis(15,Vector3.forward):keyboard.rightArrowKey.wasPressedThisFrame?Quaternion.AngleAxis(-15,Vector3.forward):
                    keyboard.upArrowKey.wasPressedThisFrame?Quaternion.AngleAxis(15,Vector3.right):keyboard.downArrowKey.wasPressedThisFrame?Quaternion.AngleAxis(-15,Vector3.right):
                    keyboard.leftBracketKey.wasPressedThisFrame?Quaternion.AngleAxis(15,Vector3.up):keyboard.rightBracketKey.wasPressedThisFrame?Quaternion.AngleAxis(-15,Vector3.up):(Quaternion?)null;
                if(step.HasValue&&desktopPart<0&&game.CanGrab(selected)&&game.BeginGrab(selected,2))
                {
                    var pose=game.poses[selected];if(!SilhouetteProjection.InWorkspace(pose))pose.position=new Vector3(0,.29f,0);
                    pose.rotation=step.Value*pose.rotation;game.MoveGrab(selected,2,pose);game.EndGrab(selected,2);
                }
            }
            float scroll=mouse.scroll.ReadValue().y;bool roll=keyboard!=null&&(keyboard.qKey.isPressed||keyboard.eKey.isPressed);
            if((Mathf.Abs(scroll)>.1f||roll)&&game.CanGrab(selected)){StartDesktop();editUntil=Time.unscaledTime+.2f;}
            if(desktopPart>=0)
            {
                var pose=game.poses[desktopPart];
                if(mouse.leftButton.isPressed)pose.position=PlanePoint(ray,pose.position.z)+dragOffset;
                if(mouse.rightButton.isPressed){var d=mouse.delta.ReadValue();pose.rotation=Quaternion.Euler(-d.y*.45f,d.x*.45f,0)*pose.rotation;}
                pose.position.z+=Mathf.Clamp(scroll/120,-3,3)*.012f;
                if(roll)pose.rotation=Quaternion.AngleAxis((keyboard.qKey.isPressed?1:-1)*65*Time.unscaledDeltaTime,Vector3.forward)*pose.rotation;
                game.MoveGrab(desktopPart,2,pose);
                if(!mouse.leftButton.isPressed&&!mouse.rightButton.isPressed&&Time.unscaledTime>editUntil)FinishDesktop();
            }
        }
        Vector3 PlanePoint(Ray ray,float z)
        {
            var plane=new Plane(transform.forward,transform.TransformPoint(new Vector3(0,0,z)));return plane.Raycast(ray,out float distance)?transform.InverseTransformPoint(ray.GetPoint(distance)):game.poses[selected].position;
        }
        void StartDesktop(){if(desktopPart<0&&game.BeginGrab(selected,2))desktopPart=selected;}
        void FinishDesktop(){if(desktopPart>=0){game.EndGrab(desktopPart,2);desktopPart=-1;}editUntil=0;}
        public void ReleaseAll()
        {
            for(int h=0;h<2;h++){if(held[h]>=0)game.EndGrab(held[h],h,true);held[h]=-1;gestures[h].Reset();}
            FinishDesktop();
        }
        void OnApplicationFocus(bool focused){if(!focused)ReleaseAll();}
        void OnApplicationPause(bool paused){if(paused)ReleaseAll();}
        void OnDisable(){if(game)ReleaseAll();}
    }
}
