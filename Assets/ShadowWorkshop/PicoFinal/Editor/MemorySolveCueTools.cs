using System.Collections.Generic;
using System.Linq;
using CloudGarden.ShadowPuzzle;
using UnityEditor;
using UnityEngine;
using Art=ShapeOfMemory.MemorySolveCue.Art;
using Spawn=ShapeOfMemory.MemorySolveCue.Spawn;
using Depth=ShapeOfMemory.MemorySolveCue.Depth;
using Layer=ShapeOfMemory.MemorySolveCue.Layer;

namespace ShapeOfMemory.Editor
{
    // The chapters' found moments (MemorySolveCue). MetaHackthone > Pico Final > Solve cues:
    // - Select cues: the folder, to tune a chapter's asset in the inspector (sounds go in its Sounds list).
    // - Make default cues: writes the four chapters' cut-outs as designed on 2026-11-15 over the assets (sounds kept),
    //   and sets up the atlas's import and the page material.
    // Places are spot units (MemorySolveCue): measured on each chapter's solved shadow (Bon Voyage's plane spans x -.55..+.7,
    // y -.35..+.35; the birthday candles' tips are at x -.20, -.03, .13, y .46).
    public static class MemorySolveCueTools
    {
        const string Menu="MetaHackthone/Pico Final/Solve cues/";
        public const string Folder=ShapeOfMemoryBuilder.Root+"/Resources/"+MemorySolveCue.ResourceFolder;
        public const string AtlasPath=ShapeOfMemoryBuilder.Root+"/Art/Cues/CueAtlas.png";
        public const string MaterialPath=ShapeOfMemoryBuilder.Root+"/Resources/"+MemorySolveMoment.MaterialPath+".mat";
        public const string Shader="ShapeOfMemory/PageCue";

        [MenuItem(Menu+"Select cues")]
        public static void Select()
        {
            var folder=AssetDatabase.LoadAssetAtPath<Object>(Folder);if(!folder){MakeDefaults();folder=AssetDatabase.LoadAssetAtPath<Object>(Folder);}
            Selection.activeObject=folder;EditorGUIUtility.PingObject(folder);
        }
        [MenuItem(Menu+"Make default cues (overwrites the cut-outs, keeps sounds)")]
        public static void MakeDefaults()
        {
            SetUpAtlas();SetUpAudio();
            System.IO.Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            var chapters=MemoryChapters.Load();var levels=chapters?chapters.chapters.Where(c=>c).ToArray():new Torch2Level[0];
            foreach(var level in levels)
            {
                var preset=Preset(level.name);if(preset==null){Debug.LogWarning("SHAPE_OF_MEMORY_CUE no preset for "+level.name);continue;}
                string path=Folder+"/"+Short(level.name)+".asset";
                var cue=AssetDatabase.LoadAssetAtPath<MemorySolveCue>(path);
                if(!cue){cue=ScriptableObject.CreateInstance<MemorySolveCue>();AssetDatabase.CreateAsset(cue,path);}
                cue.level=level;cue.layers=preset;cue.seconds=Short(level.name)=="Birthday"?6f:5.5f;
                // Sounds are the designer's once set: the defaults fill only an empty list.
                bool filled=false;if(cue.sounds==null||cue.sounds.Length==0){cue.sounds=DefaultSounds(level.name);filled=cue.sounds.Length>0;}
                EditorUtility.SetDirty(cue);
                Debug.Log($"SHAPE_OF_MEMORY_CUE wrote {path}: {preset.Length} layers, {cue.sounds.Length} sounds "+(filled?"(defaults)":"kept"));
            }
            AssetDatabase.SaveAssets();MemorySolveCue.Clear();
        }
        // ---- The chapters' sounds (cut by Tools/ShadowArt/trim_cue_audio.py into Audio/Cues; volumes from its levels)
        public const string AudioFolder=ShapeOfMemoryBuilder.Root+"/Audio/Cues";
        static MemorySolveCue.Sound Sound(string file,float delay,float volume)
        {
            var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(AudioFolder+"/"+file+".wav");
            if(!clip){Debug.LogWarning("SHAPE_OF_MEMORY_CUE no "+AudioFolder+"/"+file+".wav");return null;}
            return new MemorySolveCue.Sound{clip=clip,delay=delay,volume=volume};
        }
        static MemorySolveCue.Sound[] DefaultSounds(string level)
        {
            MemorySolveCue.Sound[] sounds;
            switch(Short(level))
            {
                // The breath with the candles going out, the horn with the confetti.
                case "Birthday":sounds=new[]{Sound("Birthday_BlowOut",BlowAt,.55f),Sound("Birthday_PartyHorn",PartyAt,.33f)};break;
                // The jet swells from the find and passes left to right (as the plane flies) about 2 s in; the cabin chime first.
                case "BonVoyage":sounds=new[]{Sound("BonVoyage_JetPass",0,.49f),Sound("BonVoyage_CabinChime",.6f,.27f)};break;
                case "GoodMorning":sounds=new[]{Sound("GoodMorning_Sizzle",.2f,1f)};break;
                // The pot hits the floor with the shards, the dog's "!" and then its bark.
                case "Home":sounds=new[]{Sound("Home_PotBreak",.3f,.75f),Sound("Home_PuppyBark",.75f,.53f)};break;
                default:sounds=new MemorySolveCue.Sound[0];break;
            }
            return sounds.Where(s=>s!=null).ToArray();
        }
        // Short cues, played often: decoded at load (no stream start-up lag), Vorbis, stereo kept where it was cut stereo.
        [MenuItem(Menu+"Set up cue audio import")]
        public static void SetUpAudio()
        {
            foreach(var guid in AssetDatabase.FindAssets("t:AudioClip",new[]{AudioFolder}))
            {
                var path=AssetDatabase.GUIDToAssetPath(guid);if(!(AssetImporter.GetAtPath(path) is AudioImporter importer))continue;
                var s=importer.defaultSampleSettings;s.loadType=AudioClipLoadType.DecompressOnLoad;s.compressionFormat=AudioCompressionFormat.Vorbis;s.quality=.7f;
                s.preloadAudioData=true;importer.defaultSampleSettings=s;importer.forceToMono=false;importer.loadInBackground=false;importer.SaveAndReimport();
            }
        }
        // The atlas: kept as drawn (sRGB, alpha as transparency, mipmaps for the far page), and the page material over it.
        public static void SetUpAtlas()
        {
            if(AssetImporter.GetAtPath(AtlasPath) is TextureImporter importer)
            {
                importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;importer.alphaSource=TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency=true;importer.mipmapEnabled=true;importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Trilinear;
                importer.maxTextureSize=1024;importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.SaveAndReimport();
            }
            else Debug.LogError("SHAPE_OF_MEMORY_CUE no atlas at "+AtlasPath+" (Tools/ShadowArt/build_cue_atlas.py)");
            var material=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);var shader=UnityEngine.Shader.Find(Shader);
            if(!shader){Debug.LogError("SHAPE_OF_MEMORY_CUE no shader "+Shader);return;}
            if(!material){material=new Material(shader){name="Page cue"};AssetDatabase.CreateAsset(material,MaterialPath);}
            material.shader=shader;material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath));EditorUtility.SetDirty(material);
        }
        static string Short(string level)=>level.Replace("_NEW","");

        // ---- The four chapters' cut-outs
        static Color A(Color c,float a){c.a=a;return c;}
        static Color[] Chapters=>Enumerable.Range(0,5).Select(MemoryPalette.Light).ToArray();
        static readonly Color White=Color.white,Sky=new Color(.86f,.92f,1f),Terracotta=MemoryPalette.Hex(0xC8714E),Flame=new Color(1f,.82f,.45f),Sun=MemoryPalette.Hex(0xF2B04A);
        // Balloons need more colour than the pastel chapter accents: against the lamp's warm spot those read as white.
        static readonly Color[] Balloons={MemoryPalette.Hex(0xE36D8A),MemoryPalette.Hex(0x6C9BE8),MemoryPalette.Hex(0xF0A45E)};
        static Layer[] Preset(string level)
        {
            switch(Short(level))
            {
                case "Birthday":return Birthday();
                case "BonVoyage":return BonVoyage();
                case "GoodMorning":return GoodMorning();
                case "Home":return Home();
            }
            return null;
        }
        // Make a wish: the candles light one by one, a breath blows them out at BlowAt (wisps of smoke), then the horn and
        // the confetti and streamers rain over the cake; balloons float up beside it from the start.
        const float BlowAt=2f,PartyAt=2.4f;
        static readonly Vector2[] Candles={new Vector2(-.205f,.535f),new Vector2(-.035f,.545f),new Vector2(.125f,.535f)};
        static Layer[] Birthday()=>new[]
        {
            new Layer{note="Candle flames, lit left to right, blown out together",art=Art.Flame,depth=Depth.Front,tints=new[]{Flame},spawn=Spawn.Points,
                points=Candles,count=3,delay=.2f,stagger=.6f,until=BlowAt+.2f,size=new Vector2(.19f,.19f),pop=.2f,fadeOut=.18f,flicker=.18f,inDark=1},
            new Layer{note="Smoke from the blown-out wicks",art=Art.Puff,depth=Depth.Front,tints=new[]{A(MemoryPalette.Faint,.8f)},spawn=Spawn.Points,
                points=Candles.Select(c=>c+new Vector2(0,.07f)).ToArray(),count=3,delay=BlowAt+.15f,stagger=.08f,life=new Vector2(1.4f,1.6f),
                size=new Vector2(.16f,.2f),velocity=new Vector2(0,.16f),sway=.03f,swayRate=.7f,pop=.3f,fadeOut=1f,inDark=.6f},
            new Layer{note="Confetti, with the horn",art=Art.Confetti,depth=Depth.Front,tints=Chapters.Append(MemoryPalette.Gold).ToArray(),count=46,
                from=new Vector2(-.9f,.55f),to=new Vector2(.9f,1f),delay=PartyAt,stagger=.8f,life=new Vector2(2.8f,3.4f),size=new Vector2(.07f,.1f),
                velocity=new Vector2(0,-.05f),jitter=.6f,gravity=.18f,drag=1.4f,sway=.07f,swayRate=.8f,spin=new Vector2(180,420),tilt=180,pop=.05f,fadeOut=.6f,inDark=.25f},
            new Layer{note="Streamers, with the horn",art=Art.Streamer,depth=Depth.Front,tints=Chapters,count=9,from=new Vector2(-.85f,.6f),to=new Vector2(.85f,.95f),
                delay=PartyAt+.1f,stagger=.7f,life=new Vector2(2.8f,3.3f),size=new Vector2(.2f,.26f),velocity=new Vector2(0,-.05f),gravity=.12f,drag=1.5f,
                sway=.04f,swayRate=.5f,spin=new Vector2(40,90),tilt=40,pop=.1f,fadeOut=.6f,inDark=.25f},
            new Layer{note="Balloons rising left of the cake",art=Art.Balloon,depth=Depth.Behind,tints=Balloons,count=3,
                from=new Vector2(-.82f,-.7f),to=new Vector2(-.55f,-.5f),delay=.9f,stagger=.7f,life=new Vector2(4.4f,4.6f),size=new Vector2(.42f,.5f),
                velocity=new Vector2(0,.2f),jitter=.2f,sway=.04f,swayRate=.45f,tilt=8,pop=.4f,fadeOut=.8f,inDark=.3f},
            new Layer{note="Sparkles round the cake",art=Art.Sparkle,depth=Depth.Front,tints=new[]{MemoryPalette.GoldBright},count=7,
                from=new Vector2(-.6f,-.5f),to=new Vector2(.75f,.75f),delay=PartyAt,stagger=2.4f,life=new Vector2(.6f,.9f),size=new Vector2(.12f,.18f),
                spin=new Vector2(30,60),pop=.15f,fadeOut=.4f,inDark=.6f},
        };
        // The plane flies on: clouds stream past it, a vapour trail leaves its tail, a few birds are overtaken.
        static Layer[] BonVoyage()=>new[]
        {
            new Layer{note="High clouds, far (slow)",art=Art.CloudSmall,depth=Depth.Behind,tints=new[]{A(White,.9f),A(Sky,.9f)},count=4,
                from=new Vector2(-1.3f,.5f),to=new Vector2(1.3f,.85f),delay=0,stagger=.6f,life=new Vector2(6,6),size=new Vector2(.32f,.42f),
                velocity=new Vector2(-.10f,0),jitter=.3f,wrap=true,pop=.6f,fadeOut=0,inDark=.05f},
            new Layer{note="Low clouds under the plane",art=Art.CloudB,depth=Depth.Behind,tints=new[]{A(White,.95f),A(Sky,.95f)},count=3,
                from=new Vector2(-1.3f,-.85f),to=new Vector2(1.3f,-.5f),delay=.1f,stagger=.6f,life=new Vector2(6,6),size=new Vector2(.5f,.6f),
                velocity=new Vector2(-.17f,0),jitter=.3f,wrap=true,pop=.6f,fadeOut=0,inDark=.05f},
            new Layer{note="Near clouds, fast, over everything low",art=Art.CloudA,depth=Depth.Front,tints=new[]{White},count=2,
                from=new Vector2(-1.4f,-.82f),to=new Vector2(1.4f,-.62f),delay=.3f,stagger=.4f,life=new Vector2(6,6),size=new Vector2(.62f,.72f),
                velocity=new Vector2(-.32f,0),jitter=.2f,wrap=true,pop=.5f,fadeOut=0,inDark=.05f},
            new Layer{note="Vapour trail from the tail",art=Art.Dash,depth=Depth.Behind,tints=new[]{A(White,.85f)},spawn=Spawn.Line,count=6,
                from=new Vector2(-.6f,.08f),to=new Vector2(-.98f,.13f),alongLine=true,delay=.2f,stagger=.7f,life=new Vector2(5,5),size=new Vector2(.28f,.28f),
                velocity=new Vector2(-.04f,0),pop=.2f,fadeOut=1f,inDark=.1f},
            new Layer{note="Birds being overtaken",art=Art.Bird,depth=Depth.Behind,tints=new[]{A(White,.85f)},count=3,
                from=new Vector2(-.2f,.55f),to=new Vector2(.6f,.8f),delay=1.2f,stagger=1f,life=new Vector2(4.2f,4.3f),size=new Vector2(.16f,.2f),
                velocity=new Vector2(-.08f,0),jitter=.3f,flipRandom=true,pop=.3f,fadeOut=.6f,inDark=.1f},
        };
        // Morning comes up behind her: the sun rises at her back, breakfast steams in front of her, birds are about.
        static Layer[] GoodMorning()=>new[]
        {
            new Layer{note="The sun rising behind her",art=Art.Sun,depth=Depth.Behind,tints=new[]{Sun},spawn=Spawn.Points,points=new[]{new Vector2(.72f,-.3f)},count=1,
                delay=0,life=new Vector2(5.5f,5.5f),size=new Vector2(.75f,.75f),velocity=new Vector2(0,.075f),spin=new Vector2(5,5),pop=.8f,fadeOut=.8f,inDark=.4f},
            new Layer{note="Steam rising in front of her",art=Art.Steam,depth=Depth.Front,tints=new[]{A(White,.75f)},count=4,loop=true,
                from=new Vector2(-.8f,-.5f),to=new Vector2(-.6f,-.4f),delay=.4f,stagger=1.2f,life=new Vector2(2,2.4f),size=new Vector2(.4f,.5f),
                velocity=new Vector2(0,.18f),sway=.05f,swayRate=.5f,tilt=8,pop=.5f,fadeOut=.8f,inDark=.4f},
            new Layer{note="Sparkles over the hat",art=Art.Sparkle,depth=Depth.Front,tints=new[]{MemoryPalette.GoldBright},count=5,
                from=new Vector2(-.35f,.55f),to=new Vector2(.4f,.85f),delay=.5f,stagger=2.5f,life=new Vector2(.6f,.9f),size=new Vector2(.12f,.18f),
                spin=new Vector2(30,60),pop=.15f,fadeOut=.4f,inDark=.6f},
            new Layer{note="Morning birds",art=Art.Bird,depth=Depth.Behind,tints=new[]{A(White,.85f)},count=2,
                from=new Vector2(-.95f,.45f),to=new Vector2(-.5f,.75f),delay=1f,stagger=1.2f,life=new Vector2(4,4.2f),size=new Vector2(.16f,.2f),
                velocity=new Vector2(-.06f,.02f),jitter=.3f,flipRandom=true,pop=.3f,fadeOut=.6f,inDark=.1f},
        };
        // The pot hits the floor (shards and dust by it), the dog startles, and hearts float up on both pages.
        static Layer[] Home()=>new[]
        {
            new Layer{note="Shards flying left from the pot",art=Art.Shard,wall=0,depth=Depth.Front,tints=new[]{Terracotta},count=5,
                from=new Vector2(-.78f,-.82f),to=new Vector2(-.66f,-.74f),delay=.3f,stagger=.05f,life=new Vector2(1,1.3f),size=new Vector2(.09f,.13f),
                velocity=new Vector2(-.35f,.45f),jitter=.5f,gravity=1.4f,spin=new Vector2(200,500),tilt=180,pop=.05f,fadeOut=.3f,inDark=.8f},
            new Layer{note="Shards flying right from the pot",art=Art.Shard,wall=0,depth=Depth.Front,tints=new[]{Terracotta},count=5,
                from=new Vector2(-.78f,-.82f),to=new Vector2(-.66f,-.74f),delay=.3f,stagger=.05f,life=new Vector2(1,1.3f),size=new Vector2(.09f,.13f),
                velocity=new Vector2(.3f,.5f),jitter=.5f,gravity=1.4f,spin=new Vector2(200,500),tilt=180,pop=.05f,fadeOut=.3f,inDark=.8f},
            new Layer{note="Dust where the pot lands",art=Art.Puff,wall=0,depth=Depth.Front,tints=new[]{A(White,.55f)},count=3,
                from=new Vector2(-.82f,-.88f),to=new Vector2(-.62f,-.78f),delay=.3f,stagger=.2f,life=new Vector2(1.2f,1.5f),size=new Vector2(.3f,.4f),
                velocity=new Vector2(0,.08f),jitter=.5f,pop=.3f,fadeOut=.8f,inDark=.8f},
            new Layer{note="The dog startles",art=Art.Bang,wall=0,depth=Depth.Front,tints=new[]{MemoryPalette.Light(3)},spawn=Spawn.Points,points=new[]{new Vector2(.36f,.6f)},count=1,
                delay=.55f,life=new Vector2(1.8f,1.8f),size=new Vector2(.36f,.36f),tilt=0,angle=-8,pop=.25f,fadeOut=.4f,inDark=.6f},
            new Layer{note="Hearts rising on both pages",art=Art.Heart,depth=Depth.Front,tints=new[]{Balloons[0],Chapters[3],Chapters[0]},count=6,
                from=new Vector2(-.6f,-.85f),to=new Vector2(.6f,-.45f),delay=.9f,stagger=2.2f,life=new Vector2(2.6f,3f),size=new Vector2(.24f,.3f),
                velocity=new Vector2(0,.18f),jitter=.2f,sway=.05f,swayRate=.5f,tilt=12,pop=.3f,fadeOut=.8f,inDark=.3f},
        };
    }
}
