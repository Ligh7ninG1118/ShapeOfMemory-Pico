using System.Collections.Generic;
using PhysicalAssembly.MetaXR;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShapeOfMemory
{
    // A baked COZY sky (Art/Sky/*.png on Shaders/SkyboxDithered; MemoryGame.SkyMaterial picks which).
    // Until the book is first opened the player sees the room (passthrough) or the desktop's plain background; Show()
    // then fades the sky in over FadeSeconds and, once it covers everything, turns passthrough off (it costs GPU time
    // behind an opaque sky). Drawn on a dome that follows the eyes rather than as the camera's skybox, so it can fade
    // over the clear eye buffer through the shader's _Alpha (the camera keeps clearing to transparent black). The dome
    // turns with the stage: the bake's pink glow (glowAzimuth, the bake's own at _Rotation 0: WindwardEveningSky 50,
    // MagicHourSky 120) sits Facing degrees right of the player's forward when the stage was placed, beyond the book.
    // Dark (the title's): a second dome, black at that alpha, drawn over the sky and under everything else (the room
    // hidden at 1).
    public sealed class MemorySky : MonoBehaviour
    {
        public const float FadeSeconds=2.5f,Radius=400,Facing=0;
        public float Alpha{get;private set;}
        public bool Shown=>time>=0;
        // 0 none .. 1 black over the room (and the sky).
        public float Dark{get=>dark;set=>dark=Mathf.Clamp01(value);}
        Material material,veil;
        Renderer dome,shade;
        Camera view;
        Transform stage;
        float time=-1,glowAzimuth,dark;
        bool passthroughOff;
        static readonly int AlphaId=Shader.PropertyToID("_Alpha"),BaseColor=Shader.PropertyToID("_BaseColor");

        // seeThrough: URP Unlit, alpha blended (MemoryGame.SeeThrough), for the dark; without it there is none.
        public static MemorySky Add(GameObject host,Material sky,Material seeThrough,float glowAzimuth,Camera view,Transform stage,List<Object> owned)
        {
            var it=host.AddComponent<MemorySky>();it.view=view;it.stage=stage;it.glowAzimuth=glowAzimuth;
            if(seeThrough)
            {
                // Right after the sky (Background), before anything else.
                it.veil=new Material(seeThrough){name="Memory dark",renderQueue=(int)RenderQueue.Background+1};it.veil.SetFloat("_Cull",(float)CullMode.Off);
                it.veil.SetColor(BaseColor,new Color(0,0,0,0));owned.Add(it.veil);it.shade=Dome(host,"Dark dome",it.veil);
            }
            if(!sky){Debug.LogWarning("SHAPE_OF_MEMORY_SKY no sky material: the room stays");return it;}
            it.material=new Material(sky){name="Memory sky"};it.material.SetFloat(AlphaId,0);owned.Add(it.material);
            it.dome=Dome(host,"Sky dome",it.material);
            return it;
        }
        static Renderer Dome(GameObject host,string name,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Sphere);go.name=name;MemoryUi.Kill(go.GetComponent<Collider>());
            go.transform.SetParent(host.transform,false);
            var r=go.GetComponent<Renderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
            r.lightProbeUsage=LightProbeUsage.Off;r.reflectionProbeUsage=ReflectionProbeUsage.Off;r.enabled=false;
            return r;
        }
        // Fades the sky in (at once: already open, as when Build and Run starts in a chapter).
        public void Show(bool instantly=false)
        {
            if(Shown||!material)return;
            time=instantly?FadeSeconds:0;Debug.Log("SHAPE_OF_MEMORY_SKY "+(instantly?"shown":"fading in"));
        }
        void LateUpdate()
        {
            if(shade)
            {
                shade.enabled=dark>0;
                if(shade.enabled){veil.SetColor(BaseColor,new Color(0,0,0,dark));Round(shade);}
            }
            if(!material||!Shown)return;
            time+=Time.unscaledDeltaTime;float t=Mathf.Clamp01(time/FadeSeconds);Alpha=t*t*(3-2*t);
            material.SetFloat(AlphaId,Alpha);dome.enabled=Alpha>0;Round(dome);
            float yaw=stage?stage.eulerAngles.y:0;dome.transform.rotation=Quaternion.Euler(0,yaw+Facing-glowAzimuth,0);
            if(Alpha>=1&&!passthroughOff){passthroughOff=true;Debug.Log("SHAPE_OF_MEMORY_SKY opaque, passthrough off:"+XRPassthrough.Show(false));}
        }
        // Round the eyes (the dome's own centre is the sky's), well inside the far plane.
        void Round(Renderer r)
        {
            float radius=view?Mathf.Min(Radius,view.farClipPlane*.8f):Radius;
            r.transform.position=view?view.transform.position:Vector3.zero;r.transform.localScale=Vector3.one*radius*2;
        }
    }
}
