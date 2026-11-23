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
    //   and sets up the atlas's import and the page material. Extra levels (WildWest, 2026-11-16; Summer, 2026-11-17) get theirs too; Make
    //   writes one level's alone.
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
            levels=levels.Concat(Extra.Select(Torch2Level.Load).Where(l=>l&&!levels.Contains(l))).ToArray();
            foreach(var level in levels)Make(level);
            AssetDatabase.SaveAssets();MemorySolveCue.Clear();
        }
        // Levels with a found moment that are not in the chapter list yet (a cue is matched by level, so it waits for it).
        static readonly string[] Extra={"WildWest","Summer"};
        // One level's cue, without touching the others': its cut-outs from the preset (overwritten), its sounds from the
        // defaults only while its list is empty. Null where the level has no preset.
        public static MemorySolveCue Make(Torch2Level level)
        {
            var preset=Preset(level.name);if(preset==null){Debug.LogWarning("SHAPE_OF_MEMORY_CUE no preset for "+level.name);return null;}
            string path=Folder+"/"+Short(level.name)+".asset";
            var cue=AssetDatabase.LoadAssetAtPath<MemorySolveCue>(path);
            if(!cue){cue=ScriptableObject.CreateInstance<MemorySolveCue>();AssetDatabase.CreateAsset(cue,path);}
            cue.level=level;cue.layers=preset;cue.seconds=Short(level.name)=="Birthday"?6f:5.5f;
            // Sounds are the designer's once set: the defaults fill only an empty list.
            bool filled=false;if(cue.sounds==null||cue.sounds.Length==0){cue.sounds=DefaultSounds(level.name);filled=cue.sounds.Length>0;}
            EditorUtility.SetDirty(cue);AssetDatabase.SaveAssetIfDirty(cue);MemorySolveCue.Clear();
            Debug.Log($"SHAPE_OF_MEMORY_CUE wrote {path}: {preset.Length} layers, {cue.sounds.Length} sounds "+(filled?"(defaults)":"kept"));
            return cue;
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
                // Riding off: four hoof beats with the dust, then the harmonica's first phrase over the sunset.
                case "WildWest":sounds=new[]{Sound("WildWest_Gallop",0,1f),Sound("WildWest_Harmonica",1f,.51f)};break;
                // The seal's flippers slap the shallows with the white water, then it barks four times with the bark lines
                // (synthesized: MemorySummerSounds, Make Summer sounds).
                case "Summer":sounds=new[]{Sound("Summer_Splash",0,.73f),Sound("Summer_SealBark",BarkAt,.55f)};break;
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
        static readonly Color White=Color.white,Sky=new Color(.86f,.92f,1f),Terracotta=MemoryPalette.Hex(0xC8714E),Flame=new Color(1f,.82f,.45f),StarGold=MemoryPalette.Hex(0xF2B04A),Sun=MemoryPalette.Hex(0xF2B04A);
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
                case "WildWest":return WildWest();
                case "Summer":return Summer();
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
        // Compliments to the chef (the chapter is a chef's kitchen now; its rising sun and morning birds were the old Good
        // Morning's, the user 2026-11-16): five stars stamp out over the hat one by one, a review, and twinkle; a chef's
        // kiss (hearts) floats from his lips; the kettle on the slide steams. The chef (spot units): hat top y .66, x -.21
        // to .43; face front x -.2 at y 0 to -.15; the kettle at (-.7,-.6).
        static readonly Vector2[] Stars={new Vector2(-.37f,.55f),new Vector2(-.18f,.8f),new Vector2(.11f,.88f),new Vector2(.4f,.8f),new Vector2(.59f,.55f)};
        static Layer[] GoodMorning()=>new[]
        {
            new Layer{note="Five stars over the hat, stamped left to right",art=Art.Star,depth=Depth.Front,tints=new[]{StarGold},spawn=Spawn.Points,points=Stars,count=5,
                delay=.35f,stagger=1f,life=new Vector2(5.5f,5.5f),size=new Vector2(.22f,.24f),tilt=10,sway=.008f,swayRate=.35f,pop=.3f,fadeOut=.6f,inDark=.7f},
            new Layer{note="Sparkles twinkling among the stars",art=Art.Sparkle,depth=Depth.Front,tints=new[]{MemoryPalette.GoldBright},count=6,
                from=new Vector2(-.5f,.45f),to=new Vector2(.72f,.95f),delay=1.5f,stagger=3.2f,life=new Vector2(.6f,.9f),size=new Vector2(.1f,.15f),
                spin=new Vector2(30,60),pop=.15f,fadeOut=.4f,inDark=.6f},
            new Layer{note="A chef's kiss: hearts from his lips, one after another",art=Art.Heart,depth=Depth.Front,tints=new[]{Balloons[0],Chapters[0],Balloons[0]},count=3,
                spawn=Spawn.Line,from=new Vector2(-.34f,-.12f),to=new Vector2(-.3f,-.06f),delay=1.3f,stagger=1.8f,life=new Vector2(2,2.3f),size=new Vector2(.14f,.17f),
                velocity=new Vector2(-.13f,.19f),jitter=.2f,sway=.03f,swayRate=.6f,tilt=14,pop=.25f,fadeOut=.7f,inDark=.4f},
            new Layer{note="Steam from the kettle",art=Art.Steam,depth=Depth.Front,tints=new[]{A(White,.75f)},count=4,loop=true,
                from=new Vector2(-.8f,-.5f),to=new Vector2(-.6f,-.4f),delay=.4f,stagger=1.2f,life=new Vector2(2,2.4f),size=new Vector2(.4f,.5f),
                velocity=new Vector2(0,.18f),sway=.05f,swayRate=.5f,tilt=8,pop=.5f,fadeOut=.8f,inDark=.4f},
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
        // Riding off into the sunset: the sun goes down behind the boot, dust kicks back off the sole with each of the four
        // hoof beats (WildWest_Gallop, one every HoofEvery), speed lines trail from the heel, the spur's star glints, two
        // birds are far off. Measured on the solved shadow of the 16-piece gym export (2026-11-17): the boot spans
        // x -.67..+.41, y -.64..+.47; the spur's star is at about -.57, -.24 (x -.67..-.47), the heel at x -.48..-.26 with
        // its underside at y -.60, the sole's underside at -.62, the toe's tip at x .41.
        const float HoofEvery=.5f;
        static readonly Color Sunset=MemoryPalette.Hex(0xE8834A),Dust=new Color(.85f,.73f,.55f);
        static Layer[] WildWest()=>new[]
        {
            new Layer{note="The sun going down behind the boot, top right",art=Art.Sun,depth=Depth.Behind,tints=new[]{Sunset},spawn=Spawn.Points,points=new[]{new Vector2(.54f,.31f)},count=1,
                delay=0,life=new Vector2(5.5f,5.5f),size=new Vector2(.6f,.6f),velocity=new Vector2(0,-.06f),spin=new Vector2(4,4),pop=.8f,fadeOut=.8f,inDark=.4f},
            new Layer{note="Dust kicked back off the sole, a puff a hoof beat",art=Art.Puff,depth=Depth.Front,tints=new[]{A(Dust,.75f)},spawn=Spawn.Line,count=4,
                from=new Vector2(.22f,-.69f),to=new Vector2(-.26f,-.64f),delay=.05f,stagger=HoofEvery*3,life=new Vector2(1.1f,1.3f),size=new Vector2(.22f,.3f),
                velocity=new Vector2(-.28f,.07f),drag=.8f,pop=.15f,fadeOut=.7f,inDark=.6f},
            new Layer{note="Smaller dust off the heel, just after each",art=Art.Puff,depth=Depth.Front,tints=new[]{A(Dust,.6f)},spawn=Spawn.Line,count=4,
                from=new Vector2(-.3f,-.66f),to=new Vector2(-.44f,-.68f),delay=.15f,stagger=HoofEvery*3,life=new Vector2(.9f,1.1f),size=new Vector2(.14f,.2f),
                velocity=new Vector2(-.34f,.1f),drag=1f,pop=.12f,fadeOut=.6f,inDark=.6f},
            new Layer{note="Speed lines trailing from the heel and spur",art=Art.Dash,depth=Depth.Front,tints=new[]{A(White,.8f)},spawn=Spawn.Points,
                points=new[]{new Vector2(-.79f,-.17f),new Vector2(-.79f,-.3f),new Vector2(-.6f,-.54f)},count=3,delay=.1f,stagger=.3f,life=new Vector2(1.6f,1.8f),
                size=new Vector2(.2f,.24f),velocity=new Vector2(-.08f,0),pop=.2f,fadeOut=.6f,inDark=.3f},
            new Layer{note="The spur's star glints",art=Art.Sparkle,depth=Depth.Front,tints=new[]{MemoryPalette.GoldBright},spawn=Spawn.Points,
                points=new[]{new Vector2(-.54f,-.2f),new Vector2(-.62f,-.29f),new Vector2(-.51f,-.29f)},count=3,delay=.2f,stagger=1.8f,life=new Vector2(.6f,.8f),
                size=new Vector2(.14f,.2f),spin=new Vector2(30,60),pop=.15f,fadeOut=.4f,inDark=.6f},
            new Layer{note="Two birds far off over the plain",art=Art.Bird,depth=Depth.Behind,tints=new[]{A(White,.85f)},count=2,
                from=new Vector2(.05f,.62f),to=new Vector2(.4f,.8f),delay=1.2f,stagger=1f,life=new Vector2(3.8f,4f),size=new Vector2(.12f,.16f),
                velocity=new Vector2(-.05f,.01f),jitter=.3f,flipRandom=true,pop=.3f,fadeOut=.6f,inDark=.1f},
        };
        // A seal at the summer shore: its flippers slap the shallows with Summer_Splash (white water thrown up, spray
        // glinting as it falls back, ripples running out both ways along the water), then it barks four times
        // (Summer_SealBark from BarkAt, one every BarkEvery), a pair of bark lines popping off its nose with each "arf". The
        // shallows lap along the shore, the sun stands high behind, the sea glints, gulls wheel and a cloud drifts by.
        // Measured on the solved shadow of the user's 13-piece gym export (2026-11-17, the seal side-on facing right, Facing
        // 1): the seal spans x -.65..+.74, y -.70..+.67. Its head is the beach ball wrapped in the flamingo, the sand rake's
        // tines are its whiskers ending at x .74, so Nose is the tip of the snout just behind them; its forked tail (swim fin
        // and flip flop) rises at the top left to y .67; its front flipper (the other swim fin) reaches down and left to
        // -.28, -.70, the lowest point, and Flippers is where it slaps through the shore line at Water. The sky is clear only
        // at the top right, so the sun stands there (SunAt) and the gulls wheel top middle, clear of the tail.
        // Summer's lamp slide (MemorySummerSlide) is drawn round Water and SunAt: redraw it after moving them.
        static readonly Vector2 Nose=new Vector2(.66f,-.02f),Flippers=new Vector2(-.26f,-.68f);
        const float Facing=1;
        public const float Water=-.62f;
        public static readonly Vector2 SunAt=new Vector2(.36f,.55f);
        // MemorySummerSounds spaces the barks by BarkEvery.
        public const float BarkAt=.6f,BarkEvery=.3f;
        static readonly Color Sea=MemoryPalette.Hex(0x40A8DB),Foam=MemoryPalette.Hex(0xD8F1FA);
        // One bark line a bark, a little further from the nose each time: over it (up 1) or under it (up -1).
        static Vector2[] BarkLines(float up)=>Enumerable.Range(0,4).Select(k=>Nose+new Vector2(Facing*(.12f+.03f*k),up*(.06f+.01f*k))).ToArray();
        static Layer[] Summer()=>new[]
        {
            new Layer{note="The summer sun high behind the seal, top right",art=Art.Sun,depth=Depth.Behind,tints=new[]{Sun},spawn=Spawn.Points,points=new[]{SunAt},count=1,
                delay=0,life=new Vector2(5.5f,5.5f),size=new Vector2(.5f,.5f),spin=new Vector2(6,6),pop=.8f,fadeOut=.8f,inDark=.4f},
            new Layer{note="The shallows lapping along the shore",art=Art.Dash,depth=Depth.Front,tints=new[]{A(Sea,.85f),A(Foam,.85f)},spawn=Spawn.Line,count=7,
                from=new Vector2(-1.05f,Water),to=new Vector2(1.05f,Water-.03f),delay=0,stagger=.5f,life=new Vector2(5.5f,5.5f),size=new Vector2(.18f,.22f),
                velocity=new Vector2(.03f,0),sway=.035f,swayRate=.35f,wrap=true,pop=.4f,fadeOut=.8f,inDark=.3f},
            new Layer{note="White water thrown up where the flippers slap the shallows",art=Art.Puff,depth=Depth.Front,tints=new[]{A(Foam,.9f),A(White,.9f)},count=6,
                from=Flippers+new Vector2(-.14f,-.08f),to=Flippers+new Vector2(.1f,0),delay=0,stagger=.15f,life=new Vector2(1f,1.3f),size=new Vector2(.18f,.26f),
                velocity=new Vector2(0,.5f),jitter=.5f,gravity=.9f,drag=.6f,pop=.1f,fadeOut=.6f,inDark=.6f},
            new Layer{note="Spray from the slap, glinting as it falls back",art=Art.Sparkle,depth=Depth.Front,tints=new[]{A(White,.95f),A(Foam,.95f),MemoryPalette.GoldBright},count=12,
                from=Flippers+new Vector2(-.12f,-.04f),to=Flippers+new Vector2(.12f,.1f),delay=.05f,stagger=.4f,life=new Vector2(.9f,1.2f),size=new Vector2(.07f,.11f),
                velocity=new Vector2(-.08f*Facing,.6f),jitter=.7f,gravity=1.3f,spin=new Vector2(90,240),pop=.08f,fadeOut=.4f,inDark=.6f},
            new Layer{note="Ripples running out both ways along the water from the slap",art=Art.Dash,depth=Depth.Front,tints=new[]{A(Foam,.9f),A(Sea,.9f)},spawn=Spawn.Points,
                points=new[]{-1,1,-2,2,-3,3}.Select(k=>new Vector2(Flippers.x+.13f*k,Water+.02f)).ToArray(),count=6,delay=.1f,stagger=.6f,life=new Vector2(1.1f,1.3f),
                size=new Vector2(.12f,.16f),pop=.15f,fadeOut=.6f,inDark=.4f},
            new Layer{note="Bark lines over the nose, one with each arf",art=Art.Dash,depth=Depth.Front,tints=new[]{A(White,.9f)},spawn=Spawn.Points,
                points=BarkLines(1),count=4,delay=BarkAt+.03f,stagger=3*BarkEvery,life=new Vector2(.5f,.6f),size=new Vector2(.13f,.16f),angle=22*Facing,
                pop=.08f,fadeOut=.25f,inDark=.5f},
            new Layer{note="Bark lines under the nose, one with each arf",art=Art.Dash,depth=Depth.Front,tints=new[]{A(White,.9f)},spawn=Spawn.Points,
                points=BarkLines(-1),count=4,delay=BarkAt+.03f,stagger=3*BarkEvery,life=new Vector2(.5f,.6f),size=new Vector2(.13f,.16f),angle=-22*Facing,
                pop=.08f,fadeOut=.25f,inDark=.5f},
            new Layer{note="The sun glinting on the sea behind the seal (the slide's sea, between the shore line and the horizon)",art=Art.Sparkle,depth=Depth.Behind,
                tints=new[]{MemoryPalette.GoldBright,A(White,.95f)},count=6,
                from=new Vector2(-.7f,Water+.08f),to=new Vector2(.6f,Water+.36f),delay=.8f,stagger=3.4f,life=new Vector2(.6f,.9f),size=new Vector2(.09f,.13f),
                spin=new Vector2(30,60),pop=.15f,fadeOut=.4f,inDark=.6f},
            new Layer{note="Gulls wheeling over the shore, between the tail and the sun",art=Art.Bird,depth=Depth.Behind,tints=new[]{A(White,.85f)},count=3,
                from=new Vector2(-.22f,.7f),to=new Vector2(.12f,.88f),delay=1f,stagger=1.1f,life=new Vector2(3.8f,4.2f),size=new Vector2(.13f,.17f),
                velocity=new Vector2(-.06f,.015f),jitter=.3f,sway=.03f,swayRate=.4f,flipRandom=true,pop=.3f,fadeOut=.6f,inDark=.1f},
            new Layer{note="A summer cloud drifting by",art=Art.CloudSmall,depth=Depth.Behind,tints=new[]{A(White,.9f)},count=2,
                from=new Vector2(-1.3f,.62f),to=new Vector2(1.3f,.88f),delay=.3f,stagger=.8f,life=new Vector2(6,6),size=new Vector2(.3f,.38f),
                velocity=new Vector2(-.05f,0),jitter=.3f,wrap=true,pop=.6f,fadeOut=0,inDark=.05f},
        };
    }
}
