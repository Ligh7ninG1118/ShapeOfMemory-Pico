using System;
using System.IO;
using System.Linq;
using PhysicalAssembly.Editor;
using ShadowWorkshopIntegration.Editor;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ShapeOfMemory.Editor
{
    // The deliverable: Scenes/PicoFinal.unity, the MemoryGame director and the play area as real objects (table, stand,
    // book, button panel; ShapeOfMemorySceneLayout), the rest built at runtime. Build makes the scene if there is none and
    // otherwise only adds what is missing: edits made in the scene stay.
    // The scene stays out of the shared build list; its build config builds it alone, under its own package id and name.
    public static class ShapeOfMemoryBuilder
    {
        public const string Root="Assets/ShadowWorkshop/PicoFinal";
        public const string Scene=Root+"/Scenes/PicoFinal.unity";
        public const string IconFolder=Root+"/Art/Icons";
        public const string IconMaterialPath=Root+"/Materials/Memory icon.mat";
        public const string SeeThroughPath=Root+"/Materials/Memory see-through.mat";
        public const string PackageId="com.metahackathon2026.shadow",ProductName="Shape of Memory";
        public const string Apk="Builds/ShapeOfMemory-Pico.apk";
        // Scene, package id, name and APK name for Build and Run, Quest and Pico: keep it in step with the constants above.
        public const string ConfigName="Shape of Memory",ConfigPath=Root+"/Editor/Shape of Memory.asset";
        // MemoryLook.DreamPieces / DreamMaterial: the dream kit (Tools/ShadowArt/build_dream_kit.py writes the FBX and its
        // baked light) on URP Unlit. Out of the shipped scene since 2026-11-11 (env art is redoing the background): the
        // scene build leaves both empty, and the Dream background menu items put them into or take them out of the open
        // scene for previewing.
        public const string DreamKitPath=Root+"/Art/Dream/DreamKit.fbx",DreamAtlasPath=Root+"/Art/Dream/DreamKit.png",DreamMaterialPath=Root+"/Materials/Dream kit.mat";
        public static Mesh[] DreamPieces()=>AssetDatabase.LoadAllAssetsAtPath(DreamKitPath).OfType<Mesh>().OrderBy(m=>m.name).ToArray();
        public static Material DreamMaterial()
        {
            var material=AssetDatabase.LoadAssetAtPath<Material>(DreamMaterialPath);
            if(!material)
            {
                material=new Material(Shader.Find("Universal Render Pipeline/Unlit")){name="Dream kit"};
                AssetDatabase.CreateAsset(material,DreamMaterialPath);
            }
            material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(DreamAtlasPath));material.SetColor("_BaseColor",Color.white);
            EditorUtility.SetDirty(material);return material;
        }
        // MemoryLook.CloudPieces / CloudMaterial: the cloud kit (Tools/ShadowArt/build_cloud_kit.py writes the FBX: geometry,
        // smoothed normals, fold and height in the vertex colour) on ShapeOfMemory/Cloud. In the shipped scene.
        public const string CloudKitPath=Root+"/Art/Clouds/CloudKit.fbx",CloudShaderPath=Root+"/Shaders/Cloud.shader",CloudMaterialPath=Root+"/Materials/Memory cloud.mat";
        public static Mesh[] CloudPieces()
        {
            if(AssetImporter.GetAtPath(CloudKitPath) is ModelImporter importer&&!(importer.materialImportMode==ModelImporterMaterialImportMode.None&&importer.importNormals==ModelImporterNormals.Import
                &&importer.importTangents==ModelImporterTangents.None&&!importer.importAnimation&&!importer.importCameras&&!importer.importLights&&!importer.importBlendShapes&&!importer.isReadable))
            {
                // The file's own normals (the smoothed ones), no materials, nothing the clouds don't use.
                importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.importNormals=ModelImporterNormals.Import;
                importer.importTangents=ModelImporterTangents.None;importer.importAnimation=false;importer.importCameras=false;
                importer.importLights=false;importer.importBlendShapes=false;importer.isReadable=false;importer.meshCompression=ModelImporterMeshCompression.Off;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAllAssetsAtPath(CloudKitPath).OfType<Mesh>().OrderBy(m=>m.name).ToArray();
        }
        public static Material CloudMaterial()
        {
            var material=AssetDatabase.LoadAssetAtPath<Material>(CloudMaterialPath);
            if(!material)
            {
                material=new Material(AssetDatabase.LoadAssetAtPath<Shader>(CloudShaderPath)){name="Memory cloud"};
                AssetDatabase.CreateAsset(material,CloudMaterialPath);
            }
            return material;
        }
        // MemoryLook.RuinPieces / RuinMaterial: the ruin kit (Tools/ShadowArt/build_ruin_kit.py) on the cloud shader with a
        // stone palette: the same light, haze and fade as the clouds, no billow, firmer edges. Set with the clouds.
        public const string RuinKitPath=Root+"/Art/Ruins/RuinKit.fbx",RuinMaterialPath=Root+"/Materials/Memory ruin.mat";
        public static Mesh[] RuinPieces()
        {
            if(AssetImporter.GetAtPath(RuinKitPath) is ModelImporter importer&&!(importer.materialImportMode==ModelImporterMaterialImportMode.None&&importer.importNormals==ModelImporterNormals.Import
                &&importer.importTangents==ModelImporterTangents.None&&!importer.importAnimation&&!importer.importCameras&&!importer.importLights&&!importer.importBlendShapes&&!importer.isReadable))
            {
                importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.importNormals=ModelImporterNormals.Import;
                importer.importTangents=ModelImporterTangents.None;importer.importAnimation=false;importer.importCameras=false;
                importer.importLights=false;importer.importBlendShapes=false;importer.isReadable=false;importer.meshCompression=ModelImporterMeshCompression.Off;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAllAssetsAtPath(RuinKitPath).OfType<Mesh>().OrderBy(m=>m.name).ToArray();
        }
        public static Material RuinMaterial()
        {
            var material=AssetDatabase.LoadAssetAtPath<Material>(RuinMaterialPath);
            if(material)return material;
            material=new Material(AssetDatabase.LoadAssetAtPath<Shader>(CloudShaderPath)){name="Memory ruin"};
            // Weathered stone in the dusk: violet-grey in shadow, warm where the glow reaches; still, firm-edged.
            material.SetColor("_Shadow",new Color(.20f,.17f,.31f));material.SetColor("_Body",new Color(.45f,.39f,.55f));
            material.SetColor("_Lit",new Color(.84f,.62f,.62f));material.SetColor("_Hot",new Color(1f,.82f,.68f));
            material.SetFloat("_Wrap",.2f);material.SetFloat("_HeightShare",.25f);material.SetFloat("_Occlusion",.5f);
            material.SetFloat("_RimGlow",.8f);material.SetFloat("_RimAll",0);material.SetFloat("_RimPower",4);
            material.SetFloat("_EdgeSoft",.25f);material.SetFloat("_EdgeStart",.6f);material.SetFloat("_BillowAmp",0);
            AssetDatabase.CreateAsset(material,RuinMaterialPath);
            return material;
        }
        // Our cloud kit recoloured toward the environment artist's clouds (the Flow scene's: lavender bodies, peach tops,
        // a gold rim), for the Blend look: their colours on our shapes. Made once; tune it by hand after.
        public const string BlendCloudMaterialPath=Root+"/Materials/Memory cloud (blend).mat";
        public static Material BlendCloudMaterial()
        {
            var material=AssetDatabase.LoadAssetAtPath<Material>(BlendCloudMaterialPath);
            if(material)return material;
            material=new Material(CloudMaterial()){name="Memory cloud (blend)"};
            BlendPalette(material);
            AssetDatabase.CreateAsset(material,BlendCloudMaterialPath);
            return material;
        }
        public static void BlendPalette(Material material)
        {
            // Matched by eye and by the colour spread of side views against theirs (2026-11-12): darker violet bodies,
            // muted lavender tops, peach only toward the glow, their gold rim.
            material.SetColor("_Shadow",new Color(.20f,.17f,.36f));material.SetColor("_Body",new Color(.38f,.33f,.56f));
            material.SetColor("_Lit",new Color(.70f,.52f,.62f));material.SetColor("_Hot",new Color(.98f,.70f,.50f));
            material.SetColor("_Rim",new Color(.95f,.64f,.32f));
            EditorUtility.SetDirty(material);
        }
        // The environment artist's ruin models (not the clouds, not the props): every SM_Ruin* prefab.
        public const string ArtPrefabs=Root+"/Art/Art Assets/Prefabs";
        public static GameObject[] RuinModels()=>AssetDatabase.FindAssets("t:Prefab",new[]{ArtPrefabs}).Select(AssetDatabase.GUIDToAssetPath)
            .Where(p=>Path.GetFileName(p).StartsWith("SM_Ruin")).OrderBy(p=>p).Select(AssetDatabase.LoadAssetAtPath<GameObject>).Where(g=>g).ToArray();

        // ---- Looks: which sky, table-top light, clouds, ruins and environment scene round the table (MemoryLook assets in
        // Looks/, one on MemoryGame.Look). The presets are written here; edit an asset by hand to explore, or duplicate one.
        //   Ours    WindwardEveningSky, the game's light, our cloud kit and ruin kit (MemoryClouds), as checked in at cs:158.
        //   Env art the environment artist's Flow sky, their sun, Scenes/PicoFinal_Environment (MemoryEnvironment), none of
        //           ours: Docs/EnvironmentIntegrationHandoff.md.
        //   Mixed   the Flow sky, the game's light, the environment scene without what lies below the player (Ruins_Below,
        //           Clouds_Below), over our cloud sea (our clouds, not our ruins).
        //   Env art, our fog  Env art with its cloud sea's haze between the clouds as the game's own sea had it on 2026-11-12
        //           (the user's version, cs:158): it hazes over from 1.5 m under the sea's tops to 6 m under, and no cloud floor
        //           under the gaps (theirs: CloudSea_Floor). Since cs:189 theirs starts at the same depth and fades over 7 m into
        //           a lighter mist; this one keeps their colours and mist (re-copied by Rebuild presets) with our 4.5 m fade.
        //   Blend   the user's pick (2026-11-12): the Flow sky and their sun; our cloud kit recoloured toward theirs, its sea
        //           swelling a few metres, a few puffs lifted; their ruin models in our layout, standing in the clouds and
        //           leaning a little, a few floating low; the environment scene lends its bake (every group off) and
        //           keeps only its giants, 450-800 m out, for the horizon (EnvironmentGiants; the user asked them back).
        // The glow: Windward's is at 50 in its cubemap and shows straight ahead (facing 0). The Flow sky's is at 30
        // (SkyPanelBuilder.GlowAzimuth) and shows 20 degrees left of forward (facing -20), as the environment scene's bake,
        // rim light and haze assume; our clouds and the sun lighting are lit from there too. Look > <name> sets the open
        // scene; Build uses DefaultLook. An environment scene also goes into the deploy config's Scenes (a build loads it by
        // path).
        public enum Preset{Ours,EnvArt,Mixed,Blend,EnvArtOurFog}
        public const Preset DefaultLook=Preset.EnvArt;
        public const string LooksFolder=Root+"/Looks";
        public const string WindwardSkyPath=Root+"/Materials/WindwardEveningSky.mat",FlowSkyPath=Root+"/Art/Sky/FlowSky.mat",MagicHourSkyPath=Root+"/Materials/MagicHourSky.mat";
        public const float WindwardGlowAzimuth=50,FlowGlowAzimuth=30,MagicHourGlowAzimuth=120,EnvironmentGlowFacing=-20;
        public const string EnvironmentScenePath=Root+"/Scenes/PicoFinal_Environment.unity";
        // Every group under the environment scene's Environment/Art (Cloud_Sea and Ruins_OnSea since env art's cs:172, their
        // cloud sea and the ruins standing in it: off where our own sea and ruins are, Docs/EnvironmentIntegrationHandoff.md T10).
        public static readonly string[] EnvironmentGroups={"Ruins_Near","Ruins_Far","Ruins_Overhead","Ruins_Below","Clouds_Mid","Clouds_Far","Clouds_Overhead","Clouds_Below","Outer_Clusters","Cloud_Sea","Ruins_OnSea"};
        public static readonly string[] MixedHide={"Ruins_Below","Clouds_Below","Cloud_Sea"};
        // Our fog: their cloud sea on a copy of its material with our haze under the tops, without its cloud floor.
        public const string SeaFloor="Cloud_Sea/CloudSea_Floor";
        public const string EnvSeaMaterialPath=Root+"/Art/Art Assets/Materials/MT_CloudSea.mat",OurFogSeaMaterialPath=Root+"/Materials/Env cloud sea (our fog).mat";
        // Their sea's tops lie 8 m under the floor and the eyes 1.36 m over it, as authored (Docs/EnvironmentIntegrationHandoff.md
        // 1.1); ours hazed from MemoryClouds.SeaClear under the tops over MemoryClouds.BaseFade (Cloud.shader's _CloudBase).
        public const float EnvSeaTop=8,EnvEyes=1.36f;
        // Made from theirs if missing; with sync (Rebuild presets), their current values copied over again first, so their
        // colour and mist changes reach it and only the haze's depths stay ours.
        public static Material OurFogSeaMaterial(bool sync=false)
        {
            var material=AssetDatabase.LoadAssetAtPath<Material>(OurFogSeaMaterialPath);
            if(material&&!sync)return material;
            var theirs=AssetDatabase.LoadAssetAtPath<Material>(EnvSeaMaterialPath);if(!theirs){Debug.LogError("SHAPE_OF_MEMORY_LOOK missing "+EnvSeaMaterialPath);return material;}
            bool made=!material;
            if(made)material=new Material(theirs){name="Env cloud sea (our fog)"};
            else{material.shader=theirs.shader;material.CopyPropertiesFromMaterial(theirs);}
            material.SetFloat("_BaseClear",EnvEyes+EnvSeaTop+MemoryClouds.SeaClear);material.SetFloat("_BaseFade",MemoryClouds.BaseFade);
            if(made)AssetDatabase.CreateAsset(material,OurFogSeaMaterialPath);else EditorUtility.SetDirty(material);
            return material;
        }
        public static string LookPath(Preset preset)=>LooksFolder+"/"+(preset==Preset.EnvArt?"Env art":preset==Preset.EnvArtOurFog?"Env art, our fog":preset.ToString())+".asset";
        // The preset's asset, made (and filled) if it isn't there yet; an existing one is left as it is (Rebuild presets
        // writes them all afresh).
        public static MemoryLook Look(Preset preset)
        {
            var look=AssetDatabase.LoadAssetAtPath<MemoryLook>(LookPath(preset));
            if(look)return look;
            Directory.CreateDirectory(LooksFolder);
            look=ScriptableObject.CreateInstance<MemoryLook>();Fill(look,preset);
            AssetDatabase.CreateAsset(look,LookPath(preset));AssetDatabase.SaveAssets();
            Debug.Log("SHAPE_OF_MEMORY_LOOK made "+LookPath(preset));
            return look;
        }
        public static void Fill(MemoryLook look,Preset preset)
        {
            bool flow=preset!=Preset.Ours;
            look.Sky=AssetDatabase.LoadAssetAtPath<Material>(flow?FlowSkyPath:WindwardSkyPath);
            look.GlowAzimuth=flow?FlowGlowAzimuth:WindwardGlowAzimuth;look.GlowFacing=flow?EnvironmentGlowFacing:0;
            bool env=preset==Preset.EnvArt||preset==Preset.EnvArtOurFog;
            look.Lighting=env||preset==Preset.Blend?MemoryLighting.Setup.Sun:MemoryLighting.Setup.Game;
            bool clouds=!env;
            look.CloudPieces=clouds?CloudPieces():Array.Empty<Mesh>();
            look.CloudMaterial=!clouds?null:preset==Preset.Blend?BlendCloudMaterial():CloudMaterial();
            look.CloudsUnderFeet=false;
            look.CloudShape=MemoryClouds.Shape.Flat;
            if(preset==Preset.Blend){look.CloudShape.SeaSwell=3;look.CloudShape.Lifted=6;}
            look.RuinPieces=preset==Preset.Ours?RuinPieces():Array.Empty<Mesh>();look.RuinMaterial=preset==Preset.Ours?RuinMaterial():null;
            look.RuinModels=preset==Preset.Blend?RuinModels():Array.Empty<GameObject>();
            look.Ruins=MemoryClouds.RuinLayout.Standing;
            if(preset==Preset.Blend){look.Ruins.FloatAbove=new Vector2(2,8);look.Ruins.Lean=15;}
            look.EnvironmentScene=flow?EnvironmentScenePath:"";
            look.EnvironmentHide=preset==Preset.Mixed?MixedHide.ToArray():preset==Preset.Blend?EnvironmentGroups.ToArray():preset==Preset.EnvArtOurFog?new[]{SeaFloor}:Array.Empty<string>();
            look.EnvironmentGiants=preset==Preset.Blend;
            look.EnvironmentSeaMaterial=preset==Preset.EnvArtOurFog?OurFogSeaMaterial(true):null;
            // The environment's own looks take the one fog for its clouds and ruins (2026-11-14: the user asked for the
            // game's 11/12 fog back, and for softer edges between clouds and buildings); the others keep each
            // material's own haze, as our cloud kit has its own.
            look.Fog=new MemoryFog{On=env};
            look.DreamPieces=Array.Empty<Mesh>();look.DreamMaterial=null;
            EditorUtility.SetDirty(look);
        }
        // Puts look on the game, and its lighting into the scene (key light and ambient, as at the stage's rest), so the
        // editor shows what the game will; an environment scene goes into the deploy config.
        public static void Use(MemoryGame game,MemoryLook look)
        {
            game.Look=look;
            if(look)
            {
                MemoryLighting.Apply(look.Lighting,MemoryLighting.FindKey(game.gameObject.scene),0,look.GlowFacing);
                if(look.HasEnvironment)ShipEnvironmentScene(look.EnvironmentScene);
            }
            EditorUtility.SetDirty(game);EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
            Debug.Log($"SHAPE_OF_MEMORY_LOOK {(look?look.ToString():"none")} in {game.gameObject.scene.name}: save the scene to keep it");
        }
        // The environment scene after PicoFinal in the deploy config's Scenes (left there when the look changes back: it
        // only costs APK size while nothing loads it).
        static void ShipEnvironmentScene(string path)
        {
            var config=AssetDatabase.LoadAssetAtPath<DeployConfig>(ConfigPath);
            var scene=AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
            if(!config||!scene||config.Scenes.Contains(scene))return;
            config.Scenes=config.Scenes.Concat(new[]{scene}).ToArray();
            EditorUtility.SetDirty(config);AssetDatabase.SaveAssetIfDirty(config);
            Debug.Log("SHAPE_OF_MEMORY_LOOK added "+path+" to "+ConfigPath+" Scenes");
        }
        [MenuItem("MetaHackthone/Pico Final/Look/Ours (Windward sky, our clouds and ruins)")]
        public static void LookOurs()=>SetLook(Preset.Ours);
        [MenuItem("MetaHackthone/Pico Final/Look/Env art (Flow sky, their sun, their scene)")]
        public static void LookEnvArt()=>SetLook(Preset.EnvArt);
        [MenuItem("MetaHackthone/Pico Final/Look/Mixed (Flow sky, their scene above, our cloud sea below)")]
        public static void LookMixed()=>SetLook(Preset.Mixed);
        [MenuItem("MetaHackthone/Pico Final/Look/Blend (Flow sky, their sun, our clouds, their ruins in our layout)")]
        public static void LookBlend()=>SetLook(Preset.Blend);
        [MenuItem("MetaHackthone/Pico Final/Look/Env art, our fog (their scene, our haze between the clouds)")]
        public static void LookEnvArtOurFog()=>SetLook(Preset.EnvArtOurFog);
        static void SetLook(Preset preset)
        {
            var game=UnityEngine.Object.FindFirstObjectByType<MemoryGame>();
            if(!game){Debug.LogWarning("SHAPE_OF_MEMORY_LOOK no MemoryGame in the open scene");return;}
            Undo.RecordObject(game,"Look "+preset);
            Undo.RecordObjects(new UnityEngine.Object[]{MemoryLighting.FindKey(game.gameObject.scene)?.transform,MemoryLighting.FindKey(game.gameObject.scene)}.Where(o=>o).ToArray(),"Look lighting");
            Use(game,Look(preset));ScenePreview.Refresh();
        }
        [MenuItem("MetaHackthone/Pico Final/Look/Rebuild presets (overwrites the five assets)")]
        public static void RebuildLooks()
        {
            foreach(Preset preset in Enum.GetValues(typeof(Preset))){var look=Look(preset);Fill(look,preset);}
            AssetDatabase.SaveAssets();Debug.Log("SHAPE_OF_MEMORY_LOOK presets rebuilt in "+LooksFolder);
        }

        // Makes the scene if there is none; otherwise opens it and adds only what is missing (the play area's pieces,
        // ShapeOfMemorySceneLayout.Add), leaving everything already there as it is.
        [MenuItem("MetaHackthone/Pico Final/Create scene or add what it is missing")]
        public static void Build()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play mode first.");
            for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Save open scenes first.");
            var player=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MetaHackthone/Generated/MetaHackathonPlayer.prefab");
            if(!player)throw new InvalidOperationException("The shared player prefab (Assets/MetaHackthone/Generated/MetaHackathonPlayer.prefab) is missing.");
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                if(File.Exists(Scene))
                {
                    var open=EditorSceneManager.OpenScene(Scene,OpenSceneMode.Single);
                    var existing=UnityEngine.Object.FindFirstObjectByType<MemoryGame>();
                    if(existing)
                    {
                        int added=ShapeOfMemorySceneLayout.Add(existing);
                        if(added>0)EditorSceneManager.SaveScene(open);
                        Debug.Log($"SHAPE_OF_MEMORY_SCENE_READY kept, {added} pieces added");return;
                    }
                }
                var icons=ImportIcons();var iconMaterial=IconMaterial();
                Directory.CreateDirectory(Path.GetDirectoryName(Scene));
                var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var game=new GameObject("Shape of Memory").AddComponent<MemoryGame>();
                game.StandalonePlayer=player;game.IconMaterial=iconMaterial;game.Icons=icons;
                game.Surface=AssetDatabase.LoadAssetAtPath<Material>("Assets/ShadowWorkshop/Materials/Graybox surfaces.mat");
                game.Screen=AssetDatabase.LoadAssetAtPath<Material>("Assets/ShadowWorkshop/Materials/Projection screen.mat");
                game.Glow=AssetDatabase.LoadAssetAtPath<Material>("Assets/ShadowWorkshop/Materials/Tracked hands and lamp.mat");
                game.SeeThrough=SeeThroughMaterial();
                game.HandLook=AssetDatabase.LoadAssetAtPath<MemoryHandLook>(MemoryHandTools.InkPath);
                // The table-top's key light (the pile's materials are URP Lit): its colour, strength and direction, and
                // the ambient, come from the look (MemoryLighting).
                var light=new GameObject("Key light").AddComponent<Light>();light.type=LightType.Directional;light.shadows=LightShadows.None;
                Use(game,Look(DefaultLook));ShapeOfMemorySceneLayout.Add(game);
                EditorSceneManager.SaveScene(scene,Scene);AssetDatabase.SaveAssets();
                Debug.Log("SHAPE_OF_MEMORY_SCENE_READY icons="+icons.Length);
            }
            finally{if(setup.Any(s=>s.isLoaded&&s.isActive))EditorSceneManager.RestoreSceneManagerSetup(setup);}
        }
        // The dream kit into or out of the open scene's look (the asset itself: every scene using that look gets it).
        [MenuItem("MetaHackthone/Pico Final/Dream background/Add to the open scene's look")]
        public static void AddDream()=>SetDream(true);
        [MenuItem("MetaHackthone/Pico Final/Dream background/Remove from the open scene's look")]
        public static void RemoveDream()=>SetDream(false);
        static void SetDream(bool on)
        {
            var game=UnityEngine.Object.FindFirstObjectByType<MemoryGame>();
            if(!game||!game.Look){Debug.LogWarning("SHAPE_OF_MEMORY_DREAM no MemoryGame with a look in the open scene");return;}
            Undo.RecordObject(game.Look,on?"Add dream background":"Remove dream background");
            game.Look.DreamPieces=on?DreamPieces():Array.Empty<Mesh>();game.Look.DreamMaterial=on?DreamMaterial():null;
            EditorUtility.SetDirty(game.Look);AssetDatabase.SaveAssetIfDirty(game.Look);
            Debug.Log($"SHAPE_OF_MEMORY_DREAM {(on?$"added ({game.Look.DreamPieces.Length} pieces)":"removed")} in look {game.Look.name}");
        }

        [MenuItem("MetaHackthone/Pico Final/Play")]
        public static void Play()
        {
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            if(!File.Exists(Scene))Build();
            EditorSceneManager.OpenScene(Scene);EditorApplication.isPlaying=true;
        }
        // The editor's own progress, and the game's on every connected headset: the headset keeps its PlayerPrefs in the
        // app's data, so clearing that data resets it (the app, if running, closes and starts fresh next time).
        // That data holds the session logs too, so they are pulled first (Pull session logs).
        [MenuItem("MetaHackthone/Pico Final/Clear saved progress")]
        public static void ClearProgress()
        {
            // Every Torch2 level's keys, not only the chapters': a level swapped out keeps its saves otherwise.
            var ids=MemoryProgress.Levels.Concat(Resources.LoadAll<CloudGarden.ShadowPuzzle.Torch2Level>(CloudGarden.ShadowPuzzle.Torch2Level.ResourceFolder.TrimEnd('/')).Select(l=>l.name)).Distinct();
            foreach(var level in ids)foreach(var key in new[]{"Solved.","Hints.","Intro."})PlayerPrefs.DeleteKey("ShapeOfMemory."+key+level);
            PlayerPrefs.DeleteKey("ShapeOfMemory.Lift");PlayerPrefs.DeleteKey("ShapeOfMemory.Taught");PlayerPrefs.Save();
            PullLogs(false);
            var cleared=new System.Collections.Generic.List<string>();Headsets.Refresh();
            foreach(var headset in Headsets.Connected)
                if(headset.Status==Headsets.Status.Ready)cleared.Add(headset.Name+"="+Headsets.Adb($"-s {headset.Serial} shell pm clear {PackageId}",out _).Trim());
            Debug.Log("SHAPE_OF_MEMORY_PROGRESS_CLEARED editor devices: "+(cleared.Count>0?string.Join(", ",cleared):"none connected"));
        }
        // Every connected headset's session logs (MemoryLog) into Logs/ShapeOfMemory/<headset>/logs, then shows the folder.
        // Clear saved progress deletes them on the headset, so pull first.
        public const string PulledLogs="Logs/ShapeOfMemory";
        [MenuItem("MetaHackthone/Pico Final/Pull session logs")]
        public static void PullLogs()=>PullLogs(true);
        public static void PullLogs(bool reveal)
        {
            var pulled=new System.Collections.Generic.List<string>();Headsets.Refresh();string root=Path.GetFullPath(PulledLogs);
            foreach(var headset in Headsets.Connected)
            {
                if(headset.Status!=Headsets.Status.Ready)continue;
                string folder=Path.Combine(root,string.Concat((headset.Name+" "+headset.Serial).Select(c=>Path.GetInvalidFileNameChars().Contains(c)||c==' '?'_':c)));
                Directory.CreateDirectory(folder);
                string output=Headsets.Adb($"-s {headset.Serial} pull /sdcard/Android/data/{PackageId}/files/{MemoryLog.Folder} \"{folder}\"",out int exit,120000).Trim();
                pulled.Add(headset.Name+": "+(exit==0?output.Split('\n').Last().Trim():"failed: "+output));
            }
            Debug.Log("SHAPE_OF_MEMORY_LOGS_PULLED "+(pulled.Count>0?string.Join("; ",pulled):"no headset connected")+" -> "+root);
            if(reveal&&pulled.Count>0&&Directory.Exists(root))EditorUtility.RevealInFinder(root);
        }
        // Kept for habit and for MCP (execute_menu_item); MetaHackthone > Build and Run does the same with the
        // "Shape of Memory" config, for Quest too, and installs and starts it.
        [MenuItem("MetaHackthone/Pico Final/Build Pico APK")]
        public static void BuildPico()=>Reveal(BuildPico(false));
        [MenuItem("MetaHackthone/Pico Final/Build Pico APK (development)")]
        public static void BuildPicoDevelopment()=>Reveal(BuildPico(true));
        static void Reveal(BuildReport report){if(report.summary.result==BuildResult.Succeeded&&File.Exists(Apk))EditorUtility.RevealInFinder(Path.GetFullPath(Apk));}
        // The gym's Run on headset: export, then start this game at the level on the Build and Run headset (a book of its
        // own unless it is one of the four), building only when the level changed since the last build.
        [InitializeOnLoadMethod] static void HookGym()=>Torch2GymTools.RunOnHeadset=gym=>
        {
            var level=Torch2GymTools.Export(gym);if(!level)return;string id=level.name;
            var config=DeployConfig.Find(ConfigName);if(!config){Debug.LogError($"The {ConfigName} build config is missing ({ConfigPath}).");return;}
            BuildAndRunWindow.Open();BuildAndRun.RunLevel(config,id);
        };
        public static BuildReport BuildPico(bool development)
        {
            if(!File.Exists(Scene))Build();
            var config=DeployConfig.Find(ConfigName)??throw new InvalidOperationException($"The {ConfigName} build config is missing ({ConfigPath}).");
            return BuildRunner.Build(config,DevicePlatform.Pico,development);
        }
        // White-on-transparent PNGs from Tools/ShadowArt/build_icons.py: sharp, no mips (they are small and close).
        static Texture2D[] ImportIcons()
        {
            var paths=Directory.Exists(IconFolder)?Directory.GetFiles(IconFolder,"*.png").Select(p=>p.Replace('\\','/')).OrderBy(p=>p).ToArray():Array.Empty<string>();
            foreach(var path in paths)
            {
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);if(importer==null)continue;
                if(importer.alphaIsTransparency&&!importer.mipmapEnabled&&importer.wrapMode==TextureWrapMode.Clamp)continue;
                importer.textureType=TextureImporterType.Default;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;
                importer.wrapMode=TextureWrapMode.Clamp;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
            }
            return paths.Select(AssetDatabase.LoadAssetAtPath<Texture2D>).Where(t=>t).ToArray();
        }
        // URP Unlit with alpha clipping, so icons need no sorting.
        static Material IconMaterial()
        {
            var material=AssetDatabase.LoadAssetAtPath<Material>(IconMaterialPath);
            if(!material)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(IconMaterialPath));
                material=new Material(Shader.Find("Universal Render Pipeline/Unlit")){name="Memory icon"};AssetDatabase.CreateAsset(material,IconMaterialPath);
            }
            material.SetFloat("_AlphaClip",1);material.SetFloat("_Cutoff",.5f);material.EnableKeyword("_ALPHATEST_ON");material.SetFloat("_Cull",0);
            material.renderQueue=(int)RenderQueue.AlphaTest;EditorUtility.SetDirty(material);return material;
        }
        // URP Unlit, transparent, alpha blended (MemoryGame.SeeThrough): an asset the scene points at, so the build keeps
        // URP Unlit's transparent variant (no other material in the build uses it). Copies at runtime set depth, culling
        // and queue (MemoryKit.Stroke, MemorySky's dark).
        public static Material SeeThroughMaterial()
        {
            var material=AssetDatabase.LoadAssetAtPath<Material>(SeeThroughPath);
            if(!material)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SeeThroughPath));
                material=new Material(Shader.Find("Universal Render Pipeline/Unlit")){name="Memory see-through"};AssetDatabase.CreateAsset(material,SeeThroughPath);
            }
            material.SetFloat("_Surface",1);material.SetFloat("_Blend",0);material.SetFloat("_AlphaClip",0);material.SetFloat("_Cull",(float)CullMode.Back);
            material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha",(float)BlendMode.One);material.SetFloat("_DstBlendAlpha",(float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite",0);material.SetColor("_BaseColor",Color.white);
            material.DisableKeyword("_ALPHATEST_ON");material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType","Transparent");material.renderQueue=(int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);return material;
        }
    }
}
