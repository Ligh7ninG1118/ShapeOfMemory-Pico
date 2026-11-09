using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PhysicalAssembly.Editor
{
    // Pick a game and a headset, press Build and Run. Everything else here is for when that isn't enough.
    public sealed class BuildAndRunWindow : EditorWindow
    {
        [MenuItem("MetaHackthone/Build and Run...",false,-1000)] public static void Open()=>GetWindow<BuildAndRunWindow>("Build and Run");
        // For MCP (execute_menu_item: builds can't start inside the player loop) and anyone who prefers the menu.
        [MenuItem("MetaHackthone/Build and Run again",false,-999)] static void Again()=>BuildAndRun.BuildAndRunNow();

        DeployConfig[] configs=new DeployConfig[0];
        int seenVersion=-1; double nextPoll; Vector2 scroll;

        void OnEnable()
        {
            titleContent=new GUIContent("Build and Run"); minSize=new Vector2(320,300);
            configs=DeployConfig.All(); BuildRunner.Recover(); Headsets.RefreshInBackground();
            BuildAndRun.Changed+=Repaint;
        }
        void OnDisable()=>BuildAndRun.Changed-=Repaint;
        void OnProjectChange() { configs=DeployConfig.All(); Repaint(); }
        void Update()
        {
            if(EditorApplication.timeSinceStartup>nextPoll) { nextPoll=EditorApplication.timeSinceStartup+3; Headsets.RefreshInBackground(); }
            if(seenVersion!=Headsets.Version) { seenVersion=Headsets.Version; Repaint(); BuildAndRunToolbar.Refresh(); }
        }

        void OnGUI()
        {
            scroll=EditorGUILayout.BeginScrollView(scroll); EditorGUIUtility.labelWidth=70;
            var config=BuildAndRun.Config;
            if(!config)
            {
                EditorGUILayout.HelpBox("No build configs in the project. Create one with Assets > Create > MetaHackthone > Build and Run config.",MessageType.Warning);
                EditorGUILayout.EndScrollView(); return;
            }
            bool busy=BuildPipeline.isBuildingPlayer || EditorApplication.isPlayingOrWillChangePlaymode;
            var headset=BuildAndRun.Target(config); var platform=BuildAndRun.PlatformFor(config,headset);
            using(new EditorGUI.DisabledScope(busy))
            {
                using(new EditorGUILayout.HorizontalScope())
                {
                    int index=Math.Max(0,Array.IndexOf(configs,config));
                    int picked=EditorGUILayout.Popup(new GUIContent("Game","What to build. Each entry is a Build and Run config asset."),index,configs.Select(c=>c.name).ToArray());
                    if(picked!=index && picked<configs.Length) BuildAndRun.Config=configs[picked];
                    if(GUILayout.Button(new GUIContent("Edit","Select the config asset"),EditorStyles.miniButton,GUILayout.Width(40))) { Selection.activeObject=config; EditorGUIUtility.PingObject(config); }
                }
                HeadsetRow(config);
                if(headset==null)
                {
                    EditorGUILayout.HelpBox(BuildAndRun.Serial==BuildAndRun.NoHeadset?"Build only: pick the platform to build for.":BuildAndRun.HeadsetAdvice(config),
                        BuildAndRun.Serial==BuildAndRun.NoHeadset?MessageType.None:MessageType.Warning);
                    var platforms=Enum.GetValues(typeof(DevicePlatform)).Cast<DevicePlatform>().Where(config.Supports).ToArray();
                    int index=Math.Max(0,Array.IndexOf(platforms,platform));
                    int picked=EditorGUILayout.Popup("Platform",index,platforms.Select(p=>p.ToString()).ToArray());
                    if(picked!=index) BuildAndRun.Platform=platforms[picked];
                }
                LevelRow(config,platform);
                EditorGUILayout.Space(4);
                bool development=Toggle(BuildAndRun.Development,"Development build","Debug keys, the Profiler and full logs. Turn it off for the build you hand in.");
                if(development!=BuildAndRun.Development) BuildAndRun.Development=development;
                bool logs=Toggle(BuildAndRun.CaptureLogs,"Save headset log","While the app runs, its log streams to Logs/Headsets.");
                if(logs!=BuildAndRun.CaptureLogs) BuildAndRun.CaptureLogs=logs;
                BuildAndRun.Clean=Toggle(BuildAndRun.Clean,"Clean build","Rebuild everything from scratch (slow). For when a build behaves oddly.");
                EditorGUILayout.Space(6);
                using(new EditorGUI.DisabledScope(headset==null))
                    if(GUILayout.Button(new GUIContent(headset!=null?$"Build and Run on {headset.Name}":"Build and Run",$"Build {config.name} for {platform}, install it and start it."),GUILayout.Height(34))) Later(BuildAndRun.BuildAndRunNow);
                using(new EditorGUILayout.HorizontalScope())
                {
                    if(GUILayout.Button(new GUIContent("Build only",$"Build {Path.GetFileName(config.ApkPath(platform))} without installing it."))) Later(BuildAndRun.BuildOnly);
                    using(new EditorGUI.DisabledScope(headset==null || !File.Exists(config.ApkPath(platform))))
                        if(GUILayout.Button(new GUIContent("Install and Run","Install the last build without building again."))) Later(BuildAndRun.InstallAndRun);
                }
                using(new EditorGUILayout.HorizontalScope())
                using(new EditorGUI.DisabledScope(headset==null))
                {
                    if(GUILayout.Button(new GUIContent("Restart","Start the installed app again, from the beginning."),EditorStyles.miniButtonLeft)) Later(BuildAndRun.RunOnly);
                    if(GUILayout.Button(new GUIContent("Stop","Close the app on the headset."),EditorStyles.miniButtonMid)) Later(BuildAndRun.Stop);
                    if(GUILayout.Button(new GUIContent("Clear data","Delete the app's saved progress on the headset."),EditorStyles.miniButtonMid)) Later(BuildAndRun.ClearData);
                    if(GUILayout.Button(new GUIContent("Uninstall","Remove the app from the headset."),EditorStyles.miniButtonRight)) Later(BuildAndRun.Uninstall);
                }
            }
            EditorGUILayout.Space(6);
            LastBuild(config.ApkPath(platform));
            StaleRow(config,platform);
            LogRow();
            if(BuildAndRun.Status!="") EditorGUILayout.HelpBox(BuildAndRun.Status,BuildAndRun.StatusType);
            EditorGUILayout.EndScrollView(); EditorGUIUtility.labelWidth=0;
        }

        void HeadsetRow(DeployConfig config)
        {
            var labels=new List<string> { "Any headset" }; var values=new List<string> { BuildAndRun.AnyHeadset };
            foreach(var h in Headsets.Connected) { labels.Add(h.Label); values.Add(h.Serial); }
            string serial=BuildAndRun.Serial;
            if(serial!=BuildAndRun.AnyHeadset && serial!=BuildAndRun.NoHeadset && !values.Contains(serial)) { labels.Add($"Not connected ({serial})"); values.Add(serial); }
            labels.Add("No headset (build only)"); values.Add(BuildAndRun.NoHeadset);
            using(new EditorGUILayout.HorizontalScope())
            {
                int index=Math.Max(0,values.IndexOf(serial));
                int picked=EditorGUILayout.Popup(new GUIContent("Headset","Headsets plugged into this computer by USB."),index,labels.ToArray());
                if(picked!=index) BuildAndRun.Serial=values[picked];
                if(GUILayout.Button(new GUIContent("Refresh","Look for headsets again"),EditorStyles.miniButton,GUILayout.Width(56))) { Headsets.Refresh(); BuildAndRunToolbar.Refresh(); }
            }
        }
        string stale,staleKey; double staleAt;
        void LevelRow(DeployConfig config,DevicePlatform platform)
        {
            var levels=config.Levels; if(levels.Length==0) return;
            string level=BuildAndRun.LevelOf(config); int index=Array.IndexOf(levels,level)+1;
            int picked=EditorGUILayout.Popup(new GUIContent("Start at",$"Start the game at this level instead of its title. The levels are the assets in {config.LevelFolder}."),index,new[] { "Start normally" }.Concat(levels).ToArray());
            if(picked!=index) { level=picked==0?"":levels[picked-1]; BuildAndRun.SetLevel(config,level); }
        }
        // Under the last build: whether it still matches the code and the picked level.
        void StaleRow(DeployConfig config,DevicePlatform platform)
        {
            if(!File.Exists(config.ApkPath(platform))) return;
            string level=BuildAndRun.LevelOf(config),key=config.name+platform+level;
            // Every 2 s at most: the check walks the assemblies and the level's dependencies.
            if(key!=staleKey || EditorApplication.timeSinceStartup>staleAt) { staleKey=key; staleAt=EditorApplication.timeSinceStartup+2; stale=BuildRunner.Stale(config,platform,level); }
            if(stale!=null) EditorGUILayout.HelpBox($"Out of date: {stale}. Build and Run to include it.",MessageType.Warning);
            else if(level!="") EditorGUILayout.LabelField($"Up to date with {level}: Restart starts it there without building, if this build is installed.",EditorStyles.wordWrappedMiniLabel);
        }
        static void LastBuild(string apk)
        {
            var file=new FileInfo(apk);
            using(new EditorGUILayout.HorizontalScope())
            {
                if(!file.Exists) { EditorGUILayout.LabelField("Last build",$"none yet ({Path.GetFileName(apk)})",EditorStyles.wordWrappedMiniLabel); return; }
                var record=BuildRunner.LoadRecord(apk);
                string details=$"{file.Name}, {file.Length/1e6f:0} MB, {Age(file.LastWriteTime)}"+(record!=null?$", took {(int)record.seconds/60}m {(int)record.seconds%60:00}s"+(record.development?"":", release"):"");
                EditorGUILayout.LabelField(new GUIContent("Last build",file.FullName),new GUIContent(details,file.FullName),EditorStyles.wordWrappedMiniLabel);
                if(GUILayout.Button("Show",EditorStyles.miniButton,GUILayout.Width(44))) EditorUtility.RevealInFinder(file.FullName);
            }
        }
        static void LogRow()
        {
            string log=Headsets.LogPath; if(log=="") return;
            bool live=Headsets.Logging;
            using(new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(new GUIContent(live?"Saving log":"Last log",log),new GUIContent(Path.GetFileName(log),log),EditorStyles.wordWrappedMiniLabel);
                if(GUILayout.Button("Open",EditorStyles.miniButton,GUILayout.Width(44)) && File.Exists(log)) EditorUtility.OpenWithDefaultApp(log);
                using(new EditorGUI.DisabledScope(!live))
                    if(GUILayout.Button("Stop",EditorStyles.miniButton,GUILayout.Width(44))) Headsets.StopLog();
            }
        }
        static string Age(DateTime time)
        {
            var age=DateTime.Now-time;
            return age.TotalMinutes<1?"just now":age.TotalHours<1?$"{age.TotalMinutes:0} min ago":time.Date==DateTime.Today?time.ToString("HH:mm"):time.ToString("MMM d, HH:mm");
        }
        static bool Toggle(bool value,string label,string tooltip)=>EditorGUILayout.ToggleLeft(new GUIContent(label,tooltip),value);
        // Actions block for minutes and may open dialogs, so they run after this GUI pass has finished.
        static void Later(Action action)=>EditorApplication.delayCall+=()=>action();
    }
}
