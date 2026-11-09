using CloudGarden.Integration;
using UnityEngine;

namespace ShapeOfMemory
{
    // Pressing with a fingertip, from the same hand joints the book and the orb read (MetaShadowPlayer.ReadHand), in place
    // of the rig's own poke interactors (MetaShadowPlayer.NoHandPokes). On the PICO 4 Ultra the SDK's poke, after a
    // one-frame drop of its hand data, found nothing to hover for the rest of the session (2026-11-07 logs, both
    // sessions), while these joints never lost the hand.
    // Per hand: the nearest live Pressable whose face the index tip is in front of (within HoverDepth, over the face or
    // within Margin of it) is hovered; the tip coming from in front to within Radius of the face presses it, once, held
    // while the tip stays in (HOLD TO RESET); backing out Release past that, or sliding off the face, lets go. A tip
    // that jumps further than MaxJump in a frame (tracking) never presses. Device log: SHAPE_OF_MEMORY_POINTER ... touch.
    [DefaultExecutionOrder(50)]
    public sealed class MemoryTouch : MonoBehaviour
    {
        public const float HoverDepth=.08f,Radius=.006f,Release=.015f,Margin=.008f,MaxJump=.08f;
        MetaShadowPlayer xr;
        sealed class Finger{public Pressable hover,down;public float depth=float.PositiveInfinity,closest=float.PositiveInfinity,beside;public Vector3 last;public bool seen;}
        readonly Finger[] fingers={new Finger(),new Finger()};

        public static MemoryTouch Add(GameObject host,MetaShadowPlayer xr){var touch=host.AddComponent<MemoryTouch>();touch.xr=xr;return touch;}
        static string Side(int hand)=>hand==0?"L":"R";
        void Update()
        {
            if(!xr||!xr.Active)return;
            for(int hand=0;hand<2;hand++){bool tracked=xr.ReadHand(hand==0,out _,out var tip,out _);Step(hand,tracked,tip);}
        }
        // One hand's fingertip this frame (tests drive it directly).
        public void Step(int hand,bool tracked,Vector3 tip)
        {
            {
                var f=fingers[hand];
                if(!tracked){Let(f,hand,"hand lost");Leave(f,hand);f.seen=false;return;}
                bool jump=f.seen&&(tip-f.last).sqrMagnitude>MaxJump*MaxJump;f.last=tip;f.seen=true;
                if(f.down)
                {
                    if(!f.down.isActiveAndEnabled){Let(f,hand,"gone");return;}
                    Depth(f.down,tip,out float held,out float aside);
                    if(held>Radius+Release||aside>Margin*2)Let(f,hand,null);
                    return;
                }
                // The nearest face the tip is in front of (or just through, if it was hovering it).
                Pressable best=null;float bestDepth=HoverDepth,bestAside=0;
                foreach(var p in Pressable.All)
                {
                    if(!p||!p.isActiveAndEnabled||p.Native==null||!p.Native.Poke)continue;
                    Depth(p,tip,out float d,out float off);
                    if(off>Margin||d>bestDepth)continue;
                    if(d<-Release&&p!=f.hover)continue;
                    best=p;bestDepth=d;bestAside=off;
                }
                bool fresh=best!=f.hover;
                if(fresh){Leave(f,hand);if(best){f.hover=best;best.TouchOn=true;f.closest=float.PositiveInfinity;Debug.Log($"SHAPE_OF_MEMORY_POINTER {best.Name} touch hover by={Side(hand)}");}}
                if(!best){f.depth=float.PositiveInfinity;return;}
                if(bestDepth<f.closest){f.closest=bestDepth;f.beside=bestAside;}
                // Through the face from in front of it (seen there last frame, not arriving from the side): a press.
                if(!fresh&&f.depth>Radius&&bestDepth<=Radius&&!jump){if(best.Touch(true,Side(hand)))f.down=best;f.closest=float.PositiveInfinity;}
                f.depth=bestDepth;
            }
        }
        // Signed distance (metres) from the face out to the tip, less Radius's worth of fingertip (negative: through it), and
        // how far beside the face's rectangle the tip is.
        static void Depth(Pressable p,Vector3 tip,out float depth,out float aside)
        {
            var face=p.Face;depth=Vector3.Dot(tip-face.position,-face.forward);
            var local=face.InverseTransformPoint(tip);
            var over=face.TransformPoint(new Vector3(local.x,local.y,0));var on=face.TransformPoint(new Vector3(Mathf.Clamp(local.x,-.5f,.5f),Mathf.Clamp(local.y,-.5f,.5f),0));
            aside=Vector3.Distance(over,on);
        }
        void Let(Finger f,int hand,string why)
        {
            if(!f.down)return;
            if(f.down)f.down.Touch(false,Side(hand));
            Debug.Log($"SHAPE_OF_MEMORY_POINTER {(f.down?f.down.Name:"?")} touch unselect by={Side(hand)}"+(why!=null?" ("+why+")":""));
            f.down=null;f.depth=float.NegativeInfinity;
        }
        void Leave(Finger f,int hand)
        {
            if(!f.hover)return;
            f.hover.TouchOn=false;
            string near=float.IsPositiveInfinity(f.closest)?"":$" closest={f.closest*1000:+0;-0} mm"+(f.beside>.003f?$" beside={f.beside*1000:0} mm":"");
            Debug.Log($"SHAPE_OF_MEMORY_POINTER {f.hover.Name} touch unhover by={Side(hand)}{near}");
            f.hover=null;f.depth=float.PositiveInfinity;
        }
        void OnDisable(){for(int hand=0;hand<2;hand++){Let(fingers[hand],hand,"off");Leave(fingers[hand],hand);}}
    }
}
