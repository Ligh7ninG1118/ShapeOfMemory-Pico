using System;
using CloudGarden.ShadowPuzzle;
using Oculus.Interaction;
using PhysicalAssembly.MetaXR;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShapeOfMemory
{
    // One press path for hands and mouse. A fingertip presses the face (MemoryTouch, from the game's own hand joints), a
    // look and a pinch does (MemoryGaze), and the rig's ray and poke interactors still can (MetaMenuButton; Shape of
    // Memory turns the hand ones off on a headset, the tests drive them); the desktop clicks it (ShadowControl, which
    // Torch2Input also honours during play). With HoldSeconds set the action fires only after a hold that long, so a
    // brushing hand cannot reset a level. Actions run on the next Update, outside the interactors' event dispatch,
    // because most of them swap the visible buttons.
    // Device log: every poke and ray event on it (SHAPE_OF_MEMORY_POINTER, named Label, by the interactor's name from
    // MemoryTrace), and on a select whether it would press or, if not, why (Why, from the owner).
    public sealed class Pressable : MonoBehaviour
    {
        public float HoldSeconds;
        // The live ones (MemoryTouch presses them with a fingertip).
        public static readonly System.Collections.Generic.List<Pressable> All=new System.Collections.Generic.List<Pressable>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetAll()=>All.Clear();
        // A ray over it, a fingertip in front of it (TouchOn, MemoryTouch) or a look on it (GazeOn, MemoryGaze).
        public bool Hover{get=>hover||GazeOn||TouchOn;private set=>hover=value;}
        public bool GazeOn,TouchOn;
        public bool Down=>hands||mouse||gazeDown||touchDown;
        bool hover,gazeDown,touchDown;
        // The face hands press: the interactables' surface (its plane; -forward points out of the face; the face spans
        // -.5..+.5 across in its own space).
        public Transform Face=>Native.Poke.transform;
        // A fingertip pressing (MemoryTouch): presses as a poke does, held while the tip stays in. Returns whether it took.
        public bool Touch(bool down,string by)
        {
            if(!down){touchDown=false;return false;}
            string why=Refusal();Debug.Log($"SHAPE_OF_MEMORY_POINTER {Name} touch select by={by}"+(why==null?" ok":" refused="+why));
            if(why!=null)return false;
            touchDown=true;Begin();return true;
        }
        // A look and a pinch: presses as a poke does, held while the pinch is (for a hold). Returns whether it took.
        public bool GazePinch(bool down)
        {
            if(!down){gazeDown=false;return false;}
            string why=Refusal();Debug.Log($"SHAPE_OF_MEMORY_POINTER {Name} gaze select"+(why==null?" ok":" refused="+why));
            if(why!=null)return false;
            gazeDown=true;Begin();return true;
        }
        public bool Available=>control&&control.Available;
        // 0..1 while a hold is under way.
        public float Held=>holding&&HoldSeconds>0?Mathf.Clamp01(held/HoldSeconds):0;
        public ShadowControl Control=>control;
        public MetaMenuButton Native{get;private set;}
        // Its name in the device log ("title/play", "play/hint", "book/forward").
        public string Label;
        // Why a press would be refused now (null: it would not be), for the device log.
        public Func<string> Why;
        public string Name=>Label??(transform.parent?transform.parent.name:name);
        ShadowControl control;
        Action action;
        Func<bool> handsAllowed;
        bool hands,mouse,holding,pending;
        float held;
        // A poke hover being measured for the log: how near the fingertip came to the face (metres, minus its radius:
        // negative = through it) and, at that moment, how far beside the face it was.
        PokeInteractor poking;
        int pokeId;
        bool pokeSelected;
        float closest,beside;

        // go: a unit cube scaled to the pressable part. face, facing: where hands press, in that cube's space (normal -Z).
        public static Pressable Add(GameObject go,Action action,Func<bool> enabled,Func<bool> handsAllowed,Vector3 face,Quaternion facing,float hold=0)
        {
            var p=go.AddComponent<Pressable>();p.action=action;p.HoldSeconds=hold;p.handsAllowed=handsAllowed;
            p.control=go.AddComponent<ShadowControl>();p.control.action=p.MousePress;p.control.enabledWhen=enabled;
            p.Native=go.AddComponent<MetaMenuButton>();
            p.Native.Initialize(p.Begin,()=>p.Available&&(handsAllowed==null||handsAllowed()),(hover,selected)=>{p.Hover=hover;p.hands=selected;},face,facing);
            // After MetaMenuButton's own handlers, so a select is logged with the decision it just made.
            p.Native.Ray.WhenPointerEventRaised+=e=>p.Trace("ray",e);p.Native.Poke.WhenPointerEventRaised+=e=>p.Trace("poke",e);
            return p;
        }
        // Why a hand's press would be refused now, or null.
        public string Refusal()
        {
            if(!Available)return Why?.Invoke()??"unavailable";
            if(handsAllowed!=null&&!handsAllowed())return Why?.Invoke()??"hands not allowed";
            return null;
        }
        void Trace(string via,PointerEvent e)
        {
            string what=e.Type switch{PointerEventType.Hover=>"hover",PointerEventType.Unhover=>"unhover",PointerEventType.Select=>"select",PointerEventType.Unselect=>"unselect",PointerEventType.Cancel=>"cancel",_=>null};
            if(what==null)return;
            string line=$"SHAPE_OF_MEMORY_POINTER {Name} {via} {what} by={MemoryTrace.Describe(e.Identifier)}";
            if(e.Type==PointerEventType.Select){string why=Refusal();line+=why==null?" ok":" refused="+why;}
            // A poke that hovered and never selected: did the finger stop short of the face, or come down beside it?
            if(via=="poke")
            {
                if(e.Type==PointerEventType.Hover){poking=MemoryTrace.Poke(e.Identifier);pokeId=e.Identifier;pokeSelected=false;closest=float.PositiveInfinity;beside=0;}
                else if(e.Type==PointerEventType.Select&&e.Identifier==pokeId)pokeSelected=true;
                else if((e.Type==PointerEventType.Unhover||e.Type==PointerEventType.Cancel)&&e.Identifier==pokeId)
                {
                    if(!pokeSelected&&!float.IsPositiveInfinity(closest))line+=$" closest={closest*1000:+0;-0} mm"+(beside>.003f?$" beside={beside*1000:0} mm":"");
                    if(e.Type==PointerEventType.Unhover)poking=null;
                }
            }
            Debug.Log(line);
        }
        void Measure()
        {
            var face=Native.Poke.transform;var tip=poking.Origin;
            float gap=Vector3.Dot(tip-face.position,-face.forward)-poking.Radius;
            if(gap>=closest)return;
            closest=gap;var local=face.InverseTransformPoint(tip);
            var on=face.TransformPoint(new Vector3(Mathf.Clamp(local.x,-.5f,.5f),Mathf.Clamp(local.y,-.5f,.5f),0));
            beside=Vector3.Distance(face.TransformPoint(new Vector3(local.x,local.y,0)),on);
        }
        void MousePress(){mouse=true;Begin();}
        void Begin(){if(HoldSeconds>0){holding=true;held=0;}else pending=true;}
        void Update()
        {
            if(poking&&poking.isActiveAndEnabled)Measure();
            if(mouse&&(Mouse.current==null||!Mouse.current.leftButton.isPressed))mouse=false;
            if(pending)
            {
                pending=false;
                if(Available)action();else Debug.Log($"SHAPE_OF_MEMORY_POINTER {Name} dropped refused={Why?.Invoke()??"unavailable"}");
                return;
            }
            if(!holding)return;
            if(!Down||!Available){holding=false;Debug.Log($"SHAPE_OF_MEMORY_POINTER {Name} hold let go at {held:0.00}s"+(Available?"":" refused="+(Why?.Invoke()??"unavailable")));return;}
            held+=Time.unscaledDeltaTime;if(held>=HoldSeconds){holding=false;action();}
        }
        void OnEnable(){if(!All.Contains(this))All.Add(this);}
        void OnDisable(){All.Remove(this);hands=mouse=holding=pending=gazeDown=GazeOn=touchDown=TouchOn=false;Hover=false;poking=null;}
    }
}
