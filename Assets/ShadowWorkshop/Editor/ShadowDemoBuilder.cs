using System;
using System.IO;
using System.Linq;
using CloudGarden.Integration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShadowWorkshopIntegration.Editor
{
    public static class ShadowDemoBuilder
    {
        public const string Entry = "Assets/ShadowWorkshop/Scenes/ThreeWishes.unity";
        public const string Everyday = "Assets/ShadowWorkshop/Scenes/EverydayObjects.unity";
        public const string Torch = "Assets/ShadowWorkshop/Scenes/TorchLevels.unity";
        public const string Torch2 = "Assets/ShadowWorkshop/Scenes/TorchLevels2.unity";
        // Iteration 2 is self-contained: its scene cycles only its own levels.
        public static readonly string[] Torch2Levels = {"Birthday","BonVoyage","GoodMorning","Home"};
        [MenuItem("MetaHackthone/Shadow Workshop/Create or rebuild scenes")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
            for (int i=0;i<SceneManager.sceneCount;i++) if(SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes first.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            Directory.CreateDirectory("Assets/ShadowWorkshop/Scenes"); AssetDatabase.Refresh();
            try
            {
                var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MetaHackthone/Generated/MetaHackathonPlayer.prefab");
                if (!player) throw new InvalidOperationException("The shared player prefab (Assets/MetaHackthone/Generated/MetaHackathonPlayer.prefab) is missing.");
                foreach (var level in new[]{"ThreeWishes","MoonCourier","ShadowWorkshop"})
                {
                    string path = "Assets/ShadowWorkshop/Scenes/"+level+".unity";
                    CreateScene(level, path, player); AddToBuild(path);
                }
                CreateScene("ALife", Everyday, player); AddToBuild(Everyday);
                CreateScene("Encore", Torch, player);
                CreateScene(Torch2Levels[0], Torch2, player, Torch2Levels);
                AssetDatabase.SaveAssets(); Debug.Log("SHADOW_WORKSHOP_SCENES_READY");
            }
            finally
            {
                if(setup.Any(s=>s.isLoaded && s.isActive)) EditorSceneManager.RestoreSceneManagerSetup(setup);
                else EditorSceneManager.OpenScene(Entry);
            }
        }
        static void AddToBuild(string path) => EditorBuildSettings.scenes = EditorBuildSettings.scenes.Where(s=>s.path!=path).Append(new EditorBuildSettingsScene(path,true)).ToArray();
        static void CreateScene(string level, string path, GameObject player, string[] playlist = null)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var demo = new GameObject(level + " shared-player entry").AddComponent<ShadowDemo>();
            demo.InitialLevel = level; demo.StandalonePlayer = player; demo.Playlist = playlist;
            demo.Surface = AssetDatabase.LoadAssetAtPath<Material>("Assets/ShadowWorkshop/Materials/Graybox surfaces.mat");
            demo.Screen = AssetDatabase.LoadAssetAtPath<Material>("Assets/ShadowWorkshop/Materials/Projection screen.mat");
            demo.Glow = AssetDatabase.LoadAssetAtPath<Material>("Assets/ShadowWorkshop/Materials/Tracked hands and lamp.mat");
            var light = new GameObject("Shadow workshop lighting").AddComponent<Light>(); light.type = LightType.Directional;
            light.intensity = 1.2f; light.color = new Color(1,.91f,.78f); light.shadows = LightShadows.None;
            light.transform.rotation = Quaternion.Euler(48,-28,0);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(.55f,.65f,.70f);
            EditorSceneManager.SaveScene(scene,path);
        }
        // The everyday-objects entry scene opens on
        // A LIFE. Every shadow level is still reachable from any shadow scene through PREV / NEXT.
        [MenuItem("MetaHackthone/Shadow Workshop/Create everyday-objects scene")]
        public static void BuildEveryday() => BuildEntry("ALife", Everyday, true, "EVERYDAY_SCENE_READY");
        // Direct editor entry for the flashlight levels. PREV / NEXT also reach them from any shadow scene, so this
        // scene stays out of the build list.
        [MenuItem("MetaHackthone/Shadow Workshop/Create torch-levels scene")]
        public static void BuildTorch() => BuildEntry("Encore", Torch, false, "TORCH_SCENE_READY");
        // Second iteration: fixed lamps, the player poses the pile. Not in the build list, like TorchLevels.
        [MenuItem("MetaHackthone/Shadow Workshop/Create torch-levels-2 scene")]
        public static void BuildTorch2() => BuildEntry(Torch2Levels[0], Torch2, false, "TORCH2_SCENE_READY", Torch2Levels);
        static void BuildEntry(string level, string path, bool register, string done, string[] playlist = null)
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
            for (int i=0;i<SceneManager.sceneCount;i++) if(SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes first.");
            var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MetaHackthone/Generated/MetaHackathonPlayer.prefab");
            if (!player) throw new InvalidOperationException("The shared player prefab (Assets/MetaHackthone/Generated/MetaHackathonPlayer.prefab) is missing.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try { CreateScene(level, path, player, playlist); if (register) AddToBuild(path); AssetDatabase.SaveAssets(); Debug.Log(done); }
            finally { if(setup.Any(s=>s.isLoaded && s.isActive)) EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }
        [MenuItem("MetaHackthone/Shadow Workshop/Play Everyday Objects")]
        public static void PlayEveryday() => PlayEntry(Everyday, BuildEveryday);
        [MenuItem("MetaHackthone/Shadow Workshop/Play Torch Levels")]
        public static void PlayTorch() => PlayEntry(Torch, BuildTorch);
        [MenuItem("MetaHackthone/Shadow Workshop/Play Torch Levels 2")]
        public static void PlayTorch2() => PlayEntry(Torch2, BuildTorch2);
        static void PlayEntry(string path, Action build)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(path)) build();
            EditorSceneManager.OpenScene(path); EditorApplication.isPlaying = true;
        }
        [MenuItem("MetaHackthone/Shadow Workshop/Play Three Wishes")]
        public static void Play()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(Entry); EditorApplication.isPlaying = true;
        }
        [MenuItem("MetaHackthone/Shadow Workshop/Build Windows preview")]
        public static void BuildWindows()
        {
            Directory.CreateDirectory("Builds/ShadowWorkshop");
            // Meta's post-build hook copies without overwrite. Only remove its
            // generated output so repeated previews receive the current bindings.
            const string bindings="Builds/ShadowWorkshop/RuntimeActionBindings.json";
            if(File.Exists(bindings))File.Delete(bindings);
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),
                target=BuildTarget.StandaloneWindows64,
                locationPathName="Builds/ShadowWorkshop/MetaHackathon.exe",
                options=BuildOptions.Development
            });
            if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded || report.summary.totalErrors>0)
                throw new InvalidOperationException("Shadow workshop build failed: "+report.summary.result+", errors="+report.summary.totalErrors);
            Debug.Log("SHADOW_WORKSHOP_WINDOWS_BUILD_OK errors="+report.summary.totalErrors);
        }
    }
}
