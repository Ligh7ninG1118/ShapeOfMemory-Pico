using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShapeOfMemory
{
    // The table-top's light (MemoryLook.Lighting): the key light's colour, strength and direction, and the ambient. Only
    // the table-top takes it (table, stand, lamps, book, the puzzle pieces): the sky, the clouds and the ruins light
    // themselves (MemorySky's glow, baked light).
    // The key comes from an azimuth (degrees right of the stage's forward, or from the sky's glow) and an elevation, and
    // turns with the stage, as the sky does, so a recentre keeps it where the sky says the light is.
    // Strength: set once here; the title (MemoryGame.Cover) scales it from the value it finds, so this runs before that.
    public sealed class MemoryLighting : MonoBehaviour
    {
        [Serializable]
        public struct Setup
        {
            public string Name;
            public Color KeyColor;
            public float KeyIntensity;
            [Tooltip("Degrees over the horizon the key comes from.")]
            public float KeyElevation;
            [Tooltip("Degrees right of the player's forward the key comes from (180: from behind). Ignored with KeyFromGlow.")]
            public float KeyAzimuth;
            [Tooltip("The key comes from the sky's glow (MemoryLook.GlowFacing), as a low sun would: it backlights the table-top.")]
            public bool KeyFromGlow;
            [Tooltip("Flat: AmbientSky alone. Trilight: sky, equator, ground.")]
            public AmbientMode Ambient;
            public Color AmbientSky,AmbientEquator,AmbientGround;

            // The game's own (the builder's since the start): a cream key from above, behind the right shoulder, and a
            // flat grey-blue ambient.
            public static Setup Game=>new Setup{Name="Game",KeyColor=new Color(1,.91f,.78f),KeyIntensity=1.2f,KeyElevation=48,KeyAzimuth=152,
                Ambient=AmbientMode.Flat,AmbientSky=new Color(.55f,.65f,.70f),AmbientEquator=new Color(.114f,.125f,.133f),AmbientGround=new Color(.047f,.043f,.035f)};
            // The environment scene's: a warm sun low in the glow and its lavender trilight (at its 0.8 intensity).
            public static Setup Sun=>new Setup{Name="Sun",KeyColor=new Color(1,.8f,.45f),KeyIntensity=1.6f,KeyElevation=15,KeyFromGlow=true,
                Ambient=AmbientMode.Trilight,AmbientSky=new Color(.525f,.609f,1.05f)*.8f,AmbientEquator=new Color(.3864f,.4232f,.8096f)*.8f,AmbientGround=new Color(.234f,.243f,.522f)*.8f};
            // The two together: that lavender ambient, a warm key from behind the player's left.
            public static Setup Hybrid=>new Setup{Name="Hybrid",KeyColor=new Color(1,.78f,.52f),KeyIntensity=1.5f,KeyElevation=32,KeyAzimuth=-150,
                Ambient=AmbientMode.Trilight,AmbientSky=Sun.AmbientSky,AmbientEquator=Sun.AmbientEquator,AmbientGround=Sun.AmbientGround};

            // Where the key comes from, degrees right of forward, for a glow facing that way.
            public float From(float glowFacing)=>KeyFromGlow?glowFacing:KeyAzimuth;
            // The light's own rotation (it shines along its forward) for a stage turned yaw degrees.
            public Quaternion Rotation(float yaw,float glowFacing)=>Quaternion.Euler(KeyElevation,yaw+From(glowFacing)+180,0);
        }

        public Setup Current{get;private set;}
        public Light Key{get;private set;}
        Transform stage;
        MemorySky sky;

        // key: the directional light the table-top is lit by (the first realtime one in the scene if null).
        public static MemoryLighting Add(GameObject host,Setup setup,Light key,MemorySky sky,Transform stage)
        {
            var it=host.AddComponent<MemoryLighting>();it.stage=stage;it.sky=sky;it.Current=setup;
            it.Key=key?key:FindKey(host.scene);
            Apply(setup,it.Key,stage?stage.eulerAngles.y:0,sky?sky.Facing:0);
            Debug.Log($"SHAPE_OF_MEMORY_LIGHTING {setup.Name}: key {(it.Key?it.Key.name:"none")} {setup.KeyIntensity} from {setup.From(sky?sky.Facing:0):0} deg, {setup.KeyElevation:0} up; ambient {setup.Ambient}");
            return it;
        }
        // Baked-only lights don't light at runtime, so they can't be the key. Light.lightmapBakeType is editor-only (the
        // player fails to compile); bakingOutput is what the bake produced, the same in the editor and on the device.
        public static Light FindKey(UnityEngine.SceneManagement.Scene scene)=>
            UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l=>l.type==LightType.Directional&&!IsBakedOnly(l)&&l.gameObject.scene==scene).OrderBy(l=>l.name).FirstOrDefault();
        static bool IsBakedOnly(Light l)=>l.bakingOutput.isBaked&&l.bakingOutput.lightmapBakeType==LightmapBakeType.Baked;

        // The whole setup at once: also how the builder writes it into the scene (yaw 0), so the editor shows it.
        public static void Apply(Setup setup,Light key,float yaw,float glowFacing)
        {
            if(key){key.color=setup.KeyColor;key.intensity=setup.KeyIntensity;key.transform.rotation=setup.Rotation(yaw,glowFacing);}
            RenderSettings.ambientMode=setup.Ambient;
            RenderSettings.ambientSkyColor=setup.AmbientSky;RenderSettings.ambientEquatorColor=setup.AmbientEquator;RenderSettings.ambientGroundColor=setup.AmbientGround;
            if(setup.Ambient==AmbientMode.Flat)RenderSettings.ambientLight=setup.AmbientSky;
            DynamicGI.UpdateEnvironment();
        }

        void LateUpdate()
        {
            if(Key)Key.transform.rotation=Current.Rotation(stage?stage.eulerAngles.y:0,sky?sky.Facing:0);
        }
    }
}
