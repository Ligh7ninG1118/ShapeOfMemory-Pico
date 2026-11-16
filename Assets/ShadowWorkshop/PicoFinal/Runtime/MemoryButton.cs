using System;
using TMPro;
using UnityEngine;

namespace ShapeOfMemory
{
    // A push button on the toolbar by the hand, in the page tiles' look (MemoryTile): a square Leather cap with a gold icon
    // that sinks while pressed, in a square collar that is its rim (Faint at rest, aqua Hover with a finger near, Grab
    // pressed); its name beside it on the panel (to the cap's +x), so the hold ring round the cap and the pips under the
    // name each have their own room (the name under the cap ran into HOLD TO RESET's ring). Optional pips under the name
    // (hints left, gold) and, on a hold button, a square ring of dots round the cap that fills Ember while it is held.
    // Unlit, so the colours are the ones seen.
    public sealed class MemoryButton : MonoBehaviour
    {
        public const float Cap=.058f,Height=.014f,Travel=.007f,Ring=.010f,LabelGap=.016f,LabelWidth=.105f;
        // A hold's dots stand DotOut out from the cap's edge: just outside the collar (Cap+.012 wide) and a little above its
        // top, so the two never share a face (they z-fought, 2026-11-15).
        public const float DotOut=.0105f;
        // From the cap's middle to the far end of its name (the panel is laid out round it).
        public const float Reach=Cap*.5f+Ring+LabelGap+LabelWidth;
        public Pressable Press{get;private set;}
        public string Caption{get=>label.text;set{label.text=value;MemoryText.Apply(label);}}
        public int PipsLit;
        Transform cap;
        Renderer collar,body,icon;
        TMP_Text label;
        Renderer[] dots=Array.Empty<Renderer>(),pips=Array.Empty<Renderer>();
        float sink;
        bool below;

        public static MemoryButton Create(Transform parent,string name,string caption,Texture2D iconTexture,Vector3 position,MemoryKit kit,
            Action action,Func<bool> enabled,Func<bool> handsAllowed,float hold=0,bool below=false)
        {
            var root=new GameObject("Button "+name).transform;root.SetParent(parent,false);root.localPosition=position;
            var b=root.gameObject.AddComponent<MemoryButton>();
            b.collar=MemoryUi.Shape(PrimitiveType.Cube,"Collar",root,new Vector3(0,.0015f,0),Quaternion.identity,new Vector3(Cap+.012f,.003f,Cap+.012f),kit.Screen,MemoryPalette.Faint).GetComponent<Renderer>();
            b.cap=new GameObject("Cap").transform;b.cap.SetParent(root,false);
            b.body=MemoryUi.Shape(PrimitiveType.Cube,"Cap body",b.cap,new Vector3(0,Height*.5f,0),Quaternion.identity,new Vector3(Cap,Height,Cap),kit.Screen,MemoryPalette.Leather).GetComponent<Renderer>();
            if(iconTexture)b.icon=MemoryUi.Picture("Icon",b.cap,new Vector3(0,Height+.0006f,0),MemoryUi.FaceUp,Vector2.one*Cap*.58f,kit.Icon,iconTexture,MemoryPalette.Gold,kit.Owned);
            float labelLeft=Cap*.5f+Ring+LabelGap;
            // below: the name under the cap, past the hold ring (a row of buttons); else beside it.
            b.below=below;
            b.label=below?MemoryUi.Label(caption,root,new Vector3(0,.0012f,-(Cap*.5f+DotOut+.0145f)),MemoryUi.FaceUp,.0115f,new Vector2(.118f,.02f),MemoryPalette.Haze):
                MemoryUi.Label(caption,root,new Vector3(labelLeft+LabelWidth*.5f,.0012f,.003f),MemoryUi.FaceUp,.0125f,new Vector2(LabelWidth,.03f),MemoryPalette.Haze,TextAlignmentOptions.MidlineLeft);
            // Things you press are named in the text face's Bold, on one line: a long name shrinks rather than wrapping.
            b.label.fontStyle=FontStyles.Bold;b.label.textWrappingMode=TextWrappingModes.NoWrap;
            b.label.enableAutoSizing=true;b.label.fontSizeMax=b.label.fontSize;b.label.fontSizeMin=b.label.fontSize*.6f;
            // The pressable face is the cap's top at rest, so a poke selects as the finger meets it.
            var hit=new GameObject("Press",typeof(BoxCollider));hit.transform.SetParent(root,false);
            hit.transform.localPosition=new Vector3(0,Height*.5f,0);hit.transform.localScale=new Vector3(Cap+.01f,Height,Cap+.01f);
            b.Press=Pressable.Add(hit,action,enabled,handsAllowed,new Vector3(0,.51f,0),MemoryUi.FaceUp,hold);
            if(hold>0)
            {
                // Round the cap's square, clockwise from twelve o'clock as seen from above.
                b.dots=new Renderer[20];float a=Cap*.5f+DotOut,perimeter=8*a;
                for(int i=0;i<b.dots.Length;i++)
                {
                    float s=(i*perimeter/b.dots.Length+a)%perimeter;Vector2 p;
                    if(s<2*a)p=new Vector2(-a+s,a);else if(s<4*a)p=new Vector2(a,a-(s-2*a));else if(s<6*a)p=new Vector2(a-(s-4*a),-a);else p=new Vector2(-a,-a+(s-6*a));
                    b.dots[i]=MemoryUi.Shape(PrimitiveType.Cylinder,"Hold dot",root,new Vector3(p.x,.002f,p.y),Quaternion.identity,new Vector3(.0055f,.002f,.0055f),kit.Screen,MemoryPalette.Ink600).GetComponent<Renderer>();
                }
            }
            return b;
        }
        // A row of small dots under the name; PipsLit of them glow.
        public void Pips(int count)
        {
            float left=Cap*.5f+Ring+LabelGap+.004f;
            pips=new Renderer[count];
            // In a row, above the cap; beside it, under the name.
            for(int i=0;i<count;i++)pips[i]=MemoryUi.Shape(PrimitiveType.Cylinder,"Pip",transform,below?new Vector3((i-(count-1)*.5f)*.014f,.0015f,Cap*.5f+DotOut+.004f):new Vector3(left+i*.014f,.0015f,-.016f),Quaternion.identity,new Vector3(.007f,.0015f,.007f),body.sharedMaterial,MemoryPalette.Ink500).GetComponent<Renderer>();
            if(!below)label.transform.localPosition+=new Vector3(0,0,.006f);
        }
        void Update()
        {
            bool available=Press.Available,down=available&&Press.Down,hover=available&&Press.Hover;
            sink=Mathf.MoveTowards(sink,down?Travel:0,Time.unscaledDeltaTime*.15f);cap.localPosition=new Vector3(0,-sink,0);
            // The face stays Leather; the collar shows the state, and a button that can't be pressed now goes quiet.
            MemoryUi.Tint(collar,!available?MemoryPalette.Ink500:down?MemoryPalette.AquaGrab:hover?MemoryPalette.AquaHover:MemoryPalette.Faint);
            if(icon)MemoryUi.Tint(icon,available?MemoryPalette.Gold:MemoryPalette.Faint);
            label.color=available?MemoryPalette.Moonlight:MemoryPalette.Faint;
            int lit=Mathf.FloorToInt(Press.Held*dots.Length+.001f);
            for(int i=0;i<dots.Length;i++)MemoryUi.Tint(dots[i],i<lit?MemoryPalette.Ember:MemoryPalette.Ink600);
            for(int i=0;i<pips.Length;i++)MemoryUi.Tint(pips[i],i<PipsLit?MemoryPalette.Gold:MemoryPalette.Ink500);
        }
    }
}
