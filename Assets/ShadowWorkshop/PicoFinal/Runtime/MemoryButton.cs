using System;
using TMPro;
using UnityEngine;

namespace ShapeOfMemory
{
    // A round push button on the console: an Ink600 cap that sinks while pressed, with a Moonlight icon, in a collar that
    // is its rim (Faint at rest, aqua Hover with a finger near, Grab pressed); its name (Haze) on the console in front.
    // Optional pips behind the cap (hints left, gold) and, on a hold button, a ring of dots that fills Ember while it is
    // held. Unlit, so the colours are the ones seen.
    public sealed class MemoryButton : MonoBehaviour
    {
        public const float Cap=.064f,Height=.016f,Travel=.007f;
        public Pressable Press{get;private set;}
        public string Caption{get=>label.text;set=>label.text=value;}
        public int PipsLit;
        Transform cap;
        Renderer collar,body,icon;
        TMP_Text label;
        Renderer[] dots=Array.Empty<Renderer>(),pips=Array.Empty<Renderer>();
        float sink;

        public static MemoryButton Create(Transform parent,string name,string caption,Texture2D iconTexture,Vector3 position,MemoryKit kit,
            Action action,Func<bool> enabled,Func<bool> handsAllowed,float hold=0)
        {
            var root=new GameObject("Button "+name).transform;root.SetParent(parent,false);root.localPosition=position;
            var b=root.gameObject.AddComponent<MemoryButton>();
            b.collar=MemoryUi.Shape(PrimitiveType.Cylinder,"Collar",root,new Vector3(0,.003f,0),Quaternion.identity,new Vector3(Cap+.014f,.003f,Cap+.014f),kit.Screen,MemoryPalette.Faint).GetComponent<Renderer>();
            b.cap=new GameObject("Cap").transform;b.cap.SetParent(root,false);
            b.body=MemoryUi.Shape(PrimitiveType.Cylinder,"Cap body",b.cap,new Vector3(0,Height*.5f,0),Quaternion.identity,new Vector3(Cap,Height*.5f,Cap),kit.Screen,MemoryPalette.Ink600).GetComponent<Renderer>();
            if(iconTexture)b.icon=MemoryUi.Picture("Icon",b.cap,new Vector3(0,Height+.0006f,0),MemoryUi.FaceUp,Vector2.one*Cap*.6f,kit.Icon,iconTexture,MemoryPalette.Moonlight,kit.Owned);
            b.label=MemoryUi.Label(caption,root,new Vector3(0,.0012f,-(Cap*.5f+.017f)),MemoryUi.FaceUp,.0125f,new Vector2(.10f,.02f),MemoryPalette.Haze);
            // The pressable face is the cap's top at rest, so a poke selects as the finger meets the paper.
            var hit=new GameObject("Press",typeof(BoxCollider));hit.transform.SetParent(root,false);
            hit.transform.localPosition=new Vector3(0,Height*.5f,0);hit.transform.localScale=new Vector3(Cap+.01f,Height,Cap+.01f);
            b.Press=Pressable.Add(hit,action,enabled,handsAllowed,new Vector3(0,.51f,0),MemoryUi.FaceUp,hold);
            if(hold>0)
            {
                b.dots=new Renderer[16];float r=Cap*.5f+.013f;
                for(int i=0;i<b.dots.Length;i++)
                {
                    // Clockwise from twelve o'clock as seen from above.
                    float a=i*Mathf.PI*2/b.dots.Length;
                    b.dots[i]=MemoryUi.Shape(PrimitiveType.Cylinder,"Hold dot",root,new Vector3(Mathf.Sin(a)*r,.0015f,Mathf.Cos(a)*r),Quaternion.identity,new Vector3(.006f,.0015f,.006f),kit.Screen,MemoryPalette.Ink600).GetComponent<Renderer>();
                }
            }
            return b;
        }
        // A row of small dots behind the cap; PipsLit of them glow.
        public void Pips(int count)
        {
            pips=new Renderer[count];
            for(int i=0;i<count;i++)pips[i]=MemoryUi.Shape(PrimitiveType.Cylinder,"Pip",transform,new Vector3((i-(count-1)*.5f)*.014f,.0015f,Cap*.5f+.012f),Quaternion.identity,new Vector3(.007f,.0015f,.007f),body.sharedMaterial,MemoryPalette.Ink500).GetComponent<Renderer>();
        }
        void Update()
        {
            bool available=Press.Available,down=available&&Press.Down,hover=available&&Press.Hover;
            sink=Mathf.MoveTowards(sink,down?Travel:0,Time.unscaledDeltaTime*.15f);cap.localPosition=new Vector3(0,-sink,0);
            // The face stays Ink600; the collar shows the state, and a button that can't be pressed now goes quiet.
            MemoryUi.Tint(collar,!available?MemoryPalette.Ink500:down?MemoryPalette.AquaGrab:hover?MemoryPalette.AquaHover:MemoryPalette.Faint);
            if(icon)MemoryUi.Tint(icon,available?MemoryPalette.Moonlight:MemoryPalette.Faint);
            label.color=available?MemoryPalette.Haze:MemoryPalette.Faint;
            int lit=Mathf.FloorToInt(Press.Held*dots.Length+.001f);
            for(int i=0;i<dots.Length;i++)MemoryUi.Tint(dots[i],i<lit?MemoryPalette.Ember:MemoryPalette.Ink600);
            for(int i=0;i<pips.Length;i++)MemoryUi.Tint(pips[i],i<PipsLit?MemoryPalette.Gold:MemoryPalette.Ink500);
        }
    }
}
