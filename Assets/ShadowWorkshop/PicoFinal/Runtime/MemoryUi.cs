using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShapeOfMemory
{
    // Materials the game's physical UI is built from, and what it owns (runtime materials and textures).
    public sealed class MemoryKit
    {
        // SeeThrough: URP Unlit, alpha blended (the see-through strokes and the dark before the title).
        public Material Surface,Screen,Icon,SeeThrough;
        public readonly List<Object> Owned=new List<Object>();
        public void Release(){foreach(var item in Owned)if(item)Object.Destroy(item);Owned.Clear();stroke=hull=flat=null;}
        // See-through light strokes (the orb's rings, the pages' edges) and their dark rims, drawn after the shadows so a
        // ring over a shadowed wall shows the shadow through it. The stroke writes depth, so its own rim, drawn after it,
        // shows only round it (behind the stroke it is hidden, not seen through it). Hull: a ring's inverted-hull rim (back
        // faces only); Flat: a quad's rim. Without SeeThrough, Screen (opaque).
        public Material Stroke=>stroke?stroke:stroke=Clear("Memory stroke",3100,true,CullMode.Back);
        public Material Hull=>hull?hull:hull=Clear("Memory stroke hull",3101,false,CullMode.Front);
        public Material Flat=>flat?flat:flat=Clear("Memory stroke rim",3101,false,CullMode.Back);
        Material stroke,hull,flat;
        Material Clear(string name,int queue,bool depth,CullMode cull)
        {
            var m=new Material(SeeThrough?SeeThrough:Screen){name=name};m.SetFloat("_Cull",(float)cull);
            if(SeeThrough){m.SetFloat("_ZWrite",depth?1:0);m.renderQueue=queue;}
            Owned.Add(m);return m;
        }
    }

    // Things brought up out of the dark: each renderer's _BaseColor and each label's colour times a level (0 black, 1 its
    // own), alpha kept. Black against the dark before the title is unseen, so coming up from 0 reads as appearing.
    // Takes the colours as they are when added (renderers switched off then are left alone); Set(1) puts them back.
    public sealed class MemoryShade
    {
        static readonly int BaseColor=Shader.PropertyToID("_BaseColor");
        readonly List<(Renderer renderer,Color color)> renderers=new List<(Renderer,Color)>();
        readonly List<(TMP_Text label,Color color)> labels=new List<(TMP_Text,Color)>();
        readonly MaterialPropertyBlock block=new MaterialPropertyBlock();
        public MemoryShade Add(Transform root,System.Func<Transform,bool> skip=null)
        {
            if(!root)return this;
            foreach(var label in root.GetComponentsInChildren<TMP_Text>())if(skip==null||!skip(label.transform))labels.Add((label,label.color));
            foreach(var r in root.GetComponentsInChildren<Renderer>())
            {
                if(!r.enabled||r.GetComponent<TMP_Text>()||!r.sharedMaterial||!r.sharedMaterial.HasProperty(BaseColor)||skip!=null&&skip(r.transform))continue;
                r.GetPropertyBlock(block);renderers.Add((r,block.HasColor(BaseColor)?block.GetColor(BaseColor):r.sharedMaterial.GetColor(BaseColor)));
            }
            return this;
        }
        public void Set(float level)
        {
            level=Mathf.Clamp01(level);
            foreach(var (r,c) in renderers)if(r){r.GetPropertyBlock(block);block.SetColor(BaseColor,new Color(c.r*level,c.g*level,c.b*level,c.a));r.SetPropertyBlock(block);}
            foreach(var (label,c) in labels)if(label)label.color=new Color(c.r*level,c.g*level,c.b*level,c.a);
        }
    }

    // Builders for the console, cards and labels; their colours are MemoryPalette's. Flat things face up with
    // Quaternion.Euler(90,0,0) and read with their top away from the player.
    public static class MemoryUi
    {
        public static readonly Quaternion FaceUp=Quaternion.Euler(90,0,0);
        static MaterialPropertyBlock block;

        // A light stroke's dark rim drawn as an inverted hull: a copy of the unlit material, Ink800, showing only the back
        // faces of a slightly larger copy of the stroke, so just its outline shows past the stroke. Opaque, no new shader.
        public static Material Rim(Material unlit,List<Object> owned)
        {
            var m=new Material(unlit){name="Memory rim"};m.SetFloat("_Cull",(float)CullMode.Front);m.SetColor("_BaseColor",MemoryPalette.Ink800);owned.Add(m);return m;
        }
        public static void Tint(Renderer r,Color color){block??=new MaterialPropertyBlock();r.GetPropertyBlock(block);block.SetColor("_BaseColor",color);r.SetPropertyBlock(block);}
        public static GameObject Shape(PrimitiveType type,string name,Transform parent,Vector3 position,Quaternion rotation,Vector3 scale,Material material,Color color)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;Kill(go.GetComponent<Collider>());
            go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localRotation=rotation;go.transform.localScale=scale;
            var r=go.GetComponent<Renderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;Tint(r,color);return go;
        }
        // A quad readable from -Z of its rotation, showing texture through an instance of material (tinted by color).
        public static Renderer Picture(string name,Transform parent,Vector3 position,Quaternion rotation,Vector2 size,Material material,Texture texture,Color color,List<Object> owned)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Quad);go.name=name;Kill(go.GetComponent<Collider>());
            go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localRotation=rotation;go.transform.localScale=new Vector3(size.x,size.y,1);
            var m=new Material(material){name=name};m.SetTexture("_BaseMap",texture);m.SetColor("_BaseColor",Color.white);owned.Add(m);
            var r=go.GetComponent<Renderer>();r.sharedMaterial=m;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;Tint(r,color);return r;
        }
        public static TMP_Text Label(string text,Transform parent,Vector3 position,Quaternion rotation,float size,Vector2 bounds,Color color,TextAlignmentOptions align=TextAlignmentOptions.Center)
        {
            var go=new GameObject("Label",typeof(TextMeshPro));go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localRotation=rotation;
            var label=go.GetComponent<TextMeshPro>();label.text=text;label.fontSize=size*10;label.color=color;label.alignment=align;
            label.rectTransform.sizeDelta=bounds;label.textWrappingMode=TextWrappingModes.Normal;return label;
        }
        // A short dry tick for every press.
        public static AudioClip Click(List<Object> owned)
        {
            var samples=new float[2200];var random=new System.Random(7);
            for(int i=0;i<samples.Length;i++){float t=i/44100f;samples[i]=((float)random.NextDouble()*2-1)*.30f*Mathf.Exp(-t*700)+Mathf.Sin(t*Mathf.PI*2*1650)*.40f*Mathf.Exp(-t*220);}
            var clip=AudioClip.Create("Memory click",samples.Length,1,44100,false);clip.SetData(samples,0);owned.Add(clip);return clip;
        }
        public static void Kill(Object o){if(o)Object.Destroy(o);}
    }
}
