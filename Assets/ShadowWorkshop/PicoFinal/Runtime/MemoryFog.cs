using System;
using UnityEngine;

namespace ShapeOfMemory
{
    // One fog for the whole environment scene (MemoryLook.Fog): its clouds (PicoFinal/Env Cloud Sea) and its ruins and
    // giants (PicoFinal/Floating Chips Baked Lit, FloatHaze.hlsl) take it in place of each material's own haze, so a cloud
    // and a building at the same distance and height fade alike. MemoryEnvironment publishes it every frame while the
    // scene is loaded (so the look asset can be tuned in Play mode, and keeps its values after); without the game (the
    // environment scene alone in the editor) every material renders as authored. Shaders/FloatingChips/PicoFinalFog.hlsl.
    // Heights are metres from the floor under the stage (negative: below it); the environment's cloud tops lie about
    // 8 m under it, and a seated player's eyes about 1.2 m over it.
    //   Colour    the sky right above: the sky behind each point, read no lower than SkyAbove degrees, so everything
    //             below the horizon takes the sky in the same direction just above the cloud horizon (2026-11-14, the
    //             user: "blend with the skybox's color directly above it"). Flow's band there is bright and changes
    //             round the horizon (orange toward the glow, pink, lavender, violet), where just below the horizon it is
    //             a flat dark purple; fog read there (the first version, at -3.4 degrees) came out the same whatever its
    //             tint. The user chose this, the sky's own colour round the horizon (2026-11-14, "R1", over one colour
    //             for all the fog). Colour/ColourShare can push it toward a colour of its own (share 1: that one colour
    //             everywhere), giving way to the sky far out (ColourToSkyFar) so the farthest clouds still melt into it.
    //   Distance  into that colour, as the game's own clouds had it on 2026-11-12 (Cloud.shader: 25 to 400 m, up to
    //             0.9, ease-out: about a third at 100 m, half at 150 m). Each material's own Haze Amount (ruins) or
    //             Haze at full (sea) caps it, so the giants (0.566) stay on the horizon.
    //   Mist      the gaps between the clouds, and whatever stands in them, fill with mist from MistTop down, full
    //             MistDepth metres lower (the 2026-11-12 sea: from 1.5 m under its tops, full 4.5 m lower), MistSky of
    //             the way from MistColour to the sky above.
    //   Wrap      buildings fade between WrapTop and WrapBottom into the colour of the cloud tops (worked out from the
    //             sea's own material, CloudTop) taken WrapSky of the way to the mist, so where one stands in a cloud
    //             there is no hard line (the clouds themselves don't take it).
    [Serializable]
    public sealed class MemoryFog
    {
        [Tooltip("Off: each of the environment scene's materials keeps its own haze, as authored.")]
        public bool On;

        [Header("Distance")]
        [Tooltip("Metres from the eyes where the fog starts.")]
        public float Start=25;
        [Tooltip("Metres from the eyes where it is at Amount.")]
        public float Full=400;
        [Range(0,1),Tooltip("How much of the sky behind at Full (each material's own Haze Amount caps it).")]
        public float Amount=.9f;
        [Range(0,1),Tooltip("The curve: 0 slow start (smoothstep: little fog in the middle distance), 1 as 2026-11-12 (ease-out: most of the change near).")]
        public float EaseOut=1;

        [Header("Mist between the clouds")]
        [Tooltip("Height where the mist starts, metres from the floor (the cloud tops are about -8).")]
        public float MistTop=-9.5f;
        [Min(.01f),Tooltip("How many metres further down it is full.")]
        public float MistDepth=4.5f;
        [Tooltip("A colour of the mist's own, MistSky of the way to the sky above.")]
        public Color MistColour=new Color32(0xA0,0x7A,0xAE,0xFF);
        [Range(0,1),Tooltip("The mist's colour: 0 MistColour, 1 the sky above (with the fog's Colour).")]
        public float MistSky=1;

        [Header("Buildings into the cloud tops")]
        [Tooltip("Height where buildings start to fade, metres from the floor.")]
        public float WrapTop=-6;
        [Tooltip("Height where they reach WrapAmount (around the tops, -8).")]
        public float WrapBottom=-8.5f;
        [Range(0,1)]
        public float WrapAmount=.8f;
        [Range(0,1),Tooltip("What they fade into: 0 the cloud tops' colour, 1 the mist (the sky above).")]
        public float WrapSky=1;

        [Header("The sky above")]
        [Range(-10,20),Tooltip("Degrees over the horizon: the fog takes its colour from the sky behind, read no lower than this, so below it takes the sky in the same direction just above the cloud horizon (Flow's bright band is about +1 to +5; below the horizon it is a flat dark purple).")]
        public float SkyAbove=3;
        [Range(1,60),Tooltip("Degrees of azimuth the colour is averaged over either side: the band's painted wisps would otherwise streak down the screen; wider, the colours round the horizon blend more.")]
        public float SkyWidth=12;
        [Range(0,10),Tooltip("Blur (mip level) of the sky read, in the band and behind what floats higher up.")]
        public float SkyBlur=3;
        [Tooltip("A colour of the fog's own, taken ColourShare of the way from the sky above (0: the sky alone, the default; 1: this one colour for all the fog). #A97AA8: the fog under the arch ahead-right in the sky's colours (#BC87BB), 10% darker, kept in case.")]
        public Color Colour=new Color32(0xA9,0x7A,0xA8,0xFF);
        [Range(0,1)]
        public float ColourShare=0;
        [Tooltip("Far out (0.6 to 1.4 of Full) the fog's own colour gives way to the sky itself, so the farthest clouds melt into the sky behind them (orange toward the glow). Off: the one colour all the way to the horizon.")]
        public bool ColourToSkyFar=true;

        static readonly int FogId=Shader.PropertyToID("_PicoFinalFog"),FarId=Shader.PropertyToID("_PicoFinalFogFar"),MistId=Shader.PropertyToID("_PicoFinalFogMist"),
            MistColourId=Shader.PropertyToID("_PicoFinalFogMistColor"),WrapId=Shader.PropertyToID("_PicoFinalFogWrap"),WrapColourId=Shader.PropertyToID("_PicoFinalFogWrapColor"),
            TintId=Shader.PropertyToID("_PicoFinalFogTint"),TintFarId=Shader.PropertyToID("_PicoFinalFogTintFar"),RingId=Shader.PropertyToID("_PicoFinalFogRing");

        // floor: the world height of the floor under the stage; sea: the cloud sea's material (for the tops' colour);
        // ring: draws the sky's horizon band for the fog's colour (MemoryFogRing; without one the fog has none).
        public void Publish(float floor,Material sea,MemoryFogRing ring=null)
        {
            Shader.SetGlobalFloat(FogId,On?1:0);
            if(!On)return;
            if(ring!=null)Shader.SetGlobalTexture(RingId,ring.Draw(this));
            Shader.SetGlobalVector(FarId,new Vector4(Start,Full,Amount,EaseOut));
            Shader.SetGlobalVector(MistId,new Vector4(floor+MistTop,MistDepth,MistSky,Mathf.Sin(SkyAbove*Mathf.Deg2Rad)));
            // Global colours reach the shader as they are (no sRGB-to-linear step, unlike a material's), and the shaders
            // work in linear: so the picked colours go over linear, or they render far lighter than picked.
            Shader.SetGlobalColor(MistColourId,MistColour.linear);
            Shader.SetGlobalVector(WrapId,new Vector4(floor+WrapTop,floor+WrapBottom,WrapAmount,SkyBlur));
            var top=(sea?CloudTop(sea):MistColour).linear;top.a=WrapSky;
            Shader.SetGlobalColor(WrapColourId,top);
            var tint=Colour.linear;tint.a=ColourShare;
            Shader.SetGlobalColor(TintId,tint);
            Shader.SetGlobalVector(TintFarId,ColourToSkyFar?new Vector4(.6f*Full,1.4f*Full):new Vector4(1e6f,2e6f));
        }
        public static void Reset()=>Shader.SetGlobalFloat(FogId,0);

        // The colour of the cloud sea's tops as PicoFinal/Env Cloud Sea shades them (facing up, high in the cloud,
        // out of the folds): its four-colour ramp at wrapped N.L toward its sun, mixed with the height share.
        public static Color CloudTop(Material sea)
        {
            Color Get(string name,Color fallback)=>sea.HasProperty(name)?sea.GetColor(name).linear:fallback.linear;
            float F(string name,float fallback)=>sea.HasProperty(name)?sea.GetFloat(name):fallback;
            Vector3 sun=sea.HasProperty("_SunDir")?(Vector3)sea.GetVector("_SunDir"):new Vector3(-.33f,.26f,.91f);
            float wrap=F("_Wrap",.35f),share=F("_HeightShare",.3f);
            float lit=Mathf.Clamp01(sun.normalized.y*(1-wrap)+wrap);
            float t=Mathf.Clamp01(Mathf.Lerp(lit,1,share));
            Color c=Color.Lerp(Get("_Shadow",new Color(.2f,.17f,.36f)),Get("_Body",new Color(.38f,.33f,.56f)),Smooth(0,.5f,t));
            c=Color.Lerp(c,Get("_Lit",new Color(.7f,.52f,.62f)),Smooth(.45f,.8f,t));
            c=Color.Lerp(c,Get("_Hot",new Color(.98f,.7f,.5f)),Smooth(.78f,1,t));
            c.a=1;
            return c.gamma;
        }
        static float Smooth(float a,float b,float x){float t=Mathf.Clamp01((x-a)/(b-a));return t*t*(3-2*t);}

        public override string ToString()=>!On?"off":$"sky above {SkyAbove} deg (+-{SkyWidth} deg, mip {SkyBlur}), colour #{ColorUtility.ToHtmlStringRGB(Colour)} at {ColourShare}, distance {Start}-{Full} m to {Amount} (ease-out {EaseOut}), "+
            $"mist from {MistTop} m over {MistDepth} m (#{ColorUtility.ToHtmlStringRGB(MistColour)}, sky {MistSky}), wrap {WrapTop} to {WrapBottom} m at {WrapAmount} (sky {WrapSky})";
    }

    // The fog's colour round the horizon: the sky's band just over the cloud horizon (MemoryFog.SkyAbove, a few degrees
    // high), averaged sideways (SkyWidth) for each azimuth of the sky's own cubemap, into a strip of Size texels one
    // high (Shaders/FloatingChips/FogRing.shader, Resources/ShapeOfMemory/Fog ring.mat). Drawn again only when the sky,
    // its tint or those settings change; the fog reads it as _PicoFinalFogRing.
    public sealed class MemoryFogRing:IDisposable
    {
        public const int Size=256;
        public const float BandDegrees=3;   // how far up from SkyAbove the band is read
        const string MaterialPath="ShapeOfMemory/Fog ring",ShaderName="Hidden/PicoFinal/Fog Ring";
        static readonly int SkyTexId=Shader.PropertyToID("_MemorySkyTex"),SkyTintId=Shader.PropertyToID("_MemorySkyTint"),ParamsId=Shader.PropertyToID("_FogRing");
        RenderTexture strip;
        Material material;
        int drawn;
        public int Draws{get;private set;}

        public Texture Draw(MemoryFog fog)
        {
            var sky=Shader.GetGlobalTexture(SkyTexId);
            if(!sky)return strip;
            var tint=Shader.GetGlobalColor(SkyTintId);
            int key=HashCode.Combine(sky.GetInstanceID(),tint,fog.SkyAbove,fog.SkyWidth,fog.SkyBlur);
            if(strip&&strip.IsCreated()&&key==drawn)return strip;
            if(!material)
            {
                var shared=Resources.Load<Material>(MaterialPath);
                var shader=shared?shared.shader:Shader.Find(ShaderName);
                if(!shader){Debug.LogError("SHAPE_OF_MEMORY_FOG no "+ShaderName+" (Resources/"+MaterialPath+")");return strip;}
                material=new Material(shader){name="Fog ring",hideFlags=HideFlags.DontSave};
            }
            if(!strip)strip=new RenderTexture(Size,1,0,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear){name="Fog ring",wrapModeU=TextureWrapMode.Repeat,wrapModeV=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear,useMipMap=false,hideFlags=HideFlags.DontSave};
            material.SetVector(ParamsId,new Vector4(fog.SkyAbove*Mathf.Deg2Rad,fog.SkyWidth*Mathf.Deg2Rad,fog.SkyBlur,BandDegrees*Mathf.Deg2Rad));
            Graphics.Blit(Texture2D.blackTexture,strip,material);
            drawn=key;Draws++;
            return strip;
        }

        public void Dispose()
        {
            if(strip){strip.Release();Kill(strip);}
            if(material)Kill(material);
            strip=null;material=null;
        }
        static void Kill(UnityEngine.Object o){if(Application.isPlaying)UnityEngine.Object.Destroy(o);else UnityEngine.Object.DestroyImmediate(o);}
    }
}
