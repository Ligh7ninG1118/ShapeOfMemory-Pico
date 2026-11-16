using System.Collections.Generic;
using PhysicalAssembly.MetaXR;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShapeOfMemory
{
    // A baked COZY sky (Art/Sky/*.png on Shaders/SkyboxDithered; MemoryLook.Sky picks which).
    // Until the book is first opened the player sees the room (passthrough) or the desktop's plain background; Show()
    // then fades the sky in over FadeSeconds and, once it covers everything, turns passthrough off (it costs GPU time
    // behind an opaque sky). Drawn on a dome that follows the eyes rather than as the camera's skybox, so it can fade
    // over the clear eye buffer through the shader's _Alpha (the camera keeps clearing to transparent black). The dome
    // turns with the stage: the bake's pink glow (glowAzimuth, where it is in the bake's own frame at _Rotation 0:
    // WindwardEveningSky 50, MagicHourSky 120, FlowSky 30) sits Facing degrees right of the player's forward when the
    // stage was placed, beyond the book (MemoryLook.GlowAzimuth, GlowFacing). FlowSky is used at Facing -20, 20
    // degrees left, as the environment scene's bake, rim light and haze assume (the dome turned by -50 either way).
    // GlowDirection, and so the clouds' light, follow Facing: the light comes from where the glow shows.
    // Dark (the title's): a second dome, black at that alpha, drawn over the sky and under everything else (the room
    // hidden at 1).
    // Order: both domes draw after the opaque geometry (SkyQueue, DarkQueue: the end of the opaque range, as URP draws
    // its own skybox), so the depth test skips every pixel the clouds, the book or the room already cover; the domes sit
    // farther than anything in the scene, so what they draw is unchanged.
    // For the clouds (Cloud.shader) it publishes the sky's cubemap, how the dome turns it, and the glow's direction
    // (_MemorySkyTex, _MemorySkyYaw, _MemorySkyTint, _MemorySunDir). The light sits SunElevation degrees up, higher than
    // the bake's glow (at the horizon), so the clouds' tops catch it as in the reference.
    public sealed class MemorySky : MonoBehaviour
    {
        // Radius: the domes sit at min(Radius, 0.8 of the far plane) round the eyes. They draw after the opaque geometry
        // with a depth test, so they must lie behind everything: the environment artist's scene reaches about 900 m (its
        // far clouds), and MemoryEnvironment raises the far plane to 1200, which puts the domes at 960 m. (Was 400 m.)
        public const float FadeSeconds=2.5f,Radius=1000,SunElevation=18;
        // Where the glow shows: degrees right of the stage's forward (negative: left).
        public float Facing=>facing;
        public const int SkyQueue=(int)RenderQueue.GeometryLast-1,DarkQueue=(int)RenderQueue.GeometryLast;
        public float Alpha{get;private set;}
        public bool Shown=>time>=0;
        // 0 none .. 1 black over the room (and the sky).
        public float Dark{get=>dark;set=>dark=Mathf.Clamp01(value);}
        Material material,veil;
        Renderer dome,shade;
        Camera view;
        Transform stage;
        float time=-1,glowAzimuth,facing,dark;
        bool passthroughOff,fading;
        static readonly int AlphaId=Shader.PropertyToID("_Alpha"),BaseColor=Shader.PropertyToID("_BaseColor");
        static readonly int SkyTexId=Shader.PropertyToID("_MemorySkyTex"),SkyYawId=Shader.PropertyToID("_MemorySkyYaw"),SkyTintId=Shader.PropertyToID("_MemorySkyTint"),SunDirId=Shader.PropertyToID("_MemorySunDir");
        // For the environment artist's shaders (FloatHaze.hlsl): the dissolve while the sky fades in (the keyword only
        // while 0 < Alpha < 1, so its clip never costs early depth testing after), the stage's yaw (their rim light and
        // haze turn with it), and their haze taken from this sky rather than each material's own cubemap.
        public const string FadeKeyword="PICOFINAL_ENV_FADE";
        static readonly int VisibleId=Shader.PropertyToID("_PicoFinalEnvVisible"),EnvYawId=Shader.PropertyToID("_PicoFinalEnvYaw"),HazeFromSkyId=Shader.PropertyToID("_PicoFinalHazeFromSky");
        // Where the glow is (world), SunElevation up: Facing degrees right of the stage's forward.
        public Vector3 GlowDirection=>Quaternion.Euler(-SunElevation,(stage?stage.eulerAngles.y:0)+Facing,0)*Vector3.forward;

        // seeThrough: URP Unlit, alpha blended (MemoryGame.SeeThrough), for the dark; without it there is none.
        // glowAzimuth: where the glow is in the sky's own frame; facing: where it should show, right of the stage's forward.
        public static MemorySky Add(GameObject host,Material sky,Material seeThrough,float glowAzimuth,float facing,Camera view,Transform stage,List<Object> owned)
        {
            var it=host.AddComponent<MemorySky>();it.view=view;it.stage=stage;it.glowAzimuth=glowAzimuth;it.facing=facing;
            ResetGlobals();
            if(seeThrough)
            {
                // Right after the sky, before anything see-through.
                it.veil=new Material(seeThrough){name="Memory dark",renderQueue=DarkQueue};it.veil.SetFloat("_Cull",(float)CullMode.Off);
                it.veil.SetColor(BaseColor,new Color(0,0,0,0));owned.Add(it.veil);it.shade=Dome(host,"Dark dome",it.veil);
            }
            if(!sky){Debug.LogWarning("SHAPE_OF_MEMORY_SKY no sky material: the room stays");return it;}
            it.material=new Material(sky){name="Memory sky",renderQueue=SkyQueue};it.material.SetFloat(AlphaId,0);owned.Add(it.material);
            it.dome=Dome(host,"Sky dome",it.material);
            it.Publish();
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
        // Another sky, as it is (a look swapped at runtime): its cubemap, turned so its glow (glowAzimuth in its own frame)
        // shows facing degrees right of forward. The fade goes on where it was.
        public void Restyle(Material sky,float glowAzimuth,float facing)
        {
            this.glowAzimuth=glowAzimuth;this.facing=facing;
            if(!sky||!material)return;
            float alpha=material.GetFloat(AlphaId);
            material.shader=sky.shader;material.CopyPropertiesFromMaterial(sky);material.renderQueue=SkyQueue;material.SetFloat(AlphaId,alpha);
            material.name="Memory sky";Publish();
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
            if(!material)return;
            Publish();
            if(!Shown)return;
            time+=Time.unscaledDeltaTime;float t=Mathf.Clamp01(time/FadeSeconds);Alpha=t*t*(3-2*t);
            material.SetFloat(AlphaId,Alpha);dome.enabled=Alpha>0;Round(dome);
            dome.transform.rotation=Quaternion.Euler(0,DomeYaw,0);
            SetFade(Alpha>0&&Alpha<1,Alpha);
            if(Alpha>=1&&!passthroughOff){passthroughOff=true;Debug.Log("SHAPE_OF_MEMORY_SKY opaque, passthrough off:"+XRPassthrough.Show(false));}
        }
        void SetFade(bool on,float visible)
        {
            if(on)Shader.SetGlobalFloat(VisibleId,visible);
            if(on==fading)return;
            fading=on;
            if(on)Shader.EnableKeyword(FadeKeyword);else{Shader.DisableKeyword(FadeKeyword);Shader.SetGlobalFloat(VisibleId,1);}
        }
        static void ResetGlobals()
        {
            Shader.DisableKeyword(FadeKeyword);Shader.SetGlobalFloat(VisibleId,1);Shader.SetGlobalFloat(EnvYawId,0);Shader.SetGlobalFloat(HazeFromSkyId,0);
        }
        void OnDestroy()=>ResetGlobals();
        // The dome's turn: the bake's glow (glowAzimuth in the cubemap) Facing degrees right of the stage's forward.
        float DomeYaw=>(stage?stage.eulerAngles.y:0)+Facing-glowAzimuth;
        // The sky as the clouds see it: the cubemap, the lookup turn (the material's _Rotation less the dome's, as
        // SkyboxDithered turns its lookup), its tint and exposure, and the glow.
        void Publish()
        {
            if(!material)return;
            if(material.HasProperty("_Tex"))Shader.SetGlobalTexture(SkyTexId,material.GetTexture("_Tex"));
            Shader.SetGlobalFloat(SkyYawId,(material.HasProperty("_Rotation")?material.GetFloat("_Rotation"):0)-DomeYaw);
            var tint=material.HasProperty("_Tint")?material.GetColor("_Tint"):Color.white;float exposure=material.HasProperty("_Exposure")?material.GetFloat("_Exposure"):1;
            Shader.SetGlobalColor(SkyTintId,new Color(tint.r*exposure,tint.g*exposure,tint.b*exposure,1));
            Shader.SetGlobalVector(SunDirId,GlowDirection);
            Shader.SetGlobalFloat(EnvYawId,stage?stage.eulerAngles.y:0);Shader.SetGlobalFloat(HazeFromSkyId,1);
        }
        // Round the eyes (the dome's own centre is the sky's), well inside the far plane.
        void Round(Renderer r)
        {
            float radius=view?Mathf.Min(Radius,view.farClipPlane*.8f):Radius;
            r.transform.position=view?view.transform.position:Vector3.zero;r.transform.localScale=Vector3.one*radius*2;
        }
    }
}
