using System.Collections.Generic;
using CloudGarden.Integration;
using CloudGarden.ShadowPuzzle;
using UnityEngine;
using UnityEngine.XR;

namespace ShapeOfMemory
{
    // Look and pinch, on a headset whose runtime gives an eye gaze pose (OpenXR XR_EXT_eye_gaze_interaction; nothing
    // happens without one). What the eyes are on lights as a hovering finger would (a tile rises, a page's edge or the
    // cover's glows, the orb's rings light); no cursor. A pinch then acts on it, wherever the hand is:
    // - a tile or a button presses (held while the pinch is, for HOLD TO RESET);
    // - the closed book's cover opens;
    // - a page on the stand is drawn by the hand's sweep across (MemoryBook.GazeGrab);
    // - the orb rolls from the point looked at, as if the fingers were there (Torch2Input.RemoteGrab).
    // Direct use wins: a pinch at a page's edge, at the orb or within Near of a button is the hand's own, not the look's.
    // Only a pinch's start counts, and nothing is taken while something is already held.
    [DefaultExecutionOrder(100)]
    public sealed class MemoryGaze : MonoBehaviour
    {
        public const float Reach=3,Near=.06f,PinchOn=MemoryBook.PinchOn,PinchOff=MemoryBook.PinchOff;
        public const string Permission="com.picovr.permission.EYE_TRACKING";
        public bool Available{get;private set;}
        public Ray Ray{get;private set;}
        MemoryGame game;
        MetaShadowPlayer xr;
        readonly List<InputDevice> devices=new List<InputDevice>();
        static readonly InputFeatureUsage<Vector3> GazePosition=new InputFeatureUsage<Vector3>("gazePosition");
        static readonly InputFeatureUsage<Quaternion> GazeRotation=new InputFeatureUsage<Quaternion>("gazeRotation");
        readonly bool[] pinching=new bool[2],seen=new bool[2];
        readonly Pressable[] pressing=new Pressable[2];
        readonly Collider[] nearby=new Collider[16];
        Pressable looked;
        bool logged;

        public static MemoryGaze Add(MemoryGame game,MetaShadowPlayer xr)
        {
            var gaze=game.gameObject.AddComponent<MemoryGaze>();gaze.game=game;gaze.xr=xr;
#if UNITY_ANDROID && !UNITY_EDITOR
            if(!UnityEngine.Android.Permission.HasUserAuthorizedPermission(Permission))UnityEngine.Android.Permission.RequestUserPermission(Permission);
#endif
            return gaze;
        }
        void Update()
        {
            bool was=Available;Available=ReadGaze(out var ray);Ray=ray;
            if(Available!=was||!logged){logged=true;Debug.Log("SHAPE_OF_MEMORY_GAZE "+(Available?"on":"off"));}
            var book=game.Book;var level=game.Level;
            // What the eyes are on.
            Pressable on=null;int page=-1;bool cover=false,orb=false;Vector3 orbPoint=default;
            if(Available)
            {
                if(Physics.Raycast(ray,out var hit,Reach)&&hit.collider.GetComponent<Pressable>() is Pressable p&&p.Available&&p!=book.CoverPress&&!IsEdge(book,p))on=p;
                if(!on&&book.OnStand)PageAt(book,ray,out page,out cover);
                if(!on&&page<0&&!cover&&level&&level.input.enabled&&!level.Completed)orb=OrbAt(level,ray,out orbPoint);
            }
            if(looked&&looked!=on)looked.GazeOn=false;looked=on;if(on)on.GazeOn=true;
            book.GazeSide=page;book.GazeCover=cover;
            if(orb&&!level.Busy)level.world.Hover=Torch2Input.Grip.Roll;
            // A pinch that starts away from anything the hand could take itself acts on what the eyes are on.
            for(int hand=0;hand<2;hand++)
            {
                if(!xr.ReadHand(hand==0,out _,out var tip,out float pinch)){pinching[hand]=seen[hand]=false;if(pressing[hand]){pressing[hand].GazePinch(false);pressing[hand]=null;}continue;}
                bool before=pinching[hand];pinching[hand]=pinch<(before?PinchOff:PinchOn);bool start=pinching[hand]&&!before&&seen[hand];seen[hand]=true;
                if(!pinching[hand]&&pressing[hand]){pressing[hand].GazePinch(false);pressing[hand]=null;}
                if(!start||!Available||Direct(book,level,hand,tip)||book.Dragging||level&&level.Busy)continue;
                if(on){if(on.GazePinch(true))pressing[hand]=on;}
                else if(cover){Debug.Log("SHAPE_OF_MEMORY_GAZE cover");book.OpenCover();}
                else if(page>=0)book.GazeGrab(page==1,hand,tip);
                else if(orb&&level.input.RemoteGrab(hand,orbPoint,tip))Debug.Log($"SHAPE_OF_MEMORY_GAZE orb hand={(hand==0?"left":"right")}");
            }
        }
        // Gaze from the runtime, in world space (its pose is in the tracking space, the camera's parent).
        bool ReadGaze(out Ray ray)
        {
            ray=default;
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.EyeTracking,devices);
            foreach(var device in devices)
            {
                if(!device.isValid)continue;
                if(device.TryGetFeatureValue(CommonUsages.isTracked,out bool tracked)&&!tracked)continue;
                if(!(device.TryGetFeatureValue(GazePosition,out var position)&&device.TryGetFeatureValue(GazeRotation,out var rotation))
                    &&!(device.TryGetFeatureValue(CommonUsages.devicePosition,out position)&&device.TryGetFeatureValue(CommonUsages.deviceRotation,out rotation)))continue;
                var space=game.Player.ViewCamera.transform.parent;
                ray=space?new Ray(space.TransformPoint(position),space.TransformDirection(rotation*Vector3.forward)):new Ray(position,rotation*Vector3.forward);
                return true;
            }
            return false;
        }
        // The hand at a page's edge or the cover, at the orb, or right by a button: its pinch is its own.
        bool Direct(MemoryBook book,Torch2Workshop level,int hand,Vector3 tip)
        {
            if(book.HandAt(hand==0))return true;
            if(level&&level.input.enabled&&level.input.Near(tip))return true;
            int count=Physics.OverlapSphereNonAlloc(tip,Near,nearby);
            for(int i=0;i<count;i++)if(nearby[i]&&nearby[i].GetComponent<Pressable>())return true;
            return false;
        }
        static bool IsEdge(MemoryBook book,Pressable p)=>p==book.Corner(true)||p==book.Corner(false);
        // Which page of the open book on its stand the look is on (0 left, 1 right), or its closed cover.
        static void PageAt(MemoryBook book,Ray ray,out int page,out bool cover)
        {
            page=-1;cover=false;var t=book.transform;
            var o=t.InverseTransformPoint(ray.origin);var d=t.InverseTransformPoint(ray.origin+ray.direction)-o;
            if(Mathf.Abs(d.y)<1e-5f)return;float along=-o.y/d.y;if(along<=0)return;
            var p=o+d*along;float w=MemoryBook.Width,h=MemoryBook.Height;
            if(p.z<0||p.z>h||p.x<-w||p.x>w)return;
            if(book.Closed){cover=p.x>=0;return;}
            page=p.x>=0?1:0;
        }
        static bool OrbAt(Torch2Workshop level,Ray ray,out Vector3 point)
        {
            var centre=level.transform.TransformPoint(level.Position);float r=level.input.Radius*level.transform.lossyScale.x;
            var to=centre-ray.origin;float along=Vector3.Dot(to,ray.direction);var closest=ray.origin+ray.direction*along;
            float off=(closest-centre).magnitude;point=default;
            if(along<=0||off>r)return false;
            point=closest-ray.direction*Mathf.Sqrt(r*r-off*off);return true;
        }
        void OnDisable(){if(looked)looked.GazeOn=false;}
    }
}
