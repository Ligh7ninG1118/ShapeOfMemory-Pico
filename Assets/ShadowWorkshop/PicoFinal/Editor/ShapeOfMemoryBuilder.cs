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
    // The deliverable: Scenes/PicoFinal.unity holds only the MemoryGame director (it builds everything else at runtime).
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

        [MenuItem("MetaHackthone/Pico Final/Create or rebuild scene")]
        public static void Build()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play mode first.");
            for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Save open scenes first.");
            var player=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MetaHackthone/Generated/MetaHackathonPlayer.prefab");
            if(!player)throw new InvalidOperationException("The shared player prefab (Assets/MetaHackthone/Generated/MetaHackathonPlayer.prefab) is missing.");
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var icons=ImportIcons();var iconMaterial=IconMaterial();
                Directory.CreateDirectory(Path.GetDirectoryName(Scene));
                var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var game=new GameObject("Shape of Memory").AddComponent<MemoryGame>();
                game.StandalonePlayer=player;game.IconMaterial=iconMaterial;game.Icons=icons;
                game.Surface=AssetDatabase.LoadAssetAtPath<Material>("Assets/ShadowWorkshop/Materials/Graybox surfaces.mat");
                game.Screen=AssetDatabase.LoadAssetAtPath<Material>("Assets/ShadowWorkshop/Materials/Projection screen.mat");
                game.Glow=AssetDatabase.LoadAssetAtPath<Material>("Assets/ShadowWorkshop/Materials/Tracked hands and lamp.mat");
                game.SkyMaterial=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/WindwardEveningSky.mat");game.SkyGlowAzimuth=50;
                game.SeeThrough=SeeThroughMaterial();
                // Same light as the other shadow scenes: the pile's materials are URP Lit.
                var light=new GameObject("Shape of Memory lighting").AddComponent<Light>();light.type=LightType.Directional;
                light.intensity=1.2f;light.color=new Color(1,.91f,.78f);light.shadows=LightShadows.None;light.transform.rotation=Quaternion.Euler(48,-28,0);
                RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.55f,.65f,.70f);
                EditorSceneManager.SaveScene(scene,Scene);AssetDatabase.SaveAssets();
                Debug.Log("SHAPE_OF_MEMORY_SCENE_READY icons="+icons.Length);
            }
            finally{if(setup.Any(s=>s.isLoaded&&s.isActive))EditorSceneManager.RestoreSceneManagerSetup(setup);}
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
